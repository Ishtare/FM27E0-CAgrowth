// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        readonly Button btnBrowseGrowthClubs=new Button{Name="GrowthBrowseClubs",Text="按俱乐部选人",Width=130};
        readonly Button btnViewGrowthQueue=new Button{Name="GrowthViewQueue",Text="已选培养名单（0）",AutoSize=true,MinimumSize=new Size(160,32)};
        readonly Button btnRemoveGrowthPlayer=new Button{Name="GrowthRemovePlayer",Text="移出此球员",Width=105};
        readonly CheckBox growthMultiClub=new CheckBox{Name="GrowthMultiClub",Text="多选俱乐部",Dock=DockStyle.Top,Height=28};
        readonly CheckedListBox growthClubChecks=new CheckedListBox{Name="GrowthClubChecks",Dock=DockStyle.Fill,
            IntegralHeight=false,CheckOnClick=true,Visible=false,HorizontalScrollbar=true,BorderStyle=BorderStyle.FixedSingle};
        readonly Dictionary<long,Club> checkedGrowthClubs=new Dictionary<long,Club>();
        readonly Label growthHint=new Label{Dock=DockStyle.Bottom,TextAlign=ContentAlignment.MiddleLeft,ForeColor=UiMuted,Padding=new Padding(0,4,0,4)};
        Control growthScopeToolbar;
        bool showGrowthQueue,switchingGrowthView,growthFilterReloadPosted;
        string browsePlayerQuery="",combinedGrowthScopeKey="";
        int combinedGrowthLoan;

        static long GrowthClubKey(Club club)
        {
            return club.Uid>0?(long)club.Uid:-(long)club.Addr;
        }

        List<Club> GrowthBrowseClubs()
        {
            if(growthMultiClub.Checked)return checkedGrowthClubs.Values.OrderBy(c=>c.Name).ThenBy(c=>c.Uid).ToList();
            var current=lstGrowClubs.SelectedItem as Club;
            return current==null?new List<Club>():new List<Club>{current};
        }

        void RebindGrowthClubs()
        {
            if(checkedGrowthClubs.Count==0)return;
            var matches=clubs.Where(c=>checkedGrowthClubs.ContainsKey(GrowthClubKey(c))).GroupBy(GrowthClubKey)
                .Where(g=>g.Select(c=>c.Addr).Distinct().Count()==1).Select(g=>g.First()).ToList();
            checkedGrowthClubs.Clear();
            foreach(var club in matches)checkedGrowthClubs.Add(GrowthClubKey(club),club);
        }

        Control BuildGrowthViewBar()
        {
            var bar=WrappingBar("GrowthViews");
            StyleButton(btnBrowseGrowthClubs,true);StyleButton(btnViewGrowthQueue);
            btnBrowseGrowthClubs.Click+=delegate{SwitchGrowthView(false);};
            btnViewGrowthQueue.Click+=delegate{SwitchGrowthView(true);};
            bar.Controls.Add(btnBrowseGrowthClubs);bar.Controls.Add(btnViewGrowthQueue);
            return bar;
        }

        void ConnectGrowthClubBrowser()
        {
            growthMultiClub.CheckedChanged+=delegate
            {
                bool multiple=growthMultiClub.Checked;
                var current=lstGrowClubs.SelectedItem as Club;
                if(multiple && current!=null)checkedGrowthClubs[GrowthClubKey(current)]=current;
                lstGrowClubs.Visible=!multiple;
                growthClubChecks.Visible=multiple;
                FilterGrowClubs();
            };
            growthClubChecks.ItemCheck+=(sender,args)=>
            {
                if(growClubFilling)return;
                var club=(Club)growthClubChecks.Items[args.Index];
                if(args.NewValue==CheckState.Checked)checkedGrowthClubs[GrowthClubKey(club)]=club;
                else checkedGrowthClubs.Remove(GrowthClubKey(club));
                RebuildGrowthScopes();LoadGrow();
            };
        }

        void SwitchGrowthView(bool queue)
        {
            if(showGrowthQueue==queue)return;
            switchingGrowthView=true;
            try
            {
                if(queue)browsePlayerQuery=txtGrowPlayerSearch.Text;
                showGrowthQueue=queue;
                splitGrow.Panel1Collapsed=queue;
                growthScopeToolbar.Visible=!queue;
                btnGrowAll.Visible=btnGrowNone.Visible=onlyGrowChecked.Visible=!queue;
                btnRemoveGrowthPlayer.Visible=queue;
                txtGrowPlayerSearch.Text=queue?"":browsePlayerQuery;
                SetHint(txtGrowPlayerSearch,queue?"搜索已选球员或俱乐部…":"搜索球员姓名或 UID…");
                StyleButton(btnBrowseGrowthClubs,!queue);StyleButton(btnViewGrowthQueue,queue);
                UpdateGrowthColumns();
            }
            finally{switchingGrowthView=false;}
            LoadGrow();LayoutGrowthWorkspace();
        }

        void UpdateGrowthColumns()
        {
            if(gridGrow.Columns.Count<15)return;
            bool detail=growDetailed.Checked;
            gridGrow.Columns[14].Visible=showGrowthQueue || growthMultiClub.Checked || detail;
            foreach(int i in new[]{5,6,12,13})gridGrow.Columns[i].Visible=detail;
            foreach(int i in new[]{7,8})gridGrow.Columns[i].Visible=showGrowthQueue || detail;
            foreach(int i in new[]{10,11})gridGrow.Columns[i].Visible=!showGrowthQueue || detail;
        }

        string GrowthPlayingClubName(Player player)
        {
            ulong address=player.Roster!=null && player.Roster.PlayingClub!=0?player.Roster.PlayingClub:player.Club;
            return address==0?"效力球队待确认":growthDirectory.ClubName(address);
        }

        bool GrowthRowMatches(Player player,string query)
        {
            return query.Length==0 || player.Matches(query) ||
                showGrowthQueue && ClubSearch.MatchesName(GrowthPlayingClubName(player),null,query);
        }

        void RemoveGrowthPlayer(Player player)
        {
            if(player==null || growOn)return;
            GrowTarget target;
            if(!growWatch.TryGetValue(player.Person,out target) || target.P.Uid!=player.Uid)
                target=growWatch.Values.FirstOrDefault(t=>t.P.Uid==player.Uid);
            if(target!=null)growWatch.Remove(target.P.Person);
        }

        // A checkbox commits inside a DataGridView event. Replace its rows after that event finishes.
        void PostGrowthFilterReload()
        {
            if(growthFilterReloadPosted || !IsHandleCreated || closingEditor)return;
            growthFilterReloadPosted=true;
            BeginInvoke((Action)(()=>
            {
                growthFilterReloadPosted=false;
                if(!closingEditor && (showGrowthQueue || onlyGrowChecked.Checked))LoadGrow();
            }));
        }

        void RefreshGrowthClubMenu()
        {
            var selected=GrowthBrowseClubs();
            var scope=growSquadFilter.SelectedItem as GrowthSquadChoice;
            var source=scope==null?null:selected.FirstOrDefault(c=>c.Addr==scope.SourceClub);
            if(source!=null || selected.Count==1){RefreshLocalClub(source??selected[0]);return;}
            if(selected.Count==0)return;
            var menu=new ContextMenuStrip();
            foreach(var club in selected)
            {
                var item=menu.Items.Add(club.Name);
                var chosen=club;
                item.Click+=delegate{RefreshLocalClub(chosen);};
            }
            menu.Closed+=delegate{menu.Dispose();};
            menu.Show(btnRefreshGrowth,0,btnRefreshGrowth.Height);
        }
    }
}
