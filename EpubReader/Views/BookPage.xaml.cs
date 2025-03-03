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
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
	Book book = new();
	Settings settings = new();
	bool disposedValue;

	public BookPage(BookViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
#if ANDROID
		EpubText.Behaviors.Add(touchbehavior);
#endif
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = ((BookViewModel)BindingContext).Settings;

		EpubText.Navigating += EpubText_Navigating;
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		if (!OperatingSystem.IsAndroid())
		{
			EpubText.Navigated += OnEpubText_Navigated;
		}

		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		Dispatcher.Dispatch(async () => await UpdateWebView());
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		Dispatcher.Dispatch(async () => await UpdateWebView());
	}

	void CreateToolBarItem(int index, Chapter chapter)
	{
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
					await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
				});
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		ArgumentNullException.ThrowIfNull(Application.Current);

		EpubText.Navigating -= EpubText_Navigating;
		EpubText.Navigated -= OnEpubText_Navigated;

		WeakReferenceMessenger.Default.UnregisterAll(this);
		Shell.Current.ToolbarItems.Clear();
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
	}

	void OnEpubText_Navigated(object? sender, WebNavigatedEventArgs e)
	{
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}");
	}

	static void EpubText_Navigating(object? sender, WebNavigatingEventArgs e)
	{
		if (e.Url.Contains("http://") || e.Url.Contains("https://") || e.Url.Contains("file:"))
		{
			e.Cancel = true;
		}
	}

	async void PreviousPage(object sender, EventArgs e)
	{
		if (book.CurrentChapter <= 0)
		{
			logger.Info("Start of book");
			return;
		}
		var result = await EpubText.EvaluateJavaScriptAsync("isHorizontalScrollAtStart()");
		if (result.Equals("true"))
		{
			book.CurrentChapter--;
			await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
			isPreviousPage = true;
			Dispatcher.Dispatch(async () => await UpdateWebView());

			return;
		}
		EpubText.Eval("prevPage()");
	}

	async void NextPage(object sender, EventArgs e)
	{
		if (book.CurrentChapter >= book.Chapters.Count)
		{
			logger.Info("End of book");
			return;
		}
		var result = await EpubText.EvaluateJavaScriptAsync("isHorizontallyScrolledToEnd()");
		if (result.Equals("true"))
		{
			book.CurrentChapter++;
			await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
			Dispatcher.Dispatch(async () => await UpdateWebView());
			return;
		}
		EpubText.Eval("nextPage()");
	}

	async Task GotoEnd()
	{
		try
		{
			var result = await EpubText.EvaluateJavaScriptAsync("isHorizontallyScrolledToEnd()");
			if (result.Equals("true"))
			{
				isPreviousPage = false;
				return;
			}
			EpubText.Eval("nextPage()");
			await GotoEnd();
		}
		catch (Exception ex)
		{
			logger.Error(ex.Message);
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

	async Task UpdateWebView()
	{
		Shimmer.IsActive = true;
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		var html = InjectIntoHtml.UpdateHtml(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		EpubText.Source = new HtmlWebViewSource { Html = html };
		Shimmer.IsActive = false;
		if(isPreviousPage)
		{
			await GotoEnd();
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
