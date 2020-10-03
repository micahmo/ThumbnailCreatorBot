using mdm.Extensions;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ThumbnailCreatorBot
{
    class Program
    {
        #region Main method

        public static void Main(string[] args)
        {
            _startDateTime = DateTimeOffset.UtcNow;

            string botTokenEnv = Environment.GetEnvironmentVariable("BOT_TOKEN");
            if (string.IsNullOrEmpty(botTokenEnv))
            {
                Console.WriteLine("Error retrieving Telegram bot token. Be sure to set BOT_TOKEN environment variable. " +
                                  "If running from Visual Studio, set the env vars in settings.env");
                return;
            }

            string chatIdEnv = Environment.GetEnvironmentVariable("CHAT_ID");
            if (string.IsNullOrEmpty(chatIdEnv))
            {
                Console.WriteLine("Error retrieving chat ID. Be sure to set CHAT_ID environment variable. " +
                                  "If running from Visual Studio, set the env vars in settings.env");
                return;
            }

            LoadFonts();
            LoggingUtilities.LogDir();
            LoggingUtilities.LogDir("fonts");
            LoggingUtilities.LogFonts();

            _botClient = new TelegramBotClient(botTokenEnv);
            _chatId = Convert.ToInt32(chatIdEnv);

            _botClient.OnMessage += Bot_OnMessage;
            _botClient.OnCallbackQuery += Bot_OnCallbackQuery;
            _botClient.StartReceiving();
            Thread.Sleep(int.MaxValue);
        }

        #endregion

        #region Event handlers

        private static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            ChatId chatId = e.Message.Chat.Id;
            string chatUsername = e.Message.Chat.Username;
            string messageText = e.Message.Text;
            string caption = e.Message.Caption;
            PhotoSize[] messagePhoto = e.Message.Photo;
            ThumbnailData data = GetData(chatId);

            try
            {
                // Handle case where user experienced exception and said 'y' to reporting it.
                if (_lastException.IsNotNull())
                {
                    if (messageText?.Trim().ToLower() == "y")
                    {
                        // Send the exception to micahmo
                        await _botClient.SendTextMessageAsync(
                            chatId: _chatId,
                            text: $"Error reported by user '{chatUsername}'.\n\n" +
                                  $"<code>{_lastException}</code>",
                            parseMode: ParseMode.Html
                        );

                        // Tell the user that their error was reported
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ErrorReported,
                            parseMode: ParseMode.Html
                        );
                    }

                    _lastException = null;
                }

                else if (messageText == "/status" && chatId.Identifier == _chatId.Identifier)
                {
                    var startTimeInEt = TimeZoneInfo.ConvertTime(_startDateTime, GetEasternTimeZone());

                    await _botClient.SendTextMessageAsync(
                        chatId: _chatId,
                        text: $"Status is good. Running on host '{Environment.MachineName}' (platform is {GetOsPlatform()}). Uptime is {DateTimeOffset.UtcNow - _startDateTime} " +
                              $"(since {startTimeInEt})",
                        parseMode: ParseMode.Html
                    );
                }

                // Else the user sent us a photo. Save it and see if there are any parameters.
                else if (messagePhoto?.LastOrDefault() is { } photoSize)
                {
                    // Get the API path of the photo from Telegram
                    var photoPath = await _botClient.GetFileAsync(photoSize.FileId);

                    // Create a file stream so that we can download the photo
                    string filePath = $"{photoSize.FileId}.png";
                    await using (FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate))
                    {

                        // Clear the contents, in case there was an existing file with this name (unlikely, but possible)
                        fileStream.SetLength(0);

                        // Download the photo from Telegram API to our file stream
                        await _botClient.DownloadFileAsync(photoPath.FilePath, fileStream);

                        // Save the path to this user's data
                        data.ImagePath = filePath;
                    }

                    // Check if the user sent JSON configuration in the caption
                    bool gotConfig = false;
                    if (caption.IsNotNullOrEmpty())
                    {
                        try
                        {
                            data.TextData = JsonConvert.DeserializeObject<TextData>(caption);

                            // If we successfully deserialized the text, we can proceed with the next step as though they have just added text
                            data.SetText();
                            await SendThumbnail(chatId, Resources.WhatNow, _inlineResponse);
                            data.State = ThumbnailState.Ongoing;

                            gotConfig = true;
                        }
                        catch
                        {
                            await _botClient.SendTextMessageAsync(
                                chatId: chatId,
                                text: Resources.InvalidConfiguration
                            );
                        }
                    }
                    
                    if (!gotConfig)
                    {
                        // Tell the user that we got their image, now send us the text to overlay
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ReceivedImage,
                            replyMarkup: _inlineResponse
                        );
                    }

                    // In any case, the state will go to Ongoing after this step
                    data.State = ThumbnailState.Ongoing;
                }
                
                else if (data.State == ThumbnailState.AwaitingText)
                {
                    data.TextData.Text = messageText;
                    data.SetText();
                    await SendThumbnail(chatId, Resources.WhatNow, _inlineResponse);
                    data.State = ThumbnailState.Ongoing;
                }

                else if (data.State == ThumbnailState.AwaitingTextSize)
                {
                    if (float.TryParse(messageText, out float textSize))
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );

                        data.TextData.TextSize = textSize;
                        data.State = ThumbnailState.Ongoing;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.InvalidSize
                        );
                    }
                }

                else if (data.State == ThumbnailState.AwaitingTextColor)
                {
                    if (Color.TryParseHex(messageText, out Color textColor))
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );

                        data.TextData.TextColor = textColor.ToHex();
                        data.State = ThumbnailState.Ongoing;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.InvalidColor
                        );
                    }
                }
                else if (data.State == ThumbnailState.AwaitingBorderColor)
                {
                    if (Color.TryParseHex(messageText, out Color borderColor))
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );

                        data.TextData.BorderColor = borderColor.ToHex();
                        data.State = ThumbnailState.Ongoing;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.InvalidColor
                        );
                    }
                }
                else if (data.State == ThumbnailState.AwaitingBorderThickness)
                {
                    if (float.TryParse(messageText, out float borderThickness))
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );

                        data.TextData.BorderThickness = borderThickness;
                        data.State = ThumbnailState.Ongoing;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.InvalidSize
                        );
                    }
                }
                else if (data.State == ThumbnailState.AwaitingHorizontalPadding)
                {
                    if (int.TryParse(messageText, out int horizontalPadding))
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );

                        data.TextData.HorizontalPadding = horizontalPadding;
                        data.State = ThumbnailState.Ongoing;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.InvalidPadding
                        );
                    }
                }
                else if (data.State == ThumbnailState.AwaitingVerticalPadding)
                {
                    if (int.TryParse(messageText, out int verticalPadding))
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );

                        data.TextData.VerticalPadding = verticalPadding;
                        data.State = ThumbnailState.Ongoing;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.InvalidPadding
                        );
                    }
                }

            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: Resources.ErrorEncountered
                );

                _lastException = ex;
            }
        }

        private static async void Bot_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            try
            {
                var chatId = e.CallbackQuery.From.Id;
                ThumbnailData data = GetData(chatId);
                string inlineQueryResult = e.CallbackQuery.Data;

                if (data.State == ThumbnailState.Ongoing)
                {
                    if (inlineQueryResult == _cancelKeyboardButton.CallbackData)
                    {
                        ClearData(chatId);

                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.Canceled
                        );
                    }
                    else if (inlineQueryResult == _chooseTextSizeKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.EnterTextSize
                        );

                        data.State = ThumbnailState.AwaitingTextSize;
                    }
                    else if (inlineQueryResult == _chooseFontKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SelectAFont,
                            replyMarkup: new InlineKeyboardMarkup(
                                Utilities.Fonts.Families.Select(x => new InlineKeyboardButton {Text = x.Name, CallbackData = x.Name}).OrderBy(x => x.Text).ToList().To2DList(3)
                            )
                        );

                        data.State = ThumbnailState.AwaitingFont;
                    }
                    else if (inlineQueryResult == _chooseTextStyleKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SelectATextStyle,
                            replyMarkup: new InlineKeyboardMarkup(
                                Enum.GetValues(typeof(FontStyle)).OfType<FontStyle>().Select(style => new InlineKeyboardButton {Text = style.ToString(), CallbackData = style.ToString()}).To2DList(2)
                            )
                        );

                        data.State = ThumbnailState.AwaitingTextStyle;
                    }
                    else if (inlineQueryResult == _chooseHorizontalAlignmentKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SelectHorizontalAlignment,
                            replyMarkup: new InlineKeyboardMarkup(
                                Enum.GetValues(typeof(HorizontalAlignment)).OfType<HorizontalAlignment>().Select(alnmt => new InlineKeyboardButton {Text = alnmt.ToString(), CallbackData = alnmt.ToString()})
                            )
                        );

                        data.State = ThumbnailState.AwaitingHorizontalAlignment;
                    }
                    else if (inlineQueryResult == _chooseVerticalAlignmentKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SelectVerticalAlignment,
                            replyMarkup: new InlineKeyboardMarkup(
                                Enum.GetValues(typeof(VerticalAlignment)).OfType<VerticalAlignment>().Select(alnmt => new InlineKeyboardButton { Text = alnmt.ToString(), CallbackData = alnmt.ToString() })
                            )
                        );

                        data.State = ThumbnailState.AwaitingVerticalAlignment;
                    }
                    else if (inlineQueryResult == _setHorizontalPaddingKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SetHorizontalPadding
                        );

                        data.State = ThumbnailState.AwaitingHorizontalPadding;
                    }
                    else if (inlineQueryResult == _setVerticalPaddingKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SetVerticalPadding
                        );

                        data.State = ThumbnailState.AwaitingVerticalPadding;
                    }
                    else if (inlineQueryResult == _addTextKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.ChooseOptionsOrAddText,
                            replyMarkup: _formattingOptionsResponse
                        );
                    }
                    else if (inlineQueryResult == _chooseTextColorKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.EnterHtmlColor
                        );

                        data.State = ThumbnailState.AwaitingTextColor;
                    }
                    else if (inlineQueryResult == _chooseBorderColorKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.EnterHtmlColor
                        );

                        data.State = ThumbnailState.AwaitingBorderColor;
                    }
                    else if (inlineQueryResult == _setBorderThicknessKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.SetBorderThickness
                        );

                        data.State = ThumbnailState.AwaitingBorderThickness;
                    }
                    else if (inlineQueryResult == _setTextKeyboardButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: Resources.WhatTextToAdd
                        );

                        data.State = ThumbnailState.AwaitingText;
                    }
                    else if (inlineQueryResult == _doneKeyboardButton.CallbackData)
                    {
                        await SendThumbnail(chatId, Resources.FinishedThumbnail);
                        ClearData(chatId);
                    }
                    else if (inlineQueryResult == _exportConfigurationButton.CallbackData)
                    {
                        await _botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"{Resources.ConfigurationOutput}{Environment.NewLine}{Environment.NewLine}<code>{data.TextDataConfig}</code>",
                            replyMarkup: _inlineResponse,
                            parseMode: ParseMode.Html
                        );
                    }
                }
                else if (data.State == ThumbnailState.AwaitingFont)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: Resources.ChooseOptionsOrAddText,
                        replyMarkup: _formattingOptionsResponse
                    );

                    data.TextData.Font = inlineQueryResult;
                    data.State = ThumbnailState.Ongoing;
                }
                else if (data.State == ThumbnailState.AwaitingTextStyle)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: Resources.ChooseOptionsOrAddText,
                        replyMarkup: _formattingOptionsResponse
                    );

                    data.TextData.TextStyle = (FontStyle)Enum.Parse(typeof(FontStyle), inlineQueryResult);
                    data.State = ThumbnailState.Ongoing;
                }
                else if (data.State == ThumbnailState.AwaitingHorizontalAlignment)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: Resources.ChooseOptionsOrAddText,
                        replyMarkup: _formattingOptionsResponse
                    );

                    data.TextData.HorizontalAlignment = (HorizontalAlignment)Enum.Parse(typeof(HorizontalAlignment), inlineQueryResult);
                    data.State = ThumbnailState.Ongoing;
                }
                else if (data.State == ThumbnailState.AwaitingVerticalAlignment)
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: Resources.ChooseOptionsOrAddText,
                        replyMarkup: _formattingOptionsResponse
                    );

                    data.TextData.VerticalAlignment = (VerticalAlignment)Enum.Parse(typeof(VerticalAlignment), inlineQueryResult);
                    data.State = ThumbnailState.Ongoing;
                }

                // Have to tell Telegram that we answered the callback, so that the wait cursor / progress bar will go away.
                await _botClient.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
            }
            catch
            {
                // Ignored
            }
        }

        #endregion

        #region Private methods
         
        private static void LoadFonts()
        {
            FontCollection fonts = new FontCollection();

            try
            {
                foreach (var font in Directory.EnumerateFiles(Path.Combine(Directory.GetCurrentDirectory(), "fonts"))
                    .Where(file => Path.GetExtension(file).EndsWith("ttf") || Path.GetExtension(file).EndsWith("otf")))
                {
                    try
                    {
                        fonts.Install(font);
                    }
                    catch
                    {
                        /* Don't let a font load failure kill us.*/
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading fonts: {ex}");
            }

            Utilities.Fonts = fonts;
        }

        private static async Task SendThumbnail(ChatId chatId, string message, IReplyMarkup replyMarkup = null)
        {
            var thumbnailPath = GetData(chatId).GenerateThumbnail();

            await using Stream thumbnailStream = System.IO.File.OpenRead(thumbnailPath);

            await _botClient.SendPhotoAsync(
                chatId: chatId,
                photo: thumbnailStream,
                caption: message,
                replyMarkup: replyMarkup
            );
        }

        private static ThumbnailData GetData(ChatId chatId)
        {
            ThumbnailData result = null;

            if (_data.TryGetValue(chatId.Identifier, out var data))
            {
                result = data;
            }

            return result ?? (_data[chatId.Identifier] = new ThumbnailData { ChatId = chatId });
        }

        private static void ClearData(ChatId chatId)
        {
            if (_data.TryGetValue(chatId.Identifier, out var data))
            {
                _data.Remove(chatId.Identifier);
                data.State = ThumbnailState.Done;
                data.Dispose();
            }
        }

        private static TimeZoneInfo GetEasternTimeZone()
        {
            if (TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(tz => tz.Id == "America/New_York") is TimeZoneInfo linuxTz)
            {
                return linuxTz;
            }
            else if (TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(tz => tz.Id == "Eastern Standard Time") is TimeZoneInfo windowsTz)
            {
                return windowsTz;
            }
            else return null;
        }

        private static string GetOsPlatform()
        {
            return (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows.ToString() : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux.ToString() : "Unknown");
        }

        #endregion

        #region Private static members

        private static DateTimeOffset _startDateTime;

        private static ITelegramBotClient _botClient;
        private static Exception _lastException;
        private static readonly Dictionary<long, ThumbnailData> _data = new Dictionary<long, ThumbnailData>();
        private static ChatId _chatId;

        private static readonly InlineKeyboardButton _addTextKeyboardButton = new InlineKeyboardButton {Text = Resources.AddText, CallbackData = Resources.AddText};
        private static readonly InlineKeyboardButton _cancelKeyboardButton = new InlineKeyboardButton {Text = Resources.Cancel, CallbackData = Resources.Cancel};
        private static readonly InlineKeyboardButton _doneKeyboardButton = new InlineKeyboardButton {Text = Resources.Done, CallbackData = Resources.Done};
        private static readonly InlineKeyboardButton _exportConfigurationButton = new InlineKeyboardButton { Text = Resources.ExportConfiguration, CallbackData = Resources.ExportConfiguration };
        private static readonly InlineKeyboardMarkup _inlineResponse = new InlineKeyboardMarkup(new List<InlineKeyboardButton>
        {
            _addTextKeyboardButton, _cancelKeyboardButton, _doneKeyboardButton, _exportConfigurationButton
        }.To2DList(3));

        private static readonly InlineKeyboardButton _chooseFontKeyboardButton = new InlineKeyboardButton {Text = Resources.ChooseFont, CallbackData = Resources.ChooseFont};
        private static readonly InlineKeyboardButton _chooseTextStyleKeyboardButton = new InlineKeyboardButton { Text = Resources.ChooseTextStyle, CallbackData = Resources.ChooseTextStyle };
        private static readonly InlineKeyboardButton _chooseTextSizeKeyboardButton = new InlineKeyboardButton { Text = Resources.ChooseTextSize, CallbackData = Resources.ChooseTextSize };
        private static readonly InlineKeyboardButton _chooseHorizontalAlignmentKeyboardButton = new InlineKeyboardButton { Text = Resources.ChooseHorizontalAlignment, CallbackData = Resources.ChooseHorizontalAlignment };
        private static readonly InlineKeyboardButton _chooseVerticalAlignmentKeyboardButton = new InlineKeyboardButton { Text = Resources.ChooseVerticalAlignment, CallbackData = Resources.ChooseVerticalAlignment };
        private static readonly InlineKeyboardButton _setHorizontalPaddingKeyboardButton = new InlineKeyboardButton { Text = Resources.SetHorizontalPadding, CallbackData = Resources.SetHorizontalPadding };
        private static readonly InlineKeyboardButton _setVerticalPaddingKeyboardButton = new InlineKeyboardButton { Text = Resources.SetVerticalPadding, CallbackData = Resources.SetVerticalPadding };
        private static readonly InlineKeyboardButton _chooseTextColorKeyboardButton = new InlineKeyboardButton { Text = Resources.ChooseTextColor, CallbackData = Resources.ChooseTextColor };
        private static readonly InlineKeyboardButton _chooseBorderColorKeyboardButton = new InlineKeyboardButton { Text = Resources.ChooseBorderColor, CallbackData = Resources.ChooseBorderColor };
        private static readonly InlineKeyboardButton _setBorderThicknessKeyboardButton = new InlineKeyboardButton { Text = Resources.SetBorderThickness, CallbackData = Resources.SetBorderThickness };
        private static readonly InlineKeyboardButton _setTextKeyboardButton = new InlineKeyboardButton { Text = Resources.SetText, CallbackData = Resources.SetText };

        private static readonly InlineKeyboardMarkup _formattingOptionsResponse = new InlineKeyboardMarkup(new List<InlineKeyboardButton>
        {
            _chooseFontKeyboardButton, _chooseTextStyleKeyboardButton, _chooseTextSizeKeyboardButton, _chooseHorizontalAlignmentKeyboardButton, _chooseVerticalAlignmentKeyboardButton,
            _setHorizontalPaddingKeyboardButton, _setVerticalPaddingKeyboardButton, _chooseTextColorKeyboardButton, _chooseBorderColorKeyboardButton,
            _setBorderThicknessKeyboardButton, _setTextKeyboardButton, _cancelKeyboardButton
        }.To2DList(1));

        #endregion
    }
}
