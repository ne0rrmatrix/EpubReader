using Microsoft.Maui.Handlers;

namespace EpubReader.Controls;
class CustomWebViewHandler : WebViewHandler
{
	public CustomWebViewHandler()
	{
		Mapper.ModifyMapping(
			nameof(Android.Webkit.WebView.WebViewClient),
			(handler, view, args) => handler.PlatformView.SetWebViewClient(new CustomWebViewClient(handler)));
	}
}
