#to run this script: .\publish-docker-image.ps1 -version "1.1.37"
#
# Publishes two image variants from the same Dockerfile:
#   slim   (default)        -> :$version        + :latest          (no ffmpeg)
#   ffmpeg (--target final-ffmpeg) -> :$version-ffmpeg + :latest-ffmpeg (bundled static ffmpeg)
# `latest` intentionally tracks the slim variant — switch to the `-ffmpeg` tag to get thumbnails.

param (
    [Parameter(Mandatory=$true)]
    [string]$version
)

# Function to write output with timestamps
function Write-TimestampedOutput {
    param([string]$message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Output "[$timestamp] $message"
}

# Build and push the slim variant (no ffmpeg)
Write-TimestampedOutput "Building and pushing slim image version $version (amd64 + arm64)..."
docker buildx build --platform linux/amd64,linux/arm64 . `
    --target final `
    -t damiankrychowski/plikshare:$version `
    -t damiankrychowski/plikshare:latest `
    --build-arg "VERSION=$version" `
    --push

if ($LASTEXITCODE -ne 0) {
    Write-TimestampedOutput "Slim image build failed. Please check the build output for errors."
    exit 1
}

# Build and push the ffmpeg variant (bundled static ffmpeg)
Write-TimestampedOutput "Building and pushing ffmpeg image version $version-ffmpeg (amd64 + arm64)..."
docker buildx build --platform linux/amd64,linux/arm64 . `
    --target final-ffmpeg `
    -t damiankrychowski/plikshare:$version-ffmpeg `
    -t damiankrychowski/plikshare:latest-ffmpeg `
    --build-arg "VERSION=$version" `
    --push

if ($LASTEXITCODE -eq 0) {
    Write-TimestampedOutput "Docker image publishing complete."
} else {
    Write-TimestampedOutput "Ffmpeg image build failed. Please check the build output for errors."
    exit 1
}

Write-TimestampedOutput "Script execution completed."
