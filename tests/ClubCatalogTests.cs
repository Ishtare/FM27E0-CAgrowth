using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class ClubCatalogTests
    {
        public static void Run(Action<bool,string> check)
        {
            using(var form=new MainForm(false,false))form.CheckPatchedClubCatalog(check);
        }
    }

    partial class MainForm
    {
        static void CheckClubSearchInput(Action<bool,string> check,TextBox search,ListBox results,Club expected,int count,string page)
        {
            search.Clear();
            SetHint(search,ClubSearch.Cue);
            check(search.TextLength==0 && results.Items.Count==count,page+" shows the complete list while the native cue is visible");
            foreach(string query in new[]{"东方巨龙","oRIENTAL Dragon","ＯＲＩＥＮＴＡＬ ＤＲＡＧＯＮ","ＵＩＤ：８３１１１７５７"})
            {
                search.Text=query;
                check(results.Items.Count==1 && ReferenceEquals(results.Items[0],expected),page+" finds the catalog record using "+query);
            }
            search.Text=ClubSearch.Cue;
            check(results.Items.Count==0,page+" filters literal typed text separately from the cue");
            search.Clear();
            check(results.Items.Count==count,page+" restores records on clearing the search");
        }

        internal void CheckPatchedClubCatalog(Action<bool,string> check)
        {
            freezeTimer.Stop();
            var handle=Handle;
            var tabs=(TabControl)growPage.Parent;
            var tabHandle=tabs.Handle;
            const int length=0x22000;
            const ulong compactType=0x810, unknownType=0x820;
            var memory=Marshal.AllocHGlobal(length);
            var savedSafety=Safety.Off;
            try
            {
                // Use only the test program's own memory.
                Marshal.Copy(new byte[length],0,memory,length);
                ulong module=(ulong)memory.ToInt64();
                fm=new FmProcess{Base=module,H=new IntPtr(-1),Version="offline-club-catalog"};
                Safety.Off=new Dictionary<string,string>();
                Action<ulong,ulong> qword=(at,value)=>Marshal.WriteInt64(new IntPtr((long)at),(long)value);
                Action<ulong,int> number=(at,value)=>Marshal.WriteInt32(new IntPtr((long)at),value);
                Action<ulong,string> name=(at,value)=>
                {
                    var bytes=Encoding.UTF8.GetBytes(value);
                    number(at,bytes.Length);
                    Marshal.Copy(bytes,0,new IntPtr((long)at+4),bytes.Length);
                };
                Func<int,int,ulong,string,string,ulong> club=(slot,uid,financeType,fullName,shortName)=>
                {
                    ulong address=module+0x1000+(ulong)slot*0x2000;
                    qword(address,module+FmProcess.ClubVtRva);number(address+12,uid);
                    if(financeType!=0)
                    {
                        qword(address+FmLayout.ClubFinance,address+0xE00);
                        qword(address+0xE00,module+financeType);
                    }
                    qword(address+FmLayout.ClubData,address+0xA00);
                    if(fullName!=null){qword(address+FmLayout.ClubName,address+0x1800);name(address+0x1800,fullName);}
                    if(shortName!=null){qword(address+FmLayout.ClubShortName,address+0x1900);name(address+0x1900,shortName);}
                    return address;
                };
                Action<ulong,ulong,ulong> team=(address,owner,first)=>
                {
                    ulong vector=address+0x1A00;
                    qword(address+FmLayout.ClubTeams,vector);
                    qword(address+FmLayout.ClubTeams+8,vector+8);
                    qword(address+FmLayout.ClubTeams+16,vector+8);
                    qword(vector,first);
                    qword(first,module+FmProcess.TeamVtRva);
                    qword(first+FmLayout.TeamClub2,owner);
                };
                ulong full=club(0,100,FmProcess.FinanceVtRva,"Test Club 1907","TC");
                ulong dragon=club(1,83111757,compactType,"Oriental Dragon Football Club","Oriental Dragon");
                ulong created=club(2,7032,0,"Imported London Club","ILC");
                ulong unfamiliar=club(3,7091,unknownType,null,"Patched Team");
                ulong noRoster=club(4,7101,compactType,"Compact Club Without Squads",null);
                ulong stale=club(5,7102,0,"Record Without Active Team",null);
                ulong invalidUid=club(6,0,compactType,"Untitled template",null);
                ulong invalidVector=club(7,7103,0,"Bad vector",null);
                ulong invalidType=club(8,7104,compactType,"Different Record",null);
                ulong invalidName=club(9,7105,compactType,"\n\0",null);
                team(full,full,full+0x700);
                team(dragon,dragon,dragon+0x700);
                team(created,created,created+0x700);
                team(unfamiliar,unfamiliar,unfamiliar+0x700);
                team(stale,full,stale+0x700);
                team(invalidVector,invalidVector,invalidVector+0x700);
                qword(invalidVector+FmLayout.ClubTeams+8,invalidVector+0x1A09);
                qword(invalidType,module+123);
                var hits=new[]{full,dragon,created,unfamiliar,noRoster,stale,invalidUid,invalidVector,invalidType,invalidName,dragon};
                var reader=new ClubCatalogReader(fm.Read,fm.ReadLenString,module,compactType);
                var found=reader.Read(hits,CancellationToken.None);
                check(found.Select(c=>c.Addr).SequenceEqual(new[]{full,dragon,created,unfamiliar,noRoster}),"discovery includes compact and owned squads but rejects duplicates and uncorroborated records");
                var money=found.Single(c=>c.Addr==full);
                var patched=found.Single(c=>c.Addr==dragon);
                check(money.HasFullFinance && ClubCatalogReader.MatchesFullFinance(fm,money),"known full finances keep the existing write path");
                check(patched.CompactFinance && !patched.HasFullFinance && patched.Finance==0 &&
                    !ClubCatalogReader.MatchesFullFinance(fm,patched),"the compact finance type is never an address for full-finance fields");
                check(found.Single(c=>c.Addr==created).Team==created+0x700 &&
                    found.Single(c=>c.Addr==unfamiliar).Name=="Patched Team","a patched club can be discovered by a team with a matching owner and a short name");
                check(found.Single(c=>c.Addr==noRoster).Team==0,"a recognized compact club may have no team data yet");
                check(ClubSearch.Matches(patched,"东方巨龙") && ClubSearch.Matches(patched,"Oriental Dragon") &&
                    ClubSearch.Matches(patched,"＃８３１１１７５７") && !ClubSearch.Matches(money,"东方巨龙") && ClubSearch.Matches(money,"1907"),
                    "patch aliases and exact UID coexist with normal number fragments in club names");
                check(ClubSearch.Matches(new Club{Name="东方龙"},"Oriental Dragon FC"),"a translated game record also matches the English name");
                var bayern=new Club{Name="FC Bayern München",ShortName="拜仁",Uid=127};
                check(ClubSearch.Matches(bayern,"bayern munchen") && ClubSearch.Matches(bayern,"拜仁"),
                    "club search matches unaccented English input and the Chinese short name on the same record");
                check(ClubSearch.Matches(new Club{Name="Test UID 127",Uid=128},"127") &&
                    !ClubSearch.Matches(new Club{Name="Test UID 127",Uid=128},"UID 127"),
                    "plain digits match name fragments while explicit UIDs require identity");
                check(ClubSearch.Matches(bayern,"　 ") && !ClubSearch.Matches(bayern,"Unrelated club"),
                    "blank searches restore the catalog and a real non-match stays empty");
                var generic=new ClubCatalogReader(fm.Read,fm.ReadLenString,module,0).Read(hits,CancellationToken.None);
                check(generic.Any(c=>c.Addr==dragon && c.Finance==0 && !c.CompactFinance) && !generic.Any(c=>c.Addr==noRoster),
                    "other builds require an owned team when their compact finance layout is unconfirmed");

                ApplyAutomaticScanResult(new ScanResult{Clubs=found});
                tabs.SelectedIndex=2;
                txtClubSearch.Text="1907";lstClubs.SelectedItem=money;
                check(loadedClub==money && !txtBalance.ReadOnly && btnApplyClub.Enabled,"the management page loads a complete club normally");
                txtClubSearch.Text="东方巨龙";lstClubs.SelectedItem=patched;
                check(lstClubs.Items.Count==1 && loadedClub==patched && lblClub.Text.Contains("简化财务") && txtBalance.ReadOnly &&
                    txtBalance.Text=="" && !btnApplyClub.Enabled && !btnFreezeClub.Enabled && !btnClubSponsors.Enabled,
                    "search and select the patched club without carrying over another club's finance preview");
                int edits=changes.Count;
                ApplyClub();
                check(FreezeMoney(patched)==0 && changes.Count==edits,"no club management writes or freezes are scheduled without complete finance");
                using(var dialog=BuildCurrencyDialog())
                {
                    var options=(ComboBox)dialog.Controls.Find("CalibrationClub",true).Single();
                    check(options.Items.Count==1 && options.Items[0]==money,"currency calibration only offers full-finance clubs");
                }
                tabs.SelectedTab=squadPage;
                txtSquadClubSearch.Text="＃８３１１１７５７";lstSquadClubs.SelectedItem=patched;
                check(lstSquadClubs.Items.Count==1 && lblSquad.Text.Contains("0 名球员") && btnRefreshSquad.Enabled,
                    "the squad page can search the patched club by UID and load its empty roster");
                RefreshLocalClub(patched);
                PerformanceMaintenanceTests.PumpUntil(()=>!rosterRefreshing);
                check(lblStatus.Text.Contains("名单已刷新") && lblStatus.Text.Contains("共 0 人"),"the normal local refresh can read the empty team without a stadium or staff");
                tabs.SelectedTab=growPage;
                txtGrowClubSearch.Text="东方龙";
                check(lstGrowClubs.Items.Count==1 && lstGrowClubs.SelectedItem==patched && btnRefreshGrowth.Enabled,
                    "the growth picker can find the same club with a Chinese short alias");
                growthMultiClub.Checked=true;
                txtGrowClubSearch.Text="Oriental Dragon";
                check(growthClubChecks.Items.Count==1 && growthClubChecks.CheckedItems[0]==patched,"the multi-club picker shares the English and Chinese search");
                tabs.SelectedIndex=2;
                txtClubSearch.Text="1907";lstClubs.SelectedItem=money;
                check(btnApplyClub.Enabled && !txtBalance.ReadOnly && !txtClubName.ReadOnly,"full editing is restored when switching back");
                qword(money.Finance,module+compactType);
                ApplyClub();
                check(!btnApplyClub.Enabled && loadedFin==null && changes.Count==edits,"stale previews cannot apply after a club changes to compact finances");

                CheckClubSearchInput(check,txtClubSearch,lstClubs,patched,found.Count,"club management");
                tabs.SelectedTab=squadPage;
                CheckClubSearchInput(check,txtSquadClubSearch,lstSquadClubs,patched,found.Count,"squad roster");
                tabs.SelectedTab=growPage;
                CheckClubSearchInput(check,txtGrowClubSearch,growthClubChecks,patched,found.Count,"multi-club growth");
                growthMultiClub.Checked=false;
                CheckClubSearchInput(check,txtGrowClubSearch,lstGrowClubs,patched,found.Count,"single-club growth");
            }
            finally
            {
                fm=null;
                Safety.Off=savedSafety;
                Marshal.FreeHGlobal(memory);
            }
        }
    }
}
