// Modified build v1.1.22. All memory used by these tests belongs to this test process.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class PerformanceMaintenanceTests
    {
        internal static void PumpUntil(Func<bool> finished)
        {
            var elapsed=Stopwatch.StartNew();
            while(!finished())
            {
                if(elapsed.ElapsedMilliseconds>10000)throw new TimeoutException("The offline UI task did not complete.");
                Application.DoEvents();
                Thread.Sleep(1);
            }
        }

        sealed class Connection : IDisposable
        {
            public volatile bool Disposed;
            public void Dispose(){Disposed=true;}
        }

        static void CheckWorker(Action<bool,string> check)
        {
            using(var entered=new ManualResetEventSlim())
            using(var resume=new ManualResetEventSlim())
            {
                var worker=new RosterReadWorker();
                var reading=worker.Run(token=>{entered.Set();resume.Wait();return 1;});
                check(entered.Wait(3000) && worker.Busy && !reading.IsCompleted,"a pending roster read leaves the caller available");
                var duplicate=worker.Run(token=>2);
                check(duplicate.IsFaulted && duplicate.Exception.GetBaseException() is InvalidOperationException,"simultaneous readers cannot publish conflicting lists");
                worker.Cancel();
                resume.Set();
                PumpUntil(()=>reading.IsCompleted);
                check(reading.IsCanceled && !worker.Busy,"a canceled read cannot publish its result");
                var next=worker.Run(token=>23);
                PumpUntil(()=>next.IsCompleted);
                check(next.Result==23,"a new read can follow cancellation");

                entered.Reset();resume.Reset();
                var closed=new Connection();
                var final=worker.Run<int>(token=>{entered.Set();resume.Wait();throw new InvalidOperationException();});
                check(entered.Wait(3000),"last read has started");
                worker.Close(closed);
                check(!closed.Disposed,"closing keeps the connection alive until its reader exits");
                resume.Set();
                PumpUntil(()=>final.IsCompleted && closed.Disposed);
                check(final.IsCanceled && !worker.Busy,"a closing reader releases the connection and discards a late failure");
            }
        }

        static void CheckIndex(Action<bool,string> check)
        {
            var club=new Club{Addr=11};
            var own=new GrowthTeam{Address=21,Club=11,Name="First"};
            var affiliate=new GrowthTeam{Address=22,Club=12,Name="Reserves"};
            var directory=new GrowthDirectory();
            directory.Teams.Add(21,own);directory.Teams.Add(22,affiliate);
            directory.ClubTeams.Add(11,new List<GrowthTeam>{own,affiliate});
            directory.ClubTeams.Add(12,new List<GrowthTeam>{affiliate});
            directory.Related.Add(11,new List<GrowthRelatedClub>{new GrowthRelatedClub{Club=12}});
            var players=new[] {
                new Player{Person=101,Uid=1,Club=11,Roster=new GrowthMembership{Team=21,Contract=1,PlayingClub=11,ContractClub=11}},
                new Player{Person=102,Uid=2,Club=12,Roster=new GrowthMembership{Team=22,Contract=2,PlayingClub=12,ContractClub=11,Loan=GrowthLoanState.Active}},
                new Player{Person=103,Uid=3,Club=11},
                new Player{Person=104,Uid=4,Club=12,Roster=new GrowthMembership{Team=22}},
                new Player{Person=105,Uid=5,Club=11,Roster=new GrowthMembership{Team=31,PlayingClub=11}}
            };
            var index=new PlayerRosterIndex(players);
            foreach(var scope in GrowthRosterView.Choices(directory,club))
                check(index.ForScope(directory,scope).OrderBy(p=>p.Uid).SequenceEqual(players.Where(p=>GrowthRosterView.Contains(directory,scope,p))),"indexed roster agrees with original membership for "+scope.Key);
            var copy=new Player{Person=101,Uid=1,Club=12};
            var change=new ClubRosterUpdate();
            change.Refreshed.Add(1,copy);change.Removed.Add(2);index.Apply(change);
            check(index.Unique(1)==copy && !index.ForClub(11).Contains(players[0]) && index.ForClub(12).Contains(copy) && index.Unique(2)==null,"team and UID indexes move together for transfers and removals");
            check(new PlayerRosterIndex(new[]{players[0],copy}).Unique(1)!=null && new PlayerRosterIndex(new[]{copy,new Player{Uid=1,Person=201}}).Unique(1)==null,"identical records are recognized and ambiguous UIDs remain unresolved");
            var name=new Player{Uid=7,Name="Old Name",Search="Old Name"};
            check(name.Matches("oldname"),"original spelling is indexed");
            name.Name="New Name";name.Search="New Name";name.ChineseName="新姓名";
            check(!name.Matches("oldname") && name.Matches("newname") && name.Matches("新姓名"),"original and translated search keys follow changes to names");
        }

        internal static void Run(Action<bool,string> check)
        {
            CheckIndex(check);
            using(var form=new MainForm(false,false))
            {
                CheckWorker(check);
                form.CheckRosterAndGrowthMaintenance(check);
            }
        }
    }

    partial class MainForm
    {
        void CheckDefaultCaGrowth(Action<bool,string> check,Club club,uint today,Action<uint> setDate,Action<ulong,int> putInt)
        {
            const int marking=5;
            growWatch.Clear();growthPreferences.Clear();
            var player=GrowthPlayer(100);
            var original=fm.Read(player.Block,69);
            var wing=(byte[])original.Clone();
            wing[12]=1;wing[9]=20;wing[15+marking]=35;
            Marshal.Copy(wing,0,new IntPtr((long)player.Block),wing.Length);
            ulong ability=player.Tail-FmLayout.AbilityBeforePerson;
            lstGrowClubs.SelectedItem=club;
            LoadGrow();
            var row=gridGrow.Rows.Cast<DataGridViewRow>().Single(r=>((Player)r.Tag).Uid==player.Uid);
            row.Cells[0].Value=true;
            var target=growWatch.Values.Single();
            check(!growthPreferences.ContainsKey(player.Uid) && GrowGear()==4 &&
                target.Options.Mode==GrowthMode.CaOnly && !target.UseFocus && target.Focus==null,
                "a newly checked winger at the default four-times rate starts with no directed plan");

            autoMonthKey=MonthKey(today);
            int firstEdit=changes.Count;
            ToggleGrow();
            check(growOn && growthLimitTargets.Length==0 && target.Span==600,
                "starting default growth leaves the caps off and uses the four-times curve");
            setDate(today+3);
            GrowTick();
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            check(fm.ReadI32(ability)==((160<<16)|101) && fm.Read(player.Block,69).SequenceEqual(wing),
                "a daily automatic-mode tick increases CA and keeps every position and attribute byte including marking seven");
            check(changes.Count==firstEdit+1 && changes[firstEdit].Addr==ability && changes[firstEdit].New.Length==2,
                "the whole initial four-times growth writes only the two-byte CA field");

            // Simulate an independent game update from marking 7 to 3.
            var afterGame=(byte[])wing.Clone();afterGame[15+marking]=15;
            Marshal.Copy(afterGame,0,new IntPtr((long)player.Block),afterGame.Length);
            setDate(today+9);
            GrowTick();
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            check(fm.ReadI32(ability)==((160<<16)|102) && fm.Read(player.Block,69).SequenceEqual(afterGame) &&
                changes.Skip(firstEdit).All(c=>c.Addr==ability),
                "the subsequent tick preserves an independently changed marking value without writing any attributes");

            var stale=fm.Read(ability,4);
            putInt(ability,(160<<16)|106);
            string error;
            int count=changes.Count;
            check(!ApplyCaOnlyGrowth(player,PlayerGuards(player),stale,103,out error) &&
                fm.ReadI32(ability)==((160<<16)|106) && changes.Count==count,
                "a stale day cannot overwrite a newer higher CA");
            putInt(ability,(101<<16)|102);
            check(!ApplyCaOnlyGrowth(player,PlayerGuards(player),stale,103,out error) &&
                fm.ReadI32(ability)==((101<<16)|102) && changes.Count==count,
                "a stale PA cannot permit an edit above the game's new limit");

            StopGrow(null);
            growWatch.Clear();
            setDate(today);
            putInt(ability,(160<<16)|100);
            Marshal.Copy(original,0,new IntPtr((long)player.Block),original.Length);
        }

        internal void CheckRosterAndGrowthMaintenance(Action<bool,string> check)
        {
            freezeTimer.Stop();
            var handle=Handle;
            uint today=(2026u<<16)|201;
            check(DayKey((2027u<<16)|1)-DayKey((2026u<<16)|365)==1 &&
                DayKey((2028u<<16)|61)-DayKey((2028u<<16)|59)==2 &&
                DayKey(today)==DayKey(today|0x400),"growth uses elapsed calendar days including leap years and ignores intraday changes");
            const int count=70, stride=0x3000, bytes=0x4000+count*stride;
            var savedDateRva=FmProcess.CurrentDateRva;
            var savedSafety=Safety.Off;
            var memory=Marshal.AllocHGlobal(bytes);
            try
            {
                Marshal.Copy(new byte[bytes],0,memory,bytes);
                ulong start=(ulong)memory.ToInt64(), team=start+0xA00;
                Action<ulong,ulong> ptr=(address,value)=>Marshal.WriteInt64(new IntPtr((long)address),(long)value);
                Action<ulong,int> integer=(address,value)=>Marshal.WriteInt32(new IntPtr((long)address),value);
                Action<ulong,ulong,ulong[]> vector=(address,storage,values)=>
                {
                    ptr(address,storage);ptr(address+8,storage+(ulong)values.Length*8);ptr(address+16,storage+(ulong)values.Length*8);
                    for(int i=0;i<values.Length;i++)ptr(storage+(ulong)i*8,values[i]);
                };
                Safety.Off=new Dictionary<string,string>();
                FmProcess.CurrentDateRva=8;
                fm=new FmProcess{Base=start,H=new IntPtr(-1),Version="offline-test"};
                integer(start+8,(int)today);
                var club=new Club{Addr=start+0x400,Uid=11,Team=team,Name="Test Club"};
                ptr(club.Addr,start+FmProcess.ClubVtRva);integer(club.Addr+12,club.Uid);
                ptr(team,start+FmProcess.TeamVtRva);ptr(team+FmLayout.TeamClub,club.Addr);ptr(team+FmLayout.TeamClub2,club.Addr);
                vector(club.Addr+FmLayout.ClubTeams,start+0x1100,new[]{team});
                var roots=new ulong[count];
                var block=Enumerable.Repeat((byte)1,15).Concat(Enumerable.Repeat((byte)50,54)).ToArray();
                block[12]=20;
                for(int i=0;i<count;i++)
                {
                    ulong slot=start+0x4000+(ulong)i*stride, person=slot+0x1000;
                    ulong contract=slot+0x1800, nameAt=slot+0x2100;
                    var name="Fixture Player "+i;
                    var text=Encoding.UTF8.GetBytes(name);
                    roots[i]=person-FmLayout.PlayerObjBeforePerson;
                    ptr(person,start+FmProcess.PersonVtRva);integer(person+FmLayout.Uid,i+100);
                    ptr(person-FmLayout.TeamBeforePerson,team);
                    ptr(person+FmLayout.ContractAfterPerson,contract);ptr(contract,start+FmProcess.ContractVtRva);
                    ptr(contract+8,person);ptr(contract+FmLayout.ContractTeam,team);
                    ptr(person+FmLayout.FullName,nameAt);integer(nameAt,text.Length);
                    Marshal.Copy(text,0,new IntPtr((long)nameAt+4),text.Length);
                    integer(person+FmLayout.BirthDate,(2001<<16)|2);
                    integer(person-FmLayout.AbilityBeforePerson,(160<<16)|100);
                    var player=new Player{Person=person,Uid=i+100,Club=club.Addr,Name=name,Search=name,Born=new DateTime(2001,1,2)};
                    Marshal.Copy(block,0,new IntPtr((long)player.Block),block.Length);
                    if(i<count-1)players.Add(player);
                }
                vector(team+FmLayout.TeamPlayers,start+0x1300,roots);
                var chosen=new GrowTarget{P=players[0],ScopeClub=club.Addr,Options=new GrowthOptions{Mode=GrowthMode.CaOnly},Elapsed=3};
                growWatch.Add(chosen.P.Person,chosen);
                RefreshLocalClub(club);
                check(rosterRefreshing,"manual refresh returns to the window while reading");
                PerformanceMaintenanceTests.PumpUntil(()=>!rosterRefreshing);
                check(GrowthPlayer(count+99)!=null && players.Count==count,"the normal refresh action publishes a new signing: "+lblStatus.Text);
                check(growWatch.Count==1 && growWatch.Values.Single()==chosen && chosen.Elapsed==3,"arrival and refresh preserve only manual selections and progress");
                loadedPlayer=GrowthPlayer(100);
                var draft=new byte[]{3,4,5};
                loadedBlock=draft;
                RefreshLocalClub(club);
                loadedPlayer=GrowthPlayer(101);
                PerformanceMaintenanceTests.PumpUntil(()=>!rosterRefreshing);
                check(loadedPlayer==GrowthPlayer(101) && loadedBlock==draft,"the player selected while a refresh is in flight keeps the current editor draft");
                loadedPlayer=null;loadedBlock=null;
                clubs.Add(club);
                lstSquadClubs.Items.Add(club);lstSquadClubs.SelectedItem=club;
                lstGrowClubs.Items.Add(club);lstGrowClubs.SelectedItem=club;
                check(squadRosterDirty && growthRosterDirty,"unopened roster pages defer their row updates");
                var tabs=(TabControl)growPage.Parent;
                var tabHandle=tabs.Handle;
                tabs.SelectedTab=squadPage;
                check(!squadRosterDirty && lstSquad.Items.Count==count,"opening the squad page displays the latest roster: "+squadRosterDirty+"/"+lstSquad.Items.Count+"/"+lstSquadClubs.SelectedIndex+"/"+tabs.SelectedIndex);
                tabs.SelectedTab=growPage;
                check(!growthRosterDirty && gridGrow.Rows.Count==count,"opening the growth page displays the latest roster");

                growWatch.Clear();
                foreach(var player in players.Where(p=>p.Uid!=100))
                {
                    var target=new GrowTarget{P=player,Options=new GrowthOptions{Mode=GrowthMode.CaOnly}};
                    if(!ResetGrowthTarget(target))throw new InvalidOperationException(target.Note);
                    growWatch.Add(player.Person,target);
                }
                autoMonthKey=MonthKey(today);growDay=DayKey(today)-10;growOn=true;
                GrowTick();
                check(pendingGrowth!=null && pendingGrowth.Next>0 && pendingGrowth.Next<=32,"a busy growth day yields between whole players");
                GrowTick();
                PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
                int gain=growWatch.Values.First().Added;
                check(gain>0 && growWatch.Values.All(t=>t.Elapsed==10 && t.Added==gain),"all selected players receive the same number of days exactly once");
                GrowTick();
                check(growWatch.Values.All(t=>t.Elapsed==10 && t.Added==gain) && fm.ReadI32(GrowthPlayer(100).Tail-FmLayout.AbilityBeforePerson)==((160<<16)|100),"a second timer tick and an unselected player receive no extra growth");

                growDay-=10;GrowTick();
                int started=growWatch.Values.Count(t=>t.Elapsed==20);
                StopGrow("test pause");RunGrowthSlice();
                check(started>0 && started<count-1 && growWatch.Values.Count(t=>t.Elapsed==20)==started && !growthBatchTimer.Enabled,"stopping during a batch leaves unfinished players untouched");
                growOn=true;growDay=DayKey(today)-10;GrowTick();
                integer(start+8,(int)today-1);RunGrowthSlice();
                check(!growOn && pendingGrowth==null,"a date rewind while waiting prevents further writes");
                Safety.Off["date"]="offline unavailable";
                growOn=true;BeginGrowthBatch(1,true,0);
                PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
                check(!growOn && growWatch.Values.All(t=>t.Done && t.StartCa+t.Added==160),"explicit CA-only fill still completes and unlocks when a calendar is unavailable");
                Safety.Off.Remove("date");
                integer(start+8,(int)today);
                foreach(var player in players)
                {
                    integer(player.Tail-FmLayout.AbilityBeforePerson,(160<<16)|100);
                    Marshal.Copy(block,0,new IntPtr((long)player.Block),block.Length);
                }
                RefreshGrow();
                CheckDefaultCaGrowth(check,club,today,date=>integer(start+8,(int)date),integer);
                CheckPersonalGrowthMultipliers(check,today,date=>integer(start+8,(int)date),integer);
                CheckGrowthLimitRuntime(check,club,today,date=>integer(start+8,(int)date));
                CheckCrossClubGrowth(check,club,team,start,today,ptr,integer,vector);
            }
            finally
            {
                growOn=false;growthBatchTimer.Stop();pendingGrowth=null;
                fm=null;Safety.Off=savedSafety;FmProcess.CurrentDateRva=savedDateRva;
                Marshal.FreeHGlobal(memory);
            }
        }
    }
}
