# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj(s) and restore
COPY ["AustinXPowerBot.Api/AustinXPowerBot.Api.csproj","AustinXPowerBot.Api/"]
COPY ["AustinXPowerBot.Shared/AustinXPowerBot.Shared.csproj","AustinXPowerBot.Shared/"]
RUN dotnet restore "AustinXPowerBot.Api/AustinXPowerBot.Api.csproj"

# copy the rest and publish
COPY . .
WORKDIR /src/AustinXPowerBot.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "AustinXPowerBot.Api.dll"]
