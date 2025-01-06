using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;

namespace EpubReader.Views;

public partial class SettingsPage : Popup
{
    int fontSize = 0;
    public SettingsPage()
    {
        InitializeComponent(); ;
        BindingContext = this;
    }
 
    void OnApplyColorChanged(object sender, EventArgs e)
    {
        string backgroundColorArgb;
        string textColorArgb;
        var selectedTheme = ThemePicker.SelectedItem.ToString();
        switch (selectedTheme)
        {
            case "Dark":
                backgroundColorArgb = "#1E1E1E"; // Soft warm background
                textColorArgb = "#D3D3D3"; // Dark text
                break;

            case "Light":
                backgroundColorArgb = "#FFFFFF"; // Light gray background
                textColorArgb = "#000000"; // Black text
                break;

            case "Sepia":
                backgroundColorArgb = "#f4ecd8"; // Sepia background
                textColorArgb = "#5b4636"; // Dark brown text
                break;

            case "Night Mode":
                backgroundColorArgb = "#000000"; // Dark background
                textColorArgb = "#ffffff"; // Light text
                break;

            case "Daylight":
                backgroundColorArgb = "#ffffff"; // Bright white background
                textColorArgb = "#000000"; // Dark text
                break;

            case "Forest":
                backgroundColorArgb = "#e0f2e9"; // Greenish background
                textColorArgb = "#2e4d38"; // Dark green text
                break;

            case "Ocean":
                backgroundColorArgb = "#e0f7fa"; // Light blue background
                textColorArgb = "#01579b"; // Navy text
                break;

            case "Sand":
                backgroundColorArgb = "#f5deb3"; // Sandy background
                textColorArgb = "#000000"; // Dark text
                break;

            case "Charcoal":
                backgroundColorArgb = "#36454f"; // Dark gray background
                textColorArgb = "#dcdcdc"; // Light gray text
                break;

            case "Vintage":
                backgroundColorArgb = "#f5f5dc"; // Yellowed paper background
                textColorArgb = "#000000"; // Dark text
                break;

            default:
                // Default color scheme if none match
                backgroundColorArgb = "#FFFFFF"; // Default white background
                textColorArgb = "#000000"; // Default black text
                break;
        }

        if (string.IsNullOrEmpty(backgroundColorArgb) || string.IsNullOrEmpty(textColorArgb))
        {
            return;
        }
        var message = new ColorMessage(backgroundColorArgb, textColorArgb);
        WeakReferenceMessenger.Default.Send(message);
    }

    void OnFontSizeSliderChanged(object sender, ValueChangedEventArgs e)
    {
        if(fontSize == (int)e.NewValue)
        {
            return;
        }
        fontSize = (int)e.NewValue;
        WeakReferenceMessenger.Default.Send(new FontSizeMessage(fontSize));
    }

    void OnFontChange(object sender, EventArgs e)
    {
        if (FontPicker.SelectedItem is not string selectedFont)
        {
            return;
        }
        var font = $"'{selectedFont}', sans-serif";
        WeakReferenceMessenger.Default.Send(new FontMessage(font));
    }

    void OnSystemThemeSwitchToggled(object sender, ToggledEventArgs e)
    {
        string backgroundColorArgb;
        string textColorArgb;
        if (e.Value)
        {
            ArgumentNullException.ThrowIfNull(Application.Current);
            if (Application.Current.RequestedTheme == AppTheme.Dark)
            {
                backgroundColorArgb = "#1E1E1E";
                textColorArgb = "#D3D3D3";
            }
            else
            {
                backgroundColorArgb = "#FFFFFF";
                textColorArgb = "#000000";
            }
            if (string.IsNullOrEmpty(backgroundColorArgb) || string.IsNullOrEmpty(textColorArgb))
            {
                return;
            }
            var message = new ColorMessage(backgroundColorArgb, textColorArgb);
            WeakReferenceMessenger.Default.Send(message);
        }
    }
}
