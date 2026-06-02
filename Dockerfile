# build-frontend-prod stage
FROM node:22-alpine AS build-frontend-prod
WORKDIR /usr/src/app
COPY ./Frontend/package*.json ./
RUN npm ci
COPY ./Frontend .
RUN npm run build-prod && npm run build-elements-prod && npm cache clean --force

# build backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-backend-prod
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PlikShare/PlikShare.csproj", "PlikShare/"]
RUN dotnet restore "PlikShare/PlikShare.csproj"
COPY . .
WORKDIR "/src/PlikShare"
RUN rm -rf wwwroot/* && \
    dotnet build "PlikShare.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build-backend-prod AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PlikShare.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Slim variant (default): no ffmpeg — thumbnail auto-generation stays disabled at runtime
# (FfmpegService probes at startup and the feature gracefully no-ops when the binary is absent).
# Copy frontend build artifacts to wwwroot directory.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build-frontend-prod /usr/src/app/dist /app/wwwroot

# Create a non-root user
RUN useradd -u 5678 --no-create-home --shell /bin/false appuser && chown -R appuser /app
USER appuser
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "PlikShare.dll"]

# ffmpeg variant: identical to `final` plus a pinned, multi-arch static ffmpeg/ffprobe dropped
# onto PATH. /usr/local/bin is on PATH, so FfmpegService resolves the literal "ffmpeg" without any
# Ffmpeg__Path config and enables thumbnail generation. mwader/static-ffmpeg ships amd64+arm64, so
# COPY --from picks the right binary under buildx multi-platform builds. Build with
# `--target final-ffmpeg`.
FROM final AS final-ffmpeg
USER root
COPY --from=mwader/static-ffmpeg:8.1.1 /ffmpeg /ffprobe /usr/local/bin/
USER appuser