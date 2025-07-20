using CommunityToolkit.Maui.Views;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class CalibreSettingsPage : Popup
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibreSettingsPage));
	CalibreSettingsPageViewModel viewModel => (CalibreSettingsPageViewModel)BindingContext;
	public CalibreSettingsPage(CalibreSettingsPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		var db = viewModel.db;
		if (db is null)
		{
			logger.Error("Database service is not available.");
			return;
		}
		var settings = await db.GetSettings() ?? new Models.Settings();
		switchController.IsToggled = settings.CalibreAutoDiscovery;
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
		var settings = await viewModel.db.GetSettings() ?? new Models.Settings();
		settings.CalibreAutoDiscovery = switchControl.IsToggled;
		EntryText.IsVisible = !settings.CalibreAutoDiscovery;
		logger.Info($"Calibre auto discovery toggled to: {settings.CalibreAutoDiscovery}");
		await viewModel.db.SaveSettings(settings);
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
		string iPAddress = "localhost"; // Default IP address
		string urlPrefix = "http"; // Default URL prefix
		int port = 8080; // Default port
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
		
		Uri.TryCreate(text, UriKind.Absolute, out var uri);
		if (uri is not null && uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
		{
			logger.Warn("URL scheme is not http or https. Defaulting to http.");
			uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttp }.Uri;
		}

		if (uri is not null && Uri.IsWellFormedUriString(text, UriKind.Absolute))
		{
			iPAddress = string.Concat(uri.Host, uri.AbsolutePath.AsSpan(1));
			urlPrefix = uri.Scheme;
			port = uri.Port;
			await SaveUrlData(urlPrefix, iPAddress, port);
			logger.Info($"URL updated to: {urlPrefix}://{iPAddress}:{port}");
		}
		else if (uri is null || !Uri.IsWellFormedUriString(text, UriKind.Absolute))
		{
			await SaveUrlData(urlPrefix, iPAddress, port);
			logger.Warn("Invalid URL format. Please enter a valid URL.");
		}
		else
		{
			EntryText.Text = string.Empty;
			await SaveUrlData(urlPrefix, iPAddress, port);
			logger.Warn("Invalid URL format. Please enter a valid URL.");
		}
	}

	async Task SaveUrlData(string urlPrefix, string ipAddress, int port)
	{
		var settings = await viewModel.db.GetSettings() ?? new Models.Settings
		{
			UrlPrefix = urlPrefix,
			IPAddress = ipAddress,
			Port = port
		};
		await viewModel.db.SaveSettings(settings);
		logger.Info($"URL data saved: {settings.UrlPrefix}://{settings.IPAddress}:{settings.Port}");
	}
}