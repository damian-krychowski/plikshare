#to run this script: .\build-docker-image.ps1 -version "1.1.0"

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

# Build the Docker image
$buildCommand = "docker build --platform linux/amd64 . -t damiankrychowski/plikshare:$version -t damiankrychowski/plikshare:latest --build-arg `"VERSION=$version`""
Write-TimestampedOutput "Building Docker image version $version..."
Invoke-Expression $buildCommand

# Check if the build was successful
if ($LASTEXITCODE -eq 0) {
    Write-TimestampedOutput "Docker image built successfully."
} else {
    Write-TimestampedOutput "Docker image build failed. Please check the build output for errors."
}

Write-TimestampedOutput "Script execution completed."