﻿using Android.Graphics;
using Android.Webkit;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Platforms.Android;
using EpubReader.Util;
using Microsoft.Maui.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EpubReader;
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
		handler.PlatformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
		handler.PlatformView.Settings.LoadWithOverviewMode = true;
		handler.PlatformView.Settings.UseWideViewPort = true;
		handler.PlatformView.Settings.TextZoom = 100;
	}
	public override global::Android.Webkit.WebResourceResponse? ShouldInterceptRequest(global::Android.Webkit.WebView? view, global::Android.Webkit.IWebResourceRequest? request)
	{
		var url = request?.Url?.ToString() ?? string.Empty;
		if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
		{
			return base.ShouldInterceptRequest(view, request);
		}

		var filename = System.IO.Path.GetFileName(url);
		var mimeType = FileService.GetMimeType(filename);
		var text = streamExtensions.Content(filename);
		if (text is not null)
		{
			var stream = StreamExtensions.GetStream(text);
			return WebResourceResponseHelper.CreateFromHtmlString(stream, mimeType, 200, "OK");
		}
		var binary = streamExtensions.ByteContent(filename);
		if (binary is not null)
		{
			var stream = StreamExtensions.GetStream(binary);
			return WebResourceResponseHelper.CreateFromHtmlString(stream, mimeType, 200, "OK");
		}
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

		if (path.Contains(csharp))
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