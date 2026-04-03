#to run this script: .\publish-docker-image.ps1 -version "1.1.0"

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

# Build and push multi-platform Docker image
Write-TimestampedOutput "Building and pushing multi-platform Docker image version $version (amd64 + arm64)..."
docker buildx build --platform linux/amd64,linux/arm64 . -t damiankrychowski/plikshare:$version -t damiankrychowski/plikshare:latest --build-arg "VERSION=$version" --push

# Check if the build was successful
if ($LASTEXITCODE -eq 0) {
    Write-TimestampedOutput "Docker image publishing complete."
} else {
    Write-TimestampedOutput "Docker image build failed. Please check the build output for errors."
}

Write-TimestampedOutput "Script execution completed."