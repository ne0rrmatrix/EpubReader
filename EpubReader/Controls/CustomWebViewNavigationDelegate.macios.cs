using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using WebKit;

namespace EpubReader.Controls;
class CustomWebViewNavigationDelegate : WKNavigationDelegate
{
	public CustomWebViewNavigationDelegate()
	{
	}

	public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Debug.WriteLine($"DidFinishNavigation {webView.Url?.AbsoluteString}");
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		var url = navigationAction.Request.Url?.AbsoluteString ?? string.Empty;
		if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
		{
			System.Diagnostics.Debug.WriteLine($"DecidePolicy {url}");
			decisionHandler(WKNavigationActionPolicy.Allow);
			return;
		}
		if (url.Contains("runcsharp"))
		{
			System.Diagnostics.Debug.WriteLine($"DecidePolicy {url}");
			var urlParts = url.Split('.');
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
			decisionHandler(WKNavigationActionPolicy.Cancel);
			return;
		}
		base.DecidePolicy(webView, navigationAction, decisionHandler);
	}
	/*
	public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
	{
		
		System.Diagnostics.Debug.WriteLine($"DidStartProvisionalNavigation {webView.Url?.AbsoluteString}");
		var path = webView.Url?.AbsoluteString ?? string.Empty;
		if (path.Contains("runcsharp"))
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
			webView.StopLoading();
		}
	}
	*/
}