using Android.Graphics;
using Android.Webkit;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;
using Microsoft.Maui.Handlers;

namespace EpubReader.Controls;

/// <summary>
/// Provides a custom implementation of <see cref="WebViewClient"/> to handle web resource requests and page navigation
/// events for a <see cref="Microsoft.Maui.Controls.WebView"/>.
/// </summary>
/// <remarks>This class enables interception of web resource requests and custom handling of URL loading and page
/// navigation events. It is designed to work with the MAUI framework and integrates with the <see
/// cref="StreamExtensions"/> service for resource streaming.</remarks>
class CustomWebViewClient : WebViewClient
{
	const string csharp = "runcsharp";
	readonly Microsoft.Maui.Controls.WebView webView;
	
	readonly CancellationTokenSource cancellationTokenSource = new();
	static readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	
	/// <summary>
	/// Initializes a new instance of the <see cref="CustomWebViewClient"/> class with the specified web view handler.
	/// </summary>
	/// <remarks>This constructor configures the platform-specific settings for the web view, enabling features such
	/// as DOM storage, JavaScript execution, and mixed content handling. It also adjusts the view settings for optimal
	/// content display, such as enabling wide viewport usage and automatic image loading.</remarks>
	/// <param name="handler">The web view handler that provides the platform-specific view and settings. Must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <see langword="null"/> or if the handler's <see
	/// cref="IWebViewHandler.VirtualView"/> is not a <see cref="Microsoft.Maui.Controls.WebView"/>.</exception>
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
		handler.PlatformView.VerticalScrollBarEnabled = false;
		handler.PlatformView.HorizontalScrollBarEnabled = false;
		handler.PlatformView.LongClickable = true;
		handler.PlatformView.SetOnLongClickListener(new LongClickListener());
#if DEBUG
		Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
#endif
	}

	/// <summary>
	/// Intercepts requests made by the <see cref="Android.Webkit.WebView"/> and provides a custom response.
	/// </summary>
	/// <remarks>This method intercepts requests to provide custom responses, such as serving local resources or
	/// modifying  the response content. If the URL starts with "data:", the request is handled by the base
	/// implementation.</remarks>
	/// <param name="view">The <see cref="Android.Webkit.WebView"/> that is requesting the resource.</param>
	/// <param name="request">The details of the request to be intercepted, including the URL and headers.</param>
	/// <returns>A <see cref="WebResourceResponse"/> containing the custom response for the request, or <see langword="null"/>  to
	/// allow the default handling of the request.</returns>
	public override WebResourceResponse? ShouldInterceptRequest(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		var url = request?.Url?.ToString() ?? string.Empty;
		if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
		{
			return base.ShouldInterceptRequest(view, request);
		}

		var filename = System.IO.Path.GetFileName(url);
		var mimeType = FileService.GetMimeType(filename);
		
		var getData = StreamAsync(url, cancellationTokenSource.Token);
		
		if (getData.IsFaulted || getData.IsCanceled)
		{
			return base.ShouldInterceptRequest(view, request);
		}
		return WebResourceResponseHelper.CreateFromHtmlString(getData.Result, mimeType, 200, "OK") ?? base.ShouldInterceptRequest(view, request);
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

	/// <summary>
	/// Determines whether the URL loading should be overridden based on the specified request.
	/// </summary>
	/// <remarks>This method checks the URL of the request and sends a <see cref="JavaScriptMessage"/> if the URL
	/// contains query parameters or a specific keyword. If the request or URL is null, the method returns <see
	/// langword="true"/> to indicate that the loading should be overridden.</remarks>
	/// <param name="view">The <see cref="global::Android.Webkit.WebView"/> that is requesting the URL.</param>
	/// <param name="request">The <see cref="IWebResourceRequest"/> containing details of the request to be evaluated.</param>
	/// <returns><see langword="true"/> if the URL loading should be overridden; otherwise, <see langword="false"/>.</returns>
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

	/// <summary>
	/// Invoked when a new page starts loading in the <see cref="Android.Webkit.WebView"/>.
	/// </summary>
	/// <remarks>This method raises the <c>Navigating</c> event for the associated <see
	/// cref="Microsoft.Maui.Controls.WebView"/> when a new page starts loading, unless the URL is null or contains the
	/// string "csharp".</remarks>
	/// <param name="view">The <see cref="Android.Webkit.WebView"/> that is initiating the callback.</param>
	/// <param name="url">The URL of the page being loaded. Cannot be null or contain the string "csharp".</param>
	/// <param name="favicon">The favicon for the page, if available.</param>
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

	/// <summary>
	/// Invoked when a page has finished loading in the <see cref="Android.Webkit.WebView"/>.
	/// </summary>
	/// <remarks>This method triggers the <c>Navigated</c> event on the associated <see
	/// cref="Microsoft.Maui.Controls.WebView"/> if the URL is not null and does not contain the specified substring. It
	/// uses reflection to access non-public members.</remarks>
	/// <param name="view">The <see cref="Android.Webkit.WebView"/> that has finished loading the page.</param>
	/// <param name="url">The URL of the page that has finished loading.</param>
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