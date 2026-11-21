// Modified build v1.1.18, 2026-11-17. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class GrowthSupportDialog : Form
    {
        readonly GrowthSupportOptions draft;
        readonly GrowthProfile profile;
        readonly byte[] values;
        readonly int ca;
        readonly int? age;
        readonly ICollection<int> scope;
        readonly DataGridView grid=new DataGridView{Name="SupportAttributeTargets",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
        readonly Label status=new Label{Name="SupportStatus",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(101,115,135)};
        readonly CheckBox enabled=new CheckBox{Name="SupportEnabled",Text="启用低值属性补强",AutoSize=true};
        readonly NumericUpDown threshold=new NumericUpDown{Name="SupportThreshold",Minimum=1,Maximum=5,Width=58};
        readonly NumericUpDown budget=new NumericUpDown{Name="SupportBudgetPercent",Minimum=5,Maximum=40,Width=58};
        public GrowthSupportOptions ResultOptions {get;private set;}
        public GrowthSupportDialog(GrowthSupportOptions options,GrowthProfile profile,byte[] values,int ca,int? age,
            ICollection<int> scope=null)
        {
            draft=(options??new GrowthSupportOptions()).Clone();draft.Validate();this.profile=profile;this.values=(byte[])values.Clone();this.ca=ca;this.age=age;
            this.scope=scope==null?null:new HashSet<int>(scope);
            Text="低值属性补强设置";Name="GrowthSupportDialog";Size=new Size(920,620);MinimumSize=new Size(760,520);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9.25F);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(14)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text="只对明显低值提出建议，不认定原数据错误。可取消某项或修改目标下限（1–12）。\n额外补强与主要培养共用预算，达到下限后停止；预算不足时不能保证全部补齐。\n调整阈值后请点“重新自动识别”；这会重置本窗口中的属性选择和目标。",ForeColor=Color.FromArgb(101,115,135)},0,0);
            var settings=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};enabled.Checked=draft.Enabled;threshold.Value=draft.Threshold;budget.Value=draft.BudgetPercent;
            settings.Controls.Add(enabled);settings.Controls.Add(new Label{Text="识别低值 ≤",AutoSize=true,Padding=new Padding(10,5,0,0)});settings.Controls.Add(threshold);
            settings.Controls.Add(new Label{Text="补强预算最多 %",AutoSize=true,Padding=new Padding(10,5,0,0)});settings.Controls.Add(budget);
            var recognize=new Button{Name="SupportRecognize",Text="重新自动识别",Width=130};MainForm.StyleWorkspaceButton(recognize);settings.Controls.Add(recognize);root.Controls.Add(settings,0,1);
            grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="include",HeaderText="补强",FillWeight=35});
            foreach(string name in new[]{"属性","当前","目标下限","原因"})grid.Columns.Add(name,name);
            grid.Columns[1].ReadOnly=grid.Columns[2].ReadOnly=grid.Columns[4].ReadOnly=true;grid.Columns[1].FillWeight=85;grid.Columns[2].FillWeight=45;grid.Columns[3].FillWeight=65;grid.Columns[4].FillWeight=250;
            grid.RowTemplate.Height=30;grid.ColumnHeadersHeight=34;grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            MainForm.StyleWorkspaceGrid(grid,Font);
            grid.CellValidating+=(sender,e)=>{if(e.RowIndex<0 || e.ColumnIndex!=3)return;int number;if(!int.TryParse(Convert.ToString(e.FormattedValue),out number) || number<1 || number>12){e.Cancel=true;status.Text="目标下限请输入 1–12 的整数。";}};
            root.Controls.Add(grid,0,2);root.Controls.Add(status,0,3);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            var cancel=new Button{Text="取消",Width=100,DialogResult=DialogResult.Cancel,CausesValidation=false};var save=new Button{Name="SaveSupportOptions",Text="保存补强设置",Width=140};MainForm.StyleWorkspaceButton(save,true);MainForm.StyleWorkspaceButton(cancel);actions.Controls.Add(cancel);actions.Controls.Add(save);root.Controls.Add(actions,0,4);Controls.Add(root);CancelButton=cancel;
            recognize.Click+=delegate
            {
                draft.Threshold=(int)threshold.Value;
                foreach(int index in draft.Targets.Keys.Where(i=>this.scope==null || this.scope.Contains(i)).ToArray())
                    draft.Targets.Remove(index);
                draft.Excluded.RemoveWhere(i=>this.scope==null || this.scope.Contains(i));
                Populate();
            };
            save.Click+=delegate
            {
                if(!grid.EndEdit())return;draft.Enabled=enabled.Checked;draft.Threshold=(int)threshold.Value;draft.BudgetPercent=(int)budget.Value;
                foreach(DataGridViewRow row in grid.Rows)
                {
                    int goal;if(!int.TryParse(Convert.ToString(row.Cells[3].Value),out goal) || goal<1 || goal>12){status.Text="请检查目标下限：1–12。";return;}
                    int index=(int)row.Tag;draft.Targets[index]=goal;
                    if(Convert.ToBoolean(row.Cells[0].Value??false))draft.Excluded.Remove(index);else draft.Excluded.Add(index);
                }
                draft.Validate();ResultOptions=draft.Clone();DialogResult=DialogResult.OK;Close();
            };
            Populate();
        }
        void Populate()
        {
            grid.Rows.Clear();var suggestions=GrowthSupportPlanner.Suggest(profile,values,ca,age,draft,scope);
            foreach(var item in suggestions){int row=grid.Rows.Add(item.Included,Zh.Attr(FmLayout.Attrs[item.Index]),(item.Current/5.0).ToString("0.0"),item.Target,item.Reason);grid.Rows[row].Tag=item.Index;}
            status.Text=suggestions.Count==0?"当前没有符合阈值和角色条件的低值项。不会因为低于平均值就自动补强所有属性。":"共 "+suggestions.Count+" 项建议；可逐项取消。强壮等基础身体项及当前角色关键项参与识别；风格、隐藏属性和惯用脚不参与。";
            if(scope!=null)status.Text+="\n启用未勾选属性上限时，只列出已勾选的属性。";
        }
    }
}
