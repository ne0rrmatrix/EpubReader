using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using EpubReader.Interfaces;
using EpubReader.Service;

namespace EpubReader;

[Activity(Theme = "@style/Maui.SplashTheme", ResizeableActivity = true, MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density )]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);

		var serviceProvider = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Unable to retrieve service provider");
		var folderPickerService = serviceProvider.GetService<IFolderPicker>() as FolderPicker;
		folderPickerService?.OnActivityResult(requestCode, resultCode, data);
	}
}
