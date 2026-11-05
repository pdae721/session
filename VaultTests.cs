using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
namespace DesktopTodo {
 public static class VaultTests {
  public static int Run(){Storage.IsTest=true;Storage.Folder=Path.Combine(Path.GetTempPath(),"DesktopTodo-vault-test-"+Guid.NewGuid().ToString("N"));try{
   DateRules.Verify();Directory.CreateDirectory(Storage.Folder);var legacy=new State();legacy.Tasks.Add(new Todo{Title="기존 로컬 할 일",Note="메모\n\n- 체크리스트",Category="캡스톤",Subcategory="발표",Color="sage"});string original=Path.Combine(Storage.Folder,"data.json");using(var f=File.Create(original))new DataContractJsonSerializer(typeof(State)).WriteObject(f,legacy);string originalText=File.ReadAllText(original);
   Directory.CreateDirectory(Storage.VaultFolder);using(var f=File.Create(Storage.FilePath))new DataContractJsonSerializer(typeof(State)).WriteObject(f,new State());
   var state=Storage.Load();Storage.Save(state);if(state.Tasks.Count!=1||Directory.GetFiles(Storage.TasksFolder,"*.md").Length!=1||File.ReadAllText(original)!=originalText)throw new Exception("Lossless legacy migration failed despite existing empty settings");
   Categories.RenameChild(state,"캡스톤","발표","발표 준비");Storage.Save(state);state=Storage.Load();if(state.Tasks[0].Subcategory!="발표 준비"||!Categories.Children(state,"캡스톤").Contains("발표 준비"))throw new Exception("Subcategory relationship persistence");state.Tasks[0].Due="1";bool invalid=false;try{Storage.Save(state);}catch(InvalidDataException){invalid=true;}if(!invalid)throw new Exception("Invalid date reached disk");state.Tasks[0].Due="";
   string path=Directory.GetFiles(Storage.TasksFolder,"*.md").Single();string text=File.ReadAllText(path);File.WriteAllText(path,text.Replace("기존 로컬 할 일","외부에서 수정한 일"));state.MainX+=50;Storage.Save(state);if(Storage.Load().Tasks[0].Title!="외부에서 수정한 일")throw new Exception("Layout save erased external edit");
   state=Storage.Load();var live=state.Tasks[0];Storage.Save(state);if(!object.ReferenceEquals(live,state.Tasks[0]))throw new Exception("Save replaced live task identity");
   File.WriteAllText(path,File.ReadAllText(path).Replace("외부에서 수정한 일","디스크 변경"));live.Title="앱 동시 변경";bool conflict=false;try{Storage.Save(state);}catch(IOException){conflict=true;}if(!conflict||!File.ReadAllText(path).Contains("디스크 변경"))throw new Exception("Concurrent edit not preserved");
   state=Storage.Load();state.Tasks[0].Done=true;Storage.Save(state);state.Tasks.Clear();Storage.Save(state);if(Directory.GetFiles(Storage.TasksFolder,"*.md").Length!=0||!Directory.GetFiles(Path.Combine(Storage.VaultFolder,"History"),"*.md",SearchOption.AllDirectories).Any())throw new Exception("Deletion history missing");
   if(!Storage.SessionFolder.StartsWith(Storage.Folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Storage.SessionFolder==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"DesktopTodoVault",".profiles"))throw new Exception("Tests share real login profiles");
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"vault-test-results.txt"),"PASS: lossless migration to per-task Markdown; original preserved; external edit survives layout save; stable task identities; conflict protection; deleted task history; isolated test profiles.");return 0;
  }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"vault-test-results.txt"),e.ToString());return 1;}finally{try{Directory.Delete(Storage.Folder,true);}catch{}}}
 }
}
