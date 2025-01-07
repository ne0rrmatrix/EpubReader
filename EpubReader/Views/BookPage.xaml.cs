using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
	readonly IDb db = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	Book book = new();
    int currentChapterIndex = 0;
	Settings settings = new();
	public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
		SettingsPageHelpers.SettingsPropertyChanged += OnSettingsClicked;
	}

	async void OnSettingsClicked(object? sender, EventArgs e)
	{
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(true);
		var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
		Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
	}

	protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        Shell.Current.ToolbarItems.Clear();
    }

    void EpubText_Navigating(object sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("http") || e.Url.StartsWith("https"))
        {
            e.Cancel = true;
        }
    }

    async void ContentPage_Loaded(object sender, EventArgs e)
    {
        book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException($"Invalid Operation: {book}");
		settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(true);
		CreateToolBar(book);
        var html = GetHtmlWithCss(book.Chapters[0].HtmlFile);
        Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[0].Title; });
    }

    void Button_Previous(object sender, EventArgs e)
    {
        if(currentChapterIndex <= 0 || currentChapterIndex >= book.Chapters.Count - 1)
        {
            return;
        }
        currentChapterIndex--;
		var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
		Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
    }

    void Button_Next(object sender, EventArgs e)
    {
        if(currentChapterIndex < 0 || currentChapterIndex >= book.Chapters.Count - 1)
        {
            return;
        }
        currentChapterIndex++;
		var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
		Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
    }

   
    void CreateToolBar(Book book)
    {
        Shell.Current.ToolbarItems.Clear();
        var chapters = book.Chapters;

        for (var i = 0; i < chapters.Count; i++)
        {
            CreateToolBarItem(i, chapters[i]);
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
				var html = GetHtmlWithCss(chapter.HtmlFile);
				EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = chapter.Title;
                
            })
        };
        MainThread.BeginInvokeOnMainThread(() => Shell.Current.ToolbarItems.Add(toolbarItem));
    }

    string GetHtmlWithCss(string html)
    {
		var css = book.Css[^1].Content ?? string.Empty;
        var cSSInject = new CssInjector(settings, css);
        var temp = cSSInject.InjectAllCss(html);
        return temp;
    }
}
