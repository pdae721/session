using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace DesktopTodo {
 public partial class Dashboard {
  static IEnumerable<DependencyObject> LogicalElements(DependencyObject root){yield return root;foreach(object child in LogicalTreeHelper.GetChildren(root)){var d=child as DependencyObject;if(d!=null)foreach(var descendant in LogicalElements(d))yield return descendant;}}
  void ApplyCommandSizes(){double width=Math.Floor(Math.Max(60,Math.Min(84,(Width-40)/4-5)));foreach(var b in LogicalElements(chrome).OfType<Button>().Where(b=>Equals(b.Tag,"command"))){b.Width=width;b.Height=32;b.Padding=new Thickness(4,0,4,0);b.HorizontalContentAlignment=HorizontalAlignment.Center;}}
  internal void VerifyUniformButtons(){foreach(var size in new[]{new[]{440.0,380.0},new[]{650.0,540.0}}){Width=size[0];Height=size[1];SetCalendar(true);ApplyCommandSizes();UpdateLayout();var buttons=LogicalElements(chrome).OfType<Button>().Where(b=>Equals(b.Tag,"command")&&b.Visibility==Visibility.Visible).ToList();if(buttons.Count<6||buttons.Any(b=>b.Width!=buttons[0].Width||b.Height!=32))throw new Exception("Unequal command buttons");}SetCalendar(false);}
 }
}
