using Terminal.Gui;

namespace PgReorder.App;

public class HelpWindow : Dialog
{
    public HelpWindow(string title, string text)
    {
        base.ColorScheme = Colors.ColorSchemes["Dialog"];
        Title = title;
        base.Text = text;
        BorderStyle = LineStyle.Rounded;
        
        Width = Dim.Auto (
            minimumContentDim: Dim.Func (() => (int)((Application.Screen.Width - GetAdornmentsThickness ().Horizontal) * (10 / 100f))),
            maximumContentDim: Dim.Func (() => (int)((Application.Screen.Width - GetAdornmentsThickness ().Horizontal) * 0.9f)));

        Height = Dim.Auto (
            minimumContentDim: Dim.Func (() => (int)((Application.Screen.Height - GetAdornmentsThickness ().Vertical) * (10 / 100f))),
            maximumContentDim: Dim.Func (() => (int)((Application.Screen.Height - GetAdornmentsThickness ().Vertical) * 0.9f)));

        KeyDown += (_, key) =>
        {
            if (key == Key.Esc || key == Key.Enter || key == Key.F1 || key == Key.F2 || key == Key.F10)
            {
                key.Handled = true;
                Application.RequestStop();
            }
        };
    }
}