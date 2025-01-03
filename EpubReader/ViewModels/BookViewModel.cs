using EpubReader.Models;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
    int currentChapter = 0;
    public int CurrentChapter
    {
        get => currentChapter;
        set
        {
            SetProperty(ref currentChapter, value);
        }
    }
    public string? Title => Book?.Title;
    public string? CoverImageFileName => Book?.CoverImageFileName;
    public string? HtmlFile => Book?.Chapters[CurrentChapter].HtmlFile;
    Book? book;
    public Book? Book
    {
        get => book;
        set
        {
            SetProperty(ref book, value);
        }
    }
    public BookViewModel()
    {
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj))
        {
            Book = bookObj as Book;
        }
    }
}
