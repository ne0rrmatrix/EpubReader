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
		
		if(OperatingSystem.IsIOSVersionAtLeast(15) || OperatingSystem.IsMacCatalystVersionAtLeast(15))
		{
			// Hide the status bar (time, battery, etc.) and home indicator.
#pragma warning disable CA1422 // Validate platform compatibility
			UIApplication.SharedApplication.StatusBarHidden = true;
#pragma warning restore CA1422 // Validate platform compatibility

		}

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

		if(OperatingSystem.IsIOSVersionAtLeast(15) || OperatingSystem.IsMacCatalystVersionAtLeast(15))
		{
			// Restore the status bar and home indicator.
#pragma warning disable CA1422 // Validate platform compatibility
			UIApplication.SharedApplication.StatusBarHidden = false;
#pragma warning restore CA1422 // Validate platform compatibility
		}
		
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