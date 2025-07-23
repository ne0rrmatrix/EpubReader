using Android.Views;
using AndroidX.Core.View;

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
	static Android.Views.Window window => Platform.CurrentActivity?.Window ?? throw new InvalidOperationException("Current activity is null");
	static Android.Views.View decorView => window.DecorView ?? throw new InvalidOperationException("DecorView is null");
	static AndroidX.Core.View.WindowInsetsControllerCompat insetsController => WindowCompat.GetInsetsController(window, decorView) ?? throw new InvalidOperationException("InsetsController is null");
	
	/// <summary>
	/// Sets the visibility of the status bars on Android devices.
	/// </summary>
	/// <remarks>This method is effective on Android versions 26 and above. On Android version 34 and above, it uses
	/// the <see cref="WindowInsetsControllerCompat"/> to manage the visibility of system bars.</remarks>
	/// <param name="hidden"><see langword="true"/> to hide the status bars; otherwise, <see langword="false"/> to show them.</param>
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
