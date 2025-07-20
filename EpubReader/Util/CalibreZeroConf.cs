using MetroLog;
using Zeroconf;

#if ANDROID
using Android.Content;
using Android.Net.Wifi;
using Android.App;
#endif

namespace EpubReader.Util;

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
#if ANDROID
		var applicationContext = Android.App.Application.Context;
		var wifi = (WifiManager?)applicationContext.GetSystemService(Context.WifiService) ?? throw new InvalidOperationException("Unable to get WifiManager");
		var mlock = wifi.CreateMulticastLock("Zeroconf lock");
		try
		{
			mlock?.Acquire();
			if (mlock is null)
			{
				logger.Error("Multicast lock is null, cannot proceed with Zeroconf discovery.");
				return [];
			}
			return await GetData(scanTimeSeconds).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to acquire multicast lock: {ex.Message}");
			return [];
		}
		finally
		{
			if (mlock?.IsHeld == true)
			{
				mlock.Release();
				logger.Info("Multicast lock released.");
			}
		}
#else
		return await GetData(scanTimeSeconds).ConfigureAwait(false);
#endif
	}

	static async Task<List<(string IpAddress, int Port)>> GetData(int scanTimeSeconds)
	{
		List<(string IpAddress, int Port)> calibreServers = [];

		TimeSpan scanTime = TimeSpan.FromSeconds(scanTimeSeconds);

		logger.Info($"Scanning for services on the local network for {scanTimeSeconds} seconds...");

		try
		{
#if WINDOWS
			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync("_calibre._tcp.local.", scanTime);
#else
			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync("_calibre._tcp", scanTime);
#endif
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
