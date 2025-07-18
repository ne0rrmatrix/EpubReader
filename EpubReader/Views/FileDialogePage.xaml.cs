using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ViewModels;
using Syncfusion.Maui.Toolkit.Carousel;

namespace EpubReader.Views;

public partial class FileDialogePage : Popup
{
	FileDialogePageViewModel viewModel => (FileDialogePageViewModel)BindingContext;
	public FileDialogePage(FileDialogePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		WeakReferenceMessenger.Default.Register<BookMessage>(this, (r, m) => OnAddBooks(m.Value));
	}

	void OnAddBooks(Book value)
	{
		viewModel.EpubFiles.Add(value.DownloadUrl);
	}

	void CurrentPage_Unloaded(object sender, EventArgs e)
	{
		viewModel?.OnClose();
	}
}