using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace DesktopTodo {
 public partial class Dashboard {
  internal string VerifyCodexQuotaLayout(){
   UsageParser.VerifyCodex();
   UsageParser.VerifyCodexCards();
   ShowSessions();
   claudeUsage.Snapshot=UsageParser.Claude("{\"five_hour\":{\"utilization\":32},\"seven_day\":{\"utilization\":28},\"seven_day_sonnet\":{\"utilization\":14},\"seven_day_opus\":{\"utilization\":9}}");
   gptUsage.Snapshot=UsageParser.Codex("{\"rate_limit\":{\"primary_window\":{\"used_percent\":32,\"limit_window_seconds\":18000},\"secondary_window\":{\"used_percent\":28,\"limit_window_seconds\":604800}}}");
   UpdateUsage();
   var claude=(StackPanel)usageCards.Children[0];
   var codex=(StackPanel)usageCards.Children[1];
   var codexCards=codex.Children.OfType<UniformGrid>().Single();
   foreach(var width in new[]{440,650,900,440}){
    Width=width;Height=380;UpdateLayout();
    var viewport=Elements(view).OfType<ScrollContentPresenter>().Single();
    var left=claude.TransformToAncestor(viewport).Transform(new Point());
    var right=codex.TransformToAncestor(viewport).Transform(new Point());
    if(Math.Abs(left.Y-right.Y)>1||left.X+claude.ActualWidth>right.X||right.X+codex.ActualWidth>viewport.ActualWidth+1)
     throw new Exception("Claude and Codex must be visible side by side at "+width);
    foreach(var provider in new[]{claude,codex}){
     var firstCard=(FrameworkElement)provider.Children.OfType<UniformGrid>().Single().Children[0];
     if(firstCard.TransformToAncestor(viewport).Transform(new Point(0,firstCard.ActualHeight)).Y>viewport.ActualHeight+1)
      throw new Exception("Both providers' first cards must be visible at "+width);
    }
    if(codexCards.Columns!=(width==900?2:1))throw new Exception("Cards must reflow within their provider column");
   }
   var text=Elements(view).OfType<TextBlock>().Select(t=>t.Text).ToList();
   if(text.Count(t=>t=="5시간 한도")!=1||text.Count(t=>t=="주간 한도")!=1)throw new Exception("Codex quota cards must be separate and visible");
   App.Capture(this,"codex-quota-preview.png");
   return "PASS: Claude left and Codex right, with both visible at 440, 650 and 900 pixels, and separate Codex limits.";
  }
 }
 public static partial class UsageParser {
  public static List<UsageMetric> CodexCards(UsageSnapshot snapshot){var cards=new List<UsageMetric>();foreach(var label in new[]{"5시간 한도","주간 한도"}){UsageMetric found=null;foreach(var metric in snapshot.Metrics)if(metric.Label==label){found=metric;break;}cards.Add(found??new UsageMetric{Label=label,Text="—"});}return cards;}
  public static void VerifyCodexCards(){var empty=CodexCards(new UsageSnapshot{Provider="Codex"});if(empty.Count!=2||empty[0].Label!="5시간 한도"||empty[1].Label!="주간 한도"||empty[0].Used.HasValue||empty[1].Used.HasValue)throw new Exception("Both Codex quota cards must always exist");var weekly=CodexCards(Codex("{\"rate_limit\":{\"primary_window\":{\"used_percent\":24,\"limit_window_seconds\":604800}}}"));if(weekly[0].Used.HasValue||weekly[1].Used!=24)throw new Exception("Weekly usage must not become five-hour usage");var daily=CodexCards(Codex("{\"rate_limit\":{\"primary_window\":{\"used_percent\":42,\"limit_window_seconds\":86400}}}"));if(daily[0].Used.HasValue||daily[1].Used.HasValue)throw new Exception("Daily usage must not be substituted for Codex quota windows");}
  static double? Number(Dictionary<string,object> data,string key){object value;double number;if(data==null||!data.TryGetValue(key,out value)||value==null||!double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out number)||double.IsNaN(number)||double.IsInfinity(number))return null;return number;}
  static void CodexWindow(UsageSnapshot result,Dictionary<string,object> rate,string key,string fallback,string prefix=""){
   object raw;if(rate==null||!rate.TryGetValue(key,out raw))return;var window=raw as Dictionary<string,object>;if(window==null)return;var used=Number(window,"used_percent");if(!used.HasValue||used.Value<0||used.Value>100)return;var seconds=Number(window,"limit_window_seconds");string label=fallback;if(seconds.HasValue){if(seconds.Value==18000)label="5시간 한도";else if(seconds.Value==604800)label="주간 한도";else if(seconds.Value>=86400)label=(seconds.Value/86400).ToString("0.#")+"일 한도";else label=(seconds.Value/3600).ToString("0.#")+"시간 한도";}
   DateTimeOffset? reset=null;var epoch=Number(window,"reset_at");var remaining=Number(window,"reset_after_seconds");if(epoch.HasValue){try{reset=new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(epoch.Value);}catch(ArgumentOutOfRangeException){}}else if(remaining.HasValue&&remaining.Value>=0)reset=DateTimeOffset.Now.AddSeconds(Math.Min(remaining.Value,315360000));
   result.Metrics.Add(new UsageMetric{Label=prefix+label,Used=used,Reset=reset});
  }
  public static UsageSnapshot Codex(string json){var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);var result=new UsageSnapshot{Provider="Codex",Updated=DateTimeOffset.Now};object raw;var rate=data.TryGetValue("rate_limit",out raw)?raw as Dictionary<string,object>:null;CodexWindow(result,rate,"primary_window","주 사용 한도");CodexWindow(result,rate,"secondary_window","보조 사용 한도");var review=data.TryGetValue("code_review_rate_limit",out raw)?raw as Dictionary<string,object>:null;CodexWindow(result,review,"primary_window","코드 리뷰 한도","리뷰 · ");CodexWindow(result,review,"secondary_window","보조 한도","리뷰 · ");string plan=data.TryGetValue("plan_type",out raw)?Convert.ToString(raw):"";result.Status=result.Metrics.Count>0?"연결됨":"한도 정보 미제공";result.Detail=result.Metrics.Count>0?"Codex 실제 사용률"+(string.IsNullOrEmpty(plan)?"":" · "+plan)+" · 1분마다 갱신":"현재 계정의 Codex 한도 응답에 사용률이 없습니다.";return result;}
  public static void VerifyCodex(){var data=Codex("{\"plan_type\":\"plus\",\"rate_limit\":{\"primary_window\":{\"used_percent\":32,\"limit_window_seconds\":18000,\"reset_at\":4070908800},\"secondary_window\":{\"used_percent\":28,\"limit_window_seconds\":604800,\"reset_after_seconds\":3600}}}");if(data.Metrics.Count!=2||data.Metrics[0].Remaining!="68% 남음"||data.Metrics[0].Label!="5시간 한도"||data.Metrics[1].Label!="주간 한도"||!data.Metrics[0].Reset.HasValue)throw new Exception("Codex rate windows failed");if(Codex("{\"rate_limit\":null}").Metrics.Count!=0||Codex("{\"rate_limit\":{\"primary_window\":{\"used_percent\":null}}}").Metrics.Count!=0)throw new Exception("Missing Codex usage must not become zero");var weekly=Codex("{\"rate_limit\":{\"primary_window\":{\"used_percent\":0,\"limit_window_seconds\":604800}}}");if(weekly.Metrics[0].Label!="주간 한도"||weekly.Metrics[0].Remaining!="100% 남음")throw new Exception("Weekly-only Codex plan");}
 }
}
