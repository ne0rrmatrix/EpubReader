#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
#endif

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ViewModels;

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using EpubReader.Util;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SkiaSharp;


#endif

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
	bool loadIndex = true;
#if WINDOWS
	WebView2? webView2;
#endif
#if ANDROID
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
	readonly IDb db;
	Book book = new();
	Settings settings = new();
	bool disposedValue;

	public BookPage(BookViewModel viewModel, IDb db)
	{
		InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
#if ANDROID
		EpubText.Behaviors.Add(touchbehavior);
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => OnJavaScriptMessageReceived(m));
#endif
	}
#if ANDROID
	async void OnJavaScriptMessageReceived(JavaScriptMessage m)
	{
		System.Diagnostics.Debug.WriteLine("runcsharp found");
		if(m.Value.Contains("next", StringComparison.CurrentCultureIgnoreCase))
		{
			System.Diagnostics.Debug.WriteLine("Next");
			await Next();
			return;
		}
		if (m.Value.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
		{
			System.Diagnostics.Debug.WriteLine("Prev");
			await Prev();
		}
	}
#endif

#if WINDOWS
	void WebView2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
	{
		ArgumentNullException.ThrowIfNull(webView2);
		webView2.CoreWebView2.Settings.AreDevToolsEnabled = true;
		webView2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
		webView2.CoreWebView2.Settings.IsReputationCheckingRequired = false;
		webView2.CoreWebView2.Settings.AreHostObjectsAllowed = true;
		webView2.CoreWebView2.Settings.IsWebMessageEnabled = true;
		webView2.CoreWebView2.Settings.IsScriptEnabled = true;
		webView2.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
		webView2.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
		webView2.CoreWebView2.FrameNavigationCompleted += CoreWebView2_FrameNavigationCompleted;
	}

	async void CoreWebView2_FrameNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
	{
		System.Diagnostics.Debug.WriteLine("FrameNavigationCompleted");
		if(args.IsSuccess && args.WebErrorStatus == 0)
		{
			System.Diagnostics.Debug.WriteLine("FrameNavigationCompleted: Success");
			await EpubText.EvaluateJavaScriptAsync($"document.body.style.backgroundColor = '{settings.BackgroundColor}'");
			await EpubText.EvaluateJavaScriptAsync($"document.body.style.color = '{settings.TextColor}'");
		}
	}

	void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(webView2);
		var url = e.Request.Uri ?? string.Empty;
		var filename = Path.GetFileName(url);
		if (url.Contains("https://runcsharp") || !ThreadSafeFileWriter.FileExists(filename))
		{
			e.Response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}

		Stream contentStream = ThreadSafeFileWriter.ReadFileStream(filename);
		var reader = new StreamReader(contentStream);
		var memoryStream = new MemoryStream();
		reader.BaseStream.CopyTo(memoryStream);
		var headers = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		CoreWebView2WebResourceResponse response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(
			memoryStream.AsRandomAccessStream(), // Content stream
			200,          // Status code (OK)
			"OK",         // Status text
			headers       // Response headers
			);
		// Set the custom response
		e.Response = response;
	}

#endif

	protected override void OnDisappearing()
	{
		if (BindingContext is BookViewModel viewModel)
		{
			System.Diagnostics.Debug.WriteLine("Disposing ViewModel");
			viewModel.Dispose();
		}
#if WINDOWS
		if (webView2 is not null)
		{
			System.Diagnostics.Debug.WriteLine("Disposing WebView2");
			webView2.CoreWebView2Initialized -= WebView2_CoreWebView2Initialized;
		}
#endif
		base.OnDisappearing();
	}
	
	async Task Next()
	{
		Debug.WriteLine("NextPage");
		if(book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None);
			var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
	}

	async Task Prev()
	{
		Debug.WriteLine("PrevPage");
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			await EpubText.EvaluateJavaScriptAsync("setPreviousPage()");
			await db.SaveBookData(book, CancellationToken.None);
			var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
			
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
	}
	
	async void webView_Navigated(object sender, WebNavigatedEventArgs e)
	{
		if (!loadIndex)
		{
			return;
		}
		loadIndex = false;
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
		await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
	}

	async void webView_Navigating(object sender, WebNavigatingEventArgs e)
	{
		var urlParts = e.Url.Split('.');
		if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			e.Cancel = true;
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				System.Diagnostics.Debug.WriteLine("Next");
				await Next();
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				System.Diagnostics.Debug.WriteLine("weview_Navigating GotoLastPage");
				await Prev();
			}
		}
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = await db.GetSettings(CancellationToken.None);
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
#if WINDOWS
		var platformView = EpubText.Handler?.PlatformView;
		if (platformView is Microsoft.UI.Xaml.Controls.WebView2 webView3)
		{
			this.webView2 = webView3;
			System.Diagnostics.Debug.WriteLine("WebView2 is available");
			webView2.CoreWebView2Initialized += WebView2_CoreWebView2Initialized;
			await webView2.EnsureCoreWebView2Async();
		}
#endif
	}
	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None);
		System.Diagnostics.Debug.WriteLine("Settings clicked");
		/*
		await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
		/*
		List<string> background = GetProperty(settings.SetBackgroundColor);
		List<string> text = GetProperty(settings.SetTextColor);
		if (!string.IsNullOrEmpty(settings.BackgroundColor) && !string.IsNullOrEmpty(settings.TextColor))
		{
			System.Diagnostics.Debug.WriteLine($"Setting background color to {background[0]} {background[1]}");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('{background[0]}', '{background[1]}')");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--User__backgroundColor', '{settings.BackgroundColor}')");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--User__textColor', '{settings.TextColor}')");
			await EpubText.EvaluateJavaScriptAsync($"document.body.style.backgroundColor = '{settings.BackgroundColor}'");
		}
		if (text.Count > 1)
		{
			System.Diagnostics.Debug.WriteLine($"Setting color to {text[0]} {text[1]}");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('{text[0]}', '{text[1]}')");
		}
				
		//await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
		//await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
		//await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--User__backgroundColor', '{settings.BackgroundColor}')");
		await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--User__textColor', '{settings.TextColor}')");
		//await EpubText.EvaluateJavaScriptAsync($"document.body.style.backgroundColor = '{settings.BackgroundColor}'");
		await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
		await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')");
		*/
	}

	static List<string> GetProperty(string key)
	{
		var temp = key.Split(":");
		if (temp.Length > 1)
		{
			return [temp[0], temp[1]];
		}
		return [];
	}
	void CreateToolBarItem(int index, Chapter chapter)
	{
		if (string.IsNullOrEmpty(chapter.Title))
		{
			return;
		}
		var toolbarItem = new ToolbarItem
		{
			Text = chapter.Title,
			Order = ToolbarItemOrder.Secondary,
			Priority = index,
			Command = new Command(() =>
			{
				Dispatcher.Dispatch(async () =>
				{
					book.CurrentChapter = index;
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await EpubText.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
					await db.SaveBookData(book, CancellationToken.None);
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);

		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
	}

	public void SwipeGestureRecognizer_Swiped(object? sender, SwipedEventArgs e)
	{
		if (sender is null)
		{
			return;
		}
		if (e.Direction == SwipeDirection.Up)
		{
			var viewModel = (BookViewModel)BindingContext;
			viewModel.Press();
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
#if ANDROID
				touchbehavior.Dispose();
#endif
			}
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
