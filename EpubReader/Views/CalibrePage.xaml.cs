using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class CalibrePage : ContentPage, IDisposable
{
	CalibrePageViewModel ViewModel => (CalibrePageViewModel)BindingContext;
	bool disposedValue;

	public CalibrePage(CalibrePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await ViewModel.LoadBooks();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				ViewModel?.Dispose();
			}
			
			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}