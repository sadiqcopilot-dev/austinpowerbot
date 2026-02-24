using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using AustinXPowerBot.Desktop.Services;

namespace AustinXPowerBot.Desktop;

public partial class App : Application
{
	private readonly AppUpdateService _appUpdateService = new();

	private static readonly string CrashLogPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"AustinXPowerBot",
		"Logs",
		"crash.log");

	protected override void OnStartup(StartupEventArgs e)
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

		base.OnStartup(e);

		_ = CheckForUpdatesOnStartupAsync();
	}

	private async Task CheckForUpdatesOnStartupAsync()
	{
		try
		{
			var manifest = await _appUpdateService.GetAvailableUpdateAsync();
			if (manifest is null)
			{
				return;
			}

			var notesText = string.IsNullOrWhiteSpace(manifest.Notes)
				? string.Empty
				: $"\n\nRelease notes:\n{manifest.Notes}";

			var result = MessageBox.Show(
				$"A new version ({manifest.Version}) is available. Install update now?{notesText}",
				"AustinXPowerBot Update",
				MessageBoxButton.YesNo,
				MessageBoxImage.Information);

			if (result != MessageBoxResult.Yes)
			{
				return;
			}

			_appUpdateService.LaunchInstaller(manifest.InstallerUrl);
			Shutdown();
		}
		catch
		{
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		DispatcherUnhandledException -= OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
		TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

		base.OnExit(e);
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		WriteCrash("DispatcherUnhandledException", e.Exception);
		MessageBox.Show(
			$"Unexpected error: {e.Exception.Message}\n\nA crash log was saved to:\n{CrashLogPath}",
			"AustinXPowerBot Error",
			MessageBoxButton.OK,
			MessageBoxImage.Error);

		e.Handled = true;
	}

	private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled domain exception.");
		WriteCrash($"AppDomainUnhandledException | IsTerminating={e.IsTerminating}", ex);
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		WriteCrash("TaskSchedulerUnobservedTaskException", e.Exception);
		e.SetObserved();
	}

	private static void WriteCrash(string source, Exception ex)
	{
		try
		{
			var folder = Path.GetDirectoryName(CrashLogPath)!;
			Directory.CreateDirectory(folder);

			var sb = new StringBuilder();
			sb.Append('[').Append(DateTimeOffset.UtcNow.ToString("O")).Append("] ")
			  .Append(source).AppendLine();
			sb.AppendLine(ex.ToString());
			sb.AppendLine(new string('-', 80));

			File.AppendAllText(CrashLogPath, sb.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
	}
}
