using System.Reflection.Metadata;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WebKit;

namespace EpubReader;
class CustomWebViewNavigationDelegate(IWebViewHandler handler) : MauiWebViewNavigationDelegate(handler)
{
	readonly IWebViewHandler handler = handler;
	/*
	public new void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		var url = navigationAction.Request.Url?.AbsoluteString;
		if (url is not null && url.Contains("https://demo", StringComparison.OrdinalIgnoreCase))
		{
			System.Diagnostics.Debug.WriteLine("CustomWebViewNavigationDelegate: " + url);
			decisionHandler(WKNavigationActionPolicy.Allow);
			return;
		}
		if (url is not null && url.Contains("runcsharp", StringComparison.OrdinalIgnoreCase))
		{
			System.Diagnostics.Debug.WriteLine("CustomWebViewNavigationDelegate: " + url);
			decisionHandler(WKNavigationActionPolicy.Cancel);
			return;
		}
		
		System.Diagnostics.Debug.WriteLine("CustomWebViewNavigationDelegate: " + url);
		base.DecidePolicy(webView, navigationAction, decisionHandler);
	}
1	*/
	// https://stackoverflow.com/questions/37509990/migrating-from-uiwebview-to-wkwebview
	
	public new void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("CustomWebViewNavigationDelegate: " + navigationAction.Request.Url?.AbsoluteString);
		if (handler is null || !handler.IsConnected())
		{
			decisionHandler.Invoke(WKNavigationActionPolicy.Cancel);
			return;
		}

		var platformView = handler?.PlatformView;
		var virtualView = handler?.VirtualView;

		if (platformView is null || virtualView is null)
		{
			decisionHandler.Invoke(WKNavigationActionPolicy.Cancel);
			return;
		}

		var url = navigationAction.Request.Url?.AbsoluteString;
		if (url is not null && url.Contains("https://demo", StringComparison.OrdinalIgnoreCase))
		{
			System.Diagnostics.Trace.WriteLine("CustomWebViewNavigationDelegate: " + url);
			decisionHandler.Invoke(WKNavigationActionPolicy.Allow);
			return;
		}
		if (url is not null && url.Contains("runcsharp", StringComparison.OrdinalIgnoreCase))
		{
			System.Diagnostics.Trace.WriteLine("CustomWebViewNavigationDelegate: " + url);
			decisionHandler.Invoke(WKNavigationActionPolicy.Cancel);
			return;
		}
		System.Diagnostics.Trace.WriteLine("CustomWebViewNavigationDelegate: " + url);
		var navEvent = WebNavigationEvent.NewPage;
		var navigationType = navigationAction.NavigationType;

		switch (navigationType)
		{
			case WKNavigationType.LinkActivated:
				navEvent = WebNavigationEvent.NewPage;

				if (navigationAction.TargetFrame == null)
				{
					webView?.LoadRequest(navigationAction.Request);
				}

				break;
			case WKNavigationType.FormSubmitted:
				navEvent = WebNavigationEvent.NewPage;
				break;
			case WKNavigationType.BackForward:
				navEvent = CurrentNavigationEvent;
				break;
			case WKNavigationType.Reload:
				navEvent = WebNavigationEvent.Refresh;
				break;
			case WKNavigationType.FormResubmitted:
				navEvent = WebNavigationEvent.NewPage;
				break;
			case WKNavigationType.Other:
				navEvent = WebNavigationEvent.NewPage;
				break;
		}

		var request = navigationAction.Request;
		var lastUrl = request.Url?.ToString() ?? throw new InvalidNavigationException("Url cannot be null.");

		bool cancel = virtualView.Navigating(navEvent, lastUrl);
		decisionHandler(cancel ? WKNavigationActionPolicy.Cancel : WKNavigationActionPolicy.Allow);
	}

	internal WebNavigationEvent CurrentNavigationEvent
	{
		get;
		set;
	}

}