using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using Microsoft.Maui.Handlers;
using WebKit;

namespace EpubReader.Controls;
class CustomWebViewNavigationDelegate(IWebViewHandler handler) : WKNavigationDelegate
{
	readonly IWebViewHandler handler = handler ?? throw new ArgumentNullException(nameof(handler));

	public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
	{
		var url = webView.Url?.AbsoluteString ?? throw new InvalidOperationException("url is null");
		handler.VirtualView?.Navigated(WebNavigationEvent.NewPage, url, WebNavigationResult.Success);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, WKWebpagePreferences preferences, Action<WKNavigationActionPolicy, WKWebpagePreferences> decisionHandler)
	{	
		var path = navigationAction.Request.Url?.AbsoluteString ?? throw new InvalidOperationException("path is null");
		if (path.Contains("https://runcsharp"))
		{
			System.Diagnostics.Debug.WriteLine($"Intercepted csharp request");
			var urlParts = path.Split('.');
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			WeakReferenceMessenger.Default.Send(new JavaScriptMessage(methodName));
			decisionHandler(WKNavigationActionPolicy.Cancel, preferences);
				return;
		}
		decisionHandler(WKNavigationActionPolicy.Allow, preferences);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse, Action<WKNavigationResponsePolicy> decisionHandler)
	{
		decisionHandler(WKNavigationResponsePolicy.Allow);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		decisionHandler(WKNavigationActionPolicy.Allow);
	}
}