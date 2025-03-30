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
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => OnJavaScriptMessageReceived(r,m));
#endif
	}

	async void OnJavaScriptMessageReceived(object sender ,JavaScriptMessage m)
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

#if WINDOWS
	void WebView2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
	{
		var viewModel = BindingContext as BookViewModel ?? throw new InvalidOperationException("BindingContext is not WebViewPageViewModel");
		if (webView2 is null)
		{
			System.Diagnostics.Debug.WriteLine("WebView2 is null after trying to initialize it.");
			return;
		}
		System.Diagnostics.Debug.WriteLine("CoreWebView2Initialized");
		webView2.CoreWebView2.Settings.AreDevToolsEnabled = true;
		webView2.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
		webView2.CoreWebView2.Settings.AreHostObjectsAllowed = true;
		webView2.CoreWebView2.Settings.IsWebMessageEnabled = true;
		webView2.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = true;
		webView2.CoreWebView2.Settings.IsScriptEnabled = true;
		webView2.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
		webView2.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
	}

	void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
	{
		if (webView2 is null)
		{
			System.Diagnostics.Debug.WriteLine("WebView2 is null in CoreWebView2_WebResourceRequested");
			return;
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("WebView2 is not null in CoreWebView2_WebResourceRequested");
		}
		var url = e.Request.Uri ?? string.Empty;
		if (url.Contains("https://runcsharp"))
		{
			System.Diagnostics.Debug.WriteLine("runcsharp found");
			e.Response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}
		var filename = Path.GetFileName(url);

		string mimeType = ThreadSafeFileWriter.GetMimeType(filename);
		if (!ThreadSafeFileWriter.FileExists(filename))
		{
			System.Diagnostics.Debug.WriteLine($"File {filename} does not exist");
			e.Response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}
		Stream contentStream = ThreadSafeFileWriter.ReadFileStream(filename);

		var reader = new StreamReader(contentStream);
		var memoryStream = new MemoryStream();
		reader.BaseStream.CopyTo(memoryStream);
		var bytes = memoryStream.ToArray();

		System.Diagnostics.Debug.WriteLine($"WebResourceRequested: {url}");
		System.Diagnostics.Debug.WriteLine($"MIME Type: {mimeType}");
		System.Diagnostics.Debug.WriteLine($"Content Length: {bytes.Length}");

		var headers = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		if (e.Request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
		{
			CoreWebView2WebResourceResponse response1 = webView2.CoreWebView2.Environment.CreateWebResourceResponse(
				null, // No content for OPTIONS
				204,  // No Content
				"OK",
				headers
			);
			e.Response = response1;
			return; // Important to return here for OPTIONS requests
		}

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
		System.Diagnostics.Debug.WriteLine("OnDisappearing");
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
		else
		{
			System.Diagnostics.Debug.WriteLine("WebView2 is null when being disposed");
		}
#endif
		System.Diagnostics.Debug.WriteLine("Calling base.OnDisappearing in WebViewPage");
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
			await db.SaveBookData(book, CancellationToken.None);
			System.Diagnostics.Debug.WriteLine($"Loading page {book.Chapters[book.CurrentChapter].FileName}");
			var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
			await EpubText.EvaluateJavaScriptAsync("setPreviousPage()");
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		}
	}
	
	async void webView_Navigated(object sender, WebNavigatedEventArgs e)
	{
		
		if (loadIndex)
		{
			loadIndex = false;
			System.Diagnostics.Debug.WriteLine($"Page has loaded successfully.");
			System.Diagnostics.Debug.WriteLine($"Loading page {book.Chapters[book.CurrentChapter].FileName}");
			var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
			var result = await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
			if (result is null)
			{
				System.Diagnostics.Debug.WriteLine("Result is null");
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"Iframe Page {pageToLoad} loaded: {result}");
				//Shimmer.IsVisible = false;
			}
		}
		
	}

	async void webView_Navigating(object sender, WebNavigatingEventArgs e)
	{
		
		if (e.Url.Contains("runcsharp"))
		{
			e.Cancel = true;
			System.Diagnostics.Debug.WriteLine("runcsharp found");
			var urlParts = e.Url.Split('.');
			if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
			{
				var funcToCall = urlParts[1].Split("?");
				var methodName = funcToCall[0][..^1];
				if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
				{
					System.Diagnostics.Debug.WriteLine("Next");
					await Next();
				}
				if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
				{
					System.Diagnostics.Debug.WriteLine("Prev");
					await Prev();
				}
			}
		}
		
		System.Diagnostics.Debug.WriteLine($"Navigating to {e.Url}");
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = await db.GetSettings(CancellationToken.None);
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
#if WINDOWS
		var platformView = EpubText.Handler?.PlatformView;
		if (platformView is Microsoft.UI.Xaml.Controls.WebView2 webView2)
		{
			this.webView2 = webView2;
			System.Diagnostics.Debug.WriteLine("WebView2 is available");
			webView2.CoreWebView2Initialized += WebView2_CoreWebView2Initialized;
			await webView2.EnsureCoreWebView2Async();
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("WebView2 is not available");
		}
#endif
	}
	void OnSettingsClicked()
	{
		/*
		settings = await db.GetSettings(CancellationToken.None);

		List<string> background = GetProperty(settings.SetBackgroundColor);
		List<string> text = GetProperty(settings.SetTextColor);
		if (background.Count > 1)
		{
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('{background[0]}', '{background[1]}')");
			await EpubText.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
		}
		if (text.Count > 1)
		{
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('{text[0]}', '{text[1]}')");
		}
		await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
		await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
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
