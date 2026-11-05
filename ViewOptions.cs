using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace DesktopTodo {
 public partial class Dashboard {
  readonly Button viewOptionsButton;readonly ComboBox subcategoryChoice;string subcategory="";
  void RefreshSubcategoryFilter(){if(updating)return;var values=new[]{"세부 분류: 전체"}.Concat(Categories.Children(app.Data,category)).ToList();var old=subcategoryChoice.ItemsSource as System.Collections.Generic.List<string>;if(old!=null&&old.SequenceEqual(values))return;updating=true;if(!values.Skip(1).Contains(subcategory))subcategory="";subcategoryChoice.ItemsSource=values;subcategoryChoice.SelectedIndex=subcategory==""?0:values.IndexOf(subcategory);updating=false;}
  void ShowViewOptions(){var menu=new ContextMenu();var compactItem=new MenuItem{Header="간결하게 보기",IsCheckable=true,IsChecked=compact,IsEnabled=screen!="sessions"};compactItem.Click+=(o,e)=>{compact=compactItem.IsChecked;Render();};menu.Items.Add(compactItem);var completedItem=new MenuItem{Header="완료된 할 일만 보기",IsCheckable=true,IsChecked=completedOnly,IsEnabled=screen!="sessions"};completedItem.Click+=(o,e)=>{completedOnly=completedItem.IsChecked;Render();};menu.Items.Add(completedItem);var colors=new MenuItem{Header="할 일 색상",IsEnabled=screen!="sessions"};foreach(var key in new[]{""}.Concat(TaskActions.ColorKeys)){string value=key;var item=new MenuItem{Header=key==""?"모든 색상":TaskActions.ColorNames[Array.IndexOf(TaskActions.ColorKeys,key)],IsCheckable=true,IsChecked=color==key};item.Click+=(o,e)=>{color=value;Render();};colors.Items.Add(item);}menu.Items.Add(colors);menu.Items.Add(new Separator());var categories=new MenuItem{Header="카테고리 · 세부 분류"};categories.Click+=(o,e)=>ShowCategories();menu.Items.Add(categories);menu.PlacementTarget=viewOptionsButton;menu.IsOpen=true;}
  void JumpToToday(){day=DateTime.Today;month=new DateTime(day.Year,day.Month,1);Render();}
  internal void VerifySubcategoryFilter(){var t=app.Data.Tasks[0];Categories.Ensure(app.Data,t.Category,"필터 검사");t.Subcategory="필터 검사";app.Commit();category=t.Category;subcategory="필터 검사";Render();if(Tasks().Count!=1||Tasks()[0].Id!=t.Id)throw new Exception("Subcategory filter is not functional");subcategory="";category="";day=DateTime.Today.AddDays(10);month=new DateTime(day.Year,day.Month,1).AddMonths(1);JumpToToday();if(day!=DateTime.Today||month.Month!=DateTime.Today.Month)throw new Exception("Today navigation failed");}
 }
}
