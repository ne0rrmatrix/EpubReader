using EpubReader.Views;

namespace EpubReader;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("//LibraryPage", typeof(LibraryPage));
        Routing.RegisterRoute("//BookPage", typeof(BookPage));
    }
}
