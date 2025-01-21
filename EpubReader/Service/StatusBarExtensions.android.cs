using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Platform;

namespace EpubReader.Service;
public static class StatusBarExtensions
{
	public static void SetStatusBarTransparent()
	{
		if (Application.Current?.PlatformAppTheme == AppTheme.Dark)
		{
			StatusBar.SetStyle(CommunityToolkit.Maui.Core.StatusBarStyle.LightContent);
		}
		else
		{
			StatusBar.SetStyle(CommunityToolkit.Maui.Core.StatusBarStyle.DarkContent);
		}
	}

	public static void SetStatusBarsHidden(bool hidden)
	{
		var window = Platform.CurrentActivity?.Window ?? throw new InvalidOperationException();
		var decorView = window.DecorView ?? throw new InvalidOperationException();
		var insets = WindowCompat.GetInsetsController(window, decorView) ?? throw new InvalidOperationException();
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		{
			if (hidden)
			{
				SetStatusBarTransparent();
				window.ClearFlags(WindowManagerFlags.LayoutNoLimits);
				window.SetFlags(WindowManagerFlags.DrawsSystemBarBackgrounds, WindowManagerFlags.DrawsSystemBarBackgrounds);
				window.ClearFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
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
				SetStatusBarTransparent();
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
