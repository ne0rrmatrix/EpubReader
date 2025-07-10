using CoreGraphics;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WebKit;

namespace EpubReader.Controls;

/// <summary>
/// Represents a custom WebView for MAUI applications that extends the functionality of <see cref="MauiWKWebView"/>.
/// </summary>
/// <remarks>This class overrides the <see cref="LoadRequest(NSUrlRequest)"/> method to load data from a stream
/// using a custom URL scheme. It utilizes the <see cref="StreamExtensions"/> service to retrieve the stream associated
/// with the requested URL.</remarks>
/// <param name="frame"></param>
/// <param name="handler"></param>
/// <param name="configuration"></param>
public class CustomMauiWKWebView(CGRect frame, WebViewHandler handler, WKWebViewConfiguration configuration) : MauiWKWebView(frame, handler, configuration)
{
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	
	/// <summary>
	/// Loads a web request and returns the navigation object for the request.
	/// </summary>
	/// <remarks>This method processes the URL from the request, determines the MIME type, and loads the data for
	/// the request. If the request cannot be loaded, it falls back to the base implementation.</remarks>
	/// <param name="request">The <see cref="NSUrlRequest"/> to be loaded. The request must contain a valid URL.</param>
	/// <returns>A <see cref="WKNavigation"/> object representing the navigation for the loaded request, or <see langword="null"/>
	/// if the request could not be loaded.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the URL in the <paramref name="request"/> is <see langword="null"/>, or if any required data for loading
	/// the request is <see langword="null"/>.</exception>
	public override WKNavigation? LoadRequest(NSUrlRequest request)
	{
		var url = request.Url.AbsoluteString ?? throw new InvalidOperationException("url is null");
		var baseUrl = NSUrl.FromString("app://demo/") ?? throw new InvalidOperationException("baseUrl is null");
		var filename = Path.GetFileName(url) ?? throw new InvalidOperationException("fileName is null");
		var mimeType = FileService.GetMimeType(filename);
		var stream = streamExtensions.GetStream(url);
		var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
		var characterEncodingName = "UTF-8";
		return LoadData(data, mimeType, characterEncodingName, baseUrl) ?? base.LoadRequest(request);
	}
}