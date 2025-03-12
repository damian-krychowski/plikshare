# build-frontend-prod stage
FROM node:18-alpine AS build-frontend-prod
WORKDIR /usr/src/app
COPY ./Frontend/package*.json ./
RUN npm ci
COPY ./Frontend .
RUN npm run build-prod && npm cache clean --force

# build backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-backend-prod
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

# Copy frontend build artifacts to wwwroot directory
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build-frontend-prod /usr/src/app/dist /app/wwwroot

# Create a non-root user
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "PlikShare.dll"]