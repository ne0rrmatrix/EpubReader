using CommunityToolkit.Maui.Views;
using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class FolderDialogePage : Popup
{
	FolderDialogePageViewModel ViewModel => (FolderDialogePageViewModel)BindingContext;
	public FolderDialogePage(FolderDialogePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	void CurrentPage_Unloaded(object sender, EventArgs e)
	{
		if (ViewModel is not null)
		{
			ViewModel.OnClose();
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("ViewModel is null, cannot close.");
		}
	}
}