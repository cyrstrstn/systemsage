using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SystemSage {
  static partial class Scan {
    public static List<string[]> Gpu(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Name,AdapterRAM,DriverVersion,DriverDate,Status,VideoProcessor FROM Win32_VideoController").Get()){
          string name=Clean(x["Name"]);if(String.IsNullOrEmpty(name))continue;
          long ram=0;Int64.TryParse(Convert.ToString(x["AdapterRAM"]),out ram);
          string vram=ram>0&&ram<uint.MaxValue?Bytes(ram):(ram>0?Bytes(ram&0xFFFFFFFF):"—");
          rows.Add(new[]{name,vram,Clean(x["DriverVersion"]),FormatWmiDate(Convert.ToString(x["DriverDate"])),Clean(x["Status"]), "Temp not exposed by Windows WMI"});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No GPU reported","—","—","—","—"});
      return rows;
    }

    public static List<string[]> DiskHealth(){
      var predict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("root\\wmi","SELECT InstanceName,PredictFailure,Reason FROM MSStorageDriver_FailurePredictStatus").Get()){
          string inst=Clean(x["InstanceName"]);bool fail=false;try{fail=Convert.ToBoolean(x["PredictFailure"]);}catch{}
          predict[inst]=fail?"PREDICT FAILURE":"OK";
        }
      }catch{}
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Model,InterfaceType,Size,Status,SerialNumber,PNPDeviceID FROM Win32_DiskDrive").Get()){
          string model=Clean(x["Model"]),pnp=Clean(x["PNPDeviceID"]),serial=Clean(x["SerialNumber"]);
          long size=0;Int64.TryParse(Convert.ToString(x["Size"]),out size);
          string smart="Unavailable";
          foreach(var kv in predict){if((!String.IsNullOrEmpty(pnp)&&kv.Key.IndexOf(pnp,StringComparison.OrdinalIgnoreCase)>=0)||(!String.IsNullOrEmpty(serial)&&kv.Key.IndexOf(serial,StringComparison.OrdinalIgnoreCase)>=0)||kv.Key.IndexOf(model,StringComparison.OrdinalIgnoreCase)>=0){smart=kv.Value;break;}}
          if(smart=="Unavailable"&&predict.Count==1)smart=predict.Values.First();
          rows.Add(new[]{model,Clean(x["InterfaceType"]),Bytes(size),Clean(x["Status"]),smart});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No disks reported","—","—","—"});
      return rows;
    }

    public static List<string[]> Wifi(){
      var rows=new List<string[]>();
      try{
        var psi=new ProcessStartInfo("netsh","wlan show interfaces"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        using(var p=Process.Start(psi)){
          string output=p.StandardOutput.ReadToEnd();p.WaitForExit(8000);
          if(p.ExitCode!=0||String.IsNullOrWhiteSpace(output)||output.IndexOf("no wireless",StringComparison.OrdinalIgnoreCase)>=0){
            rows.Add(new[]{"Unavailable","No wireless interface / Wi-Fi off","—","—","—"});
            return rows;
          }
          string name="—",ssid="—",state="—",signal="—",speed="—",radio="—";
          foreach(string raw in output.Split(new[]{"\r\n","\n"},StringSplitOptions.None)){
            string line=raw.Trim();int colon=line.IndexOf(':');if(colon<0)continue;
            string key=line.Substring(0,colon).Trim(),val=line.Substring(colon+1).Trim();
            if(key.Equals("Name",StringComparison.OrdinalIgnoreCase)){if(name!="—"&&ssid!="—"){rows.Add(new[]{name,ssid,state,signal,speed});name=val;ssid=state=signal=speed="—";}else name=val;}
            else if(key.Equals("SSID",StringComparison.OrdinalIgnoreCase)&&!key.Equals("BSSID",StringComparison.OrdinalIgnoreCase))ssid=val;
            else if(key.Equals("State",StringComparison.OrdinalIgnoreCase))state=val;
            else if(key.Equals("Signal",StringComparison.OrdinalIgnoreCase))signal=val;
            else if(key.IndexOf("Receive rate",StringComparison.OrdinalIgnoreCase)>=0||key.IndexOf("Transmit rate",StringComparison.OrdinalIgnoreCase)>=0){if(speed=="—")speed=val+" Mbps";else speed=speed+" / "+val+" Mbps";}
            else if(key.Equals("Radio type",StringComparison.OrdinalIgnoreCase))radio=val;
          }
          if(name!="—"||ssid!="—")rows.Add(new[]{name,String.IsNullOrEmpty(ssid)?"(not connected)":ssid,state+(radio!="—"?" / "+radio:""),signal,speed});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","Wi-Fi details not available","—","—","—"});
      return rows;
    }

    public static List<string[]> BootInfo(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT LastBootUpTime,Caption FROM Win32_OperatingSystem").Get()){
          DateTime boot=ManagementDateTimeConverter.ToDateTime(Convert.ToString(x["LastBootUpTime"]));
          rows.Add(new[]{"Last boot",boot.ToString("yyyy-MM-dd HH:mm:ss")});
          rows.Add(new[]{"Uptime",(DateTime.Now-boot).ToString(@"d\d\ h\h\ m\m")});
          break;
        }
      }catch(Exception e){rows.Add(new[]{"Boot time","Unavailable - "+e.Message});}
      rows.Add(new[]{"Last boot duration",ReadBootDurationSeconds()});
      rows.Add(new[]{"Startup entries",Startup().Count.ToString()+" (see systemsage startup)"});
      return rows;
    }
    static string ReadBootDurationSeconds(){
      try{
        var query=new System.Diagnostics.Eventing.Reader.EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational",System.Diagnostics.Eventing.Reader.PathType.LogName){ReverseDirection=true};
        using(var reader=new System.Diagnostics.Eventing.Reader.EventLogReader(query)){
          System.Diagnostics.Eventing.Reader.EventRecord rec;
          int checkedCount=0;
          while((rec=reader.ReadEvent())!=null&&checkedCount<50){
            using(rec){
              checkedCount++;
              if(rec.Id!=100)continue;
              string xml=rec.ToXml()??"";
              Match m=Regex.Match(xml,"BootTime[^0-9]*([0-9]+)",RegexOptions.IgnoreCase);
              if(m.Success){double ms;if(Double.TryParse(m.Groups[1].Value,out ms))return Math.Round(ms/1000.0,1)+" s (diagnostics log)";}
              break;
            }
          }
        }
      }catch{}
      return "Unavailable (boot performance log not readable)";
    }

    public static List<string[]> WindowsUpdates(){
      var rows=new List<string[]>();
      string lastSuccess="Unavailable",lastInstall="Unavailable";
      try{
        using(var key=Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\Results\\Install")){
          if(key!=null)lastInstall=Convert.ToString(key.GetValue("LastSuccessTime")??"Unavailable");
        }
        using(var key=Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\Results\\Detect")){
          if(key!=null)lastSuccess=Convert.ToString(key.GetValue("LastSuccessTime")??lastSuccess);
        }
      }catch{}
      rows.Add(new[]{"Last detect success",String.IsNullOrWhiteSpace(lastSuccess)?"Unavailable":lastSuccess});
      rows.Add(new[]{"Last install success",String.IsNullOrWhiteSpace(lastInstall)?"Unavailable":lastInstall});
      int pending=-1;string pendingNote="Unavailable";var pendingTitles=new List<string>();
      var worker=new System.Threading.Thread(()=>{
        try{
          Type t=Type.GetTypeFromProgID("Microsoft.Update.Session");
          if(t==null){pendingNote="Windows Update COM unavailable";return;}
          object session=Activator.CreateInstance(t);
          object searcher=session.GetType().InvokeMember("CreateUpdateSearcher",System.Reflection.BindingFlags.InvokeMethod,null,session,null);
          try{searcher.GetType().InvokeMember("ServerSelection",System.Reflection.BindingFlags.SetProperty,null,searcher,new object[]{1});}catch{}
          object result=searcher.GetType().InvokeMember("Search",System.Reflection.BindingFlags.InvokeMethod,null,searcher,new object[]{"IsInstalled=0 and Type='Software' and IsHidden=0"});
          object updates=result.GetType().InvokeMember("Updates",System.Reflection.BindingFlags.GetProperty,null,result,null);
          pending=Convert.ToInt32(updates.GetType().InvokeMember("Count",System.Reflection.BindingFlags.GetProperty,null,updates,null));
          pendingNote=pending+" pending (software)";
          int show=Math.Min(pending,8);
          for(int i=0;i<show;i++){
            object update=updates.GetType().InvokeMember("Item",System.Reflection.BindingFlags.GetProperty,null,updates,new object[]{i});
            pendingTitles.Add(Convert.ToString(update.GetType().InvokeMember("Title",System.Reflection.BindingFlags.GetProperty,null,update,null)));
          }
        }catch(Exception e){pendingNote="Search unavailable - "+e.Message;}
      });
      worker.IsBackground=true;worker.Start();
      if(!worker.Join(20000))pendingNote="Search timed out (20s) - registry times still shown";
      rows.Add(new[]{"Pending updates",pending>=0?pending.ToString():pendingNote});
      for(int i=0;i<pendingTitles.Count;i++)rows.Add(new[]{"Pending #"+(i+1),pendingTitles[i]});
      if(pending>pendingTitles.Count&&pendingTitles.Count>0)rows.Add(new[]{"Pending more",(pending-pendingTitles.Count)+" additional update(s) not listed"});
      return rows;
    }

    public static List<string[]> Displays(){
      var rows=new List<string[]>();
      try{
        var screens=System.Windows.Forms.Screen.AllScreens;
        for(int i=0;i<screens.Length;i++){
          var s=screens[i];
          rows.Add(new[]{
            s.Primary?"Display "+(i+1)+" (primary)":"Display "+(i+1),
            s.Bounds.Width+" x "+s.Bounds.Height,
            s.BitsPerPixel+" bpp",
            s.DeviceName,
            "Refresh: see adapter"
          });
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—"});}
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Name,CurrentRefreshRate,MaxRefreshRate,VideoModeDescription FROM Win32_VideoController").Get()){
          string name=Clean(x["Name"]);if(String.IsNullOrEmpty(name))continue;
          string refresh=Clean(x["CurrentRefreshRate"]);if(refresh=="0"||String.IsNullOrEmpty(refresh))refresh=Clean(x["MaxRefreshRate"]);
          rows.Add(new[]{"GPU mode - "+name,Clean(x["VideoModeDescription"]),refresh==""||refresh=="0"?"—":refresh+" Hz","—","—"});
        }
      }catch{}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No displays reported","—","—","—"});
      return rows;
    }

    public static List<string[]> AudioDevices(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Name,Status,Manufacturer,PNPDeviceID FROM Win32_SoundDevice").Get()){
          rows.Add(new[]{Clean(x["Name"]),Clean(x["Manufacturer"]),Clean(x["Status"]),Clean(x["PNPDeviceID"])});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No audio devices reported","—","—"});
      try{
        using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Multimedia\\Sound Mapper")){
          if(key!=null){
            string play=Convert.ToString(key.GetValue("Playback")??"");
            string rec=Convert.ToString(key.GetValue("Record")??"");
            if(!String.IsNullOrWhiteSpace(play))rows.Insert(0,new[]{"Default playback (legacy map)",play,"—","—"});
            if(!String.IsNullOrWhiteSpace(rec))rows.Insert(Math.Min(1,rows.Count),new[]{"Default record (legacy map)",rec,"—","—"});
          }
        }
      }catch{}
      return rows;
    }

    public static List<string[]> BrowserCaches(){
      var rows=new List<string[]>();
      var raw=new List<string[]>();
      string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      AddBrowserCache(raw,"Microsoft Edge",Path.Combine(local,"Microsoft","Edge","User Data","Default","Cache"));
      AddBrowserCache(raw,"Microsoft Edge (Code Cache)",Path.Combine(local,"Microsoft","Edge","User Data","Default","Code Cache"));
      AddBrowserCache(raw,"Google Chrome",Path.Combine(local,"Google","Chrome","User Data","Default","Cache"));
      AddBrowserCache(raw,"Google Chrome (Code Cache)",Path.Combine(local,"Google","Chrome","User Data","Default","Code Cache"));
      AddBrowserCache(raw,"Mozilla Firefox",Path.Combine(local,"Mozilla","Firefox","Profiles"));
      AddBrowserCache(raw,"Brave",Path.Combine(local,"BraveSoftware","Brave-Browser","User Data","Default","Cache"));
      long total=0;foreach(var r in raw){long n;if(Int64.TryParse(r[4],out n))total+=n;rows.Add(new[]{r[0],r[1],r[2],r[3]});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No browser cache folders found","0","Preview only"});
      else rows.Add(new[]{"Combined",Bytes(total),raw.Count+" locations","Preview only - nothing deleted"});
      return rows;
    }
    static void AddBrowserCache(List<string[]> rows,string label,string path){
      if(!Directory.Exists(path))return;
      long bytes=0;int files=0;
      try{
        if(label.IndexOf("Firefox",StringComparison.OrdinalIgnoreCase)>=0){
          foreach(string profile in Directory.EnumerateDirectories(path)){
            foreach(string cache in new[]{Path.Combine(profile,"cache2"),Path.Combine(profile,"startupCache")}){
              if(!Directory.Exists(cache))continue;
              long b;int f;DirStats(cache,out f,out b);bytes+=b;files+=f;
            }
          }
        }else DirStats(path,out files,out bytes);
      }catch{}
      if(files==0&&bytes==0&&!Directory.Exists(path))return;
      rows.Add(new[]{label,Bytes(bytes),files.ToString()+" files","Preview only",bytes.ToString()});
    }
    static void DirStats(string root,out int files,out long bytes){files=0;bytes=0;try{foreach(string f in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){try{var info=new FileInfo(f);files++;bytes+=info.Length;}catch{}}}catch{}}

    public static string ToJson(string title,string[] columns,List<string[]> rows){
      var sb=new StringBuilder();
      sb.Append("{\"command\":\"").Append(JsonEsc(title)).Append("\",\"generated\":\"").Append(JsonEsc(DateTime.Now.ToString("o"))).Append("\",\"columns\":[");
      for(int i=0;i<columns.Length;i++){if(i>0)sb.Append(',');sb.Append('"').Append(JsonEsc(columns[i])).Append('"');}
      sb.Append("],\"rows\":[");
      for(int r=0;r<rows.Count;r++){if(r>0)sb.Append(',');sb.Append('[');for(int c=0;c<columns.Length;c++){if(c>0)sb.Append(',');string v=c<rows[r].Length?rows[r][c]:"";sb.Append('"').Append(JsonEsc(v)).Append('"');}sb.Append(']');}
      sb.Append("]}");
      return sb.ToString();
    }
    public static string ExportJsonBundle(){
      var parts=new List<string>();
      parts.Add(ToJson("memory",new[]{"Slot","Vendor","Capacity","Type","Speed","Form","Part number"},MemoryModules()));
      parts.Add(ToJson("storage",new[]{"Drive","Label","Format","Used","Available","Usage"},Drives()));
      parts.Add(ToJson("gpu",new[]{"Adapter","VRAM","Driver","Driver date","Status","Temp"},Gpu()));
      parts.Add(ToJson("diskhealth",new[]{"Model","Interface","Size","Status","SMART"},DiskHealth()));
      parts.Add(ToJson("wifi",new[]{"Adapter","SSID","State","Signal","Rate"},Wifi()));
      parts.Add(ToJson("display",new[]{"Display","Resolution","Color","Device","Note"},Displays()));
      parts.Add(ToJson("audio",new[]{"Name","Manufacturer","Status","PNP"},AudioDevices()));
      parts.Add(ToJson("browsers",new[]{"Browser","Size","Files","Action"},BrowserCaches()));
      parts.Add(ToJson("updates",new[]{"Property","Value"},WindowsUpdates()));
      parts.Add(ToJson("boot",new[]{"Property","Value"},BootInfo()));
      return "{\"systemsage\":\""+JsonEsc("1.4.0")+"\",\"machine\":\""+JsonEsc(Environment.MachineName)+"\",\"sections\":["+String.Join(",",parts)+"]}";
    }
    static string JsonEsc(string s){if(s==null)return "";return s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t");}
    static string Clean(object value){return Convert.ToString(value??"").Replace('\r',' ').Replace('\n',' ').Trim();}
    static string FormatWmiDate(string value){try{if(String.IsNullOrWhiteSpace(value)||value.Length<8)return "—";return ManagementDateTimeConverter.ToDateTime(value).ToString("yyyy-MM-dd");}catch{return "—";}}
  }
}
