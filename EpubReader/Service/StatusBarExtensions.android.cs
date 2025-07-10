using Android.Views;
using AndroidX.Core.View;

namespace EpubReader.Service;
public static partial class StatusBarExtensions
{
	static Android.Views.Window window => Platform.CurrentActivity?.Window ?? throw new InvalidOperationException("Current activity is null");
	static Android.Views.View decorView => window.DecorView ?? throw new InvalidOperationException("DecorView is null");
	static AndroidX.Core.View.WindowInsetsControllerCompat insetsController => WindowCompat.GetInsetsController(window, decorView) ?? throw new InvalidOperationException("InsetsController is null");
	public static void SetStatusBarsHidden(bool hidden)
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		{
			if (hidden)
			{
				window.ClearFlags(WindowManagerFlags.LayoutNoLimits);
				window.AddFlags(WindowManagerFlags.Fullscreen);
				window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
				insetsController.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
				if (OperatingSystem.IsAndroidVersionAtLeast(34))
				{
					insetsController.Hide(WindowInsets.Type.SystemBars());
				}
			}
			else
			{
				window.ClearFlags(WindowManagerFlags.Fullscreen);
				window.SetFlags(WindowManagerFlags.DrawsSystemBarBackgrounds, WindowManagerFlags.DrawsSystemBarBackgrounds);
				insetsController.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorDefault;
				if (OperatingSystem.IsAndroidVersionAtLeast(34))
				{
					insetsController.Show(WindowInsets.Type.SystemBars());
				}
			}
		}
		
	}
}
