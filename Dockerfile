#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["ThumbnailCreatorBot/ThumbnailCreatorBot.csproj", "ThumbnailCreatorBot/"]
RUN dotnet restore "ThumbnailCreatorBot/ThumbnailCreatorBot.csproj"
COPY . .
WORKDIR "/src/ThumbnailCreatorBot"
RUN dotnet build "ThumbnailCreatorBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ThumbnailCreatorBot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

WORKDIR /app/fonts
COPY ThumbnailCreatorBot/fonts/* ./

WORKDIR /app

ENTRYPOINT ["dotnet", "ThumbnailCreatorBot.dll"]