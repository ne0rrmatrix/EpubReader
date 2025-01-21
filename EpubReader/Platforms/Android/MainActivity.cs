using Android.App;
using Android.Content.PM;
using Android.OS;

namespace EpubReader;

[Activity(Theme = "@style/Maui.SplashTheme", ResizeableActivity = true, MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density )]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		// Must apply the style before the DecorView setup
		Theme?.ApplyStyle(Resource.Style.OptOutEdgeToEdgeEnforcement, force: false);

		base.OnCreate(savedInstanceState);
	}
}
