#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
#endif

using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Message;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class BookPage : ContentPage, IDisposable
{
#if ANDROID
	readonly CommunityToolkit.Maui.Behaviors.TouchBehavior touchbehavior = new();
#endif
	bool isPreviousPage = false;
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
#endif
	}

	async void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = await db.GetSettings(CancellationToken.None);
		WeakReferenceMessenger.Default.Register<JavaScriptMessage>(this, (r, m) => OnMessageReceived(r,m));
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());

		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		Dispatcher.Dispatch(() => UpdateWebView());
	}

	void OnMessageReceived(object r, JavaScriptMessage m)
	{
		if(m.Value.ToString().Contains("next"))
		{
			NextPage(r, new EventArgs());
		}
		if (m.Value.ToString().Contains("prev"))
		{
			PreviousPage(r, new EventArgs());
		}
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None);
		
		List<string> background = GetProperty(settings.SetBackgroundColor);
		List<string> text = GetProperty(settings.SetTextColor);
		if(background.Count > 1)
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
		await EpubText.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize*10}%')");
		if (isPreviousPage)
		{
			isPreviousPage = false;
			await EpubText.EvaluateJavaScriptAsync("scrollToHorizontalEnd()");
		}
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
				var html = InjectIntoHtml.UpdateHtml(chapter.HtmlFile, book);
				Dispatcher.Dispatch(async () =>
				{
					EpubText.Source = new HtmlWebViewSource { Html = html };
					book.CurrentChapter = index;
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
					await db.SaveBookData(book, CancellationToken.None);
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		EpubText.Navigating -= EpubText_Navigating;
		EpubText.Navigated -= OnEpubText_Navigated;

		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
	}

	void OnEpubText_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}");
		OnSettingsClicked();
	}

#if ANDROID || IOS || MACCATALYST
	static void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
#else
	void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
#endif
#if WINDOWS
		var urlParts = e.Url.Split('.');
		if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				NextPage(this, new EventArgs());
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				PreviousPage(this, new EventArgs());
			}
		}
#endif
		if (e.Url.Contains("http://") || e.Url.Contains("https://"))
		{
			e.Cancel = true;
		}
	}

	async void PreviousPage(object sender, EventArgs e)
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			await db.SaveBookData(book, CancellationToken.None);
			isPreviousPage = true;
			Dispatcher.Dispatch(() => UpdateWebView());
		}
		else
		{
			Dispatcher.Dispatch(() => UpdateWebView());
		}
	}

	async void NextPage(object sender, EventArgs e)
	{
		if (book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None);
			Dispatcher.Dispatch(() => UpdateWebView());
		}
	}


	public void SwipeGestureRecognizer_Swiped(object? sender, SwipedEventArgs e)
	{
		if(sender is null)
		{
			return;
		}
		if (e.Direction == SwipeDirection.Up)
		{
			var viewModel = (BookViewModel)BindingContext;
			viewModel.Press();
		}
	}

	void UpdateWebView()
	{
		Shimmer.IsActive = true;
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		var html = InjectIntoHtml.UpdateHtml(book.Chapters[book.CurrentChapter].HtmlFile, book);
		EpubText.Source = new HtmlWebViewSource { Html = html };
		Shimmer.IsActive = false;
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
