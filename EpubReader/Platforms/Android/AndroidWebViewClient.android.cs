using System.Text;
using Android.Graphics;
using Android.Webkit;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Platforms.Android;
using EpubReader.Service;
using EpubReader.Util;
using Microsoft.Maui.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EpubReader;
#pragma warning restore IDE0130 // Namespace does not match folder structure

class CustomWebViewClient : WebViewClient
{
	readonly Microsoft.Maui.Controls.WebView webView;

	public CustomWebViewClient(IWebViewHandler handler)
	{
		handler.PlatformView.Settings.DomStorageEnabled = true;
		handler.PlatformView.Settings.JavaScriptEnabled = true;
		handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
		handler.PlatformView.Settings.AllowContentAccess = true;
		handler.PlatformView.Settings.LoadsImagesAutomatically = true;
		handler.PlatformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
		handler.PlatformView.Settings.LoadWithOverviewMode = true;
		this.webView = handler.VirtualView as Microsoft.Maui.Controls.WebView ?? throw new ArgumentNullException(nameof(handler));
	}
	public override global::Android.Webkit.WebResourceResponse? ShouldInterceptRequest(global::Android.Webkit.WebView? view, global::Android.Webkit.IWebResourceRequest? request)
	{
		var url = request?.Url?.ToString() ?? string.Empty;

		if (url.StartsWith("data:text/html"))
		{
			System.Diagnostics.Debug.WriteLine("ShouldInterceptRequest: Load web page data from page that is loaded using string value.");
			return base.ShouldInterceptRequest(view, request);
		}

		string path = System.IO.Path.GetFileName(request?.Url?.ToString())?.TrimEnd('/') ?? string.Empty;

		if (ThreadSafeFileWriter.FileExists(path))
		{
			System.Diagnostics.Debug.WriteLine($"File exists: {path}");
			string mimeType = ThreadSafeFileWriter.GetMimeType(path);
			System.Diagnostics.Debug.WriteLine("Path: " + path);
			Stream contentStream = ThreadSafeFileWriter.ReadFileStream(path);

			var response = WebResourceResponseHelper.CreateFromHtmlString(contentStream, mimeType, 200, "OK");
			return response;

		}
		System.Diagnostics.Debug.WriteLine($"Path: {path}");
		System.Diagnostics.Debug.WriteLine(url);
		System.Diagnostics.Debug.WriteLine("Returning base");
		return base.ShouldInterceptRequest(view, request);
	}

	public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		System.Diagnostics.Debug.WriteLine("Function: ShouldOverrideUrlLoading has been called");
		var path = request?.Url?.ToString() ?? string.Empty;
		if (request is null || request.Url is null)
		{
			return true;
		}

		if (path.Contains("runcsharp"))
		{
			System.Diagnostics.Debug.WriteLine("runcsharp found");
			var urlParts = path.Split('.');
			if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
			{
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
			}
			return true;
		}
		return false;
	}

	public override void OnPageStarted(global::Android.Webkit.WebView? view, string? url, Bitmap? favicon)
	{
		System.Diagnostics.Debug.WriteLine("Function: OnPageStarted has been called");
		if (url is null || url.Contains("runcsharp"))
		{
			return;
		}
		System.Diagnostics.Debug.WriteLine("OnPageStarted: Load web page or data.");
		System.Diagnostics.Debug.WriteLine($"URL: {url}");
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
		System.Diagnostics.Debug.WriteLine("Function: OnPageFinished has been called");
		if (url is null || url.Contains("runcsharp"))
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