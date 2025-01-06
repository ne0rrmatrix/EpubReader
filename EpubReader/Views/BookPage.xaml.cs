using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
    string backgroundColor = string.Empty;
    string textColor = string.Empty;
    string fontFamily = string.Empty;
    int fontSize = 0;
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
    Book book = new();
    int currentChapterIndex = 0;
    public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        ArgumentNullException.ThrowIfNull(Application.Current);
        RegisterWeakReferences();
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        Shell.Current.ToolbarItems.Clear();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    void EpubText_Navigating(object sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("http") || e.Url.StartsWith("https"))
        {
            var data = e.Url.Remove(0, 15);
            System.Diagnostics.Debug.WriteLine(data);
            System.Diagnostics.Debug.WriteLine($"Navigating to {e.Url}");
            var temp = book.Chapters.Find(c => c.FileName.Contains(e.Url));
            e.Cancel = true;
        }
    }

    void RegisterWeakReferences()
    {
        WeakReferenceMessenger.Default.Register<FontSizeMessage>(this, (r, m) =>
        {
            if (book.Chapters.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Book has no chapters");
                return;
            }
            fontSize = m.FontSize;
            var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
            Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
        });
        WeakReferenceMessenger.Default.Register<FontMessage>(this, (r, m) =>
        {
            if (book.Chapters.Count == 0)
            {
                return;
            }
            fontFamily = m.FontFamily;

            var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
            Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
        });
        WeakReferenceMessenger.Default.Register<ColorMessage>(this, (r, m) =>
        {
            if (book.Chapters.Count == 0)
            {
                return;
            }
            textColor = m.TextColor;
            backgroundColor = m.BackgroundColor;
            var html = GetHtmlWithCss(book.Chapters[currentChapterIndex].HtmlFile);
            Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
        });
    }
   
    void ContentPage_Loaded(object sender, EventArgs e)
    {
        book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException($"Invalid Operation: {book}");
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
        var cSSInject = new CssInjector(backgroundColor, textColor, fontSize, fontFamily, css);
        var temp = cSSInject.InjectAllCss(html);
        return temp;
    }
}
