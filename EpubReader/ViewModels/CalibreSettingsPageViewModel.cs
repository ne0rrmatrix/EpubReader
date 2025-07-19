using CommunityToolkit.Mvvm.ComponentModel;

namespace EpubReader.ViewModels;
public partial class CalibreSettingsPageViewModel : BaseViewModel
{
	[ObservableProperty]
	public partial bool CalibreAutoDiscovery { get; set; } = true;
	public CalibreSettingsPageViewModel()
	{

	}

	public async Task LoadSettings()
	{
		var settings = await db.GetSettings() ?? new Models.Settings();
		CalibreAutoDiscovery = settings.CalibreAutoDiscovery;
	}
}
