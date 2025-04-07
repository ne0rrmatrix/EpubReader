using System.Buffers.Text;
using CommunityToolkit.Mvvm.Messaging;
using CoreGraphics;
using EpubReader.Messages;
using EpubReader.Util;
using Foundation;
using WebKit;
using static CoreFoundation.DispatchSource;

namespace EpubReader.Controls;

public class CustomWebView(CGRect frame, WKWebViewConfiguration configuration) : WKWebView(frame, configuration)
{
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();

	public override WKNavigation? LoadData(NSData data, string mimeType, string characterEncodingName, NSUrl baseUrl)
	{
		if (data is null)
		{
			return null;
		}

		var url = baseUrl.AbsoluteString ?? string.Empty;
		var filename = Path.GetFileName(url) ?? throw new InvalidOperationException("url is null");
		
		
		if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
		{
			return base.LoadData(data, mimeType, characterEncodingName, baseUrl);
		}
		if (StreamExtensions.IsText(filename))
		{
			var newString = streamExtensions.Content(filename) ?? throw new InvalidOperationException("text is null");
			var stream = StreamExtensions.GetStream(newString) ?? throw new InvalidOperationException("stream is null");
			var newData = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
			return base.LoadData(newData, mimeType, characterEncodingName, baseUrl);
		}
		var isBinary = StreamExtensions.IsBinary(filename);
		if (isBinary)
		{
			var binary = streamExtensions.ByteContent(filename) ?? throw new InvalidOperationException("binary is null");
			var stream = StreamExtensions.GetStream(binary) ?? throw new InvalidOperationException("stream is null");
			var newData = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
			return base.LoadData(newData, mimeType, characterEncodingName, baseUrl);

		}
		return base.LoadData(data, mimeType, characterEncodingName, baseUrl);
	}

	public override WKNavigation? LoadRequest(NSUrlRequest? request)
	{
		var url = request?.Url?.AbsoluteString ?? string.Empty;
		var filename = Path.GetFileName(url);
		var mimeType = FileService.GetMimeType(filename);
		
		if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && request is not null)
		{
			System.Diagnostics.Debug.WriteLine($"LoadData {mimeType} {filename} {url}");
			return base.LoadRequest(request);
		}
		var isText = StreamExtensions.IsText(filename);
		if (isText && request is not null)
		{
			var text = streamExtensions.Content(filename);
			if (text is not null && StreamExtensions.IsText(filename))
			{
				var stream = StreamExtensions.GetStream(text) ?? throw new InvalidOperationException("stream is null");
				var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
				System.Diagnostics.Debug.WriteLine($"LoadData {mimeType} {filename} {url}");
				var characterEncodingName = "UTF-8";
				var baseUrl = new NSUrl(url);
				return base.LoadData(data, mimeType, characterEncodingName, baseUrl);
			}
		}
		var isBinary = StreamExtensions.IsBinary(filename);
		if (isBinary && request is not null)
		{
			var binary = streamExtensions.ByteContent(filename) ?? throw new InvalidOperationException("binary is null");
			var stream = StreamExtensions.GetStream(binary) ?? throw new InvalidOperationException("stream is null");
			var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
			System.Diagnostics.Debug.WriteLine($"LoadData {mimeType} {filename} {url}");
			var characterEncodingName = "UTF-8";
			var baseUrl = new NSUrl(url);
			return base.LoadData(data, mimeType, characterEncodingName, baseUrl);
		}
		System.Diagnostics.Debug.WriteLine($"LoadData {mimeType} {filename} {url}");
		if (request is null)
		{
			return null;
		}
		return base.LoadRequest(request);
	}
}