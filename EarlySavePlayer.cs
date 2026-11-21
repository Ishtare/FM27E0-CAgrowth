// Modified build v1.1.26. Layout references and license are in third-party/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Fm24Editor
{
    sealed class AttributeRestoreSource
    {
        internal int Uid;
        internal int? Ca;
        internal byte[] Attributes;
        internal DateTime? GameDate;
        internal DateTime Born;
        internal string Name, SourceName;
        internal bool FromDatabase;
        internal string Description
        {
            get { return GameDate.HasValue ? SourceName+"（游戏日期 "+GameDate.Value.ToString("yyyy-MM-dd")+"）" : SourceName; }
        }
        internal bool CanRestoreCa(int cap) { return Ca.HasValue && Ca.Value>=1 && Ca.Value<=cap; }
    }

    static class EarlySavePlayer
    {
        internal static AttributeRestoreSource Read(string path, Player player,uint currentDay,CancellationToken cancel)
        {
            if(player==null || player.Uid<=0) throw new InvalidDataException("请选择有有效 UID 的球员。");
            using(var save=new FmDataArchive(path,cancel,0,"save_game_summary","game_db"))
            {
                byte[] summary=save.Read("save_game_summary",32*1024*1024);
                int cursor=8;
                FmDataArchive.ReadString(summary,ref cursor,65536);
                string build=FmDataArchive.ReadString(summary,ref cursor,64);
                if(!System.Text.RegularExpressions.Regex.IsMatch(build,@"^27\.\d+\.\d+\+\d+$"))
                    throw new InvalidDataException("请选择 Football Manager 27 存档。");
                if(cursor>summary.Length-4) throw FmDataArchive.Invalid();
                int count=BitConverter.ToInt32(summary,cursor); cursor+=4;
                if(count<0 || count>4096) throw FmDataArchive.Invalid();
                for(int i=0;i<count;i++) FmDataArchive.ReadString(summary,ref cursor,1024);
                cursor+=8;
                FmDataArchive.ReadString(summary,ref cursor,1024);
                FmDataArchive.ReadString(summary,ref cursor,1024);
                if(cursor>summary.Length-8) throw FmDataArchive.Invalid();
                DateTime date=Birthdays.Decode(BitConverter.ToUInt32(summary,cursor+4));
                if(date>Birthdays.Decode(currentDay)) throw new InvalidDataException("所选存档的游戏日期晚于当前日期，请选择开档或早期存档。");
                var database=save.Read("game_db",0x7FEFFFFF);
                var result=Locate(database,player,cancel);
                result.GameDate=date; result.SourceName=Path.GetFileName(path);
                return result;
            }
        }
        static int Number(byte[] bytes,int at) { return BitConverter.ToInt32(bytes,at); }
        static bool Range(byte[] bytes,int at,int count,int min,int max)
        {
            if(at<0 || at>bytes.Length-count) return false;
            for(int i=at;i<at+count;i++) if(bytes[i]<min || bytes[i]>max) return false;
            return true;
        }
        // The public game UID closes the person record. The UID ahead of CA belongs to the
        // preceding person and must never identify the player being restored.
        internal static AttributeRestoreSource Locate(byte[] db,Player player,CancellationToken cancel)
        {
            AttributeRestoreSource found=null;
            int uid=player.Uid;
            int cursor=4;
            while(cursor<=db.Length-8)
            {
                cancel.ThrowIfCancellationRequested();
                int batch=Math.Min(1024*1024,db.Length-7-cursor);
                int end=cursor+batch;
                while(cursor<end)
                {
                    int hit=Array.IndexOf(db,(byte)uid,cursor,end-cursor);
                    if(hit<0){cursor=end;break;}
                    cursor=hit+1;
                    if(Number(db,hit)!=uid || Number(db,hit+4)!=uid) continue;
                    int close=hit-4, index=Number(db,close);
                    if(index<=0) continue;
                    var candidate=FindOpening(db,close,index-1,player,cancel);
                    if(candidate==null) continue;
                    if(found!=null) throw new InvalidDataException("同一 UID 指向多条已匹配的球员记录，无法恢复。");
                    found=candidate;
                }
            }
            if(found==null) throw new InvalidDataException("这份存档里没有找到可核实为同一人的球员。请核对生涯和存档日期，新生成球员可能尚未出现。");
            return found;
        }
        static AttributeRestoreSource FindOpening(byte[] db,int close,int previous,Player player,CancellationToken cancel)
        {
            // Serialized persons are contiguous. Require both the preceding record index
            // and a complete player block plus a unique person's identity inside its bounds.
            int cursor=close-122;
            while(cursor>=0)
            {
                cancel.ThrowIfCancellationRequested();
                int size=Math.Min(65536,cursor+1);
                int first=cursor-size+1;
                while(cursor>=first)
                {
                    int start=Array.LastIndexOf(db,(byte)previous,cursor,cursor-first+1);
                    if(start<0){cursor=first-1;break;}
                    cursor=start-1;
                    if(Number(db,start)!=previous || start+141>close) continue;
                    int openingUid=Number(db,start+4), copy=Number(db,start+8);
                    if(openingUid<=0 || copy<=0 || (openingUid!=copy && (openingUid<2000000000 || (db[start+12]!=2 && db[start+12]!=3)))) continue;
                    int rec=start+19;
                    int ca=BitConverter.ToInt16(db,rec), pa=BitConverter.ToInt16(db,rec+2);
                    if(ca<1 || ca>200 || pa>200 || pa< -100 || !Range(db,rec+24,15,1,20) || !Range(db,rec+39,54,1,100)) return null;
                    // A repeated closing index with two different valid UIDs is ambiguous.
                    for(int p=rec+122;p<=close-12;p++)
                        if(db[p]==(byte)(previous+1) && Number(db,p)==previous+1 && Number(db,p+4)>0 && Number(db,p+4)==Number(db,p+8)) return null;
                    return Identify(db,rec,close,ca,player,cancel);
                }
            }
            return null;
        }
        static AttributeRestoreSource Identify(byte[] db,int rec,int close,int ca,Player player,CancellationToken cancel)
        {
            int identity=-1; DateTime born=default(DateTime); string legal=null;
            for(int p=rec+122;p+30<=close;p++)
            {
                int year=BitConverter.ToUInt16(db,p+2),day=BitConverter.ToUInt16(db,p);
                if(year<1901 || year>2200 || day<1 || day>(DateTime.IsLeapYear(year)?366:365) ||
                   !Range(db,p+15,6,0,0) || !Range(db,p+22,8,1,20)) continue;
                int length=0;
                if(Number(db,p-4)!=0)
                {
                    for(length=2;length<=80 && p-length-19>=rec+100;length++) if(Number(db,p-4-length)==length) break;
                    if(length>80) continue;
                }
                int names=p-length-19;
                if(names<rec+100 || db[names+4]!=0 || db[names+9]!=0 || db[names+14]!=0 ||
                   !NameId(db,names) || !NameId(db,names+5) || !NameId(db,names+10)) continue;
                if(identity>=0) return null;
                identity=names; born=new DateTime(year,1,1).AddDays(day-1);
                if(length>0) legal=Encoding.UTF8.GetString(db,p-length,length);
            }
            if(identity<0) return null;
            string[] resolved=LookupNames(db,identity,cancel);
            string full=string.Join(" ",resolved.Take(2).Where(n=>!string.IsNullOrEmpty(n)));
            string[] source={legal,full,resolved[2]};
            var live=new List<string>{player.Name};
            if(player.NameParts!=null)
                live.AddRange(new[]{player.NameParts.Full,player.NameParts.Common,
                    (player.NameParts.First+" "+player.NameParts.Last).Trim()});
            var keys=new HashSet<string>(live.Where(n=>!string.IsNullOrWhiteSpace(n)).Select(PlayerNames.Normalize));
            bool nameMatches=source.Any(n=>!string.IsNullOrWhiteSpace(n) && keys.Contains(PlayerNames.Normalize(n)));
            bool birthMatches=player.Born.HasValue && player.Born.Value.Date==born;
            if(player.Uid>=2000000000 ? !(birthMatches && nameMatches) : !(birthMatches || nameMatches)) return null;
            var attributes=new byte[54]; Array.Copy(db,rec+39,attributes,0,54);
            return new AttributeRestoreSource{Uid=player.Uid,Ca=ca,Attributes=attributes,Born=born,
                Name=source.FirstOrDefault(n=>!string.IsNullOrWhiteSpace(n))??player.Name};
        }
        static bool NameId(byte[] db,int at) { int id=Number(db,at); return id>=-1 && id<8388608; }
        static string[] LookupNames(byte[] db,int names,CancellationToken cancel)
        {
            var ids=new[]{Number(db,names),Number(db,names+5),Number(db,names+10)};
            var result=new string[3];
            for(int at=0;at<db.Length-28;)
            {
                cancel.ThrowIfCancellationRequested();
                int hit=Array.IndexOf(db,(byte)0x55,at,Math.Min(db.Length-28-at,1024*1024));
                if(hit<0){at+=1024*1024;continue;}
                at=hit+1;
                if(Number(db,hit)!=46421 || Number(db,hit+20)!=0 || db[hit+5]!=0 || db[hit+6]!=0 || db[hit+7]!=0) continue;
                int scale=Number(db,hit+12);
                if(scale<256 || scale>262144 || scale%256!=0 || Number(db,hit+8)!=4*scale || Number(db,hit+16)!=8*scale) continue;
                if(TryReadPools(db,hit+24,ids,result,cancel)) break;
                Array.Clear(result,0,result.Length);
            }
            return result;
        }
        static bool TryReadPools(byte[] db,int p,int[] ids,string[] result,CancellationToken cancel)
        {
            for(int group=0;group<3;group++)
            {
                    if(p>db.Length-4) return false;
                    int count=Number(db,p);p+=4;
                    if(count<0 || count>(db.Length-p)/8) return false;
                    for(int j=0;j<count;j++)
                    {
                        if((j&16383)==0) cancel.ThrowIfCancellationRequested();
                        if(p>db.Length-8 || Number(db,p)!=j) return false;
                        int len=Number(db,p+4); p+=8;
                        if(len<0 || len>1024 || len>db.Length-p) return false;
                        if(j==ids[group]) result[group]=Encoding.UTF8.GetString(db,p,len);
                        p+=len;
                    }
            }
            return true;
        }
    }
}
