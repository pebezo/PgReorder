using System.Text;
using Terminal.Gui;

namespace PgReorder.App;

public class HelpWindow : Dialog
{
    public HelpWindow(string title)
    {
        base.ColorScheme = Colors.ColorSchemes["Dialog"];
        Title = title;
        base.Text = GetHelpBoxMessage();
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
    
    private static string GetHelpBoxMessage()
    {
        StringBuilder sb = new();

        sb.AppendLine();
        sb.AppendLine(@"     ____          ____                          __             ");           
        sb.AppendLine(@"    / __ \ ____ _ / __ \ ___   ____   _____ ____/ /___   _____  "); 
        sb.AppendLine(@"   / /_/ // __ `// /_/ // _ \ / __ \ / ___// __  // _ \ / ___/  ");
        sb.AppendLine(@"  / ____// /_/ // _, _//  __// /_/ // /   / /_/ //  __// /      ");
        sb.AppendLine(@" /_/     \__, //_/ |_| \___/ \____//_/    \__,_/ \___//_/       ");
        sb.AppendLine(@"        /____/                                                  "); 
        sb.AppendLine();
        sb.AppendLine(" F2    | Go back to the previous screen (Esc, Backspace also works)");
        sb.AppendLine(" ------[ On the columns screen ]");
        sb.AppendLine(" F5    | Move column up (Alt+Up or Ctrl+Up also works)");
        sb.AppendLine(" F8    | Move column down (Alt+Down or Ctrl+Down also works)");
        sb.AppendLine(" F9    | Copy script to clipboard (Ctrl+C also works)");
        sb.AppendLine(" Space | Select column on/off");
        sb.AppendLine(" -     | Unselect all columns");
        sb.AppendLine(" +     | Select all columns");
        sb.AppendLine(" *     | Toggle selection for all columns");
        sb.AppendLine();
        sb.AppendLine("https://github.com/pebezo/PgReorder");
        
        return sb.ToString();
    }
}