# Set the Docker host to the first command-line argument (if any)
# Example usage: .\rebuild_docker_release.ps1 ssh://user@server
$env:docker_host = $args[0]

# Rebuild/publish the code
dotnet publish -c Release

# Stop/delete the existing container, then delete the existing image
docker stop thumbnailcreatorbot-release-container
docker rm thumbnailcreatorbot-release-container
docker rmi thumbnailcreatorbot-release

# Rebuild the image, recreate the container, and start the container
docker build -t thumbnailcreatorbot-release -f Dockerfile_release .

# Use Docker run to create + start the container
# -d: Detached mode (so that we're not stuck with stdout in tty)
docker run -d --restart always --name thumbnailcreatorbot-release-container thumbnailcreatorbot-release

$env:docker_host = ""