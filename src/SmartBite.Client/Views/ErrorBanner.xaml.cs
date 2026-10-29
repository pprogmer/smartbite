using System.Windows;
using System.Windows.Input;

namespace SmartBite.Client.Views;

public partial class ErrorBanner
{
    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
        nameof(RetryCommand),
        typeof(ICommand),
        typeof(ErrorBanner)
    );

    public ErrorBanner() => InitializeComponent();

    /// <summary>When set, the banner offers a "Спробувати ще раз" button that runs this command.</summary>
    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }
}
