using Foundation;
using UIKit;
using WebKit;
using EpubReader.Service;

#pragma warning disable S1075 // URIs should not be hardcoded

namespace EpubReader.Service;
/// <summary>
/// Custom WKWebView-based auth controller. Injects CSS to hide the Safari
/// toolbar buttons (Share / Refresh) that overlay web content on iPad,
/// and intercepts the OAuth redirect to extract the authorization code.
/// WKWebView fully supports WebAuthn / passkey flows.
/// </summary>
sealed class GoogleAuthWebViewController : UIViewController, IWKNavigationDelegate
{
	readonly string authUrl;
	readonly string callbackScheme;
	readonly Action<string?> onComplete;
	bool completed;

	public GoogleAuthWebViewController(string authUrl, string callbackScheme, Action<string?> onComplete)
	{
		this.authUrl = authUrl;
		this.callbackScheme = callbackScheme;
		this.onComplete = onComplete;

		Title = "Sign in with Google";
		NavigationItem.LeftBarButtonItem = new UIBarButtonItem(
			UIBarButtonSystemItem.Cancel,
			(sender, e) => Finish(null));
	}

	public override void ViewDidLoad()
	{
		base.ViewDidLoad();

		// CSS to suppress the Safari toolbar buttons within the web view.
		const string css = @"
				/* Hide Share and Refresh toolbar overlays injected by the system */
				._sf_toolbar, ._sf_toolbar_container,
				[class*='toolbar'], [class*='Toolbar'],
				[data-original-title='Share'], [data-original-title='Refresh'],
				[aria-label='Share'], [aria-label='Refresh'],
				[title='Share'], [title='Refresh'] { display: none !important; }
				/* Ensure body has enough bottom padding so no content is cut off */
				body { padding-bottom: 0 !important; }";

		var userScript = new WKUserScript(
			new NSString(css),
			WKUserScriptInjectionTime.AtDocumentEnd,
			false);

		var config = new WKWebViewConfiguration();
		config.UserContentController.AddUserScript(userScript);

		var bounds = View is not null ? View.Bounds : UIScreen.MainScreen.Bounds;
		var webView = new WKWebView(bounds, config)
		{
			AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight,
			NavigationDelegate = this
		};
		View?.AddSubview(webView);

		webView.LoadRequest(new NSUrlRequest(new NSUrl(authUrl)));
	}

	void Finish(string? result)
	{
		if (completed)
		{
			return;
		}

		completed = true;
		onComplete(result);
	}

	[Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
	public void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		var url = navigationAction.Request.Url;
		if (url is not null && url.Scheme == callbackScheme)
		{
			// Intercept the OAuth redirect.
			decisionHandler(WKNavigationActionPolicy.Cancel);
			var authCode = AuthenticationService.ExtractAuthCodeFromCallbackUrl(url);
			Finish(authCode);
			return;
		}

		// Block reload navigation (prevents the refresh button from working).
		if (navigationAction.NavigationType == WKNavigationType.Reload)
		{
			decisionHandler(WKNavigationActionPolicy.Cancel);
			return;
		}

		decisionHandler(WKNavigationActionPolicy.Allow);
	}
}