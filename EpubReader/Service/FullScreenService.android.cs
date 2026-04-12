using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using View = Android.Views.View;
using Window = Android.Views.Window;

namespace EpubReader.Service;


class FullScreenService : IFullScreenService
{
	int defaultSystemUiVisibility;
	bool wasSystemBarVisible;

	// Lists to manage multiple overlays (CommunityToolkit.Maui bug: StatusBar has no tag, so multiple overlays can be created)
	readonly List<View> statusBarOverlays = [];
	View? navigationBarOverlay;
	public bool IsFullScreen { get; set; }

	public void SetFullScreen(bool enable)
	{
		if (enable)
		{
			EnterFullScreen();
		}
		else
		{
			ExitFullScreen();
		}
	}

	public void EnterFullScreen()
	{
		if (IsFullScreen)
		{
			return;
		}

		RunOnUiThread(() =>
		{
			if (CurrentWindow is not { DecorView: { IsAttachedToWindow: true } decorView } window)
			{
				return;
			}

			if (OperatingSystem.IsAndroidVersionAtLeast(28))
			{
				window.Attributes!.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
			}

			ApplyHide(window, decorView);

			// Delayed calls to work around aggressive OEMs (MIUI, etc.)
			foreach (var delay in (int[])[150, 500, 1000])
			{
				decorView.PostDelayed(() => ApplyHide(window, decorView), delay);
			}

			IsFullScreen = true;
		});
	}

	public void ExitFullScreen()
	{
		if (!IsFullScreen)
		{
			return;
		}

		RunOnUiThread(() =>
		{
			if (CurrentWindow is not { DecorView: { IsAttachedToWindow: true } decorView } window)
			{
				return;
			}

			RestoreSystemBars(window, decorView);
			IsFullScreen = false;
		});
	}

	void ApplyHide(Window window, View decorView)
	{
		var controller = WindowCompat.GetInsetsController(window, decorView);
		var barTypes = WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.NavigationBars();

		// Android 35+: Hide CommunityToolkit.Maui overlays
		if (OperatingSystem.IsAndroidVersionAtLeast(35))
		{
			var decorGroup = (ViewGroup)decorView;

			// Hide ALL StatusBar overlays (CTK bug: multiple can exist because no tag is set)
			foreach (var overlay in FindAllStatusBarOverlays(decorGroup))
			{
				overlay.Visibility = ViewStates.Gone;
				if (!statusBarOverlays.Contains(overlay))
				{
					statusBarOverlays.Add(overlay);
				}
			}

			if ((navigationBarOverlay = decorGroup.FindViewWithTag("NavigationBarOverlay")) is not null)
			{
				navigationBarOverlay.Visibility = ViewStates.Gone;
			}

			window.ClearFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
		}

		WindowCompat.SetDecorFitsSystemWindows(window, false);

		if (OperatingSystem.IsAndroidVersionAtLeast(30))
		{
			if (decorView.RootWindowInsets is not { } insets)
			{
				return;
			}

			wasSystemBarVisible = insets.IsVisible(WindowInsetsCompat.Type.NavigationBars()) || insets.IsVisible(WindowInsetsCompat.Type.StatusBars());
			if (wasSystemBarVisible)
			{
				window.InsetsController?.Hide(WindowInsets.Type.SystemBars());
			}
		}
		else
		{
			defaultSystemUiVisibility = (int)decorView.SystemUiFlags;
			decorView.SystemUiFlags = SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation
				| SystemUiFlags.LayoutFullscreen | SystemUiFlags.HideNavigation
				| SystemUiFlags.Fullscreen | SystemUiFlags.ImmersiveSticky;
		}

		window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.LayoutNoLimits);
		window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);
		controller?.Hide(barTypes);
		controller?.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
	}

	void RestoreSystemBars(Window window, View decorView)
	{
		var controller = WindowCompat.GetInsetsController(window, decorView);
		var barTypes = WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.NavigationBars();

		if (OperatingSystem.IsAndroidVersionAtLeast(35))
		{
			window.ClearFlags(WindowManagerFlags.LayoutNoLimits);
			window.SetFlags(WindowManagerFlags.DrawsSystemBarBackgrounds, WindowManagerFlags.DrawsSystemBarBackgrounds);

			// Restore ALL StatusBar overlays
			foreach (var overlay in statusBarOverlays)
			{
				overlay.Visibility = ViewStates.Visible;
			}

			statusBarOverlays.Clear();

			navigationBarOverlay?.Visibility = ViewStates.Visible;
			navigationBarOverlay = null;
		}

		if (OperatingSystem.IsAndroidVersionAtLeast(30))
		{
			if (wasSystemBarVisible)
			{
				window.InsetsController?.Show(WindowInsets.Type.SystemBars());
			}
		}
		else
		{
			decorView.SystemUiFlags = (SystemUiFlags)defaultSystemUiVisibility;
		}

		controller?.Show(barTypes);
		controller?.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorDefault;

		window.AddFlags(WindowManagerFlags.ForceNotFullscreen);
		window.ClearFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.LayoutNoLimits);

		if (OperatingSystem.IsAndroidVersionAtLeast(28))
		{
			window.Attributes!.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Default;
		}

		WindowCompat.SetDecorFitsSystemWindows(window, true);
	}

	#region Helpers

	static Window? CurrentWindow => Platform.CurrentActivity?.Window;

	static void RunOnUiThread(Action action) => Platform.CurrentActivity?.RunOnUiThread(() =>
	{
		try { action(); }
		catch (Exception ex) { Log.Error("FullscreenService", ex.Message); }
	});

	/// <summary>
	/// Finds ALL StatusBarOverlays from CommunityToolkit.Maui.
	/// CTK bug: StatusBar has no tag defined, so FindViewWithTag doesn't work
	/// and a new overlay is created on each color change → multiple stacked overlays.
	/// </summary>
	static List<View> FindAllStatusBarOverlays(ViewGroup decorGroup)
	{
		var overlays = new List<View>();
		var resources = Platform.CurrentActivity?.Resources;
		if (resources is null)
		{
			return overlays;
		}

		var heightId = resources.GetIdentifier("status_bar_height", "dimen", "android");
		var expectedHeight = (heightId > 0 ? resources.GetDimensionPixelSize(heightId) : 0) + 3;

		for (var i = 0; i < decorGroup.ChildCount; i++)
		{
			if (decorGroup.GetChildAt(i) is { LayoutParameters: FrameLayout.LayoutParams { Gravity: GravityFlags.Top, Width: ViewGroup.LayoutParams.MatchParent } lp } child
				&& lp.Height == expectedHeight)
			{
				overlays.Add(child);
			}
		}
		return overlays;
	}

	#endregion
}
