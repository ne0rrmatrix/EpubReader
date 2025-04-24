using System.Globalization;
using System.Runtime.Versioning;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using WebKit;

namespace EpubReader;
class CustomUrlSchemeHandler : NSObject, IWKUrlSchemeHandler
{
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	[Export("webView:startURLSchemeTask:")]
	[SupportedOSPlatform("ios11.0")]
	public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
	{
		var url = urlSchemeTask.Request.Url.AbsoluteString ?? "";
		if(!url.StartsWith("app://demo/"))
		{
			urlSchemeTask.DidFailWithError(new NSError(new NSString("com.apple.webkit.error"), 0, new NSDictionary<NSString, NSString>()));
			return;
		}
		var path = url["app://demo/".Length..];
		var filename = Path.GetFileName(path) ?? throw new InvalidOperationException("url is null");
		var mimeType = FileService.GetMimeType(filename);
		var stream = streamExtensions.GetStream(path) ?? throw new InvalidOperationException("stream is null");
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