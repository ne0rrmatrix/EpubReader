using Microsoft.Maui.Handlers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EpubReader;
#pragma warning restore IDE0130 // Namespace does not match folder structure

class CustomWebViewClient(IWebViewHandler webView) : global::Android.Webkit.WebViewClient
{
	readonly Microsoft.Maui.Controls.WebView webView = webView?.VirtualView as Microsoft.Maui.Controls.WebView ?? throw new InvalidOperationException($"{nameof(WebView)} cannot be null");

	public override void OnPageFinished(global::Android.Webkit.WebView? view, string? url)
	{
		base.OnPageFinished(view, url);
		var navigatedEventArgs = new WebNavigatedEventArgs(
		WebNavigationEvent.NewPage,
		url,
		null,
		WebNavigationResult.Success
		);

#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
		var navigatedEvent = webView.GetType().GetField(nameof(Microsoft.Maui.Controls.WebView.Navigated), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
		if (navigatedEvent?.GetValue(webView) is MulticastDelegate eventDelegate)
		{
			foreach (var handler in eventDelegate.GetInvocationList())
			{
				handler.Method.Invoke(handler.Target, [webView, navigatedEventArgs]);
			}
		}
		
	}
}
