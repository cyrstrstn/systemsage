using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SystemSage {
  static class StyledPdfReport {
    sealed class Block { public string Text; public int Kind; public int Height; public Block(string text,int kind,int height){Text=text;Kind=kind;Height=height;} }
    public static string Write(DiagnosticReport report){
      string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"SystemSage Reports");Directory.CreateDirectory(dir);
      string path=Path.Combine(dir,"SystemSage-PC-Health-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".pdf");var blocks=new List<Block>();
      blocks.Add(new Block("Generated "+report.Created.ToString("yyyy-MM-dd HH:mm:ss")+"    Recent events: "+report.ErrorCount+"    Device warnings: "+report.WarningCount,2,30));
      foreach(var section in report.Sections){blocks.Add(new Block(section.Title.ToUpperInvariant(),1,32));if(section.Rows.Count==0)blocks.Add(new Block("No issues found.",0,18));foreach(var row in section.Rows){string value=FormatRow(section.Columns,row);foreach(string line in Wrap(value,88))blocks.Add(new Block(line,0,15));blocks.Add(new Block("",3,7));}}
      Build(path,blocks);return path;
    }
    static string FormatRow(string[] columns,string[] row){var fields=new List<string>();for(int i=0;i<row.Length;i++){string label=i<columns.Length?columns[i]:"Value";fields.Add(Ascii(label)+": "+Ascii(row[i]));}return String.Join("    ",fields);}
    static string Ascii(string value){var b=new StringBuilder();foreach(char c in value??"")b.Append(c>=32&&c<=126?c:'?');return b.ToString();}
    static IEnumerable<string> Wrap(string text,int width){if(String.IsNullOrEmpty(text)){yield return "";yield break;}while(text.Length>width){int cut=text.LastIndexOf(' ',width);if(cut<1)cut=width;yield return text.Substring(0,cut).TrimEnd();text=text.Substring(cut).TrimStart();}yield return text;}
    static string Escape(string value){return value.Replace("\\","\\\\").Replace("(","\\(").Replace(")","\\)");}
    static byte[] A(string value){return Encoding.ASCII.GetBytes(value);}static void W(Stream stream,string value){byte[] bytes=A(value);stream.Write(bytes,0,bytes.Length);}
    static List<List<Block>> Paginate(List<Block> blocks){var pages=new List<List<Block>>();var page=new List<Block>();int remaining=618;string section="";for(int i=0;i<blocks.Count;i++){Block b=blocks[i];int need=b.Height;if(b.Kind==1&&i+1<blocks.Count)need+=blocks[i+1].Height;if(page.Count>0&&need>remaining){pages.Add(page);page=new List<Block>();remaining=618;if(!String.IsNullOrEmpty(section)&&b.Kind!=1){var continued=new Block(section+" (CONTINUED)",1,32);page.Add(continued);remaining-=continued.Height;}}if(b.Kind==1)section=b.Text;page.Add(b);remaining-=b.Height;}if(page.Count>0)pages.Add(page);return pages;}
    static void Build(string path,List<Block> blocks){var pages=Paginate(blocks);int pageCount=pages.Count;var objects=new List<byte[]>();objects.Add(A("<< /Type /Catalog /Pages 2 0 R >>"));string kids=String.Join(" ",Enumerable.Range(0,pageCount).Select(i=>(3+i*2)+" 0 R"));objects.Add(A("<< /Type /Pages /Kids ["+kids+"] /Count "+pageCount+" >>"));int regularId=3+pageCount*2,boldId=regularId+1;
      for(int pageIndex=0;pageIndex<pageCount;pageIndex++){int pageId=3+pageIndex*2,contentId=pageId+1;objects.Add(A("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 "+regularId+" 0 R /F2 "+boldId+" 0 R >> >> /Contents "+contentId+" 0 R >>"));var c=new StringBuilder();
        c.Append("0.07 0.08 0.07 rg 0 716 612 76 re f 0.73 1 0.27 rg 0 716 10 76 re f ");c.Append("BT /F2 21 Tf 0.73 1 0.27 rg 34 754 Td (SYSTEMSAGE) Tj ET BT /F1 9 Tf 0.90 0.92 0.88 rg 34 735 Td (COMPLETE PC HEALTH REPORT) Tj ET ");int y=688;
        foreach(Block b in pages[pageIndex]){if(b.Kind==1){c.Append("0.94 0.96 0.92 rg 30 "+(y-7)+" 552 23 re f 0.73 1 0.27 rg 30 "+(y-7)+" 4 23 re f 0.10 0.11 0.09 rg BT /F2 10 Tf 43 "+y+" Td ("+Escape(b.Text)+") Tj ET ");}else if(b.Kind==2){c.Append("0.36 0.39 0.34 rg BT /F1 9 Tf 34 "+y+" Td ("+Escape(b.Text)+") Tj ET ");}else if(b.Kind==0&&!String.IsNullOrEmpty(b.Text)){c.Append("0.18 0.20 0.17 rg BT /F1 9 Tf 43 "+y+" Td ("+Escape(b.Text)+") Tj ET ");}y-=b.Height;}
        c.Append("0.82 0.84 0.80 RG 30 30 m 582 30 l S 0.38 0.40 0.36 rg BT /F1 7 Tf 30 17 Td (READ-ONLY LOCAL DIAGNOSTIC - NO DATA UPLOADED) Tj ET BT /F1 7 Tf 526 17 Td (Page "+(pageIndex+1)+" of "+pageCount+") Tj ET ");byte[] stream=A(c.ToString());objects.Add(A("<< /Length "+stream.Length+" >>\nstream\n"+c+"\nendstream"));}
      objects.Add(A("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));objects.Add(A("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));using(var output=new FileStream(path,FileMode.Create,FileAccess.Write)){W(output,"%PDF-1.4\n");var offsets=new List<long>{0};for(int i=0;i<objects.Count;i++){offsets.Add(output.Position);W(output,(i+1)+" 0 obj\n");output.Write(objects[i],0,objects[i].Length);W(output,"\nendobj\n");}long xref=output.Position;W(output,"xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n");for(int i=1;i<offsets.Count;i++)W(output,offsets[i].ToString("0000000000")+" 00000 n \n");W(output,"trailer << /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF");}}
  }
}
