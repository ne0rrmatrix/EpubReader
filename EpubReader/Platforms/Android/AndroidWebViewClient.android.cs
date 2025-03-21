using Android.Graphics;
using Android.Webkit;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Message;
using Microsoft.Maui.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EpubReader;
#pragma warning restore IDE0130 // Namespace does not match folder structure


class CustomWebViewClient(IWebViewHandler webView) : WebViewClient
{
	readonly Microsoft.Maui.Controls.WebView webView = webView?.VirtualView as Microsoft.Maui.Controls.WebView ?? throw new InvalidOperationException($"{nameof(Microsoft.Maui.Controls.WebView)} cannot be null");
	public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		var path = request?.Url?.ToString() ?? string.Empty;
		if (request is null || request.Url is null || path.Contains("file:///android_asset/"))
		{
			return true;
		}

		if(path.Contains("runcsharp"))
		{
			var urlParts =path.Split('.');
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
		return base.ShouldOverrideUrlLoading(view, request);
	}
	
	public override void OnPageStarted(Android.Webkit.WebView? view, string? url, Bitmap? favicon)
	{
		if(url is null || url.Contains("runcsharp"))
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
		if(url is null || url.Contains("runcsharp"))
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
