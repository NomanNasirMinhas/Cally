using System.Windows;
using System.Windows.Controls;

namespace Cally;

public class FeedDialog : CallyDialog
{
    public Feed? Result { get; private set; }
    public FeedDialog(Feed? existing = null)
    {
        Title="Connect a calendar to Cally"; Width=440; SizeToContent=SizeToContent.Height;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var body=Body;

        var provider=new ComboBox {ItemsSource=new[]{"Google","Outlook","iCloud"},SelectedIndex=0};
        var name=new TextBox(); var url=new PasswordBox(); var hint=new TextBlock {TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,6,0,10)};
        var error=new TextBlock {TextWrapping=TextWrapping.Wrap};
        void Field(string caption, Control input)
        {
            var label=new Label {Content=caption,Target=input,Foreground=System.Windows.Media.Brushes.White}; input.Padding=new Thickness(8,5,8,5);
            if(input is TextBox or PasswordBox){ input.Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(29,43,66)); input.Foreground=System.Windows.Media.Brushes.White; input.BorderBrush=System.Windows.Media.Brushes.SlateGray; } body.Children.Add(label); body.Children.Add(input);
        }
        Field("_Provider",provider); body.Children.Add(hint);
        void Explain() => hint.Text=provider.SelectedIndex switch {
            0=>"Google Calendar → Settings → your calendar → Integrate calendar. Copy the secret address in iCal format. Treat this link like a password.",
            1=>"Outlook on the web → Settings → Calendar → Shared calendars → Publish a calendar. Use the ICS link. Anyone with that link can see the published details.",
            _=>"iCloud Calendar → Calendar Information → Public Calendar → Copy Link. Anyone with this link can view the shared calendar."};
        provider.SelectionChanged+=(_,_)=>Explain(); Explain();
        if(existing != null) { Title="Edit calendar"; provider.SelectedItem=existing.Provider; name.Text=existing.Name; url.Password=existing.Url; }
        Field("Calendar _name",name); Field("Calendar _link",url); body.Children.Add(error);
        AcceptAction.Content=existing == null ? "Connect calendar" : "Save changes";
        AcceptAction.IsDefault=true;
        error.Foreground=System.Windows.Media.Brushes.LightSalmon;
        Loaded+=(_,_)=>name.Focus();
        validate=()=>{
            var address=url.Password.Trim();
            if(address.StartsWith("webcal://",StringComparison.OrdinalIgnoreCase)) address="https://"+address[9..];
            if(!Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.UserInfo.Length!=0)
            {error.Text="Enter an HTTPS or webcal calendar subscription link.";url.Focus();return false;}
            string source=(string)provider.SelectedItem;
            Result=new(existing?.Id ?? Guid.NewGuid(),source,string.IsNullOrWhiteSpace(name.Text)?source:name.Text.Trim(),uri.AbsoluteUri);
            return true;
        };
    }
    readonly Func<bool> validate;
    protected override bool ValidateInput()=>validate();
}

