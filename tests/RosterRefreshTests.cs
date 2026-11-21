using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class RosterRefreshTests
    {
        sealed class Fixture
        {
            internal const ulong First=0x81000,Youth=0x82000,Away=0x83000;
            internal const ulong FirstEntries=0x90100,YouthEntries=0x90200,AwayEntries=0x90300;
            readonly Dictionary<ulong,byte> memory=new Dictionary<ulong,byte>();
            public readonly HashSet<ulong> Reads=new HashSet<ulong>();
            public readonly GrowthReadLayout Layout=new GrowthReadLayout
            {
                Module=0x140000000,PersonVt=0x1100,CoachVt=0x1200,PlayerRootVt=0x1300,CoachRootVt=0x1400,
                ClubVt=0x2000,TeamVt=0x3000,ContractVt=0x4000,LoanVt=0x4100,PlayerDirectory=0x9100,
                TeamBefore=FmLayout.TeamBeforePerson,PlayerBefore=0x288,LoanHolder=0x338,PersonContract=FmLayout.ContractAfterPerson,ClubTeams=0x18,
                ClubName=0xC0,ContractTeam=FmLayout.ContractTeam,ContractExpiry=0x48,Labels=true
            };
            public readonly Club Home=new Club{Addr=0x71000,Name="Test home club",Uid=511,Team=First};
            public readonly Club Other=new Club{Addr=0x72000,Name="Test away club",Uid=512,Team=Away};
            public readonly DateTime Today=new DateTime(2026,11,19);
            public Fixture()
            {
                foreach(var c in new[]{Home,Other})
                {
                    Put(c.Addr,new byte[0x180]);Q(c.Addr,Layout.Module+Layout.ClubVt);
                    I(c.Addr+FmLayout.Uid,c.Uid);
                }
                MakeTeam(First,Home,100);MakeTeam(Youth,Home,20);MakeTeam(Away,Other,100);
                Vector(Home.Addr+Layout.ClubTeams,0x90000,First,Youth);
                Vector(Other.Addr+Layout.ClubTeams,0x90080,Away);
            }
            public void Put(ulong address,byte[] bytes)
            {
                for(int i=0;i<bytes.Length;i++)memory[address+(ulong)i]=bytes[i];
            }
            public void Q(ulong address,ulong value){Put(address,BitConverter.GetBytes(value));}
            public void I(ulong address,int value){Put(address,BitConverter.GetBytes(value));}
            public byte[] Read(ulong address,int length)
            {
                Reads.Add(address);
                var result=new byte[length];
                for(int i=0;i<length;i++)if(!memory.TryGetValue(address+(ulong)i,out result[i]))return null;
                return result;
            }
            void MakeTeam(ulong at,Club owner,byte age)
            {
                Put(at,new byte[0x100]);Q(at,Layout.Module+Layout.TeamVt);Q(at+0x30,owner.Addr);
                I(at+0x28,age==100?0:1);Put(at+0x2C,new[]{age});
            }
            public void Vector(ulong header,ulong storage,params ulong[] addresses)
            {
                Put(storage,addresses.SelectMany(BitConverter.GetBytes).ToArray());
                Q(header,storage);Q(header+8,storage+(ulong)addresses.Length*8);Q(header+16,storage+(ulong)addresses.Length*8);
            }
            public ulong Root(Player p){return p.Tail-Layout.PlayerBefore;}
            public void Roster(ulong team,ulong storage,params Player[] people)
            {
                Vector(team+Layout.TeamPlayers,storage,people.Select(Root).ToArray());
            }
            public void Directory(params Player[] players)
            {
                Vector(Layout.Module+Layout.PlayerDirectory,0x98000,players.Select(Root).ToArray());
            }
            public Player Person(ulong root,int uid,ulong playing,ulong owner,bool coach=false,bool loan=false)
            {
                ulong extra=coach?Layout.CoachExtra:0;
                ulong person=root+Layout.PlayerBefore+extra,tail=person-extra;
                Put(root,new byte[0x600]);
                Q(root,Layout.Module+(coach?Layout.CoachRootVt:Layout.PlayerRootVt));
                Q(person,Layout.Module+(coach?Layout.CoachVt:Layout.PersonVt));I(person+FmLayout.Uid,uid);
                var block=Enumerable.Repeat((byte)50,54).ToArray();
                var positions=Enumerable.Repeat((byte)1,15).ToArray();positions[12]=20;
                Put(tail-FmLayout.BlockBeforePerson,positions.Concat(block).ToArray());
                Put(tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)90).Concat(BitConverter.GetBytes((short)130)).ToArray());
                Assign(root,person,playing,owner,loan);
                var result=Decode(person,extra,Read(person,FmLayout.HeadLen));
                result.Club=playing==Away?Other.Addr:Home.Addr;
                return result;
            }
            public void Assign(ulong root,ulong person,ulong playing,ulong owner,bool loan=false)
            {
                ulong contract=root+0x800;
                Q(root+Layout.PlayerBefore-Layout.TeamBefore,playing);
                Q(person+Layout.PersonContract,contract);
                Put(contract,new byte[0x70]);Q(contract,Layout.Module+Layout.ContractVt);Q(contract+8,person);Q(contract+Layout.ContractTeam,owner);
                Q(root+Layout.LoanHolder,0);
                if(!loan)return;
                ulong holder=root+0xA00,deal=root+0xB00;
                Q(root+Layout.LoanHolder,holder);Q(holder,deal);
                Put(deal,new byte[0x70]);Q(deal,Layout.Module+Layout.LoanVt);Q(deal+8,person);Q(deal+Layout.ContractTeam,playing);
                I(deal+0x48,(2027<<16)|181);
            }
            static Player Decode(ulong person,ulong extra,byte[] head)
            {
                int uid=BitConverter.ToInt32(head,(int)FmLayout.Uid);
                string name="Test player "+uid;
                return new Player{Person=person,Extra=extra,Uid=uid,Name=name,Search=name,Born=new DateTime(2008,6,12)};
            }
            public ClubRosterReader Reader(Func<ulong,int,byte[]> custom=null)
            {
                return new ClubRosterReader(custom??Read,Layout,Decode);
            }
        }

        static bool Reject(Action action)
        {
            try{action();return false;}
            catch(InvalidDataException){return true;}
        }

        public static void Run(Action<bool,string> check)
        {
            var game=new Fixture();
            var stay=game.Person(0x20000,1,Fixture.First,Fixture.First);
            var sale=game.Person(0x22000,2,Fixture.First,Fixture.First);
            var loanee=game.Person(0x24000,3,Fixture.First,Fixture.First);
            var oldArrival=game.Person(0x26000,4,Fixture.Away,Fixture.Away);
            var away=game.Person(0x28000,5,Fixture.Away,Fixture.Away);
            var cached=new[]{stay,sale,loanee,oldArrival,away};
            game.Roster(Fixture.First,Fixture.FirstEntries,stay,sale,loanee);
            game.Roster(Fixture.Youth,Fixture.YouthEntries);
            game.Roster(Fixture.Away,Fixture.AwayEntries,oldArrival,away);
            var initial=game.Reader().ReadClub(game.Home,cached,game.Today);
            var initialStay=initial.Refreshed[1];
            var initialSale=initial.Refreshed[2];
            uint day=(2026u<<16)|(uint)game.Today.DayOfYear;
            var layout=game.Layout;
            Func<Player,List<KeyValuePair<ulong,byte[]>>> guards=p=>RosterPlayerIdentity.Guards(p,layout.Module,layout.PersonVt,layout.CoachVt,layout.CoachExtra);
            var oldPreview=GrowthStep.Capture(game.Read,()=>day,initialSale,guards(initialSale));
            var localTarget=new GrowTarget{P=initialStay,ScopeClub=game.Home.Addr,Elapsed=19,Added=7,Options=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{38}}};
            var soldTarget=new GrowTarget{P=initialSale,ScopeClub=game.Home.Addr,Elapsed=8,Options=new GrowthOptions()};
            var loanTarget=new GrowTarget{P=initial.Refreshed[3],ScopeClub=game.Home.Addr,Elapsed=18,Options=new GrowthOptions()};
            var otherTarget=new GrowTarget{P=away,ScopeClub=game.Other.Addr,Elapsed=9,Options=new GrowthOptions()};

            // Formal transfer out, loan out, a newcomer not in the scan, and the same transfer-in UID
            // now represented by a player-coach record.
            game.Assign(game.Root(sale),sale.Person,Fixture.Away,Fixture.Away);
            game.Assign(game.Root(loanee),loanee.Person,Fixture.Away,Fixture.First,true);
            game.I(oldArrival.Person+FmLayout.Uid,404);
            var arrival=game.Person(0x2A000,4,Fixture.First,Fixture.First,true);
            var newYouth=game.Person(0x2C000,6,Fixture.Youth,Fixture.First);
            var incomingLoan=game.Person(0x2E000,7,Fixture.First,Fixture.Away,loan:true);
            game.Roster(Fixture.First,Fixture.FirstEntries,stay,arrival,incomingLoan);
            game.Roster(Fixture.Youth,Fixture.YouthEntries,newYouth);
            game.Roster(Fixture.Away,Fixture.AwayEntries,sale,loanee,away);
            game.Directory(stay,sale,loanee,away,arrival,newYouth,incomingLoan);
            game.Reads.Clear();
            var update=game.Reader().ReadClub(game.Home,initial.Merge(cached),game.Today);
            var merged=update.Merge(cached);
            check(update.CurrentPlayers==4 && merged.Count==7 && merged.Count(p=>p.Uid==4)==1,"local refresh upserts both transfer-ins and previously unscanned players by UID");
            check(!game.Reads.Contains(Fixture.Away+layout.TeamPlayers) && !game.Reads.Contains(layout.Module+layout.PlayerDirectory),"ordinary refresh does not read unrelated squads or search the global index");
            check(update.Refreshed[2].Club==game.Other.Addr && !oldPreview.Matches(game.Read,()=>day),"permanent transfer updates the global reference and invalidates its old preview");
            check(update.Refreshed[3].Roster.ContractClub==game.Home.Addr && update.Refreshed[3].Roster.Loan==GrowthLoanState.Active,"the parent's outgoing loan is retained with verified contract and playing side");
            check(update.Refreshed[7].Roster.Loan==GrowthLoanState.Active && GrowthRosterView.LoanLabel(update.Refreshed[7],game.Home.Addr)=="租入","a new incoming loan is included and labelled");
            var youth=GrowthRosterView.Choices(update.Directory,game.Home).Single(choice=>choice.Team==Fixture.Youth);
            check(merged.Where(p=>GrowthRosterView.Contains(update.Directory,youth,p)).Single().Uid==6,"a newly discovered player is available in the actual youth division");
            var rebound=GrowthRoster.Rebind(new[]{localTarget,soldTarget,loanTarget,otherTarget},merged);
            check(rebound.SequenceEqual(new[]{localTarget,loanTarget,otherTarget}) && localTarget.Elapsed==19 &&
                localTarget.Added==7 && localTarget.Options.Attributes.SequenceEqual(new[]{38}),"refresh preserves only valid explicit watches with their settings and progress");
            var current=update.Refreshed[4];
            var freshPreview=GrowthStep.Capture(game.Read,()=>day,current,guards(current));
            check(current.Extra==layout.CoachExtra && freshPreview.PlayerName==arrival.Name && freshPreview.Matches(game.Read,()=>day),"the new coach subtype can open a current growth/optimization preview");
            var wrong=RosterPlayerIdentity.Guards(current,layout.Module,layout.PersonVt,layout.PersonVt,layout.CoachExtra);
            check(Reject(()=>GrowthStep.Capture(game.Read,()=>day,current,wrong)),"using the former ordinary-person guard reproduces the player-coach failure");
            check(game.Reader().ReadPlayer(oldArrival,game.Today).Refreshed[4].Person==arrival.Person,"a stale player selection can be relocated by UID without a full scan");
            var duplicate=game.Person(0x30000,4,Fixture.First,Fixture.First);
            game.Directory(arrival,duplicate);
            check(Reject(()=>game.Reader().ReadPlayer(oldArrival,game.Today)),"two different live records with the requested UID are refused");
            game.Roster(Fixture.First,Fixture.FirstEntries,stay,arrival,duplicate);
            check(Reject(()=>game.Reader().ReadClub(game.Home,cached,game.Today)),"ambiguous local player rows are not committed");
            game.Roster(Fixture.First,Fixture.FirstEntries,stay,arrival,incomingLoan);
            int reads=0;
            var moving=game.Reader((address,count)=>
            {
                var bytes=game.Read(address,count);
                if(address==Fixture.First+layout.TeamPlayers && ++reads>1)bytes[8]^=8;
                return bytes;
            });
            check(Reject(()=>moving.ReadClub(game.Home,cached,game.Today)) && sale.Club==game.Home.Addr,"an unstable roster fails before changing the old cache");
            using(var form=new MainForm(false,false))
            {
                var squad=form.Controls.Find("RefreshSquadRoster",true).OfType<Button>().Single();
                var growth=form.Controls.Find("RefreshGrowthRoster",true).OfType<Button>().Single();
                check(squad.Text==growth.Text && !squad.Enabled && !growth.Enabled,"both workflow entry points offer the same action and require a connection");
            }
        }
    }
}
