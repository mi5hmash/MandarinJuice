using System.Windows;
using System.Windows.Media;
using Mi5hmasH.WpfHelper;

namespace GameProfileEditor;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Set the theme accent
        var colorAccent = new ColorAccentModel(
            Color.FromRgb(255, 140, 0),
            Color.FromRgb(255, 153, 16),
            Color.FromRgb(255, 182, 52),
            Color.FromRgb(255, 209, 85),
            Color.FromRgb(225, 157, 0),
            Color.FromRgb(155, 93, 0),
            Color.FromRgb(92, 33, 0));
        WpfThemeAccent.SetThemeAccent(colorAccent);
    }
}