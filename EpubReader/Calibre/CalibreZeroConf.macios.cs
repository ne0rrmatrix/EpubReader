using Zeroconf;

namespace EpubReader.Calibre;

public partial class CalibreZeroConf : ICalibreZeroConf
{
	public CalibreZeroConf(ILogger<CalibreZeroConf> logger)
	{
		this.logger = logger;
	}
	public async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServers(TimeSpan scanTime, CancellationToken cancellationToken)
	{
		// NSNetServiceBrowser on iOS MUST be created, configured, and used
		// on the main thread.  Calling it from a background thread causes
		// it to silently fail — no search results and no local network
		// permission dialog.
		return await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			List<(string IpAddress, int Port)> calibreServers = [];
			List<string> aService = ["_calibre._tcp"];

			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync(aService, scanTime, cancellationToken: cancellationToken);
			calibreServers.AddRange(hosts.SelectMany(host => host.Services
				.Select(service => (IpAddress: host.IPAddress, service.Value.Port))));
			logger.Info($"Zeroconf discovery completed. {hosts.Count} hosts found.");

			return calibreServers;
		});
	}
}
