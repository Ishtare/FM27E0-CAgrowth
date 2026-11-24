// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt. Evidence: docs/registration-evidence.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    sealed class EligibilityEdit
    {
        public ulong Address;
        public byte[] Before,After;
        public string Label;
    }

    // A complete read set, including pointer paths, so a preview cannot silently change owners.
    sealed class EligibilityReader
    {
        readonly Func<ulong,int,byte[]> read;
        public readonly Dictionary<Tuple<ulong,int>,byte[]> Reads=new Dictionary<Tuple<ulong,int>,byte[]>();
        public EligibilityReader(Func<ulong,int,byte[]> reader){read=reader;}
        public byte[] Bytes(ulong address,int length)
        {
            if(address<0x10000 || address>0x7FFFFFFFFFFFUL || length<0 || length>65536 || address+(ulong)length>0x800000000000UL)
                throw new InvalidDataException("注册数据地址或长度异常。");
            if(length==0)return new byte[0];
            var result=read(address,length);
            if(result==null || result.Length!=length)throw new InvalidDataException("注册数据未能完整读取，请重新扫描。");
            var key=Tuple.Create(address,length);byte[] old;
            if(Reads.TryGetValue(key,out old) && !old.SequenceEqual(result))throw new InvalidDataException("游戏正在更新注册数据，请暂停后重新打开。");
            Reads[key]=(byte[])result.Clone();return result;
        }
        public ulong Ptr(ulong at){return BitConverter.ToUInt64(Bytes(at,8),0);}
        public int Int(ulong at){return BitConverter.ToInt32(Bytes(at,4),0);}
        public byte Byte(ulong at){return Bytes(at,1)[0];}
        public ushort Short(ulong at){return BitConverter.ToUInt16(Bytes(at,2),0);}
        public ulong[] Vector(ulong header,int stride)
        {
            ulong b=Ptr(header),e=Ptr(header+8);
            if(b==0 && e==0)return new ulong[0];
            if(b<0x10000 || e<b || (e-b)%(ulong)stride!=0 || e-b>(ulong)stride*4096)throw new InvalidDataException("注册规则列表结构异常。");
            return Enumerable.Range(0,(int)((e-b)/(ulong)stride)).Select(i=>b+(ulong)(i*stride)).ToArray();
        }
        public ulong[] Pointers(ulong header){return Vector(header,8).Select(Ptr).ToArray();}
        public bool Matches(Func<ulong,int,byte[]> reader,IList<EligibilityEdit> changed=null)
        {
            try
            {
                foreach(var item in Reads)
                {
                    ulong at=item.Key.Item1;int n=item.Key.Item2;
                    if(changed!=null && changed.Any(w=>at<w.Address+(ulong)w.After.Length && w.Address<at+(ulong)n))continue;
                    var current=reader(at,n);if(current==null || !current.SequenceEqual(item.Value))return false;
                }
                return true;
            }
            catch{return false;}
        }
    }

    static class EligibilityTransaction
    {
        static bool Equal(byte[] a,byte[] b){return a!=null && b!=null && a.SequenceEqual(b);}
        public static bool Apply(Func<ulong,int,byte[]> read,Func<ulong,byte[],bool> write,IList<EligibilityEdit> plan,Func<bool> identity,out string error,Func<bool> recoveryIdentity=null)
        {
            error=null;var applied=new List<EligibilityEdit>();
            try
            {
                var ordered=plan.OrderBy(w=>w.Address).ToArray();
                for(int i=0;i<ordered.Length;i++)
                {
                    var w=ordered[i];
                    if(w.Before==null || w.After==null || w.Before.Length!=w.After.Length || w.After.Length==0 ||
                       w.Address<0x10000 || w.Address>0x7FFFFFFFFFFFUL || w.Address+(ulong)w.After.Length>0x800000000000UL ||
                       (i>0 && ordered[i-1].Address+(ulong)ordered[i-1].After.Length>w.Address))
                        throw new InvalidDataException("注册修改计划包含无效或重叠数据。");
                }
                if(!identity() || plan.Any(w=>!Equal(read(w.Address,w.Before.Length),w.Before)))throw new InvalidOperationException("注册预览已失效，请重新打开。");
                foreach(var w in plan)
                {
                    if(!identity() || !Equal(read(w.Address,w.Before.Length),w.Before))throw new InvalidOperationException("游戏已更新注册数据，本次停止修改。");
                    applied.Add(w);
                    if(!write(w.Address,w.After) || !Equal(read(w.Address,w.After.Length),w.After))throw new IOException("注册资格修改未能完整写入。");
                }
                if(!identity() || plan.Any(w=>!Equal(read(w.Address,w.After.Length),w.After)))
                    throw new InvalidOperationException("数据在写入结束时发生变化，本次停止修改。");
                return true;
            }
            catch(Exception ex)
            {
                bool restored=true;
                for(int i=applied.Count-1;i>=0;i--)
                {
                    var edit=applied[i];
                    try
                    {
                        var current=read(edit.Address,edit.Before.Length);
                        if(Equal(current,edit.Before))continue;
                        if(!Equal(current,edit.After) || !(recoveryIdentity??identity)() ||
                           !write(edit.Address,edit.Before) || !Equal(read(edit.Address,edit.Before.Length),edit.Before))
                            restored=false;
                    }
                    catch { restored=false; }
                }
                error=ex.Message+(restored?" 本次修改已取消。":" 部分数据无法恢复，请重载修改前的存档。");return false;
            }
        }
    }

    sealed class EligibilityLayout
    {
        public const string Build="27.0.1+2503222";
        public const ulong ImageSize=0x185FA000;
        public static ulong PersonVt{get{return CurrentBuildProfile.Active.Person;}}
        public static ulong PlayerVt{get{return CurrentBuildProfile.Active.Player;}}
        public static ulong TeamVt{get{return CurrentBuildProfile.Active.Team;}}
        public static ulong ClubVt{get{return CurrentBuildProfile.Active.Club;}}
        public static ulong NationVt{get{return CurrentBuildProfile.Active.Nation;}}
        public static ulong ContractVt{get{return CurrentBuildProfile.Active.Contract;}}
        public static ulong CompetitionVt{get{return CurrentBuildProfile.Active.Competition;}}
        public static ulong RulesHolderVt{get{return CurrentBuildProfile.Active.RulesHolder;}}
        public static ulong RulesContainerVt{get{return CurrentBuildProfile.Active.RulesContainer;}}
        public static ulong NonEuCheck{get{return CurrentBuildProfile.Active.NonEuCheck;}}
        public static readonly byte[] NonEuSignature={0xF6,0x81,0x78,0x02,0x00,0x00,0x08};
        public static bool Supported(string version,ulong imageSize,Func<ulong,int,byte[]> read,ulong moduleBase)
        {
            return CurrentBuildProfile.Supported(version,imageSize,read,moduleBase);
        }
        public static ulong CaptureDate(EligibilityReader r,ulong moduleBase)
        {
            return CurrentBuildProfile.Active.CaptureDate(r,moduleBase);
        }
    }

    sealed class EligibilityRow
    {
        public byte[] Raw;
        public ulong Target {get{return BitConverter.ToUInt64(Raw,0);}}
        public byte Kind {get{return Raw[10];}}
        public byte Type {get{return Raw[11];}}
        public byte Code {get{return Raw[12];}}
    }

    sealed class PlayerEligibilitySnapshot
    {
        public EligibilityReader Reader;
        public ulong PlayerObject,Person,Team,Club,Nation,Holder,Begin,End,Capacity,DateAddress;
        public int Uid,ClubUid,NationUid;
        public byte ItalyFlags;
        public byte[] Records;
        public readonly List<EligibilityRow> Rows=new List<EligibilityRow>();
        public bool Italian {get{return NationUid==776;}}
        public bool ItalyNonEu {get{return (ItalyFlags&8)!=0;}}
        public DateTime Today;

        public static PlayerEligibilitySnapshot Capture(Func<ulong,int,byte[]> read,ulong moduleBase,Player player)
        {
            if(player.Extra!=0)throw new InvalidOperationException("球员兼职员的注册结构尚未核实，本功能暂不写入。");
            var s=new PlayerEligibilitySnapshot {Reader=new EligibilityReader(read),Person=player.Person,PlayerObject=player.Person-0x288,Uid=player.Uid};
            var r=s.Reader;
            if(r.Ptr(s.Person)!=moduleBase+EligibilityLayout.PersonVt || r.Ptr(s.PlayerObject)!=moduleBase+EligibilityLayout.PlayerVt)
                throw new InvalidDataException("球员类型与已核实版本不符，请重新扫描。");
            if(r.Int(s.Person+12)!=player.Uid || r.Int(s.Person+8)<0)throw new InvalidDataException("球员身份已变化，请重新扫描。");
            s.ItalyFlags=r.Byte(s.PlayerObject+0x278);
            s.Team=r.Ptr(s.PlayerObject+0x130);
            if(r.Ptr(s.Team)!=moduleBase+EligibilityLayout.TeamVt)throw new InvalidDataException("球员当前球队未通过检查。");
            s.Club=r.Ptr(s.Team+0x30);
            if(r.Ptr(s.Club)!=moduleBase+EligibilityLayout.ClubVt)throw new InvalidDataException("球员俱乐部未通过检查。");
            s.ClubUid=r.Int(s.Club+12);s.Nation=r.Ptr(s.Club+0xD8);
            if(r.Ptr(s.Nation)!=moduleBase+EligibilityLayout.NationVt)throw new InvalidDataException("俱乐部所属国家未通过检查。");
            s.NationUid=r.Int(s.Nation+12);
            ulong contract=r.Ptr(s.PlayerObject+0x330);
            if(r.Ptr(contract)!=moduleBase+EligibilityLayout.ContractVt || r.Ptr(contract+8)!=s.Person || r.Ptr(contract+0x10)!=s.Team)
                throw new InvalidDataException("当前合同或球队与球员不一致，租借及临时球队情形暂不修改。");
            s.Holder=r.Ptr(s.PlayerObject+0x300);
            if(s.Holder<0x10000)throw new InvalidDataException("没有可核实的球员注册资料列表。");
            s.Begin=r.Ptr(s.Holder);s.End=r.Ptr(s.Holder+8);s.Capacity=r.Ptr(s.Holder+16);
            bool empty=s.Begin==0 && s.End==0 && s.Capacity==0;
            if(!empty && (s.Begin<0x10000 || s.End<s.Begin || s.Capacity<s.End || (s.End-s.Begin)%16!=0 ||
                (s.Capacity-s.Begin)%16!=0 || s.Capacity-s.Begin>16*1024))throw new InvalidDataException("球员注册资料列表结构异常。");
            s.Records=empty?new byte[0]:r.Bytes(s.Begin,(int)(s.End-s.Begin));
            for(int at=0;at<s.Records.Length;at+=16)
                s.Rows.Add(new EligibilityRow {Raw=s.Records.Skip(at).Take(16).ToArray()});
            s.DateAddress=EligibilityLayout.CaptureDate(r,moduleBase);
            s.Today=RetirementSnapshot.Decode(BitConverter.ToUInt32(r.Bytes(s.DateAddress,4),0));
            if(!r.Matches(read))throw new InvalidDataException("游戏正在更新球员注册资料，请暂停后重试。");
            return s;
        }
        bool Own(EligibilityRow row,byte code){return row.Kind==4 && row.Target==Team && row.Code==code;}
        public bool Registered(byte code){return Rows.Any(row=>Own(row,code) && row.Type==0x19);}
        public int Unregistrations(byte code){return Rows.Count(row=>Own(row,code) && row.Type==0x1B);}
        public List<EligibilityEdit> Plan(Func<ulong,int,byte[]> read,bool clearItaly,byte? registerCode,bool removeUnregistration)
        {
            if(registerCode.HasValue && !CurrentBuildProfile.Active.PlayerRegistrationEditing)
                throw new InvalidOperationException("当前构建的国内赛事与欧战个人登记仅可查看。");
            if(registerCode.HasValue && registerCode!=0 && registerCode!=8)throw new ArgumentException("仅支持国内赛事或欧战注册类型。");
            if(clearItaly && !Italian)throw new InvalidOperationException("所选俱乐部不属于意大利，不能用意大利状态处理其他国家的配额。");
            if(!Reader.Matches(read))throw new InvalidOperationException("球员注册预览已失效，请重新打开。");
            var plan=new List<EligibilityEdit>();
            if(clearItaly && ItalyNonEu)
                plan.Add(new EligibilityEdit {Address=PlayerObject+0x278,Before=new[]{ItalyFlags},After=new[]{(byte)(ItalyFlags&0xF7)},Label="取消意大利非欧认定（保留原国籍）"});
            if(registerCode.HasValue)
            {
                byte code=registerCode.Value;
                var rows=Rows.Where(row=>!removeUnregistration || !Own(row,code) || row.Type!=0x1B).Select(row=>(byte[])row.Raw.Clone()).ToList();
                if(!Registered(code))
                {
                    var row=new byte[16];BitConverter.GetBytes(Team).CopyTo(row,0);row[10]=4;row[11]=0x19;row[12]=code;row[15]=0xFF;rows.Add(row);
                }
                int newLength=rows.Count*16;
                if(Begin<0x10000 || Begin+(ulong)newLength>Capacity)
                    throw new InvalidOperationException("现有注册列表没有空位。此场景不能新增记录；可单独取消非欧认定后在 FM 中正常报名。");
                int length=Math.Max(newLength,Records.Length);
                if(length>0)
                {
                    var before=read(Begin,length);if(before==null || before.Length!=length)throw new InvalidDataException("注册列表空位无法读取。");
                    var after=(byte[])before.Clone();for(int i=0;i<rows.Count;i++)rows[i].CopyTo(after,i*16);
                    if(!after.SequenceEqual(before))plan.Add(new EligibilityEdit {Address=Begin,Before=before,After=after,Label=(code==8?"国内赛事":"欧战")+"注册记录修复"});
                }
                ulong newEnd=Begin+(ulong)newLength;
                if(newEnd!=End)plan.Add(new EligibilityEdit {Address=Holder+8,Before=BitConverter.GetBytes(End),After=BitConverter.GetBytes(newEnd),Label="更新所选注册记录数量"});
            }
            return plan;
        }
    }
}
