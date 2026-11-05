using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace DesktopTodo {
 public static class TaskActions {
  public static string[] ColorNames={"보라","로즈","블루","세이지","샌드"};
  public static string[] ColorKeys={"violet","rose","blue","sage","sand"};
  public static string[] ColorValues={"#B5A0DB","#D7A5AC","#A4B5D1","#A4BFB4","#C8BB9D"};
  public static Brush Color(Todo t){int i=Array.IndexOf(ColorKeys,t.Color);return UI.Color(ColorValues[Math.Max(0,i)]);}
  public static Brush Background(Todo t){var c=((SolidColorBrush)Color(t)).Color;return new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(16+c.R*0.40),(byte)(16+c.G*0.40),(byte)(19+c.B*0.40)));}
  public static void Complete(State state,Todo t,bool value){t.Done=value;t.CompletedAt=value?DateTime.Today.ToString("yyyy-MM-dd"):"";
   if(value&&!string.IsNullOrEmpty(t.Repeat)&&t.Repeat!="none"&&string.IsNullOrEmpty(t.NextId)){
    DateTime date=t.Date??DateTime.Today;do{date=t.Repeat=="monthly"?date.AddMonths(1):date.AddDays(t.Repeat=="weekly"?7:1);}while(date<=DateTime.Today);
    var next=new Todo{Title=t.Title,Category=t.Category,Subcategory=t.Subcategory,Note=t.Note,Due=date.ToString("yyyy-MM-dd"),Priority=t.Priority,Important=t.Important,Urgent=t.Urgent,Color=t.Color,Repeat=t.Repeat,Order=DateTime.Now.Ticks};DateTime reminder;if(DateTime.TryParse(t.Reminder,out reminder))next.Reminder=date.Add(reminder.TimeOfDay).ToString("yyyy-MM-ddTHH:mm");t.NextId=next.Id;state.Tasks.Add(next);
   }
  }
  public static void Move(Todo task,int quadrant){task.Important=quadrant<2;task.Urgent=quadrant%2==0;task.Order=DateTime.Now.Ticks;}
 }
}
