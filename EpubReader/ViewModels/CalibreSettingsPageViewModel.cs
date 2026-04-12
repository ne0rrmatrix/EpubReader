namespace EpubReader.ViewModels;

public partial class CalibreSettingsPageViewModel : BaseViewModel
{
	const string defaultCalibrePrefix = "http";
	static readonly ILogger logger = AppLogger.CreateLogger<CalibreSettingsPageViewModel>();
	string loadedManualServerAddress = string.Empty;
	CancellationTokenSource? settingsOperationCancellationTokenSource;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsManualAddressVisible))]
	[NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
	public partial bool IsAutoConfigEnabled { get; set; } = true;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
	public partial string ManualServerAddress { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatusMessage))]
	public partial string StatusMessage { get; set; } = "Auto configuration will discover and verify your Calibre server before saving.";

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
	[NotifyCanExecuteChangedFor(nameof(CancelCommand))]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial string SavedConfigurationSummary { get; set; } = string.Empty;

	public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
	public bool IsManualAddressVisible => !IsAutoConfigEnabled;
	public event EventHandler<bool>? CloseRequested;

	public CalibreSettingsPageViewModel()
	{
	}

	public async Task InitializeAsync(CancellationToken token = default)
	{
		Settings settings = await db.GetSettings(token) ?? new Settings();
		ApplySettings(settings);
	}

	[RelayCommand(CanExecute = nameof(CanSaveSettings))]
	async Task SaveSettingsAsync(CancellationToken token)
	{
		using CancellationTokenSource operationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
		settingsOperationCancellationTokenSource = operationCancellationTokenSource;
		IsBusy = true;

		try
		{
			Settings settings = await db.GetSettings(operationCancellationTokenSource.Token) ?? new Settings();
			settings.CalibreAutoDiscovery = IsAutoConfigEnabled;

			var result = await CalibreZeroConf.DiscoverCalibreServers(cancellationToken: operationCancellationTokenSource.Token); // Start discovery early to populate cache for faster verification, even if auto config is not enabled
			if (result.Count > 0)
			{
				var (IpAddress, Port) = result[0];
				settings.CalibreManualUrlPrefix = defaultCalibrePrefix;
				settings.CalibreManualIPAddress = IpAddress;
				settings.CalibreManualPort = Port;

				await db.SaveSettings(settings, operationCancellationTokenSource.Token);
				loadedManualServerAddress = BuildAddress(settings.CalibreManualUrlPrefix, settings.CalibreManualIPAddress, settings.CalibreManualPort);
				SavedConfigurationSummary = $"Saved connection: {BuildAddress("http", settings.CalibreManualIPAddress, settings.CalibreManualPort)}";
				StatusMessage = "Connection verified and settings saved.";
				logger.Info($"Calibre settings saved successfully using {(IsAutoConfigEnabled ? "auto configuration" : "manual configuration")}: {SavedConfigurationSummary}");
				await ShowInfoToastAsync("Calibre connection verified and settings saved.");
				SavedConfigurationSummary = $"Saved connection: ";
				logger.Info($"Discovered Calibre server at http://{settings.CalibreManualIPAddress}:{settings.CalibreManualPort} during settings save.");
				logger.Info($"Calibre settings saved successfully using {(IsAutoConfigEnabled ? "auto configuration" : "manual configuration")}: {SavedConfigurationSummary}");
				await ShowInfoToastAsync("Calibre connection verified and settings saved.");
			}
			else
			{
				settings.CalibreManualUrlPrefix = string.Empty;
				settings.CalibreManualIPAddress = string.Empty;
				settings.CalibreManualPort = 0;
				SavedConfigurationSummary = "No Calibre servers discovered on the local network. Please ensure your Calibre server is running and connected to the same network, then try again.";
			}
			CloseRequested?.Invoke(this, true);
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Calibre settings update was cancelled.";
			logger.Warn(StatusMessage);
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
			logger.Error($"Failed to save Calibre settings: {ex.Message}");
		}
		finally
		{
			settingsOperationCancellationTokenSource = null;
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanCancel))]
	void Cancel()
	{
		if (IsBusy)
		{
			StatusMessage = "Cancelling Calibre settings update...";
			settingsOperationCancellationTokenSource?.Cancel();
			return;
		}

		CloseRequested?.Invoke(this, false);
	}

	bool CanSaveSettings()
		=> !IsBusy && (IsAutoConfigEnabled || !string.IsNullOrWhiteSpace(ManualServerAddress));

	bool CanCancel()
		=> !IsBusy;

	void ApplySettings(Settings settings)
	{
		IsAutoConfigEnabled = settings.CalibreAutoDiscovery;
		ManualServerAddress = BuildAddress(settings.CalibreManualUrlPrefix, settings.CalibreManualIPAddress, settings.CalibreManualPort);
		loadedManualServerAddress = ManualServerAddress;
		SavedConfigurationSummary = string.IsNullOrWhiteSpace(settings.IPAddress)
			? string.Empty
			: $"Current connection: {BuildAddress(settings.UrlPrefix, settings.IPAddress, settings.Port)}";
		StatusMessage = IsAutoConfigEnabled
			? "Auto configuration is enabled. Verify & Save will discover the current Calibre server and save it to the database."
			: "Manual configuration is enabled. Enter your Calibre server root address, then Verify & Save.";
	}

	static string BuildAddress(string prefix, string host, int port)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return string.Empty;
		}

		return $"{prefix}://{host}:{port}";
	}

	partial void OnIsAutoConfigEnabledChanged(bool value)
	{
		if (IsBusy)
		{
			return;
		}

		StatusMessage = value
			? "Auto configuration is enabled. Verify & Save will discover the current Calibre server and save it to the database."
			: "Manual configuration is enabled. Enter your Calibre server root address, then Verify & Save.";
	}

	partial void OnManualServerAddressChanged(string value)
	{
		if (IsBusy || string.Equals(value, loadedManualServerAddress, StringComparison.Ordinal))
		{
			return;
		}

		StatusMessage = "Manual Calibre server updated. Verify & Save to validate the connection and store the settings.";
	}
}