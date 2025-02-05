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
		SetColors();
		Dispatcher.Dispatch(() => PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}");
		book.Chapters.ForEach(chapter => CreateToolBarItem(book.Chapters.IndexOf(chapter), chapter));
		EpubText.Navigating += EpubText_Navigating;
		WeakReferenceMessenger.Default.Register<SettingsMessage>(this, (r, m) => OnSettingsClicked());
		if (!OperatingSystem.IsAndroid())
		{
			EpubText.Navigated += OnEpubText_Navigated;
		}
	}

	
	async void OnSettingsClicked()
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
		SetColors();
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}";
		Dispatcher.Dispatch(() => EpubText.Source = new HtmlWebViewSource { Html = html });
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
				Dispatcher.Dispatch(() => 
				{ 
					EpubText.Source = new HtmlWebViewSource { Html = html }; 
					PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}"; 
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
		await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);

		Dispatcher.Dispatch(() => 
		{ 
			EpubText.Source = new HtmlWebViewSource { Html = html }; 
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}"; 
		});
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
		await db.SaveBookData(book, CancellationToken.None).ConfigureAwait(false);
		var html = InjectIntoHtml.InjectAllCss(book.Chapters[book.CurrentChapter].HtmlFile, book, settings);
		Dispatcher.Dispatch(() => 
		{ 
			EpubText.Source = new HtmlWebViewSource { Html = html }; 
			PageLabel.Text = $"{book.Chapters[book.CurrentChapter]?.Title ?? string.Empty}"; 
		});
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

	void SetColors()
	{
		if (string.IsNullOrEmpty(settings.BackgroundColor))
		{
			if (Application.Current?.PlatformAppTheme == AppTheme.Dark)
			{
				settings.BackgroundColor = "#FFFBF5";
				settings.TextColor = "#000000";
			}
			else
			{
				settings.BackgroundColor = "#121212";
				settings.TextColor = "#FFFFFF";
			}
		}

		Dispatcher.Dispatch(() =>
		{
			Grid.BackgroundColor = Color.FromArgb(settings.BackgroundColor);
			StackLayout.BackgroundColor = Color.FromArgb(settings.BackgroundColor);
			PageLabel.TextColor = Color.FromArgb(settings.TextColor);
			PageLabel.BackgroundColor = Color.FromArgb(settings.BackgroundColor);
			var color = Color.FromRgba(settings.BackgroundColor);
			Shell.SetBackgroundColor(Application.Current?.Windows[0].Page, color);
			CurrentPage.BackgroundColor = color;
		});
	}
}
