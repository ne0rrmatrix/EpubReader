using EpubReader.Util;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace EpubReader.Controls;

/// <summary>
/// Provides extension methods for handling WebView2 controls within a .NET MAUI application.
/// </summary>
/// <remarks>This static class includes methods to initialize and manage the lifecycle of WebView2 controls,
/// including event subscription and unsubscription to ensure proper resource management. It is designed to be used in
/// conjunction with the <see cref="IWebViewHandler"/> interface.</remarks>
public static partial class WebViewExtensions
{
	static readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	static IWebViewHandler? webViewHandler;

	/// <summary>
	/// Initializes the WebView handler and sets up the CoreWebView2 initialization event.
	/// </summary>
	/// <remarks>This method assigns the provided handler to the internal web view handler and subscribes to the
	/// CoreWebView2 initialization event. Ensure that the handler is properly configured before calling this
	/// method.</remarks>
	/// <param name="handler">The <see cref="IWebViewHandler"/> instance to be initialized. Cannot be null.</param>
	public static void Initialize(IWebViewHandler handler)
	{
		webViewHandler = handler;
		webViewHandler.PlatformView.CoreWebView2Initialized += WebView2_CoreWebView2Initialized;
	}

	/// <summary>
	/// Unsubscribes from events related to the WebView2 control when it is unloaded.
	/// </summary>
	/// <remarks>This method detaches event handlers from the WebView2 control to prevent memory leaks and ensure
	/// proper cleanup when the control is no longer in use. It is important to call this method during the unloading
	/// process of the WebView2 control.</remarks>
	public static void WebView2_Unloaded()
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		webViewHandler.PlatformView.CoreWebView2Initialized -= WebView2_CoreWebView2Initialized;
		webViewHandler.PlatformView.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;
		webViewHandler.PlatformView.CoreWebView2.DownloadStarting -= CoreWebView2_DownloadStarting;
	}

	static void WebView2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		var settings = webViewHandler.PlatformView.CoreWebView2.Settings;
		var coreWebView = webViewHandler.PlatformView.CoreWebView2;
		settings.AreDevToolsEnabled = true;
		settings.AreBrowserAcceleratorKeysEnabled = true;
		settings.IsReputationCheckingRequired = false;
		settings.AreHostObjectsAllowed = true;
		settings.IsWebMessageEnabled = true;
		settings.IsScriptEnabled = true;
		coreWebView.DownloadStarting += CoreWebView2_DownloadStarting;
		coreWebView.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
		coreWebView.WebResourceRequested += CoreWebView2_WebResourceRequested;
	}

	static void CoreWebView2_DownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
	{
		args.Handled = true;
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
		var stream = streamExtensions.GetStream(url);
		if (stream is null)
		{
			e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}
		e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
	}
	static string GenerateHeaders(string contentType)
	{
		const string baseHeaders = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		string contentTypeHeader = $"Content-Type: {contentType}";
		string completeHeaders = $"{baseHeaders}\r\n{contentTypeHeader}";
		return completeHeaders;
	}

}
