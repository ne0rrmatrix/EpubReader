using CoreGraphics;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WebKit;

namespace EpubReader;
public class CustomMauiWKWebView(CGRect frame, WebViewHandler handler, WKWebViewConfiguration configuration) : MauiWKWebView(frame, handler, configuration)
{
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	public override WKNavigation? LoadRequest(NSUrlRequest request)
	{
		var url = request.Url.AbsoluteString ?? throw new InvalidOperationException("url is null");
		var baseUrl = NSUrl.FromString("app://demo/") ?? throw new InvalidOperationException("baseUrl is null");
		var filename = Path.GetFileName(url) ?? throw new InvalidOperationException("url is null");
		var mimeType = FileService.GetMimeType(filename);
		var stream = streamExtensions.GetStream(url);
		var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
		var characterEncodingName = "UTF-8";
		return LoadData(data, mimeType, characterEncodingName, baseUrl) ?? base.LoadRequest(request);
	}
}