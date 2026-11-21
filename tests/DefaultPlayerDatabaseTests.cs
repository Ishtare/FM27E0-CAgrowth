
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class DefaultPlayerDatabaseTests
    {
        const int TestUid=2000012345;
        static readonly DateTime Birth=new DateTime(2002,2,13);
        static readonly byte[] Header={3,1,116,97,100,46,160,15,169,14,9,0};

        static void Put(byte[] data,int at,int value) { BitConverter.GetBytes(value).CopyTo(data,at); }

        static void PlayerRecord(byte[] data,int start,int close,int index,int uid,short ca)
        {
            Put(data,start-12,index-1);
            Put(data,start-8,100000+index);
            Put(data,start-4,100000+index);
            Put(data,start,index);
            data[start+4]=3;
            BitConverter.GetBytes(ca).CopyTo(data,start+5);
            BitConverter.GetBytes((short)167).CopyTo(data,start+13);
            data[start+35+12]=20;
            for(int i=0;i<54;i++) data[start+50+i]=(byte)(10+i%8);
            data[start+50+35]=14; // free kicks are a concrete default
            data[start+50+38]=7;
            data[start+50+3]=0; // generated at career creation
            int birth=start+188;
            Put(data,birth-9,-1); // no common name; resolve first and last from the archive
            Put(data,birth,(int)((uint)Birth.Year<<16)|(int)Birth.DayOfYear);
            data[birth+12]=3;
            data[birth+13]=1;
            for(int i=0;i<8;i++) data[birth+22+i]=15;
            Put(data,close,index);
            Put(data,close+4,uid);
            Put(data,close+8,uid);
        }

        static byte[] People(short ca,bool duplicate=false)
        {
            var data=new byte[133000];
            Put(data,2,24);
            // The UID starts just before the end of one decoder output chunk.
            PlayerRecord(data,130800,131065,4,TestUid,ca);
            if(duplicate) PlayerRecord(data,131300,131605,6,TestUid,ca);
            return Header.Concat(EarlySaveTests.Compress(data)).ToArray();
        }

        static byte[] Table(string name)
        {
            using(var output=new MemoryStream())
            using(var writer=new BinaryWriter(output))
            {
                writer.Write(Header.Take(8).ToArray());
                writer.Write(1);
                writer.Write(0);
                EarlySaveTests.Text(writer,name);
                return output.ToArray();
            }
        }

        static void Invalid(Action action,Action<bool,string> check,string message)
        {
            bool rejected=false;
            try { action(); }
            catch(InvalidDataException) { rejected=true; }
            check(rejected,message);
        }

        internal static void Run(Action<bool,string> check)
        {
            string root=Path.Combine(Path.GetTempPath(),"fm27-db-"+Guid.NewGuid().ToString("N"));
            string dbDir=Path.Combine(root,@"shared\data\database\db\2700\2700_fm");
            string path=Path.Combine(dbDir,"people_db.dat");
            string names=Path.Combine(dbDir,"client_db.dat");
            Directory.CreateDirectory(dbDir);
            try
            {
                byte[] people=People(124);
                byte[] nameArchive=Header.Concat(EarlySaveTests.Archive("client_db",
                    new[]{"name_table_extra_0","name_table_extra_1","name_table_extra_2"},
                    new[]{Table("Local"),Table("Baseline"),Table("Optional")})).ToArray();
                File.WriteAllBytes(path,people);
                File.WriteAllBytes(names,nameArchive);
                string engine=Path.Combine(root,@"fm_Data\Plugins\x86_64\game_plugin.dll");
                check(DefaultPlayerDatabase.InstalledSources(engine).SequenceEqual(new[]{path}),"installed database is found from the FM engine path");

                var fixture=new GrowthFocusTests.Fixture();
                fixture.Player.Uid=TestUid;
                fixture.Player.Name="Local Baseline";
                fixture.Player.Born=Birth;
                fixture.Put(fixture.Player.Person+FmLayout.Uid,BitConverter.GetBytes(TestUid));
                fixture.Guards[1]=new System.Collections.Generic.KeyValuePair<ulong,byte[]>(
                    fixture.Player.Person+FmLayout.Uid,BitConverter.GetBytes(TestUid));

                var source=DefaultPlayerDatabase.Read(path,fixture.Player,CancellationToken.None);
                check(source.FromDatabase && !source.GameDate.HasValue && source.SourceName.Contains("2700") &&
                    source.Uid==TestUid && source.Born==Birth && source.Name=="Local Baseline" &&
                    source.Ca==124 && source.Attributes[38]==35 && source.Attributes[3]==0,
                    "original data resolves a large official UID, split chunks, archived name references and unset defaults");
                check(people.SequenceEqual(File.ReadAllBytes(path)) && nameArchive.SequenceEqual(File.ReadAllBytes(names)),
                    "neither installed file changes during lookup");

                var frame=fixture.Capture();
                byte[] next=frame.RestoredAttributes(source,false);
                check(next[3]==frame.Attributes[3] && next[35]==70 && next[38]==35 &&
                    next[24]==frame.Attributes[24],"defaults restore concrete values while preserving generated and optional fields");
                frame.PlanRestore(source,true,false,fixture.Player.Uid);
                var prior=fixture.Digest();
                string error;
                check(fixture.Apply(frame,out error) && fixture.Capture().Ca==124 &&
                    fixture.Capture().Attributes.SequenceEqual(next),"confirmed default CA and attributes apply as one transaction");
                check(GrowthUndo.Apply(fixture.Read,fixture.Write,fixture.Log(frame),out error) && fixture.Digest()==prior,
                    "default restoration and its optional CA undo together");

                using(var dialog=new EarlySaveRestoreDialog(fixture.Player,fixture.Capture(),engine))
                {
                    dialog.Bind(source);
                    var grid=(DataGridView)dialog.Controls.Find("EarlySaveAttributes",true).Single();
                    check(grid.Columns[2].HeaderText=="数据库默认值" &&
                        grid.Rows.Cast<DataGridViewRow>().Any(r=>Equals(r.Cells[2].Value,"开档生成") &&
                            Equals(r.Cells[1].Value,r.Cells[3].Value)) &&
                        dialog.Controls.Find("ChooseEarlySave",true).Length==1 &&
                        dialog.Controls.Find("ReadDefaultDatabase",true).Length==1,
                        "preview distinguishes generated fields, local database defaults and early save access");
                }

                var other=new Player{Uid=TestUid,Name="Another Player",Born=Birth};
                Invalid(()=>DefaultPlayerDatabase.Read(path,other,CancellationToken.None),check,
                    "a career generated player with matching high UID and birthday cannot inherit an unrelated original");
                other.Name=fixture.Player.Name;
                other.Born=Birth.AddDays(1);
                Invalid(()=>DefaultPlayerDatabase.Read(path,other,CancellationToken.None),check,
                    "matching high UID and name with another birthday cannot restore");
                other.Uid=100004;
                Invalid(()=>DefaultPlayerDatabase.Read(path,other,CancellationToken.None),check,
                    "the UID at the opening belongs to the preceding player");
                File.WriteAllBytes(path,People(124,true));
                Invalid(()=>DefaultPlayerDatabase.Read(path,fixture.Player,CancellationToken.None),check,
                    "ambiguous database records cannot produce a preview");
                File.WriteAllBytes(path,People(0));
                var generated=DefaultPlayerDatabase.Read(path,fixture.Player,CancellationToken.None);
                check(!generated.Ca.HasValue && !generated.CanRestoreCa(160),
                    "opening-generated CA remains unavailable");
                Invalid(()=>fixture.Capture().PlanRestore(generated,true,false,fixture.Player.Uid),check,
                    "no CA is written using a generated default");
                bool cancelled=false;
                try { DefaultPlayerDatabase.Read(path,fixture.Player,new CancellationToken(true)); }
                catch(OperationCanceledException) { cancelled=true; }
                check(cancelled,"cancelled database reading does not return a source");
                File.WriteAllBytes(path,people.Take(25).ToArray());
                Invalid(()=>DefaultPlayerDatabase.Read(path,fixture.Player,CancellationToken.None),check,
                    "truncated original database cannot restore");
            }
            finally
            {
                File.Delete(path);
                File.Delete(names);
                for(var dir=new DirectoryInfo(dbDir);dir!=null && dir.FullName.StartsWith(root,StringComparison.OrdinalIgnoreCase);dir=dir.Parent)
                    dir.Delete(false);
            }
        }
    }
}
