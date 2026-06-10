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

			if (IsAutoConfigEnabled)
			{
				// Auto-discovery: use Bonjour to find Calibre servers
				var result = await CalibreZeroConf.DiscoverCalibreServers(cancellationToken: operationCancellationTokenSource.Token);
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
					logger.Info($"Calibre settings saved successfully using auto configuration: {SavedConfigurationSummary}");
					await ShowInfoToastAsync("Calibre connection verified and settings saved.");
				}
				else
				{
					// No servers found — still persist the auto-discovery preference
					await db.SaveSettings(settings, operationCancellationTokenSource.Token);
					SavedConfigurationSummary = string.Empty;
					StatusMessage = "No Calibre servers discovered on the local network. Please ensure your Calibre server is running and connected to the same network, then try again.";
					logger.Warn(StatusMessage);
				}
			}
			else
			{
				// Manual configuration: parse the user-entered address
				if (TryParseServerAddress(ManualServerAddress, out string? parsedPrefix, out string? parsedHost, out int parsedPort))
				{
					settings.CalibreManualUrlPrefix = parsedPrefix!;
					settings.CalibreManualIPAddress = parsedHost!;
					settings.CalibreManualPort = parsedPort;

					await db.SaveSettings(settings, operationCancellationTokenSource.Token);
					loadedManualServerAddress = ManualServerAddress;
					SavedConfigurationSummary = $"Saved connection: {ManualServerAddress}";
					StatusMessage = "Manual connection verified and settings saved.";
					logger.Info($"Calibre settings saved successfully using manual configuration: {ManualServerAddress}");
					await ShowInfoToastAsync("Calibre connection verified and settings saved.");
				}
				else
				{
					// Invalid manual address format — still persist the auto-discovery off preference
					await db.SaveSettings(settings, operationCancellationTokenSource.Token);
					SavedConfigurationSummary = string.Empty;
					StatusMessage = "Invalid server address format. Please use http://host:port or https://host:port.";
					logger.Warn($"Invalid manual Calibre address: {ManualServerAddress}");
					await ShowInfoToastAsync("Invalid server address format.");
				}
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

	/// <summary>
	/// Parses a server address string like "http://192.168.1.10:8080" into its components.
	/// </summary>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise <see langword="false"/>.</returns>
	static bool TryParseServerAddress(string address, out string? prefix, out string? host, out int port)
	{
		prefix = null;
		host = null;
		port = 0;

		if (string.IsNullOrWhiteSpace(address))
		{
			return false;
		}

		// Try parsing as a URI
		if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out Uri? uri))
		{
			return false;
		}

		if (uri.Scheme is not ("http" or "https"))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(uri.Host))
		{
			return false;
		}

		prefix = uri.Scheme;
		host = uri.Host;
		port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 8080);
		return true;
	}
}