// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// Facility ranges are NOT widened. The verified adapter isolates unsupported fields instead of disabling every club.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    enum FacilityStatus { Verified, Zero, OutOfRange, Unreadable, Missing, Ownership, Conflict, Unmapped }
    sealed class FacilityLayout
    {
        public static readonly string[] Titles={"训练设施","青训设施","青训指导","青训招募","商业设施"};
        public readonly string Version;
        public readonly ulong Module,ClubType,FinanceType,DataPointer,FinancePointer;
        public readonly ulong[] DataOffsets,FinanceOffsets;
        public readonly bool Mapped,VerifiedBuild,ProofFailed;
        public readonly NativeEngineLayout Native;
        public FacilityLayout(Func<ulong,int,byte[]> read,string version,ulong module,ulong size,bool verified,bool mapped,
            ulong clubType,ulong financeType,ulong dataPointer,ulong financePointer,ulong[] data,ulong[] finance)
        {
            Version=version;Module=module;ClubType=clubType;FinanceType=financeType;DataPointer=dataPointer;FinancePointer=financePointer;
            DataOffsets=data==null?new ulong[0]:(ulong[])data.Clone();FinanceOffsets=finance==null?new ulong[0]:(ulong[])finance.Clone();Mapped=mapped;
            Native=NativeEngineLayout.For(version,size);
            bool expected=verified && Native!=null && clubType==Native.Club && financeType==Native.Finance &&
                dataPointer==0x100 && financePointer==0x150 && DataOffsets.SequenceEqual(new ulong[]{0x120,0x12B,0x12C,0x12D,0}) && FinanceOffsets.SequenceEqual(new ulong[]{0,0,0,0,Native.CorporateFacility});
            if(expected)
            {
                try{VerifiedBuild=EligibilityLayout.Supported(version,size,read,module);}catch{VerifiedBuild=false;}
                ProofFailed=!VerifiedBuild;
            }
        }
        public static FacilityLayout From(FmProcess fm)
        {
            return new FacilityLayout(fm.Read,fm.Version,fm.Base,fm.ImageSize,fm.VerifiedCurrentBuild,FmLayout.Groups.Length==0 || FmLayout.Groups.Contains("facilities"),
                FmProcess.ClubVtRva,FmProcess.FinanceVtRva,FmLayout.ClubData,FmLayout.ClubFinance,FmLayout.FacilityData,FmLayout.FacilityFin);
        }
        public bool Same(FacilityLayout other)
        {return other!=null && Module==other.Module && Version==other.Version && ClubType==other.ClubType && FinanceType==other.FinanceType &&
            DataPointer==other.DataPointer && FinancePointer==other.FinancePointer && Mapped==other.Mapped && VerifiedBuild==other.VerifiedBuild && ProofFailed==other.ProofFailed &&
            DataOffsets.SequenceEqual(other.DataOffsets) && FinanceOffsets.SequenceEqual(other.FinanceOffsets);}
    }
    sealed class FacilityReadProblem : Exception
    {
        public readonly FacilityStatus Status;
        public FacilityReadProblem(FacilityStatus status,string message):base(message){Status=status;}
    }
    sealed class FacilityField
    {
        public int Index,ClubUid;
        public FacilityStatus Status;
        public string Reason;
        public EligibilityReader Reader;
        public readonly List<KeyValuePair<ulong,byte>> Copies=new List<KeyValuePair<ulong,byte>>();
        public bool Editable {get{return Status==FacilityStatus.Verified;}}
        public int Value {get{return Editable && Copies.Count>0?Copies[0].Value:-1;}}
        public static FacilityField Capture(Func<ulong,int,byte[]> read,FacilityLayout layout,Club club,int index)
        {
            var item=new FacilityField{Index=index,Reader=new EligibilityReader(read)};var r=item.Reader;
            try
            {
                if(!layout.Mapped || index<0 || index>=FacilityLayout.Titles.Length || layout.DataOffsets.Length!=5 || layout.FinanceOffsets.Length!=5 ||
                    layout.DataOffsets[index]==0 && layout.FinanceOffsets[index]==0 || layout.DataOffsets[index]>65535 || layout.FinanceOffsets[index]>65535)
                    throw new FacilityReadProblem(FacilityStatus.Unmapped,"当前配置未映射此设施项目");
                if(layout.ProofFailed)throw new FacilityReadProblem(FacilityStatus.Ownership,"当前构建的原生检查已变化，请重新连接");
                if(layout.VerifiedBuild && layout.Native.Signatures.Any(s=>!r.Bytes(layout.Module+s.Rva,s.Bytes.Length).SequenceEqual(s.Bytes)))
                    throw new FacilityReadProblem(FacilityStatus.Ownership,"当前构建的原生检查已变化");
                if(club==null || r.Ptr(club.Addr)!=layout.Module+layout.ClubType)
                    throw new FacilityReadProblem(FacilityStatus.Ownership,"俱乐部类型未通过检查");
                item.ClubUid=r.Int(club.Addr+12);if(item.ClubUid<=0)throw new FacilityReadProblem(FacilityStatus.Ownership,"俱乐部身份无效");
                // Prefer the finance representation, as the original editor did; validate every mapped copy before writing.
                if(layout.FinanceOffsets[index]!=0)
                {
                    ulong finance=r.Ptr(club.Addr+layout.FinancePointer);
                    if(finance<0x10000)throw new FacilityReadProblem(FacilityStatus.Missing,"未读取到此设施所需的完整财务资料");
                    if(finance!=club.Finance || r.Ptr(finance)!=layout.Module+layout.FinanceType)
                        throw new FacilityReadProblem(FacilityStatus.Ownership,"财务归属或完整财务类型不匹配");
                    ulong at=finance+layout.FinanceOffsets[index];item.Copies.Add(new KeyValuePair<ulong,byte>(at,r.Byte(at)));
                }
                if(layout.DataOffsets[index]!=0)
                {
                    ulong data=r.Ptr(club.Addr+layout.DataPointer);
                    if(data<0x10000)throw new FacilityReadProblem(FacilityStatus.Missing,"未读取到俱乐部设施资料");
                    if(data!=club.Data)throw new FacilityReadProblem(FacilityStatus.Ownership,"俱乐部设施资料归属已变化");
                    ulong at=data+layout.DataOffsets[index];item.Copies.Add(new KeyValuePair<ulong,byte>(at,r.Byte(at)));
                }
                if(!r.Matches(read))throw new FacilityReadProblem(FacilityStatus.Ownership,"设施或所属资料正在变化，请重新读取");
                if(item.Copies.Any(p=>p.Value==0))throw new FacilityReadProblem(FacilityStatus.Zero,"原值为 0，含义未核实，保持只读");
                if(item.Copies.Any(p=>p.Value>20))throw new FacilityReadProblem(FacilityStatus.OutOfRange,"原值超出 1–20（"+string.Join(" / ",item.Copies.Select(p=>p.Value.ToString()).ToArray())+"），保持只读");
                if(item.Copies.Select(p=>p.Key).Distinct().Count()!=item.Copies.Count || item.Copies.Select(p=>p.Value).Distinct().Count()!=1)
                    throw new FacilityReadProblem(FacilityStatus.Conflict,"设施副本不一致（"+string.Join(" / ",item.Copies.Select(p=>p.Value.ToString()).ToArray())+"），保持只读");
                item.Status=FacilityStatus.Verified;item.Reason="已核对当前俱乐部、字段和值";
            }
            catch(FacilityReadProblem ex){item.Status=ex.Status;item.Reason=ex.Message;}
            catch(Exception){item.Status=FacilityStatus.Unreadable;item.Reason="设施资料未能完整读取，保持只读";}
            return item;
        }
    }
    sealed class ClubFacilitySnapshot
    {
        public readonly FacilityLayout Layout;
        public readonly Club Club;
        public readonly FacilityField[] Fields;
        public ClubFacilitySnapshot(Func<ulong,int,byte[]> read,FacilityLayout layout,Club club)
        {Layout=layout;Club=club;Fields=Enumerable.Range(0,5).Select(i=>FacilityField.Capture(read,layout,club,i)).ToArray();}
        public FacilityEditBatch Plan(IDictionary<int,int> requested)
        {
            var fields=new List<FacilityField>();var edits=new List<EligibilityEdit>();
            foreach(var pair in requested.OrderBy(p=>p.Key))
            {
                if(pair.Key<0 || pair.Key>=5 || pair.Value<1 || pair.Value>20)throw new ArgumentOutOfRangeException("requested","设施目标只接受 1–20。");
                var field=Fields[pair.Key];if(!field.Editable)throw new InvalidOperationException(FacilityLayout.Titles[pair.Key]+"："+field.Reason);
                if(field.Value==pair.Value)continue;fields.Add(field);
                foreach(var copy in field.Copies)edits.Add(new EligibilityEdit{Address=copy.Key,Before=new[]{copy.Value},After=new[]{(byte)pair.Value},
                    Label=Club.Name+"："+FacilityLayout.Titles[pair.Key]+" "+copy.Value+" → "+pair.Value+(field.Copies.Count>1?"（同步副本）":"")});
            }
            return new FacilityEditBatch(fields,edits);
        }
    }
    sealed class FacilityEditBatch
    {
        public readonly List<EligibilityEdit> Edits;
        readonly List<FacilityField> fields;
        public FacilityEditBatch(List<FacilityField> fields,List<EligibilityEdit> edits){this.fields=fields;Edits=edits;}
        public bool IdentityMatches(Func<ulong,int,byte[]> read){return fields.All(f=>f.Reader.Matches(read,Edits));}
        public bool Apply(Func<ulong,int,byte[]> read,Func<ulong,byte[],bool> write,Func<bool> extraGuard,out string error)
        {return FacilityTransactions.Apply(read,write,Edits,()=>IdentityMatches(read) && (extraGuard==null || extraGuard()),out error);}
    }
    static class FacilityTransactions
    {
        static bool Equal(byte[] a,byte[] b){return a!=null && b!=null && a.SequenceEqual(b);}
        public static bool Apply(Func<ulong,int,byte[]> read,Func<ulong,byte[],bool> write,IList<EligibilityEdit> edits,Func<bool> identity,out string error)
        {
            if(edits==null || edits.Count==0){error="没有设施改动。";return false;}
            int completed=0;
            Func<bool> guard=()=>identity() && edits.All(e=>{var current=read(e.Address,e.Before.Length);return Equal(current,e.After) || completed<edits.Count && Equal(current,e.Before);});
            Func<ulong,byte[],bool> writeStep=(at,bytes)=>{bool ok=write(at,bytes);if(ok)completed++;return ok;};
            bool result=EligibilityTransaction.Apply(read,writeStep,edits,guard,out error,identity);
            if(error!=null)error=error.Replace("注册资格","设施").Replace("注册","设施");return result;
        }
        public static bool Undo(Func<ulong,int,byte[]> read,Func<ulong,byte[],bool> write,IList<Change> group,out string error)
        {
            error=null;
            if(group==null || group.Count==0 || group.Any(c=>!c.Facility || c.Undone || c.Group<=0) || group.Select(c=>c.Group).Distinct().Count()!=1)
            {error="设施撤销组不完整。";return false;}
            var edits=group.Reverse().Select(c=>new EligibilityEdit{Address=c.Addr,Before=c.New,After=c.Old,Label=c.What}).ToList();
            if(!Apply(read,write,edits,()=>group.All(c=>c.Guard==null || c.Guard()),out error))return false;
            foreach(var change in group)change.Undone=true;return true;
        }
    }
    sealed class FacilityCoverage
    {
        public int Clubs,Complete,Valid;
        readonly int[,] counts=new int[5,8];
        readonly List<string> examples=new List<string>();
        public void Add(ClubFacilitySnapshot snapshot)
        {
            Clubs++;if(snapshot.Fields.All(f=>f.Editable))Complete++;
            foreach(var field in snapshot.Fields)
            {
                counts[field.Index,(int)field.Status]++;if(field.Editable)Valid++;
                else if(examples.Count<5)examples.Add("俱乐部 UID "+field.ClubUid+"，"+FacilityLayout.Titles[field.Index]+"："+field.Reason);
            }
        }
        public bool CanEnable(FacilityLayout layout)
        {return layout.Mapped && !layout.ProofFailed && (layout.VerifiedBuild?Valid>0:Clubs>0 && Complete>=Clubs*0.95);}
        public string BlockedReason(FacilityLayout layout)
        {return !layout.Mapped?"当前配置未映射设施":layout.ProofFailed?"当前构建原生检查已变化":layout.VerifiedBuild?"没有任何通过归属及范围检查的设施项目":"未核实构建的整体设施检查未通过（仍使用 95% 门槛）";}
        public List<string> Report(FacilityLayout layout)
        {
            Func<int,string> n=value=>value.ToString("N0",CultureInfo.InvariantCulture);
            var report=new List<string>{(CanEnable(layout)?"检查通过：":"检查失败：")+"设施："+(layout.VerifiedBuild?"已核实构建，按俱乐部及项目检查":"未核实构建，保留整体 95% 门槛")+
                "；可用 "+n(Valid)+"/"+n(Clubs*5)+" 项，五项完整通过 "+n(Complete)+"/"+n(Clubs)+" 家。"};
            for(int i=0;i<5;i++)report.Add("设施诊断："+FacilityLayout.Titles[i]+"：有效 "+n(counts[i,0])+"，原值0（含义未核实） "+n(counts[i,1])+"，越界 "+n(counts[i,2])+"，读取失败 "+n(counts[i,3])+
                "，资料缺失 "+n(counts[i,4])+"，归属或变化检查失败 "+n(counts[i,5])+"，副本不一致 "+n(counts[i,6])+"，未映射 "+n(counts[i,7])+"。");
            report.AddRange(examples.Select(s=>"设施异常样例："+s));return report;
        }
    }
}
