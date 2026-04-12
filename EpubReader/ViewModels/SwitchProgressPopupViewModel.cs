namespace EpubReader.ViewModels;

/// <summary>
/// Represents the view model for the synced progress confirmation popup.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SwitchProgressPopupViewModel"/> class.
/// </remarks>
/// <param name="title">The popup title.</param>
/// <param name="message">The popup message.</param>
/// <param name="confirmText">The confirmation button text.</param>
/// <param name="cancelText">The cancellation button text.</param>
public partial class SwitchProgressPopupViewModel(string title, string message, string confirmText, string cancelText) : BaseViewModel
{
	[ObservableProperty]
	public partial string Title { get; set; } = title;

	[ObservableProperty]
	public partial string Message { get; set; } = message;

	[ObservableProperty]
	public partial string ConfirmText { get; set; } = confirmText;

	[ObservableProperty]
	public partial string CancelText { get; set; } = cancelText;

	/// <summary>
	/// Occurs when the popup requests to close with a result.
	/// </summary>
	public event Action<bool>? CloseRequested;

	[RelayCommand]
	void Confirm()
	{
		CloseRequested?.Invoke(true);
	}

   [RelayCommand]
	void Cancel()
	{
		CloseRequested?.Invoke(false);
	}
}
