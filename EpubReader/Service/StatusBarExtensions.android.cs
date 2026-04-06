using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Core.Platform;
using Activity = Android.App.Activity;
using View = Android.Views.View;

namespace EpubReader.Service;

/// <summary>
/// Provides extension methods for managing the status bar visibility on Android devices.
/// </summary>
/// <remarks>This class contains methods that allow developers to control the visibility of the status bars on
/// Android devices, particularly useful for applications that require full-screen modes or custom UI experiences. The
/// methods are designed to work with Android version 26 and above, utilizing the <see
/// cref="WindowInsetsControllerCompat"/> for managing system bar visibility on Android version 34 and above.</remarks>
public static partial class StatusBarExtensions
{
	static Activity Activity => Platform.CurrentActivity ?? throw new InvalidOperationException("Android Activity can't be null.");

	/// <summary>
	/// Sets the visibility of the status bars on Android devices.
	/// </summary>
	/// <remarks>This method is effective on Android versions 26 and above. On Android version 34 and above, it uses
	/// the <see cref="WindowInsetsControllerCompat"/> to manage the visibility of system bars. Callers must invoke this
	/// method on the UI thread.</remarks>
	/// <param name="hidden"><see langword="true"/> to hide the status bars; otherwise, <see langword="false"/> to show them.</param>
	public static void SetStatusBarsHidden(bool hidden)
	{
		if (Activity.GetCurrentWindow() is not Android.Views.Window { DecorView.RootView: not null } window)
		{
			return;
		}

		View decorView = window.DecorView;
		if (decorView is null || decorView.RootView is null)
		{
			return;
		}
		var insetsController = WindowCompat.GetInsetsController(window, decorView) ?? throw new InvalidOperationException("InsetsController is null");
		
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		{
			if (hidden)
			{
				StatusBar.SetColor(Colors.Transparent);
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