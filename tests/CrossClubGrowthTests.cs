// Modified build v1.1.23. Extends the offline roster fixture in PerformanceMaintenanceTests.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        static void ClickGrowthTestButton(Button button)
        {
            if(!button.Enabled)throw new InvalidOperationException("Disabled command: "+button.Text);
            typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});
        }

        void CheckCrossClubGrowth(Action<bool,string> check,Club first,ulong firstTeam,ulong start,uint today,
            Action<ulong,ulong> ptr,Action<ulong,int> integer,Action<ulong,ulong,ulong[]> vector)
        {
            var como=new Club{Addr=start+0x1800,Team=start+0x2000,Uid=22,Name="科莫"};
            var madrid=new Club{Addr=start+0x2500,Team=start+0x2B00,Uid=33,Name="皇家马德里"};
            first.Name="曼城";
            var second=GrowthPlayer(102);
            var third=GrowthPlayer(103);
            var loanee=GrowthPlayer(104);
            var people=new[]{second,third};
            var added=new[]{como,madrid};
            foreach(var c in added)clubs.Add(c);
            for(int i=0;i<added.Length;i++)
            {
                var c=added[i];var p=people[i];
                ptr(c.Addr,start+FmProcess.ClubVtRva);integer(c.Addr+12,c.Uid);
                ptr(c.Team,start+FmProcess.TeamVtRva);ptr(c.Team+FmLayout.TeamClub,c.Addr);
                ptr(c.Team+FmLayout.TeamClub2,c.Addr);
                vector(c.Addr+FmLayout.ClubTeams,start+0x3100+(ulong)i*0x20,new[]{c.Team});
                vector(c.Team+FmLayout.TeamPlayers,start+0x3300+(ulong)i*0x20,new[]{p.Tail-FmLayout.PlayerObjBeforePerson});
                ptr(p.Tail-FmLayout.TeamBeforePerson,c.Team);ptr(p.Roster.Contract+FmLayout.ContractTeam,c.Team);
            }
            ptr(loanee.Tail-FmLayout.TeamBeforePerson,como.Team);
            ulong holder=loanee.Person+0x1400, deal=loanee.Person+0x1500;
            ptr(loanee.Tail-FmLayout.PlayerObjBeforePerson+FmLayout.PlayerLoanContract,holder);
            ptr(holder,deal);ptr(deal,start+FmProcess.LoanContractVtRva);ptr(deal+8,loanee.Person);
            ptr(deal+FmLayout.ContractTeam,como.Team);integer(deal+FmLayout.ContractExpiry,(2027<<16)|181);
            vector(como.Team+FmLayout.TeamPlayers,start+0x3300,new[]{second.Tail-FmLayout.PlayerObjBeforePerson,loanee.Tail-FmLayout.PlayerObjBeforePerson});
            vector(firstTeam+FmLayout.TeamPlayers,start+0x1300,players.Where(p=>!people.Contains(p) && p!=loanee).Select(p=>p.Tail-FmLayout.PlayerObjBeforePerson).ToArray());
            growWatch.Clear();growthPreferences.Clear();
            foreach(var club in new[]{first,como,madrid})
            {
                RefreshLocalClub(club);
                PerformanceMaintenanceTests.PumpUntil(()=>!rosterRefreshing);
                check(lblStatus.Text.Contains("名单已刷新"),"offline team refreshed: "+club.Name+" "+lblStatus.Text);
            }
            FilterGrowClubs();
            Func<int,DataGridViewRow> row=uid=>gridGrow.Rows.Cast<DataGridViewRow>().Single(r=>((Player)r.Tag).Uid==uid);
            Func<int[]> visible=()=>gridGrow.Rows.Cast<DataGridViewRow>().Select(r=>((Player)r.Tag).Uid).OrderBy(id=>id).ToArray();
            Action<int,bool> watch=(uid,on)=>row(uid).Cells[0].Value=on;
            lstGrowClubs.SelectedItem=first;
            watch(100,true);watch(101,true);
            lstGrowClubs.SelectedItem=como;watch(102,true);
            SaveGrowthOptions(100,new GrowthOptions{Mode=GrowthMode.CaOnly});
            SaveGrowthOptions(102,new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}});
            lstGrowClubs.SelectedItem=madrid;txtGrowPlayerSearch.Text="#103";
            check(visible().SequenceEqual(new[]{103}) && growWatch.Count==3 && btnGrowToggle.Text.Contains("3 人"),"browsing a third club keeps the complete three-player queue");
            onlyGrowChecked.Checked=true;
            check(gridGrow.Rows.Count==0 && growWatch.Count==3,"the local checked-only filter still applies to a third club");
            ClickGrowthTestButton(btnViewGrowthQueue);
            check(visible().SequenceEqual(new[]{100,101,102}) && txtGrowPlayerSearch.Text=="" && splitGrow.Panel1Collapsed,"the queue opens on everyone regardless of club and browser search");
            check((string)row(100).Cells[14].Value=="曼城" && (string)row(102).Cells[14].Value=="科莫" &&
                gridGrow.Columns[14].Visible && gridGrow.Columns[7].Visible && gridGrow.Columns[9].Visible,"the global view displays the playing club, plan and progress");
            txtGrowPlayerSearch.Text="科莫";
            check(visible().SequenceEqual(new[]{102}) && btnViewGrowthQueue.Text.Contains("3"),"filtering the queue by playing club changes only the visible rows");
            txtGrowPlayerSearch.Text="";
            gridGrow.CurrentCell=row(101).Cells[1];ClickGrowthTestButton(btnRemoveGrowthPlayer);
            check(visible().SequenceEqual(new[]{100,102}) && btnGrowToggle.Text.Contains("2 人"),"removing the selected player updates both the queue and the start count");
            watch(102,false);
            PerformanceMaintenanceTests.PumpUntil(()=>gridGrow.Rows.Count==1);
            check(growWatch.Count==1 && visible().SequenceEqual(new[]{100}),"unchecking a queue row removes it after the checkbox event");
            ClickGrowthTestButton(btnBrowseGrowthClubs);
            check(txtGrowPlayerSearch.Text=="#103" && onlyGrowChecked.Checked && gridGrow.Rows.Count==0,"returning to browsing restores its independent query and checked-only filter");
            onlyGrowChecked.Checked=false;
            check(visible().SequenceEqual(new[]{103}),"clearing the candidate-only filter again shows the third club");

            txtGrowPlayerSearch.Text="";growthMultiClub.Checked=true;
            Action<Club,bool> pick=(club,on)=>growthClubChecks.SetItemChecked(growthClubChecks.Items.IndexOf(club),on);
            pick(madrid,false);
            txtGrowClubSearch.Text="曼城";pick(first,true);
            txtGrowClubSearch.Text="科莫";pick(como,true);
            txtGrowClubSearch.Text="找不到的俱乐部";
            check(growthClubChecks.Items.Count==0 && checkedGrowthClubs.Count==2 && visible().Contains(100) && visible().Contains(102) && !visible().Contains(103),
                "checked clubs from separate searches continue contributing candidates after the filter hides them");
            txtGrowClubSearch.Text="";
            check(growthClubChecks.CheckedItems.Count==2 && gridGrow.Columns[14].Visible,"club check marks and playing club column are restored");
            var branch=growSquadFilter.Items.Cast<GrowthSquadChoice>().Single(choice=>choice.SourceClub==como.Addr && choice.Kind==GrowthSquadKind.Team);
            growLoanFilter.SelectedIndex=3;
            check(visible().SequenceEqual(new[]{104}),"the combined outgoing scope finds the parent club's active loan");
            watch(104,true);
            growSquadFilter.SelectedItem=branch;
            check(visible().SequenceEqual(new[]{102,104}) && (bool)row(104).Cells[0].Value,"the borrowed player is already checked when browsing the receiving squad");
            txtGrowPlayerSearch.Text="#104";ClickGrowthTestButton(btnGrowAll);
            ClickGrowthTestButton(btnViewGrowthQueue);
            check(visible().SequenceEqual(new[]{100,104}) && (string)row(104).Cells[14].Value=="科莫" &&
                (string)row(104).Cells[11].Value=="租出","two club entry points produce one queue row and distinguish the playing club from ownership");
            gridGrow.CurrentCell=row(104).Cells[1];ClickGrowthTestButton(btnRemoveGrowthPlayer);
            ClickGrowthTestButton(btnBrowseGrowthClubs);
            growSquadFilter.SelectedIndex=0;txtGrowPlayerSearch.Text="#102";
            ClickGrowthTestButton(btnGrowAll);ClickGrowthTestButton(btnGrowAll);
            check(growWatch.Count==2 && growWatch.Values.Select(t=>t.P.Uid).OrderBy(id=>id).SequenceEqual(new[]{100,102}),"select results adds just the visible player and repeated clicks are idempotent");
            ClickGrowthTestButton(btnGrowNone);
            check(growWatch.Count==1 && growWatch.Values.Single().P.Uid==100,"unselect results leaves a hidden club's selection intact");
            ClickGrowthTestButton(btnGrowAll);
            RefreshLocalClub(first);
            PerformanceMaintenanceTests.PumpUntil(()=>!rosterRefreshing);
            check(growWatch.Count==2 && growWatch.Values.Any(t=>t.P.Club==como.Addr),"refreshing one club preserves the other club's selection");
            gridGrow.CurrentCell=row(102).Cells[1];
            ClickGrowthTestButton(btnViewGrowthQueue);
            gridGrow.CurrentCell=row(100).Cells[1];
            ClickGrowthTestButton(btnBrowseGrowthClubs);
            check(GrowthSelectedUid()==102 && txtGrowPlayerSearch.Text=="#102" && checkedGrowthClubs.Count==2,
                "switching back restores the browsing selection and keeps both checked clubs");
            ClickGrowthTestButton(btnViewGrowthQueue);
            check(GrowthSelectedUid()==100 && visible().SequenceEqual(new[]{100,102}) && growWatch.Count==2,
                "the queue restores its own selection with both enrolled players");
            ClickGrowthTestButton(btnBrowseGrowthClubs);
            pick(first,false);pick(como,false);
            check(gridGrow.Rows.Count==0 && growWatch.Count==2,"clearing every club filter leaves both players enrolled");
            ClickGrowthTestButton(btnViewGrowthQueue);txtGrowPlayerSearch.Text="科莫";
            check(visible().SequenceEqual(new[]{102}) && growWatch[GrowthPlayer(102).Person].Options.Attributes.SequenceEqual(new[]{34,38}),"adding a previously configured player restores his individual plan");
            int oldCa=100;
            var runners=new[]{GrowthPlayer(100),GrowthPlayer(102)};
            var before=runners.Select(p=>fm.Read(p.Block+15,54)).ToArray();
            autoMonthKey=MonthKey(today);
            ClickGrowthTestButton(btnGrowToggle);
            check(growOn && !btnRemoveGrowthPlayer.Enabled && btnGrowToggle.Text.Contains("2 人"),"start from a filtered queue runs and locks the entire two-player plan");
            ClickGrowthTestButton(btnBrowseGrowthClubs);
            integer(start+8,(int)today+10);
            GrowTick();
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            var grown=players.Where(p=>BitConverter.ToInt16(fm.Read(p.Tail-FmLayout.AbilityBeforePerson,4),0)>oldCa).Select(p=>p.Uid).OrderBy(id=>id).ToArray();
            check(grown.SequenceEqual(new[]{100,102}) && growWatch.Values.All(t=>t.Elapsed==10),"only the two confirmed players in different clubs gain CA across a game-day change");
            var after=runners.Select(p=>fm.Read(p.Block+15,54)).ToArray();
            var changed=Enumerable.Range(0,54).Where(i=>before[1][i]!=after[1][i]).ToArray();
            check(before[0].SequenceEqual(after[0]) && changed.Length>0 && changed.All(i=>i==34 || i==38),
                "CA-only and selected-attributes plans execute independently in the same multi-club run");
            ClickGrowthTestButton(btnGrowToggle);
            ClickGrowthTestButton(btnViewGrowthQueue);
            check(!growOn && visible().SequenceEqual(new[]{100,102}),"stopping and reopening the queue retains all valid participants");
            CheckCrossClubBatchSettings(check,today,date=>integer(start+8,(int)date),row);
            CheckOverlapAndClubRebinding(check,first,como);
        }

        DialogResult SubmitGrowthBatch(GrowthMode? mode,bool? limit,int? gear,out string feedback)
        {
            using(var dialog=BuildGrowthBatchSettingsDialog())
            {
                ((ComboBox)dialog.Controls.Find("GrowthBatchMode",true).Single()).SelectedIndex=mode.HasValue?(int)mode.Value+1:0;
                ((ComboBox)dialog.Controls.Find("GrowthBatchLimit",true).Single()).SelectedIndex=limit.HasValue?(limit.Value?1:2):0;
                ((ComboBox)dialog.Controls.Find("GrowthBatchSpeed",true).Single()).SelectedIndex=gear.HasValue?gear.Value-1:0;
                ClickGrowthTestButton((Button)dialog.Controls.Find("ApplyGrowthBatchSettings",true).Single());
                feedback=dialog.Controls.Find("GrowthBatchProblem",true).Single().Text;
                return dialog.DialogResult;
            }
        }

        void CheckCrossClubBatchSettings(Action<bool,string> check,uint today,Action<uint> setDate,Func<int,DataGridViewRow> row)
        {
            var runners=new[]{GrowthPlayer(100),GrowthPlayer(102)};
            var order=new[]{34,38};
            Func<Player,GrowTarget> target=p=>growWatch[p.Person];
            check(btnGrowBatch.Enabled && btnGrowBatch.Text.Contains("整份培养名单"),
                "the queued players enable the action with an explicit full-queue label");
            gridGrow.CurrentCell=row(100).Cells[1];
            int history=changes.Count;
            string problem;
            check(SubmitGrowthBatch(GrowthMode.SelectedAttributes,true,6,out problem)==DialogResult.None &&
                problem.Contains("至少勾选") && GrowGear()==4 &&
                target(runners[0]).Options.Mode==GrowthMode.CaOnly && !target(runners[1]).Options.LimitsUnselected,
                "a missing custom selection leaves both clubs and the speed untouched");

            txtGrowPlayerSearch.Text="科莫";
            gridGrow.CurrentCell=row(102).Cells[1];
            check(SubmitGrowthBatch(GrowthMode.SelectedAttributes,true,6,out problem)==DialogResult.OK &&
                runners.All(p=>target(p).Options.Attributes.SequenceEqual(order) && target(p).Options.LimitsUnselected && target(p).Focus.HasLimits),
                "one batch uses the selected custom template where missing and enables caps across both clubs");
            check(growWatch.Count==2 && !growthPreferences.ContainsKey(103) && changes.Count==history &&
                GrowthSelectedUid()==102 && txtGrowPlayerSearch.Text=="科莫",
                "the batch affects only the full watched set and preserves the queue search, selection and game data");
            check(GrowGear()==6 && runners.All(p=>target(p).Span==GrowSpanFor(target(p).StartCa,target(p).Cap,6)),
                "the selected rate is shared by both players' next run");

            setDate(today);autoMonthKey=MonthKey(today);
            ClickGrowthTestButton(btnGrowToggle);
            check(growOn && !btnGrowBatch.Enabled && growthLimitTargets.Length==2,
                "the batch is locked during a run and both opted-in caps are scheduled");
            var before=runners.Select(p=>fm.Read(p.Block+15,54)).ToArray();
            setDate(today+10);GrowTick();
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            for(int player=0;player<runners.Length;player++)
            {
                var after=fm.Read(runners[player].Block+15,54);
                var changed=Enumerable.Range(0,54).Where(i=>after[i]!=before[player][i]).ToArray();
                check(changed.Length>0 && changed.All(order.Contains),"a batched custom plan writes only its selected attributes");
            }
            ClickGrowthTestButton(btnGrowToggle);

            check(SubmitGrowthBatch(null,false,null,out problem)==DialogResult.OK && runners.All(p=>!target(p).Focus.HasLimits &&
                target(p).Options.Mode==GrowthMode.SelectedAttributes && !target(p).Options.LimitsUnselected),
                "one batch turns off the ceiling across clubs while preserving custom mode and its selection");
            check(SubmitGrowthBatch(GrowthMode.CaOnly,true,8,out problem)==DialogResult.OK &&
                runners.All(p=>!target(p).UseFocus && target(p).Focus==null && !target(p).Options.LimitsUnselected &&
                    target(p).Options.Attributes.SequenceEqual(order)),
                "switching to system allocation ignores the cap request and retains custom selections for a later switch");

            setDate(today);
            ClickGrowthTestButton(btnGrowToggle);
            check(growOn && GrowGear()==8 && growthLimitTargets.Length==0,"the new rate starts automatic growth without a cap schedule");
            history=changes.Count;
            var positionsAndAttributes=runners.Select(p=>fm.Read(p.Block,69)).ToArray();
            setDate(today+10);GrowTick();
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            var abilityAddresses=runners.Select(p=>p.Tail-FmLayout.AbilityBeforePerson).ToArray();
            var edits=changes.Skip(history).ToArray();
            check(edits.Length==2 && edits.All(c=>abilityAddresses.Contains(c.Addr) && c.New.Length==2) &&
                runners.Select((p,index)=>fm.Read(p.Block,69).SequenceEqual(positionsAndAttributes[index])).All(same=>same),
                "the same two players' automatic run writes only CA and leaves every attribute and position byte unchanged");
            ClickGrowthTestButton(btnGrowToggle);

            var custom=new[]{new[]{34},new[]{38}};
            for(int i=0;i<runners.Length;i++)
            {
                var options=target(runners[i]).Options.Clone();
                options.Attributes=custom[i];
                SaveGrowthOptions(runners[i].Uid,options);
            }
            Func<bool> sameSelections=()=>runners
                .Select((p,index)=>target(p).Options.Attributes.SequenceEqual(custom[index])).All(same=>same);
            check(SubmitGrowthBatch(GrowthMode.SelectedAttributes,null,null,out problem)==DialogResult.OK && sameSelections() &&
                runners.All(p=>target(p).Options.Mode==GrowthMode.SelectedAttributes),
                "batch mode switching restores both existing, different attribute selections without copying the visible template");
            check(SubmitGrowthBatch(null,true,null,out problem)==DialogResult.OK && sameSelections() &&
                runners.All(p=>target(p).Options.LimitsUnselected && target(p).Focus.HasLimits),
                "toggling caps on preserves each player's own selection across clubs");
            check(SubmitGrowthBatch(null,null,5,out problem)==DialogResult.OK && sameSelections() &&
                runners.All(p=>target(p).Options.LimitsUnselected && target(p).Span==GrowSpanFor(target(p).StartCa,target(p).Cap,5)),
                "a speed-only change keeps each player's attributes, cap and mode");
            check(SubmitGrowthBatch(null,false,null,out problem)==DialogResult.OK && sameSelections() &&
                runners.All(p=>!target(p).Options.LimitsUnselected && target(p).Options.Mode==GrowthMode.SelectedAttributes) &&
                gridGrow.Rows.Count==1 && GrowthSelectedUid()==102 && btnViewGrowthQueue.Text.Contains("2"),
                "toggling caps off preserves distinct selections for the visible and filtered-out queued players");
        }

        void CheckOverlapAndClubRebinding(Action<bool,string> check,Club parent,Club borrower)
        {
            var directory=new GrowthDirectory();
            var shared=new GrowthTeam{Club=borrower.Addr,Address=borrower.Team,Name="U20 青年队"};
            directory.Teams.Add(shared.Address,shared);
            foreach(var c in new[]{parent,borrower})directory.ClubTeams.Add(c.Addr,new List<GrowthTeam>{shared});
            var loan=new Player{Uid=700,Person=0x70000,Club=borrower.Addr,Roster=new GrowthMembership{Contract=1,Team=shared.Address,
                PlayingClub=borrower.Addr,ContractClub=parent.Addr,Loan=GrowthLoanState.Active}};
            var ambiguous=new Player{Uid=701,Person=0x70100,Club=parent.Addr};
            var other=new Player{Uid=701,Person=0x70200,Club=borrower.Addr};
            var idx=new PlayerRosterIndex(new[]{loan,ambiguous,other});
            var choices=GrowthRosterView.ChoicesForClubs(directory,new[]{parent,borrower});
            check(idx.ForScope(directory,choices[0]).SequenceEqual(new[]{loan}),"overlapping parent, affiliate and playing squads include a player once and reject ambiguous UIDs");
            var outgoing=choices.Single(s=>s.Parts!=null && s.Kind==GrowthSquadKind.OutOnLoan);
            check(idx.ForScope(directory,outgoing).Single()==loan && GrowthRosterView.Contains(directory,outgoing,loan),"an outgoing loan can also be reached from its verified parent");
            checkedGrowthClubs[GrowthClubKey(parent)]=parent;checkedGrowthClubs[GrowthClubKey(borrower)]=borrower;
            var current=new Club{Uid=parent.Uid,Addr=parent.Addr+0x100,Name=parent.Name};
            clubs=new List<Club>{current};RebindGrowthClubs();
            check(checkedGrowthClubs.Count==1 && checkedGrowthClubs.Values.Single()==current,"a roster rescan refreshes checked club objects by identity and drops absent clubs");
        }
    }
}
