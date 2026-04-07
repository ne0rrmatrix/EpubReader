using Zeroconf;

namespace EpubReader.Util;

/// <summary>
/// Provides functionality to discover Calibre content servers on the local network.
/// </summary>
public partial class CalibreZeroConf
{
  static readonly ILogger logger = AppLogger.CreateLogger<CalibreZeroConf>();

	protected CalibreZeroConf()
	{
	}

	/// <summary>
	/// Discovers Calibre content servers on the local network.
	/// </summary>
	/// <param name="scanTimeSeconds">The duration in seconds to scan for services. Default is 5 seconds.</param>
	/// <returns>A list of tuples containing the IP address and port of discovered Calibre servers.</returns>
	public static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServers(int scanTimeSeconds = 5)
	{
		TimeSpan scanTime = TimeSpan.FromSeconds(scanTimeSeconds);

		logger.Info($"Scanning for services on the local network for {scanTimeSeconds} seconds...");

		try
		{
#if ANDROID
			var calibreServers = await MainThread.InvokeOnMainThreadAsync(() => DiscoverCalibreServersOnAndroidAsync(scanTime));
#else
			var calibreServers = await DiscoverCalibreServersWithZeroconfAsync(scanTime);
#endif
			if (calibreServers.Count == 0)
			{
				logger.Info("No Calibre content servers found on the local network.");
			}

			return calibreServers;
		}
		catch (Exception ex)
		{
			logger.Error($"An error occurred during Calibre discovery: {ex.Message}");
		}

		logger.Info("No Calibre content servers found on the local network.");
		return [];
	}

	static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServersWithZeroconfAsync(TimeSpan scanTime)
	{
		List<(string IpAddress, int Port)> calibreServers = [];

		ResolveOptions resolveOptions = new("_calibre._tcp.local.")
		{
			ScanTime = scanTime,
			Retries = 3,
			RetryDelay = TimeSpan.FromSeconds(1),
			AllowOverlappedQueries = true
		};

		IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync(resolveOptions);
		foreach (var host in hosts)
		{
			logger.Info($"Discovered Host: {host.DisplayName} (IP: {host.IPAddress})");

			foreach (var service in host.Services)
			{
				logger.Info($"  Service: {service.Key} (Port: {service.Value.Port})");
				if (IsPotentialCalibrePort(service.Value.Port) || MatchesCalibreServiceType(service.Key))
				{
					logger.Info($"    Potential Calibre Server Found: {host.IPAddress}:{service.Value.Port}");
					calibreServers.Add((host.IPAddress, service.Value.Port));
				}
			}
		}

		return calibreServers;
	}

	static bool MatchesCalibreServiceType(string? serviceType)
		=> !string.IsNullOrWhiteSpace(serviceType) && serviceType.Contains("_calibre._tcp", StringComparison.OrdinalIgnoreCase);

	static bool IsPotentialCalibrePort(int port)
		=> port == 8080 || port == 8081;
}