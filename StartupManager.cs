using System;
using System.IO;
namespace DesktopTodo {
 public static class StartupManager {
  public const string TaskName="DesktopTodo";
  public static bool Enabled(){try{dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));service.Connect();return (bool)service.GetFolder("\\").GetTask(TaskName).Enabled;}catch{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return key!=null&&key.GetValue(TaskName)!=null;}}
  public static void SetEnabled(bool value){dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));service.Connect();dynamic folder=service.GetFolder("\\");if(!value){try{folder.GetTask(TaskName).Enabled=false;}catch{}RemoveLegacy();return;}
   string installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DesktopTodo","DesktopTodo.exe");string exe=File.Exists(installed)?installed:System.Reflection.Assembly.GetExecutingAssembly().Location;string user=System.Security.Principal.WindowsIdentity.GetCurrent().Name;
   dynamic definition=service.NewTask(0);definition.RegistrationInfo.Description="Desktop Todo 바탕화면 위젯 · 로그인 후 시작 및 실패 시 재시도";definition.Principal.UserId=user;definition.Principal.LogonType=3;definition.Principal.RunLevel=0;
   dynamic trigger=definition.Triggers.Create(9);trigger.UserId=user;trigger.Delay="PT15S";dynamic action=definition.Actions.Create(0);action.Path=exe;action.WorkingDirectory=Path.GetDirectoryName(exe);action.Arguments="--startup";
   definition.Settings.Enabled=true;definition.Settings.AllowDemandStart=true;definition.Settings.StartWhenAvailable=true;definition.Settings.DisallowStartIfOnBatteries=false;definition.Settings.StopIfGoingOnBatteries=false;definition.Settings.ExecutionTimeLimit="PT0S";definition.Settings.RestartCount=3;definition.Settings.RestartInterval="PT1M";definition.Settings.MultipleInstances=2;
   folder.RegisterTaskDefinition(TaskName,definition,6,user,null,3,null);RemoveLegacy();
  }
  static void RemoveLegacy(){using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))if(key!=null)key.DeleteValue(TaskName,false);}
 }
}
