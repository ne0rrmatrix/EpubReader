using CommunityToolkit.Maui.Views;
using EpubReader.Interfaces;
using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class CalibreSettingsPage : Popup
{
	CalibreSettingsPageViewModel viewModel => (CalibreSettingsPageViewModel)BindingContext;
	MetroLog.ILogger logger => viewModel.Logger;

	/// <summary>
	/// Gets or sets the database service used by the application.
	/// </summary>
	public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	public CalibreSettingsPage(CalibreSettingsPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		if (db is null)
		{
			logger.Error("Database service is not available.");
			return;
		}
		var settings = await db.GetSettings() ?? new Models.Settings();
		if(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
		{
			logger.Info("CalibreSettingsPage loaded on Windows or macOS, setting EntryText to visible.");
			switchController.IsToggled = settings.CalibreAutoDiscovery;
		}
		else
		{
			logger.Info("CalibreSettingsPage loaded on Android or iOS, setting EntryText to not visible.");
			switchController.IsToggled = false;
		}
		
		EntryText.Text = $"{settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}";
		logger.Info("CalibreSettingsPage loaded.");
	}

	async void Switch_Toggled(object? sender, ToggledEventArgs e)
	{
		if (sender is null)
		{
			logger.Warn("Sender is null, cannot toggle Calibre auto discovery.");
			return;
		}
		if (sender is not Switch switchControl)
		{
			logger.Warn("Sender is not a Switch control, cannot toggle Calibre auto discovery.");
			return;
		}
		var settings = await db.GetSettings() ?? new Models.Settings();
		settings.CalibreAutoDiscovery = switchControl.IsToggled;
		EntryText.IsVisible = !settings.CalibreAutoDiscovery;
		logger.Info($"Calibre auto discovery toggled to: {settings.CalibreAutoDiscovery}");
		await db.SaveSettings(settings);
		logger.Info("Settings saved successfully.");
	}

	void CurrentPage_Unloaded(object sender, EventArgs e)
	{
		horizontalStacklayout.Remove(switchController);
		switchController.Toggled -= Switch_Toggled;
		grid.Remove(EntryText);
		EntryText.Completed -= Entry_Completed;
	}

	async void Entry_Completed(object? sender, EventArgs e)
	{
		if(sender is null)
		{
			logger.Warn("Sender is null, cannot process completed event.");
			return;
		}
		if (sender is not Entry entry)
		{
			logger.Warn("Sender is not an Entry control, cannot process completed event.");
			return;
		}
		logger.Info("Entry completed event triggered.");
		var text = entry.Text;
		
		if (string.IsNullOrWhiteSpace(text))
		{
			logger.Warn("URL is empty or whitespace. Please enter a valid URL.");
			return;
		}
		logger.Info($"Processing URL: {text}");

		var settings = await db.GetSettings() ?? new Models.Settings();
		var uri = new Uri(text);
		settings.IPAddress = string.Concat(uri.Host, uri.AbsolutePath.AsSpan(1));
		settings.UrlPrefix = uri.Scheme;
		settings.Port = uri.Port;
		await db.SaveSettings(settings);
		logger.Info($"URL updated to: {settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}");
	}
}