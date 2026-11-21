// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        // Advance the watched players by elapsed game days. Focused growth and attribute
        // ceilings use guarded edits and record each transaction for undo.

        readonly TextBox txtGrowPlayerSearch=new TextBox{Name="GrowthPlayerSearch",Width=190};
        readonly CheckBox onlyGrowChecked=new CheckBox{Name="GrowthOnlyChecked",Text="仅看已勾选",AutoSize=true,Margin=new Padding(0,8,8,0)};
        readonly CheckBox growDetailed=new CheckBox{Name="GrowthDetailedColumns",Text="详细列",AutoSize=true};
        readonly Button btnGrowAll=new Button{Name="GrowthSelectVisible",Text="勾选当前筛选结果",AutoSize=true};
        readonly Button btnGrowNone=new Button{Name="GrowthUnselectVisible",Text="取消勾选当前筛选结果",AutoSize=true};
        readonly Button btnGrowEdit=new Button{Name="GrowthEditPlayer",Text="球员设置…",Width=100};
        readonly Button btnGrowOptimize=new Button{Name="GrowthOptimizeAttributes",Text="属性优化…",Width=105};
        SplitContainer growContent;
        bool growClubFilling,growLayoutBusy;
        readonly System.Windows.Forms.Timer growthBatchTimer=new System.Windows.Forms.Timer{Interval=15};
        GrowthBatch pendingGrowth;
        bool growthValuesDirty, growthRosterDirty;

        sealed class GrowthBatch
        {
            public readonly GrowTarget[] Targets;
            public readonly int Days;
            public readonly bool Fill;
            public readonly long Day;
            public int Next,Written,Reached,Stopped;
            public GrowthBatch(IEnumerable<GrowTarget> targets,int days,bool fill,long day)
            {
                Targets=targets.ToArray();
                Days=days;
                Fill=fill;
                Day=day;
            }
        }

        TabPage BuildGrowTab()
        {
            var page=new TabPage("CA 成长"){Name="GrowthPage"};
            var root=new Panel{Name="GrowthWorkspace",Dock=DockStyle.Fill};
            var title=WrappingBar("GrowthHeading");
            title.Controls.Add(new Label{Text="CA 成长",Font=new Font(Font.FontFamily,12,FontStyle.Bold),
                AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,0,12,0)});
            lblGrow.Dock=DockStyle.None;lblGrow.AutoSize=true;lblGrow.Anchor=AnchorStyles.Left;
            lblGrow.Padding=Padding.Empty;lblGrow.ForeColor=UiMuted;title.Controls.Add(lblGrow);
            cmbGrowSpeed.Name="GrowthSpeed";cmbGrowSpeed.Width=90;cmbGrowSpeed.Margin=new Padding(3,4,8,4);
            for(int g=2;g<=10;g++)cmbGrowSpeed.Items.Add(g+" 倍");cmbGrowSpeed.SelectedIndex=2;
            var fill=new Button{Name="GrowthFillToPa",Text="直接到 PA",Width=100};StyleButton(fill);StyleButton(btnGrowEdit);StyleButton(btnGrowToggle,WorkspaceButtonRole.Primary);btnGrowToggle.Width=144;btnGrowToggle.Name="GrowthStartStop";
            StyleButton(btnGrowOptimize);
            foreach(var button in new[]{btnGrowEdit,btnGrowOptimize,fill,btnGrowToggle})button.AutoSize=true;
            var singleActions=FieldGroup(FieldLabel("单人设置"),btnGrowEdit,btnGrowOptimize);
            singleActions.Name="GrowthSingleActions";
            growButtons=FieldGroup(FieldLabel("批量动作"),FieldLabel("速度"),cmbGrowSpeed,fill,btnGrowToggle);
            growButtons.Name="GrowthActions";
            var commands=WrappingBar("GrowthCommandBar");
            commands.Dock=DockStyle.Bottom;
            commands.Controls.Add(singleActions);
            commands.Controls.Add(growButtons);
            splitGrow=new SplitContainer{Dock=DockStyle.Fill,Size=new Size(1100,500),SplitterWidth=5,FixedPanel=FixedPanel.Panel1,SplitterDistance=194};
            lstGrowClubs.BorderStyle=BorderStyle.FixedSingle;lstGrowClubs.HorizontalScrollbar=true;splitGrow.Panel1.Padding=new Padding(0,0,8,0);
            splitGrow.Panel1.Controls.Add(lstGrowClubs);splitGrow.Panel1.Controls.Add(growthClubChecks);
            splitGrow.Panel1.Controls.Add(growthMultiClub);splitGrow.Panel1.Controls.Add(SearchWithListSpacing(txtGrowClubSearch));
            splitGrow.Panel1.Controls.Add(new Label{Text="选择俱乐部",Dock=DockStyle.Top,Height=26,ForeColor=UiMuted});
            var views=BuildGrowthViewBar();ConnectGrowthClubBrowser();
            growContent=new SplitContainer{Name="GrowthContent",Dock=DockStyle.Fill,Size=new Size(900,500),SplitterWidth=5,
                FixedPanel=FixedPanel.Panel2,Panel1MinSize=0,Panel2MinSize=0,Panel2Collapsed=true};
            var center=new Panel{Dock=DockStyle.Fill};
            growthScopeToolbar=BuildGrowthScopeBar();
            var toolbar=WrappingBar("GrowthRosterToolbar");
            txtGrowPlayerSearch.Font=Font;txtGrowPlayerSearch.Margin=new Padding(0,5,8,4);growDetailed.Margin=new Padding(0,8,2,0);
            StyleButton(btnGrowAll);StyleButton(btnGrowNone);StyleButton(btnRemoveGrowthPlayer);btnRemoveGrowthPlayer.Visible=false;
            toolbar.Controls.Add(txtGrowPlayerSearch);toolbar.Controls.Add(onlyGrowChecked);toolbar.Controls.Add(btnGrowAll);toolbar.Controls.Add(btnGrowNone);toolbar.Controls.Add(btnRemoveGrowthPlayer);toolbar.Controls.Add(growDetailed);
            gridGrow.Name="GrowthRoster";gridGrow.EditMode=DataGridViewEditMode.EditOnEnter;gridGrow.SelectionMode=DataGridViewSelectionMode.FullRowSelect;gridGrow.MultiSelect=false;
            gridGrow.Columns.Add(new DataGridViewCheckBoxColumn{HeaderText="培养",Name="watch",FillWeight=34,MinimumWidth=38,SortMode=DataGridViewColumnSortMode.NotSortable});
            gridGrow.Columns[0].HeaderText="选";gridGrow.Columns[0].ToolTipText="勾选此球员参与培养";
            foreach(var column in new[]{"球员","年龄","CA","PA 上限","距 PA","累计增加","进度","状态","培养方案"})gridGrow.Columns.Add(column,column);
            gridGrow.Columns[4].HeaderText="PA";gridGrow.Columns[4].ToolTipText="当前潜力上限";
            for(int i=1;i<gridGrow.Columns.Count;i++)gridGrow.Columns[i].ReadOnly=true;
            gridGrow.Columns[1].FillWeight=180;gridGrow.Columns[1].MinimumWidth=135;
            foreach(int i in new[]{3,4}){gridGrow.Columns[i].FillWeight=45;gridGrow.Columns[i].MinimumWidth=42;}
            foreach(var column in new[]{"所属分队","租借状态","来源／去向","租借到期"})gridGrow.Columns.Add(column,column);
            foreach(int i in new[]{5,6,7,8,12,13})gridGrow.Columns[i].Visible=false;
            gridGrow.Columns[2].FillWeight=42;gridGrow.Columns[2].MinimumWidth=52;
            gridGrow.Columns[10].HeaderText="分队";gridGrow.Columns[11].HeaderText="租借";
            gridGrow.Columns[10].FillWeight=100;gridGrow.Columns[10].MinimumWidth=96;gridGrow.Columns[11].FillWeight=60;gridGrow.Columns[11].MinimumWidth=58;
            foreach(int i in new[]{10,11,12,13})gridGrow.Columns[i].ReadOnly=true;
            gridGrow.Columns[7].FillWeight=80;gridGrow.Columns[8].FillWeight=110;gridGrow.Columns[9].FillWeight=105;
            var clubColumn=gridGrow.Columns[gridGrow.Columns.Add("GrowthPlayingClub","当前效力俱乐部")];
            clubColumn.ReadOnly=true;clubColumn.FillWeight=120;clubColumn.MinimumWidth=112;clubColumn.DisplayIndex=2;
            UpdateGrowthColumns();
            center.Controls.Add(gridGrow);
            center.Controls.Add(toolbar);
            center.Controls.Add(growthScopeToolbar);
            growContent.Panel1.Controls.Add(center);growEditor.Dock=DockStyle.Fill;growContent.Panel2.Controls.Add(growEditor);
            splitGrow.Panel2.Controls.Add(growContent);
            WrapDockedLabel(growthHint);
            root.Controls.Add(splitGrow);
            root.Controls.Add(growthHint);
            root.Controls.Add(commands);
            root.Controls.Add(views);
            root.Controls.Add(title);
            page.Controls.Add(root);
            ConnectGrowthEditor(growEditor);
            txtGrowClubSearch.TextChanged+=delegate{if(growthMultiClub.Checked)PopulateGrowthClubList();else FilterGrowClubs();};
            lstGrowClubs.SelectedIndexChanged+=delegate{if(!growClubFilling && !growthMultiClub.Checked){RebuildGrowthScopes();LoadGrow();}};
            txtGrowPlayerSearch.TextChanged+=delegate{if(!switchingGrowthView)LoadGrow();};
            onlyGrowChecked.CheckedChanged+=delegate{if(!showGrowthQueue)LoadGrow();};
            growDetailed.CheckedChanged+=delegate{UpdateGrowthColumns();};
            gridGrow.SelectionChanged+=delegate{if(!growFilling){BindGrowthEditor();RefreshGrowCount();}};
            gridGrow.CurrentCellDirtyStateChanged+=delegate{if(gridGrow.IsCurrentCellDirty)gridGrow.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            gridGrow.CellValueChanged+=(sender,e)=>{if(growFilling || e.RowIndex<0 || e.ColumnIndex!=0 || growOn)return;WatchRow(gridGrow.Rows[e.RowIndex]);};
            gridGrow.CellDoubleClick+=(sender,e)=>{if(e.RowIndex<0 || e.ColumnIndex==0)return;var player=gridGrow.Rows[e.RowIndex].Tag as Player;if(player==null)return;txtPlayerSearch.Text=player.Name;((TabControl)page.Parent).SelectedIndex=0;lstPlayers.SelectedItem=player;LoadPlayer(player);};
            btnGrowAll.Click+=delegate{if(growOn)return;foreach(DataGridViewRow row in gridGrow.Rows)WatchPlayer(row.Tag as Player);RefreshGrow();BindGrowthEditor();};
            btnGrowNone.Click+=delegate{if(growOn)return;foreach(DataGridViewRow row in gridGrow.Rows)RemoveGrowthPlayer(row.Tag as Player);LoadGrow();};
            btnRemoveGrowthPlayer.Click+=delegate{if(growOn || gridGrow.CurrentRow==null)return;RemoveGrowthPlayer(gridGrow.CurrentRow.Tag as Player);LoadGrow();};
            btnGrowEdit.Click+=delegate{OpenGrowthEditor();};fill.Click+=delegate{GrowFill();};btnGrowToggle.Click+=delegate{ToggleGrow();};
            btnGrowOptimize.Click+=delegate{var player=gridGrow.CurrentRow==null?null:gridGrow.CurrentRow.Tag as Player;if(player!=null){GrowTarget target;growWatch.TryGetValue(player.Person,out target);OpenAttributeOptimizer(player,target==null?OptionsFor(player):target.Options);}};
            growContent.SizeChanged+=delegate{LayoutGrowthWorkspace();};splitGrow.SizeChanged+=delegate{LayoutGrowthWorkspace();};
            growthBatchTimer.Tick+=delegate{RunGrowthSlice();};
            page.Enter+=delegate{LayoutGrowthWorkspace();BindGrowthEditor();};
            RefreshGrowCount();return page;
        }
        void LayoutGrowthWorkspace()
        {
            if(growContent==null || growLayoutBusy)return;growLayoutBusy=true;
            try
            {
                int width=growContent.ClientSize.Width;
                bool wasCollapsed=growContent.Panel2Collapsed;
                int rosterMinimum=LayoutPixels(560);
                int settingsWidth=Math.Max(LayoutPixels(350),growEditor.Font.Height*22);
                bool roomForSettings=width>=rosterMinimum+settingsWidth+growContent.SplitterWidth &&
                    growContent.ClientSize.Height>=LayoutPixels(340);
                if(!roomForSettings)
                {
                    growContent.Panel1MinSize=0;growContent.Panel2MinSize=0;
                    if(!wasCollapsed)growContent.Panel2Collapsed=true;
                }
                else
                {
                    if(wasCollapsed)growContent.Panel2Collapsed=false;
                    growContent.SplitterDistance=width-settingsWidth-growContent.SplitterWidth;
                }
                int rosterWidth=growContent.Panel1.ClientSize.Width;
                txtGrowPlayerSearch.Width=Math.Max(LayoutPixels(80),Math.Min(LayoutPixels(190),rosterWidth-LayoutPixels(12)));
                growSquadFilter.Width=Math.Max(LayoutPixels(92),Math.Min(LayoutPixels(240),rosterWidth-LayoutPixels(62)));
                if(wasCollapsed && !growContent.Panel2Collapsed)BindGrowthEditor();
            }
            finally{growLayoutBusy=false;}
        }

        void FilterGrowClubs()
        {
            PopulateGrowthClubList();
            RebuildGrowthScopes();UpdateGrowthColumns();LoadGrow();
        }

        void PopulateGrowthClubList()
        {
            var selected=lstGrowClubs.SelectedItem as Club;ulong previous=selected==null?0:selected.Addr;string query=txtGrowClubSearch.Text.Trim();
            var matches=clubs.Where(c=>ClubSearch.Matches(c,query)).ToArray();
            bool multi=growthMultiClub.Checked;
            ListBox list=multi?(ListBox)growthClubChecks:lstGrowClubs;
            growClubFilling=true;list.BeginUpdate();
            try
            {
                if(multi)
                {
                    growthClubChecks.Items.Clear();growthClubChecks.Items.AddRange(matches);
                    if(checkedGrowthClubs.Count>0)for(int i=0;i<matches.Length;i++)
                        if(checkedGrowthClubs.ContainsKey(GrowthClubKey(matches[i])))growthClubChecks.SetItemChecked(i,true);
                    return;
                }
                lstGrowClubs.Items.Clear();lstGrowClubs.Items.AddRange(matches);
                if(selected!=null)for(int i=0;i<matches.Length;i++)
                {
                    var club=(Club)lstGrowClubs.Items[i];
                    if(previous!=0?club.Addr==previous:club.Name==selected.Name){lstGrowClubs.SelectedIndex=i;break;}
                }
                if(lstGrowClubs.SelectedIndex<0 && lstGrowClubs.Items.Count==1)lstGrowClubs.SelectedIndex=0;
            }
            finally{list.EndUpdate();growClubFilling=false;}
        }

        // the date FM shows (today's date when the game date is not readable)
        DateTime GameDate()
        {
            uint v = fm != null && Safety.On("date") ? (fm.ReadGameDate() ?? 0) : 0;
            return (v >> 16) >= 1900 && (v & 0x1FF) >= 1 ? FromPacked(v) : DateTime.Today;
        }

        // the packed FM date without its time-of-day bits, so two reads on the same day compare equal
        static long DayKey(uint v) { return FromPacked(GrowthStep.DayOnly(v)).Ticks / TimeSpan.TicksPerDay; }

        void LoadGrow()
        {LoadGrowthRows(GrowthSelectedUid());}

        int GrowthSelectedUid()
        {
            var player=gridGrow.CurrentRow==null?null:gridGrow.CurrentRow.Tag as Player;
            return player==null?0:player.Uid;
        }

        void LoadGrowthRows(int selectedUid)
        {
            if(RosterPageHidden(growPage)){growthRosterDirty=true;return;}
            growthRosterDirty=false;
            growthValuesDirty=false;
            growFilling=true;
            try
            {
                gridGrow.Rows.Clear();
                if(fm!=null)
                {
                    DateTime? now=GrowthToday();string query=txtGrowPlayerSearch.Text.Trim();
                    IEnumerable<Player> source=showGrowthQueue?growWatch.Values.Select(t=>t.P):GrowthScopedPlayers();
                    foreach(var p in source.Where(p=>GrowthRowMatches(p,query)).OrderBy(p=>p.Name).ThenBy(p=>p.Uid))
                    {
                        GrowTarget target;growWatch.TryGetValue(p.Person,out target);
                        if(!showGrowthQueue && onlyGrowChecked.Checked && target==null)continue;
                        var ab=fm.Read(p.Tail-FmLayout.AbilityBeforePerson,4);int ca=ab==null?0:BitConverter.ToInt16(ab,0),pa=ab==null?0:BitConverter.ToInt16(ab,2);
                        int i=gridGrow.Rows.Add(target!=null,p.DisplayName,"",ca,PaTop(pa),Math.Max(0,PaTop(pa)-ca),target==null?(object)"":target.Added,target==null?"":GrowProgress(target),target==null?"":GrowStatus(target,growOn));
                        var row=gridGrow.Rows[i];row.Tag=p;row.Cells[1].ToolTipText=p.DisplayName+"\n双击查看球员资料";RefreshGrowthDirection(row,target);
                        FillGrowthRosterInfo(row,p,now);
                        row.Cells[14].Value=GrowthPlayingClubName(p);
                        if(pa<0)row.Cells[4].ToolTipText="随机区间上限，存储值 "+pa;
                    }
                    var same=gridGrow.Rows.Cast<DataGridViewRow>().FirstOrDefault(row=>((Player)row.Tag).Uid==selectedUid);
                    if(same!=null)gridGrow.CurrentCell=same.Cells[1];else if(gridGrow.Rows.Count>0)gridGrow.CurrentCell=gridGrow.Rows[0].Cells[1];
                }
            }
            finally{growFilling=false;}
            RefreshGrowCount();BindGrowthEditor();
        }

        static string GrowStatus(GrowTarget t, bool running)
        {
            if (t.Note.Length > 0) return t.Note;
            if (t.Done) return "已完成";
            return running ? "成长中" : "待开始";
        }

        // how far the natural curve has come, in in-game days
        static string GrowProgress(GrowTarget t)
        {
            if (t.Span <= 0) return "";
            return t.Elapsed >= t.Span ? "已到 PA" : t.Elapsed + "/" + t.Span + " 天";
        }

        void WatchRow(DataGridViewRow row)
        {
            var player=row.Tag as Player;if(player==null || growOn)return;
            if(Convert.ToBoolean(row.Cells[0].Value??false))WatchPlayer(player);
            else RemoveGrowthPlayer(player);
            RefreshGrow();BindGrowthEditor();
            if(showGrowthQueue || onlyGrowChecked.Checked)PostGrowthFilterReload();
        }
        void WatchPlayer(Player player)
        {
            if(player==null || fm==null || player.Uid<=0 || growWatch.ContainsKey(player.Person) || growWatch.Values.Any(t=>t.P.Uid==player.Uid))return;
            if(showGrowthQueue)return;
            if(GrowthPlayer(player.Uid)==null){lblStatus.Text="无法唯一确认该球员，请刷新球队名单。";return;}
            try{player=RefreshSelectedPlayer(player);}
            catch(Exception ex){lblStatus.Text="暂不能勾选该球员。"+ex.Message;return;}
            if(!GrowthRosterView.Contains(growthDirectory,growSquadFilter.SelectedItem as GrowthSquadChoice,player))
            {lblStatus.Text="球员已离开当前分队，请刷新本队名单。";return;}
            var target=new GrowTarget{P=player,Options=OptionsFor(player),ScopeClub=GrowthWatchScope(player)};ResetGrowthTarget(target);growWatch[player.Person]=target;
        }

        // the selected gear m: K = 40 / m in-game days per CA point (4x -> K=10). A player's time depends only on
        // his gap, not his age, with a 21-day floor and no upper cap
        int GrowGear() { return cmbGrowSpeed.SelectedIndex + 2; }
        int GrowSpanFor(int ca, int cap)
        {
            double k = 40.0 / GrowGear();
            return Math.Max(21, (int)Math.Round((cap - ca) * k));
        }

        // natural growth: quick while there is a long way to go, slowing down as CA nears the ceiling, and exactly
        // at the ceiling when the time is up. f(0)=0, f(1)=1, f'(t)=2(1-t)
        static double GrowCurve(double t) { return t <= 0 ? 0 : t >= 1 ? 1 : t * (2 - t); }

        void RefreshGrow()
        {
            if(RosterPageHidden(growPage))
            {
                growthValuesDirty=true;
                RefreshGrowCount();
                return;
            }
            growthValuesDirty=false;
            growFilling=true;
            try
            {
                var today=GrowthToday();
                if(fm!=null)foreach(DataGridViewRow row in gridGrow.Rows)
                {
                    var player=row.Tag as Player;
                    if(player==null)continue;
                    GrowTarget target;
                    growWatch.TryGetValue(player.Person,out target);
                    RefreshGrowthRow(row,player,target,today);
                }
            }
            finally{growFilling=false;}
            RefreshGrowCount();
        }
        void RefreshGrowthOptionRow(Player player,GrowTarget target)
        {
            if(RosterPageHidden(growPage)){growthValuesDirty=true;return;}
            growFilling=true;
            try
            {
                foreach(DataGridViewRow row in gridGrow.Rows)
                {
                    var shown=row.Tag as Player;
                    if(shown==null || shown.Uid!=player.Uid)continue;
                    if(target==null || fm==null)RefreshGrowthDirection(row,target);
                    else RefreshGrowthRow(row,player,target,GrowthToday());
                    break;
                }
            }
            finally { growFilling=false; }
        }
        void RefreshGrowthRow(DataGridViewRow row,Player player,GrowTarget target,DateTime? today)
        {
            var ab=fm.Read(player.Tail-FmLayout.AbilityBeforePerson,4);
            int ca=ab==null?0:BitConverter.ToInt16(ab,0);
            int cap=ab==null?0:PaTop(BitConverter.ToInt16(ab,2));
            SetGrowthCell(row,0,target!=null);
            SetGrowthCell(row,3,ca);
            SetGrowthCell(row,4,cap);
            SetGrowthCell(row,5,Math.Max(0,cap-ca));
            SetGrowthCell(row,6,target==null?(object)"":target.Added);
            SetGrowthCell(row,7,target==null?"":GrowProgress(target));
            SetGrowthCell(row,8,target==null?"":GrowStatus(target,growOn));
            RefreshGrowthDirection(row,target);
            var age=GrowthSupportPlanner.Age(player.Born,today);
            SetGrowthCell(row,2,age.HasValue?(object)age.Value:"未知");
        }
        static void SetGrowthCell(DataGridViewRow row,int column,object value)
        {
            var cell=row.Cells[column];
            if(!object.Equals(cell.Value,value))cell.Value=value;
        }
        void RefreshGrowCount()
        {
            lblGrow.Text=(growOn?"成长中":"已暂停")+" · 当前显示 "+gridGrow.Rows.Count+" 人";
            btnViewGrowthQueue.Text="已选培养名单（"+growWatch.Count+"）";
            if(showGrowthQueue)
                growthHint.Text=growWatch.Count==0?"培养名单为空。在“浏览筛选”中勾选球员。":
                    gridGrow.Rows.Count==0?"当前搜索没有匹配的已选球员。清空搜索可查看全部。":"开始成长将处理整份名单，每名球员使用自己的培养方案。";
            else growthHint.Text=growthMultiClub.Checked?"已选 "+checkedGrowthClubs.Count+" 家俱乐部。勾选球员即可加入培养名单。":"可切换俱乐部继续勾选球员，也可开启“多选俱乐部”合并浏览。";
            if(growPage!=null)growPage.Text=growWatch.Count==0?"CA 成长":"CA 成长（"+growWatch.Count+"）";
            btnGrowToggle.Text=(growOn?"停止成长":"开始成长")+"（"+growWatch.Count+" 人）";btnGrowToggle.Enabled=growOn || fm!=null && growWatch.Count>0 && Safety.On("ability");
            btnGrowAll.Enabled=btnGrowNone.Enabled=!growOn && fm!=null && gridGrow.Rows.Count>0;btnGrowEdit.Enabled=gridGrow.CurrentRow!=null && gridGrow.CurrentRow.Tag is Player;
            btnRemoveGrowthPlayer.Enabled=!growOn && btnGrowEdit.Enabled;
            btnGrowOptimize.Enabled=!growOn && fm!=null && btnGrowEdit.Enabled && Safety.On("core") && Safety.On("ability") && Safety.On("date");
        }
        bool ValidateGrowthQueue()
        {
            var invalid=growWatch.Values.Where(t=>t.Note.StartsWith("已停止：")).ToArray();
            if(invalid.Length==0)return true;
            const string message="下列已勾选球员尚不能开始，请先调整设置或取消勾选：";
            var reasons=invalid.Select(t=>t.P.DisplayName+"："+t.Note).ToArray();
            ShowOperationFeedback(FeedbackKind.Failed,message+Environment.NewLine+string.Join(Environment.NewLine,reasons));
            MessageBox.Show(message+"\n"+string.Join("\n",reasons.Take(8)),"培养设置需要处理");
            RefreshGrow();return false;
        }

        // A player's transaction is indivisible. Yield between players so stop and navigation remain responsive.
        void BeginGrowthBatch(int days,bool fill,long day)
        {
            pendingGrowth=new GrowthBatch(growWatch.Values,days,fill,day);
            growthBatchTimer.Start();
            RunGrowthSlice();
        }

        bool ApplyNextGrowth(GrowthBatch batch)
        {
            var t=batch.Targets[batch.Next++];
            GrowTarget current;
            if(t.Done || !growWatch.TryGetValue(t.P.Person,out current) || current!=t)return true;
            bool ok = true;
            foreach (var g in PlayerGuards(t.P)) { var cur = fm.Read(g.Key, g.Value.Length); if (cur == null || !cur.SequenceEqual(g.Value)) { ok = false; break; } }
            if (!ok) { t.Done = true; t.Note = "已停止：对象被移动"; batch.Stopped++; return true; }
            var ab = fm.Read(t.P.Tail - FmLayout.AbilityBeforePerson, 4);
            if (ab == null) { t.Done = true; t.Note = "已停止：读取失败"; batch.Stopped++; return true; }
            int ca = BitConverter.ToInt16(ab, 0), pa = BitConverter.ToInt16(ab, 2), cap = PaTop(pa);
            t.Cap = cap;
            if (ca < 1 || ca > 200 || cap < 1 || cap > 200) { t.Done = true; t.Note = "已停止：CA/PA 超出范围"; batch.Stopped++; return true; }
            if (ca >= cap) { t.Done = true; t.Note = "已到 PA"; batch.Reached++; return true; }
            int want;
            if (batch.Fill) want = cap;
            else
            {
                t.Elapsed += batch.Days;
                want = t.StartCa + (int)Math.Round((cap - t.StartCa) * GrowCurve((double)t.Elapsed / t.Span));
                if (t.Elapsed >= t.Span) want = cap;       // time is up: he is at his ceiling
                if (want < ca) want = ca;                // the game moved him further than the curve already
            }
            if (want > cap) want = cap;
            if (want <= ca) return true;
            int gain = want - ca;
            string focusError = null;
            bool applied = t.UseFocus ? ApplyFocusGrowth(t,ab,want,!batch.Fill,out focusError) :
                WriteLogged(t.P.Tail - FmLayout.AbilityBeforePerson, BitConverter.GetBytes((short)want),
                    t.P.Name + "：CA 成长 " + ca + " -> " + want + (batch.Fill ? " (to PA)" : " (+" + gain + ")"));
            if (applied)
            {
                t.Added += gain; batch.Written++;
                if (want >= cap) { t.Done = true; t.Note = "已到 PA"; batch.Reached++; }
            }
            else
            {
                t.Done = true; t.Note = "已停止：" + (focusError ?? "写入失败"); batch.Stopped++;
                LogLine(t.P.DisplayName + "：" + t.Note);
                if (focusError != null && focusError.Contains("重载")) { StopGrow("部分数据无法恢复，请重载修改前的存档"); return false; }
            }
            return true;
        }

        void FinishGrowthBatch(GrowthBatch batch)
        {
            RefreshGrow();
            var msg = "写入 " + batch.Written + " 条";
            if (batch.Reached > 0) msg += "，" + batch.Reached + " 人已到 PA";
            if (batch.Stopped > 0) msg += "，" + batch.Stopped + " 人停止";
            string result=msg+(growWatch.Values.Any(t=>t.UseFocus)?
                "。关键属性按估算预算同步成长，可重新载入球员资料查看。":"。属性由 FM 后续训练处理。");
            if (batch.Stopped>0) result+=Environment.NewLine+"停止原因见已选培养名单中的球员状态及变更记录。";
            if (growOn && growWatch.Count > 0 && growWatch.Values.All(t => t.Done))
            {
                StopGrow("培养队列已完成或停止，请查看各球员状态");
                result+=Environment.NewLine+lblStatus.Text;
            }
            ShowOperationFeedback(WriteFeedbackKind(batch.Written,batch.Stopped),result);
        }

        void RunGrowthSlice()
        {
            var batch=pendingGrowth;
            if(batch==null || !growOn || closingEditor)return;
            if(fm==null || !Safety.On("ability") ||
                scannedSaveLoads.HasValue && SaveLoadCount(fm)!=scannedSaveLoads)
            {
                StopGrow("连接、存档或游戏日期已变化，请重新核对培养名单");
                return;
            }
            try
            {
                if(batch.Day!=0)
                {
                    var date=Safety.On("date")?fm.ReadGameDate():null;
                    if(!date.HasValue || DayKey(date.Value)<batch.Day)
                    { StopGrow("游戏日期已变化或读取失败，请重新核对培养名单");return; }
                }
                var slice=Stopwatch.StartNew();
                int end=Math.Min(batch.Targets.Length,batch.Next+32);
                while(batch.Next<batch.Targets.Length)
                {
                    if(!ApplyNextGrowth(batch) || pendingGrowth!=batch)return;
                    if(batch.Next<batch.Targets.Length && (batch.Next>=end || slice.ElapsedMilliseconds>=12))return;
                }
            }
            catch(Exception ex){StopGrow("成长暂停。"+ex.Message);return;}
            pendingGrowth=null;
            growthBatchTimer.Stop();
            if(batch.Fill)StopGrow(null);
            FinishGrowthBatch(batch);
        }

        void GrowFill()
        {
            if (fm == null) { MessageBox.Show("请先点「挂接并扫描」。", "CA 成长"); return; }
            if (!Safety.On("ability"))
            {
                ShowOperationMessage(FeedbackKind.ReadOnly,"这个 fm.exe 的 CA/PA 是只读的" +
                    (Safety.Off.ContainsKey("ability") ? "（" + Safety.Off["ability"] + "）" : "") + "。详见「变更记录」页。","CA 成长");
                return;
            }
            foreach (var target in growWatch.Values) ResetGrowthTarget(target);
            if (!ValidateGrowthQueue()) return;
            int n = growWatch.Values.Count(t => !t.Done);
            if (n == 0)
            {
                ShowOperationMessage(FeedbackKind.Information,
                    "先勾选至少一名还没到 PA 的球员。「勾选当前筛选结果」只添加当前筛选后显示的球员。","CA 成长");
                return;
            }
            string mode = growWatch.Values.Any(t => t.UseFocus) ? "同时按培养方向和估算预算提升关键属性；FM 后续训练仍可能调整。" : "属性由 FM 后续训练处理。";
            if (MessageBox.Show("把 " + n + " 名球员的 CA 直接拉到 PA？" + mode + "修改记入变更记录并支持撤销。", "直接拉到 PA", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            long day=0;
            if(growWatch.Values.Any(t=>!t.Done && t.UseFocus))
            {
                var date=fm.ReadGameDate();
                try { day=DayKey(date??0); }
                catch(InvalidDataException){ShowOperationMessage(FeedbackKind.Failed,"游戏日期无法稳定读取，请稍后再试。","直接到 PA");return;}
            }
            growOn=true;
            SetGrowControlsLocked(true);
            RefreshGrowCount();
            BeginGrowthBatch(1,true,day);
        }

        void ToggleGrow()
        {
            if (growOn) { StopGrow("手动停止"); lblStatus.Text = "成长已停止，勾选名单保留。"; return; }
            if (fm == null) { MessageBox.Show("请先点「挂接并扫描」。", "CA 成长"); return; }
            if (!Safety.On("ability"))
            {
                ShowOperationMessage(FeedbackKind.ReadOnly,"这个 fm.exe 的 CA/PA 是只读的" +
                    (Safety.Off.ContainsKey("ability") ? "（" + Safety.Off["ability"] + "）" : "") + "。详见「变更记录」页。","CA 成长");
                return;
            }
            if (!Safety.On("date"))
            {
                ShowOperationMessage(FeedbackKind.ReadOnly,"成长需要读游戏日期，而这个 fm.exe 的日期没过安全检查，无法开始成长。","CA 成长");
                return;
            }
            // the clock starts now: each player's time depends on his own gap at the selected gear
            foreach (var t in growWatch.Values)
            {
                ResetGrowthTarget(t);
            }
            if (!ValidateGrowthQueue()) return;
            int n = growWatch.Values.Count(t => !t.Done);
            if (n == 0)
            {
                RefreshGrow();
                string reason = growWatch.Values.FirstOrDefault(t => t.Note.StartsWith("已停止：")) == null ? "先勾选至少一名未到 PA 的球员。" : growWatch.Values.First(t => t.Note.StartsWith("已停止：")).Note;
                ShowOperationMessage(FeedbackKind.Failed,reason,"无法开始成长"); return;
            }
            var startDate = fm.ReadGameDate();
            try { if (!startDate.HasValue) throw new InvalidOperationException(); GrowthStep.DayOnly(startDate.Value); }
            catch { ShowOperationMessage(FeedbackKind.Failed,"游戏日期无法稳定读取，请暂停后重试。","无法开始成长"); return; }
            growDay = DayKey(startDate.Value);
            growOn = true;
            RebuildGrowthLimitSchedule();
            SetGrowControlsLocked(true);
            string limits=growthLimitTargets.Length>0?"，"+growthLimitTargets.Length+" 人启用属性上限":"";
            LogLine("CA 成长已开启：" + n + " 名球员，" + (growWatch.Values.Any(t => t.UseFocus) ? "按各球员培养设置" : "仅提升 CA") + "，速度档位 " + GrowGear() + " 倍，各球员独立计算时长，上限为 PA"+limits);
            RefreshGrow();
            lblStatus.Text = "成长运行中：" + n + " 人，" + GrowGear() + " 倍加速"+limits+"。保持修改器开启。";
        }

        void StopGrow(string why)
        {
            pendingGrowth=null;
            growthBatchTimer.Stop();
            ClearGrowthLimitSchedule();
            if (!growOn) return;
            growOn = false;
            if (!string.IsNullOrEmpty(why))
            {
                lblStatus.Text="CA 成长已停止。"+why;
                LogLine(lblStatus.Text);
            }
            SetGrowControlsLocked(false);
            if(!string.IsNullOrEmpty(why) && !closingEditor)RefreshGrow();
            else RefreshGrowCount();
        }

        // while growth runs every control in the row is locked so the gear and the roster cannot be changed, but
        // the stop button stays usable (a disabled parent would disable it too, so the children are toggled one by one)
        void SetGrowControlsLocked(bool locked)
        {
            foreach (Control c in growButtons.Controls)
                if (c != btnGrowToggle) c.Enabled = !locked;
            btnGrowOptimize.Enabled=!locked;
            btnGrowAll.Enabled = btnGrowNone.Enabled = !locked && fm != null && gridGrow.Rows.Count > 0;
            BindGrowthEditor();
            btnGrowToggle.Enabled = true;
            gridGrow.Columns[0].ReadOnly = locked;
        }

        // pays the watched players once per in-game day (called from the 500 ms timer)
        void GrowTick()
        {
            if (fm == null || !btnScan.Enabled || closingEditor) return;
            var d=Safety.On("date")?fm.ReadGameDate():null;
            long day=0;
            if(d.HasValue)
                try {day=DayKey(d.Value); CheckAutoRefresh(d.Value);}
                catch(InvalidDataException){if(growOn)StopGrow("游戏日期无效");return;}
            if (!growOn || pendingGrowth!=null) return;
            if (!Safety.On("ability")) { StopGrow("CA/PA 变成只读了"); return; }
            if (!Safety.On("date")) { StopGrow("游戏日期读不到了"); return; }
            if (d == null) return;
            if (growDay == 0) { growDay = day; return; }
            if(day==growDay){CheckGrowthLimits();return;}
            if (day < growDay)
            {
                growDay = day;
                if (growWatch.Values.Any(t => t.UseFocus)) StopGrow("游戏日期回退，请重新扫描并开始培养");
                return;
            }
            int days = (int)Math.Min(365, day - growDay);   // a long skip (a holiday) counts up to a full season
            growDay = day;
            BeginGrowthBatch(days,false,day);
        }

        // one in-game month as a single number, so a new month is noticed even after a long holiday
        static long MonthKey(uint v)
        {
            var d = FromPacked(v);
            return (long)d.Year * 12 + d.Month;
        }

        // the rosters are only known from the last scan, so a player signed after it would never show up: read them
        // again whenever the game moves into a new month (or reaches a later one after a long skip)
        void CheckAutoRefresh(uint packed)
        {
            if (autoScanning || rosterWorker.Busy || !btnScan.Enabled || closingEditor) return;
            long mk = MonthKey(packed);
            if (autoMonthKey == 0) { autoMonthKey = mk; return; }
            if (mk == autoMonthKey) return;
            if (mk < autoMonthKey) { autoMonthKey = mk; return; }   // a save was loaded: re-anchor instead of scanning
            autoMonthKey = mk;
            AutoRefreshScan();
        }

        // a second scan over the same process: the handle and the addresses stay valid, so freezes and the running
        // growth keep going, and the watched players are moved onto their new Player objects by UID
        async void AutoRefreshScan()
        {
            autoScanning = true;
            UpdateRosterRefreshButtons();
            try
            {
                var result=await rosterWorker.Run(token=>
                {
                    var scan=ScanAll(f=>token.ThrowIfCancellationRequested(),token);
                    scan.PrepareDisplayLists();
                    return scan;
                });
                if(closingEditor || IsDisposed)return;
                if(result.Players.Count<100)throw new InvalidOperationException("可确认的球员数量过少");
                if(scannedSaveLoads.HasValue && (result.SaveLoads!=scannedSaveLoads || SaveLoadCount(fm)!=scannedSaveLoads))
                {
                    StopGrow("载入的存档已变化，请重新挂接");
                    throw new InvalidOperationException("载入的存档已变化，请重新挂接");
                }
                ApplyAutomaticScanResult(result);
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                if(closingEditor || IsDisposed)return;
                lblStatus.Text="自动刷新名单未完成。"+ex.Message;
                LogLine(lblStatus.Text);
            }
            finally
            {
                autoScanning=false;
                FinishRosterRead();
            }
        }

        internal void ApplyAutomaticScanResult(ScanResult r)
        {
            r.PrepareDisplayLists();
            growthDirectory=r.Growth;
            scannedSaveLoads=r.SaveLoads;
            genderFilterVerified=r.GenderVerified;
            retirementManager = r.RetirementManager;
            players = r.Players; rosterIndex=r.RosterIndex;
            clubs = r.Clubs;RebindGrowthClubs();
            staff = r.Staff;
            foreach (var kv in r.Nations) if (!nations.ContainsKey(kv.Key)) nations[kv.Key] = kv.Value;
            CarryGrowth();
            FilterPlayers(); FilterClubs(); FilterSquadClubs(); FilterStaff();
            FilterGrowClubs();
            // the refresh is silent: no status bar announcement, it is treated as background behaviour
        }

        // the watch holds Player objects from the previous scan: rebind them by UID so a refresh does not lose progress
        void CarryGrowth(string source="自动刷新名单",ClubRosterUpdate update=null)
        {
            int previous=growWatch.Count;
            var targets=growWatch.Values.ToList();
            var keep=update==null?GrowthRoster.Rebind(targets,RosterIndex.Unique):
                targets.Where(t=>!update.Removed.Contains(t.P.Uid) && !update.Refreshed.ContainsKey(t.P.Uid))
                    .Concat(GrowthRoster.Rebind(targets.Where(t=>update.Refreshed.ContainsKey(t.P.Uid)),update.Refreshed.Values)).ToList();
            growWatch.Clear();
            foreach(var target in keep)growWatch[target.P.Person]=target;
            if(growOn)RebuildGrowthLimitSchedule();
            if(update==null || update.Club!=0 || previous!=keep.Count)
                LogLine(source+"：保留 "+keep.Count+" 名手动勾选球员"+(previous>keep.Count?"，移出 "+(previous-keep.Count)+" 名离队或无法确认的球员":"")+"。");
            if(growOn && growWatch.Count==0)StopGrow("已勾选球员均已离队或无法确认");
        }

    }
}
