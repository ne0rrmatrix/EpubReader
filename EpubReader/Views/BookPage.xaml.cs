#if ANDROID
using Android.Views;
using AndroidX.Core.View;
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

public partial class BookPage : ContentPage
{
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
	Book book = new();
	Settings settings = new();
	public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
		BindingContext = viewModel;
	}

	void CurrentPage_Loaded(object sender, EventArgs e)
	{
		book = ((BookViewModel)BindingContext).Book;
		settings = ((BookViewModel)BindingContext).Settings;
	
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		logger.Info("Toolbar created");

		EpubText.Navigating += EpubText_Navigating;
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		if (!OperatingSystem.IsAndroid())
		{
			EpubText.Navigated += OnEpubText_Navigated;
		}
		
	}

	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		if(Dispatcher.IsDispatchRequired)
		{
			Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
		}
		else
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
		}
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
				var html = InjectIntoHtml.InjectAllCss(chapter.HtmlFile, book, settings);
				if(Dispatcher.IsDispatchRequired)
				{
					Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
				}
				else
				{
					EpubText.Source = new HtmlWebViewSource { Html = html };
				}
			})
		};
		Shell.Current.ToolbarItems.Add(toolbarItem);
	}

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        EpubText.Navigating -= EpubText_Navigating;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        EpubText.Navigated -= OnEpubText_Navigated;

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

	async Task PreviousPage()
	{
		if(book.CurrentChapter <= 0)
		{
			logger.Info("Start of book");
			return;
		}
		book.CurrentChapter--;
		await db.SaveBookData(book, CancellationToken.None);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		if (Dispatcher.IsDispatchRequired)
		{
			Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
		}
		else
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
		}
	}
	
	async void PreviousPage(object sender, EventArgs e)
	{
		await PreviousPage();
	}

	
	async void NextPage(object sender, EventArgs e)
	{
		await NextPage();
	}

	async Task NextPage()
	{
		if(book.CurrentChapter >= book.Chapters.Count)
		{
			logger.Info("End of book");
			return;
		}
		book.CurrentChapter++;
		await db.SaveBookData(book, CancellationToken.None);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		if (Dispatcher.IsDispatchRequired)
		{
			Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; });
		}
		else
		{
			EpubText.Source = new HtmlWebViewSource { Html = html };
		}
	}

	public async void SwipeGestureRecognizer_Swiped(object sender, SwipedEventArgs e)
	{
		if (e.Direction == SwipeDirection.Left)
		{
			await NextPage();
		}
		else if (e.Direction == SwipeDirection.Right)
		{
			await PreviousPage();
		}
	}
}
