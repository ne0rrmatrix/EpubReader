using System;
using System.Buffers.Text;
using System.Reflection.Metadata;
using System.Runtime.Versioning;
using System.Web;
using CoreGraphics;
using EpubReader.Util;
using ExCSS;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using ObjCRuntime;
using WebKit;

namespace EpubReader;
public class CustomMauiWKWebView : MauiWKWebView
{
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	readonly WebViewHandler handler;
	public CustomMauiWKWebView(CGRect frame, WebViewHandler handler, WKWebViewConfiguration configuration) : base(frame, handler, configuration)
	{
		_ = handler ?? throw new ArgumentNullException(nameof(handler));
		this.handler = handler;
		System.Diagnostics.Debug.WriteLine($"CustomMauiWKWebView {frame} {handler} {configuration}");
	}
	
	public override WKNavigation? LoadRequest(NSUrlRequest request)
	{
		var url = request.Url.AbsoluteString;
		System.Diagnostics.Debug.WriteLine($"LoadRequest {url}");
		
		var baseUrl = NSUrl.FromString("app://demo/");
		var filename = Path.GetFileName(url) ?? throw new InvalidOperationException("url is null");
		var mimeType = FileService.GetMimeType(filename);
		var text = StreamExtensions.Instance?.Content(filename);
		if (text is not null && StreamExtensions.IsText(filename))
		{
			//System.Diagnostics.Debug.WriteLine($"Text: {text}");
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
	
	public override WKNavigation? LoadData(NSData data, string mimeType, string characterEncodingName, NSUrl baseUrl)
	{
		var url = baseUrl.AbsoluteString;
		System.Diagnostics.Debug.WriteLine($"LoadData {mimeType} {url}");
		return base.LoadData(data, mimeType, characterEncodingName, baseUrl);
	}

	public override WKNavigation? GoBack()
	{
		System.Diagnostics.Debug.WriteLine($"GoBack");
		return base.GoBack();
	}

	public override WKNavigation? GoForward()
	{
		System.Diagnostics.Debug.WriteLine($"GoForward");
		return base.GoForward();
	}

	public override WKNavigation? GoTo(WKBackForwardListItem item)
	{
		var url = item.Url.AbsoluteString;
		System.Diagnostics.Debug.WriteLine($"GoTo {url}");
		return base.GoTo(item);
	}
	
	public override WKNavigation LoadFileRequest(NSUrlRequest request, NSUrl readAccessURL)
	{
		System.Diagnostics.Debug.WriteLine($"LoadFileRequest {request} {readAccessURL}");
		return base.LoadFileRequest(request, readAccessURL);
	}

	public override WKNavigation? LoadFileUrl(NSUrl url, NSUrl readAccessUrl)
	{
		System.Diagnostics.Debug.WriteLine($"LoadFileUrl {url} {readAccessUrl}");
		return base.LoadFileUrl(url, readAccessUrl);
	}

	public override WKNavigation? LoadHtmlString(NSString htmlString, NSUrl? baseUrl)
	{
		System.Diagnostics.Debug.WriteLine($"LoadHtmlString {htmlString} {baseUrl}");
		return base.LoadHtmlString(htmlString, baseUrl);
	}
	
	public override WKNavigation LoadSimulatedRequest(NSUrlRequest request, NSUrlResponse response, NSData data)
	{
		System.Diagnostics.Debug.WriteLine($"LoadSimulatedRequest {request} {response} {data}");
		return base.LoadSimulatedRequest(request, response, data);
	}
	
	public override WKNavigation LoadSimulatedRequest(NSUrlRequest request, string htmlString)
	{
		System.Diagnostics.Debug.WriteLine($"LoadSimulatedRequest {request} {htmlString}");
		return base.LoadSimulatedRequest(request, htmlString);
	}
	void test()
	{
		/*
		 * var url = request.Url?.ToString() ?? throw new InvalidOperationException("url is null");
		var baseUrl = NSUrl.FromString("https://demo/");
		var filename = Path.GetFileName(url) ?? throw new InvalidOperationException("url is null");
		var mimeType = FileService.GetMimeType(filename);
		var text = StreamExtensions.Instance?.Content(filename);
		
		System.Diagnostics.Debug.WriteLine($"Filename: {filename} mimeType: {mimeType} url: {url} baseUrl: {baseUrl}");
		if (text is not null && StreamExtensions.IsText(filename))
		{
			System.Diagnostics.Debug.WriteLine($"Text: {text}");
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
		 */
	}
}