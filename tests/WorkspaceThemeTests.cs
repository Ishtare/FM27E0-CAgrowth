using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class WorkspaceThemeTests
    {
        static T Named<T>(Control parent,string name) where T:Control
        {
            return (T)parent.Controls.Find(name,true).Single();
        }

        static double Linear(int channel)
        {
            double srgb=channel/255.0;
            return srgb<=0.04045?srgb/12.92:Math.Pow((srgb+0.055)/1.055,2.4);
        }

        static double Luminance(Color color)
        {
            return 0.2126*Linear(color.R)+0.7152*Linear(color.G)+0.0722*Linear(color.B);
        }

        static void CheckContrast(Action<bool,string> check,Color ink,Color paper,string context)
        {
            double a=Luminance(ink),b=Luminance(paper);
            double ratio=(Math.Max(a,b)+0.05)/(Math.Min(a,b)+0.05);
            check(ratio>=4.5,context+" text contrast is "+ratio.ToString("0.00")+":1, minimum 4.5:1");
        }

        static void CheckGrid(Action<bool,string> check,DataGridView grid,string context)
        {
            var header=grid.ColumnHeadersDefaultCellStyle;
            var body=grid.DefaultCellStyle;
            CheckContrast(check,header.ForeColor,header.BackColor,context+" header");
            CheckContrast(check,header.SelectionForeColor,header.SelectionBackColor,context+" selected header");
            CheckContrast(check,body.ForeColor,body.BackColor,context+" cell");
            CheckContrast(check,body.ForeColor,grid.AlternatingRowsDefaultCellStyle.BackColor,context+" alternating row");
            CheckContrast(check,body.SelectionForeColor,body.SelectionBackColor,context+" selected cell");
            check(!grid.EnableHeadersVisualStyles && header.Font.Bold && grid.BorderStyle==BorderStyle.FixedSingle &&
                grid.AutoSizeColumnsMode==DataGridViewAutoSizeColumnsMode.Fill,context+" keeps the shared bold header, border and fill");
        }

        static void CheckButtonRoles(Action<bool,string> check,MainForm form)
        {
            var primary=Named<Button>(form,"AttachAndScan");
            var secondary=Named<Button>(form,"ScanDetailsButton");
            var undo=Named<Button>(form,"UndoSelectedChanges");
            var undoAll=Named<Button>(form,"UndoAllChanges");
            CheckContrast(check,primary.ForeColor,primary.BackColor,"primary button");
            CheckContrast(check,secondary.ForeColor,secondary.BackColor,"secondary button");
            CheckContrast(check,undo.ForeColor,undo.BackColor,"undo button");
            check(undo.BackColor==undoAll.BackColor && undo.BackColor!=primary.BackColor && undo.BackColor!=secondary.BackColor,
                "both history undo actions have the same distinct style");

            var originalPrimary=primary.Text;
            var originalSecondary=secondary.Text;
            var originalUndo=undo.Text;
            try
            {
                primary.Text="重新读取";
                secondary.Text="应用预览";
                undo.Text="还原所选记录";
                primary.Enabled=false;
                undo.Enabled=false;
                check(primary.BackColor==Color.FromArgb(235,239,244) && undo.BackColor==primary.BackColor &&
                    primary.ForeColor==Color.FromArgb(111,121,135) && undo.ForeColor==primary.ForeColor,
                    "primary and undo use the grey disabled palette regardless of captions");
                primary.Enabled=true;
                undo.Enabled=true;
                check(primary.BackColor==Color.FromArgb(37,99,210) && primary.ForeColor==Color.White &&
                    secondary.BackColor==Color.White && undo.ForeColor==undoAll.ForeColor,
                    "reenabling actions restores their explicit role independently of captions");
            }
            finally
            {
                primary.Text=originalPrimary;secondary.Text=originalSecondary;undo.Text=originalUndo;
            }
        }

        static void CheckDialog(Action<bool,string> check,Form dialog,Button action,Control input,Font workspaceFont,bool enterPreviews=false)
        {
            var handle=dialog.Handle;
            dialog.PerformLayout();
            var footer=Named<FlowLayoutPanel>(dialog,"DialogActions");
            var dismiss=(Button)dialog.CancelButton;
            check(dialog.Font.Name==workspaceFont.Name && dialog.StartPosition==FormStartPosition.CenterParent &&
                dialog.AutoScaleMode==AutoScaleMode.Dpi,dialog.Name+" shares the font, centering and DPI settings");
            check(dialog.Padding.Left>0 && dialog.Padding.Left==dialog.Padding.Right &&
                footer.Left==dialog.Padding.Left && footer.Right==dialog.ClientSize.Width-dialog.Padding.Right &&
                footer.Bottom==dialog.ClientSize.Height-dialog.Padding.Bottom,dialog.Name+" keeps its footer inside consistent margins");
            check(action.Parent==footer && dismiss.Parent==footer &&
                footer.ClientRectangle.Contains(action.Bounds) && footer.ClientRectangle.Contains(dismiss.Bounds) &&
                action.Right<dismiss.Left && Math.Abs(action.Top-dismiss.Top)<=1,
                dialog.Name+" keeps the action and dismissal right aligned, separated, and entirely visible");
            check(action.TabIndex<dismiss.TabIndex && action.DialogResult==DialogResult.None &&
                dismiss.DialogResult==DialogResult.Cancel && !dismiss.CausesValidation,dialog.Name+" ends Tab order with a safe Escape action");
            dialog.ActiveControl=input;
            check(enterPreviews?dialog.AcceptButton==action:dialog.AcceptButton==null,
                dialog.Name+" only permits a non-writing preview as the default Enter action");
        }

        public static void Run(Action<bool,string> check,MainForm form)
        {
            const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
            foreach(string key in new[]{"gridAttrs","gridPos","gridStaff","gridGrow","gridFind","gridStaffFind","gridPrize"})
                CheckGrid(check,(DataGridView)typeof(MainForm).GetField(key,fields).GetValue(form),key);

            var status=Named<Control>(form,"ScanStatusBar");
            var heading=Named<Label>(status,"OperationFeedbackTitle");
            var feedback=Named<TextBox>(status,"OperationFeedbackText");
            CheckContrast(check,feedback.ForeColor,feedback.BackColor,"feedback message");
            var setTitle=typeof(MainForm).GetMethod("SetFeedbackTitle",fields);
            var kinds=Enum.GetValues(setTitle.GetParameters()[0].ParameterType);
            foreach(var kind in kinds)
            {
                setTitle.Invoke(form,new[]{kind});
                CheckContrast(check,heading.ForeColor,status.BackColor,"feedback "+kind);
            }
            setTitle.Invoke(form,new[]{kinds.GetValue(0)});
            CheckButtonRoles(check,form);
            var batchEntry=Named<Button>(form,"GrowthBatchSettings");
            bool batchEnabled=batchEntry.Enabled;
            try
            {
                batchEntry.Enabled=true;
                CheckContrast(check,batchEntry.ForeColor,batchEntry.BackColor,"batch setting entry");
                check(batchEntry.BackColor==Color.White,"opening batch settings is a secondary command");
            }
            finally{batchEntry.Enabled=batchEnabled;}
            using(var batch=form.BuildGrowthBatchSettingsDialog())
            {
                var mode=Named<ComboBox>(batch,"GrowthBatchMode");
                var limit=Named<ComboBox>(batch,"GrowthBatchLimit");
                var speed=Named<ComboBox>(batch,"GrowthBatchSpeed");
                var apply=Named<Button>(batch,"ApplyGrowthBatchSettings");
                CheckDialog(check,batch,apply,mode,form.Font);
                check(mode.TabIndex<limit.TabIndex && limit.TabIndex<speed.TabIndex && !apply.Enabled,
                    "batch settings order mode, cap and speed, with an idle Apply action until there is a choice");
                foreach(var input in new[]{mode,limit,speed})
                    CheckContrast(check,input.ForeColor,input.BackColor,input.Name);
                foreach(var label in Named<Control>(batch,"GrowthBatchFields").Controls.OfType<Label>())
                    CheckContrast(check,label.ForeColor,label.BackColor,"batch form label "+label.Text);
                apply.Enabled=true;
                CheckContrast(check,apply.ForeColor,apply.BackColor,"apply full queue settings");
                check(apply.BackColor==Color.FromArgb(37,99,210),"applying queue settings is the primary command");
                check(mode.Items.Cast<string>().Contains("仅加速成长（系统自动分配属性）") &&
                    mode.Items.Cast<string>().Contains("自定义勾选属性"),"both modes are explicitly labeled in bulk settings");
                var key=typeof(Form).GetMethod("ProcessDialogKey",fields);
                check(mode.TabStop && limit.TabStop && speed.TabStop && apply.TabStop,
                    "all batch settings and the apply action are reachable by Tab");
                check(!(bool)key.Invoke(batch,new object[]{Keys.Enter}) && batch.DialogResult==DialogResult.None,
                    "Enter in the mode field does not apply an enabled batch action");
                check((bool)key.Invoke(batch,new object[]{Keys.Escape}) && batch.CancelButton.DialogResult==DialogResult.Cancel,
                    "Escape is routed to the batch cancel action");
            }

            var source=new Club{Addr=1,Team=2,Name="Source Club"};
            var origin=new StaffTransferOrigin{Person=new Staff{Name="Test Staff"},Employment=new StaffEmployment{Job=12}};
            using(var transfer=new StaffTransferDialog(origin,source,new[]{source},false))
            {
                CheckDialog(check,transfer,transfer.PreviewButton,transfer.Search,form.Font,true);
                check(transfer.Text=="职员转会与职务" && transfer.Search.TabIndex<transfer.Targets.TabIndex &&
                    transfer.Targets.TabIndex<transfer.Roles.TabIndex && transfer.Roles.Parent.TabIndex<transfer.PreviewButton.Parent.TabIndex,
                    "staff transfer walks through search, targets, role and then preview");
            }

            var fixture=new GrowthFocusTests.Fixture();
            var frame=fixture.Capture();
            using(var optimizer=new AttributeOptimizationDialog(fixture.Player,frame,new GrowthOptions()))
            {
                var mode=Named<ComboBox>(optimizer,"OptimizationMode");
                CheckDialog(check,optimizer,Named<Button>(optimizer,"ApplyAttributeOptimization"),mode,form.Font);
                var roles=Named<DualRoleSelector>(optimizer,"OptimizerDualRoleSelector");
                check(optimizer.Text=="一键属性优化" && roles.TabIndex<mode.TabIndex &&
                    mode.TabIndex<Named<CheckBox>(optimizer,"OptimizationUsePa").TabIndex &&
                    roles.InPosition.TabIndex<roles.InRole.TabIndex && roles.OutPosition.TabIndex<roles.OutRole.TabIndex,
                    "optimization selects phase roles before allocation mode and potential");
                CheckGrid(check,Named<DataGridView>(optimizer,"OptimizationComparison"),"optimization comparison");
                var summary=Named<Label>(optimizer,"OptimizationSummary");
                CheckContrast(check,summary.ForeColor,summary.BackColor,"optimization summary");
                foreach(string name in new[]{"OptimizationUnmetHighlights","OptimizationTierDetails"})
                {
                    var note=Named<TextBox>(optimizer,name);
                    CheckContrast(check,note.ForeColor,note.BackColor,note.Name);
                    check(note.ReadOnly && note.TabStop,note.Name+" is reachable and read-only");
                }
            }

            using(var birthday=new BirthdayEditorDialog("年龄编辑测试球员",new DateTime(2026,11,21),new DateTime(1998,12,6),27))
            {
                birthday.Preview.Text="出生日期：1998-12-06 → 1999-12-06；年龄：27 → 26 岁";
                CheckDialog(check,birthday,birthday.ApplyButton,birthday.Age,form.Font);
                check(birthday.Text=="修改年龄／出生日期" && birthday.Age.TabIndex<birthday.Birthday.TabIndex &&
                    birthday.Age.Parent.TabIndex<birthday.ApplyButton.Parent.TabIndex && !birthday.ApplyButton.Enabled,
                    "age edit starts on age, moves to birthday and only then offers an action");
                check(birthday.Age.Parent.ClientRectangle.Contains(birthday.Birthday.Bounds) &&
                    birthday.Preview.Parent.ClientRectangle.Contains(birthday.Preview.Bounds),
                    "age edit keeps the date input and change preview inside the form");
            }

            using(var restore=new EarlySaveRestoreDialog(fixture.Player,frame))
                CheckDialog(check,restore,Named<Button>(restore,"ApplyEarlySaveRestore"),Named<Button>(restore,"ReadDefaultDatabase"),form.Font);
            using(var support=new GrowthSupportDialog(new GrowthSupportOptions(),GrowthProfile.All[8],frame.Attributes,frame.Ca,18))
                CheckDialog(check,support,Named<Button>(support,"SaveSupportOptions"),Named<CheckBox>(support,"SupportEnabled"),form.Font);
        }
    }
}
