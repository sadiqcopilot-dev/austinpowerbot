Deployment to Render — step-by-step

Prerequisites
- Repository pushed to GitHub (branch `main`).
- Render account and API key (create in Render dashboard).
- Telegram bot token ready.

1) Push code to GitHub

```bash
git add .
git commit -m "Add render/docker/CI and webhook controller"
git push origin main
```

2) GitHub repository secrets (Settings → Secrets → Actions)
- `RENDER_API_KEY` = your Render API key
- `RENDER_SERVICE_API_ID` = (leave empty for now if using `render.yaml`, or set after creating service in dashboard)
- `RENDER_SERVICE_TELEGRAM_ID` = (same as above)
- Optional: other secrets you plan to use

3) Create services on Render (recommended: use `render.yaml`)
- In Render: click New + Import from GitHub → choose your repo → Render will detect `render.yaml`.
- If Render prompts, choose the plans you want for:
  - AustinXPowerBot-API (Web Service) — Docker
  - AustinXPowerBot-Telegram (Background Worker) — Docker
  - austinxpowerbot-db (Postgres) — Managed DB

4) If you prefer manual creation, create services like:
- Web Service (API)
  - Environment: Docker
  - Dockerfile path: `AustinXPowerBot.Api/Dockerfile`
  - Start command: default (Docker ENTRYPOINT)
- Background Worker (Telegram bot)
  - Environment: Docker
  - Dockerfile path: `AustinXPowerBot.TelegramBot/Dockerfile`
- Managed Postgres: create and note connection string

5) Set environment variables in Render (Service → Environment)
- For API service:
  - `ConnectionStrings__Default` = Render Postgres connection string
  - `Jwt__Key` = (strong secret)
  - `Jwt__Issuer` = AustinXPowerBot.Api
  - `Jwt__Audience` = AustinXPowerBot.Clients
  - `WEBHOOK_URL` = https://<your-api>.onrender.com/api/telegram/webhook (optional)
- For Telegram worker (if used):
  - `TELEGRAM_BOT_TOKEN`
  - `ADMIN_BOT_TOKEN` (optional)
  - `API_BASE_URL` = https://<your-api>.onrender.com/
  - `ADMIN_TELEGRAM_CHAT_ID` = numeric chat id

6) Trigger deploys
- GitHub push to `main` will run the workflow `.github/workflows/deploy-render.yml` which:
  - Builds images and pushes to GHCR
  - Calls Render API to create a deploy for each service using your `RENDER_API_KEY` and `RENDER_SERVICE_*_ID` secrets

- Manually trigger a deploy via Render API (replace placeholders):

```bash
curl -X POST "https://api.render.com/v1/services/<SERVICE_ID>/deploys" \
  -H "Authorization: Bearer ${RENDER_API_KEY}" \
  -H "Content-Type: application/json" \
  -d '{}' \
  | jq
```

7) Set Telegram webhook (after API is live)
- Use Telegram API directly:

```bash
curl -s -X POST "https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/setWebhook" \
  -d "url=https://<your-api>.onrender.com/api/telegram/webhook"
```

- Or use the helper endpoint added to the API:

```bash
curl -X POST "https://<your-api>.onrender.com/api/telegram/setwebhook?url=https://<your-api>.onrender.com/api/telegram/webhook"
```

8) Verify behavior
- Send a message to the bot. The API should receive Telegram update POSTS and forward client messages to the admin chat id configured in `ADMIN_TELEGRAM_CHAT_ID`.

9) Local testing (optional)
- Build and run locally with Docker Compose (set `TELEGRAM_BOT_TOKEN` in your environment):

```bash
export TELEGRAM_BOT_TOKEN="<your-token>"
docker compose up --build
```

10) Cleanup & notes
- If you switch to webhook-only flow, you can remove the long-polling worker service.
- Rotate `Jwt__Key` and DB credentials; do NOT commit secrets.
- If you want, I can generate Render CLI scripts to create services via API, or create/complete the GitHub repo secrets for you (I cannot set secrets remotely). 

If you want the automated Render API JSON payloads and example `curl` commands to create services (so you can run them locally), tell me and I'll add them next.