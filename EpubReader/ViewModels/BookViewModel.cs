﻿using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Views;

namespace EpubReader.ViewModels;

public partial class BookViewModel() : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial bool IsNavMenuVisible { get; set; } = true;

    Book? book;
    public Book? Book
    {
        get => book;
        set
        {
            SetProperty(ref book, value);
            IsNavMenuVisible = false;
        }
    }

    public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();

	public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj))
        {
            Book = bookObj as Book;
        }
    }

    [RelayCommand]
    static void ShowPopup()
    {
        SettingsPage popup = new();
        Shell.Current.ShowPopup(popup);
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
