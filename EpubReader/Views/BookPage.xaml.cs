using EpubReader.Models;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
    static readonly string DarkModeBackgroundColor = "#1E1E1E";
    static readonly string DarkModeTextColor = "#D3D3D3";
    static readonly string LightModeBackgroundColor = "#FFFFFF";
    static readonly string LightModeTextColor = "#000000";
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
    Book book = new();
    int currentChapterIndex = 0;
    public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        ArgumentNullException.ThrowIfNull(Application.Current);
        currentChapterIndex = 0;
        var (textColor, backgroundColor) = GetColors();
        Application.Current.RequestedThemeChanged += Current_RequestedThemeChanged;
    }

    private void Current_RequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        string html = e.RequestedTheme switch
        {
            AppTheme.Dark => AddColors(book.Chapters[currentChapterIndex].HtmlFile, DarkModeBackgroundColor, DarkModeTextColor),
            AppTheme.Light => AddColors(book.Chapters[currentChapterIndex].HtmlFile, LightModeBackgroundColor, LightModeTextColor),
            _ => AddColors(book.Chapters[currentChapterIndex].HtmlFile, LightModeBackgroundColor, LightModeTextColor)
        };
        Dispatcher.Dispatch(() => EpubText.Source = new HtmlWebViewSource { Html = html });
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        Shell.Current.ToolbarItems.Clear();
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
                var (textColor, backgroundColor) = GetColors();
                var css = book.Css[^1].Content ?? string.Empty;
                string html = InjectCss(chapter.HtmlFile, css);
                html = AddColors(html, backgroundColor, textColor);
                EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = chapter.Title;
            })
        };
        MainThread.BeginInvokeOnMainThread(() => Shell.Current.ToolbarItems.Add(toolbarItem));
    }

    public static string AddColors(string htmlContent, string backgroundColor, string textColor)
    {
        string styleTag = $@"
            body {{
                background-color: {backgroundColor};
                color: {textColor};
            }}";

       return InjectCss(htmlContent, styleTag);
    }

    static string InjectCss(string htmlContent, string cssToInject)
    {
        string styleStartTag = "<style type='text/css' title='override_css'>";
        string styleEndTag = "</style>";
        bool styleStartTagExists = htmlContent.Contains(styleStartTag);
        bool styleEndTagExists = htmlContent.Contains(styleEndTag);

        if (!styleStartTagExists || !styleEndTagExists)
        {
            // If no <style> tag exists, create one at the end of the <head> section
            int headEndPosition = htmlContent.IndexOf("</head>");
            if (headEndPosition == -1)
            {
                logger.Info("No <head> tag found in the HTML content.");
                return htmlContent;
            }

            string styleTag = $"{styleStartTag}\n{cssToInject}\n{styleEndTag}";
            htmlContent = htmlContent.Insert(headEndPosition, styleTag);
        }
        else
        {
            // Inject the CSS before the closing </style> tag
            int styleEndPosition = htmlContent.IndexOf(styleEndTag);
            htmlContent = htmlContent.Insert(styleEndPosition, cssToInject);
        }
        return htmlContent;
    }

    void ContentPage_Loaded(object sender, EventArgs e)
    {
        book = ((BookViewModel)BindingContext).Book ?? throw new InvalidOperationException($"Invalid Operation: {book}");
        var (textColor, backgroundColor) = GetColors();
        var css = book.Css[^1].Content ?? string.Empty;
        string html = InjectCss(book.Chapters[0].HtmlFile, css);
        html = AddColors(html, backgroundColor, textColor);
        CreateToolBar(book);
        Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[0].Title; });
    }

    static (string textColor, string backgroundColor) GetColors()
    {
        var currentTheme = Application.Current?.RequestedTheme;
        return currentTheme switch
        {
            AppTheme.Dark => (DarkModeTextColor, DarkModeBackgroundColor),
            AppTheme.Light => (LightModeTextColor, LightModeBackgroundColor),
            _ => (LightModeTextColor, LightModeBackgroundColor)
        };
    }

    void Button_Previous(object sender, EventArgs e)
    {
        if(currentChapterIndex <= 0 || currentChapterIndex >= book.Chapters.Count - 1)
        {
            return;
        }
        currentChapterIndex--;
        var (textColor, backgroundColor) = GetColors();
        var css = book.Css[^1].Content ?? string.Empty;
        string html = InjectCss(book.Chapters[currentChapterIndex].HtmlFile, css);
        html = AddColors(html, backgroundColor, textColor);
        Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
    }

    void Button_Next(object sender, EventArgs e)
    {
        if(currentChapterIndex < 0 || currentChapterIndex >= book.Chapters.Count - 1)
        {
            return;
        }
        currentChapterIndex++;
        var (textColor, backgroundColor) = GetColors();
        var css = book.Css[^1].Content ?? string.Empty;
        string html = InjectCss(book.Chapters[currentChapterIndex].HtmlFile, css);
        html = AddColors(html, backgroundColor, textColor);
        Dispatcher.Dispatch(() => { EpubText.Source = new HtmlWebViewSource { Html = html }; ChapterLabel.Text = book.Chapters[currentChapterIndex].Title; });
    }

    void EpubText_Navigating(object sender, WebNavigatingEventArgs e)
    {
        if(e.Url.StartsWith("http") || e.Url.StartsWith("https"))
        {
            var data = e.Url.Remove(0, 15);
            System.Diagnostics.Debug.WriteLine(data);
            System.Diagnostics.Debug.WriteLine($"Navigating to {e.Url}");
            var temp = book.Chapters.Find(c => c.FileName.Contains(e.Url));
            e.Cancel = true;
        }
    }
}
