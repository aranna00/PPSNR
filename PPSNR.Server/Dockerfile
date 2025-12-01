FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5271
EXPOSE 7155

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PPSNR.Server/PPSNR.Server.csproj", "PPSNR.Server/"]
RUN dotnet restore "PPSNR.Server/PPSNR.Server.csproj"
COPY . .
WORKDIR "/src/PPSNR.Server"
RUN dotnet build "./PPSNR.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PPSNR.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy entrypoint that reads Docker secrets into environment variables
# Set execute permissions at copy time to avoid needing chmod under non-root USER
COPY --chmod=0755 PPSNR.Server/docker-entrypoint.sh /app/docker-entrypoint.sh

# Use entrypoint wrapper so secrets (mounted at /run/secrets/*) are exported as env vars
ENTRYPOINT ["/app/docker-entrypoint.sh"]
CMD ["dotnet", "PPSNR.Server.dll"]
