# ThumbnailCreatorBot

A [Telegram bot](https://core.telegram.org/bots) written using the C# [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) client.

Allows the user to send a thumbnail image to the bot, which can be edited to add text of various fonts, sizes, colors, alignments, etc.

Runs in Linux Docker container.

![](https://i.imgur.com/IkM4xXB.png)

![](https://i.imgur.com/Ge36EGH.png)

![](https://i.imgur.com/Lw8ZJI6.png)

![](https://i.imgur.com/LucBCOR.png)

# Building and Running

## Development
With Visual Studio and Docker Desktop installed, clone the repository and open in Visual Studio. Run using the "Docker" option in Visual Studio. It will automatically create and run a Docker container which can be inspected and debugged from Visual Studio.

To set environment variables, rename (or copy) the `settings.env.sample` file to `settings.env` and enter the desired values.

## Deployment
To run outside of a development environment, use `docker run` to pull and set up the image from Docker Hub.
```
docker run -d \
  --name=ThumbnailCreatorBot \
  --net=host \
  -e BOT_TOKEN=<your_bot_token> \
  -e CHAT_ID=<your_chat_ID_optional> \
  ghcr.io/micahmo/thumbnailcreatorbot
```

## Unraid
Use the Unraid container template to easily configure and run on an Unraid server.

- In Unraid, go to the Docker tab.
- Scroll to the bottom and edit the "Template Repositories" area.
- Add `https://github.com/micahmo/docker-templates` on a new line and press Save.
- Choose Add Container.
- In the Template drop down, choose `ThumbnailCreatorBot` from the list.
- Set variables as desired and Apply.
