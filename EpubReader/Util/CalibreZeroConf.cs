#if ANDROID
using Android.Content;
using Android.Net.Wifi;
#endif
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
    public static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServers(int scanTimeSeconds = 5, CancellationToken cancellationToken = default)
	{
		TimeSpan scanTime = TimeSpan.FromSeconds(scanTimeSeconds);
		logger.Info($"Scanning for services on the local network for {scanTimeSeconds} seconds...");

#if ANDROID
		List<(string IpAddress, int Port)> calibreServers = [];
		var ApplicationContext = Android.App.Application.Context;
		var wifi = (WifiManager?)ApplicationContext.GetSystemService(Context.WifiService) ?? throw new InvalidOperationException("WiFi service unavailable.");
		var mlock = wifi.CreateMulticastLock("Zeroconf lock");
		if (mlock is null)
		{
			logger.Error("Failed to create multicast lock for Zeroconf discovery.");
			return [];
		}
		try
		{
			mlock.Acquire();
			logger.Info("Multicast lock acquired for Zeroconf discovery.");
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to acquire multicast lock for Zeroconf discovery: {ex.Message}");
			return [];
		}
		finally
		{
			if (mlock.IsHeld)
			{
				logger.Info("Starting Zeroconf discovery on Android with multicast lock held.");
				calibreServers = await DiscoverCalibreServersWithZeroconfInternalAsync(scanTime, cancellationToken);
				logger.Info("Zeroconf discovery completed on Android. Releasing multicast lock.");
				mlock.Release();
				logger.Info("Multicast lock released after Zeroconf discovery.");
				
			}
			else
			{
				logger.Warn("Multicast lock was not held during release attempt.");
			}
		}
		return calibreServers;
		
#else
		return await DiscoverCalibreServersWithZeroconfInternalAsync(scanTime, cancellationToken);
#endif
	}


	static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServersWithZeroconfInternalAsync(TimeSpan scanTime, CancellationToken cancellationToken)
	{
		List<(string IpAddress, int Port)> calibreServers = [];
		var aService = new List<string> { "_calibre._tcp.local." };
		await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync(aService, scanTime).WaitAsync(cancellationToken);
			calibreServers.AddRange(hosts.SelectMany(host => host.Services.Where(service => service.Value.Port == 8080 || service.Value.Port == 8081)
				.Select(service => (IpAddress: host.IPAddress, service.Value.Port))));
			logger.Info($"Zeroconf discovery completed. {hosts.Count} hosts found.");
		});
	
		return calibreServers;
	}
}