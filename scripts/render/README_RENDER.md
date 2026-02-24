# Trigger Render deploy

This folder contains a small script to trigger a Render deploy for an existing Render service.

Prerequisites:

- A Render service already created and connected to this repository (via GitHub/GitLab).
- A Render API key with permissions to trigger deploys. Set it as the environment variable `RENDER_API_KEY`.
- The target service id set as `RENDER_SERVICE_ID` environment variable.

Usage (PowerShell):

```powershell
# set env vars for this session (or configure in system/user env)
$env:RENDER_API_KEY = 'your_api_key_here'
$env:RENDER_SERVICE_ID = 'svc-xxxxxxxxxxxxxxxx'

# trigger deploy for branch 'main'
.
\scripts\render\trigger_render_deploy.ps1 -Branch main
```

Or pass parameters directly:

```powershell
.
\scripts\render\trigger_render_deploy.ps1 -ApiKey "YOUR_KEY" -ServiceId "svc-..." -Branch "main"
```

Notes:
- If you don't yet have a Render service: open https://dashboard.render.com/new and create a Web Service using this repository. After creation, copy the Service ID from the service settings into `RENDER_SERVICE_ID`.
- This script only triggers a deploy; if you need full infrastructure creation using `render.yaml`, use the Render dashboard or CLI and the repository manifest.
