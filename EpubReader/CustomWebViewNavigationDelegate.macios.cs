using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using WebKit;

namespace EpubReader;
class CustomWebViewNavigationDelegate : WKNavigationDelegate
{
	readonly IWebViewHandler handler;
	public CustomWebViewNavigationDelegate(IWebViewHandler handler)
	{
		this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
		System.Diagnostics.Debug.WriteLine($"CustomWebViewNavigationDelegate {handler}");
	}
	public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
	{
		var url = webView.Url?.AbsoluteString ?? throw new InvalidOperationException("url is null");
		System.Diagnostics.Debug.WriteLine($"DidFinishNavigation: {url}");
		
		handler.VirtualView?.Navigated(WebNavigationEvent.NewPage, url, WebNavigationResult.Success);
	}
	public override void DidCommitNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Trace.WriteLine("DidCommitNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, WKWebpagePreferences preferences, Action<WKNavigationActionPolicy, WKWebpagePreferences> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("DecidePolicy Action: " + navigationAction.Request.Url?.AbsoluteString);
		
		var path = navigationAction.Request.Url?.AbsoluteString ?? throw new InvalidOperationException("path is null");
		if (path.Contains("https://runcsharp") is true)
		{
			var urlParts = path.Split('.');
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				WeakReferenceMessenger.Default.Send(new JavaScriptMessage("next"));
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				WeakReferenceMessenger.Default.Send(new JavaScriptMessage("prev"));
			}
			if (methodName.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
			{
				WeakReferenceMessenger.Default.Send(new JavaScriptMessage("pageLoad"));
			}

			decisionHandler(WKNavigationActionPolicy.Cancel, preferences);
				return;
		}
		decisionHandler(WKNavigationActionPolicy.Allow, preferences);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse, Action<WKNavigationResponsePolicy> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("DecidePolicy Response: " + navigationResponse.Response.Url?.AbsoluteString);
		decisionHandler(WKNavigationResponsePolicy.Allow);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("DecidePolicyAction2: " + navigationAction.Request.Url?.AbsoluteString);
		decisionHandler(WKNavigationActionPolicy.Allow);
	}

	public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Trace.WriteLine("DidStartProvisionalNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
	{
		System.Diagnostics.Trace.WriteLine("DidFailNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
	{
		System.Diagnostics.Trace.WriteLine("DidFailProvisionalNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DidReceiveServerRedirectForProvisionalNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Trace.WriteLine("DidReceiveServerRedirectForProvisionalNavigation: " + webView.Url?.AbsoluteString);
	}
}