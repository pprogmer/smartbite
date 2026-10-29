using System.Windows;
using SmartBite.Client.ViewModels;

namespace SmartBite.Client.Views;

public partial class EstimateView
{
    public EstimateView() => InitializeComponent();

    // A photo can be dropped on the grey area instead of being chosen with the button.
    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            ((EstimateViewModel)DataContext).SetPhoto(files[0]);
        }
    }
}
