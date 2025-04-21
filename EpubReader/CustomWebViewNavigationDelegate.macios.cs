using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;
using Foundation;
using Microsoft.Maui.Handlers;
using WebKit;

namespace EpubReader;
class CustomWebViewNavigationDelegate : WKNavigationDelegate
{
	readonly IWebViewHandler handler;
	public CustomWebViewNavigationDelegate(IWebViewHandler handler)
	{
		this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
		System.Diagnostics.Debug.WriteLine($"CustomWebViewNavigationDelegate {handler}");
	}
	public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
	{
		var url = webView.Url?.AbsoluteString;
		System.Diagnostics.Debug.WriteLine($"DidFinishNavigation: {url}");
		WeakReferenceMessenger.Default.Send(new JavaScriptMessage("pageLoad"));
	}
	public override void DidCommitNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Trace.WriteLine("DidCommitNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, WKWebpagePreferences preferences, Action<WKNavigationActionPolicy, WKWebpagePreferences> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("DecidePolicy Action: " + navigationAction.Request.Url?.AbsoluteString);
		if(navigationAction.Request.Url?.AbsoluteString?.Contains("about:blank") is true)
		{
			System.Diagnostics.Trace.WriteLine("DecidePolicy Action: about:blank");
			//decisionHandler(WKNavigationActionPolicy.Cancel, preferences);
			//var book = StreamExtensions.Instance?.Book ?? throw new InvalidOperationException("Book is null");
			//var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			//System.Diagnostics.Trace.WriteLine($"DecidePolicy Action: {pageToLoad}");
			//await webView.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
			//return;
		}
			decisionHandler(WKNavigationActionPolicy.Allow, preferences);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationResponse navigationResponse, Action<WKNavigationResponsePolicy> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("DecidePolicy Response: " + navigationResponse.Response.Url?.AbsoluteString);
		decisionHandler(WKNavigationResponsePolicy.Allow);
	}
	public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
	{
		System.Diagnostics.Trace.WriteLine("DecidePolicyAction2: " + navigationAction.Request.Url?.AbsoluteString);
		decisionHandler(WKNavigationActionPolicy.Allow);
	}

	public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Trace.WriteLine("DidStartProvisionalNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
	{
		System.Diagnostics.Trace.WriteLine("DidFailNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
	{
		System.Diagnostics.Trace.WriteLine("DidFailProvisionalNavigation: " + webView.Url?.AbsoluteString);
	}
	public override void DidReceiveServerRedirectForProvisionalNavigation(WKWebView webView, WKNavigation navigation)
	{
		System.Diagnostics.Trace.WriteLine("DidReceiveServerRedirectForProvisionalNavigation: " + webView.Url?.AbsoluteString);
	}
}