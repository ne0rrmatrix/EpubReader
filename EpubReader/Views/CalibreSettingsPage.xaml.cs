using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class CalibreSettingsPage : Popup
{
	CalibreSettingsPageViewModel viewModel => (CalibreSettingsPageViewModel)BindingContext;
	public CalibreSettingsPage(CalibreSettingsPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		await viewModel.LoadSettings();
	}

	async void Switch_Toggled(object sender, ToggledEventArgs e)
	{
		if (sender is not Switch switchControl)
		{
			return;
		}
		var settings = await viewModel.db.GetSettings() ?? new Models.Settings();
		settings.CalibreAutoDiscovery = switchControl.IsToggled;
		await viewModel.db.SaveSettings(settings);
	}

	void CurrentPage_Unloaded(object sender, EventArgs e)
	{
		horizontalStacklayout.Remove(switchController);
	}
}