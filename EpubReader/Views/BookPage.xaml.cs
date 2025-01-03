using EpubReader.Models;
using EpubReader.Service;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class BookPage : ContentPage
{
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookPage));
    Book book = new();
    int currentPageIndex = 0;
    int currentChapter = 0;
    TextPaginator? paginator;
    public BookPage(BookViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    void CreateToolBar(Book book)
    {
        Shell.Current.ToolbarItems.Clear();
        var chapters = book.Chapters;
        for (var i = 0; i < chapters.Count; i++)
        {
            int currentIndex = i;
            var toolbarItem = new ToolbarItem
            {
                Text = chapters[i].Title,
                Order = ToolbarItemOrder.Secondary,
                Priority = i,
                Command = new Command(() =>
                {
                    book = ((BookViewModel)BindingContext).Book ?? new();
                    paginator = new TextPaginator(400, 700);
                    LoadChapter(currentIndex);
                    currentPageIndex = 0;
                    UpdatePageDisplay();
                })
            };
            Shell.Current.ToolbarItems.Add(toolbarItem);
        }
    }
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
        {
            return;
        }
        book = ((BookViewModel)BindingContext).Book ?? new();
        paginator = new TextPaginator((int)width, (int)height);
        currentChapter = 0;
        currentPageIndex = 0;
        LoadChapter(currentChapter);
        CreateToolBar(book);
        while (paginator.Pages.Count == 0)
        {
            currentChapter++;
            if (book.Chapters.Count <= currentChapter)
            {
                break;
            }
            LoadChapter(currentChapter);
            currentPageIndex = 0;
        }
        UpdatePageDisplay();
    }
    void LoadChapter(int chapterIndex)
    {
        currentChapter = chapterIndex;
        var chapter = book.Chapters[currentChapter];
        paginator?.PaginateText(chapter.PlainText, EpubText);
    }
    void UpdatePageDisplay()
    {

        if(paginator?.Pages.Count == 0 || currentPageIndex > paginator?.Pages.Count)
        {
            MainThread.BeginInvokeOnMainThread(() => EpubText.Text = string.Empty);
            EpubText.Text = string.Empty;
            return;
        }
        MainThread.BeginInvokeOnMainThread(() => EpubText.Text = paginator?.Pages[currentPageIndex]);
    }
    void SwipeGestureRecognizer_Swiped(object sender, SwipedEventArgs e)
    {
        switch (e.Direction)
        {
            case SwipeDirection.Left:
                if(paginator?.Pages.Count == 0)
                {
                    currentChapter++;
                    book = ((BookViewModel)BindingContext).Book ?? new();
                    paginator = new TextPaginator(400, 700);
                    LoadChapter(currentChapter);
                    currentPageIndex = 0;
                    UpdatePageDisplay();
                    break;
                }
                if (currentPageIndex < paginator?.Pages.Count - 1)
                {
                    currentPageIndex++;
                    UpdatePageDisplay();
                    break;
                }
                if (currentPageIndex == paginator?.Pages.Count - 1)
                {
                    currentChapter++;
                    LoadChapter(currentChapter);
                    currentPageIndex = 0;
                    UpdatePageDisplay();
                    break;
                }
                break;
            case SwipeDirection.Right:
                if (currentPageIndex > 0)
                {
                    currentPageIndex--;
                    UpdatePageDisplay();
                    break;
                }
                else if (currentPageIndex == 0 && currentChapter > 0)
                {
                    currentChapter--;
                    book = ((BookViewModel)BindingContext).Book ?? new();
                    paginator = new TextPaginator(400, 700);
                    LoadChapter(currentChapter);
                    if (paginator?.Pages.Count - 1 > 0)
                    {
                        currentPageIndex = paginator?.Pages.Count - 1 ?? 0;
                    }
                    else
                    {
                        currentPageIndex = 0;
                    }
                    UpdatePageDisplay();
                    break;
                }
                break;
        }
    }
}
