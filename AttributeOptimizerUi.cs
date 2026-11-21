// Modified build v1.1.26. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
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
        readonly CheckBox raise=new CheckBox{Name="OptimizationUsePa",Dock=DockStyle.Fill};
        readonly Button switchMode=new Button{Name="OptimizationSwitchMode",Text="切换到重新分配",Dock=DockStyle.Fill};
        readonly CheckBox changesOnly=new CheckBox{Name="OptimizationChangesOnly",Text="只看改动",Checked=true,AutoSize=true};
        readonly DataGridView grid=new WorkspaceGrid{Name="OptimizationComparison",Dock=DockStyle.Fill,ReadOnly=true,
            AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,BackgroundColor=Color.White};
        readonly Label summary=new Label{Name="OptimizationSummary",Dock=DockStyle.Bottom,Height=85,Padding=new Padding(8),ForeColor=MainForm.UiMuted};
        readonly Button apply=new Button{Name="ApplyAttributeOptimization",Text="应用当前方案",Width=145};
        public AttributeOptimizationResult Plan {get;private set;}
        bool failed;
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
            Size=new Size(1080,760);MinimumSize=new Size(960,650);

            string potential=frame.RawPa>0?frame.RawPa.ToString():"待确认";
            var header=new Label{Dock=DockStyle.Top,Height=54,Padding=new Padding(4,0,4,4),Text=player.DisplayName+"\n当前 CA "+frame.Ca+"   ·   PA "+potential,ForeColor=MainForm.UiMuted,UseMnemonic=false};
            var split=new SplitContainer{Dock=DockStyle.Fill,Size=new Size(1040,600),FixedPanel=FixedPanel.Panel1,SplitterDistance=330,SplitterWidth=7,TabIndex=0};
            split.Panel1.Padding=new Padding(0,4,8,4);split.Panel1.AutoScroll=true;split.Panel1.TabIndex=0;
            split.Panel2.Padding=new Padding(4,4,0,4);split.Panel2.TabIndex=1;
            var settings=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=8};
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int height in new[]{24,160,38,36,40,72,42,42})settings.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            selector.TabIndex=0;mode.TabIndex=1;raise.TabIndex=2;switchMode.TabIndex=3;
            settings.Controls.Add(new Label{Text="有球与无球的职责",Dock=DockStyle.Fill,Font=new Font(Font,FontStyle.Bold)},0,0);settings.Controls.Add(selector,0,1);selector.Bind(roles,frame.Positions,true);
            mode.Items.AddRange(new object[]{"保留特点：不降低现有属性","重新分配：允许属性升降"});mode.SelectedIndex=(int)initial;settings.Controls.Add(mode,0,2);
            raise.Enabled=AttributeOptimizer.CanRaiseToPotential(frame);
            raise.Checked=raise.Enabled && raiseToPotential;
            raise.Text="同时升到 PA（"+potential+"）";
            settings.Controls.Add(raise,0,3);
            MainForm.StyleButton(switchMode);settings.Controls.Add(switchMode,0,4);
            settings.Controls.Add(new Label{Dock=DockStyle.Fill,Padding=new Padding(0,6,0,0),ForeColor=MainForm.UiMuted,Text="有球与无球角色共用所选预算。实际位置、熟练度和双脚计入估算，定位球保持原值。"},0,5);
            var restore=new Button{Name="RestoreFromEarlySave",Text="恢复默认／开档属性…",Dock=DockStyle.Fill,TabIndex=4};MainForm.StyleButton(restore);settings.Controls.Add(restore,0,6);
            var reload=new Button{Name="OptimizationReload",Text="重新读取球员资料",Dock=DockStyle.Fill,TabIndex=5};MainForm.StyleButton(reload);settings.Controls.Add(reload,0,7);split.Panel1.Controls.Add(settings);
            var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=35,TabIndex=0};toolbar.Controls.Add(changesOnly);toolbar.Controls.Add(new Label{Text="改动由预览决定，点击应用并确认后才写入",AutoSize=true,Padding=new Padding(12,0,0,0),ForeColor=MainForm.UiMuted});
            foreach(string title in new[]{"属性","当前","方案","变化","依据"})grid.Columns.Add(title,title);grid.Columns[0].FillWeight=105;grid.Columns[1].FillWeight=50;grid.Columns[2].FillWeight=50;grid.Columns[3].FillWeight=50;grid.Columns[4].FillWeight=125;grid.TabIndex=1;
            MainForm.StyleWorkspaceGrid(grid);
            split.Panel2.Controls.Add(grid);split.Panel2.Controls.Add(summary);split.Panel2.Controls.Add(toolbar);
            var footer=MainForm.DialogActions(this,MainForm.DialogDismiss("CloseAttributeOptimization","关闭"),apply);
            footer.TabIndex=1;
            Controls.Add(split);Controls.Add(header);Controls.Add(footer);
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
        void RefreshPlan()
        {
            Plan=null;apply.Enabled=false;switchMode.Visible=false;
            try
            {
                var plan=AttributeOptimizer.Create(frame,roles,(AttributeOptimizationMode)mode.SelectedIndex,raise.Checked);
                DrawComparison(plan);
                Plan=plan;apply.Enabled=!failed && plan.CanApply;
                bool canPreserveAtPa=!raise.Checked && raise.Enabled && plan.BeforeRca<=frame.RawPa;
                switchMode.Visible=plan.Mode==AttributeOptimizationMode.Preserve && plan.Conflict!=null && !canPreserveAtPa;
                summary.ForeColor=plan.Conflict==null?MainForm.UiMuted:MainForm.UiWarningInk;
                summary.Text="CA "+plan.SourceCa+" → "+plan.TargetCa+"     整套属性估算 "+plan.BeforeRca+" → "+plan.AfterRca+
                    "     "+(plan.Remaining<0?"超额 "+(-plan.Remaining):"剩余额度 "+plan.Remaining)+" 点\n"+
                    (plan.Conflict??("提高 "+plan.Increased+" 项，降低 "+plan.Decreased+" 项。计费位置 "+
                    Zh.Z(FmLayout.Positions[plan.AfterSlot])+"。"+(apply.Enabled?"":"当前无需调整。")))+
                    (Math.Max(plan.BeforeRca,plan.AfterRca)>200?"\n游戏推荐 CA 最高显示 200。此处列出未封顶的估算。":"\n可在游戏内对照同一球员的推荐 CA。");
            }
            catch(Exception ex){grid.Rows.Clear();summary.Text=ex.Message;}
        }
        void DrawComparison(AttributeOptimizationResult plan)
        {
            grid.Rows.Clear();
            if(plan==null)return;
            var inside=GrowthProfile.Resolve(frame.Positions,roles.Profile);
            var outside=roles.DefendingProfile==-2?null:GrowthProfile.Resolve(frame.Positions,roles.DefendingProfile);
            for(int i=0;i<54;i++)
            {
                int delta=plan.After[i]-plan.Before[i];
                if(changesOnly.Checked && delta==0)continue;
                bool ip=(roles.DefendingProfile==-2 || roles.PossessionWeight>0) && inside.Core.Contains(i);
                bool oop=outside!=null && roles.PossessionWeight<100 && outside.Core.Contains(i);
                string basis=GrowthProfile.IsSetPiece(i)?"定位球保留":!AttributeOptimizer.Editable(plan.Profile,i)?"保护项":
                    ip && oop?"双阶段共同关键":ip?"有球关键":oop?"无球关键":plan.Profile.Priority(i)>0?"角色重要项":"基础与其他";
                int row=grid.Rows.Add(Zh.Attr(FmLayout.Attrs[i]),(plan.Before[i]/5.0).ToString("0.0"),(plan.After[i]/5.0).ToString("0.0"),
                    (delta>0?"+":"")+(delta/5.0).ToString("0.0"),basis);
                grid.Rows[row].Cells[3].Style.ForeColor=delta<0?MainForm.UiErrorInk:delta>0?MainForm.UiSuccessInk:MainForm.UiMuted;
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
