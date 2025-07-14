﻿using EpubReader.Util;
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

	/// <summary>
	/// Initializes the settings and event handlers for a WebView2 instance when the CoreWebView2 is initialized.
	/// </summary>
	/// <remarks>This method configures various settings for the WebView2, such as enabling developer tools and
	/// script execution. It also sets up event handlers for download starting and web resource requests.</remarks>
	/// <param name="sender">The WebView2 instance that triggered the initialization event.</param>
	/// <param name="args">The event data associated with the CoreWebView2 initialization.</param>
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

	/// <summary>
	/// Handles the event that occurs when a download is starting in the WebView2 control.
	/// </summary>
	/// <remarks>This method sets the <see cref="CoreWebView2DownloadStartingEventArgs.Handled"/> property to <see
	/// langword="true"/>, indicating that the download event has been handled.</remarks>
	/// <param name="sender">The <see cref="CoreWebView2"/> instance that triggered the event.</param>
	/// <param name="args">The <see cref="CoreWebView2DownloadStartingEventArgs"/> containing event data, including the download operation
	/// details.</param>
	static void CoreWebView2_DownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
	{
		args.Handled = true;
	}

	/// <summary>
	/// Handles the WebResourceRequested event for a CoreWebView2 instance.
	/// </summary>
	/// <remarks>This method processes web resource requests by checking the request URI and responding accordingly.
	/// If the URI contains "https://runcsharp", a 404 response is returned. Otherwise, it attempts to retrieve the
	/// requested resource and respond with the appropriate data and MIME type.</remarks>
	/// <param name="sender">The CoreWebView2 instance that raised the event.</param>
	/// <param name="e">The event arguments containing details about the web resource request.</param>
	static async void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(webViewHandler);
		var url = e.Request.Uri ?? string.Empty;
		var filename = Path.GetFileName(url);

		if (url.Contains("https://runcsharp"))
		{
			e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}

		CancellationTokenSource cancellationTokenSource = new();
		var getData = await StreamAsync(url, cancellationTokenSource.Token);
		var mimeType = StreamExtensions.GetMimeType(filename);
		if (getData is null)
		{
			e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}
        e.Response = webViewHandler.PlatformView.CoreWebView2.Environment.CreateWebResourceResponse(getData.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
		cancellationTokenSource.Dispose();
	}

	/// <summary>
	/// Generates HTTP headers for CORS and content type.
	/// </summary>
	/// <param name="contentType">The MIME type to be set in the Content-Type header.</param>
	/// <returns>A string containing the complete set of HTTP headers, including CORS and the specified Content-Type.</returns>
	static string GenerateHeaders(string contentType)
	{
		const string baseHeaders = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		string contentTypeHeader = $"Content-Type: {contentType}";
		string completeHeaders = $"{baseHeaders}\r\n{contentTypeHeader}";
		return completeHeaders;
	}

	/// <summary>
	/// Asynchronously retrieves a stream from the specified URL.
	/// </summary>
	/// <param name="url">The URL from which to retrieve the stream. Must be a valid, accessible URL.</param>
	/// <param name="cancellation">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the stream retrieved from the specified
	/// URL.</returns>
	static async Task<Stream> StreamAsync(string url, CancellationToken cancellation = default)
	{
		var result = await streamExtensions.GetStream(url, cancellation);
		return result;
	}
}
