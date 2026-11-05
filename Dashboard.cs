using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace DesktopTodo {
 public partial class Dashboard:Window {
  readonly App app;readonly Grid chrome;readonly ContentControl view;readonly TextBlock status;
  readonly Button matrixButton,calendarButton,recordsButton,compactButton,completedButton,lockButton;
  readonly Button sessionButton;readonly WrapPanel filterControls;readonly ComboBox categoryChoice,colorChoice;bool updating,compact,completedOnly;string screen="matrix",category="",color="";
  string gesture="";Point taskStart;Native.PointNative start,origin;double startWidth,startHeight;bool rendering;
  DateTime month=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1),day=DateTime.Today;
  public Dashboard(App owner){app=owner;Title="Desktop Todo";FontFamily=Design.Font;Background=Design.Paper;ShowInTaskbar=false;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;MinWidth=440;MinHeight=380;MaxWidth=1400;MaxHeight=1100;Width=Math.Max(MinWidth,app.Data.Width);Height=Math.Max(MinHeight,app.Data.Height);
   chrome=new Grid{Background=Brushes.Transparent};var body=new DockPanel{Margin=new Thickness(14)};chrome.Children.Add(new Border{BorderBrush=UI.Color("#393840"),BorderThickness=new Thickness(1),Child=body});Content=chrome;
   var header=new DockPanel{Background=Brushes.Transparent,Margin=new Thickness(0,0,0,9)};var actions=UI.Row();lockButton=Design.Button("",()=>{app.Data.Locked=!app.Data.Locked;app.SaveLayout();Render();});actions.Children.Add(lockButton);actions.Children.Add(Design.Button("크기 ▾",ShowSizes));viewOptionsButton=Design.Button("보기 ≡",ShowViewOptions);actions.Children.Add(viewOptionsButton);actions.Children.Add(Design.Button("···",()=>app.ShowMenu()));DockPanel.SetDock(actions,Dock.Right);header.Children.Add(actions);header.Children.Add(new Border{Background=Brushes.Transparent,MinWidth=30,ToolTip="드래그하여 이동"});DockPanel.SetDock(header,Dock.Top);body.Children.Add(header);
   header.PreviewMouseLeftButtonDown+=(o,e)=>{if(!app.Data.Locked&&!IsControl(e.OriginalSource as DependencyObject))StartGesture("move",e);};
   var nav=new WrapPanel{Margin=new Thickness(0,0,0,12),Background=UI.Color("#202329")};matrixButton=Design.Button("매트릭스",()=>{screen="matrix";Render();});calendarButton=Design.Button("캘린더",()=>{screen="calendar";Render();});recordsButton=Design.Button("기록",()=>{screen="records";Render();});compactButton=Design.Button("간결하게",()=>{compact=!compact;Render();});completedButton=Design.Button("완료 보기",()=>{completedOnly=!completedOnly;Render();});foreach(var b in new[]{matrixButton,calendarButton,recordsButton}){b.FontSize=11;b.Padding=new Thickness(10,7,10,7);b.Margin=new Thickness(0,0,5,3);nav.Children.Add(b);}sessionButton=Design.Button("세션",ShowSessions);nav.Children.Add(sessionButton);nav.Margin=new Thickness(0);nav.Background=Brushes.Transparent;var navFrame=new Border{Child=nav,Background=UI.Color("#202329"),CornerRadius=new CornerRadius(10),Padding=new Thickness(2),Margin=new Thickness(0,0,0,8),HorizontalAlignment=HorizontalAlignment.Left};DockPanel.SetDock(navFrame,Dock.Top);body.Children.Add(navFrame);
   var filters=new WrapPanel{Margin=new Thickness(0,0,0,9)};filterControls=filters;categoryChoice=new ComboBox{Width=128,Margin=new Thickness(0,0,8,0)};colorChoice=new ComboBox{Width=150,ItemsSource=new[]{"할 일 색상: 모두"}.Concat(TaskActions.ColorNames).ToList(),SelectedIndex=0,ToolTip="상세 편집에서 지정한 할 일 표시 색상으로 필터링합니다."};categoryChoice.SelectionChanged+=(o,e)=>{if(updating)return;category=categoryChoice.SelectedIndex<=0?"":categoryChoice.SelectedItem.ToString();Render();};colorChoice.SelectionChanged+=(o,e)=>{color=colorChoice.SelectedIndex<=0?"":TaskActions.ColorKeys[colorChoice.SelectedIndex-1];Render();};filters.Children.Add(categoryChoice);subcategoryChoice=new ComboBox{Width=128,Margin=new Thickness(0,0,8,0),ToolTip="선택한 카테고리에 속하는 세부 분류로 할 일을 필터링합니다."};subcategoryChoice.SelectionChanged+=(o,e)=>{if(updating)return;subcategory=subcategoryChoice.SelectedIndex<=0?"":subcategoryChoice.SelectedItem.ToString();Render();};filters.Children.Add(subcategoryChoice);filters.Children.Add(Design.Button("카테고리",ShowCategories));DockPanel.SetDock(filters,Dock.Top);body.Children.Add(filters);
   status=Design.Text("",10,Design.Muted);status.Margin=new Thickness(0,7,0,0);DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);view=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};body.Children.Add(view);
   chrome.PreviewMouseLeftButtonDown+=(o,e)=>{if(app.Data.Locked||inlineEditor!=null)return;var g=HitGesture(e.GetPosition(chrome),ActualWidth,ActualHeight);if(g!="")StartGesture(g,e);};
   chrome.PreviewMouseMove+=(o,e)=>{if(gesture!=""){Native.PointNative p;Native.GetCursorPos(out p);ApplyGesture(p.X-start.X,p.Y-start.Y);e.Handled=true;return;}var g=app.Data.Locked?"":HitGesture(e.GetPosition(chrome),ActualWidth,ActualHeight);chrome.Cursor=g=="move"?Cursors.SizeAll:g=="nw"||g=="se"?Cursors.SizeNWSE:g=="ne"||g=="sw"?Cursors.SizeNESW:null;};
   chrome.PreviewMouseLeftButtonUp+=(o,e)=>{if(gesture=="")return;gesture="";chrome.ReleaseMouseCapture();app.SaveLayout();e.Handled=true;};chrome.LostMouseCapture+=(o,e)=>{if(gesture!=""){gesture="";app.SaveLayout();}};
   SetupUsage();
   Render();
  }
  static bool IsControl(DependencyObject d){while(d!=null){if(d is ButtonBase||d is ComboBox)return true;d=VisualTreeHelper.GetParent(d);}return false;}
  static string HitGesture(Point p,double w,double h){bool left=p.X<10,right=p.X>w-10,top=p.Y<10,bottom=p.Y>h-10;if(!(left||right||top||bottom))return "";if(p.X<22&&p.Y<22)return "nw";if(p.X>w-22&&p.Y<22)return "ne";if(p.X<22&&p.Y>h-22)return "sw";if(p.X>w-22&&p.Y>h-22)return "se";return "move";}
  void StartGesture(string kind,MouseButtonEventArgs e){gesture=kind;Native.GetCursorPos(out start);origin=Native.Position(this);startWidth=Width;startHeight=Height;chrome.CaptureMouse();e.Handled=true;}
  void ApplyGesture(double dx,double dy){if(app.Data.Locked)return;if(gesture=="move"){Native.Move(this,origin.X+(int)dx,origin.Y+(int)dy);return;}var source=PresentationSource.FromVisual(this);double scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;bool left=gesture=="nw"||gesture=="sw",top=gesture=="nw"||gesture=="ne";Width=Math.Max(MinWidth,Math.Min(MaxWidth,startWidth+(left?-dx:dx)/scale));Height=Math.Max(MinHeight,Math.Min(MaxHeight,startHeight+(top?-dy:dy)/scale));Native.Move(this,origin.X+(left?(int)((startWidth-Width)*scale):0),origin.Y+(top?(int)((startHeight-Height)*scale):0));}
  void SetSize(double w,double h){if(app.Data.Locked)return;Width=w;Height=h;app.SaveLayout();}
  void ShowSizes(){var menu=new ContextMenu();foreach(var size in new[]{new[]{480.0,420.0},new[]{650.0,540.0},new[]{850.0,680.0}}){var value=size;var item=new MenuItem{Header=string.Format("{0} × {1}",value[0],value[1]),IsEnabled=!app.Data.Locked};item.Click+=(o,e)=>SetSize(value[0],value[1]);menu.Items.Add(item);}if(app.Data.Locked)menu.Items.Add(new MenuItem{Header="배치 잠금을 먼저 해제하세요",IsEnabled=false});menu.IsOpen=true;}
  void Active(Button b,bool active){b.Background=active?Design.Teal:UI.Color("#292930");b.Foreground=active?Design.Paper:Design.Ink;}
  System.Collections.Generic.List<Todo> Tasks(){return app.Data.Tasks.Where(t=>t.Done==completedOnly&&(category==""||t.Category==category)&&(color==""||t.Color==color)&&(subcategory==""||t.Subcategory==subcategory)).OrderBy(t=>t.Order).ThenBy(t=>t.Date??DateTime.MaxValue).ToList();}
  public void Render(){if(view==null||rendering)return;rendering=true;try{var names=new[]{"전체 카테고리"}.Concat(app.Data.Categories).ToList();var old=categoryChoice.ItemsSource as System.Collections.Generic.List<string>;if(old==null||!old.SequenceEqual(names)){updating=true;if(!app.Data.Categories.Contains(category))category="";categoryChoice.ItemsSource=names;categoryChoice.SelectedIndex=category==""?0:names.IndexOf(category);updating=false;}
   filterControls.Visibility=screen=="sessions"?Visibility.Collapsed:Visibility.Visible;compactButton.Visibility=completedButton.Visibility=screen=="sessions"?Visibility.Collapsed:Visibility.Visible;RefreshSubcategoryFilter();Active(viewOptionsButton,screen!="sessions"&&(compact||completedOnly||color!=""));Active(sessionButton,screen=="sessions");Active(matrixButton,screen=="matrix");Active(calendarButton,screen=="calendar");Active(recordsButton,screen=="records");Active(compactButton,compact);Active(completedButton,completedOnly);Active(lockButton,app.Data.Locked);lockButton.Content=app.Data.Locked?"잠금 해제":"배치 잠금";compactButton.Content=compact?"간결하게 ✓":"간결하게";completedButton.Content=completedOnly?"완료 보기 ✓":"완료 보기";
   status.Text=(completedOnly?"완료된 할 일 ":"미완료 할 일 ")+Tasks().Count+"개"+(compact?" · 간결 보기":" · 상세 보기")+(app.Data.Locked?" · 위치·크기 잠김":" · 테두리로 이동 / 모서리로 크기 조절");if(screen=="sessions")status.Text="구독 사용량 · 1분마다 갱신 · 마지막 확인 시각 표시";view.Content=screen=="sessions"?SessionsView():screen=="calendar"?Calendar():screen=="records"?Statistics():Matrix();ApplyCommandSizes();
  }finally{rendering=false;}}
  public void SetCalendar(bool value){screen=value?"calendar":"matrix";Render();}
  UIElement TaskRow(Todo t){bool shortRow=compact;var row=new DockPanel();var check=new CheckBox{Tag="task-check",IsChecked=t.Done,Margin=new Thickness(0,0,5,0),VerticalAlignment=VerticalAlignment.Center,Cursor=Cursors.Hand,ToolTip=t.Done?"완료 취소":"완료 처리"};System.Windows.Automation.AutomationProperties.SetName(check,(t.Done?"완료 취소: ":"완료 처리: ")+t.Title);check.Click+=(o,e)=>{TaskActions.Complete(app.Data,t,check.IsChecked==true);app.Commit();};DockPanel.SetDock(check,Dock.Left);row.Children.Add(check);var info=new StackPanel();var title=Design.Text(t.Title,shortRow?12:13,t.Done?Design.Muted:Design.Ink);if(t.Done)title.TextDecorations=TextDecorations.Strikethrough;title.FontFamily=Design.BoldFont;title.FontWeight=FontWeights.Bold;info.Children.Add(title);if(!shortRow){var meta=Design.Text(t.Category+(string.IsNullOrEmpty(t.Subcategory)?"":" / "+t.Subcategory)+(t.Date.HasValue?" · "+t.Date.Value.ToString("M/d"):""),10,Design.Muted);meta.Tag="task-meta";info.Children.Add(meta);}var button=Design.Button("",()=>ShowInline(t));button.Tag="task-open";button.Width=double.NaN;button.Height=double.NaN;button.Content=info;button.Background=Brushes.Transparent;button.Padding=new Thickness(0);button.Margin=new Thickness(0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;row.Children.Add(button);var card=Design.Panel(row,"#26262D",shortRow?5:9);card.Background=TaskActions.Background(t);check.BorderBrush=TaskActions.Color(t);check.Foreground=TaskActions.Color(t);card.Tag="task-card";card.BorderBrush=TaskActions.Color(t);card.BorderThickness=new Thickness(4,0,0,0);card.Margin=new Thickness(0,0,0,shortRow?3:6);bool dragFromCard=false;
   card.PreviewMouseLeftButtonDown+=(o,e)=>{dragFromCard=!check.IsMouseOver;taskStart=e.GetPosition(card);};
   card.PreviewMouseMove+=(o,e)=>{
    if(!dragFromCard||e.LeftButton!=MouseButtonState.Pressed)return;
    var p=e.GetPosition(card);
    if(Math.Abs(p.X-taskStart.X)+Math.Abs(p.Y-taskStart.Y)<=12)return;
    dragFromCard=false;
    DragDrop.DoDragDrop(card,new DataObject("DesktopTodo.Task",t.Id),DragDropEffects.Move);
   };
   return card;
  }
  UIElement Matrix(){var grid=new Grid();var descriptions=new System.Collections.Generic.List<TextBlock>();for(int i=0;i<2;i++){grid.RowDefinitions.Add(new RowDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());}var tasks=Tasks();for(int q=0;q<4;q++){int target=q;var pane=new DockPanel();var header=new DockPanel{Margin=new Thickness(0,0,0,compact?5:8)};var add=Design.Button("＋",()=>{ShowInline(null,null,target);});add.Padding=new Thickness(7,3,7,3);DockPanel.SetDock(add,Dock.Right);header.Children.Add(add);var heading=new StackPanel();heading.Children.Add(Design.Text(Design.Names[q]+"  "+tasks.Count(t=>t.Quadrant==q),compact?13:15));if(!compact){var description=Design.Text(Design.Descriptions[q],10,Design.Muted);heading.Children.Add(description);descriptions.Add(description);}header.Children.Add(heading);DockPanel.SetDock(header,Dock.Top);pane.Children.Add(header);var list=new StackPanel();foreach(var t in tasks.Where(t=>t.Quadrant==target))list.Children.Add(TaskRow(t));if(list.Children.Count==0)list.Children.Add(Design.Text(completedOnly?"완료한 일이 없습니다":"할 일이 없습니다",10,Design.Muted));pane.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});var box=Design.Panel(pane,"#202126",compact?8:10);box.BorderBrush=UI.Color("#35343D");box.BorderThickness=new Thickness(1);box.Margin=new Thickness(q%2==0?0:4,q<2?0:4,q%2==0?4:0,q<2?4:0);box.AllowDrop=true;box.DragOver+=(o,e)=>{e.Effects=e.Data.GetDataPresent("DesktopTodo.Task")?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};box.Drop+=(o,e)=>{var task=app.Data.Tasks.FirstOrDefault(t=>t.Id==e.Data.GetData("DesktopTodo.Task") as string);if(task!=null){TaskActions.Move(task,target);app.Commit();}e.Handled=true;};Design.Put(grid,box,q/2,q%2);}
   grid.SizeChanged+=(o,e)=>{
    var visibility=e.NewSize.Height>=295?Visibility.Visible:Visibility.Collapsed;
    foreach(var description in descriptions)description.Visibility=visibility;
   };
   return grid;
  }
  UIElement Calendar(){
   var layout=new Grid();
   layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(3,GridUnitType.Star),MinHeight=98});
   layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star),MinHeight=64});
   layout.ColumnDefinitions.Add(new ColumnDefinition());
   layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});var header=new DockPanel{Margin=new Thickness(0,0,0,5)};var nav=UI.Row();nav.Children.Add(Design.Button("이전 달",()=>{month=month.AddMonths(-1);Render();}));var todayButton=Design.Button(day==DateTime.Today&&month.Year==day.Year&&month.Month==day.Month?"오늘 선택됨":"오늘로 이동",JumpToToday);todayButton.IsEnabled=!(day==DateTime.Today&&month.Year==day.Year&&month.Month==day.Month);todayButton.ToolTip="오늘 날짜가 있는 달로 이동하고, 오늘의 할 일을 아래에 표시합니다.";nav.Children.Add(todayButton);nav.Children.Add(Design.Button("다음 달",()=>{month=month.AddMonths(1);Render();}));DockPanel.SetDock(nav,Dock.Right);header.Children.Add(nav);header.Children.Add(Design.Text(month.ToString("yyyy년 M월"),17));Design.Put(layout,header,0,0);Grid.SetColumnSpan(header,2);var dates=new Grid{Tag="calendar-dates"};dates.RowDefinitions.Add(new RowDefinition{Height=new GridLength(14)});var dateButtons=new System.Collections.Generic.List<Button>();for(int i=0;i<6;i++)dates.RowDefinitions.Add(new RowDefinition());for(int i=0;i<7;i++){dates.ColumnDefinitions.Add(new ColumnDefinition());var label=Design.Text(new[]{"일","월","화","수","목","금","토"}[i],10,Design.Muted);label.HorizontalAlignment=HorizontalAlignment.Center;Design.Put(dates,label,0,i);}var tasks=Tasks();var first=month.AddDays(-(int)month.DayOfWeek);for(int i=0;i<42;i++){DateTime date=first.AddDays(i);int count=tasks.Count(t=>t.Date==date);var text=Design.Text(date.Day+(count>0?" ·":""),10,date==day?Design.Paper:date.Month!=month.Month?UI.Color("#67636E"):Design.Ink);text.TextWrapping=TextWrapping.NoWrap;text.HorizontalAlignment=HorizontalAlignment.Center;var b=new Button{Tag="calendar-day",Content=text,Background=date==day?Design.Teal:date==DateTime.Today?UI.Color("#393144"):Brushes.Transparent,Padding=new Thickness(0),Margin=new Thickness(0),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,ToolTip=date.ToString("yyyy-MM-dd")+" · "+count+"개"};b.Click+=(o,e)=>{day=date;Render();};Design.Put(dates,b,i/7+1,i%7);dateButtons.Add(b);}Design.Put(layout,dates,1,0);
   var agenda=new DockPanel{Tag="calendar-agenda",Margin=new Thickness(0,6,0,0)};var agendaHead=new DockPanel();var add=Design.Button("＋ 추가",()=>ShowInline(null,day));add.Padding=new Thickness(7,2,7,2);DockPanel.SetDock(add,Dock.Right);agendaHead.Children.Add(add);agendaHead.Children.Add(Design.Text("선택한 날짜 · "+day.ToString("M월 d일")+" · "+tasks.Count(t=>t.Date==day)+"개",11,Design.Teal));DockPanel.SetDock(agendaHead,Dock.Top);agenda.Children.Add(agendaHead);var items=new StackPanel();foreach(var t in tasks.Where(t=>t.Date==day))items.Children.Add(TaskRow(t));if(items.Children.Count==0)items.Children.Add(Design.Text(completedOnly?"완료된 일정이 없습니다":"등록된 일정이 없습니다",10,Design.Muted));agenda.Children.Add(new ScrollViewer{Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Design.Put(layout,agenda,2,0);
   layout.SizeChanged+=(o,e)=>{
    bool wide=e.NewSize.Width>=780;
    if(wide!=(Grid.GetColumn(agenda)==1)){
     layout.ColumnDefinitions[0].Width=new GridLength(wide?2:1,GridUnitType.Star);
     layout.ColumnDefinitions[1].Width=wide?new GridLength(1,GridUnitType.Star):new GridLength(0);
     layout.RowDefinitions[2].MinHeight=wide?0:64;
     layout.RowDefinitions[2].Height=wide?new GridLength(0):new GridLength(1,GridUnitType.Star);
     Grid.SetRow(agenda,wide?1:2);Grid.SetColumn(agenda,wide?1:0);
     agenda.Margin=wide?new Thickness(16,0,0,0):new Thickness(0,6,0,0);
    }
    bool dense=compact||dates.ActualHeight<185;
    if((dates.RowDefinitions[0].Height.Value==14)!=dense){
     dates.RowDefinitions[0].Height=new GridLength(dense?14:17);
     foreach(var button in dateButtons){
      ((TextBlock)button.Content).FontSize=dense?10:12;
      button.Margin=new Thickness(dense?0:1);
     }
    }
   };
   return layout;
  }
  UIElement Statistics(){var p=new StackPanel();p.Children.Add(Design.Text("완료 기록",21));p.Children.Add(Design.Text("완료 "+app.Data.Tasks.Count(t=>t.Done)+"개 · 진행 중 "+app.Data.Tasks.Count(t=>!t.Done)+"개",13,Design.Muted));for(int q=0;q<4;q++){var row=Design.Text(Design.Names[q]+"   "+app.Data.Tasks.Count(t=>t.Quadrant==q&&!t.Done),14);row.Margin=new Thickness(0,15,0,0);p.Children.Add(row);}foreach(var t in app.Data.Tasks.Where(t=>t.Done).OrderByDescending(t=>t.CompletedAt).Take(30))p.Children.Add(Design.Text("✓  "+t.Title+"  "+t.CompletedAt,12,Design.Muted));return new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};}
  static System.Collections.Generic.IEnumerable<FrameworkElement> Elements(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);var f=c as FrameworkElement;if(f!=null)yield return f;foreach(var d in Elements(c))yield return d;}}
  internal void VerifyLayout(){app.Data.Locked=false;double before=Width;SetSize(before+42,Height+28);if(Storage.Load().Width!=Width)throw new Exception("Resize persistence failed");lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));before=Width;SetSize(before+20,Height);if(Width!=before||!Storage.Load().Locked)throw new Exception("Locked resize failed");calendarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(screen!="calendar"||calendarButton.Background!=Design.Teal)throw new Exception("Calendar selection failed");recordsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(screen!="records"||recordsButton.Background!=Design.Teal||calendarButton.Background==Design.Teal)throw new Exception("Records selection failed");matrixButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(HitGesture(new Point(2,100),650,540)!="move"||HitGesture(new Point(2,2),650,540)!="nw")throw new Exception("Border gestures failed");}
  internal void VerifyViews(){
   screen="matrix";compact=false;completedOnly=false;Render();UpdateLayout();
   if(!Elements(view).Any(e=>Equals(e.Tag,"task-meta")))throw new Exception("Missing detail rows");
   compactButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();
   if(Elements(view).Any(e=>Equals(e.Tag,"task-meta"))||compactButton.Background!=Design.Teal)throw new Exception("Compact view failed");
   var check=Elements(view).OfType<CheckBox>().First(c=>Equals(c.Tag,"task-check"));
   if(check.InputHitTest(new Point(check.ActualWidth/2,check.ActualHeight/2))==null)throw new Exception("Checkbox center cannot be clicked");
   check.IsChecked=true;
   check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   completedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();
   if(Tasks().Count!=1||!Tasks()[0].Done||completedButton.Background!=Design.Teal||Storage.Load().Tasks.Count(t=>t.Done)!=1)throw new Exception("Completed checkbox did not save");
   check=Elements(view).OfType<CheckBox>().Single(c=>Equals(c.Tag,"task-check"));
   check.IsChecked=false;
   check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   if(Storage.Load().Tasks.Any(t=>t.Done))throw new Exception("Checkbox undo did not save");
   completedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));compactButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   SetCalendar(true);
   var calendar=view.Content;
   foreach(var size in new[]{new[]{440.0,380.0},new[]{900.0,700.0},new[]{650.0,540.0}}){
    Width=size[0];Height=size[1];UpdateLayout();
    var cells=Elements(view).Where(e=>Equals(e.Tag,"calendar-day")).ToList();
    var agenda=Elements(view).Single(e=>Equals(e.Tag,"calendar-agenda"));
    if(!ReferenceEquals(view.Content,calendar)||Grid.GetColumn(agenda)!=(Width>=900?1:0)||cells.Count!=42||cells.Any(c=>c.ActualHeight<14||c.ActualWidth<20))throw new Exception("Calendar resize failed at "+Width+"x"+Height);
    App.Capture(this,"calendar-"+(int)Width+"x"+(int)Height+".png");
   }
   SetCalendar(false);
  }
 }
}
