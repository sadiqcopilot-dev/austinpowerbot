This file documents a minimal Render deployment for Austin X PowerBot.

Overview
- API: Render Web Service (Docker). Exposes HTTP + WebSockets (SignalR).
- Telegram bot: Render Background Worker (Docker) running long-polling.
- Database: Render Managed PostgreSQL (or external DB).

Quick steps (GitHub → Render)
1. Push this repository to GitHub.
2. In Render, create a new "Web Service" from the repo:
   - Environment: Docker
   - Dockerfile path: `AustinXPowerBot.Api/Dockerfile`
   - Build Command: default
   - Start Command: default (uses Dockerfile ENTRYPOINT)
3. Create a new "Background Worker" in Render:
   - Environment: Docker
   - Dockerfile path: `AustinXPowerBot.TelegramBot/Dockerfile`
4. Create a new PostgreSQL database in Render (managed) and note the connection string.
5. Add Environment Variables / Secrets to both services (Render dashboard):
   - `ConnectionStrings__Default` (Postgres connection string)
   - `Jwt__Key` (strong secret)
   - `Jwt__Issuer` = `AustinXPowerBot.Api`
   - `Jwt__Audience` = `AustinXPowerBot.Clients`
   - `TELEGRAM_BOT_TOKEN` (bot token)
   - `ADMIN_BOT_TOKEN` (optional)
   - `API_BASE_URL` (https://<your-api>.onrender.com/)

Migrations
- Program.cs runs `dbContext.Database.Migrate()` automatically when a connection string is present. Ensure the service has permission to run migrations.

Local testing with Docker Compose
- To test locally, set `TELEGRAM_BOT_TOKEN` in your environment and run:

```bash
docker compose up --build
```

Notes & recommendations
- Replace placeholder secrets in `appsettings.json` and do not commit secrets.
- Render provides TLS for `*.onrender.com` domains. Use `API_BASE_URL` with `https` when running the Telegram bot in production.
- SignalR requires WebSocket support — Render Web Services support WebSockets by default.
- For scale, consider switching the Telegram bot to webhook mode and expose a secured webhook endpoint on the API.
 - For scale, consider switching the Telegram bot to webhook mode and expose a secured webhook endpoint on the API.
 - For scale, use webhook mode (API receives Telegram updates) and remove long-polling worker.

Setting webhook (optional)
- After deploying the API, set the webhook URL so Telegram will POST updates to your API:

   - Use the controller endpoint: `POST https://<your-api>/api/telegram/setwebhook?url=https://<your-api>/api/telegram/webhook`
   - Or run the helper in `AustinXPowerBot.TelegramBot` (it sets the webhook and exits). Configure env vars for that service and let it run once.

Environment variables used by webhook controller / helper
- `TELEGRAM_BOT_TOKEN` - Bot token used to send notifications to admin
- `ADMIN_TELEGRAM_CHAT_ID` - Numeric chat id for the admin to receive forwarded client messages
- `WEBHOOK_URL` or `API_BASE_URL` + `WEBHOOK_PATH` - webhook target

Script to create services via Render API
- See `scripts/render_create_services.sh` for example curl payloads to create the Postgres DB, API web service, and Telegram worker. Edit the script with your repo URL and run locally with `RENDER_API_KEY` set.

If you want, I can also:
- Add GitHub Actions to build and push Docker images and auto-deploy to Render.
- Switch the bot to webhook mode and add a small webhook controller in the API.
