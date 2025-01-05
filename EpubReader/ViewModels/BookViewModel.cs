using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Database;
using EpubReader.Models;

namespace EpubReader.ViewModels;

public partial class BookViewModel(Db db) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial bool IsNavMenuVisible { get; set; } = false;

    Book? book;
    public Book? Book
    {
        get => book;
        set
        {
            SetProperty(ref book, value);
        }
    }

    public readonly Db db = db;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj))
        {
            Book = bookObj as Book;
        }
    }

    [RelayCommand]
    void LongPress()
    {
        if (IsNavMenuVisible)
        {
            IsNavMenuVisible = false;
            Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, false);
        }
        else
        {
            IsNavMenuVisible = true;
            Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
        }
    }
}
