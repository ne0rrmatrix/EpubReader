namespace EpubReader.Interfaces;

public interface ICalibreZeroConf
{
	Task<List<(string IpAddress, int Port)>> DiscoverCalibreServers(TimeSpan scanTime, CancellationToken cancellationToken);
}
