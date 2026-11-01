using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Cally;

public partial class Widget : Window
{
    readonly Preferences settings;
    readonly Dictionary<Guid,List<Meeting>> events=new();
    readonly Dictionary<Guid,string> errors=new();
    readonly DispatcherTimer clock=new() {Interval=TimeSpan.FromSeconds(15)};
    readonly DispatcherTimer updateTimer = new() { Interval=TimeSpan.FromHours(12) };
    Task<Version?>? updateRequest;
    Version? availableUpdate;
    bool closed;

    readonly Forms.NotifyIcon tray=new() {Text="Cally"};
    DateTime refreshed=DateTime.MinValue;
    bool busy, desktop;
    Meeting? next;
    public Widget()
    {
        InitializeComponent();
        settings=Preferences.Load(); Width=Math.Clamp(settings.Width,375,620);
        var area=SystemParameters.WorkArea;
        Left=Math.Clamp(settings.X,area.Left,Math.Max(area.Left,area.Right-Width));
        Top=Math.Clamp(settings.Y,area.Top,Math.Max(area.Top,area.Bottom-170));
        using (var stream = Application.GetResourceStream(new Uri("/Cally;component/Assets/Cally.ico", UriKind.Relative)).Stream)
        using (var icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize))
            tray.Icon = (System.Drawing.Icon)icon.Clone();
        tray.Visible = true;
        tray.MouseClick += (_, e) =>
        {
            if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right)
                OpenMenu(PlacementMode.MousePoint, true);
        };
        PreviewMouseRightButtonUp += (_, e) => { e.Handled=true; OpenMenu(PlacementMode.MousePoint); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift))
            { e.Handled=true; OpenMenu(PlacementMode.Bottom); }
        };
        updateTimer.Tick+=async (_,_)=>await CheckUpdates(false);
        Loaded+=async (_,_)=> {desktop=Desktop.Attach(this); Draw(); updateTimer.Start(); _=CheckUpdates(false); await Refresh(); clock.Start();};
        clock.Tick+=async (_,_)=> { if(DateTime.Now-refreshed>=TimeSpan.FromMinutes(2) || refreshed.Date!=DateTime.Today) await Refresh(); else Draw(); };
        Closed+=(_,_)=>{closed=true; updateTimer.Stop(); clock.Stop(); var icon=tray.Icon; tray.Dispose(); icon?.Dispose();};
    }
    async Task CheckUpdates(bool interactive)
    {
        try
        {
            var request=updateRequest ??= ReleaseUpdates.Fetch();
            Version? latest;
            try { latest=await request; }
            finally { if(updateRequest==request) updateRequest=null; }
            if(closed) return;
            availableUpdate=latest!=null && latest>ReleaseUpdates.Installed ? latest : null;
            UpdateLink.Visibility=availableUpdate==null ? Visibility.Collapsed : Visibility.Visible;
            UpdateLink.Content=$"Cally {availableUpdate} available  ↗";
            tray.Text=availableUpdate==null ? "Cally" : $"Cally {availableUpdate} available";
            if(!interactive) return;
            if(availableUpdate!=null)
            {
                if(CallyDialog.Prompt("Update available",$"Installed: {ReleaseUpdates.Installed}\nLatest: {availableUpdate}", "View release"))
                    WebPage.Open(ReleaseUpdates.Page);
            }
            else if(latest==null) CallyDialog.Prompt("No public release found","GitHub did not return a published release for Cally.");
            else CallyDialog.Prompt("Cally is up to date",$"Installed version: {ReleaseUpdates.Installed}");
        }
        catch(Exception error) when(error is System.Net.Http.HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or FormatException)
        {
            if(interactive && !closed && CallyDialog.Prompt("Update check unavailable","Cally could not verify the latest version. Try again later or open GitHub Releases.", "Open releases"))
                WebPage.Open(ReleaseUpdates.Page);
        }
    }

    void OpenRelease(object sender,RoutedEventArgs e)=>WebPage.Open(ReleaseUpdates.Page);

    async void RefreshClicked(object sender, RoutedEventArgs e) => await Refresh();

    async Task Refresh()
    {
        if(busy) return;
        busy=true;
        RefreshButton.IsEnabled=false;
        RefreshButton.Opacity=0.5;
        try {
            do {
                var feeds=settings.Feeds.ToArray();
                await Task.WhenAll(feeds.Select(async f=>{
                    try { var meetings=await Calendars.Read(f); if(settings.Feeds.Contains(f)){events[f.Id]=meetings; errors.Remove(f.Id);} }
                    catch (Exception error) { if(settings.Feeds.Contains(f)) errors[f.Id]=$"{f.Name} — {Calendars.Failure(f,error)}"; }
                }));
                refreshed=DateTime.Now;
                if(settings.Feeds.SequenceEqual(feeds)) break;
            } while(true);
        } finally {busy=false;RefreshButton.IsEnabled=settings.Feeds.Count>0;RefreshButton.Opacity=RefreshButton.IsEnabled?1:0.5;Draw();}
    }
    static Brush Color(string value)=> (Brush)new BrushConverter().ConvertFromString(value)!;
    static TextBlock Label(string value,double size,Brush ink)=>new(){Text=value,FontSize=size,Foreground=ink,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
    void Draw()
    {
        var now=DateTime.Now; var selected=settings.Feeds.Where(f=>settings.Filter==null || settings.Filter==f.Id).ToList();
        var agenda=selected.SelectMany(f=>events.GetValueOrDefault(f.Id)??[]).Where(m=>m.Start.Date==now.Date || m.Start<now && m.End>now).OrderBy(m=>m.Start).ToList();
        next=agenda.FirstOrDefault(m=>!m.AllDay && m.End>now) ?? agenda.FirstOrDefault(m=>!m.AllDay && m.Start>=now);
        int minutes=next==null?0:Math.Max(0,(int)Math.Ceiling((next.Start-now).TotalMinutes));
        Count.Text=next==null?"—":minutes==0?"Now":minutes>99?$"{minutes/60}":minutes.ToString();
        Unit.Text=next==null?"Cally":minutes==0?"live":minutes>99?"hr":"min";
        double angle=2*Math.PI*Math.Clamp(minutes/60d,0,.999);
        var path=new StreamGeometry(); using(var pen=path.Open()){pen.BeginFigure(new Point(62,10),false,false);pen.ArcTo(new Point(62+52*Math.Sin(angle),62-52*Math.Cos(angle)),new Size(52,52),0,angle>Math.PI,SweepDirection.Clockwise,true,false);} Arc.Data=path;
        bool unavailable = selected.Count > 0 && selected.All(f => errors.ContainsKey(f.Id) && !events.ContainsKey(f.Id));
        Heading.Text=unavailable?"Calendar unavailable":next==null?(settings.Feeds.Count==0?"Welcome to Cally":"No upcoming meetings"):TitleFor(next);
        Sub.Text=unavailable?"Refresh to try again":next==null?(settings.Feeds.Count==0?"Connect your first calendar":"Your day at a glance"):$"{next.Start:HH:mm}  ·  {(settings.Private?"Busy":next.Place.Length>0?next.Place:next.Account)}";
        Heading.ToolTip=Heading.Text; Sub.ToolTip=Sub.Text;
        HeroJoin.Visibility=next?.Join!=null && !settings.Private?Visibility.Visible:Visibility.Collapsed;
        HeroJoin.ToolTip=next==null?null:"Join "+TitleFor(next);
        Today.Content=settings.Expanded?"Today  ▴":"Today  ▾"; Scope.Content=(settings.Filter==null?"All accounts":selected.FirstOrDefault()?.Name??"All accounts")+"  ▾";
        Panel.Visibility=settings.Expanded?Visibility.Visible:Visibility.Collapsed;
        var focusedRow=(Keyboard.FocusedElement as Button)?.Tag;
        Rows.Children.Clear();
        foreach(var m in agenda) AddRow(m);
        if(focusedRow!=null) Rows.Children.OfType<Button>().FirstOrDefault(row=>Equals(row.Tag,focusedRow))?.Focus();
        if(settings.Feeds.Count==0){var connect=new Button {Content="Connect a calendar",Style=(Style)FindResource("Action")};connect.Click+=(_,_)=>Connect();Rows.Children.Add(connect);}
        else if(agenda.Count==0 && !selected.Any(f=>errors.ContainsKey(f.Id))) Rows.Children.Add(Label("No events to show today",14,Brushes.White));
        Status.Text=string.Join(" · ",selected.Where(f=>errors.ContainsKey(f.Id)).Select(f=>errors[f.Id]));
        if(!desktop) Status.Text+="  Desktop layer unavailable";
        Status.Visibility=string.IsNullOrWhiteSpace(Status.Text)?Visibility.Collapsed:Visibility.Visible;
    }
    string TitleFor(Meeting m)=>settings.Private?"Busy":m.Title;
    void AddRow(Meeting m)
    {
        var grid=new Grid {Margin=new Thickness(9,8,6,8)};
        foreach(var width in new[]{new GridLength(15),new GridLength(55),new GridLength(1,GridUnitType.Star),GridLength.Auto}) grid.ColumnDefinitions.Add(new(){Width=width});
        void Add(UIElement element,int col){Grid.SetColumn(element,col);grid.Children.Add(element);}
        var feed=settings.Feeds.First(f=>f.Id==m.FeedId);
        Add(Label("●",13,Color(feed.Provider switch {"Google"=>"#FFAE6D","iCloud"=>"#61D6B2",_=>"#828AFF"})),0);
        Add(Label(m.AllDay?"All day":m.Start.ToString("HH:mm"),14,Brushes.White),1);
        var info=new StackPanel();info.Children.Add(Label(TitleFor(m),15,Brushes.White));
        if(!settings.Private && m.Place.Length>0)info.Children.Add(Label(m.Place,13,Color("#BBC9ED")));Add(info,2);
        var detail=$"{TitleFor(m)} · {m.Start:t}–{m.End:t}"+(settings.Private?"":$" · {m.Place} · {m.Account}");
        if(!settings.Private)Add(Label(m.Account.Length>12?m.Account[..12]+"…":m.Account,12,Color("#B2D7FF")),3);
        var arrow=Label("›",18,Color("#CAD5F9"));
        arrow.Opacity=0;
        grid.ColumnDefinitions.Add(new(){Width=new GridLength(14)});
        Add(arrow,4);
        var card=new Button {Style=(Style)FindResource("Action"),Content=grid,Padding=new Thickness(0),
            HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=m==next?Color("#253354"):Brushes.Transparent,
            ToolTip="View meeting details", Tag=(m.FeedId,m.Start,m.Title)};
        void Highlight()=>arrow.Opacity=card.IsMouseOver || card.IsKeyboardFocused ? 1 : 0;
        card.MouseEnter+=(_,_)=>Highlight(); card.MouseLeave+=(_,_)=>Highlight();
        card.GotKeyboardFocus+=(_,_)=>Highlight(); card.LostKeyboardFocus+=(_,_)=>Highlight();
        System.Windows.Automation.AutomationProperties.SetName(card,detail);
        card.Click+=(_,_)=>{
            bool canJoin=m.Join!=null && !settings.Private;
            var when=m.AllDay?"All day":$"{m.Start:t} – {m.End:t}";
            var lines=settings.Private?when:string.Join(Environment.NewLine,new[]{when,m.Place,m.Account}.Where(value=>!string.IsNullOrWhiteSpace(value)));
            if(CallyDialog.Prompt(TitleFor(m),lines,canJoin?"Join meeting":"Close") && canJoin)Launch(m);
        };
        Rows.Children.Add(card);
    }
    void Launch(Meeting m){if(!settings.Private && m.Join!=null)try{Process.Start(new ProcessStartInfo(m.Join){UseShellExecute=true});}catch{CallyDialog.Prompt("Meeting link unavailable","Windows could not open the meeting link.");}}
    void Join(object sender,RoutedEventArgs e){if(next!=null)Launch(next);}
    void Toggle(object sender,RoutedEventArgs e){settings.Expanded=!settings.Expanded;Save();}
    void Privacy(){settings.Private=!settings.Private;Save();}
    void Save(){settings.Save();Draw();}
    async void Connect(){var dialog=new FeedDialog();if(dialog.ShowDialog()!=true || dialog.Result==null)return;settings.Feeds.Add(dialog.Result);settings.Filter=null;Save();await Refresh();}
    void StartMove(object sender,MouseButtonEventArgs e)
    {
        if(e.ButtonState!=MouseButtonState.Pressed || e.Handled) return;
        var header=(Grid)sender;
        var offset=PointToScreen(e.GetPosition(this))-PointToScreen(new Point());
        if(!header.CaptureMouse()) return;
        void Moving(object target,MouseEventArgs args)
        {
            Desktop.Move(this,PointToScreen(args.GetPosition(this))-offset);
        }
        void Released(object target,MouseEventArgs args)
        {
            header.MouseMove-=Moving;
            header.LostMouseCapture-=Released;
            header.MouseLeftButtonUp-=End;
            var position=PointToScreen(new Point());
            var dpi=VisualTreeHelper.GetDpi(this);
            settings.X=position.X/dpi.DpiScaleX;
            settings.Y=position.Y/dpi.DpiScaleY;
            settings.Save();
        }
        void End(object target,MouseButtonEventArgs args)=>header.ReleaseMouseCapture();
        header.MouseMove+=Moving;
        header.MouseLeftButtonUp+=End;
        header.LostMouseCapture+=Released;
    }
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(nint handle);
    ContextMenu? activeMenu;
    void Menu(object sender,RoutedEventArgs e) => OpenMenu(PlacementMode.Bottom);

    void OpenMenu(PlacementMode placement, bool fromTray = false)
    {
        if (activeMenu != null) activeMenu.IsOpen = false;
        var menu = new ContextMenu { PlacementTarget=Scope, Placement=placement };
        if (!SystemParameters.HighContrast) menu.Style=(Style)FindResource("CallyMenu");
        var rowStyle=SystemParameters.HighContrast ? null : (Style)FindResource("CallyRow");
        MenuItem Row(string label, string glyph)
        {
            var entry=new MenuItem { Header=label, Icon=new TextBlock {
                Text=glyph, FontFamily=new FontFamily("Segoe MDL2 Assets"), FontSize=14 } };
            if(rowStyle!=null) entry.Style=rowStyle;
            return entry;
        }
        MenuItem Item(string label, Action action, string glyph="\uE737", bool? check=null)
        {
            var item=Row(label,glyph);
            if(check.HasValue) { item.IsCheckable=true; item.IsChecked=check.Value; }
            item.Click+=(_,_)=>action();
            menu.Items.Add(item);
            return item;
        }
        if(fromTray) Item("Focus widget",()=>{Activate();Scope.Focus();},"\uE8A7");
        var accounts=Row("Calendar view","\uE787");
        menu.Items.Add(accounts);
        void Filter(string label, Guid? id)
        {
            var choice=Row(label,"\uE8EF");
            choice.IsCheckable=true; choice.IsChecked=settings.Filter==id;
            choice.Click+=(_,_)=>{settings.Filter=id;Save();}; accounts.Items.Add(choice);
        }
        Filter("All accounts",null);
        foreach(var f in settings.Feeds) Filter(f.Provider+" · "+f.Name,f.Id);
        Item("Connect calendar…",Connect,"\uE710");
        if(settings.Feeds.FirstOrDefault(f=>f.Id==settings.Filter) is {} selected)
            Item("Edit calendar link…",async ()=>{
                var dialog=new FeedDialog(selected);
                if(dialog.ShowDialog()!=true || dialog.Result==null) return;
                int index=settings.Feeds.IndexOf(selected);
                if(index<0) return;
                settings.Feeds[index]=dialog.Result;
                events.Remove(selected.Id); errors.Remove(selected.Id);
                Save(); await Refresh();
            });
        var remove = Row("Remove account…","\uE74D"); remove.IsEnabled=settings.Feeds.Count>0;
        foreach (var feed in settings.Feeds)
        {
            var choice = Row(feed.Provider+" · "+feed.Name,"\uE8EF");
            choice.Click += (_, _) =>
            {
                if (!CallyDialog.Prompt("Remove calendar?", $"Remove {feed.Name} ({feed.Provider}) from this PC? Your events stay with the calendar provider.", "Remove calendar", destructive:true)) return;
                settings.Feeds.Remove(feed);
                events.Remove(feed.Id);
                errors.Remove(feed.Id);
                if (settings.Filter == feed.Id) settings.Filter = null;
                Save();
            };
            remove.Items.Add(choice);
        }
        menu.Items.Add(remove);
        Item("Refresh calendars",async ()=>await Refresh(),"\uE72C").IsEnabled=!busy && settings.Feeds.Count>0;
        menu.Items.Add(new Separator());
        Item("Hide meeting details",Privacy,"\uE72E",settings.Private);
        try
        {
            bool enabled=Startup.Enabled;
            Item("Launch at Windows sign-in",()=>{
                try { Startup.Set(!enabled); }
                catch(Exception) { CallyDialog.Prompt("Startup setting unavailable","Windows could not update the sign-in setting."); }
            },"\uE7E8",enabled);
        }
        catch (System.Security.SecurityException) { Item("Startup setting unavailable",()=>{}).IsEnabled=false; }
        var size=Row("Widget width","\uE8B9");
        foreach(var (label,delta) in new[]{("Increase width",30),("Decrease width",-30)})
        {
            var option=Row(label,delta>0?"\uE710":"\uE738");
            option.IsEnabled=delta>0?Width<620:Width>375;
            option.Click+=(_,_)=>{Width=settings.Width=Math.Clamp(Width+delta,375,620);Save();};
            size.Items.Add(option);
        }
        menu.Items.Add(size);
        menu.Items.Add(new Separator());
        if(availableUpdate!=null)
            Item($"View update · {availableUpdate}",()=>WebPage.Open(ReleaseUpdates.Page),"\uE896");
        Item("Check for updates…",async ()=>await CheckUpdates(true),"\uE895");
        Item("Exit Cally",Close,"\uE711");
        activeMenu=menu;
        menu.Closed+=(_,_)=>{if(activeMenu==menu) activeMenu=null;};
        menu.Opened+=(_,_)=>{
            if(fromTray && PresentationSource.FromVisual(menu) is HwndSource popup)
                SetForegroundWindow(popup.Handle);
        };
        menu.IsOpen=true;
    }
}





