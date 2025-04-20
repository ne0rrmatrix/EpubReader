using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EpubReader.Interfaces;
using Microsoft.Maui.Handlers;

namespace EpubReader.Util;
public static partial class WebViewExtensions
{
	static readonly IDb db = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	public static async Task OnSettingsClicked(IWebViewHandler handler)
	{
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
	}
}
