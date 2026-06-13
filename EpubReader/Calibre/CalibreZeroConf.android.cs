using Android.Content;
using Android.Net.Wifi;
using Zeroconf;
using CancellationToken = System.Threading.CancellationToken;

namespace EpubReader.Calibre;

public partial class CalibreZeroConf : ICalibreZeroConf
{
	public CalibreZeroConf(ILogger<CalibreZeroConf> logger)
	{
		this.logger = logger;
	}
	async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServersWithZeroconfInternalAsync(TimeSpan scanTime, CancellationToken cancellationToken)
	{

		List<(string IpAddress, int Port)> calibreServers = [];
		List<string> aService = ["_calibre._tcp"];

		await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync(aService, scanTime).WaitAsync(cancellationToken);

			// REMOVE strict port checking. Calibre can run on any port (e.g. 8082, 9000).
			calibreServers.AddRange(hosts.SelectMany(host => host.Services
				.Select(service => (IpAddress: host.IPAddress, service.Value.Port))));

			logger.Info($"Zeroconf discovery completed. {hosts.Count} hosts found.");
		});

		return calibreServers;
	}

	public async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServers(TimeSpan scanTime, CancellationToken cancellationToken)
	{
		logger.Info($"Scanning for services on the local network for {scanTime.TotalSeconds} seconds...");

		List<(string IpAddress, int Port)> calibreServers = [];
		Context ApplicationContext = Android.App.Application.Context;
		WifiManager wifi = (WifiManager?)ApplicationContext.GetSystemService(Context.WifiService) ?? throw new InvalidOperationException("WiFi service unavailable.");
		WifiManager.MulticastLock? mlock = wifi.CreateMulticastLock("Zeroconf lock");
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
	}
}
