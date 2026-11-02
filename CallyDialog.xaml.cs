using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Cally;

public partial class CallyDialog : Window
{
    public CallyDialog()
    {
        InitializeComponent();
        MaxHeight=Math.Max(240,SystemParameters.WorkArea.Height-32);
        Width=Math.Min(Width,SystemParameters.WorkArea.Width-24);
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Finish(false);}};
    }
    void Finish(bool accepted){if(ShowActivated)DialogResult=accepted;else Close();}
    void MoveDialog(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton==MouseButton.Left && e.LeftButton==MouseButtonState.Pressed) DragMove();
    }
    void Dismiss(object sender,RoutedEventArgs e)=>Finish(false);
    void Accept(object sender,RoutedEventArgs e){if(ValidateInput()) Finish(true);}
    protected virtual bool ValidateInput()=>true;

    public static bool Prompt(string title,string details,string action="Close",bool destructive=false)
    {
        var dialog=new CallyDialog { Title=title };
        dialog.Body.Children.Add(new TextBlock {Text=details,TextWrapping=TextWrapping.Wrap,LineHeight=21});
        dialog.AcceptAction.Content=action;
        dialog.CancelAction.Visibility=action=="Close"?Visibility.Collapsed:Visibility.Visible;
        if(destructive) dialog.AcceptAction.Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(150,52,65));
        dialog.Loaded+=(_,_)=>{if(destructive)dialog.CancelAction.Focus(); else dialog.AcceptAction.Focus();};
        return dialog.ShowDialog()==true;
    }
}

