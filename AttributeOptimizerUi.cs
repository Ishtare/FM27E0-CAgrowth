// Modified build v1.1.26. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class AttributeOptimizationDialog : Form
    {
        readonly GrowthStep frame;
        readonly GrowthOptions roles;
        readonly DualRoleSelector selector=new DualRoleSelector("Optimizer");
        readonly ComboBox mode=new ComboBox{Name="OptimizationMode",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        readonly CheckBox raise=new CheckBox{Name="OptimizationUsePa",AutoSize=true,Anchor=AnchorStyles.Left};
        readonly Button switchMode=new Button{Name="OptimizationSwitchMode",Text="切换到重新分配",AutoSize=true,Anchor=AnchorStyles.Left};
        readonly CheckBox changesOnly=new CheckBox{Name="OptimizationChangesOnly",Text="只看改动",Checked=true,AutoSize=true};
        readonly DataGridView grid=new WorkspaceGrid{Name="OptimizationComparison",Dock=DockStyle.Fill,ReadOnly=true,
            AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,BackgroundColor=Color.White};
        readonly Label summary=new Label{Name="OptimizationSummary",AutoSize=true,Padding=new Padding(4,2,4,4),ForeColor=MainForm.UiMuted};
        readonly TextBox unmet=ReadOnlyNote("OptimizationUnmetHighlights");
        readonly TextBox details=ReadOnlyNote("OptimizationTierDetails");
        readonly Button apply=new Button{Name="ApplyAttributeOptimization",Text="应用当前方案",Width=145};
        public AttributeOptimizationResult Plan {get;private set;}
        bool failed,canSwitchMode;
        Action layoutSettings;
        public GrowthOptions RoleOptions {get{return roles.Clone();}}
        public AttributeOptimizationMode SelectedMode {get{return (AttributeOptimizationMode)mode.SelectedIndex;}}
        public bool RaiseToPotential {get{return raise.Checked;}}
        public event Func<bool> RestoreRequested;
        public event Func<AttributeOptimizationResult,bool> ApplyRequested;
        public AttributeOptimizationDialog(Player player,GrowthStep frame,GrowthOptions options,
            AttributeOptimizationMode initial=AttributeOptimizationMode.Preserve,bool raiseToPotential=false)
        {
            this.frame=frame;roles=options.Clone();Name="AttributeOptimizationDialog";
            MainForm.StyleWorkspaceDialog(this,"一键属性优化");
            Size=new Size(1080,760);MinimumSize=new Size(920,610);

            string potential=frame.RawPa>0?frame.RawPa.ToString():"待确认";
            var header=new Label{Name="OptimizationPlayer",AutoSize=true,Padding=new Padding(4,0,4,4),Text=player.DisplayName+"\n当前 CA "+frame.Ca+"   ·   PA "+potential,ForeColor=MainForm.UiMuted,UseMnemonic=false};
            var body=new TableLayoutPanel{Name="OptimizationBody",Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,TabIndex=0};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var split=new SplitContainer{Name="OptimizationSplit",Dock=DockStyle.Fill,Size=new Size(1040,600),FixedPanel=FixedPanel.Panel1,SplitterDistance=340,SplitterWidth=7,TabIndex=0};
            split.Panel1.Padding=new Padding(0,4,8,4);split.Panel1.TabIndex=0;
            split.Panel2.Padding=new Padding(4,4,0,4);split.Panel2.TabIndex=1;
            var settingsScroll=new Panel{Name="OptimizationSettingsScroll",Dock=DockStyle.Fill,AutoScroll=true,TabIndex=0};
            var settings=new Panel{Name="OptimizationSettings",Margin=Padding.Empty,Padding=new Padding(0,0,0,4)};
            selector.Dock=mode.Dock=DockStyle.None;
            selector.TabIndex=0;mode.TabIndex=1;raise.TabIndex=2;switchMode.TabIndex=3;
            selector.Bind(roles,frame.Positions,true);
            mode.Items.AddRange(new object[]{"保留特点：不降低现有属性","重新分配：允许属性升降"});mode.SelectedIndex=(int)initial;
            raise.Enabled=AttributeOptimizer.CanRaiseToPotential(frame);
            raise.Checked=raise.Enabled && raiseToPotential;
            raise.Text="同时升到 PA（"+potential+"）";
            MainForm.StyleButton(switchMode);
            var hint=new Label{Name="OptimizationSettingsHint",AutoSize=true,Padding=new Padding(0,6,0,4),ForeColor=MainForm.UiMuted,Text="按培养预设分层。实际位置、熟练度和双脚计入估算，定位球保持原值。"};
            var restore=new Button{Name="RestoreFromEarlySave",Text="恢复默认／开档属性…",AutoSize=true,Anchor=AnchorStyles.Left,TabIndex=4};MainForm.StyleButton(restore);
            var reload=new Button{Name="OptimizationReload",Text="重新读取球员资料",AutoSize=true,Anchor=AnchorStyles.Left,TabIndex=5};MainForm.StyleButton(reload);
            var fields=new Control[]{new Label{Text="有球与无球角色",AutoSize=true},selector,mode,raise,switchMode,hint,restore,reload};
            foreach(var field in fields)field.Anchor=AnchorStyles.Top|AnchorStyles.Left;
            settings.Controls.AddRange(fields);
            settingsScroll.Controls.Add(settings);
            split.Panel1.Controls.Add(settingsScroll);

            // A native combo can grow after its handle is created; use its final bounds for the next row.
            bool fittingSettings=false;
            layoutSettings=delegate
            {
                if(fittingSettings)return;
                int width=settingsScroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth;
                if(width<=0)return;
                fittingSettings=true;
                settings.SuspendLayout();
                try
                {
                    int top=settings.Padding.Top;
                    foreach(var field in fields)
                    {
                        if(field==switchMode && !canSwitchMode)continue;
                        var margin=field.Margin;
                        int available=Math.Max(1,width-settings.Padding.Horizontal-margin.Horizontal);
                        if(field==hint)hint.MaximumSize=new Size(available,0);
                        var preferred=field.GetPreferredSize(new Size(available,0));
                        bool fullWidth=field==selector || field==mode;
                        int height=field==selector?selector.Height:preferred.Height;
                        field.SetBounds(settings.Padding.Left+margin.Left,top+margin.Top,
                            fullWidth?available:Math.Min(available,preferred.Width),height);
                        top=field.Bottom+margin.Bottom;
                    }
                    settings.Size=new Size(width,top+settings.Padding.Bottom);
                }
                finally
                {
                    settings.ResumeLayout(false);
                    fittingSettings=false;
                }
            };
            EventHandler fitSettings=delegate{layoutSettings();};
            foreach(var field in fields)
            {
                field.FontChanged+=fitSettings;
                field.SizeChanged+=fitSettings;
                field.MarginChanged+=fitSettings;
                field.HandleCreated+=fitSettings;
            }
            settingsScroll.ClientSizeChanged+=fitSettings;
            layoutSettings();

            var results=new TableLayoutPanel{Name="OptimizationResults",Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            results.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var unmetRow=new RowStyle(SizeType.Absolute);
            results.RowStyles.Add(unmetRow);
            results.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            EventHandler fitUnmet=delegate{unmetRow.Height=unmet.Font.Height*3+unmet.Margin.Vertical+8;};
            unmet.FontChanged+=fitUnmet;
            fitUnmet(unmet,EventArgs.Empty);
            var resultTabs=new TabControl{Name="OptimizationResultTabs",Dock=DockStyle.Fill,TabIndex=1};
            var comparison=new TabPage("属性对比"){BackColor=Color.White,Padding=new Padding(4)};
            var breakdown=new TabPage("分层与保护"){BackColor=Color.White,Padding=new Padding(4)};
            var toolbar=new FlowLayoutPanel{Name="OptimizationComparisonFilter",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,TabIndex=0};
            toolbar.Controls.Add(changesOnly);
            foreach(string title in new[]{"属性","当前","方案","变化","依据"})grid.Columns.Add(title,title);grid.Columns[0].FillWeight=105;grid.Columns[1].FillWeight=50;grid.Columns[2].FillWeight=50;grid.Columns[3].FillWeight=50;grid.Columns[4].FillWeight=125;grid.TabIndex=1;
            MainForm.StyleWorkspaceGrid(grid);
            grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
            comparison.Controls.Add(grid);comparison.Controls.Add(toolbar);
            breakdown.Controls.Add(details);
            resultTabs.TabPages.Add(comparison);resultTabs.TabPages.Add(breakdown);
            results.Controls.Add(summary,0,0);results.Controls.Add(unmet,0,1);results.Controls.Add(resultTabs,0,2);
            split.Panel2.Controls.Add(results);
            body.Controls.Add(header,0,0);body.Controls.Add(split,0,1);
            WrapLabel(body,header);
            WrapLabel(results,summary);
            var footer=MainForm.DialogActions(this,MainForm.DialogDismiss("CloseAttributeOptimization","关闭"),apply);
            footer.TabIndex=1;
            footer.MinimumSize=new Size(0,44);
            footer.AutoSize=true;
            footer.AutoSizeMode=AutoSizeMode.GrowAndShrink;
            Controls.Add(body);Controls.Add(footer);
            selector.Changed+=(inside,outside,weight)=>{roles.Profile=inside;roles.DefendingProfile=outside;roles.PossessionWeight=weight;RefreshPlan();};
            mode.SelectedIndexChanged+=delegate{RefreshPlan();};
            changesOnly.CheckedChanged+=delegate{DrawComparison(Plan);};
            raise.CheckedChanged+=delegate{RefreshPlan();};
            switchMode.Click+=delegate{mode.SelectedIndex=(int)AttributeOptimizationMode.Redistribute;};
            restore.Click+=delegate{if(RestoreRequested!=null && RestoreRequested()){DialogResult=DialogResult.OK;Close();}};
            reload.Click+=delegate{DialogResult=DialogResult.Retry;Close();};apply.Click+=delegate
            {
                if(Plan==null || ApplyRequested==null)return;
                try{if(ApplyRequested(Plan)){DialogResult=DialogResult.OK;Close();}}
                catch(Exception ex){failed=true;summary.Text=ex.Message+"\n请重新读取后再生成方案。";apply.Enabled=false;}
            };
            RefreshPlan();
        }

        static TextBox ReadOnlyNote(string name)
        {
            return new TextBox{Name=name,Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,
                ScrollBars=ScrollBars.Vertical,BackColor=Color.White,ForeColor=MainForm.UiInk};
        }

        static void WrapLabel(Control parent,Label label)
        {
            EventHandler fit=delegate
            {
                int width=parent.ClientSize.Width-parent.Padding.Horizontal-label.Margin.Horizontal;
                if(width>0 && label.MaximumSize.Width!=width)label.MaximumSize=new Size(width,0);
            };
            parent.SizeChanged+=fit;
            fit(parent,EventArgs.Empty);
        }

        void RefreshPlan()
        {
            Plan=null;apply.Enabled=false;canSwitchMode=false;
            try
            {
                var plan=AttributeOptimizer.Create(frame,roles,(AttributeOptimizationMode)mode.SelectedIndex,raise.Checked);
                DrawComparison(plan);
                DrawTiers(plan);
                Plan=plan;apply.Enabled=!failed && plan.CanApply;
                bool canPreserveAtPa=!raise.Checked && raise.Enabled && plan.BeforeRca<=frame.RawPa;
                canSwitchMode=plan.Mode==AttributeOptimizationMode.Preserve && plan.Conflict!=null && !canPreserveAtPa;
                summary.ForeColor=plan.Conflict==null?MainForm.UiMuted:MainForm.UiWarningInk;
                summary.Text="CA "+plan.SourceCa+" → "+plan.TargetCa+"     整套属性估算 "+plan.BeforeRca+" → "+plan.AfterRca+
                    "     "+(plan.Remaining<0?"超额 "+(-plan.Remaining):"剩余额度 "+plan.Remaining)+" 点\n"+
                    (plan.Conflict??("提高 "+plan.Increased+" 项，降低 "+plan.Decreased+" 项。计费位置 "+
                    Zh.Z(FmLayout.Positions[plan.AfterSlot])+"。"+(apply.Enabled?"":"当前无需调整。")))+
                    (Math.Max(plan.BeforeRca,plan.AfterRca)>200?"\n游戏推荐 CA 最高显示 200。此处列出未封顶的估算。":"\n可在游戏内对照同一球员的推荐 CA。");
            }
            catch(Exception ex){grid.Rows.Clear();unmet.Clear();details.Clear();summary.Text=ex.Message;}
            switchMode.Visible=canSwitchMode;
            layoutSettings();
        }

        void DrawTiers(AttributeOptimizationResult plan)
        {
            var lines=new List<string>();
            var gaps=new List<string>();
            for(var tier=AttributeTier.Core;tier<=AttributeTier.Other;tier++)
            {
                var members=plan.Tiers.Members(tier);
                int target=RoleAttributeTiers.Target(tier);
                if(members.Length==0)
                {
                    lines.Add(RoleAttributeTiers.Label(tier)+"  无可调计费项");
                }
                else
                {
                    var values=members.Select(i=>plan.After[i]).ToArray();
                    lines.Add(RoleAttributeTiers.Label(tier)+"  达标 "+values.Count(v=>v>=target)+"/"+values.Length+
                        "  目标 "+Display(target)+"  预计 "+Display(values.Min())+"–"+Display(values.Max()));
                }
                if(tier>AttributeTier.Supporting)continue;
                var profileIndices=tier==AttributeTier.Core?plan.Profile.Core:plan.Profile.Supporting;
                var missed=profileIndices.Where(i=>plan.After[i]<target).Select(i=>AttributeName(i)+" "+Display(plan.After[i])+
                    (plan.Tiers.TierOf(i)==AttributeTier.Protected?"（保留）":"")).ToArray();
                if(missed.Length>0)gaps.Add(RoleAttributeTiers.Label(tier)+"未达标（"+Display(target)+"）  "+string.Join("、",missed));
            }
            unmet.Text=gaps.Count==0?"蓝档和绿档均达到目标。":string.Join(Environment.NewLine,gaps);
            unmet.SelectionStart=0;unmet.ScrollToCaret();
            lines.Add("");
            lines.Add("被压低项 "+plan.Decreased+" 项");
            lines.AddRange(Enumerable.Range(0,54).Where(i=>plan.After[i]<plan.Before[i])
                .Select(i=>AttributeName(i)+"  "+Display(plan.Before[i])+" → "+Display(plan.After[i])));
            lines.Add("");
            lines.Add("保留项");
            lines.Add(string.Join("、",Enumerable.Range(0,54).Where(i=>plan.Tiers.TierOf(i)==AttributeTier.Protected).Select(AttributeName)));
            details.Text=string.Join(Environment.NewLine,lines);
            details.SelectionStart=0;details.ScrollToCaret();
        }

        static string AttributeName(int index) { return Zh.Attr(FmLayout.Attrs[index]); }
        static string Display(int raw) { return (raw/5.0).ToString("0.0"); }

        void DrawComparison(AttributeOptimizationResult plan)
        {
            grid.Rows.Clear();
            if(plan==null)return;
            for(int i=0;i<54;i++)
            {
                int delta=plan.After[i]-plan.Before[i];
                if(changesOnly.Checked && delta==0)continue;
                var tier=plan.Tiers.TierOf(i);
                string basis=GrowthProfile.IsSetPiece(i)?"定位球保留":RoleAttributeTiers.Label(tier);
                int row=grid.Rows.Add(AttributeName(i),Display(plan.Before[i]),Display(plan.After[i]),
                    (delta>0?"+":"")+Display(delta),basis);
                grid.Rows[row].Cells[3].Style.ForeColor=delta<0?MainForm.UiErrorInk:delta>0?MainForm.UiSuccessInk:MainForm.UiMuted;
                if(tier==AttributeTier.Core || tier==AttributeTier.Supporting)
                    grid.Rows[row].Cells[4].Style.ForeColor=tier==AttributeTier.Core?Color.FromArgb(25,73,145):MainForm.UiSuccessInk;
            }
        }
    }
    partial class MainForm
    {
        readonly Button btnOptimizeAttributes=new Button{Name="PlayerOptimizeAttributes",Text="一键属性优化…",AutoSize=true,Enabled=false};
        void OpenAttributeOptimizer(Player player,GrowthOptions selectedRoles=null)
        {
            if(player==null || fm==null){MessageBox.Show("请先连接并选择球员。","一键属性优化");return;}
            if(!Safety.On("core") || !Safety.On("ability") || !Safety.On("date")){MessageBox.Show("当前属性、CA/PA 或日期尚未通过检查。","一键属性优化");return;}
            StopGrow("打开一键属性优化，先停止自动成长");
            var options=(selectedRoles??OptionsFor(player)).Clone();
            var mode=AttributeOptimizationMode.Preserve;
            bool raiseToPotential=false;
            try
            {
                while(true)
                {
                    player=RefreshSelectedPlayer(player);
                    var frame=GrowthStep.Capture(fm.Read,fm.ReadGameDate,player,PlayerGuards(player),true);
                    var currentPlayer=player;
                    using(var dialog=new AttributeOptimizationDialog(player,frame,options,mode,raiseToPotential))
                    {
                        dialog.ApplyRequested+=plan=>SaveAttributeOptimization(currentPlayer,frame,plan);
                        dialog.RestoreRequested+=()=>OpenEarlySaveRestore(dialog,currentPlayer,frame);
                        var result=dialog.ShowDialog(this);
                        options=dialog.RoleOptions;mode=dialog.SelectedMode;raiseToPotential=dialog.RaiseToPotential;
                        if(result==DialogResult.Retry)continue;
                        if(result==DialogResult.OK){if(loadedPlayer!=null && loadedPlayer.Uid==player.Uid)LoadPlayer(player);RefreshGrow();}
                        break;
                    }
                }
            }
            catch(Exception ex){MessageBox.Show(ex.Message,"属性优化未完成");}
        }
    }
}
