using UIKit;

namespace EpubReader.Service;

class FullScreenService : IFullScreenService
{
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

		// Hide the status bar (time, battery, etc.) and home indicator.
		UIApplication.SharedApplication.StatusBarHidden = true;

		if (OperatingSystem.IsIOSVersionAtLeast(13) || OperatingSystem.IsMacCatalystVersionAtLeast(13))
		{
			var vc = Platform.GetCurrentUIViewController();
			if (vc is not null)
			{
				System.Diagnostics.Trace.TraceInformation("Entering full screen: hiding status bar");
				vc.PrefersStatusBarHidden();
				vc.SetNeedsUpdateOfHomeIndicatorAutoHidden();
				vc.SetNeedsStatusBarAppearanceUpdate();
			}
		}

		IsFullScreen = true;
	}

	public void ExitFullScreen()
	{
		if (!IsFullScreen)
		{
			return;
		}

		// Restore the status bar and home indicator.
		UIApplication.SharedApplication.StatusBarHidden = false;

		if (OperatingSystem.IsIOSVersionAtLeast(13) || OperatingSystem.IsMacCatalystVersionAtLeast(13))
		{
			var vc = Platform.GetCurrentUIViewController();
			if (vc is not null)
			{
				System.Diagnostics.Trace.TraceInformation("Exiting full screen: restoring status bar");
				vc.PrefersStatusBarHidden();
				vc.SetNeedsUpdateOfHomeIndicatorAutoHidden();
				vc.SetNeedsStatusBarAppearanceUpdate();
			}
		}

		IsFullScreen = false;
	}
}