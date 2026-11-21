// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    static class EligibilityTests
    {
        sealed class Memory
        {
            public readonly Dictionary<ulong,byte> Data=new Dictionary<ulong,byte>();
            public int Writes,FailWrite;
            public byte[] Read(ulong at,int n){var b=new byte[n];for(int i=0;i<n;i++)if(!Data.TryGetValue(at+(ulong)i,out b[i]))return null;return b;}
            public void Put(ulong at,byte[] bytes){for(int i=0;i<bytes.Length;i++)Data[at+(ulong)i]=bytes[i];}
            public void Q(ulong at,ulong v){Put(at,BitConverter.GetBytes(v));}
            public void I(ulong at,int v){Put(at,BitConverter.GetBytes(v));}
            public bool Write(ulong at,byte[] bytes){Writes++;if(Writes==FailWrite)return false;Put(at,bytes);return true;}
            ulong next=0x2000000;
            public ulong Allocate(int n){ulong at=next;next+=(ulong)(n+32);Put(at,new byte[n]);return at;}
            public void Vector(ulong at,byte[] bytes,int spare=0){ulong b=Allocate(bytes.Length+spare);Put(b,bytes);Q(at,b);Q(at+8,b+(ulong)bytes.Length);Q(at+16,b+(ulong)bytes.Length+(ulong)spare);}
            public ulong Node(params KeyValuePair<string,ulong>[] values)
            {
                ulong node=Allocate(16),header=Allocate(24);Q(node+8,header);Dictionary(header,values);return node;
            }
            public void Dictionary(ulong header,params KeyValuePair<string,ulong>[] values)
            {
                var rows=new byte[values.Length*16];
                for(int i=0;i<values.Length;i++){Encoding.ASCII.GetBytes(values[i].Key).CopyTo(rows,i*16);BitConverter.GetBytes(values[i].Value).CopyTo(rows,i*16+8);}
                Vector(header,rows);
            }
            public ulong List(params ulong[] values){ulong h=Allocate(24);Vector(h,values.SelectMany(BitConverter.GetBytes).ToArray());return h;}
        }
        const ulong Base=0x10000000,Obj=0x300000,Team=0x400000,Club=0x500000,Nation=0x600000,Contract=0x700000,Holder=0x800000,Manager=0x900000;
        static ulong Person {get{return Obj+0x288;}}
        static Player Player(){return new Player {Person=Person,Uid=123,Name="Test Player"};}
        static void Date(Memory m)
        {
            var layout=CurrentBuildProfile.Active;
            if(layout.DirectDate!=0)m.Put(Base+layout.DirectDate,BitConverter.GetBytes((2027u<<16)|256u));
            else{m.Q(Base+layout.DateRoot,Manager);m.Q(Manager,Base+layout.DateType);m.Put(Manager+0x7DC,BitConverter.GetBytes((2027u<<16)|256u));}
        }
        static byte[] Row(ulong target,byte kind,byte type,byte code,byte flags=0)
        {var r=new byte[16];BitConverter.GetBytes(target).CopyTo(r,0);r[10]=kind;r[11]=type;r[12]=code;r[13]=flags;r[15]=255;return r;}
        static Memory PlayerMemory(byte flag,byte[][] rows,int spare=0)
        {
            var m=new Memory();m.Q(Obj,Base+EligibilityLayout.PlayerVt);m.Q(Person,Base+EligibilityLayout.PersonVt);m.I(Person+8,42);m.I(Person+12,123);
            m.Put(Obj+0x278,new[]{flag});m.Q(Obj+0x130,Team);m.Q(Team,Base+EligibilityLayout.TeamVt);m.Q(Team+0x30,Club);
            m.Q(Club,Base+EligibilityLayout.ClubVt);m.I(Club+12,456);m.Q(Club+0xD8,Nation);m.Q(Nation,Base+EligibilityLayout.NationVt);m.I(Nation+12,776);
            m.Q(Obj+0x330,Contract);m.Q(Contract,Base+EligibilityLayout.ContractVt);m.Q(Contract+8,Person);m.Q(Contract+0x10,Team);
            m.Q(Obj+0x300,Holder);m.Vector(Holder,rows.SelectMany(x=>x).ToArray(),spare);Date(m);return m;
        }
        static bool Same(Memory m,Dictionary<ulong,byte> before){return before.All(k=>m.Data[k.Key]==k.Value);}
        static bool Apply(Memory m,EligibilityReader snapshot,List<EligibilityEdit> plan,out string error)
        {return EligibilityTransaction.Apply(m.Read,m.Write,plan,()=>snapshot.Matches(m.Read,plan),out error);}
        public static void RunCountryCases(Action<bool,string> check)
        {
            foreach(int country in new[]{776,796,769,771,765,9999})
            {
                var foreign=Row(0xABCDEF,4,0x19,8,0x31);var m=PlayerMemory(0xAD,new[]{Row(Team,4,0x1B,8),foreign},32);m.I(Nation+12,country);
                var snapshot=PlayerEligibilitySnapshot.Capture(m.Read,Base,Player());var plan=snapshot.Plan(m.Read,false,8,true);string error;
                check(plan.All(edit=>edit.Address!=Obj+0x278),"common player registration does not alter any national historical marker: "+country);
                check(Apply(m,snapshot.Reader,plan,out error),"verified common player records are editable in each club country: "+country);
                var after=PlayerEligibilitySnapshot.Capture(m.Read,Base,Player());
                check(after.Registered(8) && after.Unregistrations(8)==0 && after.Rows.Any(row=>row.Raw.SequenceEqual(foreign)),"repair is limited to the selected player's current team and domestic type: "+country);
                check(m.Read(Obj+0x278,1)[0]==0xAD,"non-EU historical state is preserved by a generic registration repair: "+country);
                check(RegistrationCountrySupport.VerifiedHistoricalFlag(country)==(country==776),"an Italy-specific bit is never presented as a verified Spanish/French/German flag: "+country);
                if(country!=776)
                {
                    bool refused=false;try{after.Plan(m.Read,true,null,false);}catch{refused=true;}
                    check(refused,"unknown national history markers are refused before any write: "+country);
                }
            }
        }
        static KeyValuePair<string,ulong> Pair(string key,ulong value){return new KeyValuePair<string,ulong>(key,value);}
        static List<EligibilityEdit> Reverse(List<EligibilityEdit> edits){return edits.AsEnumerable().Reverse().Select(e=>new EligibilityEdit {Address=e.Address,Before=e.After,After=e.Before,Label="undo"}).ToList();}
        static Memory RuleMemory(out EligibilityCompetition competition,out ulong primary,out ulong[] copies,int code=0x12,bool minimum=false)
        {
            var m=new Memory();Date(m);competition=new EligibilityCompetition {Address=0xA00000,Uid=9999,Name="Test League"};
            ulong holder=0xA10000,common=0xA20000,groups=0xA30000,group=0xA40000,root=0xA50000;
            m.Q(competition.Address,Base+EligibilityLayout.CompetitionVt);m.I(competition.Address+12,competition.Uid);m.Q(competition.Address+0xB0,holder);m.Q(competition.Address+0x60,Nation);
            m.Q(Nation,Base+EligibilityLayout.NationVt);m.Q(Nation+0x108,root);m.Q(root,Base+EligibilityLayout.RulesContainerVt);
            m.Q(holder,Base+EligibilityLayout.RulesHolderVt);m.Put(holder+0x31,new byte[]{0});m.Q(holder+0x370,common);m.Q(common+0x1E0,groups);
            m.Vector(groups+0x60,BitConverter.GetBytes(group));m.Put(group+0x120,new byte[]{0});
            primary=m.Allocate(32);m.I(primary+0x18,3);m.Put(primary+0x1C,new byte[]{(byte)code,0,(byte)(minimum?1:255),2});m.Q(group+0x70,m.List(primary));
            var copyAddresses=new List<ulong>();
            for(int s=0;s<2;s++)
            {
                ulong id=m.Allocate(16);m.I(id+8,competition.Uid);ulong identity=m.Node(Pair("pmoc",id));
                ulong type=m.Allocate(16),value=m.Allocate(16);m.I(type+8,code);m.I(value+8,3);copyAddresses.Add(value+8);
                ulong rule=m.Node(Pair("epyt",type),Pair("ulav",value)),ruleList=m.Allocate(16);m.Q(ruleList+8,m.List(rule));
                ulong entry=m.Node(Pair("pmoc",identity),Pair("rsqs",ruleList));
                ulong otherId=m.Allocate(16);m.I(otherId+8,7777);ulong otherIdentity=m.Node(Pair("pmoc",otherId));
                ulong other=m.Node(Pair("pmoc",otherIdentity),Pair("rsqs",ruleList));
                ulong list=m.Allocate(16);m.Q(list+8,m.List(entry,other));
                if(s==0)m.Dictionary(root+0x570,Pair("lrxf",list));
                else {ulong source=m.Node(Pair("lrxf",list));m.Vector(root+0x588,BitConverter.GetBytes(source));}
            }
            copies=copyAddresses.ToArray();return m;
        }
        public static void RunSelectedBuildRules(Action<bool,string> check)
        {
            foreach(int code in LeagueEligibilitySnapshot.Labels.Keys)
            {
                EligibilityCompetition competition;ulong primary;ulong[] copies;string error;
                var memory=RuleMemory(out competition,out primary,out copies,code,LeagueEligibilitySnapshot.MinimumCodes.Contains(code));
                var snapshot=LeagueEligibilitySnapshot.Capture(memory.Read,Base,competition,null,code);
                int target=snapshot.IsMinimum?0:25;var plan=snapshot.Plan(memory.Read,target);
                check(snapshot.RuleCode==code && LeagueEligibilitySnapshot.Codes(memory.Read,Base,competition).Contains(code),"current build exposes only the actual mapped competition rule");
                check(Apply(memory,snapshot.Reader,plan,out error) && snapshot.Addresses.All(a=>BitConverter.ToInt32(memory.Read(a,4),0)==target),"selected build changes the rule and both source copies atomically");
                var reverse=Reverse(plan);check(Apply(memory,snapshot.Reader,reverse,out error) && snapshot.Addresses.All(a=>BitConverter.ToInt32(memory.Read(a,4),0)==3),"current-build quota undo restores all rule copies");
            }
        }
        public static void Run(Action<bool,string> check)
        {
            Action<Action,string> throws=(action,message)=>{bool bad=false;try{action();}catch{bad=true;}check(bad,message);};
            string error;
            for(int flag=0;flag<256;flag++)
            {
                var m=PlayerMemory((byte)flag,new[]{Row(Team,4,0x19,8),Row(Nation,8,9,100,80)});
                var s=PlayerEligibilitySnapshot.Capture(m.Read,Base,Player());var original=m.Data.ToDictionary(x=>x.Key,x=>x.Value);
                var edits=s.Plan(m.Read,true,8,true);
                check(edits.Count==((flag&8)==0?0:1),"Italian repair does not duplicate an existing domestic registration");
                check(Apply(m,s.Reader,edits,out error),"Italian repair succeeds for every unrelated flag combination");
                check(m.Read(Obj+0x278,1)[0]==(byte)(flag&0xF7),"Italian repair changes only the documented bit");
                check(original.All(x=>x.Key==Obj+0x278 || m.Data[x.Key]==x.Value),"Italian repair preserves nationality and every other captured byte");
                var reverse=Reverse(edits);check(Apply(m,s.Reader,reverse,out error) && Same(m,original),"Italian flag repair undo is exact");
            }
            var preserved=Row(0xB00000,3,0xF1,73,42);var away=Row(0xC00000,4,0x1B,8);
            var mem=PlayerMemory(0x88,new[]{preserved,away,Row(Team,4,0x19,0),Row(Team,4,0x1B,8)});
            var snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());var before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);
            var plan=snapshot.Plan(mem.Read,true,8,true);
            check(Apply(mem,snapshot.Reader,plan,out error),"full relation vector can replace the selected unregistration with a registration");
            var after=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());
            check(after.Registered(8) && after.Registered(0) && after.Unregistrations(8)==0,"domestic repair preserves UEFA and removes only the selected blocker");
            check(after.Rows.Any(x=>x.Raw.SequenceEqual(preserved)) && after.Rows.Any(x=>x.Raw.SequenceEqual(away)),"unknown and other-team relationship records survive byte-identically");
            check(Apply(mem,snapshot.Reader,Reverse(plan),out error) && Same(mem,before),"compound flag and registration undo restores the exact original vector");
            mem=PlayerMemory(0x88,new[]{preserved,Row(Team,4,0x19,8),Row(Team,4,0x1B,8),Row(Team,4,0x1B,8)});
            snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);plan=snapshot.Plan(mem.Read,false,8,true);
            check(Apply(mem,snapshot.Reader,plan,out error),"conflicting duplicate unregistration records are removed together");
            after=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());
            check(after.Rows.Count==2 && after.Registered(8) && after.Unregistrations(8)==0 && after.ItalyNonEu,"registration repair alone preserves the non-EU flag");
            check(Apply(mem,snapshot.Reader,Reverse(plan),out error) && Same(mem,before),"vector compaction and end-pointer undo are exact");
            mem=PlayerMemory(0x88,new[]{preserved});snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());
            throws(()=>snapshot.Plan(mem.Read,true,8,true),"a full vector never triggers an unowned allocation or partial flag repair");
            check(mem.Writes==0 && snapshot.Plan(mem.Read,true,null,false).Count==1,"flag-only repair remains available without vector capacity");
            mem=PlayerMemory(0,new[]{preserved},16);snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());plan=snapshot.Plan(mem.Read,false,8,true);
            check(Apply(mem,snapshot.Reader,plan,out error) && PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player()).Registered(8),"an existing verified free slot can receive one registration");
            throws(()=>snapshot.Plan(mem.Read,false,19,true),"unverified registration codes cannot be written");
            mem=PlayerMemory(0x88,new[]{preserved,Row(Team,4,0x1B,8)});snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);plan=snapshot.Plan(mem.Read,true,8,true);mem.FailWrite=2;
            check(!Apply(mem,snapshot.Reader,plan,out error) && Same(mem,before),"failure during compound registration repair rolls back the flag and vector");
            mem=PlayerMemory(0x88,new[]{preserved});snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());mem.I(Person+12,124);
            throws(()=>snapshot.Plan(mem.Read,true,null,false),"changed player identity invalidates the preview");
            check(mem.Writes==0,"stale preview never writes");
            throws(()=>PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player()),"capture rejects reused player objects");
            mem=PlayerMemory(0x88,new[]{preserved});snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());mem.I(Manager+0x7DC,(2027<<16)|257);
            throws(()=>snapshot.Plan(mem.Read,true,null,false),"day advance invalidates an old eligibility preview");
            mem=PlayerMemory(0x88,new[]{preserved});mem.I(Nation+12,765);snapshot=PlayerEligibilitySnapshot.Capture(mem.Read,Base,Player());
            throws(()=>snapshot.Plan(mem.Read,true,null,false),"Italian exemption cannot be applied to another nation's quota");
            var coach=Player();coach.Extra=0xF8;throws(()=>PlayerEligibilitySnapshot.Capture(mem.Read,Base,coach),"unverified player-coach layout is refused");
            mem.Put(Base+EligibilityLayout.NonEuCheck,EligibilityLayout.NonEuSignature);
            check(EligibilityLayout.Supported(EligibilityLayout.Build,EligibilityLayout.ImageSize,mem.Read,Base),"verified build requires native signature");
            check(!EligibilityLayout.Supported("27.0.1+other",EligibilityLayout.ImageSize,mem.Read,Base),"unknown builds stay read-only");
            mem.Put(Base+EligibilityLayout.NonEuCheck,new byte[7]);check(!EligibilityLayout.Supported(EligibilityLayout.Build,EligibilityLayout.ImageSize,mem.Read,Base),"altered native code cannot authorize a layout");
            EligibilityCompetition competition;ulong primary;ulong[] copies;
            mem=RuleMemory(out competition,out primary,out copies);
            var rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null);
            check(rules.Limit==3 && rules.Addresses.Length==3 && rules.SharedIds.Contains(7777),"quota preview finds three equal copies and shared competition identity");
            check(mem.Read(primary+0x1F,1)[0]==2,"direction uses its own byte and preserves independent rule metadata");
            before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);plan=rules.Plan(mem.Read,30);
            check(Apply(mem,rules.Reader,plan,out error) && rules.Addresses.All(a=>BitConverter.ToInt32(mem.Read(a,4),0)==30),"quota updates all three copies");
            check(mem.Read(primary+0x1F,1)[0]==2,"quota updates do not rewrite rule metadata");
            check(Apply(mem,rules.Reader,Reverse(plan),out error) && Same(mem,before),"quota undo restores all rule copies and no unrelated bytes");
            mem=RuleMemory(out competition,out primary,out copies);rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null);before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);plan=rules.Plan(mem.Read,25);mem.FailWrite=2;
            check(!Apply(mem,rules.Reader,plan,out error) && Same(mem,before),"quota write failure restores earlier rule copies");
            mem=RuleMemory(out competition,out primary,out copies);mem.I(copies[1],4);
            throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null),"disagreeing runtime and save copies cannot be edited");
            mem=RuleMemory(out competition,out primary,out copies);mem.I(primary,7);
            throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null),"conditional quota rules stay read-only");
            mem=RuleMemory(out competition,out primary,out copies);mem.Put(primary+0x1E,new byte[]{1});
            throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null),"minimum rules cannot be treated as foreign-player maxima");
            mem=RuleMemory(out competition,out primary,out copies);rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null);
            throws(()=>rules.Plan(mem.Read,-1),"negative quota sentinel is not invented");throws(()=>rules.Plan(mem.Read,1001),"unverified quota range is rejected");
            plan=rules.Plan(mem.Read,0);check(plan.All(x=>BitConverter.ToInt32(x.After,0)==0),"zero means a zero allowance, never removal of the rule");
            mem.I(competition.Address+12,1111);throws(()=>rules.Plan(mem.Read,25),"changed competition identity invalidates quota previews");
            check(mem.Writes==0,"invalid quota previews do not write");
            foreach(int code in new[]{0x02,0x03,0x04,0x05,0x06,0x07,0x09,0x12,0x15,0x1E,0x67})
            {
                mem=RuleMemory(out competition,out primary,out copies,code);
                check(LeagueEligibilitySnapshot.Codes(mem.Read,Base,competition).SequenceEqual(new[]{code}),"only actually present mapped quota types are offered");
                rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,code);plan=rules.Plan(mem.Read,15);
                check(rules.RuleCode==code && Apply(mem,rules.Reader,plan,out error) && rules.Addresses.All(a=>BitConverter.ToInt32(mem.Read(a,4),0)==15),"selected foreign/annual/match quota updates its three verified copies");
            }
            foreach(int code in new[]{0x0B,0x26,0x27,0x91,0x92,0xD4,0xFD})
            {
                mem=RuleMemory(out competition,out primary,out copies,code,true);before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);
                rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,code);
                check(rules.IsMinimum && rules.DirectionLabel=="至少" && rules.RelaxedLimit==0,"homegrown and same-nation minima are distinct from foreign maxima");
                check(LeagueEligibilitySnapshot.Codes(mem.Read,Base,competition).SequenceEqual(new[]{code}),"actual minimum rules are offered without inventing a non-EU maximum");
                string overview=LeagueEligibilitySnapshot.Overview(mem.Read,Base,competition);
                check(overview.Contains("最低") && overview.Contains("至少 3 人") && overview.Contains("未发现直接"),"a minimum-only league explains its actual constraints");
                check(MainForm.LeagueRuleInstruction(rules).Contains("0 表示不再要求"),"minimum zero has a separate user-facing meaning");
                plan=rules.Plan(mem.Read,rules.RelaxedLimit);
                check(plan.Count==3 && plan.All(e=>BitConverter.ToInt32(e.After,0)==0) && Apply(mem,rules.Reader,plan,out error),"relaxing a minimum changes all three copies to zero");
                check(mem.Read(primary+0x1E,2).SequenceEqual(new byte[]{1,2}),"minimum edits preserve direction and independent metadata");
                check(Apply(mem,rules.Reader,Reverse(plan),out error) && Same(mem,before),"minimum-rule undo restores all values and metadata");
                mem=RuleMemory(out competition,out primary,out copies,code,true);rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,code);
                before=mem.Data.ToDictionary(x=>x.Key,x=>x.Value);plan=rules.Plan(mem.Read,0);mem.FailWrite=2;
                check(!Apply(mem,rules.Reader,plan,out error) && Same(mem,before),"partial minimum writes roll back all known own changes");
                mem=RuleMemory(out competition,out primary,out copies,code,false);
                throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,code),"a minimum code with a maximum direction stays read-only");
            }
            mem=RuleMemory(out competition,out primary,out copies);rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null);
            check(!rules.IsMinimum && rules.RelaxedLimit==1000 && MainForm.LeagueRuleInstruction(rules).Contains("0 表示零名额"),"maximum relaxation never incorrectly writes zero places");
            plan=rules.Plan(mem.Read,1000);check(plan.All(e=>BitConverter.ToInt32(e.After,0)==1000),"large maximum is explicit rather than a fabricated unlimited sentinel");
            mem=RuleMemory(out competition,out primary,out copies,0x09);rules=LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,0x09);
            check(rules.MinimumValue==1 && MainForm.LeagueRuleInstruction(rules).Contains("U21"),"total squad capacity has independent bounds and does not claim to alter exemptions");
            throws(()=>rules.Plan(mem.Read,0),"an unverified zero-size squad sentinel is never written");
            plan=rules.Plan(mem.Read,35);check(Apply(mem,rules.Reader,plan,out error) && rules.Addresses.All(a=>BitConverter.ToInt32(mem.Read(a,4),0)==35),"squad-size changes update all three copies");
            mem=RuleMemory(out competition,out primary,out copies,0x92,true);mem.I(primary+0x18,8);
            ulong totalRule=mem.Allocate(32);mem.I(totalRule+0x18,25);mem.Put(totalRule+0x1C,new byte[]{9,0,255,0});mem.Q(0xA40000+0x70,mem.List(primary,totalRule));
            check(LeagueEligibilitySnapshot.Overview(mem.Read,Base,competition).Contains("至多 17 人（25 − 8）"),"homegrown and total capacity explain the derived non-homegrown allowance");
            mem.I(totalRule+0x18,30);
            check(LeagueEligibilitySnapshot.Overview(mem.Read,Base,competition).Contains("至多 22 人（30 − 8）"),"non-homegrown allowance follows the loaded values instead of a hardcoded league limit");
            mem.I(primary,42);
            check(!LeagueEligibilitySnapshot.Overview(mem.Read,Base,competition).Contains("名额至多"),"conditional homegrown rules do not produce an unconditional inferred allowance");
            mem=RuleMemory(out competition,out primary,out copies);mem.I(primary,42);
            check(LeagueEligibilitySnapshot.Overview(mem.Read,Base,competition).Contains("附加条件，只读"),"conditional rules remain visible without becoming writable");
            throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null),"visible conditional rows are still refused by the writer");
            foreach(var example in new[]{new EligibilityCompetition {Uid=67,Name="Spanish First Division"},new EligibilityCompetition {Uid=16,Name="French First Division"},new EligibilityCompetition {Uid=22,Name="German First Division"},new EligibilityCompetition {Uid=11,Name="English Premier Division"},new EligibilityCompetition {Uid=32,Name="Italian First Division"}})
            {
                string query=example.Uid==67?"西甲":example.Uid==16?"法甲":example.Uid==22?"德甲":example.Uid==11?"英超":"意甲";
                check(example.Matches(query) && example.Matches("UID:"+example.Uid) && example.Matches(example.Name) && !example.Matches("不存在的赛事"),"league shortcuts and exact UIDs filter actual discovered competitions only");
            }
            mem=RuleMemory(out competition,out primary,out copies);
            throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,0x123),"unknown quota codes cannot be edited");
            throws(()=>LeagueEligibilitySnapshot.Capture(mem.Read,Base,competition,null,0x15),"an absent mapped quota cannot alias a different rule");
        }
    }
}
