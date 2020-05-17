using mdm.Extensions;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using Telegram.Bot.Types;
using File = System.IO.File;
using Path = System.IO.Path;

namespace ThumbnailCreatorBot
{
    /// <summary>
    /// Describes a set of data associated with a particular user
    /// </summary>
    public class ThumbnailData : IDisposable
    {
        #region Constructor

        public ThumbnailData() => State = ThumbnailState.New;

        #endregion

        #region Public methods

        public void SetText()
        {
            var font = Utilities.Fonts.CreateFont(TextData.Font, TextData.TextSize, TextData.TextStyle);
            var graphicsOptions = new TextGraphicsOptions {TextOptions = new TextOptions {HorizontalAlignment = TextData.HorizontalAlignment, VerticalAlignment = TextData.VerticalAlignment}};

            float x = TextData.HorizontalAlignment switch
            {
                HorizontalAlignment.Left => 0 + TextData.HorizontalPadding,
                HorizontalAlignment.Center => ModifiedImage.Width / 2,
                HorizontalAlignment.Right => ModifiedImage.Width - TextData.HorizontalPadding,
                _ => 0
            };

            float y = TextData.VerticalAlignment switch
            {
                VerticalAlignment.Top => 0 + TextData.VerticalPadding,
                VerticalAlignment.Center => ModifiedImage.Height / 2,
                VerticalAlignment.Bottom => ModifiedImage.Height - TextData.VerticalPadding,
                _ => 0
            };

            // Put the text on the image
            ModifiedImage.Mutate(action =>
            {
                if (TextData.BorderColor.IsNotNullOrEmpty())
                {
                    action.DrawText(graphicsOptions, TextData.Text, font, Brushes.Solid(Color.ParseHex(TextData.TextColor)), new Pen(Color.ParseHex(TextData.BorderColor), TextData.BorderThickness), new PointF(x, y));
                }
                else
                {
                    action.DrawText(graphicsOptions, TextData.Text, font, Color.ParseHex(TextData.TextColor), new PointF(x, y));
                }
            });

            TextDataConfig = JsonConvert.SerializeObject(TextData);
            TextData = null;
        }

        public string GenerateThumbnail()
        {
            ModifiedImage.Save(ThumbnailPath);
            return Path.GetFullPath(ThumbnailPath);
        }

        #endregion

        #region Public properties

        public string TextDataConfig { get; set; }

        public TextData TextData
        {
            get
            {
                if (_textData.IsNull())
                {
                    _textData = new TextData {Font = "Arial", Text = string.Empty};
                }

                return _textData;
            }
            set => _textData = value;
        }

        private TextData _textData;

        public ThumbnailState State { get; set; }

        public ChatId ChatId { get; set; }

        public Image Image
        {
            get
            {
                if (_image.IsNull() && ImagePath.IsNotNullOrEmpty())
                {
                    _image = Image.Load(ImagePath);
                }

                return _image;
            }
        }
        private Image _image;

        public string ImagePath { get; set; }

        #endregion

        #region Private properties

        private string ThumbnailPath => $"{Path.GetFileNameWithoutExtension(ImagePath)}-processed.png";

        private Image ModifiedImage {
            get
            {
                if (_modifiedImage.IsNull())
                {
                    _modifiedImage = Image.CloneAs<Rgba32>();
                }

                return _modifiedImage;
            }
            set => _modifiedImage = value;
        }
        private Image _modifiedImage;

        #endregion

        #region IDisposable members

        public void Dispose()
        {
            _image?.Dispose();

            try
            {
                File.Delete(ImagePath);
                File.Delete(ThumbnailPath);
            }
            catch
            {
                // Ignore
            }
        }

        #endregion
    }

    public enum ThumbnailState
    {
        New,
        Ongoing,
        AwaitingText,
        AwaitingTextStyle,
        AwaitingFont,
        AwaitingTextSize,
        AwaitingHorizontalAlignment,
        AwaitingVerticalAlignment,
        AwaitingHorizontalPadding,
        AwaitingVerticalPadding,
        AwaitingTextColor,
        AwaitingBorderColor,
        AwaitingBorderThickness,
        Done
    }

    public class TextData
    {
        public string Text { get; set; }
        public float TextSize { get; set; } = 100f;
        public FontStyle TextStyle { get; set; } = FontStyle.Regular; 
        public float BorderThickness { get; set; } = 3f;
        public string Font { get; set; }
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
        public int HorizontalPadding { get; set; } = 75;
        public int VerticalPadding { get; set; } = 75;
        public string TextColor { get; set; } = Color.Gray.ToHex();
        public string BorderColor { get; set; } = null;
    }
}
