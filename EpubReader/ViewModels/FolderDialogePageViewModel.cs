using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using EpubReader.Util;

namespace EpubReader.ViewModels;
public partial class FolderDialogPageViewModel : BaseViewModel
{
	readonly ProcessEpubFiles processEpubFiles = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ProcessEpubFiles>() ?? throw new InvalidOperationException();
	[ObservableProperty]
	public partial string Text { get; set; } = "Please wait...";

	/// <summary>
	/// Gets or sets the list of EPUB file paths.
	/// </summary>
	[ObservableProperty]
	public partial List<string> EpubFiles { get; set; } = [];

	/// <summary>
	/// Gets or sets the current count value.
	/// </summary>
	[ObservableProperty]
	public partial int Count { get; set; } = 0;

	/// <summary>
	/// Gets or sets a value indicating whether the element should be visible.
	/// </summary>
	[ObservableProperty]
	public partial bool ShouldBeVisible { get; set; } = false;

	public FolderDialogPageViewModel()
	{
		WeakReferenceMessenger.Default.Register<FolderMessage>(this, (r, m) => { Text = $"{m.Value.Title} ({m.Value.Count}/{m.Value.MaxCount})"; });
	}

	/// <summary>
	/// Releases the unmanaged resources used by the class and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only
	/// unmanaged resources.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			processEpubFiles?.Dispose();
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Cancels the ongoing operation and updates the visibility state.
	/// </summary>
	/// <remarks>This method triggers the cancellation of the current operation by signaling the associated <see
	/// cref="CancellationTokenSource"/>. After cancellation, it sets the visibility state to indicate that the operation
	/// is no longer active.</remarks>
	[RelayCommand]
	void Cancel()
	{
		WeakReferenceMessenger.Default.Send(new CalibreMessage(true));
		ShouldBeVisible = false;
	}

	/// <summary>
	/// Handles the closing event by updating visibility and unregistering from message notifications.
	/// </summary>
	/// <remarks>This method sets the visibility flag to false and unregisters the current instance from all
	/// messages using the default messenger. It should be called when the object is being closed or disposed to ensure
	/// proper cleanup.</remarks>
	public void OnClose()
	{
		ShouldBeVisible = false;
		WeakReferenceMessenger.Default.UnregisterAll(this);
	}
}
