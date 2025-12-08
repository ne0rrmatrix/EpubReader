using Microsoft.Maui.Handlers;
using WebKit;

namespace EpubReader.Controls;

/// <summary>
/// Provides custom navigation handling for a <see cref="WKWebView"/> by implementing the <see
/// cref="WKNavigationDelegate"/> interface.
/// </summary>
/// <remarks>This delegate is responsible for managing navigation events in a web view, allowing for custom
/// handling of navigation actions and responses. It interacts with an <see cref="IWebViewHandler"/> to notify about
/// navigation events and to make decisions about navigation policies.</remarks>
/// <param name="handler"></param>
class CustomWebViewNavigationDelegate(IWebViewHandler handler) : WKNavigationDelegate
{
	readonly IWebViewHandler handler = handler ?? throw new ArgumentNullException(nameof(handler));

	/// <summary>
	/// Handles the completion of a navigation in the web view.
	/// </summary>
	/// <remarks>This method is called when the web view has successfully completed loading a page. It triggers the
	/// <see cref="handler.VirtualView"/> to update its state based on the navigation event.</remarks>
	/// <param name="webView">The <see cref="WKWebView"/> that finished navigating.</param>
	/// <param name="navigation">The <see cref="WKNavigation"/> object representing the navigation action.</param>
	/// <exception cref="InvalidOperationException">Thrown if the URL of the web view is null.</exception>
	public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
	{
		var url = webView.Url?.AbsoluteString ?? throw new InvalidOperationException("url is null");
		handler.VirtualView?.Navigated(WebNavigationEvent.NewPage, url, WebNavigationResult.Success);
	}

	/// <summary>
	/// Determines the navigation policy for a given web view navigation action.
	/// </summary>
	/// <remarks>This method evaluates the URL of the navigation request. If the URL contains query parameters or 
	/// matches a specific pattern ("https://runcsharp"), it sends a message using <see cref="WeakReferenceMessenger"/> 
	/// and cancels the navigation. Otherwise, it allows the navigation to proceed.</remarks>
	/// <param name="webView">The <see cref="WKWebView"/> instance that initiated the navigation action.</param>
	/// <param name="navigationAction">The <see cref="WKNavigationAction"/> representing the navigation request.</param>
	/// <param name="preferences">The <see cref="WKWebpagePreferences"/> associated with the navigation action.</param>
	/// <param name="decisionHandler">An <see cref="Action{T1, T2}"/> delegate that takes a <see cref="WKNavigationActionPolicy"/> and  <see
	/// cref="WKWebpagePreferences"/> as parameters, used to specify the navigation policy.</param>
	/// <exception cref="InvalidOperationException">Thrown if the URL of the navigation request is null.</exception>
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, WKWebpagePreferences preferences, Action<WKNavigationActionPolicy, WKWebpagePreferences> decisionHandler)
	{
		var path = navigationAction.Request.Url?.AbsoluteString ?? throw new InvalidOperationException("path is null");
		var url = path.Split('?');

		if (url.Length > 1 || path.Contains("https://runcsharp"))
		{
			WeakReferenceMessenger.Default.Send(new JavaScriptMessage(path));
			decisionHandler(WKNavigationActionPolicy.Cancel, preferences);
			return;
		}
		decisionHandler(WKNavigationActionPolicy.Allow, preferences);
	}

	/// <summary>
	/// Determines the navigation policy for a given web view and navigation response.
	/// </summary>
	/// <remarks>This method is typically used to decide whether a navigation request should be allowed or blocked
	/// based on the response.</remarks>
	/// <param name="webView">The web view that initiated the navigation request.</param>
	/// <param name="navigationResponse">The navigation response containing information about the navigation request.</param>
	/// <param name="decisionHandler">A callback to invoke with the decision on how to handle the navigation.  Pass <see
	/// cref="WKNavigationResponsePolicy.Allow"/> to allow the navigation, or <see
	/// cref="WKNavigationResponsePolicy.Cancel"/> to cancel it.</param>
	public override void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse, Action<WKNavigationResponsePolicy> decisionHandler)
	{
		decisionHandler(WKNavigationResponsePolicy.Allow);
	}

	/// <summary>
	/// Determines the navigation policy for a given web view and navigation action.
	/// </summary>
	/// <remarks>This method is typically used to control navigation behavior in a web view, such as  deciding
	/// whether to load a particular URL or block it based on custom logic.</remarks>
	/// <param name="webView">The web view that initiated the navigation request.</param>
	/// <param name="navigationAction">The navigation action containing details about the request.</param>
	/// <param name="decisionHandler">A callback to invoke with the decision on whether to allow or cancel the navigation.  Pass <see
	/// cref="WKNavigationActionPolicy.Allow"/> to allow the navigation.</param>
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		decisionHandler(WKNavigationActionPolicy.Allow);
	}
}