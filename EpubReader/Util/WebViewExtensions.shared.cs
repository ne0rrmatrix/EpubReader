using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using Microsoft.Maui.Handlers;

namespace EpubReader.Util;
public static partial class WebViewExtensions
{
	static readonly IDb db = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	public static async Task OnSettingsClicked(IWebViewHandler handler)
	{
		System.Diagnostics.Trace.WriteLine("OnSettingsClicked");
		var settings = db.GetSettings() ?? new();
		var webView = handler.VirtualView as WebView ?? throw new InvalidOperationException();

		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__backgroundColor', '{settings.BackgroundColor}')");
		await webView.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textColor', '{settings.TextColor}')");
		await webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
		await webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','1')");
		await webView.EvaluateJavaScriptAsync("gotoEnd();");
		var pages = await webView.EvaluateJavaScriptAsync("getPageCount()");
		System.Diagnostics.Debug.WriteLine("pages: " + pages);
	}

	public static async void OnJavaScriptMessageReceived(JavaScriptMessage m,Label label, Book book, WebView webView)
	{
		System.Diagnostics.Trace.WriteLine($"OnJavaScriptMessageReceived: {m.Value}");
		if (m.Value.Contains("next", StringComparison.CurrentCultureIgnoreCase))
		{
			await Next(label, webView, book);
			return;
		}
		if (m.Value.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
		{
			await Prev(label, book, webView);
		}
		if (m.Value.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
		{
			await OnSettingsClicked(webView.Handler as IWebViewHandler ?? throw new InvalidOperationException());
		}
	}
	public static async Task LoadPage(Label label, WebView webView, Book book)
	{
		System.Diagnostics.Trace.WriteLine($"LoadPage: {book.Chapters[book.CurrentChapter]?.FileName}");
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
		await webView.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
		label.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
	}
	public static async Task Next(Label label, WebView webView, Book book)
	{
		if (book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			db.UpdateBookMark(book);
			await LoadPage(label, webView, book);
		}
	}

	public static async Task Prev(Label label, Book book, WebView webView)
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			db.UpdateBookMark(book);
			await webView.EvaluateJavaScriptAsync("setPreviousPage()");
			await LoadPage(label, webView, book);
		}
	}
}
