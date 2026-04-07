namespace EpubReader.ViewModels;

public partial class CalibreSettingsPageViewModel : BaseViewModel
{
	const string defaultCalibrePrefix = "http";
	const int defaultCalibrePort = 8080;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibreSettingsPageViewModel));
	string loadedManualServerAddress = string.Empty;

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
		token.ThrowIfCancellationRequested();

		Settings settings = await db.GetSettings(token) ?? new Settings();
		ApplySettings(settings);
	}

	[RelayCommand(CanExecute = nameof(CanSaveSettings))]
		async Task SaveSettingsAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		if (IsBusy)
		{
			return;
		}

		IsBusy = true;

		try
		{
			Settings settings = await db.GetSettings(token) ?? new Settings();

			if (!TryBuildManualEndpoint(ManualServerAddress, out CalibreEndpoint? manualEndpoint, out string manualValidationMessage) && !IsAutoConfigEnabled)
			{
				StatusMessage = manualValidationMessage;
				return;
			}

			if (manualEndpoint is not null)
			{
				SaveManualEndpoint(settings, manualEndpoint);
			}

			CalibreEndpoint verifiedEndpoint;
			if (IsAutoConfigEnabled)
			{
				StatusMessage = "Discovering Calibre server and verifying the connection...";
				verifiedEndpoint = await DiscoverAndVerifyEndpointAsync(settings, token);
			}
			else
			{
				StatusMessage = "Verifying Calibre server settings...";
				verifiedEndpoint = manualEndpoint ?? throw new InvalidOperationException("Manual endpoint is required when auto configuration is disabled.");
				await VerifyEndpointAsync(verifiedEndpoint, token);
			}

			settings.CalibreAutoDiscovery = IsAutoConfigEnabled;
			settings.UrlPrefix = verifiedEndpoint.UrlPrefix;
			settings.IPAddress = verifiedEndpoint.Host;
			settings.Port = verifiedEndpoint.Port;
			SaveManualEndpoint(settings, verifiedEndpoint);

			await db.SaveSettings(settings, token);

			loadedManualServerAddress = BuildAddress(settings.CalibreManualUrlPrefix, settings.CalibreManualIPAddress, settings.CalibreManualPort);
			SavedConfigurationSummary = $"Saved connection: {BuildAddress(verifiedEndpoint.UrlPrefix, verifiedEndpoint.Host, verifiedEndpoint.Port)}";
			StatusMessage = "Connection verified and settings saved.";
			logger.Info($"Calibre settings saved successfully using {(IsAutoConfigEnabled ? "auto configuration" : "manual configuration")}: {SavedConfigurationSummary}");
			await ShowInfoToastAsync("Calibre connection verified and settings saved.");
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
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanCancel))]
	void Cancel()
	{
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

	static bool TryBuildManualEndpoint(string manualServerAddress, out CalibreEndpoint? endpoint, out string validationMessage)
	{
		endpoint = null;
		validationMessage = string.Empty;

		if (string.IsNullOrWhiteSpace(manualServerAddress))
		{
			validationMessage = "Enter a Calibre server address such as http://192.168.1.10:8080.";
			return false;
		}

		string normalizedAddress = manualServerAddress.Trim();
		if (!normalizedAddress.Contains("://", StringComparison.Ordinal))
		{
			normalizedAddress = $"{defaultCalibrePrefix}://{normalizedAddress}";
		}

		if (!Uri.TryCreate(normalizedAddress, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Host))
		{
			validationMessage = "Enter a valid Calibre server address.";
			return false;
		}

		if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
		{
			validationMessage = "Only http and https Calibre server addresses are supported.";
			return false;
		}

		if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || (uri.AbsolutePath.Length > 1 && uri.AbsolutePath != "/"))
		{
			validationMessage = "Use the Calibre server root address only. Do not include /opds, query strings, or fragments.";
			return false;
		}

		int port = uri.IsDefaultPort ? defaultCalibrePort : uri.Port;
		endpoint = new CalibreEndpoint(uri.Scheme, uri.Host, port);
		validationMessage = string.Empty;
		return true;
	}

	static async Task<CalibreEndpoint> DiscoverAndVerifyEndpointAsync(Settings settings, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		foreach (var cachedEndpoint in GetSavedEndpoints(settings))
		{
			try
			{
				await VerifyEndpointAsync(cachedEndpoint, token);
				logger.Info($"Reusing previously saved Calibre endpoint {BuildAddress(cachedEndpoint.UrlPrefix, cachedEndpoint.Host, cachedEndpoint.Port)} for auto configuration.");
				return cachedEndpoint;
			}
			catch (Exception ex)
			{
				logger.Warn($"Saved Calibre endpoint {BuildAddress(cachedEndpoint.UrlPrefix, cachedEndpoint.Host, cachedEndpoint.Port)} failed verification: {ex.Message}");
			}
		}

		List<(string IpAddress, int Port)> discoveredServers = await CalibreZeroConf.DiscoverCalibreServers();
		if (discoveredServers.Count == 0)
		{
			throw new InvalidOperationException("No Calibre servers were discovered on the local network.");
		}

		foreach (var server in discoveredServers)
		{
			var endpoint = new CalibreEndpoint(defaultCalibrePrefix, server.IpAddress, server.Port);
			try
			{
				await VerifyEndpointAsync(endpoint, token);
				return endpoint;
			}
			catch (Exception ex)
			{
				logger.Warn($"Discovered Calibre endpoint {BuildAddress(endpoint.UrlPrefix, endpoint.Host, endpoint.Port)} failed verification: {ex.Message}");
			}
		}

		throw new InvalidOperationException("Calibre servers were discovered, but none responded successfully to OPDS verification.");
	}

	static IEnumerable<CalibreEndpoint> GetSavedEndpoints(Settings settings)
	{
		HashSet<string> seenEndpoints = new(StringComparer.OrdinalIgnoreCase);

		foreach (var endpoint in new[]
		{
			TryCreateSavedEndpoint(settings.UrlPrefix, settings.IPAddress, settings.Port),
			TryCreateSavedEndpoint(settings.CalibreManualUrlPrefix, settings.CalibreManualIPAddress, settings.CalibreManualPort),
		})
		{
			if (endpoint is null)
			{
				continue;
			}

			string key = BuildAddress(endpoint.UrlPrefix, endpoint.Host, endpoint.Port);
			if (seenEndpoints.Add(key))
			{
				yield return endpoint;
			}
		}
	}

	static CalibreEndpoint? TryCreateSavedEndpoint(string prefix, string host, int port)
	{
		if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(host) || port <= 0)
		{
			return null;
		}

		return new CalibreEndpoint(prefix, host, port);
	}

	static async Task VerifyEndpointAsync(CalibreEndpoint endpoint, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		string baseAddress = BuildAddress(endpoint.UrlPrefix, endpoint.Host, endpoint.Port);
		if (!NetworkChecker.IsAddressLocalOrPermittedExternal(baseAddress))
		{
			throw new InvalidOperationException("The supplied Calibre address must be local for HTTP, or use HTTPS when accessed externally.");
		}

		string opdsAddress = $"{baseAddress}/opds";
		if (!await NetworkChecker.ValidateNetworkConnection(opdsAddress, token))
		{
			throw new InvalidOperationException("Unable to reach the Calibre OPDS feed at the supplied address.");
		}

		if (endpoint.UrlPrefix.Equals("https", StringComparison.OrdinalIgnoreCase) && !await NetworkChecker.ValidateSSLCertificate(opdsAddress))
		{
			throw new InvalidOperationException("The Calibre HTTPS certificate could not be validated.");
		}
	}

	static void SaveManualEndpoint(Settings settings, CalibreEndpoint endpoint)
	{
		settings.CalibreManualUrlPrefix = endpoint.UrlPrefix;
		settings.CalibreManualIPAddress = endpoint.Host;
		settings.CalibreManualPort = endpoint.Port;
	}

	static string BuildAddress(string prefix, string host, int port)
	{
		if (string.IsNullOrWhiteSpace(host))
		{
			return string.Empty;
		}

		return $"{prefix}://{host}:{port}";
	}

	sealed record CalibreEndpoint(string UrlPrefix, string Host, int Port);

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