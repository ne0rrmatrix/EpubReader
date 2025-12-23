using System.Diagnostics;
using System.IO;
using Microsoft.Maui.Storage;

namespace EpubReader.Views;

public partial class PrivacyPage : ContentPage
{
	public PrivacyPage(PrivacyPageViewModel viewModel)
	{
		BindingContext = viewModel;
		InitializeComponent();
	}

}