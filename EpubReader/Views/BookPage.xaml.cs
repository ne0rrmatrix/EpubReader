#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
#endif

using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
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
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
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

		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());

		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		Dispatcher.Dispatch(() => UpdateWebView());
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None);
		await EpubText.EvaluateJavaScriptAsync($"changeTextStyle({settings.FontSize})");
		await EpubText.EvaluateJavaScriptAsync($"applyStyles({{ fontFamily: '{settings.FontFamily}', backgroundColor: '{settings.BackgroundColor}', textColor: '{settings.TextColor}' }});");
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
				var html = InjectIntoHtml.UpdateHtml(chapter.HtmlFile, book, settings);
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

	async void OnEpubText_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}");
		if (isPreviousPage)
		{
			await EpubText.EvaluateJavaScriptAsync("scrollToHorizontalEnd()");
			isPreviousPage = false;
		}
	}

	void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		var urlParts = e.Url.Split('.');
		if (urlParts[0].Contains("runcsharp", StringComparison.CurrentCultureIgnoreCase))
		{
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			var parameters = funcToCall[1];
			e.Cancel = true;
			System.Diagnostics.Debug.WriteLine($"Method Name: {methodName}");
			System.Diagnostics.Debug.WriteLine($"Parameters: {parameters}");
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				NextPage(this, new EventArgs());
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				PreviousPage(this, new EventArgs());
			}
		}
		if (e.Url.Contains("http://") || e.Url.Contains("https://") || e.Url.Contains("file:"))
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
			logger.Info("Sender is null");
			return;
		}
		switch (e.Direction)
		{
			case SwipeDirection.Left:
				NextPage(sender, new EventArgs());
				break;
			case SwipeDirection.Right:
				PreviousPage(sender, new EventArgs());
				break;
			default:
				var viewModel = (BookViewModel)BindingContext;
				viewModel.Press();
				break;
		}
	}

	void UpdateWebView()
	{
		Shimmer.IsActive = true;
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		var html = InjectIntoHtml.UpdateHtml(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
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
