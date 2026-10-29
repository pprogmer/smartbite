using System.Windows;
using SmartBite.Client.ViewModels;

namespace SmartBite.Client.Views;

/// <summary>The dialog of a diary entry; it closes when its view model reports the result.</summary>
public partial class DiaryEntryWindow
{
    public DiaryEntryWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
            ((DiaryEntryViewModel)DataContext).CloseRequested += saved => DialogResult = saved;
    }
}
