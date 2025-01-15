using CommunityToolkit.Mvvm.ComponentModel;
using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;

namespace EpubReader.ViewModels;

public partial class BaseViewModel : ObservableObject
{
	public readonly IDispatcher Dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();

	Book book = new();
	public Book Book
	{
		get => book;
		set
		{
			SetProperty(ref book, value);
		}
	}

	public BaseViewModel()
	{
	}
}
