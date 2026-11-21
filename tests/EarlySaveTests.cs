using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Fm24Editor
{
    static class EarlySaveTests
    {
        static readonly DateTime Birth=new DateTime(2001,5,16);
        static uint Packed(DateTime d){return (uint)(d.Year<<16)|(uint)d.DayOfYear;}
        static void Put(byte[] data,int at,int n){Buffer.BlockCopy(BitConverter.GetBytes(n),0,data,at,4);}
        internal static void Text(BinaryWriter w,string text){var b=Encoding.UTF8.GetBytes(text);w.Write(b.Length);w.Write(b);}
        static void Reject(Action action,Action<bool,string> check,string message)
        {bool denied=false;try{action();}catch(InvalidDataException){denied=true;}check(denied,message);}
        static byte[] Database(int uid,string name)
        {
            byte[] db=new byte[1024]; int start=35, rec=start+19;
            Put(db,start,7);Put(db,start+4,98321);Put(db,start+8,98321);db[start+12]=2;
            Put(db,rec,120|(175<<16));
            for(int i=0;i<15;i++)db[rec+24+i]=(byte)(i==12?20:1);
            for(int i=0;i<54;i++)db[rec+39+i]=(byte)(20+i);
            int p=300;var legal=Encoding.UTF8.GetBytes(name);Put(db,p-4-legal.Length,legal.Length);
            Buffer.BlockCopy(legal,0,db,p-legal.Length,legal.Length);Put(db,p,(int)Packed(Birth));
            for(int i=0;i<8;i++)db[p+22+i]=12;
            Put(db,550,8);Put(db,554,uid);Put(db,558,uid);db[562]=1;
            return db;
        }
        [DllImport(@"third-party\libzstd.dll",CallingConvention=CallingConvention.Cdecl),DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
        static extern UIntPtr ZSTD_compress(byte[] dst,UIntPtr cap,byte[] src,UIntPtr size,int level);
        internal static byte[] Compress(byte[] data)
        {
            byte[] b=new byte[data.Length+512]; var size=ZSTD_compress(b,(UIntPtr)b.Length,data,(UIntPtr)data.Length,1);
            return b.Take(checked((int)size.ToUInt32())).ToArray();
        }
        static byte[] Container(byte[] db,DateTime date)
        {
            byte[] info;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                w.Write(0L);Text(w,"fm");Text(w,"27.1.0+2505117");w.Write(0);w.Write(0L);
                Text(w,"save");Text(w,"career");w.Write(0);w.Write(Packed(date));info=s.ToArray();
            }
            return Archive("Synthetic fixture",new[]{"save_game_summary","game_db"},new[]{info,db});
        }
        internal static byte[] Archive(string name,string[] entries,byte[][] raw)
        {
            var blocks=raw.Select(Compress).ToArray();
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                Text(w,name);w.Write(entries.Length);long offset=0;
                for(int i=0;i<entries.Length;i++)
                {Text(w,entries[i]);Text(w,".dat");w.Write(offset);w.Write((long)blocks[i].Length);
                    w.Write((long)raw[i].Length);w.Write(0L);w.Write(0L);offset+=blocks[i].Length;}
                byte[] header=new byte[26];header[0]=2;header[1]=1;Encoding.ASCII.GetBytes("fmf.").CopyTo(header,2);
                BitConverter.GetBytes(26L+offset-9).CopyTo(header,9);
                return header.Concat(blocks.SelectMany(b=>b)).Concat(new byte[9]).Concat(Compress(s.ToArray())).ToArray();
            }
        }
        internal static void Run(Action<bool,string> check)
        {
            var f=new GrowthFocusTests.Fixture();f.Player.Born=Birth;
            var db=Database(f.Player.Uid,f.Player.Name);
            var bytes=Container(db,new DateTime(2026,7,1));
            string path=Path.Combine(Path.GetTempPath(),"fm27-restore-"+Guid.NewGuid().ToString("N")+".fm");
            try
            {
                File.WriteAllBytes(path,bytes);
                var source=EarlySavePlayer.Read(path,f.Player,f.Date,CancellationToken.None);
                check(source.Uid==f.Player.Uid && source.Ca==120 && source.Born==Birth && source.Name==f.Player.Name &&
                    source.Attributes[38]==58 && source.GameDate==new DateTime(2026,7,1),"early FM container returns the selected closing UID, original CA, date and all raw attributes");
                check(File.ReadAllBytes(path).SequenceEqual(bytes),"source save remains byte-for-byte identical");
                Reject(()=>EarlySavePlayer.Read(path,f.Player,Packed(new DateTime(2025,1,1)),CancellationToken.None),check,"a later source save cannot restore the past");
                Reject(()=>EarlySavePlayer.Locate(db,new Player{Uid=98321,Name=f.Player.Name,Born=Birth},CancellationToken.None),check,"previous record's UID cannot bind to the next player's attributes");
                Reject(()=>EarlySavePlayer.Locate(db,new Player{Uid=99999,Name=f.Player.Name,Born=Birth},CancellationToken.None),check,"missing player rejected even when name/birthday match");
                var duplicate=db.Concat(db).ToArray();
                Reject(()=>EarlySavePlayer.Locate(duplicate,f.Player,CancellationToken.None),check,"multiple matching records prevent restoring");
                var gen=new Player{Uid=2000001234,Name="Generated Fixture",Born=Birth};
                var genDb=Database(gen.Uid,gen.Name);check(EarlySavePlayer.Locate(genDb,gen,CancellationToken.None).Uid==gen.Uid,"newgen UID, birth and name match");
                gen.Name="Different player";Reject(()=>EarlySavePlayer.Locate(genDb,gen,CancellationToken.None),check,"same newgen UID and birthday from a different player rejected");
                var frame=f.Capture();var expected=frame.RestoredAttributes(source,false);
                check(expected[27]==source.Attributes[27] && expected[24]==50 && expected[25]==50 && expected[49]==50,"default restoration includes set pieces, with feet and hidden opt-in");
                frame.PlanRestore(source,false,false,f.Player.Uid);
                check(frame.Edits.Count>0 && frame.Edits.All(e=>e.Address>=f.Player.Block+15 && e.Address<f.Player.Block+69),"default restoration never writes CA/PA or position");
                string error;string prior=f.Digest();
                check(f.Apply(frame,out error) && f.Read(f.Player.Block+15,54).SequenceEqual(expected),"reviewed restore writes exactly the expected attributes");
                check(GrowthUndo.Apply(f.Read,f.Write,f.Log(frame),out error) && f.Digest()==prior,"full restore can be undone in one operation");
                frame=f.Capture();frame.PlanRestore(source,true,true,f.Player.Uid);f.FailAt=f.Writes+3;
                check(!f.Apply(frame,out error) && f.Digest()==prior,"interrupted CA and attribute restore rolls back");
                f.FailAt=0;frame=f.Capture();frame.PlanRestore(source,true,true,f.Player.Uid);
                check(f.Apply(frame,out error) && f.Capture().Ca==source.Ca && f.Capture().Attributes.SequenceEqual(source.Attributes),"explicit full restoration applies all 54 fields and source CA, current PA stays intact");
                frame=f.Capture();source.Ca=180;Reject(()=>frame.PlanRestore(source,true,false,f.Player.Uid),check,"optional early CA cannot exceed current PA");
                source.Ca=120;Reject(()=>frame.PlanRestore(source,false,false,77),check,"changed target identity blocks prepared restoration");
                bool cancelled=false;try{EarlySavePlayer.Read(path,f.Player,f.Date,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}check(cancelled,"cancelled file read stops before producing a preview");
                File.WriteAllBytes(path,bytes.Take(32).ToArray());Reject(()=>EarlySavePlayer.Read(path,f.Player,f.Date,CancellationToken.None),check,"truncated archive cannot produce an edit");
                var uiFixture=new GrowthFocusTests.Fixture();using(var d=new EarlySaveRestoreDialog(uiFixture.Player,uiFixture.Capture()))
                {
                    d.Bind(source);
                    check(!((CheckBox)d.Controls.Find("RestoreOriginalCA",true).Single()).Checked && !((CheckBox)d.Controls.Find("RestoreHiddenAndFeet",true).Single()).Checked &&
                        ((Button)d.Controls.Find("ApplyEarlySaveRestore",true).Single()).Enabled,"restore has a difference preview and both optional groups start off");
                    ((CheckBox)d.Controls.Find("RestoreOriginalCA",true).Single()).Checked=true;
                    check(d.Controls.Find("EarlySaveComparison",true).Single().Text.Contains("100 → 120") && uiFixture.Writes==0,"CA toggle updates preview only");
                }
            }
            finally{if(File.Exists(path))File.Delete(path);}
            var free=new GrowthFocusTests.Fixture();free.Player.Club=0;free.Put(free.Player.Tail-FmLayout.TeamBeforePerson,new byte[8]);
            var single=GrowthStep.Capture(free.Read,()=>free.Date,free.Player,free.Guards,true);
            check(single.Matches(free.Read,()=>free.Date),"individual restore can target a verified unattached player");
            free.Put(free.Player.Tail-FmLayout.TeamBeforePerson,BitConverter.GetBytes(0x60000UL));
            check(!single.Matches(free.Read,()=>free.Date),"joining a team invalidates the unattached preview");
        }
    }
}
