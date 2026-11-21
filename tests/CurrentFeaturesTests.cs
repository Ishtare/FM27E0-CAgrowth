using System;
using System.Collections.Generic;
using System.Linq;

namespace Fm24Editor
{
    static class CurrentFeaturesTests
    {
        const ulong Module=0x100000000,Obj=0x20000,Person=Obj+0x288,Team=0x30000,ClubAddress=0x40000,Contract=0x50000,Holder=0x60000,List=0x61000,Record=0x62000,InjuryType=0x63000,LoanHolder=0x64000,Loan=0x65000,Finance=0x70000,Data=0x72000;
        sealed class Memory
        {
            public Dictionary<ulong,byte> Bytes=new Dictionary<ulong,byte>();public int Writes,Fail;
            public void Put(ulong a,byte[] b){for(int i=0;i<b.Length;i++)Bytes[a+(ulong)i]=b[i];}
            public void Q(ulong a,ulong b){Put(a,BitConverter.GetBytes(b));}
            public void I(ulong a,int b){Put(a,BitConverter.GetBytes(b));}
            public byte[] Read(ulong a,int n){var b=new byte[n];for(int i=0;i<n;i++)if(!Bytes.TryGetValue(a+(ulong)i,out b[i]))return null;return b;}
            public bool Write(ulong a,byte[] b){Writes++;if(Writes==Fail)return false;Put(a,b);return true;}
        }
        static Player P(){return new Player{Person=Person,Uid=123,Name="Test Player"};}
        static Memory Fixture()
        {
            var m=new Memory();m.Put(Obj,new byte[0x400]);m.Q(Obj,Module+0x4E2B4E8);m.Q(Person,Module+0x4E2BA18);m.I(Person+12,123);
            m.Q(Obj+0x130,Team);m.Put(Team,new byte[0x40]);m.Q(Team,Module+EligibilityLayout.TeamVt);m.Q(Team+0x30,ClubAddress);
            m.Put(ClubAddress,new byte[0x160]);m.Q(ClubAddress,Module+EligibilityLayout.ClubVt);m.I(ClubAddress+12,456);
            m.Q(Obj+0x330,Contract);m.Put(Contract,new byte[0x70]);m.Q(Contract,Module+EligibilityLayout.ContractVt);m.Q(Contract+8,Person);m.Q(Contract+0x10,Team);m.Put(Contract+0x5D,new byte[]{7});
            m.Q(Obj+0xF8,Holder);m.Q(Holder,List);m.Q(Holder+8,List+8);m.Q(Holder+16,List+16);m.Q(List,Record);m.Put(List+8,new byte[8]);m.Put(Record,new byte[0x38]);m.Q(Record+8,InjuryType);m.Q(InjuryType,Module+0x4DB4278);m.Put(Record+0x20,BitConverter.GetBytes(((uint)2026<<16)|194u));
            m.Q(ClubAddress+0x150,Finance);m.Q(ClubAddress+0x100,Data);m.Q(Finance,Module+0x4DF4E38);m.Put(Finance+0x10,BitConverter.GetBytes(-1f));m.I(Data+0xC0,100);
            return m;
        }
        static bool Throws(Action a){try{a();return false;}catch{return true;}}
        public static void Run(Action<bool,string> check)
        {
            var saved=Profile.Fields().ToDictionary(f=>f,f=>f.GetValue(null) is Array?((Array)f.GetValue(null)).Clone():f.GetValue(null));
            try
            {
                CurrentBuildProfile.Apply();
                foreach(int index in new[]{0,1,2})
                    foreach(int value in new[]{0,1,FmLayout.StatusMax[index]/2,FmLayout.StatusMax[index]})
                        check(StatusValues.Decode(StatusValues.Encode(value,index),index)==value,"current status storage round trip");
                check(StatusValues.Encode(20,2).Length==1,"morale edit and freeze values have one-byte storage");
                check(Throws(()=>StatusValues.Encode(21,2)) && Throws(()=>StatusValues.Encode(-1,0)),"status values reject overflow and negative values");
                check(StatusValues.Decode(new byte[]{20,0xA5},2)==-1,"morale decoder rejects a two-byte read");
                var memory=Fixture();memory.Put(Obj+0x26C,new byte[]{8,0xA5});
                var field=CurrentFieldSnapshot.Capture(memory.Read,Module,P(),Obj+0x26C,1);var plan=field.Plan(StatusValues.Encode(20,2),"morale");string error;
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>field.Reader.Matches(memory.Read,plan),out error),"guarded morale edit applies");
                check(memory.Read(Obj+0x26C,2).SequenceEqual(new byte[]{20,0xA5}),"morale edit preserves the adjacent byte");
                var undo=plan.Select(x=>new EligibilityEdit{Address=x.Address,Before=x.After,After=x.Before,Label="undo"}).ToList();
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,undo,()=>field.Reader.Matches(memory.Read,plan),out error) && memory.Read(Obj+0x26C,2).SequenceEqual(new byte[]{8,0xA5}),"morale undo preserves neighbouring state");
                memory.I(Person+12,124);int writes=memory.Writes;
                check(!EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>field.Reader.Matches(memory.Read,plan),out error) && memory.Writes==writes,"changed player identity rejects status edits before writing");
                memory=Fixture();memory.Put(Obj+0x25A,BitConverter.GetBytes((short)-150));field=CurrentFieldSnapshot.Capture(memory.Read,Module,P(),Obj+0x25A,2);plan=field.Plan(BitConverter.GetBytes((short)0),"jaded");
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>field.Reader.Matches(memory.Read,plan),out error) && BitConverter.ToInt16(memory.Read(Obj+0x25A,2),0)==0,"fatigue uses a signed two-byte field");

                memory=Fixture();var injury=CurrentInjurySnapshot.Capture(memory.Read,Module,P());plan=injury.Plan();
                check(injury.Count==1 && plan.Count==1 && plan[0].Address==Holder+8,"healing only changes the injury vector end");
                var baseline=memory.Bytes.ToDictionary(x=>x.Key,x=>x.Value);
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>injury.Reader.Matches(memory.Read,plan),out error),"injury removal applies with a complete read set");
                check(CurrentInjurySnapshot.Capture(memory.Read,Module,P()).Count==0 && memory.Read(Record,0x38).SequenceEqual(Enumerable.Range(0,0x38).Select(i=>baseline[Record+(ulong)i])),"healing does not overwrite or free injury records");
                undo=plan.Select(x=>new EligibilityEdit{Address=x.Address,Before=x.After,After=x.Before,Label="undo"}).ToList();
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,undo,()=>injury.Reader.Matches(memory.Read,plan),out error) && baseline.All(x=>memory.Bytes[x.Key]==x.Value),"healing undo restores the exact list and records");
                memory.Q(Obj+0xF8,Holder+0x80);writes=memory.Writes;
                check(!EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>injury.Reader.Matches(memory.Read,plan),out error) && memory.Writes==writes,"moved medical holder blocks healing");
                memory=Fixture();memory.Q(InjuryType,Module+1);check(Throws(()=>CurrentInjurySnapshot.Capture(memory.Read,Module,P())),"invalid injury type is refused");
                memory=Fixture();memory.Q(Holder+8,List+9);check(Throws(()=>CurrentInjurySnapshot.Capture(memory.Read,Module,P())),"misaligned injury vector is refused");
                memory=Fixture();memory.Q(Obj+0xF8,0);check(CurrentInjurySnapshot.Capture(memory.Read,Module,P()).Count==0,"absent medical holder is healthy and not a read failure");

                memory=Fixture();var shirt=CurrentShirtSnapshot.Capture(memory.Read,Module,P());
                check(shirt!=null && !shirt.Loan && shirt.Address==Contract+0x5D,"own-team shirt uses the main contract");
                memory.Q(Obj+0x130,Team+0x100);memory.Put(Team+0x100,new byte[0x40]);memory.Q(Team+0x100,Module+EligibilityLayout.TeamVt);memory.Q(Team+0x130,ClubAddress);
                check(!CurrentShirtSnapshot.Capture(memory.Read,Module,P()).Loan,"reserve-team assignment at the same club is not mistaken for a loan");
                memory.Q(Contract+0x10,Team+0x200);memory.Put(Team+0x200,new byte[0x40]);memory.Q(Team+0x200,Module+EligibilityLayout.TeamVt);memory.Q(Team+0x230,ClubAddress+0x200);memory.Q(ClubAddress+0x200,Module+EligibilityLayout.ClubVt);
                memory.Q(Obj+0x338,LoanHolder);memory.Q(LoanHolder,Loan);memory.Put(Loan,new byte[0x70]);memory.Q(Loan,Module+0x4ECDCF8);memory.Q(Loan+8,Person);memory.Q(Loan+0x10,Team);memory.Put(Loan+0x5D,new byte[]{12});
                memory.Q(LoanHolder,0);check(CurrentShirtSnapshot.Capture(memory.Read,Module,P())==null,"empty loan holder is unavailable and cannot be mistaken for a writable contract");memory.Q(LoanHolder,Loan);
                shirt=CurrentShirtSnapshot.Capture(memory.Read,Module,P());plan=shirt.Plan(27);
                check(shirt.Loan && shirt.Address==Loan+0x5D,"loan shirt uses the borrowing contract even when assigned to reserves");
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>shirt.Reader.Matches(memory.Read,plan),out error) && memory.Read(Loan+0x5D,1)[0]==27 && memory.Read(Contract+0x5D,1)[0]==7,"loan number editing preserves the parent contract number");
                check(Throws(()=>shirt.Plan(100)) && Throws(()=>shirt.Plan(0)) && shirt.Plan(255)[0].After[0]==255,"shirt number range supports only 1–99 and unset");
                memory.Q(LoanHolder,Loan+0x80);writes=memory.Writes;undo=plan.Select(x=>new EligibilityEdit{Address=x.Address,Before=x.After,After=x.Before,Label="undo"}).ToList();
                check(!EligibilityTransaction.Apply(memory.Read,memory.Write,undo,()=>shirt.Reader.Matches(memory.Read,plan),out error) && memory.Writes==writes,"changed loan holder blocks stale undo");

                memory=Fixture();var club=new Club{Addr=ClubAddress,Finance=Finance,Data=Data};var ticket=CurrentTicketSnapshot.Capture(memory.Read,Module,club);
                check(ticket.Price==-1 && ticket.Plan(null,null).Count==0,"unset ticket price is a valid unchanged state");
                plan=ticket.Plan(150,25f);baseline=memory.Bytes.ToDictionary(x=>x.Key,x=>x.Value);
                memory.Fail=2;
                check(!EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>ticket.Reader.Matches(memory.Read,plan),out error) && baseline.All(x=>memory.Bytes[x.Key]==x.Value),"ticket batch failure rolls back the first field");
                memory.Fail=0;
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>ticket.Reader.Matches(memory.Read,plan),out error),"unset price can be set with the season-ticket count");
                check(BitConverter.ToSingle(memory.Read(Finance+0x10,4),0)==25 && BitConverter.ToInt32(memory.Read(Data+0xC0,4),0)==150,"ticket plan updates only the requested fields");
                undo=plan.Select(x=>new EligibilityEdit{Address=x.Address,Before=x.After,After=x.Before,Label="undo"}).ToList();
                check(EligibilityTransaction.Apply(memory.Read,memory.Write,undo,()=>ticket.Reader.Matches(memory.Read,plan),out error) && baseline.All(x=>memory.Bytes[x.Key]==x.Value),"ticket undo restores the unset sentinel");
                check(Throws(()=>ticket.Plan(-1,null)) && Throws(()=>ticket.Plan(null,float.NaN)) && Throws(()=>ticket.Plan(null,float.PositiveInfinity)) && Throws(()=>ticket.Plan(null,-1f)) && Throws(()=>ticket.Plan(null,2001f)),"ticket values reject invalid counts, non-finite values and new sentinels");
                memory.Q(ClubAddress+0x150,Finance+0x100);writes=memory.Writes;
                check(!EligibilityTransaction.Apply(memory.Read,memory.Write,plan,()=>ticket.Reader.Matches(memory.Read,plan),out error) && memory.Writes==writes,"finance reallocation prevents stale ticket writes");
            }
            finally{foreach(var f in saved)f.Key.SetValue(null,f.Value);}

            var limits=new Dictionary<string,string>{{"status","not mapped for FM"},{"injury","not mapped for FM"},{"jaded","not mapped for FM"},{"squadnumber","租借合同包含有效球衣号码"},{"tickets","季票数量和票价有效"}};
            using(var fm=new FmProcess{Version=CurrentBuildProfile.Build,ImageSize=CurrentBuildProfile.ImageSize,VerifiedCurrentBuild=true})
            {
                string report=AttachReport.Build(fm.Version,false,new ScanResult(),limits,false);
                check(report.Contains("尚未适配当前构建") && report.Contains("以下项目检查未通过") && !report.Contains("not mapped"),"mixed reports classify unmapped features separately in Chinese");
                check(!AttachReport.ShouldPopup(fm,limits),"known partial limits do not repeatedly open a failure-style popup");
                limits["contract"]="new contract failure";check(AttachReport.ShouldPopup(fm,limits),"new contract failures still trigger a report");limits.Remove("contract");
                limits["core"]="0 players";check(AttachReport.ShouldPopup(fm,limits),"core failures always trigger a report");limits.Remove("core");
                fm.VerifiedCurrentBuild=false;check(AttachReport.ShouldPopup(fm,limits),"unverified engines do not inherit current known-limit suppression");
            }
        }
    }
}
