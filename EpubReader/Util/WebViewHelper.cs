using EpubReader.Interfaces;
using EpubReader.Models;

namespace EpubReader.Util;
public partial class WebViewHelper(WebView handler)
{

	readonly IDb db = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	readonly WebView webView = handler;

	public async Task OnSettingsClicked()
	{
		var settings = db.GetSettings() ?? new();

		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__backgroundColor', '{settings.BackgroundColor}')");
		await webView.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textColor', '{settings.TextColor}')");
		await webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
		await webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')");
		if (settings.SupportMultipleColumns)
		{
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','2')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','1')");
		}
		await webView.EvaluateJavaScriptAsync("gotoEnd();");
		await webView.EvaluateJavaScriptAsync("getPageCount()");
	}
	
	public async Task LoadPage(Label label, Book book)
	{
#if ANDROID || WINDOWS
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
#elif IOS || MACCATALYST
		var pageToLoad = $"app://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
#endif
		await webView.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
		label.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
	}
	public async Task Next(Label label, Book book)
	{
		if (book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			db.UpdateBookMark(book);
			await LoadPage(label, book);
		}
	}
	public async Task Prev(Label label, Book book)
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			db.UpdateBookMark(book);
			await webView.EvaluateJavaScriptAsync("setPreviousPage()");
			await LoadPage(label, book);
		}
	}
}
