using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DesktopTodo {
 public static class Design {
  public static Brush Ink=UI.Color("#F5F5F7"),Muted=UI.Color("#B8B7C3"),Teal=UI.Color("#78ABFF"),Paper=UI.Color("#18191D");
  public static FontFamily Font=new FontFamily("AppleSDGothicNeoM00, Segoe UI");
  public static FontFamily BoldFont=new FontFamily("AppleSDGothicNeoB00, Segoe UI");
  public static string[] Names={"지금 하기","계획하기","위임하기","줄이기"};
  public static string[] Descriptions={"중요 · 긴급","중요 · 긴급하지 않음","중요하지 않음 · 긴급","중요하지 않음 · 긴급하지 않음"};
  public static string[] Colors={"#202126","#202126","#202126","#202126"};
  public static TextBlock Text(string s,double size=13,Brush brush=null){var text=UI.Label(s,size,brush??Ink);if(size>=14){text.FontFamily=BoldFont;text.FontWeight=FontWeights.Bold;}return text;}
  public static Button Button(string s,Action action,bool selected=false){var b=UI.Button(s,action);b.Background=selected?Teal:UI.Color("#292930");b.Foreground=selected?Paper:Ink;b.Width=84;b.Height=32;b.Tag="command";b.HorizontalContentAlignment=HorizontalAlignment.Center;b.Padding=new Thickness(4,0,4,0);return b;}
  public static TextBox Input(string text=""){var b=UI.Input(text);b.Background=UI.Color("#26262D");b.Foreground=Ink;b.CaretBrush=Ink;b.BorderBrush=UI.Color("#393840");return b;}
  public static Border Panel(UIElement child,string color,int padding=12){return new Border{Background=UI.Color(color),CornerRadius=new CornerRadius(10),Padding=new Thickness(padding),Child=child};}
  public static void Put(Grid g,UIElement e,int row,int col){Grid.SetRow(e,row);Grid.SetColumn(e,col);g.Children.Add(e);}
 }
 public class App:Application {
  bool vaultErrorReported;
  public State Data;Dashboard dashboard;Forms.NotifyIcon tray;DispatcherTimer timer;IntPtr host;bool exiting;int startupProbeCount;DateTime today=DateTime.Today;
  [STAThread] public static int Main(string[] args){if(args.Contains("--quit"))return AppExit.Signal();Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;if(args.Contains("--vault-test"))return VaultTests.Run();if(args.Contains("--self-test"))return Tests.Run();Storage.IsTest=args.Any(a=>a.EndsWith("-test",StringComparison.Ordinal));if(Storage.IsTest)Storage.Folder=Path.Combine(Path.GetTempPath(),"DesktopTodo-test-"+Guid.NewGuid().ToString("N"));if(InstanceGuard.ForwardToInstalled(args))return 0;if(!Storage.IsTest&&!InstanceGuard.Enter()){if(args.Contains("--sessions"))AppExit.ShowSessions();return 0;}bool created;using(var mutex=new System.Threading.Mutex(true,(Storage.IsTest?"Local\\DesktopTodo.Test."+Path.GetFileName(Storage.Folder):"Local\\DesktopTodo.Widget"),out created)){if(!created){if(args.Contains("--sessions"))return AppExit.ShowSessions();if(!args.Contains("--startup"))MessageBox.Show("이미 실행 중입니다. 알림 영역의 Desktop Todo 메뉴를 확인하세요.");return 0;}var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NativeTheme.xaml"),UriKind.Absolute)});app.DispatcherUnhandledException+=(o,e)=>{MessageBox.Show(e.Exception.Message,"Desktop Todo");e.Handled=true;};app.Startup+=(o,e)=>app.Start();using(var exitHandle=AppExit.Listen(app))app.Run();InstanceGuard.Release();if(Storage.IsTest){try{Directory.Delete(Storage.Folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}}return 0;}}
  void Start(){bool fresh=!File.Exists(Storage.FilePath)&&!File.Exists(Path.Combine(Storage.Folder,"data.json"));try{Data=Storage.Load();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"vault-check.txt"),Storage.Diagnostic());}catch(Exception e){MessageBox.Show("저장 데이터를 읽을 수 없습니다. 기존 파일은 보존합니다.\n"+e.Message);Shutdown();return;}bool migrated=Data.Version<2;Data.Migrate();Data.Theme="dark";if(migrated||fresh){Data.MainX=32;Data.MainY=32;}Save();
   tray=new Forms.NotifyIcon{Icon=System.Drawing.SystemIcons.Application,Visible=true,Text="Desktop Todo · 매트릭스"};var menu=new Forms.ContextMenuStrip();menu.Items.Add("카테고리 · 세부 분류",null,(o,e)=>Dispatcher.Invoke(()=>OpenEditor()));menu.Items.Add("바탕화면 다시 연결",null,(o,e)=>Dispatcher.Invoke(()=>Attach()));menu.Items.Add("위치·크기 초기화",null,(o,e)=>Dispatcher.Invoke(()=>Reset()));var startup=new Forms.ToolStripMenuItem("로그인 시 자동 실행");try{if((fresh||migrated)&&!Storage.IsTest)SetAutoStart(true);}catch(Exception e){MessageBox.Show("자동 실행을 설정하지 못했습니다.\n"+e.Message);}startup.Checked=AutoStart();startup.Click+=(o,e)=>{try{SetAutoStart(!startup.Checked);startup.Checked=AutoStart();}catch(Exception ex){MessageBox.Show(ex.Message);}};menu.Items.Add(startup);menu.Items.Add("로컬 저장소 열기",null,(o,e)=>System.Diagnostics.Process.Start("explorer.exe",Storage.VaultFolder));menu.Items.Add("로그인 프로필 폴더",null,(o,e)=>System.Diagnostics.Process.Start("explorer.exe",Storage.SessionFolder));menu.Items.Add("종료",null,(o,e)=>Dispatcher.Invoke(()=>Quit()));tray.ContextMenuStrip=menu;tray.DoubleClick+=(o,e)=>Dispatcher.Invoke(()=>OpenEditor());
   Attach();timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};timer.Tick+=(o,e)=>{if(!Storage.IsTest&&AppExit.Poll(this))return;if(dashboard==null||!Native.IsWindow(host)||!Native.IsAttached(dashboard,host)||Native.CurrentDesktop()!=host)Attach();CheckReminders();if(dashboard!=null&&!dashboard.IsEditing){try{if(Storage.HasExternalChanges()){Data=Storage.Load();Refresh();}vaultErrorReported=false;}catch(Exception){if(!vaultErrorReported){tray.ShowBalloonTip(6000,"로컬 파일 확인 필요","저장소의 파일 형식을 확인하세요. 기존 파일은 덮어쓰지 않습니다.",Forms.ToolTipIcon.Warning);vaultErrorReported=true;}}}if(today!=DateTime.Today){today=DateTime.Today;Refresh();}};timer.Start();SessionEnding+=(o,e)=>{SaveLayout();Quit();};
   if(Environment.GetCommandLineArgs().Contains("--smoke-test"))Dispatcher.BeginInvoke(new Action(()=>{dashboard.UpdateLayout();Capture(dashboard,"matrix-preview.png");dashboard.SetCalendar(true);dashboard.UpdateLayout();Capture(dashboard,"calendar-preview.png");File.WriteAllText("smoke-results.txt","PASS: single desktop dashboard, matrix/calendar/editor render; desktop host="+host);Quit();}),DispatcherPriority.ApplicationIdle);
   if(Environment.GetCommandLineArgs().Contains("--ui-test"))Dispatcher.BeginInvoke(new Action(()=>{try{
    dashboard.VerifyLayout();dashboard.VerifyCategoryFlow();dashboard.VerifySubcategoryFilter();dashboard.VerifyImmediateClose();dashboard.VerifyInline();dashboard.VerifyViews();dashboard.VerifyUniformButtons();dashboard.UpdateLayout();Capture(dashboard,"matrix-preview.png");dashboard.SetCalendar(true);dashboard.UpdateLayout();Capture(dashboard,"calendar-preview.png");
    SaveLayout();var expected=Storage.Load();dashboard.Close();Data=Storage.Load();Data.Migrate();Attach();var restored=Native.Position(dashboard);if(dashboard.Width!=expected.Width||dashboard.Height!=expected.Height||!Data.Locked||restored.X!=(int)expected.MainX||restored.Y!=(int)expected.MainY)throw new Exception("Window recreation failed");
    File.WriteAllText("ui-test-results.txt","PASS: strict date validation; single-click close; inline categories/subcategories; today navigation; inline editing; active buttons; compact and completed filters; calendar at 440x380, 650x540, 900x700; border/corner gestures; persisted layout restoration; equal command button dimensions.");
   }catch(Exception e){File.WriteAllText("ui-test-results.txt",e.ToString());}finally{exiting=true;timer.Stop();tray.Dispose();Shutdown();}}),DispatcherPriority.ApplicationIdle);
   if(Environment.GetCommandLineArgs().Contains("--session-test"))Dispatcher.BeginInvoke(new Action(async()=>{try{File.WriteAllText("session-test-results.txt",await dashboard.VerifyUsageSessions());}catch(Exception e){File.WriteAllText("session-test-results.txt",e.ToString());}finally{Quit();}}),DispatcherPriority.ApplicationIdle);
   if(Environment.GetCommandLineArgs().Contains("--quota-test"))Dispatcher.BeginInvoke(new Action(()=>{try{File.WriteAllText("quota-test-results.txt",dashboard.VerifyCodexQuotaLayout());}catch(Exception e){File.WriteAllText("quota-test-results.txt",e.ToString());}finally{Quit();}}),DispatcherPriority.ApplicationIdle);
   if(Environment.GetCommandLineArgs().Contains("--sessions"))dashboard.ShowSessions();
   if(Environment.GetCommandLineArgs().Contains("--startup-test")){var probe=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};probe.Tick+=(o,e)=>{probe.Stop();File.WriteAllText("startup-test-results.txt",dashboard!=null&&Native.IsAttached(dashboard,host)?"PASS: survived unavailable desktop during startup; attached after shell became ready; working directory is executable folder.":"FAIL: desktop not attached after startup retries");Quit();};probe.Start();}
  }
  void Attach(){if(exiting)return;host=Environment.GetCommandLineArgs().Contains("--startup-test")&&startupProbeCount++<2?IntPtr.Zero:Native.Desktop();if(host==IntPtr.Zero){if(dashboard!=null)dashboard.Hide();return;}if(dashboard==null){dashboard=new Dashboard(this);dashboard.Closed+=(o,e)=>{dashboard=null;};new WindowInteropHelper(dashboard).EnsureHandle();}if(Native.Attach(dashboard,host,Data.MainX,Data.MainY)){dashboard.Show();Native.Move(dashboard,(int)Data.MainX,(int)Data.MainY);}else{host=IntPtr.Zero;dashboard.Hide();}}
  void Reset(){Data.Locked=false;Data.MainX=32;Data.MainY=32;Data.Width=650;Data.Height=540;dashboard.Width=650;dashboard.Height=540;Save();Attach();Refresh();}
  public void SaveLayout(){if(dashboard==null)return;var p=Native.Position(dashboard);Data.MainX=p.X;Data.MainY=p.Y;Data.Width=dashboard.Width;Data.Height=dashboard.Height;Save();}
  bool Save(){try{Storage.Save(Data);return true;}catch(Exception ex){MessageBox.Show("저장하지 못했습니다. 기존 로컬 파일은 보존됩니다.\n"+ex.Message);return false;}}
  public bool Commit(){bool saved=Save();Refresh();return saved;}
  void Refresh(){if(dashboard!=null)dashboard.Render();}
  public void OpenEditor(Todo task=null,DateTime? date=null,bool create=false){if(dashboard!=null){if(task!=null||create)dashboard.OpenTask(task,date);else dashboard.ShowCategories();}}
  public void OpenSessions(){dashboard.ShowSessions();}
  public void ShowMenu(){tray.ContextMenuStrip.Show(Forms.Cursor.Position);}
  void CheckReminders(){bool changed=false;foreach(var t in Data.Tasks){DateTime alarm;if(!t.Done&&!string.IsNullOrEmpty(t.Reminder)&&t.Reminded!=t.Reminder&&DateTime.TryParse(t.Reminder,out alarm)&&alarm<=DateTime.Now){tray.ShowBalloonTip(6000,"결 · "+t.Title,t.Category+" · 할 일을 확인해 주세요.",Forms.ToolTipIcon.Info);t.Reminded=t.Reminder;changed=true;}}if(changed)Save();}
  public void RequestExit(){Quit();}
  void Quit(){if(dashboard!=null&&!dashboard.PrepareClose())return;exiting=true;SaveLayout();if(timer!=null)timer.Stop();tray.Dispose();Shutdown();}
  static bool AutoStart(){return StartupManager.Enabled();}
  static void SetAutoStart(bool value){StartupManager.SetEnabled(value);}
  internal static void Capture(Window w,string path){var b=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(b));using(var f=File.Create(path))encoder.Save(f);}
 }
 public static class Tests {
  public static int Run(){string folder=Path.Combine(Path.GetTempPath(),"DesktopTodo-test-"+Guid.NewGuid().ToString("N"));Storage.Folder=folder;try{
   var old=new State{Version=0,Width=0,Height=0};old.Tasks.Add(new Todo{Title="기존 할 일",Priority=2,Due=DateTime.Today.ToString("yyyy-MM-dd")});old.Migrate();if(old.Tasks[0].Quadrant!=0||old.Width!=650)throw new Exception("Legacy migration");
   for(int q=0;q<4;q++){var t=new Todo{Important=q<2,Urgent=q%2==0};if(t.Quadrant!=q)throw new Exception("Matrix classification");}
   var recurring=new State();var daily=new Todo{Title="매일 할 일",Due=DateTime.Today.ToString("yyyy-MM-dd"),Repeat="daily",Color="rose",Reminder=DateTime.Today.AddHours(9).ToString("yyyy-MM-ddTHH:mm")};recurring.Tasks.Add(daily);TaskActions.Complete(recurring,daily,true);if(recurring.Tasks.Count!=2||recurring.Tasks[1].Date!=DateTime.Today.AddDays(1)||recurring.Tasks[1].Color!="rose"||string.IsNullOrEmpty(recurring.Tasks[1].Reminder))throw new Exception("Repeat successor");TaskActions.Complete(recurring,daily,false);TaskActions.Complete(recurring,daily,true);if(recurring.Tasks.Count!=2)throw new Exception("Duplicate recurrence");TaskActions.Move(daily,3);if(daily.Quadrant!=3)throw new Exception("Move classification");
   old.Width=711;old.Height=523;old.MainX=123;old.MainY=456;old.Locked=true;Storage.Save(old);var loaded=Storage.Load();loaded.Migrate();if(loaded.Width!=711||loaded.Height!=523||loaded.MainX!=123||loaded.MainY!=456||!loaded.Locked||loaded.Tasks[0].Title!="기존 할 일"||!loaded.Tasks[0].Important||!loaded.Tasks[0].Urgent)throw new Exception("Persistence");loaded.Tasks[0].Done=true;Storage.Save(loaded);if(!Storage.Load().Tasks[0].Done||!File.Exists(Storage.FilePath+".bak"))throw new Exception("Backup");
   File.WriteAllText("test-results.txt","PASS: legacy migration; four quadrants and moving; recurring successor/reminder/color; no duplicate recurrence; persistence, layout lock, backup.");return 0;
  }catch(Exception e){File.WriteAllText("test-results.txt",e.ToString());return 1;}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}}
 }
}
