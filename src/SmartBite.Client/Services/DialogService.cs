using System.Windows;
using Microsoft.Win32;
using SmartBite.Client.ViewModels;
using SmartBite.Client.Views;

namespace SmartBite.Client.Services;

/// <summary>Windows opened above the main window. View models use it instead of touching windows directly.</summary>
public class DialogService
{
    private static Window? Owner => Application.Current.MainWindow;

    /// <summary>Shows the diary entry dialog; true if the entry was saved.</summary>
    public bool ShowEntry(DiaryEntryViewModel entry) =>
        new DiaryEntryWindow { DataContext = entry, Owner = Owner }.ShowDialog() == true;

    public bool Confirm(string text) =>
        MessageBox.Show(Owner, text, "SmartBite", MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    /// <summary>Returns the chosen photo path, or null if the user cancelled.</summary>
    public string? PickPhoto()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Зображення (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
}
