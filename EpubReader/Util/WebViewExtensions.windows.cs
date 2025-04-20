using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Models;
using FFImageLoading;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace EpubReader.Util;
public static partial class WebViewExtensions
{
	static IWebViewHandler? webViewHandler;
	static readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	public static void Initialize(IWebViewHandler handler)
	{
		webViewHandler = handler;
		webViewHandler.PlatformView.CoreWebView2Initialized += WebView2_CoreWebView2Initialized;
	}

	public static void WebView2_Unloaded()
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		webViewHandler.PlatformView.CoreWebView2Initialized -= WebView2_CoreWebView2Initialized;
		webViewHandler.PlatformView.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;
		webViewHandler.PlatformView.CoreWebView2.FrameNavigationCompleted -= CoreWebView2_FrameNavigationCompleted;
		System.Diagnostics.Debug.WriteLine("WebView2 Unloaded");
	}

	static void WebView2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		webViewHandler.PlatformView.CoreWebView2.Settings.AreDevToolsEnabled = true;
		webViewHandler.PlatformView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
		webViewHandler.PlatformView.CoreWebView2.Settings.IsReputationCheckingRequired = false;
		webViewHandler.PlatformView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
		webViewHandler.PlatformView.CoreWebView2.Settings.IsWebMessageEnabled = true;
		webViewHandler.PlatformView.CoreWebView2.Settings.IsScriptEnabled = true;
		webViewHandler.PlatformView.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
		webViewHandler.PlatformView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
		webViewHandler.PlatformView.CoreWebView2.FrameNavigationCompleted += CoreWebView2_FrameNavigationCompleted;
	}

	static async void CoreWebView2_FrameNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		if (args.IsSuccess && args.WebErrorStatus == 0)
		{
			await WebViewExtensions.OnSettingsClicked(webViewHandler);
		}
	}

	static void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		var url = e.Request.Uri ?? string.Empty;
		var filename = Path.GetFileName(url);

		if (url.Contains("https://runcsharp"))
		{
			e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}

		var mimeType = StreamExtensions.GetMimeType(filename);
		var text = streamExtensions.Content(filename);
		if (text is not null)
		{
			var stream = StreamExtensions.GetStream(text);
			var response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
			e.Response = response;
			return;
		}
		var binary = streamExtensions.ByteContent(filename);
		if (binary is not null)
		{
			var stream = StreamExtensions.GetStream(binary);
			var response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
			e.Response = response;
			return;
		}
		e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
	}
	static string GenerateHeaders(string contentType)
	{
		const string baseHeaders = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		string contentTypeHeader = $"Content-Type: {contentType}";
		string completeHeaders = $"{baseHeaders}\r\n{contentTypeHeader}";
		return completeHeaders;
	}

}
