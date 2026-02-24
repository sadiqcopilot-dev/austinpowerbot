using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AustinXPowerBot.Desktop.Services;

namespace AustinXPowerBot.Desktop.Pages;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void ClearClaimLock_Click(object sender, RoutedEventArgs e)
    {
        var storage = new LocalStorageService();
        await storage.DeleteAsync("claim-bonus-claimed");
        await storage.DeleteAsync("claim-bonus-info");
        MessageBox.Show("Claim lock cleared.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ResetGeneratedCode_Click(object sender, RoutedEventArgs e)
    {
        var storage = new LocalStorageService();
        await storage.DeleteAsync("claim-bonus-generated");
        await storage.DeleteAsync("claim-bonus-code");
        // Also remove Telegram bonus codes file if present
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AustinXPowerBot", "TelegramBot", "bonus-codes.json");
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }

        MessageBox.Show("Generated code reset. Users can generate a new code.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AustinXPowerBot");
            if (!Directory.Exists(baseFolder))
            {
                MessageBox.Show("No settings found to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog { FileName = $"AustinXPowerBot-settings-{DateTime.Now:yyyyMMddHHmmss}.zip", Filter = "Zip archive|*.zip" };
            if (dlg.ShowDialog() != true) return;

            ZipFile.CreateFromDirectory(baseFolder, dlg.FileName, CompressionLevel.Optimal, includeBaseDirectory: false);
            MessageBox.Show($"Settings exported to {dlg.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
 
