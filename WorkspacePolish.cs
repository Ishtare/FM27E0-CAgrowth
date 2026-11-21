// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class WorkspaceGrid : DataGridView
    {
        static readonly Color FrameColor=Color.FromArgb(194,204,217);

        public WorkspaceGrid()
        {
            BorderStyle=BorderStyle.FixedSingle;
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if(BorderStyle==BorderStyle.FixedSingle)
                ControlPaint.DrawBorder(e.Graphics,ClientRectangle,SystemInformation.HighContrast?SystemColors.WindowFrame:FrameColor,
                    ButtonBorderStyle.Solid);
        }
    }

    partial class MainForm
    {
        readonly ComboBox playerAttributeCategory=new ComboBox{Name="PlayerAttributeCategory",DropDownStyle=ComboBoxStyle.DropDownList,Width=144};
        readonly TextBox staffAttributeSearch=new TextBox{Name="StaffAttributeSearch",Width=190};
        internal static void StyleWorkspaceGrid(DataGridView grid,Font font)
        {
            grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersHeight=36;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.FixedSingle;grid.GridColor=UiLine;
            grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(239,243,249);grid.ColumnHeadersDefaultCellStyle.ForeColor=UiMuted;
            grid.ColumnHeadersDefaultCellStyle.Font=new Font(font,FontStyle.Bold);grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;grid.RowTemplate.Height=30;
            grid.DefaultCellStyle.Padding=new Padding(4,2,4,2);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(224,235,254);grid.DefaultCellStyle.SelectionForeColor=UiInk;
        }
        void PolishWorkspace(Control root)
        {
            foreach(Control control in root.Controls)
            {
                var grid=control as DataGridView;
                if(grid!=null)
                {
                    StyleWorkspaceGrid(grid,Font);
                }
                var button=control as Button;
                if(button!=null && button.FlatStyle!=FlatStyle.Flat)StyleButton(button,button.Text.StartsWith("应用") || button.Text.StartsWith("保存"));
                var list=control as ListBox;
                if(list!=null){list.BackColor=Color.White;list.ForeColor=UiInk;list.IntegralHeight=false;list.HorizontalScrollbar=true;}
                var tabs=control as TabControl;
                if(tabs!=null && !(tabs is ContentTabs)){tabs.Padding=new Point(12,6);foreach(TabPage page in tabs.TabPages){page.BackColor=Color.White;}}
                PolishWorkspace(control);
            }
        }
        Control BuildPlayerAttributeToolbar()
        {
            var bar=WrappingBar("PlayerAttributeToolbar");
            playerAttributeCategory.Items.AddRange(new object[]{"全部属性","技术","精神","身体与惯用脚","门将","隐藏与性格"});playerAttributeCategory.SelectedIndex=0;
            bar.Controls.Add(FieldGroup(FieldLabel("属性类别"),playerAttributeCategory));
            playerAttributeCategory.SelectedIndexChanged+=delegate{ApplyPlayerAttributeFilter();};return bar;
        }
        static int PlayerAttributeGroup(int index)
        {
            if(index>=54 || new[]{41,44,47,48,49}.Contains(index))return 5;
            if(new[]{11,12,13,14,15,16,19,21,31,32,33}.Contains(index))return 4;
            if(new[]{24,25,34,36,37,38,39,42,46,50}.Contains(index))return 3;
            if(new[]{17,18,20,26,28,29,40,43,45,51,52,53}.Contains(index))return 2;
            return 1;
        }
        void ApplyPlayerAttributeFilter()
        {
            if(gridAttrs.Rows.Count==0)return;gridAttrs.EndEdit();gridAttrs.CurrentCell=null;int selected=playerAttributeCategory.SelectedIndex;
            for(int i=0;i<gridAttrs.Rows.Count;i++)gridAttrs.Rows[i].Visible=selected<=0 || PlayerAttributeGroup(i)==selected;
        }
        Control BuildStaffAttributeToolbar()
        {
            var bar=WrappingBar("StaffAttributeToolbar");
            bar.Controls.Add(FieldGroup(FieldLabel("筛选属性名称"),staffAttributeSearch));
            staffAttributeSearch.TextChanged+=delegate{ApplyStaffAttributeFilter();};return bar;
        }
        void ApplyStaffAttributeFilter()
        {
            if(gridStaff.Rows.Count==0)return;gridStaff.EndEdit();gridStaff.CurrentCell=null;string query=staffAttributeSearch.Text.Trim();
            foreach(DataGridViewRow row in gridStaff.Rows)row.Visible=query.Length==0 || Convert.ToString(row.Cells[0].Value).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;
        }
        static TableLayoutPanel ManagementForm(string name)
        {
            var table=new TableLayoutPanel{Name=name,Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                ColumnCount=2,Padding=new Padding(12),Margin=Padding.Empty};
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
            return table;
        }
        static void AddManagementNote(TableLayoutPanel table,int row,Label note)
        {
            note.Dock=DockStyle.Fill;
            table.Controls.Add(note,0,row);
            table.SetColumnSpan(note,2);
        }
        static void MountManagementForm(TabPage page,TableLayoutPanel table)
        {
            foreach(Control input in table.Controls)
            {
                if(input is TextBox || input is NumericUpDown)input.Anchor=AnchorStyles.Left|AnchorStyles.Right;
                else if(input is Label)input.Dock=DockStyle.Fill;
            }
            page.Controls.Add(table);
        }
        readonly Button btnApplyClub=new Button{Name="ApplyClubChanges",Text="应用俱乐部修改",Width=145,Enabled=false};
        readonly Button btnReloadClub=new Button{Name="ReloadClub",Text="重新加载",Width=96,Enabled=false};
        readonly Button btnFreezeClub=new Button{Name="FreezeClubFunds",Text="冻结资金",Width=100,Enabled=false};
        readonly Button btnClubSponsors=new Button{Name="EditClubSponsors",Text="赞助合同…",Width=110,Enabled=false};

        TabPage BuildClubManagementTab()
        {
            var page=new TabPage("俱乐部管理");var split=splitClubs=new SplitContainer{Dock=DockStyle.Fill};
            split.Panel1.Controls.Add(lstClubs);split.Panel1.Controls.Add(SearchWithListSpacing(txtClubSearch));split.Panel1.Controls.Add(new Label{Text="搜索俱乐部",Dock=DockStyle.Top,Height=26,ForeColor=UiMuted});
            var sections=new TabControl{Name="ClubManagementSections",Dock=DockStyle.Fill};
            var money=new TabPage("资金与预算"){AutoScroll=true};var facilities=new TabPage("设施与球场"){AutoScroll=true};var identity=new TabPage("资料与票务"){AutoScroll=true};sections.TabPages.AddRange(new[]{money,facilities,identity});
            var finance=ManagementForm("ClubFinanceForm");AddCurRow(finance,0,"俱乐部结余（{C}）",txtBalance);AddCurRow(finance,1,"剩余转会预算（{C}）",txtTransfer);AddCurRow(finance,2,"下赛季转会预算（{C}）",txtTransferNext);AddCurRow(finance,3,"周薪预算（{C}）",txtWage);
            AddRow(finance,4,"年薪预算参考",lblWageYear);AddRow(finance,5,"当前周薪总额",lblWageSpend);
            AddManagementNote(finance,6,lblStoredPounds);
            AddManagementNote(finance,7,new Label{Text="金额按顶部显示货币换算。换算率可在“显示货币”中调整。",AutoSize=true,ForeColor=UiMuted,Margin=new Padding(0,14,0,0)});
            MountManagementForm(money,finance);
            foreach(var box in new[]{txtBalance,txtTransfer,txtTransferNext,txtWage,txtTicketPrice})box.TextAlign=HorizontalAlignment.Right;
            var infrastructure=ManagementForm("ClubFacilitiesForm");for(int i=0;i<numFacility.Length;i++)AddRow(infrastructure,i,FacilityLayout.Titles[i]+"（1–20）",BuildFacilityEditor(i));
            AddRow(infrastructure,5,"俱乐部声望",numReputation);AddRow(infrastructure,6,"球场容量",numCapacity);AddRow(infrastructure,7,"已用座位",numUsedCap);
            AddManagementNote(infrastructure,8,new Label{Name="FacilityScopeNote",Text="设施按当前俱乐部逐项核对。缺失、越界或归属不明的项目单独只读，不会自动改成 1。",AutoSize=true,ForeColor=UiMuted,Margin=new Padding(0,12,0,0)});
            MountManagementForm(facilities,infrastructure);
            var details=ManagementForm("ClubTicketForm");AddRow(details,0,"俱乐部全名",txtClubName);AddRow(details,1,"俱乐部简称",txtClubShort);AddRow(details,2,"季票持有者",numSeasonTickets);AddCurRow(details,3,"平均单场票价（{C}）",txtTicketPrice);
            AddManagementNote(details,4,new Label{Text="票价可从“未设置”状态录入。名称长度、票价及人数按当前资料检查。",AutoSize=true,ForeColor=UiMuted,Margin=new Padding(0,14,0,0)});
            MountManagementForm(identity,details);
            lblClub.Dock=DockStyle.Top;lblClub.Padding=new Padding(4,10,4,4);lblClub.ForeColor=UiInk;
            WrapDockedLabel(lblClub);
            if(string.IsNullOrEmpty(lblClub.Text))lblClub.Text="选择俱乐部后查看和编辑资料";
            var actions=new FlowLayoutPanel{Name="ClubActions",Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new Padding(0,8,0,0)};
            foreach(var button in new[]{btnApplyClub,btnReloadClub,btnFreezeClub,btnClubSponsors}){StyleButton(button,button==btnApplyClub);actions.Controls.Add(button);}
            btnApplyClub.Click+=delegate{ApplyClub();};btnReloadClub.Click+=delegate{LoadClub(loadedClub);};
            btnFreezeClub.Click+=delegate{if(loadedClub!=null)lblStatus.Text=FreezeMoney(loadedClub)+" 项资金已冻结，详情见冻结管理。";};
            btnClubSponsors.Click+=delegate{SponsorsDialog();};
            split.Panel2.Controls.Add(sections);split.Panel2.Controls.Add(actions);split.Panel2.Controls.Add(lblClub);page.Controls.Add(split);
            txtClubSearch.TextChanged+=delegate{FilterClubs();};lstClubs.SelectedIndexChanged+=delegate{LoadClub(lstClubs.SelectedItem as Club);};return page;
        }
        TabPage BuildReadableLogTab()
        {
            var page=new TabPage("变更记录");
            var heading=new Label{Text="变更记录\n选中记录查看详情。成长与属性方案按整组撤销；被游戏更新的数据会拒绝覆盖。",Dock=DockStyle.Top,Height=60,Padding=new Padding(4,8,4,4),ForeColor=UiMuted};
            var detail=new TextBox{Name="ChangeDetails",Dock=DockStyle.Bottom,Height=110,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Color.FromArgb(247,249,252),Font=new Font(FontFamily.GenericMonospace,9)};
            lstLog.Font=Font;lstLog.FormattingEnabled=true;lstLog.Format+=(sender,e)=>{var change=e.ListItem as Change;if(change!=null)e.Value=(change.Undone?"[已撤销] ":"")+change.When.ToString("HH:mm:ss")+"  "+change.What;};
            lstLog.SelectedIndexChanged+=delegate{detail.Text=string.Join(Environment.NewLine,lstLog.SelectedItems.Cast<object>().Select(item=>item.ToString()).ToArray());};
            var actions=new FlowLayoutPanel{Name="ChangeActions",Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new Padding(0,8,0,0)};
            var selected=new Button{Text="撤销所选",Width=105};var all=new Button{Text="撤销本次全部",Width=125};var copy=new Button{Text="复制选中详情",Width=125};
            foreach(var button in new[]{selected,all,copy}){StyleButton(button,button==selected);actions.Controls.Add(button);}
            selected.Click+=delegate{UndoSelected();};all.Click+=delegate{UndoAll();};copy.Click+=delegate{if(detail.Text.Length>0)try{Clipboard.SetText(detail.Text);}catch(Exception ex){MessageBox.Show(ex.Message,"复制未完成");}};
            page.Controls.Add(lstLog);page.Controls.Add(detail);page.Controls.Add(actions);page.Controls.Add(heading);return page;
        }
        TabPage BuildReadableFreezeTab()
        {
            var page=new TabPage("冻结管理");lblFreeze.Height=66;lblFreeze.Padding=new Padding(4,8,4,4);lblFreeze.ForeColor=UiMuted;
            var actions=new FlowLayoutPanel{Name="FreezeActions",Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,Padding=new Padding(0,8,0,0)};
            var selected=new Button{Text="停止所选冻结",Width=130};var all=new Button{Text="停止全部冻结",Width=130};StyleButton(selected,true);StyleButton(all);actions.Controls.Add(selected);actions.Controls.Add(all);
            selected.Click+=delegate{foreach(var item in lstFreeze.SelectedItems.Cast<Freeze>().ToList())StopFreeze(item,"已停止");RefreshFreezeList();};all.Click+=delegate{StopAllFreezes("已停止");};
            page.Controls.Add(lstFreeze);page.Controls.Add(actions);page.Controls.Add(lblFreeze);RefreshFreezeList();return page;
        }
        void BuildSquadActions(FlowLayoutPanel bar,Button heal,Button maximize,Button restore,Button freeze)
        {
            bar.Controls.Clear();bar.Padding=new Padding(0,8,0,0);heal.Text="治疗全队伤病";restore.Text="恢复全队状态";
            StyleButton(restore,true);StyleButton(heal);bar.Controls.Add(restore);bar.Controls.Add(heal);
            StyleButton(btnRefreshSquad);bar.Controls.Add(btnRefreshSquad);
            var more=new Button{Name="SquadMoreActions",Text="更多批量操作…",Width=145};StyleButton(more);bar.Controls.Add(more);
            var menu=new ContextMenuStrip();menu.Items.Add(MenuGroup("属性与冻结",maximize,freeze));menu.Opening+=delegate{RefreshCommandMenu(menu.Items);};more.Click+=delegate{menu.Show(more,new Point(0,more.Height));};more.ContextMenuStrip=menu;
        }
        void OrganizeFinderToolbar(FlowLayoutPanel bar,Button search)
        {
            bar.BackColor=Color.FromArgb(247,249,252);bar.Padding=new Padding(10,4,10,4);
            StyleButton(search,true);search.Text="搜索名单";search.AutoSize=true;search.Width=110;
        }
    }
}
