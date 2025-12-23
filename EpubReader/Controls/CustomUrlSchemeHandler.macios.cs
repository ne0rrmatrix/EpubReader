using System.Globalization;
using System.Runtime.Versioning;
using Foundation;
using WebKit;

namespace EpubReader.Controls;

/// <summary>
/// Provides a custom URL scheme handler for processing requests in a <see cref="WKWebView"/>.
/// </summary>
/// <remarks>This handler is designed to manage URL requests that begin with the "app://demo/" scheme. It
/// retrieves the requested resource, determines its MIME type, and sends the appropriate response back to the web view.
/// The handler also ensures that local caching is disabled to allow user scripts to execute correctly.</remarks>
class CustomUrlSchemeHandler : NSObject, IWKUrlSchemeHandler
{
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();

	/// <summary>
	/// Handles the start of a custom URL scheme task in a <see cref="WKWebView"/>.
	/// </summary>
	/// <remarks>This method processes requests with URLs starting with "app://demo/". If the URL does not match
	/// this pattern, the task fails with an error. The method retrieves the requested resource, determines its MIME type,
	/// and sends the appropriate response back to the web view, including headers to disable local caching.</remarks>
	/// <param name="webView">The <see cref="WKWebView"/> that initiated the URL scheme task.</param>
	/// <param name="urlSchemeTask">The URL scheme task to be processed.</param>
	/// <exception cref="InvalidOperationException">Thrown if the URL or the data stream is null, indicating an invalid request or resource.</exception>
	[Export("webView:startURLSchemeTask:")]
	[SupportedOSPlatform("ios11.0")]
	public async void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
	{
		var url = urlSchemeTask.Request.Url.AbsoluteString ?? "";
		if (!url.StartsWith("app://demo/"))
		{
			urlSchemeTask.DidFailWithError(new NSError(new NSString("com.apple.webkit.error"), 0, new NSDictionary<NSString, NSString>()));
			return;
		}
		var path = url["app://demo/".Length..];
		var filename = Path.GetFileName(path) ?? throw new InvalidOperationException("url is null");
		var mimeType = FileService.GetMimeType(filename);
		var stream = await streamExtensions.GetStream(path) ?? throw new InvalidOperationException("stream is null");
		var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
		using var dic = new NSMutableDictionary<NSString, NSString>
		{
			[(NSString)"Content-Type"] = (NSString)mimeType,
			// Allow caching so the WebView can reuse chapter resources when preloading/seek occurs.
			[(NSString)"Cache-Control"] = (NSString)"public, max-age=86400",
			[(NSString)"Content-Length"] = (NSString)data.Length.ToString(CultureInfo.InvariantCulture)
		};

		using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url, 200, "HTTP/1.1", dic);
		// 2.a. Send the response
		urlSchemeTask.DidReceiveResponse(response);
		// 2.c. Send the data
		urlSchemeTask.DidReceiveData(data);

		// 2.d. Finish the task
		urlSchemeTask.DidFinish();
	}
}