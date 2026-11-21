// Modified build v1.1.23, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        readonly Button btnRefreshSquad=new Button{Name="RefreshSquadRoster",Text="刷新本队名单",Width=122,Enabled=false};
        readonly Button btnRefreshGrowth=new Button{Name="RefreshGrowthRoster",Text="刷新本队名单",Width=122,Enabled=false};
        readonly Button btnApplyPlayer=new Button{Text="应用修改",Width=120,Enabled=false};
        bool rosterRefreshing,genderFilterVerified;
        ulong? scannedSaveLoads;
        readonly RosterReadWorker rosterWorker=new RosterReadWorker();
        bool reconnectAfterRead,closingEditor,rosterViewsQueued;

        void FinishRosterRead()
        {
            if(closingEditor || IsDisposed)return;
            UpdateRosterRefreshButtons();
            if(!reconnectAfterRead)return;
            reconnectAfterRead=false;
            StartScan();
        }

        static ulong? SaveLoadCount(FmProcess process)
        {
            return CurrentBuildProfile.Matches(process)?
                CurrentBuildProfile.Active.ReadSaveLoads(process.Read,process.Base):null;
        }

        void VerifyRosterConnection()
        {
            if(fm==null || !btnScan.Enabled || !Safety.On("core"))
                throw new InvalidOperationException("请先连接已载入存档的 FM，并完成名单检查。");
            if(CurrentBuildProfile.Matches(fm) && CurrentBuildProfile.Active.GameUiRoot!=0 &&
                (!scannedSaveLoads.HasValue || SaveLoadCount(fm)!=scannedSaveLoads))
                throw new InvalidOperationException("当前存档与上次扫描不一致，或游戏仍在载入，请重新挂接。");
        }

        void UpdateRosterRefreshButtons()
        {
            bool ready=fm!=null && btnScan.Enabled && Safety.On("core") && !rosterRefreshing && !autoScanning;
            btnRefreshSquad.Enabled=ready && lstSquadClubs.SelectedItem is Club;
            int count=growthMultiClub.Checked?checkedGrowthClubs.Count:(lstGrowClubs.SelectedItem is Club?1:0);
            btnRefreshGrowth.Text=count>1?"刷新球队…":"刷新本队名单";
            btnRefreshGrowth.Enabled=ready && count>0;
        }

        static Player DecodeRefreshedPlayer(FmProcess fm,ulong person,ulong extra,byte[] head)
        {
            string name,search;
            PersonNameParts parts;
            fm.ReadPersonNames(head,out name,out search,out parts);
            ulong nation=BitConverter.ToUInt64(head,(int)FmLayout.Nation);
            int nationUid=0;
            if(nation>=0x10000 && CurrentBuildProfile.Matches(fm) && fm.ReadPtr(nation)==fm.Base+EligibilityLayout.NationVt)
                nationUid=fm.ReadI32(nation+FmLayout.Uid)??0;
            return new Player
            {
                Person=person, Extra=extra, Uid=BitConverter.ToInt32(head,(int)FmLayout.Uid), Name=name,
                Search=search, NameParts=parts, NameNationUid=nationUid, Born=BornFrom(head),
                Female=GenderFilter.Female(head,fm.Version,fm.VerifiedCurrentBuild)
            };
        }

        ClubRosterReader CurrentRosterReader(CancellationToken token=default(CancellationToken))
        {
            var connection=fm;
            bool filterWomen=genderFilterVerified;
            Func<ulong,int,byte[]> read=connection.Read;
            if(token.CanBeCanceled)read=(address,count)=>
            {
                token.ThrowIfCancellationRequested();
                return connection.Read(address,count);
            };
            return new ClubRosterReader(read,GrowthReadLayout.From(connection),
                (person,extra,head)=>DecodeRefreshedPlayer(connection,person,extra,head),
                player=>!filterWomen || player.Female!=true);
        }

        static bool SameRosterPlayer(Player previous,Player current)
        {
            var old=previous.Roster;
            var fresh=current.Roster;
            return previous.Person==current.Person && previous.Extra==current.Extra && previous.Club==current.Club &&
                previous.Name==current.Name && previous.Search==current.Search && previous.Born==current.Born &&
                previous.NameNationUid==current.NameNationUid && old!=null && fresh!=null && old.Team==fresh.Team &&
                old.PlayingClub==fresh.PlayingClub && old.ContractClub==fresh.ContractClub && old.ContractTeam==fresh.ContractTeam &&
                old.Contract==fresh.Contract && old.Loan==fresh.Loan && old.LoanEnd==fresh.LoanEnd;
        }

        Player RefreshSelectedPlayer(Player requested)
        {
            VerifyRosterConnection();
            if(autoScanning)throw new InvalidOperationException("全局名单正在刷新，请稍后重新打开球员资料。");
            requested=RosterIndex.Unique(requested.Uid)??requested;
            var update=CurrentRosterReader().ReadPlayer(requested,GrowthToday());
            var current=update.Refreshed[requested.Uid];
            VerifyRosterConnection();
            if(SameRosterPlayer(requested,current))return requested;
            AcceptRosterUpdate(update);
            if(IsHandleCreated && !rosterRefreshing && !rosterViewsQueued)
            {
                rosterViewsQueued=true;
                var connection=fm;
                BeginInvoke((Action)(()=>
                {
                    rosterViewsQueued=false;
                    if(closingEditor || fm!=connection || autoScanning || !btnScan.Enabled)return;
                    RebuildGrowthScopes();
                    LoadGrow();
                    LoadSquad();
                }));
            }
            return current;
        }

        void AcceptRosterUpdate(ClubRosterUpdate update)
        {
            // Reuse name matches for familiar players. For new identities include possible same-name or
            // same-birthday matches from the directory so a local refresh keeps the normal disambiguation.
            var namesToRefresh=new List<Player>();
            var index=RosterIndex;
            foreach(var fresh in update.Refreshed.Values)
            {
                var matches=index.ForUid(fresh.Uid).Where(p=>p.Name==fresh.Name && p.Search==fresh.Search &&
                    p.Born==fresh.Born && p.NameNationUid==fresh.NameNationUid).ToArray();
                if(matches.Length==1)
                {
                    var prior=matches[0];
                    fresh.ChineseName=prior.ChineseName;
                    fresh.NameAliases=prior.NameAliases;
                    fresh.NameSource=prior.NameSource;
                    fresh.FallbackName=prior.FallbackName;
                }
                else namesToRefresh.Add(fresh);
            }
            var merged=update.Merge(players);
            if(namesToRefresh.Count>0)
            {
                var birthdays=new HashSet<DateTime>(namesToRefresh.Where(p=>p.Born.HasValue).Select(p=>p.Born.Value));
                var names=new HashSet<string>(namesToRefresh.SelectMany(p=>p.OriginalNameKeys()),StringComparer.Ordinal);
                var pending=new HashSet<Player>(namesToRefresh);
                var peers=merged.Where(p=>!pending.Contains(p) &&
                    (p.Born.HasValue && birthdays.Contains(p.Born.Value) || names.Overlaps(p.OriginalNameKeys())))
                    .Select(p=>new Player{Uid=p.Uid,Name=p.Name,Search=p.Search,Born=p.Born}).ToList();
                PlayerNames.Apply(namesToRefresh.Concat(peers).ToList());
            }
            index.Apply(update);
            players=merged;
            growthDirectory.UpdateFrom(update.Directory,update.Club);
            CarryGrowth("刷新当前名单",update);
        }

        async void RefreshLocalClub(Club club)
        {
            if(club==null || rosterWorker.Busy || rosterRefreshing || autoScanning)return;
            try
            {
                VerifyRosterConnection();
                rosterRefreshing=true;
                UpdateRosterRefreshButtons();
                lblStatus.Text="正在刷新 "+club.Name+" 的球队名单…";
                var snapshot=players;
                var currentIndex=RosterIndex;
                long previousDetails=personDetailsRevision;
                var before=new HashSet<int>(currentIndex.ForClub(club.Addr).Select(p=>p.Uid));
                DateTime? today=GrowthToday();
                var update=await rosterWorker.Run(token=>CurrentRosterReader(token).ReadClub(club,snapshot,today));
                if(closingEditor || IsDisposed)return;
                VerifyRosterConnection();
                if(!object.ReferenceEquals(snapshot,players) || rosterIndex!=currentIndex || previousDetails!=personDetailsRevision)
                    throw new InvalidOperationException("读取期间球员名单已更新，请再点一次刷新。");
                AcceptRosterUpdate(update);
                var current=new HashSet<int>(update.Refreshed.Values.Where(p=>p.Club==club.Addr).Select(p=>p.Uid));
                var selected=loadedPlayer;
                var fresh=selected==null?null:GrowthPlayer(selected.Uid);
                FilterPlayerResults(fresh);
                if(selected!=null)
                {
                    if(fresh!=null && update.Refreshed.ContainsKey(fresh.Uid))
                    {
                        if(SameRosterPlayer(selected,fresh))loadedPlayer=fresh;
                        else LoadPlayer(fresh);
                    }
                    else if(update.Removed.Contains(selected.Uid))ClearPlayerPreview("所选球员已不在当前名单中。");
                }
                RebuildGrowthScopes();
                LoadGrow();
                LoadSquad();
                lblStatus.Text=club.Name+" 名单已刷新，共 "+update.CurrentPlayers+" 人，新增 "+current.Except(before).Count()+
                    " 人，移出 "+before.Except(current).Count()+" 人。";
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                if(closingEditor || IsDisposed)return;
                string reason=ex.Message.Replace("注册数据","名单资料");
                lblStatus.Text="刷新本队名单未完成，保留上次确认的名单。"+reason;
                LogLine("刷新本队名单未完成："+reason);
            }
            finally
            {
                rosterRefreshing=false;
                FinishRosterRead();
            }
        }

        void ClearPlayerPreview(string reason)
        {
            loadedPlayer=null;
            loadedBlock=null;
            loadedShirt=null;
            gridAttrs.Rows.Clear();
            gridPos.Rows.Clear();
            btnApplyPlayer.Enabled=false;
            btnOptimizeAttributes.Enabled=false;
            lblPlayer.Text=reason;
        }
    }
}
