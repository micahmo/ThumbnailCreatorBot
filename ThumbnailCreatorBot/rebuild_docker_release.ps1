# Rebuild/publish the code
dotnet publish -c Release

# Stop/delete the existing container, then delete the existing image
docker stop thumbnailcreatorbot-release-container
docker rm thumbnailcreatorbot-release-container
docker rmi thumbnailcreatorbot-release

# Rebuild the image, recreate the container, and start the container
docker build -t thumbnailcreatorbot-release -f Dockerfile_release .
docker create --restart always --name thumbnailcreatorbot-release-container thumbnailcreatorbot-release
docker start thumbnailcreatorbot-release-container