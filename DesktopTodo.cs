using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopTodo {
 [DataContract] public class SubcategoryDefinition {
  [DataMember] public string Category;
  [DataMember] public string Name;
 }
 [DataContract] public class Todo {
  [DataMember] public string Id = Guid.NewGuid().ToString();
  [DataMember] public string Title = "";
  [DataMember] public string Category = "개인";
  [DataMember] public string Subcategory = "";
  [DataMember] public string Note = "";
  [DataMember] public string Due = "";
  [DataMember] public int Priority = 1;
  [DataMember] public bool Done;
  [DataMember] public bool Important;
  [DataMember] public bool Urgent;
  [DataMember] public string Color = "violet";
  [DataMember] public string Repeat = "none";
  [DataMember] public string Reminder = "";
  [DataMember] public string Reminded = "";
  [DataMember] public string CompletedAt = "";
  [DataMember] public string NextId = "";
  [DataMember] public double Order;
  public int Quadrant { get { return (Important?0:2)+(Urgent?0:1); } }
  public DateTime? Date { get { DateTime d; return DateTime.TryParseExact(Due,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out d) ? d : (DateTime?)null; } }
 }
 [DataContract] public class State {
  [DataMember] public List<Todo> Tasks = new List<Todo>();
  [DataMember] public List<string> Categories = new List<string> { "캡스톤", "과제", "개인" };
  [DataMember] public List<SubcategoryDefinition> Subcategories = new List<SubcategoryDefinition>();
  [DataMember] public double MainX = 40, MainY = 50, CalendarX = 500, CalendarY = 50;
  [DataMember] public double Width = 650, Height = 540;
  [DataMember] public bool Locked;
  [DataMember] public int Version = 2;
  [DataMember] public string Theme = "dark";
  public void Migrate() {
   if(Subcategories==null)Subcategories=new List<SubcategoryDefinition>();foreach(var t in Tasks)if(!string.IsNullOrWhiteSpace(t.Subcategory)&&!Subcategories.Exists(s=>s.Category==t.Category&&s.Name==t.Subcategory))Subcategories.Add(new SubcategoryDefinition{Category=t.Category,Name=t.Subcategory});
   if(Version<2) { Width=650;Height=540; foreach(var t in Tasks){t.Important=t.Priority==2;t.Urgent=t.Date.HasValue&&t.Date.Value<=DateTime.Today.AddDays(1);}Version=2; }
   Width=Math.Max(440,Math.Min(1400,Width)); Height=Math.Max(380,Math.Min(1100,Height));
  }
 }
 public static class Native {
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
  delegate bool EnumProc(IntPtr h,IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb,IntPtr p);
  [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int index,int value);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out PointNative p);
  [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h,ref PointNative p);
  [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RectNative r);
  public struct PointNative { public int X,Y; }
  struct RectNative { public int L,T,R,B; }
  public static IntPtr Desktop() {
   var prog = FindWindow("Progman",null); IntPtr result;
   if(prog == IntPtr.Zero) return IntPtr.Zero;
   SendMessageTimeout(prog,0x052c,IntPtr.Zero,IntPtr.Zero,2,1000,out result);
   IntPtr host = IntPtr.Zero;
   EnumWindows(delegate(IntPtr h,IntPtr p) {
    IntPtr view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);
    if(view!=IntPtr.Zero) {
     // Windows 11 and Wallpaper Engine can composite Progman children behind
     // the desktop surface. Host alongside the icon list inside that surface.
     host = view;
     return false;
    }
    return true;
   },IntPtr.Zero);
   return host;
  }
  public static bool Attach(Window w,IntPtr parent,double x,double y) {
   var h = new WindowInteropHelper(w).Handle;
   if(parent==IntPtr.Zero) return false;
   int style = GetWindowLong(h,-16);
   SetWindowLong(h,-16,(style | 0x40000000) & unchecked((int)~0x80000000));
   SetParent(h,parent);
   if(GetParent(h)!=parent) return false;
   SetWindowLong(h,-20,(GetWindowLong(h,-20)|0x80)&~0x40000);
   Move(w,(int)x,(int)y);
   SetWindowPos(h,IntPtr.Zero,0,0,0,0,0x0073);
   return true;
  }
  public static void Move(Window w,int x,int y) {
   var h = new WindowInteropHelper(w).Handle; var p = new PointNative{X=x,Y=y};
   ScreenToClient(GetParent(h),ref p);
   SetWindowPos(h,IntPtr.Zero,p.X,p.Y,0,0,0x0015);
  }
  public static PointNative Position(Window w) { RectNative r; GetWindowRect(new WindowInteropHelper(w).Handle,out r); return new PointNative{X=r.L,Y=r.T}; }
  public static int PixelWidth(Window w) {RectNative r;GetWindowRect(new WindowInteropHelper(w).Handle,out r);return r.R-r.L;}
  public static bool IsAttached(Window w,IntPtr parent){if(w==null)return false;var h=new WindowInteropHelper(w).Handle;return IsWindow(h)&&GetParent(h)==parent&&IsWindowVisible(h);}
  public static IntPtr CurrentDesktop(){IntPtr result=IntPtr.Zero;EnumWindows(delegate(IntPtr h,IntPtr p){var view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);if(view==IntPtr.Zero)return true;result=view;return false;},IntPtr.Zero);return result;}
 }
 public static class UI {
  public static Brush Bg = Color("#171B24"), Card = Color("#222835"), Text = Color("#EDF1FA"), Muted = Color("#94A0B6"), Accent = Color("#B7A1FF");
  public static Brush Color(string s) { return (Brush)new BrushConverter().ConvertFromString(s); }
  public static TextBlock Label(string s,double size,Brush color=null) { return new TextBlock{Text=s,FontSize=size,Foreground=color??Text,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center}; }
  public static Button Button(string s,Action click,bool primary=false) {
   var b = new Button{Content=s,Foreground=primary?Bg:Text,Background=primary?Accent:Card,BorderThickness=new Thickness(0),Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,6,0),Cursor=Cursors.Hand,FontSize=12};
   b.Click+=(o,e)=>click(); return b;
  }
  public static Border Box(UIElement child,Brush bg=null,int padding=14) { return new Border{Child=child,Background=bg??Card,CornerRadius=new CornerRadius(12),Padding=new Thickness(padding),Margin=new Thickness(0,0,0,9)}; }
  public static StackPanel Row() { return new StackPanel{Orientation=Orientation.Horizontal}; }
  public static TextBox Input(string value="") { return new TextBox{Text=value,FontSize=14,Padding=new Thickness(9),Margin=new Thickness(0,5,0,12),Background=Card,Foreground=Text,BorderBrush=Color("#394253"),CaretBrush=Text}; }
 }
}
