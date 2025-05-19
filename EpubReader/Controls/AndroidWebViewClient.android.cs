using Android.Graphics;
using Android.Webkit;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;
using Microsoft.Maui.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EpubReader.Controls;
#pragma warning restore IDE0130 // Namespace does not match folder structure

class CustomWebViewClient : WebViewClient
{
	const string csharp = "runcsharp";
	readonly Microsoft.Maui.Controls.WebView webView;
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	public CustomWebViewClient(IWebViewHandler handler)
	{
		this.webView = handler.VirtualView as Microsoft.Maui.Controls.WebView ?? throw new ArgumentNullException(nameof(handler));
		handler.PlatformView.Settings.DomStorageEnabled = true;
		handler.PlatformView.Settings.JavaScriptEnabled = true;
		handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
		handler.PlatformView.Settings.AllowContentAccess = true;
		handler.PlatformView.Settings.LoadsImagesAutomatically = true;
		handler.PlatformView.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
		handler.PlatformView.Settings.LoadWithOverviewMode = true;
		handler.PlatformView.Settings.UseWideViewPort = true;
		handler.PlatformView.Settings.TextZoom = 100;
		handler.PlatformView.VerticalScrollBarEnabled = false;
		handler.PlatformView.HorizontalScrollBarEnabled = false;
	}

	public override WebResourceResponse? ShouldInterceptRequest(global::Android.Webkit.WebView? view, global::Android.Webkit.IWebResourceRequest? request)
	{
		var url = request?.Url?.ToString() ?? string.Empty;
		if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
		{
			return base.ShouldInterceptRequest(view, request);
		}

		var filename = System.IO.Path.GetFileName(url);
		var mimeType = FileService.GetMimeType(filename);
		var stream = streamExtensions.GetStream(url);
		return WebResourceResponseHelper.CreateFromHtmlString(stream, mimeType, 200, "OK") ?? base.ShouldInterceptRequest(view, request);
	}
	public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		var path = request?.Url?.ToString() ?? string.Empty;
		var url = path.Split('?');
		if (request is null || request.Url is null)
		{
			return true;
		}
		if(url.Length > 1 || path.Contains(csharp))
		{
			WeakReferenceMessenger.Default.Send(new JavaScriptMessage(path));
			return true;
		}
		return false;
	}

	public override void OnPageStarted(global::Android.Webkit.WebView? view, string? url, Bitmap? favicon)
	{
		if (url is null || url.Contains(csharp))
		{
			return;
		}
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
		if (url is null || url.Contains(csharp))
		{
			return;
		}
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