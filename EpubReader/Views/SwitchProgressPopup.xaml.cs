using CommunityToolkit.Maui.Views;
using EpubReader.ViewModels;

namespace EpubReader.Views;

/// <summary>
/// Represents a confirmation popup used when the reader can switch to a synced reading position.
/// </summary>
public sealed partial class SwitchProgressPopup : Popup<bool>, IDisposable
{
   readonly SwitchProgressPopupViewModel viewModel;
	bool disposedValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="SwitchProgressPopup"/> class.
	/// </summary>
	/// <param name="title">The popup title.</param>
	/// <param name="message">The popup message.</param>
	/// <param name="confirmText">The confirmation button text.</param>
	/// <param name="cancelText">The cancellation button text.</param>
	public SwitchProgressPopup(string title, string message, string confirmText, string cancelText)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(confirmText);
		ArgumentException.ThrowIfNullOrWhiteSpace(cancelText);

      viewModel = new SwitchProgressPopupViewModel(title, message, confirmText, cancelText);
		viewModel.CloseRequested += OnCloseRequested;
		BindingContext = viewModel;

		InitializeComponent();
	}

   async void OnCloseRequested(bool result)
	{
      viewModel.CloseRequested -= OnCloseRequested;
		await CloseAsync(result);
       Dispose();
	}

	void Dispose(bool disposing)
	{
		if (disposedValue)
		{
			return;
		}

		if (disposing)
		{
			viewModel.CloseRequested -= OnCloseRequested;
			viewModel.Dispose();
		}

		disposedValue = true;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
