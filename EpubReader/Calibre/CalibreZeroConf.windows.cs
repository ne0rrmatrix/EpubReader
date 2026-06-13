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
		List<(string IpAddress, int Port)> calibreServers = [];
		var aService = new List<string> { "_calibre._tcp" };

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
}
