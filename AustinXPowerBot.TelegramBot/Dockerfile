# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj(s) and restore
COPY ["AustinXPowerBot.TelegramBot/AustinXPowerBot.TelegramBot.csproj","AustinXPowerBot.TelegramBot/"]
COPY ["AustinXPowerBot.Shared/AustinXPowerBot.Shared.csproj","AustinXPowerBot.Shared/"]
RUN dotnet restore "AustinXPowerBot.TelegramBot/AustinXPowerBot.TelegramBot.csproj"

# copy the rest and publish
COPY . .
WORKDIR /src/AustinXPowerBot.TelegramBot
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AustinXPowerBot.TelegramBot.dll"]
