using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace EpubReader;

/// <summary>
/// Custom WebAuthenticator callback activity for handling OAuth redirects on Android.
/// This activity must be present to intercept the callback URL after OAuth authentication.
/// The IntentFilter matches the callback URL scheme used in WebAuthenticator.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
	new[] { Intent.ActionView },
	Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
	DataScheme = "com.googleusercontent.apps.507277680982-hejogn5jq19j8kvmmr8teei2odg3rlda",
	DataHost = "oauth2redirect",
	DataPathPrefix = "/")]

public class WebAuthenticatorActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
