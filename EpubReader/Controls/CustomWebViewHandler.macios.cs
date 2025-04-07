using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WebKit;

namespace EpubReader.Controls;
class CustomWebViewHandler : WebViewHandler
{
	public CustomWebViewHandler()
	{
		WebViewHandler.PlatformViewFactory = (handler) =>
		{
			WKWebViewConfiguration config = MauiWKWebView.CreateConfiguration();
			config.Preferences.JavaScriptEnabled = true;
			config.Preferences.JavaScriptCanOpenWindowsAutomatically = true;
			config.Preferences.SetValueForKey(new NSNumber(true), new NSString("allowFileAccessFromFileURLs"));
			config.Preferences.SetValueForKey(new NSNumber(true), new NSString("allowUniversalAccessFromFileURLs"));
			config.ApplicationNameForUserAgent = "MyProduct/1.0.0";
			handler.PlatformView.NavigationDelegate = new CustomWebViewNavigationDelegate();
			return new CustomWebView(CGRect.Empty, config);
		};
	}

	protected override void ConnectHandler(WKWebView platformView)
	{
		base.ConnectHandler(platformView);
	}
	protected override void DisconnectHandler(WKWebView platformView)
	{
		base.DisconnectHandler(platformView);
	}
}
