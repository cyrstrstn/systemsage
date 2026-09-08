using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Threading;

namespace SystemSage {
  static class Ui {
    public static void Header(string title) {
      if(!Console.IsOutputRedirected)try{Console.Clear();}catch{} Console.ForegroundColor=ConsoleColor.Green;
      Console.WriteLine("  ____            _                 ____");
      Console.WriteLine(" / ___| _   _ ___| |_ ___ _ __ ___/ ___|  __ _  __ _  ___");
      Console.WriteLine(" \\___ \\| | | / __| __/ _ \\ '_ ` _ \\___ \\ / _` |/ _` |/ _ \\");
      Console.WriteLine("  ___) | |_| \\__ \\ ||  __/ | | | | |___) | (_| | (_| |  __/");
      Console.WriteLine(" |____/ \\__, |___/\\__\\___|_| |_| |_|____/ \\__,_|\\__, |\\___|");
      Console.WriteLine("        |___/                                  |___/");
      Console.ResetColor(); Console.WriteLine(" Lightweight Windows diagnostics  |  MIT open source\n");
      if(!String.IsNullOrEmpty(title)){Console.ForegroundColor=ConsoleColor.White;Console.WriteLine(" "+title.ToUpperInvariant());Console.ForegroundColor=ConsoleColor.DarkGray;Console.WriteLine(" "+new string('-',Math.Min(70,title.Length+8)));Console.ResetColor();}
    }
    static int progressLength;
    public static void Progress(int percent,string label){if(Console.IsOutputRedirected)return;percent=Math.Max(0,Math.Min(100,percent));int width=Math.Max(12,Math.Min(34,SafeWidth()-38));int filled=(int)Math.Round(width*percent/100.0);string line=" ["+new string('#',filled)+new string('-',width-filled)+"] "+percent.ToString().PadLeft(3)+"%  "+label;int available=Math.Max(20,SafeWidth()-1);if(line.Length>available)line=line.Substring(0,Math.Max(1,available-3))+"...";Console.Write("\r"+line.PadRight(Math.Max(progressLength,line.Length)));progressLength=Math.Max(progressLength,line.Length);if(percent>=100){Console.WriteLine();progressLength=0;}}
    public static T Loading<T>(string label,Func<T> action){if(Console.IsOutputRedirected)return action();bool done=false;Exception failure=null;T result=default(T);var worker=new Thread(()=>{try{result=action();}catch(Exception e){failure=e;}finally{done=true;}});worker.Start();string[] frames={"|","/","-","\\"};int i=0;while(!done){string line=" ["+frames[i++%frames.Length]+"] "+label;Console.Write("\r"+line.PadRight(Math.Max(progressLength,line.Length)));progressLength=Math.Max(progressLength,line.Length);Thread.Sleep(90);}Console.Write("\r"+new string(' ',progressLength)+"\r");progressLength=0;if(failure!=null)throw failure;Console.ForegroundColor=ConsoleColor.Green;Console.WriteLine(" [OK] "+label);Console.ResetColor();return result;}
    public static void Pair(string name,string value){Console.ForegroundColor=ConsoleColor.DarkGray;Console.Write((" "+name).PadRight(23));Console.ResetColor();Console.WriteLine(value);}
    public static void Table(string[] heads,List<string[]> rows){int width=Math.Max(70,SafeWidth()-2),cols=heads.Length;int[] sizes=new int[cols];for(int i=0;i<cols;i++){sizes[i]=heads[i].Length;foreach(var r in rows)if(i<r.Length)sizes[i]=Math.Max(sizes[i],r[i]==null?0:r[i].Length);sizes[i]=Math.Min(sizes[i],Math.Max(12,width/cols));}Console.ForegroundColor=ConsoleColor.Green;WriteRow(heads,sizes);Console.ResetColor();Console.ForegroundColor=ConsoleColor.DarkGray;Console.WriteLine(" "+new string('-',Math.Min(width,sizes.Sum()+cols*2)));Console.ResetColor();foreach(var r in rows)WriteRow(r,sizes);}
    static void WriteRow(string[] row,int[] sizes){Console.Write(" ");for(int i=0;i<sizes.Length;i++){string v=i<row.Length?(row[i]??""):"";if(v.Length>sizes[i])v=v.Substring(0,Math.Max(1,sizes[i]-3))+"...";Console.Write(v.PadRight(sizes[i]+2));}Console.WriteLine();}
    static int SafeWidth(){try{return Console.WindowWidth;}catch{return 100;}}
    public static void Pause(){Console.ForegroundColor=ConsoleColor.DarkGray;Console.Write("\n Press any key to return...");Console.ResetColor();Console.ReadKey(true);}
  }

