using Android.Views;
using AndroidX.Core.View;

namespace EpubReader.Service;
public static partial class StatusBarExtensions
{
	public static void SetStatusBarsHidden(bool hidden)
	{
		var window = Platform.CurrentActivity?.Window ?? throw new InvalidOperationException();
		var decorView = window.DecorView ?? throw new InvalidOperationException();
		var insets = WindowCompat.GetInsetsController(window, decorView) ?? throw new InvalidOperationException();
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		{
			if (hidden)
			{
				window.ClearFlags(WindowManagerFlags.LayoutNoLimits);
				window.AddFlags(WindowManagerFlags.Fullscreen);
				window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
				insets.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
				if (OperatingSystem.IsAndroidVersionAtLeast(34))
				{
					insets.Hide(WindowInsets.Type.SystemBars());
				}
			}
			else
			{
				window.ClearFlags(WindowManagerFlags.Fullscreen);
				window.SetFlags(WindowManagerFlags.DrawsSystemBarBackgrounds, WindowManagerFlags.DrawsSystemBarBackgrounds);
				insets.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorDefault;
				if (OperatingSystem.IsAndroidVersionAtLeast(34))
				{
					insets.Show(WindowInsets.Type.SystemBars());
				}
			}
		}
		
	}
}
