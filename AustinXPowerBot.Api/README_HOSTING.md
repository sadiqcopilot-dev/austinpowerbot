Hosting the Notifications API

Run locally (development):

```powershell
cd AustinXPowerBot.Api
dotnet run --project AustinXPowerBot.Api.csproj
```

By default the Kestrel server will listen on the URLs configured in `launchSettings.json` or the environment. To expose the API publicly in production:
- Use a reverse proxy (NGINX, IIS, Caddy) with HTTPS termination.
- Open the chosen port(s) in your firewall and allow the service.
- Use a managed certificate (Let's Encrypt) or an enterprise CA for TLS.

Security notes:
- Add authentication (API key, JWT) before accepting admin pushes from the public internet.
- Store notifications in a persistent DB (SQL/NoSQL) for scale and durability.
