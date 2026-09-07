using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SystemSage {
  static class StyledPdfReport {
    sealed class Block {
      public string Text; public int Kind; public int Height;
      public Block(string text,int kind,int height){Text=text;Kind=kind;Height=height;}
    }

    public static string Write(DiagnosticReport report){
      string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SystemSage Reports");
      Directory.CreateDirectory(dir);
      string path=Path.Combine(dir,"SystemSage-PC-Health-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".pdf");
      var blocks=new List<Block>();
      foreach(var section in report.Sections){
        blocks.Add(new Block(section.Title.ToUpperInvariant(),1,32));
        if(section.Rows.Count==0)blocks.Add(new Block("No issues found.",0,18));
        foreach(var row in section.Rows){
          foreach(string line in Wrap(FormatRow(section.Columns,row),88))blocks.Add(new Block(line,0,15));
          blocks.Add(new Block("",3,7));
        }
      }
      Build(path,report,blocks);return path;
    }

    static ReportSection Section(DiagnosticReport report,string title){return report.Sections.FirstOrDefault(x=>x.Title==title);}
    static string Result(DiagnosticReport report,string section,string name){var s=Section(report,section);if(s==null)return "Unavailable";var row=s.Rows.FirstOrDefault(x=>x.Length>1&&x[0]==name);return row==null?"Unavailable":row[1];}
    static int Percent(string value){var m=Regex.Match(value??"","(\\d{1,3})%");int n;return m.Success&&Int32.TryParse(m.Groups[1].Value,out n)?Math.Max(0,Math.Min(100,n)):0;}
    static int DiskPercent(DiagnosticReport report){var s=Section(report,"Storage volumes");if(s==null||s.Rows.Count==0||s.Rows[0].Length<6)return 0;return Percent(s.Rows[0][5]);}
    static int Score(DiagnosticReport report,int cpu,int memory,int disk,int battery){int score=100;if(memory>=90)score-=18;else if(memory>=80)score-=10;if(disk>=90)score-=20;else if(disk>=80)score-=10;if(battery>0&&battery<60)score-=18;else if(battery>0&&battery<80)score-=9;if(cpu>=90)score-=10;else if(cpu>=75)score-=5;score-=Math.Min(15,report.WarningCount*5);score-=Math.Min(10,report.ErrorCount/5);return Math.Max(0,score);}
    static string Status(int score){return score>=90?"HEALTHY":score>=70?"GOOD - MINOR ATTENTION":"ACTION RECOMMENDED";}
    static List<string> Recommendations(DiagnosticReport report,int cpu,int memory,int disk,int battery){var result=new List<string>();if(memory>=80)result.Add("Memory use is high. Close unused apps and review the top-process list.");if(disk>=80)result.Add("Storage is filling up. Review large files before removing anything.");if(battery>0&&battery<80)result.Add("Battery capacity has declined. Monitor runtime and plan service if it worsens.");if(report.WarningCount>0)result.Add(report.WarningCount+" device issue(s) need attention in Device Manager.");if(report.ErrorCount>0)result.Add("Review recurring Windows events; repeated sources matter more than one-off entries.");if(cpu>=75)result.Add("CPU use was elevated during the scan. Recheck when the PC is idle.");if(result.Count==0)result.Add("No urgent action found. Keep Windows and security definitions updated.");return result.Take(4).ToList();}

    static string Dashboard(DiagnosticReport report,int pageCount){
      int cpu=Percent(Result(report,"Health summary","CPU usage"));
      int memory=Percent(Result(report,"Health summary","Memory usage"));
      int battery=Percent(Result(report,"Health summary","Battery health"));
      int disk=DiskPercent(report),score=Score(report,cpu,memory,disk,battery);
      var c=new StringBuilder();Header(c,"PC HEALTH OVERVIEW",false);
      Text(c,"/F1",9,"0.38 0.41 0.36",36,688,"Generated "+report.Created.ToString("dd MMM yyyy, HH:mm")+"  |  "+Environment.MachineName);
      // Overall score panel.
      Fill(c,"0.95 0.97 0.93",30,548,552,112);Fill(c,score>=85?"0.73 1 0.27":score>=65?"1 0.75 0.20":"1 0.35 0.25",30,548,8,112);
      Text(c,"/F2",12,"0.32 0.35 0.30",55,625,"OVERALL HEALTH");Text(c,"/F2",44,"0.08 0.09 0.08",54,570,score.ToString());Text(c,"/F1",12,"0.32 0.35 0.30",122,574,"/ 100");
      Text(c,"/F2",20,"0.08 0.09 0.08",250,610,Status(score));Text(c,"/F1",10,"0.32 0.35 0.30",250,588,report.WarningCount+" device warnings  |  "+report.ErrorCount+" recent Windows events");
      // Metric cards.
      Metric(c,30,420,"CPU LOAD",cpu,cpu>=75?"High":"Normal",cpu>=75);Metric(c,312,420,"MEMORY",memory,memory>=80?"High":"Normal",memory>=80);
      Metric(c,30,304,"STORAGE",disk,disk>=80?"Running low":"Available",disk>=80);Metric(c,312,304,"BATTERY HEALTH",battery,battery>0&&battery<80?"Worn":"Good",battery>0&&battery<80);
      Text(c,"/F2",12,"0.08 0.09 0.08",30,264,"WHAT THIS MEANS");
      string summary=score>=85?"Your PC looks healthy. No urgent hardware or capacity problem was detected.":score>=65?"Your PC is usable, but one or more areas would benefit from attention.":"Several signals need attention. Start with the actions below before they worsen.";
      int sy=244;foreach(string line in Wrap(summary,82)){Text(c,"/F1",10,"0.28 0.30 0.27",30,sy,line);sy-=15;}
      Text(c,"/F2",12,"0.08 0.09 0.08",30,198,"RECOMMENDED NEXT STEPS");int y=176,n=1;foreach(string item in Recommendations(report,cpu,memory,disk,battery)){Fill(c,"0.73 1 0.27",30,y-5,18,18);Text(c,"/F2",8,"0.08 0.09 0.08",36,y,n.ToString());int ty=y;foreach(string line in Wrap(item,78)){Text(c,"/F1",9,"0.22 0.24 0.21",58,ty,line);ty-=13;}y=Math.Min(y-35,ty-14);n++;}
      Footer(c,1,pageCount,"SUMMARY - TECHNICAL DETAILS FOLLOW");return c.ToString();
    }

    static void Metric(StringBuilder c,int x,int y,string label,int value,string note,bool alert){Fill(c,"0.96 0.97 0.95",x,y,270,94);Text(c,"/F2",9,"0.35 0.38 0.33",x+18,y+67,label);Text(c,"/F2",24,"0.08 0.09 0.08",x+18,y+35,value+"%");Text(c,"/F1",8,"0.38 0.41 0.36",x+210,y+38,note);Fill(c,"0.84 0.86 0.82",x+18,y+15,234,7);Fill(c,alert?"1 0.55 0.20":"0.73 1 0.27",x+18,y+15,(int)(234*Math.Max(0,Math.Min(100,value))/100.0),7);}
    static void Header(StringBuilder c,string subtitle,bool appendix){Fill(c,"0.07 0.08 0.07",0,716,612,76);Fill(c,"0.73 1 0.27",0,716,10,76);Text(c,"/F2",21,"0.73 1 0.27",34,754,"SYSTEMSAGE");Text(c,"/F1",9,"0.90 0.92 0.88",34,735,appendix?"TECHNICAL APPENDIX":subtitle);}
    static void Footer(StringBuilder c,int page,int pages,string label){c.Append("0.82 0.84 0.80 RG 30 30 m 582 30 l S ");Text(c,"/F1",7,"0.38 0.40 0.36",30,17,label);Text(c,"/F1",7,"0.38 0.40 0.36",526,17,"Page "+page+" of "+pages);}
    static void Fill(StringBuilder c,string color,int x,int y,int w,int h){c.Append(color+" rg "+x+" "+y+" "+w+" "+h+" re f ");}
    static void Text(StringBuilder c,string font,int size,string color,int x,int y,string text){c.Append(color+" rg BT "+font+" "+size+" Tf "+x+" "+y+" Td ("+Escape(text)+") Tj ET ");}

    static string FormatRow(string[] columns,string[] row){var fields=new List<string>();for(int i=0;i<row.Length;i++){string label=i<columns.Length?columns[i]:"Value";fields.Add(Ascii(label)+": "+Ascii(row[i]));}return String.Join("    ",fields);}
    static string Ascii(string value){var b=new StringBuilder();foreach(char ch in value??"")b.Append(ch>=32&&ch<=126?ch:'?');return b.ToString();}
    static IEnumerable<string> Wrap(string text,int width){if(String.IsNullOrEmpty(text)){yield return "";yield break;}while(text.Length>width){int cut=text.LastIndexOf(' ',width);if(cut<1)cut=width;yield return text.Substring(0,cut).TrimEnd();text=text.Substring(cut).TrimStart();}yield return text;}
    static string Escape(string value){return Ascii(value).Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");}
    static byte[] A(string value){return Encoding.ASCII.GetBytes(value);}static void W(Stream stream,string value){byte[] bytes=A(value);stream.Write(bytes,0,bytes.Length);}
    static List<List<Block>> Paginate(List<Block> blocks){var pages=new List<List<Block>>();var page=new List<Block>();int remaining=618;string section="";for(int i=0;i<blocks.Count;i++){Block b=blocks[i];int need=b.Height;if(b.Kind==1&&i+1<blocks.Count)need+=blocks[i+1].Height;if(page.Count>0&&need>remaining){pages.Add(page);page=new List<Block>();remaining=618;if(!String.IsNullOrEmpty(section)&&b.Kind!=1){var continued=new Block(section+" (CONTINUED)",1,32);page.Add(continued);remaining-=continued.Height;}}if(b.Kind==1)section=b.Text;page.Add(b);remaining-=b.Height;}if(page.Count>0)pages.Add(page);return pages;}

    static void Build(string path,DiagnosticReport report,List<Block> blocks){
      var detailPages=Paginate(blocks);int pageCount=detailPages.Count+1;var contents=new List<string>{Dashboard(report,pageCount)};
      for(int i=0;i<detailPages.Count;i++){var c=new StringBuilder();Header(c,"",true);Text(c,"/F2",11,"0.35 0.38 0.33",30,688,"DETAILED SYSTEM DATA");Text(c,"/F1",8,"0.45 0.47 0.43",440,688,"For troubleshooting and support");int y=658;foreach(Block b in detailPages[i]){if(b.Kind==1){Fill(c,"0.94 0.96 0.92",30,y-7,552,23);Fill(c,"0.73 1 0.27",30,y-7,4,23);Text(c,"/F2",10,"0.10 0.11 0.09",43,y,b.Text);}else if(b.Kind==0&&!String.IsNullOrEmpty(b.Text))Text(c,"/F1",9,"0.18 0.20 0.17",43,y,b.Text);y-=b.Height;}Footer(c,i+2,pageCount,"READ-ONLY LOCAL DIAGNOSTIC - NO DATA UPLOADED");contents.Add(c.ToString());}
      var objects=new List<byte[]>();objects.Add(A("<< /Type /Catalog /Pages 2 0 R >>"));string kids=String.Join(" ",Enumerable.Range(0,pageCount).Select(i=>(3+i*2)+" 0 R"));objects.Add(A("<< /Type /Pages /Kids ["+kids+"] /Count "+pageCount+" >>"));int regularId=3+pageCount*2,boldId=regularId+1;
      for(int i=0;i<pageCount;i++){int pageId=3+i*2,contentId=pageId+1;objects.Add(A("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 "+regularId+" 0 R /F2 "+boldId+" 0 R >> >> /Contents "+contentId+" 0 R >>"));byte[] stream=A(contents[i]);objects.Add(A("<< /Length "+stream.Length+" >>\nstream\n"+contents[i]+"\nendstream"));}
      objects.Add(A("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));objects.Add(A("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));using(var output=new FileStream(path,FileMode.Create,FileAccess.Write)){W(output,"%PDF-1.4\n");var offsets=new List<long>{0};for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);W(output,(i+1)+" 0 obj\n");output.Write(objects[i],0,objects[i].Length);W(output,"\nendobj\n");}long xref=output.Position;W(output,"xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Count;i++)W(output,offsets[i].ToString("0000000000")+" 00000 n \n");W(output,"trailer << /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF");}
    }
  }
}
