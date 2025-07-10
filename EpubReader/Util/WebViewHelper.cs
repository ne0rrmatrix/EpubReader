using EpubReader.Interfaces;
using EpubReader.Models;

namespace EpubReader.Util;
public partial class WebViewHelper(WebView handler)
{

	readonly IDb db = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	readonly WebView webView = handler;

	public async Task OnSettingsClickedAsync()
	{
		var settings = db.GetSettings() ?? new();
		await SetColorScheme(settings);
		await SetFontData(settings);
		var colCount = settings.SupportMultipleColumns ? "2" : "1";

		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','{colCount}')");
		if(OperatingSystem.IsWindows())
		{
			var width = await GetWidth(settings);
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__lineLength', '{width}px');");
		}
		
		await webView.EvaluateJavaScriptAsync("gotoEnd();");
		await webView.EvaluateJavaScriptAsync("getPageCount()");
	}
	async Task SetColorScheme(Settings settings)
	{
		if (string.IsNullOrEmpty(settings.BackgroundColor) && string.IsNullOrEmpty(settings.TextColor))
		{
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__backgroundColor')");
			await webView.EvaluateJavaScriptAsync($"setBackgroundColor('{string.Empty}')");
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__textColor')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__backgroundColor', '{settings.BackgroundColor}')");
			await webView.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textColor', '{settings.TextColor}')");
		}
	}

	async Task SetFontData(Settings settings)
	{
		if (!string.IsNullOrEmpty(settings.FontFamily))
		{
			await webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontOverride')");
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontFamily')");
		}
		if (settings.FontSize > 0)
		{
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontSize')");
		}
	}
	async Task<int> GetWidth(Settings settings)
	{
		var result = await webView.EvaluateJavaScriptAsync("getWidth()");
		var fontSize = settings.FontSize > 0 ? settings.FontSize * 10 : 30;
		
		if (settings.SupportMultipleColumns)
		{
			return (Convert.ToInt32(result) / 3 - fontSize);
		}
		return (Convert.ToInt32(result) - fontSize);
	}
	public async Task LoadPage(Label label, Book book)
	{
#if ANDROID || WINDOWS
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
#elif IOS || MACCATALYST
		var pageToLoad = $"app://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
#endif
		await webView.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
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
