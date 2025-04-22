using System.Globalization;
using System.Runtime.Versioning;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using WebKit;

namespace EpubReader;
class CustomUrlSchemeHandler :NSObject, IWKUrlSchemeHandler
{
	readonly WebViewHandler handler;
	public CustomUrlSchemeHandler(WebViewHandler handler)
	{
		this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
	}
	[Export("webView:startURLSchemeTask:")]
	[SupportedOSPlatform("ios11.0")]
	public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
	{
		var url = urlSchemeTask.Request.Url.AbsoluteString ?? "";
		var baseUrl = NSUrl.FromString("app://demo/");
		if (url.StartsWith("app://"))
		{
			var path = url.Substring("app://demo/".Length);
			var filename = Path.GetFileName(path) ?? throw new InvalidOperationException("url is null");
			System.Diagnostics.Debug.WriteLine($"fileName: {filename}");
			var mimeType = FileService.GetMimeType(filename);
			var text = StreamExtensions.Instance?.Content(filename);
			if (text is not null && StreamExtensions.IsText(filename))
			{
				System.Diagnostics.Debug.WriteLine($"File: {filename} mimeType: {mimeType} url: {url} baseUrl: {baseUrl}");
				var stream = StreamExtensions.GetStream(text) ?? throw new InvalidOperationException("stream is null");
				var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
				
				using var dic = new NSMutableDictionary<NSString, NSString>();
				if (mimeType is not null)
				{
					dic[(NSString)"Content-Type"] = (NSString)mimeType;
				}
				// Disable local caching which would otherwise prevent user scripts from executing correctly.
				dic[(NSString)"Cache-Control"] = (NSString)"no-cache, max-age=0, must-revalidate, no-store";
				dic[(NSString)"Content-Length"] = (NSString)data.Length.ToString(CultureInfo.InvariantCulture);

				using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url, 200, "HTTP/1.1", dic);
				// 2.a. Send the response
				urlSchemeTask.DidReceiveResponse(response);
				// 2.c. Send the data
				urlSchemeTask.DidReceiveData(data);

				// 2.d. Finish the task
				urlSchemeTask.DidFinish();
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("File not text file");
			}
			var binary = StreamExtensions.Instance?.ByteContent(filename);
			if (binary is not null && StreamExtensions.IsBinary(filename))
			{
				System.Diagnostics.Debug.WriteLine($"File: {filename} mimeType: {mimeType} url: {url} baseUrl: {baseUrl}");
				var stream = StreamExtensions.GetStream(binary) ?? throw new InvalidOperationException("stream is null");
				var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
				using var dic = new NSMutableDictionary<NSString, NSString>();
				if (mimeType is not null)
				{
					dic[(NSString)"Content-Type"] = (NSString)mimeType;
				}
				// Disable local caching which would otherwise prevent user scripts from executing correctly.
				dic[(NSString)"Cache-Control"] = (NSString)"no-cache, max-age=0, must-revalidate, no-store";
				dic[(NSString)"Content-Length"] = (NSString)data.Length.ToString(CultureInfo.InvariantCulture);

				using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url, 200, "HTTP/1.1", dic);
				// 2.a. Send the response
				urlSchemeTask.DidReceiveResponse(response);
				// 2.c. Send the data
				urlSchemeTask.DidReceiveData(data);

				// 2.d. Finish the task
				urlSchemeTask.DidFinish();
			}
		}
	}
}