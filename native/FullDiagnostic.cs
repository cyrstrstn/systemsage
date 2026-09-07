using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;

namespace SystemSage {
  sealed class ReportSection { public string Title; public string[] Columns; public List<string[]> Rows=new List<string[]>(); }
  sealed class DiagnosticReport { public DateTime Created=DateTime.Now; public List<ReportSection> Sections=new List<ReportSection>(); public int WarningCount; public int ErrorCount; }

  static class FullDiagnostic {
    public static DiagnosticReport Collect() {
      var report=new DiagnosticReport();
      Add(report,"System",new[]{"Property","Value"},Scan.SystemInfo());
      long[] memory=Scan.Memory();int cpu=Scan.Cpu();string[] battery=Scan.Battery();
      Add(report,"Health summary",new[]{"Metric","Result"},new List<string[]>{new[]{"CPU usage",cpu+"%"},new[]{"Memory usage",(memory[1]>0?Math.Round((double)memory[0]/memory[1]*100):0)+"% - "+Scan.Bytes(memory[0])+" / "+Scan.Bytes(memory[1])},new[]{"Battery charge",battery[0]},new[]{"Battery health",battery[1]},new[]{"Power state",battery[2]}});
      Add(report,"Physical memory",new[]{"Slot","Manufacturer","Capacity","Speed","Part number"},Wmi("SELECT DeviceLocator,Manufacturer,Capacity,Speed,PartNumber FROM Win32_PhysicalMemory",new[]{"DeviceLocator","Manufacturer","Capacity","Speed","PartNumber"},(row,key)=>key=="Capacity"?Scan.Bytes(ToLong(row[key])):Clean(row[key])));
      Add(report,"Storage volumes",new[]{"Drive","Label","Format","Used","Available","Usage"},Scan.Drives());
      Add(report,"Physical disks",new[]{"Model","Interface","Size","Status"},Wmi("SELECT Model,InterfaceType,Size,Status FROM Win32_DiskDrive",new[]{"Model","InterfaceType","Size","Status"},(row,key)=>key=="Size"?Scan.Bytes(ToLong(row[key])):Clean(row[key])));
      Add(report,"Graphics",new[]{"Adapter","Memory","Driver"},Wmi("SELECT Name,AdapterRAM,DriverVersion FROM Win32_VideoController",new[]{"Name","AdapterRAM","DriverVersion"},(row,key)=>key=="AdapterRAM"?Scan.Bytes(ToLong(row[key])):Clean(row[key])));
      Add(report,"Battery",new[]{"Metric","Result"},new List<string[]>{new[]{"Current charge",battery[0]},new[]{"Estimated capacity health",battery[1]},new[]{"Power state",battery[2]},new[]{"Estimated remaining",battery[3]}});
      Add(report,"Security providers",new[]{"Provider","Registration","State"},Scan.Security());
      var devices=Wmi("SELECT Name,PNPClass,ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0",new[]{"Name","PNPClass","ConfigManagerErrorCode"},(row,key)=>Clean(row[key]));Add(report,"Devices requiring attention",new[]{"Device","Class","Error code"},devices);report.WarningCount+=devices.Count;
      var events=RecentErrors();Add(report,"Recent Windows warnings and errors - 7 days",new[]{"Time","Log","Source","Event ID","Message"},events);report.ErrorCount+=events.Count;
      Add(report,"Top processes by memory",new[]{"Process","PID","Memory","Threads"},Scan.Processes(10));
      Add(report,"Startup programs",new[]{"Name","Scope","Command"},Scan.Startup());
      string userTemp=Path.GetTempPath(),windowsTemp=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp");long userBytes,windowsBytes;int userFiles,windowsFiles;ScanFolder(userTemp,7,out userFiles,out userBytes);ScanFolder(windowsTemp,7,out windowsFiles,out windowsBytes);Add(report,"Temporary file preview",new[]{"Location","Files older than 7 days","Recoverable size","Action"},new List<string[]>{new[]{userTemp,userFiles.ToString(),Scan.Bytes(userBytes),"Preview only - nothing deleted"},new[]{windowsTemp,windowsFiles.ToString(),Scan.Bytes(windowsBytes),"Preview only - access permitting"},new[]{"Combined",(userFiles+windowsFiles).ToString(),Scan.Bytes(userBytes+windowsBytes),"No files deleted"}});
      Add(report,"10 largest files in user profile",new[]{"File","Size","Modified"},LargestFiles(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),10));
      return report;
    }
    static void Add(DiagnosticReport r,string title,string[] columns,List<string[]> rows){r.Sections.Add(new ReportSection{Title=title,Columns=columns,Rows=rows});}
    static long ToLong(object value){long n;return Int64.TryParse(Convert.ToString(value),out n)?n:0;}
    static string Clean(object value){return Convert.ToString(value??"").Replace('\r',' ').Replace('\n',' ').Trim();}
    delegate string WmiFormat(ManagementObject row,string key);
    static List<string[]> Wmi(string query,string[] fields,WmiFormat format){var rows=new List<string[]>();try{foreach(ManagementObject item in new ManagementObjectSearcher(query).Get()){var row=new string[fields.Length];for(int i=0;i<fields.Length;i++)row[i]=format(item,fields[i]);rows.Add(row);}}catch(Exception e){rows.Add(new[]{"Unavailable",e.Message});}return rows;}
    static List<string[]> RecentErrors(){var rows=new List<string[]>();DateTime since=DateTime.Now.AddDays(-7);foreach(string logName in new[]{"System","Application"}){try{using(var log=new EventLog(logName)){foreach(EventLogEntry e in log.Entries.Cast<EventLogEntry>().Reverse().Where(x=>x.TimeGenerated>=since&&(x.EntryType==EventLogEntryType.Error||x.EntryType==EventLogEntryType.Warning)).Take(15))rows.Add(new[]{e.TimeGenerated.ToString("yyyy-MM-dd HH:mm"),logName,e.Source,e.InstanceId.ToString(),Clean(e.Message)});}}catch(Exception e){rows.Add(new[]{"Unavailable",logName,"SystemSage","-",e.Message});}}return rows.OrderByDescending(x=>x[0]).Take(25).ToList();}
    static void ScanFolder(string root,int ageDays,out int count,out long bytes){count=0;bytes=0;DateTime cutoff=DateTime.Now.AddDays(-ageDays);try{foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)){try{var info=new FileInfo(file);if(info.LastWriteTime<cutoff){count++;bytes+=info.Length;}}catch{}}}catch{}}
    static List<string[]> LargestFiles(string root,int limit){var files=new List<FileInfo>();var pending=new Stack<string>();pending.Push(root);int visited=0;while(pending.Count>0&&visited<250000){string dir=pending.Pop();try{foreach(string file in Directory.EnumerateFiles(dir)){visited++;try{var info=new FileInfo(file);files.Add(info);if(files.Count>limit*20)files=files.OrderByDescending(x=>x.Length).Take(limit*4).ToList();}catch{}}foreach(string child in Directory.EnumerateDirectories(dir)){try{var a=File.GetAttributes(child);if((a&FileAttributes.ReparsePoint)==0)pending.Push(child);}catch{}}}catch{}}return files.OrderByDescending(x=>x.Length).Take(limit).Select(x=>new[]{x.FullName,Scan.Bytes(x.Length),x.LastWriteTime.ToString("yyyy-MM-dd HH:mm")}).ToList();}

    public static void Print(DiagnosticReport report){Ui.Header("Complete PC health overview");Ui.Pair("Generated",report.Created.ToString("yyyy-MM-dd HH:mm:ss"));Ui.Pair("Recent warning/error events",report.ErrorCount.ToString());Ui.Pair("Device warnings",report.WarningCount.ToString());foreach(var section in report.Sections){Console.ForegroundColor=ConsoleColor.White;Console.WriteLine("\n "+section.Title.ToUpperInvariant());Console.ResetColor();if(section.Rows.Count==0)Console.WriteLine(" No issues found.");else Ui.Table(section.Columns,section.Rows);}}
  }

  static class PdfReport {
    public static string Write(DiagnosticReport report){string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SystemSage Reports");Directory.CreateDirectory(dir);string path=Path.Combine(dir,"SystemSage-PC-Health-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".pdf");var lines=new List<string>();lines.Add("SYSTEMSAGE - COMPLETE PC HEALTH REPORT");lines.Add("Generated: "+report.Created.ToString("yyyy-MM-dd HH:mm:ss"));lines.Add("Windows errors: "+report.ErrorCount+"    Device warnings: "+report.WarningCount);lines.Add("");foreach(var section in report.Sections){lines.Add(section.Title.ToUpperInvariant());lines.Add(new string('-',Math.Min(92,section.Title.Length)));if(section.Rows.Count==0)lines.Add("No issues found.");foreach(var row in section.Rows){string text=String.Join(" | ",row.Select(Ascii));foreach(string wrapped in Wrap(text,105))lines.Add(wrapped);}lines.Add("");}BuildPdf(path,lines);return path;}
    static string Ascii(string value){var b=new StringBuilder();foreach(char c in value??"")b.Append(c>=32&&c<=126?c:'?');return b.ToString();}
    static IEnumerable<string> Wrap(string text,int width){while(text.Length>width){int cut=text.LastIndexOf(' ',width);if(cut<1)cut=width;yield return text.Substring(0,cut);text=text.Substring(cut).TrimStart();}yield return text;}
    static string Escape(string s){return s.Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");}
    static void BuildPdf(string path,List<string> lines){const int perPage=56;int pages=(int)Math.Ceiling(lines.Count/(double)perPage);var objects=new List<byte[]>();objects.Add(Bytes("<< /Type /Catalog /Pages 2 0 R >>"));var kids=String.Join(" ",Enumerable.Range(0,pages).Select(i=>(3+i*2)+" 0 R"));objects.Add(Bytes("<< /Type /Pages /Kids ["+kids+"] /Count "+pages+" >>"));int fontId=3+pages*2;for(int page=0;page<pages;page++){int pageId=3+page*2,contentId=pageId+1;objects.Add(Bytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 "+fontId+" 0 R >> >> /Contents "+contentId+" 0 R >>"));var content=new StringBuilder("BT /F1 9 Tf 42 752 Td 12 TL ");foreach(string line in lines.Skip(page*perPage).Take(perPage))content.Append("("+Escape(line)+") Tj T* ");content.Append("ET");byte[] stream=Bytes(content.ToString());objects.Add(Bytes("<< /Length "+stream.Length+" >>\nstream\n"+content+"\nendstream"));}objects.Add(Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"));using(var output=new FileStream(path,FileMode.Create,FileAccess.Write)){Write(output,Bytes("%PDF-1.4\n"));var offsets=new List<long>{0};for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);Write(output,Bytes((i+1)+" 0 obj\n"));Write(output,objects[i]);Write(output,Bytes("\nendobj\n"));}long xref=output.Position;Write(output,Bytes("xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n"));for(int i=1;i<offsets.Count;i++)Write(output,Bytes(offsets[i].ToString("0000000000")+" 00000 n \n"));Write(output,Bytes("trailer << /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF"));}}
    static byte[] Bytes(string value){return Encoding.ASCII.GetBytes(value);}static void Write(Stream s,byte[] b){s.Write(b,0,b.Length);}
  }
}