  static partial class Scan {
    public static int Cpu(){using(var c=new PerformanceCounter("Processor","% Processor Time","_Total")){c.NextValue();Thread.Sleep(400);return (int)Math.Round(c.NextValue());}}
    public static long[] Memory(){foreach(ManagementObject x in new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem").Get()){long t=Convert.ToInt64(x["TotalVisibleMemorySize"])*1024,f=Convert.ToInt64(x["FreePhysicalMemory"])*1024;return new[]{t-f,t};}return new long[]{0,0};}
    public static string Bytes(long value){string[] u={"B","KB","MB","GB","TB"};double n=value;int i=0;while(n>=1024&&i<u.Length-1){n/=1024;i++;}return n.ToString(n>=100?"0":n>=10?"0.0":"0.00")+" "+u[i];}
    public static List<string[]> Processes(int limit){return Process.GetProcesses().Select(p=>{try{return new[]{p.ProcessName,p.Id.ToString(),Bytes(p.WorkingSet64),p.WorkingSet64.ToString(),p.Threads.Count.ToString()};}catch{return null;}}).Where(x=>x!=null).OrderByDescending(x=>Int64.Parse(x[3])).Take(limit).Select(x=>new[]{x[0],x[1],x[2],x[4]}).ToList();}
    public static List<string[]> Drives(){return DriveInfo.GetDrives().Where(d=>d.IsReady).Select(d=>new[]{d.Name,String.IsNullOrEmpty(d.VolumeLabel)?"Local disk":d.VolumeLabel,d.DriveFormat,Bytes(d.TotalSize-d.AvailableFreeSpace),Bytes(d.AvailableFreeSpace),Math.Round((double)(d.TotalSize-d.AvailableFreeSpace)/d.TotalSize*100)+"%"}).ToList();}
    public static List<string[]> Security(){var rows=new List<string[]>();try{foreach(ManagementObject x in new ManagementObjectSearcher("root\\SecurityCenter2","SELECT displayName,productState FROM AntivirusProduct").Get())rows.Add(new[]{Convert.ToString(x["displayName"]),"Registered","0x"+Convert.ToInt32(x["productState"]).ToString("X6")});}catch(Exception e){rows.Add(new[]{"Windows Security","Unavailable",e.Message});}return rows;}
    public static string[] Battery(){
      var p=System.Windows.Forms.SystemInformation.PowerStatus;
      bool noBattery=(p.BatteryChargeStatus&System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)!=0;
      bool plugged=p.PowerLineStatus==System.Windows.Forms.PowerLineStatus.Online;
      if(noBattery)return new[]{"N/A (no battery)","100%","Plugged in","AC power - desktop / no battery"};
      int charge=(int)Math.Round(p.BatteryLifePercent*100);
      if(charge<0||charge>100)charge=plugged?100:0;
      string health="Not exposed by firmware";
      try{
        double full=0,design=0;
        foreach(ManagementObject x in new ManagementObjectSearcher("root\\wmi","SELECT FullChargedCapacity FROM BatteryFullChargedCapacity").Get()){full=Convert.ToDouble(x["FullChargedCapacity"]);break;}
        foreach(ManagementObject x in new ManagementObjectSearcher("root\\wmi","SELECT DesignedCapacity FROM BatteryStaticData").Get()){design=Convert.ToDouble(x["DesignedCapacity"]);break;}
        if(design>0)health=Math.Round(full/design*100)+"%";
      }catch{}
      // Desktop-like / firmware silent while on AC: treat as healthy plugged-in power
      if(health=="Not exposed by firmware"&&plugged)health="100%";
      string remaining;
      if(plugged)remaining="Plugged in - charging / AC power";
      else if(p.BatteryLifeRemaining>0)remaining=TimeSpan.FromSeconds(p.BatteryLifeRemaining).ToString(@"h\h\ m\m");
      else remaining="Calculating";
      return new[]{charge+"%",health,plugged?"Plugged in":"On battery",remaining};
    }
    public static List<string[]> Network(){return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().OrderBy(x=>x.State).Take(100).Select(x=>new[]{x.State.ToString(),x.LocalEndPoint.ToString(),x.RemoteEndPoint.ToString()}).ToList();}
    public static List<string[]> Startup(){var rows=new List<string[]>();ReadRun(Registry.CurrentUser,"Current user",rows);ReadRun(Registry.LocalMachine,"All users",rows);return rows;}
    static void ReadRun(RegistryKey root,string scope,List<string[]> rows){using(var key=root.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(key==null)return;foreach(string name in key.GetValueNames())rows.Add(new[]{name,scope,Convert.ToString(key.GetValue(name))});}}
    public static List<string[]> SystemInfo(){var rows=new List<string[]>();rows.Add(new[]{"Computer",Environment.MachineName});try{foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Caption,Version,LastBootUpTime FROM Win32_OperatingSystem").Get()){rows.Add(new[]{"Windows",Convert.ToString(x["Caption"])+" "+Convert.ToString(x["Version"])});DateTime boot=ManagementDateTimeConverter.ToDateTime(Convert.ToString(x["LastBootUpTime"]));rows.Add(new[]{"Uptime",(DateTime.Now-boot).ToString(@"d\d\ h\h\ m\m")});break;}}catch{rows.Add(new[]{"Windows",Environment.OSVersion.VersionString});}rows.Add(new[]{"Architecture",Environment.Is64BitOperatingSystem?"64-bit":"32-bit"});rows.Add(new[]{"Logical processors",Environment.ProcessorCount.ToString()});try{foreach(ManagementObject x in new ManagementObjectSearcher("SELECT Manufacturer,Model FROM Win32_ComputerSystem").Get()){rows.Add(new[]{"Manufacturer",Convert.ToString(x["Manufacturer"])});rows.Add(new[]{"Model",Convert.ToString(x["Model"])});break;}}catch{}return rows;}

    public static string MemoryTypeName(int code){
      switch(code){case 20:return "DDR";case 21:return "DDR2";case 24:return "DDR3";case 26:return "DDR4";case 27:return "LPDDR";case 28:return "LPDDR2";case 29:return "LPDDR3";case 30:return "LPDDR4";case 34:return "DDR5";case 35:return "LPDDR5";default:return code<=0?"Unknown":"Unknown ("+code+")";}
    }
    public static string FormFactorName(int code){switch(code){case 8:return "DIMM";case 12:return "SODIMM";default:return code<=0?"Unknown":"Other ("+code+")";}}
    static string TrimMem(object value){string s=Convert.ToString(value??"").Trim();if(String.IsNullOrEmpty(s)||s.Equals("Unknown",StringComparison.OrdinalIgnoreCase)||s.Equals("None",StringComparison.OrdinalIgnoreCase))return "—";return s;}
    static string ResolveMemoryType(int smb,int cim,string part){
      string fromSmb=MemoryTypeName(smb);if(!fromSmb.StartsWith("Unknown",StringComparison.Ordinal))return fromSmb;
      string fromCim=MemoryTypeName(cim);if(!fromCim.StartsWith("Unknown",StringComparison.Ordinal))return fromCim;
      string p=(part??"").ToUpperInvariant();
      if(p.Contains("DDR5")||p.StartsWith("F5-")||p.Contains("PC5-"))return "DDR5";
      if(p.Contains("DDR4")||p.StartsWith("F4-")||p.Contains("PC4-"))return "DDR4";
      if(p.Contains("DDR3")||p.StartsWith("F3-")||p.Contains("PC3-"))return "DDR3";
      if(p.Contains("LPDDR5"))return "LPDDR5";
      if(p.Contains("LPDDR4"))return "LPDDR4";
      return fromSmb;
    }
    public static string MemoryGenerationSummary(List<string[]> modules){
      var types=new List<string>();
      foreach(var row in modules){if(row==null||row.Length<4)continue;string t=row[3];if(String.IsNullOrEmpty(t)||t=="—"||t.StartsWith("Unknown",StringComparison.Ordinal))continue;if(!types.Contains(t))types.Add(t);}
      if(types.Count==0)return "Unknown (firmware did not report DDR3/4/5)";
      if(types.Count==1)return types[0];
      return String.Join(" + ",types);
    }
    public static List<string[]> MemoryModules(){
      var rows=new List<string[]>();
      try{
        foreach(ManagementObject x in new ManagementObjectSearcher("SELECT DeviceLocator,Manufacturer,Capacity,Speed,ConfiguredClockSpeed,PartNumber,SMBIOSMemoryType,MemoryType,FormFactor FROM Win32_PhysicalMemory").Get()){
          long cap=0;Int64.TryParse(Convert.ToString(x["Capacity"]),out cap);
          int smb=0,cim=0,form=0,cfg=0,spd=0;
          Int32.TryParse(Convert.ToString(x["SMBIOSMemoryType"]),out smb);
          Int32.TryParse(Convert.ToString(x["MemoryType"]),out cim);
          Int32.TryParse(Convert.ToString(x["FormFactor"]),out form);
          Int32.TryParse(Convert.ToString(x["ConfiguredClockSpeed"]),out cfg);
          Int32.TryParse(Convert.ToString(x["Speed"]),out spd);
          int mhz=cfg>0?cfg:spd;
          string part=TrimMem(x["PartNumber"]);
          string type=ResolveMemoryType(smb,cim,part=="—"?"":part);
          rows.Add(new[]{TrimMem(x["DeviceLocator"]),TrimMem(x["Manufacturer"]),Bytes(cap),type,mhz>0?mhz+" MHz":"—",FormFactorName(form),part});
        }
      }catch(Exception e){rows.Add(new[]{"Unavailable",e.Message,"—","—","—","—","—"});}
      return rows;
    }

    public static List<string[]> TempPreview(int ageDays){
      string userTemp=Path.GetTempPath(),windowsTemp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp");
      long userBytes,windowsBytes;int userFiles,windowsFiles;
      TempScanFolder(userTemp,ageDays,out userFiles,out userBytes);TempScanFolder(windowsTemp,ageDays,out windowsFiles,out windowsBytes);
      return new List<string[]>{
        new[]{userTemp,userFiles.ToString(),Bytes(userBytes),"Preview only"},
        new[]{windowsTemp,windowsFiles.ToString(),Bytes(windowsBytes),"Preview only - access permitting"},
        new[]{"Combined",(userFiles+windowsFiles).ToString(),Bytes(userBytes+windowsBytes),"Nothing deleted"}
      };
    }
    public static string[] TempClean(int ageDays){
      string userTemp=Path.GetTempPath(),windowsTemp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp");
      int deleted=0,skipped=0;long freed=0;
      TempDeleteFolder(userTemp,ageDays,ref deleted,ref skipped,ref freed);
      TempDeleteFolder(windowsTemp,ageDays,ref deleted,ref skipped,ref freed);
      return new[]{deleted.ToString(),Bytes(freed),skipped.ToString()};
    }
    static void TempScanFolder(string root,int ageDays,out int count,out long bytes){count=0;bytes=0;DateTime cutoff=DateTime.Now.AddDays(-ageDays);try{foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){try{var info=new FileInfo(file);if(info.LastWriteTime<cutoff){count++;bytes+=info.Length;}}catch{}}}catch{}}
    static void TempDeleteFolder(string root,int ageDays,ref int deleted,ref int skipped,ref long freed){DateTime cutoff=DateTime.Now.AddDays(-ageDays);try{foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){try{var info=new FileInfo(file);if(info.LastWriteTime>=cutoff)continue;long len=info.Length;info.Delete();deleted++;freed+=len;}catch{skipped++;}}}catch{}}

    public static List<string[]> OutlookAccounts(){
      var emails=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var rows=new List<string[]>();
      foreach(string ver in new[]{"16.0","15.0"}){
        CollectOfficeIdentityEmails(ver,emails);
        CollectOutlookProfileEmails(ver,emails);
      }
      CollectNewOutlookEmails(emails);
      if(emails.Count==0){rows.Add(new[]{"Unavailable","No local Outlook / Office identity found"});return rows;}
      foreach(string email in emails.OrderBy(x=>x))rows.Add(new[]{email,"Local identity"});
      return rows;
    }
    public static List<string[]> OutlookStores(){
      var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var rows=new List<string[]>();
      string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      string classic=Path.Combine(local,"Microsoft","Outlook");
      string docs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Outlook Files");
      AddOutlookGlob(classic,"*.ost",paths);AddOutlookGlob(classic,"*.pst",paths);AddOutlookGlob(docs,"*.pst",paths);
      foreach(string ver in new[]{"16.0","15.0"})CollectOutlookProfilePaths(ver,paths);
      long total=0;int count=0;
      foreach(string path in paths.OrderBy(x=>x)){
        try{var info=new FileInfo(path);if(!info.Exists)continue;count++;total+=info.Length;rows.Add(new[]{info.Name,Bytes(info.Length),info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),info.FullName});}catch{}
      }
      string olk=Path.Combine(local,"Microsoft","olk");
      if(Directory.Exists(olk)){long cache=DirBytes(olk);rows.Add(new[]{"New Outlook cache",Bytes(cache),Directory.GetLastWriteTime(olk).ToString("yyyy-MM-dd HH:mm"),olk});total+=cache;count++;}
      if(rows.Count==0){rows.Add(new[]{"Unavailable","No OST/PST or new Outlook cache found","—","—"});return rows;}
      rows.Add(new[]{"Total",Bytes(total),count+" item(s)","On-disk only (not server mailbox size)"});
      return rows;
    }
    static void AddOutlookGlob(string dir,string pattern,HashSet<string> paths){if(!Directory.Exists(dir))return;try{foreach(string f in Directory.EnumerateFiles(dir,pattern))paths.Add(f);}catch{}}
    static long DirBytes(string root){long total=0;try{foreach(string f in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){try{total+=new FileInfo(f).Length;}catch{}}}catch{}return total;}
    static void CollectOfficeIdentityEmails(string ver,HashSet<string> emails){
      try{using(var root=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Office\\"+ver+"\\Common\\Identity\\Identities")){if(root==null)return;foreach(string id in root.GetSubKeyNames()){using(var key=root.OpenSubKey(id)){if(key==null)continue;string email=Convert.ToString(key.GetValue("EmailAddress"));if(LooksLikeEmail(email))emails.Add(email.Trim());}}}}catch{}
    }
    static void CollectOutlookProfileEmails(string ver,HashSet<string> emails){WalkOutlookProfileValues(ver,new[]{"001f6641","001f6607","001f3001"},(name,text)=>{if(LooksLikeEmail(text))emails.Add(text.Trim());});}
    static void CollectOutlookProfilePaths(string ver,HashSet<string> paths){
      try{using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Office\\"+ver+"\\Outlook")){if(key!=null){foreach(string name in new[]{"ForceOSTPath","ForcePSTPath"}){string p=Environment.ExpandEnvironmentVariables(Convert.ToString(key.GetValue(name)??""));if(!String.IsNullOrWhiteSpace(p)){if(File.Exists(p))paths.Add(p);else if(Directory.Exists(p)){AddOutlookGlob(p,"*.ost",paths);AddOutlookGlob(p,"*.pst",paths);}}}}}}catch{}
      WalkOutlookProfileValues(ver,new[]{"001f6610","001f6700"},(name,text)=>{if(!String.IsNullOrWhiteSpace(text)&&File.Exists(text.Trim()))paths.Add(text.Trim());});
    }
    static void WalkOutlookProfileValues(string ver,string[] valueNames,Action<string,string> onValue){
      string[] roots={"Software\\Microsoft\\Office\\"+ver+"\\Outlook\\Profiles","Software\\Microsoft\\Windows NT\\CurrentVersion\\Windows Messaging Subsystem\\Profiles"};
      foreach(string rootPath in roots){try{using(var root=Registry.CurrentUser.OpenSubKey(rootPath)){if(root==null)continue;WalkRegistry(root,valueNames,onValue);}}catch{}}
    }
    static void WalkRegistry(RegistryKey key,string[] valueNames,Action<string,string> onValue){
      foreach(string name in key.GetValueNames()){if(Array.IndexOf(valueNames,name)<0)continue;object raw=key.GetValue(name);string text=DecodeOutlookBinary(raw);if(!String.IsNullOrWhiteSpace(text))onValue(name,text);}
      foreach(string sub in key.GetSubKeyNames()){try{using(var child=key.OpenSubKey(sub)){if(child!=null)WalkRegistry(child,valueNames,onValue);}}catch{}}
    }
    static string DecodeOutlookBinary(object raw){
      if(raw is string)return Convert.ToString(raw).Trim('\0').Trim();
      byte[] bytes=raw as byte[];if(bytes==null||bytes.Length<2)return "";
      try{return Encoding.Unicode.GetString(bytes).Trim('\0').Trim();}catch{return "";}
    }
    static void CollectNewOutlookEmails(HashSet<string> emails){
      string olk=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","olk");
      if(!Directory.Exists(olk))return;
      try{
        foreach(string file in Directory.EnumerateFiles(olk,"UserSettings.json",SearchOption.AllDirectories)){
          string json=File.ReadAllText(file);
          foreach(Match m in Regex.Matches(json,"\\\"(?:EmailAddress|email|PrimarySmtpAddress|preferred_username)\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",RegexOptions.IgnoreCase)){
            if(LooksLikeEmail(m.Groups[1].Value))emails.Add(m.Groups[1].Value.Trim());
          }
        }
      }catch{}
    }
    static bool LooksLikeEmail(string value){if(String.IsNullOrWhiteSpace(value))return false;value=value.Trim();return value.IndexOf('@')>0&&value.IndexOf('.')>value.IndexOf('@')&&value.Length<320&&value.IndexOf(' ')<0;}
  }

  static class App {
    const string Version="1.4.0";
    public static int Main(string[] args){try{if(args.Length==0&&Environment.UserInteractive&&!Console.IsInputRedirected){if(CheckForUpdate())return 0;Menu();return 0;}return Command(args);}catch(Exception e){Console.ForegroundColor=ConsoleColor.Red;Console.Error.WriteLine("SystemSage: "+e.Message);Console.ResetColor();return 1;}}
    static bool CheckForUpdate(){try{ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/cyrstrstn/systemsage/releases/latest");request.UserAgent="SystemSage/"+Version;request.Timeout=2500;request.ReadWriteTimeout=2500;string json;using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))json=reader.ReadToEnd();var match=Regex.Match(json,"\\\"tag_name\\\"\\s*:\\s*\\\"v?([^\\\"]+)\\\"");if(!match.Success)return false;Version latest;if(!System.Version.TryParse(match.Groups[1].Value,out latest))return false;Version current;if(!System.Version.TryParse(Version,out current)||latest<=current)return false;Ui.Header("Update available");Console.ForegroundColor=ConsoleColor.Green;Console.WriteLine(" SystemSage "+latest+" is available");Console.ResetColor();Console.WriteLine(" Installed version: "+Version+"\n");Console.Write(" Press U to update now, or any other key to skip: ");if(Console.ReadKey(true).Key!=ConsoleKey.U)return false;Console.WriteLine("\n Starting the one-click updater...");string url="https://raw.githubusercontent.com/cyrstrstn/systemsage/main/scripts/irm-install.ps1?cache="+Guid.NewGuid().ToString("N");string command="$ProgressPreference='SilentlyContinue'; irm '"+url+"' | iex";Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -Command \""+command+"\""){UseShellExecute=true});return true;}catch{return false;}}
    static bool HasFlag(string[] args,string flag){for(int i=1;i<args.Length;i++)if(String.Equals(args[i],flag,StringComparison.OrdinalIgnoreCase))return true;return false;}
    static int Command(string[] args){
      string c=args.Length==0?"doctor":args[0].ToLowerInvariant();
      if(c=="--help"||c=="help"){Help();return 0;}
      if(c=="--version"){Console.WriteLine(Version);return 0;}
      if(c=="json"||c=="export"){ShowJsonExport();return 0;}
      if(c=="doctor"||c=="overview"){Overview();return 0;}
      if(c=="memory"){ShowMemory(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="processes"){ShowProcesses();return 0;}
      if(c=="storage"){ShowStorage(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="temp"){return ShowTemp(HasFlag(args,"--clean"),HasFlag(args,"--yes"));}
      if(c=="outlook"){ShowOutlook(HasFlag(args,"--pdf"));return 0;}
      if(c=="gpu"){ShowGpu(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="diskhealth"||c=="disk"){ShowDiskHealth(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="wifi"){ShowWifi(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="boot"){ShowBoot(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="updates"){ShowUpdates(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="display"){ShowDisplay(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="audio"){ShowAudio(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="browsers"||c=="browser"){ShowBrowsers(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="security"){ShowSecurity();return 0;}
      if(c=="battery"){ShowBattery(HasFlag(args,"--pdf"),HasFlag(args,"--json"));return 0;}
      if(c=="network"){ShowNetwork();return 0;}
      if(c=="startup"){ShowStartup();return 0;}
      if(c=="system"){ShowSystem();return 0;}
      if(c=="report"){Report();return 0;}
      Console.Error.WriteLine("Unknown command: "+c+". Run systemsage --help.");return 2;
    }
    static void Menu(){
      int selected=0;
      string[] labels={"Complete PC health overview","Memory modules","GPU","Disk health","Storage usage","Wi-Fi","Boot time","Windows Update","Display","Audio devices","Browser cache","Temporary files","Outlook accounts & storage","Top processes","Security status","Battery health","Network connections","Startup programs","System details","Export JSON","Generate full PDF report","Exit"};
      while(true){
        Ui.Header("Diagnostic menu");
        for(int i=0;i<labels.Length;i++){if(i==selected){Console.BackgroundColor=ConsoleColor.DarkGreen;Console.ForegroundColor=ConsoleColor.White;Console.WriteLine(" > "+labels[i].PadRight(36));Console.ResetColor();}else Console.WriteLine("   "+labels[i]);}
        Console.ForegroundColor=ConsoleColor.DarkGray;Console.WriteLine("\n Arrow keys navigate  |  Enter selects  |  Q exits");Console.ResetColor();
        var k=Console.ReadKey(true);
        if(k.Key==ConsoleKey.UpArrow)selected=(selected+labels.Length-1)%labels.Length;
        else if(k.Key==ConsoleKey.DownArrow)selected=(selected+1)%labels.Length;
        else if(k.Key==ConsoleKey.Q||k.Key==ConsoleKey.Escape)return;
        else if(k.Key==ConsoleKey.Enter){if(selected==labels.Length-1)return;RunIndex(selected);Ui.Pause();}
      }
    }
    static void RunIndex(int i){
      if(i==0)Overview();else if(i==1)ShowMemory(false,false);else if(i==2)ShowGpu(false,false);else if(i==3)ShowDiskHealth(false,false);
      else if(i==4)ShowStorage(false,false);else if(i==5)ShowWifi(false,false);else if(i==6)ShowBoot(false,false);else if(i==7)ShowUpdates(false,false);
      else if(i==8)ShowDisplay(false,false);else if(i==9)ShowAudio(false,false);else if(i==10)ShowBrowsers(false,false);else if(i==11)ShowTemp(false,false);
      else if(i==12)ShowOutlook(false);else if(i==13)ShowProcesses();else if(i==14)ShowSecurity();else if(i==15)ShowBattery(false,false);
      else if(i==16)ShowNetwork();else if(i==17)ShowStartup();else if(i==18)ShowSystem();else if(i==19)ShowJsonExport();else Report();
    }
    static void Overview(){Ui.Header("Complete PC health overview");var report=FullDiagnostic.Collect(Ui.Progress);Ui.Progress(97,"Building readable PDF report");string pdf=StyledPdfReport.Write(report);Ui.Progress(100,"Diagnostic complete");FullDiagnostic.Print(report);Console.ForegroundColor=ConsoleColor.Green;Console.WriteLine("\n PDF report ready");Console.ResetColor();Console.WriteLine(" "+pdf);if(!Console.IsInputRedirected&&!Console.IsOutputRedirected){Console.Write("\n Press V to view the PDF, or any other key to continue: ");if(Console.ReadKey(true).Key==ConsoleKey.V)TryOpen(pdf);}}
    static void ShowMemory(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("memory",new[]{"Slot","Vendor","Capacity","Type","Speed","Form","Part number"},Scan.MemoryModules()));return;}
      Ui.Header("Memory modules");
      long[] mem=Ui.Loading("Measuring live memory usage",Scan.Memory);
      var modules=Ui.Loading("Reading RAM modules",Scan.MemoryModules);
      double pct=mem[1]>0?Math.Round((double)mem[0]/mem[1]*100):0;
      string generation=Scan.MemoryGenerationSummary(modules);
      Ui.Pair("Memory usage",pct+"% - "+Scan.Bytes(mem[0])+" / "+Scan.Bytes(mem[1]));
      Ui.Pair("Memory type",generation);
      Ui.Pair("Installed modules",modules.Count.ToString());
      Console.WriteLine();
      Ui.Table(new[]{"Slot","Vendor","Capacity","Type","Speed","Form","Part number"},modules);
      if(pdf||OfferPdf())WriteSectionPdf("Memory",new[]{"Metric","Result"},new List<string[]>{new[]{"Memory usage",pct+"% - "+Scan.Bytes(mem[0])+" / "+Scan.Bytes(mem[1])},new[]{"Memory type",generation},new[]{"Installed modules",modules.Count.ToString()}},new[]{"Slot","Vendor","Capacity","Type","Speed","Form","Part number"},modules);
    }
    static void ShowProcesses(){Ui.Header("Top processes");Ui.Table(new[]{"Process","PID","Memory","Threads"},Ui.Loading("Reading running processes",()=>Scan.Processes(30)));}
    static void ShowStorage(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("storage",new[]{"Drive","Label","Format","Used","Available","Usage"},Scan.Drives()));return;}
      Ui.Header("Storage usage");
      var rows=Ui.Loading("Reading connected drives",Scan.Drives);
      Ui.Table(new[]{"Drive","Label","Format","Used","Available","Usage"},rows);
      if(pdf||OfferPdf())WriteSectionPdf("Storage",new[]{"Metric","Result"},new List<string[]>{new[]{"Volumes",rows.Count.ToString()}},new[]{"Drive","Label","Format","Used","Available","Usage"},rows);
    }
    static int ShowTemp(bool clean,bool yes){
      Ui.Header("Temporary files");
      var preview=Ui.Loading("Scanning temporary files older than 7 days",()=>Scan.TempPreview(7));
      Ui.Table(new[]{"Location","Files","Recoverable","Action"},preview);
      if(!clean){
        if(!Console.IsInputRedirected&&!Console.IsOutputRedirected){
          Console.Write("\n Press C to clean files older than 7 days, or any other key to skip: ");
          if(Console.ReadKey(true).Key!=ConsoleKey.C){Console.WriteLine();return 0;}
          Console.Write("\n Type YES to permanently delete previewed temp files: ");
          string confirm=Console.ReadLine()??"";
          if(!String.Equals(confirm.Trim(),"YES",StringComparison.Ordinal)){Console.WriteLine(" Cleanup cancelled.");return 0;}
          clean=true;yes=true;
        }else return 0;
      }
      if(clean&&!yes){Console.Error.WriteLine("Refusing cleanup without --yes (non-interactive safety).");return 2;}
      if(!clean)return 0;
      var result=Ui.Loading("Deleting unlocked temp files older than 7 days",()=>Scan.TempClean(7));
      Ui.Pair("Files deleted",result[0]);Ui.Pair("Space freed",result[1]);Ui.Pair("Skipped (locked/denied)",result[2]);
      return 0;
    }
    static void ShowOutlook(bool pdf){
      Ui.Header("Outlook accounts & storage");
      var accounts=Ui.Loading("Reading local Outlook identities",Scan.OutlookAccounts);
      var stores=Ui.Loading("Measuring Outlook on-disk storage",Scan.OutlookStores);
      Console.ForegroundColor=ConsoleColor.White;Console.WriteLine("\n ACCOUNTS");Console.ResetColor();
      Ui.Table(new[]{"Account","Source"},accounts);
      Console.ForegroundColor=ConsoleColor.White;Console.WriteLine("\n ON-DISK STORAGE");Console.ResetColor();
      Ui.Table(new[]{"Name","Size","Modified","Path"},stores);
      Console.ForegroundColor=ConsoleColor.DarkGray;Console.WriteLine("\n Sizes are local cache/OST/PST only — not Microsoft 365 mailbox quota.");Console.ResetColor();
      if(pdf||OfferPdf())WriteSectionPdf("Outlook",new[]{"Account","Source"},accounts,new[]{"Name","Size","Modified","Path"},stores);
    }
    static void ShowGpu(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("gpu",new[]{"Adapter","VRAM","Driver","Driver date","Status","Temp"},Scan.Gpu()));return;}
      Ui.Header("GPU");
      var rows=Ui.Loading("Reading graphics adapters",Scan.Gpu);
      Ui.Table(new[]{"Adapter","VRAM","Driver","Driver date","Status","Temp"},rows);
      Console.ForegroundColor=ConsoleColor.DarkGray;Console.WriteLine("\n GPU temperature is not exposed by stock Windows WMI (no extra drivers).");Console.ResetColor();
      if(pdf||OfferPdf())WriteSectionPdf("GPU",new[]{"Metric","Result"},new List<string[]>{new[]{"Adapters",rows.Count.ToString()}},new[]{"Adapter","VRAM","Driver","Driver date","Status","Temp"},rows);
    }
    static void ShowDiskHealth(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("diskhealth",new[]{"Model","Interface","Size","Status","SMART"},Scan.DiskHealth()));return;}
      Ui.Header("Disk health");
      var rows=Ui.Loading("Reading disk health signals",Scan.DiskHealth);
      Ui.Table(new[]{"Model","Interface","Size","Status","SMART"},rows);
      if(pdf||OfferPdf())WriteSectionPdf("Disk health",new[]{"Metric","Result"},new List<string[]>{new[]{"Disks",rows.Count.ToString()}},new[]{"Model","Interface","Size","Status","SMART"},rows);
    }
    static void ShowWifi(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("wifi",new[]{"Adapter","SSID","State","Signal","Rate"},Scan.Wifi()));return;}
      Ui.Header("Wi-Fi");
      var rows=Ui.Loading("Reading wireless interfaces",Scan.Wifi);
      Ui.Table(new[]{"Adapter","SSID","State","Signal","Rate"},rows);
      if(pdf||OfferPdf())WriteSectionPdf("Wi-Fi",new[]{"Metric","Result"},new List<string[]>{new[]{"Interfaces",rows.Count.ToString()}},new[]{"Adapter","SSID","State","Signal","Rate"},rows);
    }
    static void ShowBoot(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("boot",new[]{"Property","Value"},Scan.BootInfo()));return;}
      Ui.Header("Boot time");
      var rows=Ui.Loading("Reading boot timing",Scan.BootInfo);
      var startup=Ui.Loading("Reading startup entries",Scan.Startup);
      Ui.Table(new[]{"Property","Value"},rows);
      Console.ForegroundColor=ConsoleColor.White;Console.WriteLine("\n STARTUP PROGRAMS");Console.ResetColor();
      Ui.Table(new[]{"Name","Scope","Command"},startup);
      if(pdf||OfferPdf())WriteSectionPdf("Boot",new[]{"Property","Value"},rows,new[]{"Name","Scope","Command"},startup);
    }
    static void ShowUpdates(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("updates",new[]{"Property","Value"},Scan.WindowsUpdates()));return;}
      Ui.Header("Windows Update");
      var rows=Ui.Loading("Checking Windows Update status",Scan.WindowsUpdates);
      Ui.Table(new[]{"Property","Value"},rows);
      if(pdf||OfferPdf())WriteSectionPdf("Windows Update",new[]{"Metric","Result"},new List<string[]>{new[]{"Rows",rows.Count.ToString()}},new[]{"Property","Value"},rows);
    }
    static void ShowDisplay(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("display",new[]{"Display","Resolution","Color","Device","Note"},Scan.Displays()));return;}
      Ui.Header("Display");
      var rows=Ui.Loading("Reading displays",Scan.Displays);
      Ui.Table(new[]{"Display","Resolution","Color","Device","Note"},rows);
      if(pdf||OfferPdf())WriteSectionPdf("Display",new[]{"Metric","Result"},new List<string[]>{new[]{"Entries",rows.Count.ToString()}},new[]{"Display","Resolution","Color","Device","Note"},rows);
    }
    static void ShowAudio(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("audio",new[]{"Name","Manufacturer","Status","PNP"},Scan.AudioDevices()));return;}
      Ui.Header("Audio devices");
      var rows=Ui.Loading("Reading sound devices",Scan.AudioDevices);
      Ui.Table(new[]{"Name","Manufacturer","Status","PNP"},rows);
      if(pdf||OfferPdf())WriteSectionPdf("Audio",new[]{"Metric","Result"},new List<string[]>{new[]{"Devices",rows.Count.ToString()}},new[]{"Name","Manufacturer","Status","PNP"},rows);
    }
    static void ShowBrowsers(bool pdf,bool json){
      if(json){Console.WriteLine(Scan.ToJson("browsers",new[]{"Browser","Size","Files","Action"},Scan.BrowserCaches()));return;}
      Ui.Header("Browser cache");
      var rows=Ui.Loading("Measuring browser cache folders",Scan.BrowserCaches);
      Ui.Table(new[]{"Browser","Size","Files","Action"},rows);
      Console.ForegroundColor=ConsoleColor.DarkGray;Console.WriteLine("\n Preview only — SystemSage does not delete browser cache in this version.");Console.ResetColor();
      if(pdf||OfferPdf())WriteSectionPdf("Browser cache",new[]{"Metric","Result"},new List<string[]>{new[]{"Locations",rows.Count.ToString()}},new[]{"Browser","Size","Files","Action"},rows);
    }
    static void ShowJsonExport(){
      Ui.Header("Export JSON");
      string json=Ui.Loading("Collecting diagnostic JSON",Scan.ExportJsonBundle);
      string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SystemSage Reports");
      Directory.CreateDirectory(dir);
      string path=Path.Combine(dir,"SystemSage-Export-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json");
      File.WriteAllText(path,json,Encoding.UTF8);
      Console.ForegroundColor=ConsoleColor.Green;Console.WriteLine(" [READY] JSON export saved");Console.ResetColor();
      Console.WriteLine(" "+path);
    }
    static void ShowSecurity(){Ui.Header("Security status");Ui.Table(new[]{"Provider","Registration","State"},Ui.Loading("Checking Windows Security Center",Scan.Security));}
    static void ShowBattery(bool pdf,bool json){
      var b=Scan.Battery();
      var rows=new List<string[]>{new[]{"Current charge",b[0]},new[]{"Estimated health",b[1]},new[]{"Power state",b[2]},new[]{"Time remaining",b[3]}};
      if(json){Console.WriteLine(Scan.ToJson("battery",new[]{"Metric","Result"},rows));return;}
      Ui.Header("Battery health");
      b=Ui.Loading("Reading battery telemetry",Scan.Battery);
      rows=new List<string[]>{new[]{"Current charge",b[0]},new[]{"Estimated health",b[1]},new[]{"Power state",b[2]},new[]{"Time remaining",b[3]}};
      foreach(var row in rows)Ui.Pair(row[0],row[1]);
      if(pdf||OfferPdf())WriteSectionPdf("Battery",new[]{"Metric","Result"},rows,new[]{"Metric","Result"},rows);
    }
    static void ShowNetwork(){Ui.Header("Network connections");Ui.Table(new[]{"State","Local endpoint","Remote endpoint"},Ui.Loading("Reading active TCP connections",Scan.Network));}
    static void ShowStartup(){Ui.Header("Startup programs");Ui.Table(new[]{"Name","Scope","Command"},Ui.Loading("Reading startup entries",Scan.Startup));}
    static void ShowSystem(){Ui.Header("System details");Ui.Table(new[]{"Property","Value"},Ui.Loading("Reading device identity",Scan.SystemInfo));}
    static void Report(){Ui.Header("Generate full PDF report");var report=FullDiagnostic.Collect(Ui.Progress);Ui.Progress(97,"Building readable PDF report");string path=StyledPdfReport.Write(report);Ui.Progress(100,"Report complete");Console.ForegroundColor=ConsoleColor.Green;Console.WriteLine(" [READY] PDF report saved");Console.ResetColor();Console.WriteLine(" "+path);if(!Console.IsInputRedirected&&!Console.IsOutputRedirected){Console.Write("\n Press V to view the PDF, or any other key to continue: ");if(Console.ReadKey(true).Key==ConsoleKey.V)TryOpen(path);}}
    static bool OfferPdf(){if(Console.IsInputRedirected||Console.IsOutputRedirected)return false;Console.Write("\n Press P to save a PDF for this check, or any other key to continue: ");return Console.ReadKey(true).Key==ConsoleKey.P;}
    static void WriteSectionPdf(string title,string[] cols1,List<string[]> rows1,string[] cols2,List<string[]> rows2){
      var report=new DiagnosticReport();
      report.Sections.Add(new ReportSection{Title=title+" summary",Columns=cols1,Rows=rows1});
      report.Sections.Add(new ReportSection{Title=title+" details",Columns=cols2,Rows=rows2});
      string path=PdfReport.Write(report);
      Console.ForegroundColor=ConsoleColor.Green;Console.WriteLine("\n [READY] PDF saved");Console.ResetColor();Console.WriteLine(" "+path);
      if(!Console.IsInputRedirected&&!Console.IsOutputRedirected){Console.Write("\n Press V to view the PDF, or any other key to continue: ");if(Console.ReadKey(true).Key==ConsoleKey.V)TryOpen(path);}
    }
    static void TryOpen(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){Console.WriteLine(" Could not open PDF: "+e.Message);}}
    static void Help(){Console.WriteLine("SystemSage "+Version+" - lightweight Windows diagnostics\n\nUsage:\n  systemsage                 Interactive menu\n  systemsage doctor          Complete PC overview and PDF\n  systemsage memory [--pdf] [--json]\n  systemsage gpu [--pdf] [--json]\n  systemsage diskhealth [--pdf] [--json]\n  systemsage storage [--pdf] [--json]\n  systemsage wifi [--pdf] [--json]\n  systemsage boot [--pdf] [--json]\n  systemsage updates [--pdf] [--json]\n  systemsage display [--pdf] [--json]\n  systemsage audio [--pdf] [--json]\n  systemsage browsers [--pdf] [--json]\n  systemsage temp            Temp preview (older than 7 days)\n  systemsage temp --clean --yes\n  systemsage outlook [--pdf]\n  systemsage processes|security|battery|network|startup|system\n  systemsage json            Export multi-section JSON report\n  systemsage report          Full PDF report\n  systemsage --version");}
  }
}


