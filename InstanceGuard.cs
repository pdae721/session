using System;
using System.IO;
using System.Linq;
namespace DesktopTodo {
 public static class InstanceGuard {
  static FileStream lease;
  public static bool Enter(){try{Directory.CreateDirectory(Storage.VaultFolder);lease=new FileStream(Path.Combine(Storage.VaultFolder,".running.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);return true;}catch(IOException){return false;}}
  public static void Release(){if(lease!=null){lease.Dispose();lease=null;}}
  public static bool ForwardToInstalled(string[] args){if(Storage.IsTest)return false;string installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DesktopTodo","DesktopTodo.exe");if(!File.Exists(installed)||string.Equals(Path.GetFullPath(installed),Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location),StringComparison.OrdinalIgnoreCase))return false;string arguments=string.Join(" ",args.Where(a=>a.StartsWith("--")&&a.All(c=>char.IsLetterOrDigit(c)||c=='-')));System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installed,arguments){WorkingDirectory=Path.GetDirectoryName(installed),UseShellExecute=true});return true;}
 }
}
