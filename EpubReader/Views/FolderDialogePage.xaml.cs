using CommunityToolkit.Maui.Views;
using EpubReader.ViewModels;

namespace EpubReader.Views;

/// <summary>
/// Represents a popup dialog page for folder selection.
/// </summary>
/// <remarks>This class is used to display a dialog that allows users to select a folder. It is initialized with a
/// <see cref="FolderDialogPageViewModel"/> which provides the necessary data and commands for the dialog's operation.
/// The dialog automatically calls the <see cref="FolderDialogPageViewModel.OnClose"/> method when it is
/// unloaded.</remarks>
public partial class FolderDialogePage : Popup
{
	FolderDialogPageViewModel ViewModel => (FolderDialogPageViewModel)BindingContext;
	public FolderDialogePage(FolderDialogPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	void CurrentPage_Unloaded(object? sender, EventArgs? e)
	{
		ViewModel?.OnClose();
	}
}