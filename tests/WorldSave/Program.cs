using System.Text.Json;
using RagnavikServerBridge;
using UnityEngine;

void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); }
var root=Path.Combine(Path.GetTempPath(),"ragnavik-save-test-"+Guid.NewGuid());Directory.CreateDirectory(root);BepInEx.Paths.ConfigPath=root;
try {
 WorldSaveAdapter.Install(new BridgePlugin());
 string Send(long? created=null) {
  var id=Guid.NewGuid().ToString("N");
  File.WriteAllText(Path.Combine(root,"RagnavikMaintenance/save-request.json"),JsonSerializer.Serialize(new {id,createdAt=created??DateTimeOffset.UtcNow.ToUnixTimeSeconds()}));
  Time.realtimeSinceStartup+=2;WorldSaveAdapter.Tick();return id;
 }
 JsonElement Result()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"RagnavikMaintenance/save-result.json"))).RootElement;
 var id=Send();Check(Result().GetProperty("id").GetString()==id&&Result().GetProperty("status").GetString()=="completed","successful writes produce matching durable receipt");
 Check(ZNet.instance.Calls==1,"save invoked once");
 Time.realtimeSinceStartup+=2;WorldSaveAdapter.Tick();Check(ZNet.instance.Calls==1,"same request not repeated");
 WorldSaveAdapter.Stop();WorldSaveAdapter.Install(new BridgePlugin());Time.realtimeSinceStartup+=2;WorldSaveAdapter.Tick();Check(ZNet.instance.Calls==1,"completed request not repeated after adapter restart");
 Send(DateTimeOffset.UtcNow.ToUnixTimeSeconds()-60);Check(Result().GetProperty("status").GetString()=="failed"&&ZNet.instance.Calls==1,"expired request never saves");
 ZNet.instance.Busy=true;Send();Check(Result().GetProperty("status").GetString()=="failed"&&ZNet.instance.Calls==1,"overlapping save refused");ZNet.instance.Busy=false;
 ZNet.instance.Outcome="failure";Send();Check(Result().GetProperty("status").GetString()=="failed","write failure returns failed receipt");
 ZNet.instance.Outcome="none";Send();Check(Result().GetProperty("status").GetString()=="failed","skipped save cannot count as success");
 ZNet.instance.Outcome="throw";Send();Check(Result().GetProperty("error").GetString()=="fixture failure","original exception reported");
 Check(Application.Observers==0,"log observer always removed");
 ZNet.instance.Dedicated=false;var calls=ZNet.instance.Calls;Send();Check(ZNet.instance.Calls==calls,"client/host save requests refused");
} finally {WorldSaveAdapter.Stop();Directory.Delete(root,true);}

public class ZNet {
 public static ZNet instance=new(); public int Calls;public bool Busy;public bool Dedicated=true;public string Outcome="success";
 public bool IsServer()=>true;public bool IsDedicated()=>Dedicated;public bool IsSaving()=>Busy;
 public void Save(bool sync,bool profiles,bool delayed) {
  if(!sync||profiles||delayed)throw new Exception("incorrect save options");Calls++;
  if(Outcome=="throw")throw new Exception("fixture failure");
  if(Outcome=="none")return;
  Application.Emit("### Save World Thread Started! ###");
  // Exercise the actual threaded log callback used by the save worker.
  Task.Run(()=>Application.Emit(Outcome=="success"?"World save (5/5) done.":"World save (5/5) FAILED.")).GetAwaiter().GetResult();
 }
}
namespace BepInEx {public static class Paths {public static string ConfigPath="";}}
namespace UnityEngine {
 public static class Time {public static float realtimeSinceStartup;}
 public enum LogType {Log}
 public static class Application {
  public static event Action<string,string,LogType>? logMessageReceivedThreaded;
  public static int Observers=>logMessageReceivedThreaded?.GetInvocationList().Length??0;
  public static void Emit(string s)=>logMessageReceivedThreaded?.Invoke(s,"",LogType.Log);
 }
 public static class JsonUtility {
  private static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
  public static T? FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,Options);
  public static string ToJson(object o)=>JsonSerializer.Serialize(o,o.GetType(),Options);
 }
}
namespace RagnavikServerBridge { public class BridgePlugin {public LogSink Log=new();} public class LogSink { public void LogWarning(string s){} public void LogInfo(string s){} } }
