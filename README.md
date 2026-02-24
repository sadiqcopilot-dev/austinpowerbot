<<<<<<< HEAD
# austinpowerbot
=======
# Austin X PowerBot (Structure-Only Source)

This repository contains **structure-only** source code for the Austin X PowerBot system:
- WPF Desktop App (toggle sidebar + fixed right panel + screens)
- ASP.NET Core API (placeholder endpoints)
- Telegram bot project (placeholder console app)
- Shared DTO library

## What is included
✅ Full UI skeleton with pages:
- Dashboard (includes Device Model + Device ID area)
- Auto Trade (Selenium Engine layout placeholders)
- Signals
- Telegram Control
- Risk Manager
- Trade History
- Claim Bonus
- Device & License
- Settings
- Help & Support
- Notifications Center

✅ Solution file + csproj files

## What is NOT included (by design for now)
- Selenium automation logic
- Signal sources/webhooks
- Real Telegram.Bot implementation
- Database/auth/licensing logic

## How to open in Visual Studio
1. Extract the zip
2. Open `AustinXPowerBot.sln` in Visual Studio 2022+
3. Set `AustinXPowerBot.Desktop` as Startup Project and run

## Next steps (when you're ready)
- Add real services to Desktop `Services/` and wire them to ViewModels
- Implement API services + database
- Add Telegram.Bot package and implement real commands

## Desktop auto-update (configured)
- The desktop app checks for updates on startup using a remote JSON manifest.
- If a newer version is found, users are prompted to install it.
- Configure manifest URL using either:
	- Environment variable: `AUSTINXPOWERBOT_UPDATE_MANIFEST_URL`
	- File: `%AppData%\AustinXPowerBot\update.settings.json` with:

```json
{
	"updateManifestUrl": "https://your-domain.com/updates/desktop-manifest.json"
}
```

Manifest shape:

```json
{
	"version": "1.0.5",
	"installerUrl": "https://your-domain.com/updates/AustinXPowerBot.Desktop.Setup.exe",
	"notes": "Bug fixes and stability improvements"
}
```

User/license data remains preserved across updates because app state is stored in user profile folders (`%AppData%` / `%LocalAppData%`) and is not tied to the install directory.
>>>>>>> a43abd5 (Prepare for deploy: add Docker/Render/CI/webhook)
