using CommunityToolkit.Mvvm.ComponentModel;

namespace EpubReader.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    public readonly IDispatcher Dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
}
