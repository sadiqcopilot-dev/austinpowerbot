using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AustinXPowerBot.Desktop.Services;

public static class TelegramBonusCodeValidator
{
    private static readonly string BonusCodesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AustinXPowerBot",
        "TelegramBot",
        "bonus-codes.json");

    // Path where the desktop app persists a one-time generated code
    private static readonly string AppGeneratedCodePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AustinXPowerBot",
        "claim-bonus-code.json");

    public static bool IsValid(string code, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(code))
        {
            error = "Bonus code is empty.";
            return false;
        }
        var codes = LoadCodes();
        var now = DateTimeOffset.UtcNow;
        var match = codes.FirstOrDefault(x => string.Equals(x.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            if ((now - match.GeneratedAtUtc).TotalMinutes > 10)
            {
                error = "Bonus code expired. Generate a new code from the app or Telegram.";
                return false;
            }

            return true;
        }

        // If not found in Telegram-generated list, check the desktop app persisted one-time code
        try
        {
            if (File.Exists(AppGeneratedCodePath))
            {
                var json = File.ReadAllText(AppGeneratedCodePath);
                var existing = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
                if (string.Equals(existing, code.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // ignore read/deserialize errors and fallthrough to error message
        }

        error = "Bonus code not recognized. Generate a new code from the app or Telegram.";
        return false;
    }

    private static List<BonusCodeRecord> LoadCodes()
    {
        try
        {
            if (!File.Exists(BonusCodesPath))
                return new List<BonusCodeRecord>();
            var json = File.ReadAllText(BonusCodesPath);
            return JsonSerializer.Deserialize<List<BonusCodeRecord>>(json) ?? new List<BonusCodeRecord>();
        }
        catch { return new List<BonusCodeRecord>(); }
    }

    private sealed class BonusCodeRecord
    {
        public string Code { get; set; } = string.Empty;
        public long TelegramChatId { get; set; }
        public DateTimeOffset GeneratedAtUtc { get; set; }
    }
}
