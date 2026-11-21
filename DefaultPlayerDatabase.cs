
// Modified build v1.1.26. Reads the local FM27 database using the 2700 layout.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Fm24Editor
{
    static class DefaultPlayerDatabase
    {
        const string PlayersFile="people_db.dat";
        const string NamesFile="client_db.dat";
        const int AttributeOffset=50, AttributeCount=54;
        static readonly byte[] PeopleHeader={3,1,116,97,100,46,160,15,169,14,9,0,40,181,47,253};
        static readonly string[] NameTables={"name_table_extra_0","name_table_extra_1","name_table_extra_2"};
        static readonly Encoding Utf8=new UTF8Encoding(false,true);

        static string Version(DirectoryInfo directory)
        {
            string name=directory.Name;
            if(name.Length==7 && name.StartsWith("27",StringComparison.Ordinal) &&
               name[2]>='0' && name[2]<='9' && name[3]>='0' && name[3]<='9' &&
               name.EndsWith("_fm",StringComparison.OrdinalIgnoreCase)) return name.Substring(0,4);
            return null;
        }

        internal static string[] InstalledSources(string enginePath)
        {
            if(string.IsNullOrEmpty(enginePath)) return new string[0];
            for(var directory=new FileInfo(enginePath).Directory; directory!=null; directory=directory.Parent)
            {
                foreach(string relative in new[]{@"shared\data\database\db",@"data\database\db"})
                {
                    string root=Path.Combine(directory.FullName,relative);
                    if(!Directory.Exists(root)) continue;
                    try
                    {
                        var matches=new DirectoryInfo(root).EnumerateDirectories("27*")
                            .Select(d=>new DirectoryInfo(Path.Combine(d.FullName,d.Name+"_fm")))
                            .Where(d=>Version(d)!=null && File.Exists(Path.Combine(d.FullName,NamesFile)))
                            .Select(d=>Path.Combine(d.FullName,PlayersFile)).Where(File.Exists)
                            .OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
                        if(matches.Length>0) return matches;
                    }
                    catch(IOException) { }
                    catch(UnauthorizedAccessException) { }
                }
            }
            return new string[0];
        }

        internal static AttributeRestoreSource Read(string path,Player player,CancellationToken cancel)
        {
            if(player==null || player.Uid<=0) throw new InvalidDataException("请选择有有效 UID 的球员。");
            cancel.ThrowIfCancellationRequested();
            var fileInfo=new FileInfo(path);
            string version=Version(fileInfo.Directory);
            string namesPath=Path.Combine(fileInfo.DirectoryName,NamesFile);
            if(!fileInfo.Name.Equals(PlayersFile,StringComparison.OrdinalIgnoreCase) || version==null || !File.Exists(namesPath))
                throw new InvalidDataException("请选择 FM27 安装目录 27xx_fm 中的 people_db.dat，同目录需要有 client_db.dat。");

            var search=new RecordSearch(player.Uid);
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var reader=new BinaryReader(file,Encoding.UTF8,true))
            {
                if(!reader.ReadBytes(PeopleHeader.Length).SequenceEqual(PeopleHeader))
                    throw new InvalidDataException("所选球员数据库的格式未识别。请选择已安装的 FM27 原版数据库。");
                file.Position=12;
                NativeZstd.Decode(file,file.Length-file.Position,1024*1024*1024,cancel,search.Append);
            }
            if(search.Records.Count==0) throw MissingPlayer();

            var resolved=new Dictionary<int,string>[NameTables.Length];
            using(var archive=new FmDataArchive(namesPath,cancel,12,NameTables))
                for(int group=0;group<NameTables.Length;group++)
                {
                    var needed=new HashSet<int>(search.Records.Select(r=>r.NameIds[group]).Where(i=>i>=0));
                    resolved[group]=ReadNames(archive.Read(NameTables[group],64*1024*1024),needed,cancel);
                }

            var liveNames=new List<string>{player.Name};
            if(player.NameParts!=null)
                liveNames.AddRange(new[]{player.NameParts.Full,player.NameParts.Common,
                    (player.NameParts.First+" "+player.NameParts.Last).Trim()});
            var nameKeys=new HashSet<string>(liveNames.Select(PlayerNames.Normalize).Where(n=>n.Length>0));
            AttributeRestoreSource found=null;
            foreach(var entry in search.Records)
            {
                var names=new string[NameTables.Length];
                for(int group=0;group<names.Length;group++)
                    resolved[group].TryGetValue(entry.NameIds[group],out names[group]);
                string full=(names[0]+" "+names[1]).Trim();
                var candidates=new[]{entry.LegalName,full,names[2]};
                bool sameName=candidates.Any(n=>nameKeys.Contains(PlayerNames.Normalize(n)) ||
                    (!string.IsNullOrEmpty(n) && liveNames.Any(m=>PlayerNames.CompatibleFullNames(m,n))));
                bool sameBirth=player.Born.HasValue && player.Born.Value.Date==entry.Born;
                // Official FM UIDs can exceed two billion. Require a name and birthday
                // there since players generated in a career can use the same range.
                if(player.Uid>=2000000000 ? !(sameName && sameBirth) : !(sameName || sameBirth)) continue;
                if(found!=null) throw new InvalidDataException("数据库中有多条可匹配的球员资料，无法确认原值。");
                found=new AttributeRestoreSource{Uid=player.Uid,Name=candidates.FirstOrDefault(n=>!string.IsNullOrWhiteSpace(n))??player.Name,
                    Born=entry.Born,Ca=entry.Ca==0?(int?)null:entry.Ca,Attributes=entry.Attributes,
                    FromDatabase=true,SourceName="FM27 原版数据库 "+version};
            }
            if(found==null) throw MissingPlayer();
            return found;
        }

        static InvalidDataException MissingPlayer()
        {
            return new InvalidDataException("原版数据库没有这名球员的可核实默认值。补丁新增或游戏生成的球员请使用包含该球员的开档存档。");
        }

        static Dictionary<int,string> ReadNames(byte[] data,HashSet<int> needed,CancellationToken cancel)
        {
            if(data.Length<12 || !PeopleHeader.Take(8).SequenceEqual(data.Take(8))) throw FmDataArchive.Invalid();
            int count=BitConverter.ToInt32(data,8);
            if(count<0 || count>(data.Length-12)/8 || needed.Any(id=>id>=count)) throw FmDataArchive.Invalid();
            var names=new Dictionary<int,string>();
            int cursor=12;
            for(int id=0;id<count;id++)
            {
                if((id&16383)==0) cancel.ThrowIfCancellationRequested();
                if(cursor>data.Length-8 || BitConverter.ToInt32(data,cursor)!=id) throw FmDataArchive.Invalid();
                int len=BitConverter.ToInt32(data,cursor+4);
                cursor+=8;
                if(len<0 || len>1024 || len>data.Length-cursor) throw FmDataArchive.Invalid();
                if(needed.Contains(id)) names.Add(id,Utf8.GetString(data,cursor,len));
                cursor+=len;
            }
            if(cursor!=data.Length) throw FmDataArchive.Invalid();
            return names;
        }

        sealed class PlayerRecord
        {
            internal int Ca;
            internal byte[] Attributes;
            internal int[] NameIds;
            internal string LegalName;
            internal DateTime Born;
        }

        // The people stream is hundreds of MB uncompressed. Keep only one decoder
        // chunk and a bounded history for records that straddle two writes.
        sealed class RecordSearch
        {
            const int History=128*1024;
            const int NameReferencesLength=15, IdentityTail=30;
            readonly byte[] buffer=new byte[History+NativeZstd.ChunkSize];
            readonly int uid;
            int kept, next=4, recordCount;
            internal readonly List<PlayerRecord> Records=new List<PlayerRecord>();

            internal RecordSearch(int uid) { this.uid=uid; }
            int Int(int at) { return BitConverter.ToInt32(buffer,at); }

            internal void Append(byte[] chunk,int size)
            {
                Buffer.BlockCopy(chunk,0,buffer,kept,size);
                int end=kept+size;
                if(end<6){kept=end;return;}
                if(recordCount==0)
                {
                    recordCount=Int(2);
                    if(buffer[0]!=0 || buffer[1]!=0 || recordCount<1 || recordCount>3000000)
                        throw FmDataArchive.Invalid();
                }
                while(next<=end-8)
                {
                    int hit=Array.IndexOf(buffer,(byte)uid,next,end-next-7);
                    if(hit<0) break;
                    next=hit+1;
                    if(Int(hit)==uid && Int(hit+4)==uid) FindRecord(hit-4);
                }
                kept=Math.Min(end,History);
                Buffer.BlockCopy(buffer,end-kept,buffer,0,kept);
                next=Math.Max(4,kept-7);
            }

            void FindRecord(int close)
            {
                int index=Int(close);
                if(index<0 || index>=recordCount) return;
                // The index prefixes the record and is repeated alongside the two UID
                // copies at its end. The UID before the prefix belongs to its neighbour.
                int start=close-AttributeOffset-AttributeCount-NameReferencesLength-4-IdentityTail;
                while(start>=12)
                {
                    int at=Array.LastIndexOf(buffer,(byte)index,start,start-11);
                    if(at<0) break;
                    start=at-1;
                    if(Int(at)!=index || buffer[at+4]!=2 && buffer[at+4]!=3) continue;
                    int previousIndex=Int(at-12), previousUid=Int(at-8);
                    if(previousIndex<0 || previousIndex>=recordCount || previousIndex==index ||
                       previousUid<=0 || previousUid!=Int(at-4)) continue;
                    int ca=BitConverter.ToInt16(buffer,at+5), pa=BitConverter.ToInt16(buffer,at+13);
                    if(ca<0 || ca>200 || pa< -100 || pa>200 || !InRange(at+35,15,20) ||
                       !InRange(at+AttributeOffset,AttributeCount,20)) continue;
                    var positions=new ArraySegment<byte>(buffer,at+35,15);
                    if(!positions.Any(v=>v>=12)) continue;
                    FindIdentity(at,close,ca);
                }
            }

            bool InRange(int at,int count,int max)
            {
                for(int i=at;i<at+count;i++) if(buffer[i]>max) return false;
                return true;
            }

            void FindIdentity(int at,int close,int ca)
            {
                int afterAttributes=at+AttributeOffset+AttributeCount;
                for(int birth=afterAttributes+NameReferencesLength+4;birth<=close-IdentityTail;birth++)
                {
                    if(buffer[birth+12]!=3 || buffer[birth+13]>1 || buffer[birth+9]!=0 ||
                       buffer[birth+10]!=0 || buffer[birth+11]!=0 || !InRange(birth+22,8,20)) continue;
                    int year=BitConverter.ToUInt16(buffer,birth+2), day=BitConverter.ToUInt16(buffer,birth);
                    if(year<1900 || year>2200 || day<1 || day>(DateTime.IsLeapYear(year)?366:365)) continue;
                    int maxLength=Math.Min(200,birth-afterAttributes-NameReferencesLength-4);
                    for(int len=0;len<=maxLength;len++)
                    {
                        int countAt=birth-len-4;
                        if(Int(countAt)!=len) continue;
                        int namesAt=countAt-NameReferencesLength;
                        if(buffer[namesAt+4]!=0 || buffer[namesAt+9]!=0 || buffer[namesAt+14]!=0) continue;
                        int[] ids={Int(namesAt),Int(namesAt+5),Int(namesAt+10)};
                        if(ids.Any(id=>id< -1 || id>=8388608) || ids.All(id=>id<0)) continue;
                        string legal;
                        try { legal=Utf8.GetString(buffer,birth-len,len); }
                        catch(DecoderFallbackException) { continue; }
                        if(legal.Any(char.IsControl) || len>0 && !legal.Any(char.IsLetter)) continue;
                        byte[] attributes=new byte[AttributeCount];
                        for(int i=0;i<attributes.Length;i++) attributes[i]=(byte)(buffer[at+AttributeOffset+i]*5);
                        if(attributes.All(v=>v==0)) continue;
                        if(Records.Count>=16) throw new InvalidDataException("默认属性记录无法唯一确认。");
                        Records.Add(new PlayerRecord{Ca=ca,Attributes=attributes,NameIds=ids,LegalName=legal,
                            Born=new DateTime(year,1,1).AddDays(day-1)});
                    }
                }
            }
        }
    }
}
