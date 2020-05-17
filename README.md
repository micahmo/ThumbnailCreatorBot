# ThumbnailCreatorBot

A [Telegram bot](https://core.telegram.org/bots) written using the C# [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) client.

Allows the user to send a thumbnail image to the bot, which can be edited to add text of various fonts, sizes, colors, alignments, etc.

Runs in Linux Docker container.

![](https://i.imgur.com/IkM4xXB.png)

![](https://i.imgur.com/Ge36EGH.png)

![](https://i.imgur.com/Lw8ZJI6.png)

![](https://i.imgur.com/LucBCOR.png)

# Testing
To test, simply run in Docker from Visual Studio. To "deploy", run the [rebuild_docker_release.ps1](https://github.com/micahmo/ThumbnailCreatorBot/blob/master/ThumbnailCreatorBot/rebuild_docker_release.ps1) PowerShell script. This creates a permanent image and container which can continue to run even after Visual Studio is disconnected.
