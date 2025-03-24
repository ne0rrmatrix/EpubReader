using Android.Graphics;
using Android.Webkit;
using Microsoft.Maui.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EpubReader;
#pragma warning restore IDE0130 // Namespace does not match folder structure

class CustomWebViewClient : WebViewClient
{
	readonly Microsoft.Maui.Controls.HybridWebView webView;
	public CustomWebViewClient(IHybridWebViewHandler handler)
	{
		if (handler is null)
		{
			throw new InvalidOperationException(nameof(handler));
		}
			webView = handler.VirtualView as Microsoft.Maui.Controls.HybridWebView ?? throw new InvalidOperationException($"{nameof(Microsoft.Maui.Controls.HybridWebView)} cannot be null");
		handler.PlatformView.Settings.AllowFileAccess = true;
		handler.PlatformView.Settings.AllowFileAccessFromFileURLs = true;
		handler.PlatformView.Settings.AllowUniversalAccessFromFileURLs = true;
		handler.PlatformView.Settings.AllowContentAccess = true;
		handler.PlatformView.Settings.DomStorageEnabled = true;
		handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
		handler.PlatformView.Settings.JavaScriptEnabled = true;
		handler.PlatformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
		handler.PlatformView.Settings.SaveFormData = true;
		handler.PlatformView.Settings.UseWideViewPort = true;
		handler.PlatformView.Settings.LoadWithOverviewMode = true;
		handler.VirtualView.HybridRoot
	}
	public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		return base.ShouldOverrideUrlLoading(view, request);
	}

	public override void OnPageStarted(Android.Webkit.WebView? view, string? url, Bitmap? favicon)
	{
		base.OnPageStarted(view, url, favicon);
		var navigatedEventArgs = new WebNavigatingEventArgs(
		WebNavigationEvent.NewPage,
		new HtmlWebViewSource { Html = url }, url);

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
		var navigatingEvent = webView.GetType().GetField(nameof(Microsoft.Maui.Controls.WebView.Navigating), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
		if (navigatingEvent?.GetValue(webView) is MulticastDelegate eventDelegate)
		{
			foreach (var handler in eventDelegate.GetInvocationList())
			{
				handler.Method.Invoke(handler.Target, [webView, navigatedEventArgs]);
			}
		}
	}

	public override void OnPageFinished(global::Android.Webkit.WebView? view, string? url)
	{
		base.OnPageFinished(view, url);
		var navigatedEventArgs = new WebNavigatedEventArgs(
		WebNavigationEvent.NewPage,
		url,
		null,
		WebNavigationResult.Success
		);

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
		var navigatedEvent = webView.GetType().GetField(nameof(Microsoft.Maui.Controls.WebView.Navigated), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
		if (navigatedEvent?.GetValue(webView) is MulticastDelegate eventDelegate)
		{
			foreach (var handler in eventDelegate.GetInvocationList())
			{
				handler.Method.Invoke(handler.Target, [webView, navigatedEventArgs]);
			}
		}
	}
}