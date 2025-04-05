using EpubReader.Models;
using EpubReader.Util;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.ViewModels;
using Microsoft.Maui.Controls.Platform;

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
#endif

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
	bool loadIndex = true;
#if WINDOWS
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
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
		if(m.Value.Contains("next", StringComparison.CurrentCultureIgnoreCase))
		{
			await Next();
			return;
		}
		if (m.Value.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
		{
			await Prev();
		}
		if (m.Value.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
		{
			OnSettingsClicked();
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

	void CoreWebView2_FrameNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
	{
		if(args.IsSuccess && args.WebErrorStatus == 0)
		{
			OnSettingsClicked();
		}
	}

	void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(webView2);
		var url = e.Request.Uri ?? string.Empty;
		var filename = Path.GetFileName(url);

		if (url.Contains("https://runcsharp"))
		{
			e.Response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
			return;
		}
		
		var mimeType = StreamExtensions.GetMimeType(filename);
		var text = streamExtensions.Content(filename);
		if (text is not null)
		{
			var stream = StreamExtensions.GetStream(text);
			var response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
			e.Response = response;
			return;
		}
		var binary = streamExtensions.ByteContent(filename);
		if (binary is not null)
		{
			var stream = StreamExtensions.GetStream(binary);
			var response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(stream.AsRandomAccessStream(), 200, "OK", GenerateHeaders(mimeType));
			e.Response = response;
			return;
		}
		e.Response = webView2.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
	}
	static string GenerateHeaders(string contentType)
	{
		const string baseHeaders = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type, Authorization";
		string contentTypeHeader = $"Content-Type: {contentType}";
		string completeHeaders = $"{baseHeaders}\r\n{contentTypeHeader}";
		return completeHeaders;
	}
#endif

	protected override void OnDisappearing()
	{
		if (BindingContext is BookViewModel viewModel)
		{
			viewModel.Dispose();
		}
#if WINDOWS
		if (webView2 is not null)
		{
			webView2.CoreWebView2Initialized -= WebView2_CoreWebView2Initialized;
		}
#endif
		base.OnDisappearing();
	}
	
	async Task Next()
	{
		if(book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			await LoadPage();
		}
	}

	async Task Prev()
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			await EpubText.EvaluateJavaScriptAsync("setPreviousPage()");
			await LoadPage();
		}
	}
	
	async Task LoadPage()
	{
		db.UpdateBookMark(book);
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
		await EpubText.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
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
		Shimmer.IsActive = false;
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
				await Next();
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				await Prev();
			}
			if (methodName.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
			{
				OnSettingsClicked();
			}
		}
	}
#if WINDOWS
	async void CurrentPage_Loaded(object sender, EventArgs e)
#elif ANDROID || IOS || MACCATALYST
		void CurrentPage_Loaded(object sender, EventArgs e)
#endif
	{
		book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException("BookViewModel is null");
		settings = db.GetSettings() ?? new();
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
#if WINDOWS
		var platformView = EpubText.Handler?.PlatformView;
		if (platformView is Microsoft.UI.Xaml.Controls.WebView2 webView3)
		{
			this.webView2 = webView3;
			webView2.CoreWebView2Initialized += WebView2_CoreWebView2Initialized;
			await webView2.EnsureCoreWebView2Async();
		}
#endif
	}
	async void OnSettingsClicked()
	{
		settings = db.GetSettings() ?? new();
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__backgroundColor', '{settings.BackgroundColor}')");
			await EpubText.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textColor', '{settings.TextColor}')");
			await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__advancedSettings', 'readium-advanced-on')");
			await EpubText.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
			await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')");
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
					db.UpdateBookMark(book);
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					var file = Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
					await EpubText.EvaluateJavaScriptAsync($"loadPage(\"{file}\")");
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
