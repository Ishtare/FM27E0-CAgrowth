using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class GrowthSquadTests
    {
        internal sealed class Fixture
        {
            public readonly Dictionary<ulong,byte> Bytes=new Dictionary<ulong,byte>();
            public readonly GrowthReadLayout Layout=new GrowthReadLayout{Module=0x140000000,PersonVt=0x1000,CoachVt=0x1100,TeamVt=0x2000,ClubVt=0x3000,ContractVt=0x4000,LoanVt=0x5000,TeamBefore=0x158,PlayerBefore=0x288,LoanHolder=0x338,PersonContract=0xA8,ClubTeams=0x18,ClubName=0xC0,ContractTeam=0x10,ContractExpiry=0x48,Labels=true};
            public readonly Club A=new Club{Addr=0x70000,Name="Synthetic Club A"},B=new Club{Addr=0x71000,Name="Synthetic Club B"};
            public readonly List<Player> Players=new List<Player>();
            ulong text=0xC0000;
            public Fixture()
            {
                foreach(var c in new[]{A,B}){Put(c.Addr,new byte[0x180]);Q(c.Addr,Layout.Module+Layout.ClubVt);string name=c.Name;var data=Encoding.UTF8.GetBytes(name);Q(c.Addr+0xC0,text);Put(text,BitConverter.GetBytes(data.Length).Concat(data).ToArray());text+=0x100;}
                Team(0x80000,A.Addr,0,100,101);Team(0x81000,A.Addr,0x999,20,102);Team(0x82000,A.Addr,0x10C,18,103);Team(0x83000,B.Addr,0,100,104);
                TeamList(A,0x80000,0x81000,0x82000);TeamList(B,0x83000);
            }
            public void Put(ulong at,byte[] data){for(int i=0;i<data.Length;i++)Bytes[at+(ulong)i]=data[i];}
            public void Q(ulong at,ulong value){Put(at,BitConverter.GetBytes(value));}
            public byte[] Read(ulong at,int length){return Enumerable.Range(0,length).All(i=>Bytes.ContainsKey(at+(ulong)i))?Enumerable.Range(0,length).Select(i=>Bytes[at+(ulong)i]).ToArray():null;}
            public void Team(ulong at,ulong club,int classification,byte age,int uid)
            {Put(at,new byte[0xB0]);Q(at,Layout.Module+Layout.TeamVt);Q(at+0x30,club);Put(at+12,BitConverter.GetBytes(uid));Put(at+0x28,BitConverter.GetBytes(classification));Put(at+0x2C,new[]{age});}
            public void TeamList(Club club,params ulong[] teams)
            {ulong vector=0xB0000+(club.Addr-A.Addr);Put(vector,teams.SelectMany(BitConverter.GetBytes).ToArray());Q(club.Addr+0x18,vector);Q(club.Addr+0x20,vector+(ulong)(teams.Length*8));Q(club.Addr+0x28,vector+(ulong)(teams.Length*8));}
            public Player Person(int uid,ulong at,ulong playing,ulong owner,bool loan=false)
            {
                var p=new Player{Uid=uid,Person=at,Name="Synthetic roster "+uid,Born=new DateTime(2009,11,18),Club=playing==0x83000?B.Addr:A.Addr};
                Put(at,new byte[0xC0]);Q(at,Layout.Module+Layout.PersonVt);Put(at+FmLayout.Uid,BitConverter.GetBytes(uid));Q(at-Layout.TeamBefore,playing);
                ulong contract=at+0x2000;Put(contract,new byte[0x80]);Q(at+0xA8,contract);Q(contract,Layout.Module+Layout.ContractVt);Q(contract+8,at);Q(contract+0x10,owner);
                Q(at-Layout.PlayerBefore+Layout.LoanHolder,0);
                if(loan)
                {
                    ulong holder=at+0x3000,loanContract=at+0x4000;Q(at-Layout.PlayerBefore+Layout.LoanHolder,holder);Q(holder,loanContract);Put(loanContract,new byte[0x80]);Q(loanContract,Layout.Module+Layout.LoanVt);Q(loanContract+8,at);Q(loanContract+0x10,playing);Put(loanContract+0x48,BitConverter.GetBytes((2027u<<16)|181u));
                }
                Players.Add(p);return p;
            }
            public GrowthDirectory Capture(){return new GrowthDirectoryReader(Read,Layout).Capture(Players,new[]{A,B},new DateTime(2026,11,17));}
        }
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,Private).GetValue(f);}
        public static void Run(Action<bool,string> check)
        {
            var fixture=new Fixture();var main=fixture.Person(1,0x20000,0x80000,0x80000);var youth=fixture.Person(2,0x30000,0x81000,0x80000);var incoming=fixture.Person(3,0x40000,0x82000,0x83000,true);var outgoing=fixture.Person(4,0x50000,0x83000,0x80000,true);
            var directory=fixture.Capture();
            check(directory.ForClub(fixture.A.Addr).Count==3,"all actual club teams, including empty squads, are retained");
            check(directory.Teams[0x81000].Name=="U20 青年队" && directory.Teams[0x82000].Name=="U18 青年队","age limit data distinguishes Italian-style U20/U18 without hardcoded U21 assumptions");
            check(main.Roster.Loan==GrowthLoanState.None && youth.Roster.Loan==GrowthLoanState.None,"a first-team contract with a youth playing team is not misclassified as a loan");
            check(incoming.Roster.Loan==GrowthLoanState.Active && GrowthRosterView.LoanLabel(incoming,fixture.A.Addr)=="租入","incoming loans are identified from full owner, current team and loan contract");
            check(outgoing.Roster.Loan==GrowthLoanState.Active && GrowthRosterView.LoanLabel(outgoing,fixture.A.Addr)=="租出" && outgoing.Roster.LoanEnd==new DateTime(2027,6,30),"outgoing loans retain destination and expiry evidence");
            var choices=GrowthRosterView.Choices(directory,fixture.A);var all=choices.First();var u20=choices.Single(s=>s.Team==0x81000);var outView=choices.Single(s=>s.Kind==GrowthSquadKind.OutOnLoan);
            check(fixture.Players.Where(p=>GrowthRosterView.Contains(directory,all,p)).Select(p=>p.Uid).SequenceEqual(new[]{1,2,3}),"all local squads exclude players currently away on loan");
            check(fixture.Players.Where(p=>GrowthRosterView.Contains(directory,u20,p)).Single()==youth,"specific team filters use actual team pointers, not age or the full contract team");
            check(fixture.Players.Where(p=>GrowthRosterView.Contains(directory,outView,p)).Single()==outgoing,"outgoing players have an explicit separate view");
            check(GrowthSupportPlanner.Age(main.Born,new DateTime(2026,11,17))==16 && GrowthSupportPlanner.Age(main.Born,new DateTime(2026,11,18))==17,"age is calculated on the supplied game date and birthday boundary");
            check(!GrowthSupportPlanner.Age(main.Born,null).HasValue,"unreadable game date never falls back to the computer date");
            fixture.Layout.Labels=false;var unknownLabels=fixture.Capture();check(!unknownLabels.Teams[0x81000].Name.Contains("U20"),"unverified label mappings do not invent youth-team ages");fixture.Layout.Labels=true;
            fixture.Put(0x81000+0x2C,new byte[]{19});check(fixture.Capture().Teams[0x81000].Name=="U19 青年队","a different save's actual age limit is displayed without a league-name whitelist");
            fixture.Q(0x44000+8,999);fixture.Capture();check(incoming.Roster.Loan==GrowthLoanState.Unknown && GrowthRosterView.LoanLabel(incoming,fixture.A.Addr)=="待确认","mismatched loan owner is unavailable rather than classified as not loaned");
            fixture.Q(0x44000+8,incoming.Person);fixture.Q(0x43000,0);fixture.Capture();check(incoming.Roster.Loan==GrowthLoanState.Unknown,"an unreadable/empty loan holder is not proof of no loan");
            fixture.Q(0x43000,0x44000);fixture.Put(0x44000+0x48,BitConverter.GetBytes((2026u<<16)|1u));fixture.Capture();check(incoming.Roster.Loan==GrowthLoanState.Unknown,"expired loan pointers are not presented as current loans");
            fixture.Put(0x44000+0x48,BitConverter.GetBytes((2027u<<16)|181u));directory=fixture.Capture();
            fixture.Q(fixture.A.Addr+0x118,0xD0000);fixture.Q(fixture.A.Addr+0x120,0xD0008);fixture.Q(0xD0000,0xD1000);fixture.Put(0xD1000,new byte[0x38]);fixture.Q(0xD1000,fixture.A.Addr);fixture.Q(0xD1008,fixture.B.Addr);fixture.Put(0xD1030,new byte[]{16});
            directory=fixture.Capture();var related=GrowthRosterView.Choices(directory,fixture.A).Single(s=>s.Kind==GrowthSquadKind.Related);
            check(related.Name.StartsWith("关联俱乐部：") && !GrowthRosterView.Contains(directory,all,outgoing),"a measured affiliation is offered separately without pretending every feeder is an internal B team");
            var owned=new GrowTarget{P=main,ScopeClub=fixture.A.Addr,Options=new GrowthOptions()};var next=new Player{Person=0x99000,Uid=main.Uid,Club=fixture.B.Addr,Roster=new GrowthMembership{Contract=0xA1000,ContractClub=fixture.A.Addr,PlayingClub=fixture.B.Addr,Loan=GrowthLoanState.Active}};
            check(GrowthRoster.Rebind(new[]{owned},new[]{next}).Count==1,"already selected owned players can remain selected after going on loan");
            next=new Player{Person=0x99100,Uid=main.Uid,Club=fixture.B.Addr,Roster=new GrowthMembership{Contract=0xA1100,ContractClub=fixture.B.Addr,Loan=GrowthLoanState.None}};
            check(GrowthRoster.Rebind(new[]{owned},new[]{next}).Count==0,"a permanent transfer out of the watched ownership scope removes the target");
            var borrowed=new GrowTarget{P=incoming,ScopeClub=fixture.A.Addr};next=new Player{Person=0x99200,Uid=incoming.Uid,Club=fixture.B.Addr,Roster=new GrowthMembership{Contract=0xA1200,ContractClub=fixture.B.Addr}};
            check(GrowthRoster.Rebind(new[]{borrowed},new[]{next}).Count==0,"an incoming loanee is removed after returning to the owning club");
            using(var form=new MainForm(false,false))
            {
                typeof(MainForm).GetField("growthDirectory",Private).SetValue(form,directory);Field<List<Player>>(form,"players").AddRange(fixture.Players);
                var clubs=Field<ListBox>(form,"lstGrowClubs");clubs.Items.Add(fixture.A);clubs.SelectedIndex=0;
                var squads=(ComboBox)form.Controls.Find("GrowthSquadFilter",true).Single();var loans=(ComboBox)form.Controls.Find("GrowthLoanFilter",true).Single();
                var watched=Field<Dictionary<ulong,GrowTarget>>(form,"growWatch");watched[main.Person]=new GrowTarget{P=main,Options=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}}};
                squads.SelectedItem=squads.Items.Cast<GrowthSquadChoice>().Single(s=>s.Team==0x81000);
                var scoped=(List<Player>)typeof(MainForm).GetMethod("GrowthScopedPlayers",Private).Invoke(form,null);
                check(scoped.Count==1 && scoped[0]==youth,"the production UI applies the actual selected youth-team filter");
                loans.SelectedIndex=3;check(((GrowthSquadChoice)squads.SelectedItem).Kind==GrowthSquadKind.OutOnLoan,"choosing outgoing loan status selects the outgoing view");
                squads.SelectedItem=squads.Items.Cast<GrowthSquadChoice>().Single(s=>s.Team==0x80000);
                check(loans.SelectedIndex==0,"choosing a local squad clears a conflicting outgoing-only filter");
                check(watched.Count==1 && watched[main.Person].Options.Attributes.SequenceEqual(new[]{34,38}),"changing squad or loan filters preserves the manual queue and each plan");
                var grid=Field<DataGridView>(form,"gridGrow");check(grid.Columns[2].Visible && grid.Columns[10].Visible && grid.Columns[11].Visible,"age, actual squad and loan status are visible by default");
                int row=grid.Rows.Add();typeof(MainForm).GetMethod("FillGrowthRosterInfo",Private).Invoke(form,new object[]{grid.Rows[row],outgoing,new DateTime(2026,11,17)});
                check(Convert.ToInt32(grid.Rows[row].Cells[2].Value)==16 && Convert.ToString(grid.Rows[row].Cells[11].Value)=="租入","row age uses the game date and loan label is relative to the active playing-club context");
                ((CheckBox)form.Controls.Find("GrowthDetailedColumns",true).Single()).Checked=true;check(grid.Columns[2].Visible && grid.Columns[12].Visible && grid.Columns[13].Visible,"detailed columns add direction/expiry while retaining default age");
            }
            var growth=new GrowthFocusTests.Fixture();growth.Player.Roster=new GrowthMembership{Contract=0x85000,ContractClub=growth.Player.Club};
            growth.Put(growth.Player.Person+FmLayout.ContractAfterPerson,BitConverter.GetBytes(0x85000UL));growth.Put(0x85008,BitConverter.GetBytes(growth.Player.Person));growth.Put(0x85000+FmLayout.ContractTeam,BitConverter.GetBytes(0x86000UL));growth.Put(0x86000+FmLayout.TeamClub,BitConverter.GetBytes(growth.Player.Club));
            var planned=growth.Plan();growth.Put(0x86000+FmLayout.TeamClub,BitConverter.GetBytes(0x99900UL));string error;
            check(!growth.Apply(planned,out error) && growth.Writes==0,"a change in contract-club ownership blocks a growth transaction before any write");
        }
    }
}
