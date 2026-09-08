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
    public static List<string[]> Printers(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Name,Default,PrinterStatus,WorkOffline,PortName,DriverName FROM Win32_Printer").Get()){
          bool def=false;try{def=Convert.ToBoolean(x["Default"]);}catch{}
          bool offline=false;try{offline=Convert.ToBoolean(x["WorkOffline"]);}catch{}
          int st=0;Int32.TryParse(Convert.ToString(x["PrinterStatus"]),out st);
          string status=offline?"Offline":(st==3?"Idle":st==4?"Printing":st==0?"Other":"Status "+st);
          rows.Add(new[]{Clean(x["Name"]),def?"Yes":"No",status,Clean(x["PortName"]),Clean(x["DriverName"])});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No printers found","—","—","—"});
      return rows;
    }

    public static List<string[]> Bluetooth(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Name,Status,PNPClass,DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth' OR Name LIKE '%Bluetooth%'").Get()){
          string name=Clean(x["Name"]);if(String.IsNullOrEmpty(name))continue;
          rows.Add(new[]{name,Clean(x["Status"]),Clean(x["PNPClass"]),Clean(x["DeviceID"])});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—"});}
      if(rows.Count==0){
        try{
          using(var key=Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\BTHPORT\\Parameters\\Devices")){
            if(key!=null){
              foreach(string id in key.GetSubKeyNames()){
                using(var d=key.OpenSubKey(id)){
                  if(d==null)continue;
                  object raw=d.GetValue("Name");string name=DecodeRegBytes(raw);
                  if(String.IsNullOrWhiteSpace(name))name=id;
                  rows.Add(new[]{name,"Paired (registry)","Bluetooth",id});
                }
              }
            }
          }
        }catch{}
      }
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No Bluetooth devices / adapter reported","—","—"});
      return rows.Take(40).ToList();
    }

    public static List<string[]> UsbDevices(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Name,Status,PNPClass,DeviceID,Manufacturer FROM Win32_PnPEntity WHERE PNPClass = 'USB' OR DeviceID LIKE 'USB\\\\%'").Get()){
          string name=Clean(x["Name"]);if(String.IsNullOrEmpty(name))continue;
          if(name.IndexOf("Root Hub",StringComparison.OrdinalIgnoreCase)>=0)continue;
          rows.Add(new[]{name,Clean(x["Manufacturer"]),Clean(x["Status"]),Clean(x["PNPClass"]),Clean(x["DeviceID"])});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No USB devices reported","—","—","—"});
      return rows.GroupBy(r=>r[0]+"|"+r[4]).Select(g=>g.First()).Take(50).ToList();
    }

    public static List<string[]> Firewall(){
      var rows=new List<string[]>();
      try{
        var psi=new ProcessStartInfo("netsh","advfirewall show allprofiles"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
        using(var p=Process.Start(psi)){
          string output=p.StandardOutput.ReadToEnd();p.WaitForExit(8000);
          string profile="—",state="—",inbound="—",outbound="—";
          foreach(string raw in output.Split(new[]{"\r\n","\n"},StringSplitOptions.None)){
            string line=raw.Trim();
            if(line.EndsWith("Profile Settings:",StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(line,"Profile Settings:\\s*$",RegexOptions.IgnoreCase)){
              if(profile!="—")rows.Add(new[]{profile,state,inbound,outbound});
              profile=Regex.Replace(line,"\\s*Profile Settings:\\s*$","",RegexOptions.IgnoreCase).Trim();
              state=inbound=outbound="—";
              continue;
            }
            if(line.StartsWith("State",StringComparison.OrdinalIgnoreCase)&&line.IndexOf("Profile",StringComparison.OrdinalIgnoreCase)<0){
              state=line.Substring(5).Trim(' ','\t',':');continue;
            }
            if(line.StartsWith("Firewall Policy",StringComparison.OrdinalIgnoreCase)){
              string val=line.Substring("Firewall Policy".Length).Trim(' ','\t',':');
              inbound=val.IndexOf("BlockInbound",StringComparison.OrdinalIgnoreCase)>=0?"Block inbound":(val.IndexOf("AllowInbound",StringComparison.OrdinalIgnoreCase)>=0?"Allow inbound":val);
              outbound=val.IndexOf("BlockOutbound",StringComparison.OrdinalIgnoreCase)>=0?"Block outbound":(val.IndexOf("AllowOutbound",StringComparison.OrdinalIgnoreCase)>=0?"Allow outbound":val);
              continue;
            }
          }
          if(profile!="—")rows.Add(new[]{profile,state,inbound,outbound});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","Firewall status not readable","—","—"});
      return rows;
    }

    public static List<string[]> BitLocker(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("root\\CIMV2\\Security\\MicrosoftVolumeEncryption","SELECT DriveLetter,ProtectionStatus,ConversionStatus,EncryptionMethod FROM Win32_EncryptableVolume").Get()){
          string letter=Clean(x["DriveLetter"]);if(String.IsNullOrEmpty(letter))letter="—";
          int prot=0,conv=0,meth=0;
          Int32.TryParse(Convert.ToString(x["ProtectionStatus"]),out prot);
          Int32.TryParse(Convert.ToString(x["ConversionStatus"]),out conv);
          Int32.TryParse(Convert.ToString(x["EncryptionMethod"]),out meth);
          string protection=prot==0?"Off / unprotected":prot==1?"On":prot==2?"Unknown":"Status "+prot;
          string conversion=conv==0?"Fully decrypted":conv==1?"Fully encrypted":conv==2?"Encryption in progress":conv==3?"Decryption in progress":"Status "+conv;
          string method=meth==0?"None":meth==1?"AES-128+diffuser":meth==2?"AES-256+diffuser":meth==3?"AES-128":meth==4?"AES-256":meth==5?"Hardware":meth==6?"XTS-AES-128":meth==7?"XTS-AES-256":"Method "+meth;
          rows.Add(new[]{letter,protection,conversion,method});
        }
      }catch(Exception e){
        rows.Add(new[]{"Unavailable","Need elevated rights or BitLocker WMI unavailable","—",e.Message});
      }
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No encryptable volumes reported","—","—"});
      return rows;
    }

    public static List<string[]> RestorePoints(){
      var rows=new List<string[]>();
      try{
        var points=new List<ManagementObject>();
        foreach(ManagementObject x in new ManagementObjectSearcher("root\\DEFAULT","SELECT SequenceNumber,Description,CreationTime,RestorePointType FROM SystemRestore").Get())points.Add(x);
        rows.Add(new[]{"Restore point count",points.Count.ToString(),"","",""});
        foreach(var x in points.OrderByDescending(p=>Convert.ToString(p["CreationTime"]??"")).Take(15)){
          string created="—";
          try{created=ManagementDateTimeConverter.ToDateTime(Convert.ToString(x["CreationTime"])).ToString("yyyy-MM-dd HH:mm");}catch{}
          rows.Add(new[]{"#"+Clean(x["SequenceNumber"]),Clean(x["Description"]),created,RestoreTypeName(Convert.ToString(x["RestorePointType"])),""});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—",""});}
      if(rows.Count==1&&rows[0][0]=="Restore point count"&&rows[0][1]=="0")rows.Add(new[]{"None","No restore points found","—","—",""});
      return rows.Select(r=>new[]{r[0],r[1],r[2],r[3]}).ToList();
    }
    static string RestoreTypeName(string code){
      int n;if(!Int32.TryParse(code,out n))return code??"—";
      switch(n){case 0:return "Application install";case 1:return "Application uninstall";case 6:return "Restore";case 7:return "Checkpoint";case 10:return "Device driver";case 12:return "Modify settings";case 13:return "Cancelled operation";default:return "Type "+n;}
    }

    public static List<string[]> InstalledApps(int limit){
      var apps=new List<AppSize>();
      CollectUninstallApps(Registry.LocalMachine,"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall",apps);
      CollectUninstallApps(Registry.LocalMachine,"Software\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall",apps);
      CollectUninstallApps(Registry.CurrentUser,"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall",apps);
      var ranked=apps.Where(a=>!String.IsNullOrWhiteSpace(a.Name)).GroupBy(a=>a.Name,StringComparer.OrdinalIgnoreCase).Select(g=>g.OrderByDescending(x=>x.Bytes).First()).OrderByDescending(a=>a.Bytes).Take(limit).ToList();
      var rows=ranked.Select(a=>new[]{a.Name,Bytes(a.Bytes),a.Version,a.Publisher}).ToList();
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No installed apps found","—","—"});
      long total=ranked.Sum(a=>a.Bytes);
      rows.Add(new[]{"Top "+ranked.Count+" combined",Bytes(total),ranked.Count.ToString()+" apps","Estimated from InstallLocation when present"});
      return rows;
    }
    sealed class AppSize { public string Name; public string Version; public string Publisher; public long Bytes; }
    static void CollectUninstallApps(RegistryKey root,string path,List<AppSize> apps){
      try{
        using(var key=root.OpenSubKey(path)){
          if(key==null)return;
          foreach(string sub in key.GetSubKeyNames()){
            try{
              using(var app=key.OpenSubKey(sub)){
                if(app==null)continue;
                if(Convert.ToInt32(app.GetValue("SystemComponent",0))==1)continue;
                string name=Convert.ToString(app.GetValue("DisplayName")??"").Trim();
                if(String.IsNullOrEmpty(name))continue;
                string ver=Convert.ToString(app.GetValue("DisplayVersion")??"—");
                string pub=Convert.ToString(app.GetValue("Publisher")??"—");
                long bytes=0;
                object est=app.GetValue("EstimatedSize");
                if(est!=null){long kb;if(Int64.TryParse(Convert.ToString(est),out kb))bytes=kb*1024;}
                string loc=Convert.ToString(app.GetValue("InstallLocation")??"").Trim().Trim('"');
                if(!String.IsNullOrEmpty(loc)&&Directory.Exists(loc)){
                  long folder=DirBytesLimited(loc,TimeSpan.FromMilliseconds(250));
                  if(folder>bytes)bytes=folder;
                }
                apps.Add(new AppSize{Name=name,Version=ver,Publisher=pub,Bytes=bytes});
              }
            }catch{}
          }
        }
      }catch{}
    }
    static long DirBytesLimited(string root,TimeSpan budget){
      long total=0;var sw=Stopwatch.StartNew();var pending=new Stack<string>();pending.Push(root);int files=0;
      while(pending.Count>0&&sw.Elapsed<budget&&files<8000){
        string dir=pending.Pop();
        try{
          foreach(string f in Directory.EnumerateFiles(dir)){try{total+=new FileInfo(f).Length;files++;}catch{} if(sw.Elapsed>=budget||files>=8000)break;}
          foreach(string child in Directory.EnumerateDirectories(dir)){try{var a=File.GetAttributes(child);if((a&FileAttributes.ReparsePoint)==0)pending.Push(child);}catch{}}
        }catch{}
      }
      return total;
    }

    public static List<string[]> Thermal(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("root\\wmi","SELECT InstanceName,CurrentTemperature FROM MSAcpi_ThermalZoneTemperature").Get()){
          double raw=0;Double.TryParse(Convert.ToString(x["CurrentTemperature"]),out raw);
          // tenths Kelvin
          string c=raw>0?Math.Round((raw/10.0)-273.15,1)+" C":"—";
          rows.Add(new[]{Clean(x["InstanceName"]),c,raw>0?Math.Round(raw/10.0,1)+" K":"—","ACPI thermal zone"});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","WMI thermal not exposed on many desktops"});}
      if(rows.Count==0)rows.Add(new[]{"Unavailable","No ACPI thermal zones reported","—","Normal on many PCs without OEM sensors in WMI"});
      return rows;
    }

    public static List<string[]> ProxyAndHosts(){
      var rows=new List<string[]>();
      try{
        using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings")){
          if(key!=null){
            int enabled=0;Int32.TryParse(Convert.ToString(key.GetValue("ProxyEnable")??"0"),out enabled);
            string server=Convert.ToString(key.GetValue("ProxyServer")??"—");
            string bypass=Convert.ToString(key.GetValue("ProxyOverride")??"—");
            string auto=Convert.ToString(key.GetValue("AutoConfigURL")??"—");
            rows.Add(new[]{"Proxy enabled",enabled==1?"Yes":"No"});
            rows.Add(new[]{"Proxy server",String.IsNullOrWhiteSpace(server)?"—":server});
            rows.Add(new[]{"Proxy bypass",String.IsNullOrWhiteSpace(bypass)?"—":bypass});
            rows.Add(new[]{"Auto-config URL",String.IsNullOrWhiteSpace(auto)?"—":auto});
          }
        }
      }catch(Exception e){rows.Add(new[]{"Proxy","Unavailable - "+e.Message});}
      try{
        string hosts=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"drivers\\etc\\hosts");
        if(File.Exists(hosts)){
          var lines=File.ReadAllLines(hosts).Where(l=>{string t=l.Trim();return t.Length>0&&!t.StartsWith("#");}).ToList();
          rows.Add(new[]{"Hosts file",hosts});
          rows.Add(new[]{"Hosts custom entries",lines.Count.ToString()});
          foreach(string line in lines.Take(12))rows.Add(new[]{"Hosts entry",line.Length>120?line.Substring(0,117)+"...":line});
          if(lines.Count>12)rows.Add(new[]{"Hosts more",(lines.Count-12)+" additional entries not listed"});
        }else rows.Add(new[]{"Hosts file","Not found"});
      }catch(Exception e){rows.Add(new[]{"Hosts file","Unavailable - "+e.Message});}
      return rows;
    }

    public static string BaselinePath(){
      string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SystemSage Reports");
      Directory.CreateDirectory(dir);
      return Path.Combine(dir,"SystemSage-baseline.json");
    }
    public static string SaveBaseline(){
      string path=BaselinePath();
      File.WriteAllText(path,BuildBaselineJson(),Encoding.UTF8);
      return path;
    }
    public static List<string[]> CompareBaseline(){
      string path=BaselinePath();
      var rows=new List<string[]>();
      if(!File.Exists(path)){rows.Add(new[]{"Baseline","Not found - run systemsage baseline --save first","",""});return rows;}
      string oldJson=File.ReadAllText(path);
      string newJson=BuildBaselineJson();
      var oldMap=ParseBaselineMap(oldJson);var newMap=ParseBaselineMap(newJson);
      string oldWhen=ExtractJsonString(oldJson,"generated");string newWhen=ExtractJsonString(newJson,"generated");
      rows.Add(new[]{"Baseline file",path,"",""});
      rows.Add(new[]{"Baseline generated",String.IsNullOrEmpty(oldWhen)?"—":oldWhen,"",""});
      rows.Add(new[]{"Current generated",String.IsNullOrEmpty(newWhen)?"—":newWhen,"",""});
      var keys=oldMap.Keys.Union(newMap.Keys).OrderBy(k=>k).ToList();
      int changed=0;
      foreach(string key in keys){
        string a=oldMap.ContainsKey(key)?oldMap[key]:"(missing)";
        string b=newMap.ContainsKey(key)?newMap[key]:"(missing)";
        if(a==b)continue;
        changed++;
        rows.Add(new[]{key,a,b,"Changed"});
      }
      if(changed==0)rows.Add(new[]{"Result","No differences in tracked metrics","","OK"});
      else rows.Insert(3,new[]{"Changed metrics",changed.ToString(),"","Review rows below"});
      return rows;
    }
    static string BuildBaselineJson(){
      long[] mem=Memory();
      var drives=Drives();
      var startup=Startup();
      var sb=new StringBuilder();
      sb.Append("{\"systemsage\":\"1.5.0\",\"generated\":\"").Append(JsonEsc(DateTime.Now.ToString("o"))).Append("\",\"machine\":\"").Append(JsonEsc(Environment.MachineName)).Append("\",\"metrics\":{");
      sb.Append("\"memory_used\":\"").Append(JsonEsc(Bytes(mem[0]))).Append("\",");
      sb.Append("\"memory_total\":\"").Append(JsonEsc(Bytes(mem[1]))).Append("\",");
      sb.Append("\"memory_type\":\"").Append(JsonEsc(MemoryGenerationSummary(MemoryModules()))).Append("\",");
      sb.Append("\"startup_count\":\"").Append(startup.Count).Append("\",");
      for(int i=0;i<drives.Count;i++){
        var d=drives[i];
        sb.Append("\"drive_").Append(JsonEsc((d[0]??"").Replace(":\\","").Replace("\\",""))).Append("_used\":\"").Append(JsonEsc(d[3])).Append("\",");
        sb.Append("\"drive_").Append(JsonEsc((d[0]??"").Replace(":\\","").Replace("\\",""))).Append("_free\":\"").Append(JsonEsc(d[4])).Append("\",");
        sb.Append("\"drive_").Append(JsonEsc((d[0]??"").Replace(":\\","").Replace("\\",""))).Append("_usage\":\"").Append(JsonEsc(d[5])).Append("\",");
      }
      string[] bat=Battery();
      sb.Append("\"battery_health\":\"").Append(JsonEsc(bat[1])).Append("\",");
      sb.Append("\"power_state\":\"").Append(JsonEsc(bat[2])).Append("\"");
      sb.Append("}}");
      return sb.ToString();
    }
    static Dictionary<string,string> ParseBaselineMap(string json){
      var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
      Match m=Regex.Match(json,"\\\"metrics\\\"\\s*:\\s*\\{([^}]*)\\}");
      if(!m.Success)return map;
      foreach(Match kv in Regex.Matches(m.Groups[1].Value,"\\\"([^\\\"]+)\\\"\\s*:\\s*\\\"([^\\\"]*)\\\""))map[kv.Groups[1].Value]=kv.Groups[2].Value;
      return map;
    }
    static string ExtractJsonString(string json,string key){
      Match m=Regex.Match(json,"\\\""+Regex.Escape(key)+"\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"");
      return m.Success?m.Groups[1].Value:"";
    }
    static string DecodeRegBytes(object raw){
      if(raw is string)return Convert.ToString(raw).Trim('\0').Trim();
      byte[] bytes=raw as byte[];if(bytes==null||bytes.Length==0)return "";
      try{return Encoding.UTF8.GetString(bytes).Trim('\0').Trim();}catch{try{return Encoding.Unicode.GetString(bytes).Trim('\0').Trim();}catch{return "";}}
    }
  }
}
