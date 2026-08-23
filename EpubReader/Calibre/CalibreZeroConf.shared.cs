
namespace EpubReader.Calibre;

/// <summary>
/// Provides functionality to discover Calibre content servers on the local network.
/// </summary>
public partial class CalibreZeroConf
{
	readonly ILogger logger = AppLogger.CreateLogger<CalibreZeroConf>();

	protected CalibreZeroConf()
	{
	}

}