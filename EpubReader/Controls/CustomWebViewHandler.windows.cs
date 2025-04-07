using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace EpubReader.Controls;
class CustomWebViewHandler : WebViewHandler
{
	WebView2? webView;
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
	public CustomWebViewHandler()
	{
	}
	protected override void ConnectHandler(WebView2 platformView)
	{
		webView = platformView;
		platformView.CoreWebView2Initialized += WebView2_CoreWebView2Initialized;
		base.ConnectHandler(platformView);
	}
	protected override void DisconnectHandler(WebView2 platformView)
	{
		ArgumentNullException.ThrowIfNull(webView);
		webView.CoreWebView2Initialized -= WebView2_CoreWebView2Initialized;
		webView.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;
		webView.CoreWebView2.FrameNavigationCompleted -= CoreWebView2_FrameNavigationCompleted;
		base.DisconnectHandler(platformView);
	}
	
	void WebView2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
	{
		ArgumentNullException.ThrowIfNull(webView);
		webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
		webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
		webView.CoreWebView2.Settings.IsReputationCheckingRequired = false;
		webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
		webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
		webView.CoreWebView2.Settings.IsScriptEnabled = true;
		webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
		webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
		webView.CoreWebView2.FrameNavigationCompleted += CoreWebView2_FrameNavigationCompleted;
	}

	static void CoreWebView2_FrameNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
	{
		if (args.IsSuccess && args.WebErrorStatus == 0)
		{
			WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		}
	}

	void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(webView);
		var url = e.Request.Uri ?? string.Empty;
		var filename = Path.GetFileName(url);

		if (url.Contains("https://runcsharp"))
		{
			e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}

		var mimeType = StreamExtensions.GetMimeType(filename);
		var text = streamExtensions.Content(filename);
		if (text is not null)
		{
			var stream = StreamExtensions.GetStream(text);
			var response = webView.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
			e.Response = response;
			return;
		}
		var binary = streamExtensions.ByteContent(filename);
		if (binary is not null)
		{
			var stream = StreamExtensions.GetStream(binary);
			var response = webView.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
			e.Response = response;
			return;
		}
		e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
	}
	static string GenerateHeaders(string contentType)
	{
		const string baseHeaders = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		string contentTypeHeader = $"Content-Type: {contentType}";
		string completeHeaders = $"{baseHeaders}\r\n{contentTypeHeader}";
		return completeHeaders;
	}
}
