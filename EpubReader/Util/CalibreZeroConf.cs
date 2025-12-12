using Zeroconf;

namespace EpubReader.Util;

/// <summary>
/// Provides functionality to discover Calibre content servers on the local network using Zeroconf.
/// </summary>
/// <remarks>This class utilizes Zeroconf to identify Calibre servers by scanning the local network for services
/// that match the Calibre service type. It is designed to be used in environments where Calibre servers are expected to
/// be running and accessible over the network.</remarks>
public partial class CalibreZeroConf
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(CalibreZeroConf));
	protected CalibreZeroConf()
	{
	}

	/// <summary>
	/// Discovers Calibre content servers on the local network using Zeroconf.
	/// </summary>
	/// <param name="scanTimeSeconds">The duration in seconds to scan for services. Default is 5 seconds.</param>
	/// <returns>A list of tuples containing the IP address and port of discovered Calibre servers.</returns>
	public static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServers(int scanTimeSeconds = 5)
	{
		List<(string IpAddress, int Port)> calibreServers = [];

		TimeSpan scanTime = TimeSpan.FromSeconds(scanTimeSeconds);

		logger.Info($"Scanning for services on the local network for {scanTimeSeconds} seconds...");

		try
		{
			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync("_calibre._tcp.local.", scanTime);
			foreach (var host in hosts)
			{
				logger.Info($"Discovered Host: {host.DisplayName} (IP: {host.IPAddress})");

				foreach (var service in host.Services)
				{
					logger.Info($"  Service: {service.Key} (Port: {service.Value.Port})");
					if (service.Value.Port == 8080 || service.Value.Port == 8081) // Calibre default is 8080, sometimes 8081 for docker
					{
						logger.Info($"    Potential Calibre Server Found: {host.IPAddress}:{service.Value.Port}");
						calibreServers.Add((host.IPAddress, service.Value.Port));
					}
				}
			}
			if (calibreServers.Count == 0)
			{
				logger.Info("No Calibre content servers found on the local network.");
			}
		}
		catch (Exception ex)
		{
			logger.Error($"An error occurred during Zeroconf discovery: {ex.Message}");
		}

		if (calibreServers.Count == 0)
		{
			logger.Info("No Calibre content servers found on the local network.");
		}

		return calibreServers;
	}
}