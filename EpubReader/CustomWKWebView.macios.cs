using CoreGraphics;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WebKit;

namespace EpubReader;
public class CustomMauiWKWebView(CGRect frame, WebViewHandler handler, WKWebViewConfiguration configuration) : MauiWKWebView(frame, handler, configuration)
{
	public override WKNavigation? LoadRequest(NSUrlRequest request)
	{
		var url = request.Url.AbsoluteString;
		var baseUrl = NSUrl.FromString("app://demo/");
		var filename = Path.GetFileName(url) ?? throw new InvalidOperationException("url is null");
		var mimeType = FileService.GetMimeType(filename);
		var text = StreamExtensions.Instance?.Content(filename);
		if (text is not null && StreamExtensions.IsText(filename))
		{
			var stream = StreamExtensions.GetStream(text) ?? throw new InvalidOperationException("stream is null");
			var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
			var characterEncodingName = "UTF-8";
			return LoadData(data, mimeType, characterEncodingName, baseUrl!);
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
			var characterEncodingName = "UTF-8";
			return LoadData(data, mimeType, characterEncodingName, baseUrl!);
		}
		return base.LoadRequest(request);
	}
}