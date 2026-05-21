using System.Diagnostics;
using Android.Graphics;
using Android.Webkit;
using AndroidX.Core.View;
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
	readonly Microsoft.Maui.Controls.WebView webView;
	readonly global::Android.Webkit.WebView platformView;
	readonly StreamExtensions streamExtensions;

	readonly CancellationTokenSource cancellationTokenSource = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="CustomWebViewClient"/> class with the specified web view handler.
	/// </summary>
	/// <remarks>This constructor configures the platform-specific settings for the web view, enabling features such
	/// as DOM storage, JavaScript execution, and mixed content handling. It also adjusts the view settings for optimal
	/// content display, such as enabling wide viewport usage and automatic image loading.</remarks>
	/// <param name="handler">The web view handler that provides the platform-specific view and settings. Must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="handler"/> is <see langword="null"/> or if the handler's <see
	/// cref="IWebViewHandler.VirtualView"/> is not a <see cref="Microsoft.Maui.Controls.WebView"/>.</exception>
	public CustomWebViewClient(IWebViewHandler handler, StreamExtensions streamExtensions, IJavaScriptBridgeDispatcher dispatcher)
	{
		this.webView = handler.VirtualView as Microsoft.Maui.Controls.WebView ?? throw new ArgumentNullException(nameof(handler));
		platformView = handler.PlatformView;
		this.streamExtensions = streamExtensions;
		platformView.Settings.DomStorageEnabled = true;
		platformView.Settings.JavaScriptEnabled = true;
		platformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
		platformView.Settings.AllowContentAccess = true;
		platformView.Settings.LoadsImagesAutomatically = true;
		platformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
		platformView.Settings.LoadWithOverviewMode = false;
		platformView.Settings.UseWideViewPort = true;
		platformView.Settings.SetSupportZoom(false);
		platformView.Settings.BuiltInZoomControls = false;
		platformView.Settings.DisplayZoomControls = false;
		platformView.Settings.TextZoom = 100;
		platformView.SetInitialScale(0);
		platformView.VerticalScrollBarEnabled = false;
		platformView.HorizontalScrollBarEnabled = false;
		platformView.AddJavascriptInterface(new JSBridge(dispatcher), "jsBridge");
		platformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
		Trace.TraceInformation("Configured Android WebView with density-aware initial scaling, wide viewport support, and neutral text zoom.");
		// Ensure caching is enabled so JS fetch() can populate and use cache for preloaded chapters
#pragma warning disable CA1422  // Type or member is obsolete
		platformView.Settings.SetAppCacheEnabled(true);
		var absolutePath = Platform.AppContext.CacheDir?.AbsolutePath ?? throw new InvalidOperationException();
        platformView.Settings.SetAppCachePath(absolutePath);
		// Ensure caching is enabled so JS fetch() can populate and use cache for preloaded chapters
#pragma warning disable CS0618  // Type or member is obsolete
        platformView.Settings.SetRenderPriority(Android.Webkit.WebSettings.RenderPriority.High);
		platformView.Settings.CacheMode = CacheModes.Default;
#pragma warning restore CS0618  // Type or member is obsolete
#pragma warning restore CA1422  // Type or member is obsolete
       platformView.Post(() => ApplyReaderSafeAreaInsets());
	}

	void ApplyReaderSafeAreaInsets()
	{
		var rootInsets = ViewCompat.GetRootWindowInsets(platformView);
		if (rootInsets is null)
		{
			return;
		}

		var displayCutoutInsets = rootInsets.GetInsets(WindowInsetsCompat.Type.DisplayCutout());
		var statusBarInsets = rootInsets.GetInsets(WindowInsetsCompat.Type.StatusBars());
		var navigationBarInsets = rootInsets.GetInsets(WindowInsetsCompat.Type.NavigationBars());

		var displayCutoutTop = displayCutoutInsets?.Top ?? 0;
		var displayCutoutRight = displayCutoutInsets?.Right ?? 0;
		var displayCutoutBottom = displayCutoutInsets?.Bottom ?? 0;
		var displayCutoutLeft = displayCutoutInsets?.Left ?? 0;
		var statusBarTop = statusBarInsets?.Top ?? 0;
		var navigationBarRight = navigationBarInsets?.Right ?? 0;
		var navigationBarBottom = navigationBarInsets?.Bottom ?? 0;
		var navigationBarLeft = navigationBarInsets?.Left ?? 0;

		var topInset = Math.Max(displayCutoutTop, statusBarTop);
		var rightInset = Math.Max(displayCutoutRight, navigationBarRight);
		var bottomInset = Math.Max(displayCutoutBottom, navigationBarBottom);
		var leftInset = Math.Max(displayCutoutLeft, navigationBarLeft);
		var density = platformView.Resources?.DisplayMetrics?.Density ?? 1f;
		if (density <= 0)
		{
			density = 1f;
		}

		var cssTopInset = (int)Math.Round(topInset / density, MidpointRounding.AwayFromZero);
		var cssRightInset = (int)Math.Round(rightInset / density, MidpointRounding.AwayFromZero);
		var cssBottomInset = (int)Math.Round(bottomInset / density, MidpointRounding.AwayFromZero);
		var cssLeftInset = (int)Math.Round(leftInset / density, MidpointRounding.AwayFromZero);

		platformView.EvaluateJavascript($"setNativeSafeAreaInsets({cssTopInset}, {cssRightInset}, {cssBottomInset}, {cssLeftInset});", null);
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

		// Allow the GitHub Pages site to load normally without interception
		if (url.StartsWith("https://ne0rrmatrix.github.io/EpubReader/", StringComparison.OrdinalIgnoreCase))
		{
			return base.ShouldInterceptRequest(view, request);
		}
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
		// Ensure caching headers are present in the response so the WebView can store resources
		var additionalHeaders = new Dictionary<string, string>
		{
			{ "Cache-Control", "public, max-age=86400" }
		};
		return WebResourceResponseHelper.CreateFromHtmlString(getData.Result, mimeType, 200, "OK", additionalHeaders) ?? base.ShouldInterceptRequest(view, request);
	}

	/// <summary>
	/// Asynchronously retrieves a stream from the specified URL.
	/// </summary>
	/// <param name="url">The URL from which to retrieve the stream. Must be a valid, accessible URL.</param>
	/// <param name="cancellation">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the stream retrieved from the specified
	/// URL.</returns>
	async Task<Stream> StreamAsync(string url, CancellationToken cancellation = default)
	{
		var result = await streamExtensions.GetStream(url, cancellation);
		return result;
	}

	/// <summary>
	/// Determines whether the URL loading should be overridden based on the specified request.
	/// </summary>
	/// <remarks>This method checks the URL of the request and forwards bridge-compatible URLs into the shared reader bridge dispatcher.
	/// contains query parameters or a specific keyword. If the request or URL is null, the method returns <see
	/// langword="true"/> to indicate that the loading should be overridden.</remarks>
	/// <param name="view">The <see cref="global::Android.Webkit.WebView"/> that is requesting the URL.</param>
	/// <param name="request">The <see cref="IWebResourceRequest"/> containing details of the request to be evaluated.</param>
	/// <returns><see langword="true"/> if the URL loading should be overridden; otherwise, <see langword="false"/>.</returns>
	public override bool ShouldOverrideUrlLoading(global::Android.Webkit.WebView? view, IWebResourceRequest? request)
	{
		if (request is null || request.Url is null)
		{
			return true;
		}

		return base.ShouldOverrideUrlLoading(view, request);
	}

	/// <summary>
	/// Invoked when a new page starts loading in the <see cref="Android.Webkit.WebView"/>.
	/// </summary>
	/// <remarks>This method raises the <c>Navigating</c> event for the associated <see
	/// cref="Microsoft.Maui.Controls.WebView"/> when a new page starts loading, unless the URL is null or contains the
	/// string "csharp".</remarks>
	/// <param name="view">The <see cref="global::Android.Webkit.WebView"/> that is initiating the callback.</param>
	/// <param name="url">The URL of the page being loaded. Cannot be null or contain the string "csharp".</param>
	/// <param name="favicon">The favicon for the page, if available.</param>
	public override void OnPageStarted(global::Android.Webkit.WebView? view, string? url, Bitmap? favicon)
	{
		if (url is null)
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
		if (url is null)
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

		view?.Post(() => ApplyReaderSafeAreaInsets());
	}
}