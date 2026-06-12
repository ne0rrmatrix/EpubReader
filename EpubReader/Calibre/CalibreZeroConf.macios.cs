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
			IReadOnlyList<string> domains;

			if (ZeroconfResolver.IsiOSWorkaroundEnabled)
			{
				// Read the known Bonjour service from Info.plist
				// (e.g. "_calibre._tcp") and append the "local."
				// domain.  The Zeroconf library parses the
				// domain from the service string, and
				// Info.plist stores only the service type per
				// Apple convention.
				domains = ZeroconfResolver.GetiOSInfoPlistServices("local.");
			}
			else
			{
				var browseDomains = await ZeroconfResolver.BrowseDomainsAsync(cancellationToken: cancellationToken);
				domains = [.. browseDomains.Select(g => g.Key)];
			}

			IReadOnlyList<IZeroconfHost> hosts = await ZeroconfResolver.ResolveAsync(domains, scanTime, cancellationToken: cancellationToken);
			calibreServers.AddRange(hosts.SelectMany(host => host.Services.Where(service => service.Value.Port == 8080 || service.Value.Port == 8081)
					.Select(service => (IpAddress: host.IPAddress, service.Value.Port))));
			logger.Info($"Zeroconf discovery completed. {hosts.Count} hosts found.");

			return calibreServers;
		});
	}
}
