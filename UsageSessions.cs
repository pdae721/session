using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace DesktopTodo {
 public class UsageMetric {
  public string Label,Text;public double? Used;public DateTimeOffset? Reset;
  public string Remaining {get{return Used.HasValue?Math.Max(0,100-Used.Value).ToString("0.#")+"% 남음":"";}}
  public string ResetText {get{if(!Reset.HasValue)return "초기화 시간 미제공";var d=Reset.Value-DateTimeOffset.Now;if(d.TotalSeconds<=0)return "초기화 시각 지남 · 갱신 필요";return (d.TotalDays>=1?(int)d.TotalDays+"일 ":"")+(d.TotalHours>=1?d.Hours+"시간 ":"")+d.Minutes+"분 후 초기화";}}
 }
 public class UsageSnapshot {
  public string Provider,Status="로그인 후 확인",Detail="",Organization="";public DateTimeOffset? Updated;
  public List<UsageMetric> Metrics=new List<UsageMetric>();public bool LoginRequired;
 }
 public static partial class UsageParser {
  static Dictionary<string,object> Obj(object o){return o as Dictionary<string,object>;}
  public static UsageSnapshot Claude(string json){var result=new UsageSnapshot{Provider="Claude"};var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);var keys=new[]{"five_hour","seven_day","seven_day_sonnet","seven_day_opus","seven_day_oauth_apps","extra_usage"};var labels=new[]{"현재 세션 · 5시간","전체 모델 · 7일","Sonnet · 7일","Opus · 7일","앱 · 7일","추가 사용량"};for(int i=0;i<keys.Length;i++){object raw;if(!data.TryGetValue(keys[i],out raw))continue;var entry=Obj(raw);if(entry==null)continue;object u;double percent;if(!entry.TryGetValue("utilization",out u)||u==null||!double.TryParse(Convert.ToString(u,System.Globalization.CultureInfo.InvariantCulture),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out percent)||percent<0||percent>100)continue;DateTimeOffset reset;object resetRaw;DateTimeOffset? resetAt=entry.TryGetValue("resets_at",out resetRaw)&&DateTimeOffset.TryParse(Convert.ToString(resetRaw),out reset)?reset:(DateTimeOffset?)null;result.Metrics.Add(new UsageMetric{Label=labels[i],Used=percent,Reset=resetAt});}result.Updated=DateTimeOffset.Now;result.Status=result.Metrics.Count>0?"연결됨":"사용량 정보 미제공";result.Detail=result.Metrics.Count>0?"Claude 계정의 실제 사용률 · 1분마다 갱신":"현재 계정 응답에 표시할 사용률이 없습니다.";return result;}
  public static void Verify(){var s=Claude("{\"five_hour\":{\"utilization\":32,\"resets_at\":\"2099-01-01T00:00:00Z\"},\"seven_day\":{\"utilization\":28},\"seven_day_sonnet\":null}");if(s.Metrics.Count!=2||s.Metrics[0].Remaining!="68% 남음")throw new Exception("Usage percentage parsing");if(Claude("{}").Metrics.Count!=0||Claude("{\"five_hour\":{\"utilization\":null}}").Metrics.Count!=0)throw new Exception("Missing usage must not become zero");if(Claude("{\"five_hour\":{\"utilization\":0}}").Metrics[0].Used!=0)throw new Exception("Real zero usage lost");}
 }
 public class UsageConnection:IDisposable {
  public readonly string Provider;public readonly WebView2 Browser;public UsageSnapshot Snapshot;public Action Changed;bool busy,disposed,documentReady,refreshQueued;ulong documentId;Task initialization;readonly string profile;readonly JavaScriptSerializer serializer=new JavaScriptSerializer();readonly Dictionary<string,TaskCompletionSource<Dictionary<string,object>>> pending=new Dictionary<string,TaskCompletionSource<Dictionary<string,object>>>();
  async Task<Dictionary<string,object>> Query(string script){var id=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<Dictionary<string,object>>();pending[id]=completion;try{await Browser.ExecuteScriptAsync("(async()=>{const payload=await ("+script+");window.chrome.webview.postMessage({kind:'desktop-usage',id:"+serializer.Serialize(id)+",payload});})()");if(await Task.WhenAny(completion.Task,Task.Delay(18000))!=completion.Task)throw new TimeoutException();return await completion.Task;}finally{pending.Remove(id);}}
  void Receive(object sender,CoreWebView2WebMessageReceivedEventArgs e){Uri uri;if(!Uri.TryCreate(e.Source,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!=(Provider=="Claude"?"claude.ai":"chatgpt.com"))return;try{var data=serializer.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);if(!data.ContainsKey("kind")||Convert.ToString(data["kind"])!="desktop-usage")return;TaskCompletionSource<Dictionary<string,object>> completion;if(pending.TryGetValue(Convert.ToString(data["id"]),out completion))completion.TrySetResult((Dictionary<string,object>)data["payload"]);}catch{}}
  public UsageConnection(string provider){Provider=provider;profile=provider=="Claude"?"claude":"chatgpt";Snapshot=new UsageSnapshot{Provider=provider};Browser=new WebView2{DefaultBackgroundColor=System.Drawing.Color.FromArgb(24,25,29)};}
  string UsageUrl {get{return Provider=="Claude"?"https://claude.ai/settings/usage":"https://chatgpt.com/codex/settings/usage";}}
  public Task Initialize(){
   if(disposed)return Task.FromResult(0);
   if(initialization==null||(initialization.IsCompleted&&Browser.CoreWebView2==null))initialization=InitializeCore();
   return initialization;
  }
  async Task InitializeCore(){
   SetStatus("연결 확인 중","저장된 로그인으로 사용량을 확인하고 있습니다.");
   try{
    var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Storage.SessionFolder,profile));
    if(disposed)return;
    var options=env.CreateCoreWebView2ControllerOptions();
    options.ProfileName="Default";options.IsInPrivateModeEnabled=false;
    await Browser.EnsureCoreWebView2Async(env,options);
    if(disposed)return;
    var core=Browser.CoreWebView2;
    core.WebMessageReceived+=Receive;
    core.Settings.AreDevToolsEnabled=false;
    core.Settings.IsStatusBarEnabled=false;
    core.NavigationStarting+=(o,e)=>{
     if(!e.Uri.StartsWith("https://")&&e.Uri!="about:blank"){e.Cancel=true;return;}
     documentReady=false;documentId=e.NavigationId;
     foreach(var request in pending.Values.ToArray())request.TrySetCanceled();
    };
    core.NewWindowRequested+=(o,e)=>{e.Handled=true;if(e.Uri.StartsWith("https://"))core.Navigate(e.Uri);};
    core.DOMContentLoaded+=async(o,e)=>{
     if(documentId!=e.NavigationId)return;
     documentReady=true;
     await Refresh();
    };
    core.NavigationCompleted+=async(o,e)=>{
     if(documentId!=e.NavigationId||documentReady)return;
     if(e.IsSuccess){documentReady=true;await Refresh();}
     else SetStatus("연결 오류","인터넷 연결을 확인한 뒤 다시 시도하세요.");
    };
    core.Navigate(UsageUrl);
   }catch(Exception ex){SetStatus("연결 오류","로그인 화면을 시작할 수 없습니다: "+ex.Message);}
  }
  void SetStatus(string status,string detail,bool login=false){if(disposed)return;Snapshot.Status=status;Snapshot.Detail=detail;Snapshot.LoginRequired=login;if(!disposed&&Changed!=null)Changed();}
  public async Task Refresh(bool retryNavigation=false){
   if(disposed)return;
   if(Browser.CoreWebView2==null){await Initialize();return;}
   if(!documentReady){
    if(retryNavigation&&Snapshot.Status=="연결 오류")Browser.CoreWebView2.Navigate(UsageUrl);
    return;
   }
   if(busy){refreshQueued=true;return;}
   Uri current;
   string host=Provider=="Claude"?"claude.ai":"chatgpt.com";
   if(!Uri.TryCreate(Browser.CoreWebView2.Source,UriKind.Absolute,out current)||current.Scheme!="https"||current.Host!=host){
    SetStatus("로그인 필요","연결 버튼을 눌러 서비스에 로그인하세요.",true);return;
   }
   ulong requestDocument=documentId;
   busy=true;
   try{
   if(Provider=="Claude"){
    const string script=@"(async()=>{try{const orgResponse=await fetch('/api/organizations',{credentials:'same-origin'});if(!orgResponse.ok)return {status:orgResponse.status};const orgs=await orgResponse.json();if(!Array.isArray(orgs)||!orgs.length)return {status:401};const org=orgs.find(x=>Array.isArray(x.capabilities)&&x.capabilities.includes('chat'))||orgs[0];if(!/^[a-zA-Z0-9-]+$/.test(org.uuid))return {status:422};const r=await fetch('/api/organizations/'+encodeURIComponent(org.uuid)+'/usage',{credentials:'same-origin'});return {status:r.status,organization:org.name||'',data:r.ok?await r.json():null};}catch(e){return {status:0};}})()";
    var response=await Query(script);if(disposed||requestDocument!=documentId)return;int code=Convert.ToInt32(response["status"]);if(code==200&&response.ContainsKey("data")&&response["data"]!=null){Snapshot=UsageParser.Claude(serializer.Serialize(response["data"]));Snapshot.Organization=response.ContainsKey("organization")?Convert.ToString(response["organization"]):"";if(!disposed&&Changed!=null)Changed();}else SetStatus(code==401||code==403?"로그인 필요":code==429?"조회 제한":"조회 실패",code==401||code==403?"계정 연결을 다시 확인하세요.":"사용량을 가져오지 못했습니다. 잠시 후 새로고침하세요.",code==401||code==403);
   }else{
    const string script=@"(async()=>{try{const sessionResponse=await fetch('/api/auth/session',{credentials:'same-origin'});if(!sessionResponse.ok)return {status:sessionResponse.status};const session=await sessionResponse.json();if(!session.accessToken)return {status:401};const response=await fetch('/backend-api/wham/usage',{credentials:'same-origin',headers:{Authorization:'Bearer '+session.accessToken}});return {status:response.status,data:response.ok?await response.json():null};}catch(e){return {status:0};}})()";
    var response=await Query(script);if(disposed||requestDocument!=documentId)return;int code=Convert.ToInt32(response["status"]);
    if(code==200&&response.ContainsKey("data")&&response["data"]!=null){Snapshot=UsageParser.Codex(serializer.Serialize(response["data"]));if(!disposed&&Changed!=null)Changed();}
    else SetStatus(code==401||code==403?"로그인 필요":code==429?"조회 제한":"조회 실패",code==401||code==403?"Codex를 사용하는 ChatGPT 계정으로 연결하세요.":"Codex 한도를 읽지 못했습니다. 잠시 후 다시 시도하세요.",code==401||code==403);
   }
   }catch{
    if(!disposed&&requestDocument==documentId)SetStatus("조회 실패","응답을 읽을 수 없습니다. 재연결 또는 새로고침하세요.");
   }finally{busy=false;}
   if(refreshQueued){refreshQueued=false;await Refresh();}
  }
  public async void OpenLogin(){
   bool initialized=Browser.CoreWebView2!=null;
   await Initialize();
   if(initialized&&!disposed&&Browser.CoreWebView2!=null)Browser.CoreWebView2.Navigate(UsageUrl);
  }
  public void Dispose(){disposed=true;Changed=null;foreach(var item in pending.Values.ToArray())item.TrySetCanceled();pending.Clear();Browser.Dispose();}
 }
 public partial class Dashboard {
  UsageConnection claudeUsage,gptUsage,loginConnection;Grid engines;DispatcherTimer usageTimer;Border loginOverlay;Grid usageRoot;ScrollViewer usageScroll;WrapPanel usageCards;TextBlock usageInfo;
  public void ShowSessions(){
   screen="sessions";Render();
   if(!Storage.IsTest||Environment.GetCommandLineArgs().Contains("--session-test"))StartUsage();
  }
  UIElement SessionsView(){
   if(usageRoot!=null)return usageRoot;usageRoot=new Grid();usageRoot.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});usageRoot.RowDefinitions.Add(new RowDefinition());usageRoot.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var header=new DockPanel{Margin=new Thickness(0,0,0,10)};var refresh=Design.Button("새로고침",async()=>await RefreshUsage(true));DockPanel.SetDock(refresh,Dock.Right);header.Children.Add(refresh);header.Children.Add(Design.Text("구독 사용량",19));Design.Put(usageRoot,header,0,0);usageCards=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};usageScroll=new ScrollViewer{Content=usageCards,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};usageScroll.SizeChanged+=(o,e)=>SizeUsageCards();Design.Put(usageRoot,usageScroll,1,0);usageInfo=Design.Text("",10,Design.Muted);usageInfo.Margin=new Thickness(0,8,0,0);Design.Put(usageRoot,usageInfo,2,0);
   UpdateUsage();return usageRoot;
  }
  void SetupUsage(){
   engines=new Grid{Width=2,Height=2,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false};claudeUsage=new UsageConnection("Claude");gptUsage=new UsageConnection("Codex");engines.Children.Add(claudeUsage.Browser);engines.Children.Add(gptUsage.Browser);chrome.Children.Add(engines);foreach(var connection in new[]{claudeUsage,gptUsage}){var c=connection;c.Changed=()=>{UpdateUsage();if(loginConnection==c&&c.Snapshot.Metrics.Count>0&&!c.Snapshot.LoginRequired)CloseUsageLogin(c);};}if(!Storage.IsTest)Loaded+=(o,e)=>Dispatcher.BeginInvoke(new Action(StartUsage),DispatcherPriority.Background);usageTimer=new DispatcherTimer{Interval=TimeSpan.FromMinutes(1)};usageTimer.Tick+=async(o,e)=>await RefreshUsage(true);Closed+=(o,e)=>{usageTimer.Stop();claudeUsage.Dispose();gptUsage.Dispose();};
  }
  async void StartUsage(){
   await Task.WhenAll(claudeUsage.Initialize(),gptUsage.Initialize());
   if(!usageTimer.IsEnabled&&IsLoaded)usageTimer.Start();
  }
  Task RefreshUsage(bool retryNavigation){return Task.WhenAll(claudeUsage.Refresh(retryNavigation),gptUsage.Refresh(retryNavigation));}
  void UpdateUsage(){
   if(!Storage.IsTest){
    try{
     File.WriteAllText(Path.Combine(Storage.VaultFolder,"session-status.txt"),string.Join(Environment.NewLine,new[]{claudeUsage,gptUsage}.Select(c=>c.Provider+": "+c.Snapshot.Status+"; metrics="+c.Snapshot.Metrics.Count)));
    }catch(IOException){}catch(UnauthorizedAccessException){}
   }
   if(usageCards==null)return;
   usageCards.Children.Clear();foreach(var connection in new[]{claudeUsage,gptUsage}){var c=connection;var snapshot=c.Snapshot;var panel=new StackPanel{Margin=new Thickness(5,0,5,12)};var head=new DockPanel();var button=Design.Button(snapshot.Metrics.Count>0?"계정":"연결",()=>OpenUsageLogin(c));button.Padding=new Thickness(7,4,7,4);DockPanel.SetDock(button,Dock.Right);head.Children.Add(button);head.Children.Add(Design.Text(c.Provider,16));panel.Children.Add(head);var statusText=Design.Text(snapshot.Status,11,Design.Muted);statusText.Margin=new Thickness(0,5,0,8);panel.Children.Add(statusText);var metrics=new System.Windows.Controls.Primitives.UniformGrid{Columns=1};panel.Children.Add(metrics);if(c.Provider=="Codex"){foreach(var metric in UsageParser.CodexCards(snapshot))metrics.Children.Add(UsageCard(metric,c.Provider));}else{if(snapshot.Metrics.Count==0)metrics.Children.Add(UsageCard(new UsageMetric{Label="사용률 / 잔여량",Text="—"},c.Provider));foreach(var metric in snapshot.Metrics)metrics.Children.Add(UsageCard(metric,c.Provider));}var detail=Design.Text(snapshot.Detail,10,Design.Muted);detail.Margin=new Thickness(0,7,0,0);panel.Children.Add(detail);if(snapshot.Updated.HasValue)panel.Children.Add(Design.Text("마지막 확인 "+snapshot.Updated.Value.ToLocalTime().ToString("HH:mm:ss"),9,Design.Muted));usageCards.Children.Add(panel);}usageInfo.Text="로그인 상태는 이 PC에 유지됩니다. ‘연결’은 같은 위젯 안에서 열립니다.";SizeUsageCards();ApplyCommandSizes();}
  void SizeUsageCards(){
   double available=usageScroll.ActualWidth>0?usageScroll.ActualWidth:Width-30;
   double groupWidth=Math.Max(120,Math.Min(600,Math.Floor((available-10)/2)-10));
   foreach(StackPanel group in usageCards.Children){
    group.Width=groupWidth;
    var metrics=group.Children.OfType<System.Windows.Controls.Primitives.UniformGrid>().Single();
    metrics.Columns=Math.Max(1,Math.Min(metrics.Children.Count,(int)(groupWidth/162)));
   }
  }
  UIElement UsageCard(UsageMetric metric,string provider){var p=new StackPanel{Margin=new Thickness(3)};var value=Design.Text(metric.Used.HasValue?metric.Used.Value.ToString("0.#")+"%":metric.Text??"—",metric.Used.HasValue?28:22);value.HorizontalAlignment=HorizontalAlignment.Center;value.TextAlignment=TextAlignment.Center;p.Children.Add(value);if(metric.Used.HasValue){var remaining=Design.Text(metric.Remaining,12,Design.Muted);remaining.HorizontalAlignment=HorizontalAlignment.Center;p.Children.Add(remaining);p.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=metric.Used.Value,Height=4,Margin=new Thickness(0,9,0,7),Foreground=provider=="Claude"?UI.Color("#DE9C81"):UI.Color("#8FCDB8"),Background=UI.Color("#393840")});var reset=Design.Text(metric.ResetText,10,Design.Muted);reset.TextAlignment=TextAlignment.Center;p.Children.Add(reset);}var label=Design.Text(metric.Label,11,provider=="Claude"?UI.Color("#DE9C81"):UI.Color("#8FCDB8"));label.Margin=new Thickness(0,8,0,0);label.TextAlignment=TextAlignment.Center;p.Children.Add(label);return new Border{Child=p,Background=UI.Color("#202126"),BorderBrush=UI.Color("#454249"),BorderThickness=new Thickness(3),CornerRadius=new CornerRadius(17),Padding=new Thickness(12),Margin=new Thickness(3,0,3,8)};}
  void OpenUsageLogin(UsageConnection connection){if(loginOverlay!=null)return;loginConnection=connection;var panel=new DockPanel();var controls=new DockPanel{Margin=new Thickness(0,0,0,8)};var buttons=UI.Row();buttons.Children.Add(Design.Button("새로고침",()=>{if(connection.Browser.CoreWebView2!=null)connection.Browser.Reload();else connection.OpenLogin();}));buttons.Children.Add(Design.Button("카드로 돌아가기",async()=>{CloseUsageLogin(connection);await connection.Refresh();}));DockPanel.SetDock(buttons,Dock.Right);controls.Children.Add(buttons);controls.Children.Add(Design.Text(connection.Provider+" 계정 연결",14));DockPanel.SetDock(controls,Dock.Top);panel.Children.Add(controls);engines.Children.Remove(connection.Browser);panel.Children.Add(connection.Browser);loginOverlay=new Border{Child=panel,Margin=new Thickness(10),Padding=new Thickness(10),Background=Design.Paper,BorderBrush=Design.Teal,BorderThickness=new Thickness(1)};chrome.Children.Add(loginOverlay);ApplyCommandSizes();connection.OpenLogin();}
  void CloseUsageLogin(UsageConnection connection){if(loginOverlay==null)return;((DockPanel)loginOverlay.Child).Children.Remove(connection.Browser);chrome.Children.Remove(loginOverlay);loginOverlay=null;loginConnection=null;engines.Children.Add(connection.Browser);UpdateUsage();}
  internal async Task<string> VerifyUsageSessions(){UsageParser.Verify();UsageParser.VerifyCodex();UsageParser.VerifyCodexCards();ShowSessions();for(int n=0;n<100&&(claudeUsage.Browser.CoreWebView2==null||gptUsage.Browser.CoreWebView2==null);n++)await Task.Delay(200);if(claudeUsage.Browser.CoreWebView2==null||gptUsage.Browser.CoreWebView2==null)throw new Exception("Inline session initialization failed");UpdateLayout();App.Capture(this,"session-preview.png");return "PASS: inline session view; two persistent service profiles; always-visible five-hour and weekly Codex cards; independent periods; no daily substitution; remaining/reset calculations; missing values stay unknown. Account login and live account values require user login.";}
 }
}
