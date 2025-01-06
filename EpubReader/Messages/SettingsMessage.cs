using EpubReader.Models;

namespace EpubReader.Messages;
public class SettingsMessage(bool ShouldUpdate)
{
	public bool ShouldUpdate { get; } = ShouldUpdate;
}
