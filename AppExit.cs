using System;
using System.Threading;
namespace DesktopTodo {
 public static class AppExit {
  static string Name {get{return "Local\\DesktopTodo.Exit."+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;}}
  static string CommandFile {get{return System.IO.Path.Combine(Storage.VaultFolder,"desktop-command.txt");}}
  public static int Signal(){return Send("exit");}
  public static int ShowSessions(){return Send("sessions");}
  static int Send(string command){try{System.IO.Directory.CreateDirectory(Storage.VaultFolder);System.IO.File.WriteAllText(CommandFile,DateTime.UtcNow.Ticks+"\n"+command);return 0;}catch{return 2;}}
  public static bool Poll(App app){try{if(!System.IO.File.Exists(CommandFile))return false;var lines=System.IO.File.ReadAllLines(CommandFile);long ticks;System.IO.File.Delete(CommandFile);if(lines.Length<2||!long.TryParse(lines[0],out ticks)||ticks<System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks)return false;if(lines[1]=="exit"){app.RequestExit();return true;}if(lines[1]=="sessions")app.OpenSessions();}catch{}return false;}
  public static EventWaitHandle Listen(App app){var handle=new EventWaitHandle(false,EventResetMode.AutoReset,Name);ThreadPool.RegisterWaitForSingleObject(handle,(o,t)=>{if(!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(new Action(app.RequestExit));},null,Timeout.Infinite,false);return handle;}
 }
}
