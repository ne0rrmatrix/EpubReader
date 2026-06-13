using Foundation;
using Network;
using UIKit;

namespace EpubReader;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		// Firebase must be configured before ANY Firebase API is accessed.
		// This is the earliest possible entry point on iOS.
		try
		{
			global::Firebase.Core.App.Configure();
			System.Diagnostics.Trace.TraceInformation("Firebase configured in AppDelegate.FinishedLaunching");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceError($"Firebase configuration failed: {ex.Message}");
		}

		return base.FinishedLaunching(application, launchOptions);
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}