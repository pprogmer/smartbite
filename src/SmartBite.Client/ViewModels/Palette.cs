using System.Windows.Media;

namespace SmartBite.Client.ViewModels;

/// <summary>Colors that view models hand to the UI (the rest are in App.xaml).</summary>
public static class Palette
{
    public static readonly Brush Protein = Make("#2663AA");
    public static readonly Brush Fat = Make("#C27C00");
    public static readonly Brush Carbs = Make("#C0629F");
    public static readonly Brush Ok = Make("#2E7D4F");
    public static readonly Brush Over = Make("#C42B25");
    public static readonly Brush Empty = Make("#D3CDC2");

    private static Brush Make(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
