// Modified build v1.1.23, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        GrowthDirectory growthDirectory=new GrowthDirectory();
        readonly ComboBox growSquadFilter=new ComboBox{Name="GrowthSquadFilter",Width=240,DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=480};
        readonly ComboBox growLoanFilter=new ComboBox{Name="GrowthLoanFilter",Width=120,DropDownStyle=ComboBoxStyle.DropDownList};
        readonly Dictionary<ulong,string> growthSquadViews=new Dictionary<ulong,string>();
        readonly Dictionary<ulong,int> growthLoanViews=new Dictionary<ulong,int>();
        bool growScopeFilling;
        Control BuildGrowthScopeBar()
        {
            var bar=new FlowLayoutPanel{Name="GrowthScopeToolbar",Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,3,0,0)};
            bar.Controls.Add(new Label{Text="队伍",AutoSize=true,Padding=new Padding(0,6,4,0)});bar.Controls.Add(growSquadFilter);
            bar.Controls.Add(new Label{Text="租借",AutoSize=true,Padding=new Padding(8,6,4,0)});bar.Controls.Add(growLoanFilter);
            StyleButton(btnRefreshGrowth);bar.Controls.Add(btnRefreshGrowth);
            btnRefreshGrowth.Click+=delegate{RefreshGrowthClubMenu();};
            growLoanFilter.Items.AddRange(new object[]{"全部状态","未租借","租入","租出","待确认"});growLoanFilter.SelectedIndex=0;
            growSquadFilter.SelectedIndexChanged+=delegate
            {
                if(growScopeFilling)return;
                var scope=growSquadFilter.SelectedItem as GrowthSquadChoice;
                if(scope!=null && scope.Kind==GrowthSquadKind.OutOnLoan){growScopeFilling=true;growLoanFilter.SelectedIndex=3;growScopeFilling=false;}
                else if(growLoanFilter.SelectedIndex==3){growScopeFilling=true;growLoanFilter.SelectedIndex=0;growScopeFilling=false;}
                RememberGrowthScope();LoadGrow();
            };
            growLoanFilter.SelectedIndexChanged+=delegate
            {
                if(growScopeFilling)return;
                if(growLoanFilter.SelectedIndex==3)
                {
                    var current=growSquadFilter.SelectedItem as GrowthSquadChoice;
                    var loaned=growSquadFilter.Items.Cast<GrowthSquadChoice>().FirstOrDefault(s=>s.Kind==GrowthSquadKind.OutOnLoan &&
                        (current==null || current.SourceClub==s.SourceClub));
                    if(loaned!=null){growScopeFilling=true;growSquadFilter.SelectedItem=loaned;growScopeFilling=false;}
                }
                else if(growSquadFilter.SelectedItem is GrowthSquadChoice && ((GrowthSquadChoice)growSquadFilter.SelectedItem).Kind==GrowthSquadKind.OutOnLoan)
                {growScopeFilling=true;growSquadFilter.SelectedIndex=0;growScopeFilling=false;}
                RememberGrowthScope();LoadGrow();
            };
            return bar;
        }
        void RememberGrowthScope()
        {
            var selected=GrowthBrowseClubs();var scope=growSquadFilter.SelectedItem as GrowthSquadChoice;
            if(scope==null || selected.Count==0)return;
            if(selected.Count==1){growthSquadViews[selected[0].Addr]=scope.Key;growthLoanViews[selected[0].Addr]=growLoanFilter.SelectedIndex;}
            else {combinedGrowthScopeKey=scope.Key;combinedGrowthLoan=growLoanFilter.SelectedIndex;}
        }
        void RebuildGrowthScopes()
        {
            var clubsToBrowse=GrowthBrowseClubs();
            var club=clubsToBrowse.Count==1?clubsToBrowse[0]:null;
            growScopeFilling=true;
            try
            {
                growSquadFilter.Items.Clear();
                var choices=GrowthRosterView.ChoicesForClubs(growthDirectory,clubsToBrowse);growSquadFilter.Items.AddRange(choices.Cast<object>().ToArray());
                string key=combinedGrowthScopeKey;int loan=combinedGrowthLoan;
                if(club!=null){growthSquadViews.TryGetValue(club.Addr,out key);growthLoanViews.TryGetValue(club.Addr,out loan);}
                var selected=choices.FirstOrDefault(s=>s.Key==key);
                if(selected!=null)growSquadFilter.SelectedItem=selected;else if(choices.Count>0)growSquadFilter.SelectedIndex=0;
                growLoanFilter.SelectedIndex=selected==null?0:loan;
                growSquadFilter.Enabled=growLoanFilter.Enabled=clubsToBrowse.Count>0;
            }
            finally{growScopeFilling=false;}
            UpdateRosterRefreshButtons();
        }
        List<Player> GrowthScopedPlayers()
        {
            var scope=growSquadFilter.SelectedItem as GrowthSquadChoice;
            var members = RosterIndex.ForScope(growthDirectory,scope);
            string loan = growLoanFilter.SelectedIndex > 0 ? (string)growLoanFilter.SelectedItem : null;
            return loan == null ? members.ToList() :
                members.Where(p => GrowthRosterView.LoanLabel(p,GrowthDisplayClub(p)) == loan).ToList();
        }
        ulong GrowthDisplayClub(Player p)
        {
            GrowTarget target;
            if(showGrowthQueue && growWatch.TryGetValue(p.Person,out target))return target.ScopeClub==0?p.Club:target.ScopeClub;
            var scope=growSquadFilter.SelectedItem as GrowthSquadChoice;
            if(scope==null)return p.Club;
            if(scope.Kind==GrowthSquadKind.OutOnLoan)return scope.Parts!=null && p.Roster!=null?p.Roster.ContractClub:scope.Club;
            return p.Roster!=null && p.Roster.PlayingClub!=0?p.Roster.PlayingClub:p.Club;
        }
        ulong GrowthWatchScope(Player p)
        {
            var scope=growSquadFilter.SelectedItem as GrowthSquadChoice;
            if(scope==null || p.Roster==null || p.Roster.Contract==0)return p.Club;
            ulong owner=p.Roster.ContractClub;
            if(owner==0)return p.Club;
            if(scope.SourceClub==owner || scope.Parts!=null && scope.Parts.Any(part=>part.SourceClub==owner && GrowthRosterView.Contains(growthDirectory,part,p)))return owner;
            return p.Club;
        }
        DateTime? GrowthToday()
        {
            if(fm==null || !Safety.On("date"))return null;
            try{var date=fm.ReadGameDate();return date.HasValue?(DateTime?)Birthdays.Decode(date.Value):null;}catch{return null;}
        }
        void FillGrowthRosterInfo(DataGridViewRow row,Player p,DateTime? today)
        {
            int? age=GrowthSupportPlanner.Age(p.Born,today);row.Cells[2].Value=age.HasValue?(object)age.Value:"未知";
            if(row.Cells.Count<14)return;
            var membership=p.Roster;string status=GrowthRosterView.LoanLabel(p,GrowthDisplayClub(p));
            row.Cells[10].Value=growthDirectory.TeamName(p);row.Cells[11].Value=status;
            row.Cells[12].Value=membership!=null && membership.Loan==GrowthLoanState.Active?
                growthDirectory.ClubName(status=="租出"?membership.PlayingClub:membership.ContractClub):"";
            row.Cells[13].Value=membership!=null && membership.LoanEnd.HasValue?membership.LoanEnd.Value.ToString("yyyy-MM-dd"):"";
            string note=membership==null?"租借资料尚未读取":membership.Note+"\n合同所属："+growthDirectory.ClubName(membership.ContractClub)+"\n当前效力："+growthDirectory.ClubName(membership.PlayingClub);
            if(membership!=null && membership.LoanEnd.HasValue)note+="\n租借到期："+membership.LoanEnd.Value.ToString("yyyy-MM-dd");
            row.Cells[11].ToolTipText=row.Cells[12].ToolTipText=note;
            GrowthTeam team;if(membership!=null && growthDirectory.Teams.TryGetValue(membership.Team,out team))
                row.Cells[10].ToolTipText=team.Name+(team.Competition==null?"":"\n赛事："+team.Competition)+"\n按实际队伍与年龄上限显示，不按联赛固定生成。";
        }
    }
}
