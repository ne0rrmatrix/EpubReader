using EpubReader.ViewModels;

namespace EpubReader.Views;

public partial class CalibrePage : ContentPage, IDisposable
{
	CalibrePageViewModel ViewModel => (CalibrePageViewModel)BindingContext;
	bool disposedValue;
	readonly bool isLoaded = false;
	public CalibrePage(CalibrePageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		isLoaded = true;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (!isLoaded)
		{
			return;
		}
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