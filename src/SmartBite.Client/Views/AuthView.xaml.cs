using System.Windows;
using System.Windows.Controls;
using SmartBite.Client.ViewModels;

namespace SmartBite.Client.Views;

public partial class AuthView
{
    public AuthView() => InitializeComponent();

    // PasswordBox.Password cannot be bound, so the passwords are passed to the view model here.
    private void Password_Changed(object sender, RoutedEventArgs e) =>
        ((AuthViewModel)DataContext).Password = ((PasswordBox)sender).Password;

    private void ConfirmPassword_Changed(object sender, RoutedEventArgs e) =>
        ((AuthViewModel)DataContext).ConfirmPassword = ((PasswordBox)sender).Password;
}
