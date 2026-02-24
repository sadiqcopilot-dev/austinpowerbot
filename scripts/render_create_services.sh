#!/usr/bin/env bash
# Template script to create Render services via API.
# Replace placeholders and run locally. Requires RENDER_API_KEY env var.

set -euo pipefail

if [ -z "${RENDER_API_KEY:-}" ]; then
  echo "Set RENDER_API_KEY environment variable with your Render API key." >&2
  exit 1
fi

# Replace these values
REPO_GIT_URL="https://github.com/<your-org>/<your-repo>.git"
REGION="oregon"
PLAN="starter"

# 1) Create Postgres managed DB
echo "Creating Postgres database..."
cat <<EOF > /tmp/render_db_payload.json
{
  "service": {
    "name": "austinxpowerbot-db",
    "type": "postgres",
    "plan": "starter",
    "region": "${REGION}"
  }
}
EOF

curl -s -X POST "https://api.render.com/v1/services" \
  -H "Authorization: Bearer ${RENDER_API_KEY}" \
  -H "Content-Type: application/json" \
  -d @/tmp/render_db_payload.json | jq

# 2) Create API web service (Docker)
echo "Creating API web service..."
cat <<EOF > /tmp/render_api_payload.json
{
  "service": {
    "name": "AustinXPowerBot-API",
    "repo": "${REPO_GIT_URL}",
    "type": "web",
    "env": "docker",
    "dockerfilePath": "AustinXPowerBot.Api/Dockerfile",
    "plan": "${PLAN}",
    "region": "${REGION}"
  }
}
EOF

curl -s -X POST "https://api.render.com/v1/services" \
  -H "Authorization: Bearer ${RENDER_API_KEY}" \
  -H "Content-Type: application/json" \
  -d @/tmp/render_api_payload.json | jq

# 3) Create Telegram worker (Docker)
echo "Creating Telegram worker service..."
cat <<EOF > /tmp/render_telegram_payload.json
{
  "service": {
    "name": "AustinXPowerBot-Telegram",
    "repo": "${REPO_GIT_URL}",
    "type": "worker",
    "env": "docker",
    "dockerfilePath": "AustinXPowerBot.TelegramBot/Dockerfile",
    "plan": "${PLAN}",
    "region": "${REGION}"
  }
}
EOF

curl -s -X POST "https://api.render.com/v1/services" \
  -H "Authorization: Bearer ${RENDER_API_KEY}" \
  -H "Content-Type: application/json" \
  -d @/tmp/render_telegram_payload.json | jq

echo "Requests submitted — check the Render dashboard to confirm and set environment variables for each service."