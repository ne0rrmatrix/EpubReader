using System.IO;
using System.Diagnostics;
using Microsoft.Maui.Storage;

namespace EpubReader.Views;

public partial class PrivacyPage : ContentPage
{
    public PrivacyPage(PrivacyPageViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
	protected override async void OnAppearing()
	{
        await LoadPolicyHtml();
		base.OnAppearing();
	}

    async Task LoadPolicyHtml()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("privacy.html");
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();
            PolicyWebView.Source = new HtmlWebViewSource { Html = html };
        }
        catch (Exception ex)
        {
            PolicyWebView.Source = new HtmlWebViewSource { Html = "<p>Unable to load privacy policy.</p>" };
            Trace.TraceWarning($"PrivacyPage: failed to load privacy.html: {ex}");
        }
    }
}
