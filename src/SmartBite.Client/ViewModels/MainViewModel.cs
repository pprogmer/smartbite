namespace SmartBite.Client.ViewModels;

public class MainViewModel : ViewModelBase
{
    private ViewModelBase? _current;

    public ViewModelBase? Current => _current;

    public void Navigate(ViewModelBase viewModel) => Set(ref _current, viewModel, nameof(Current));
}
