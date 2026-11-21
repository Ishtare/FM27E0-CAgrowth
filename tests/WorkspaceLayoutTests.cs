using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class WorkspaceLayoutTests
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Private).GetValue(form);}
        public static void Run(Action<bool,string> check)
        {
            using(var form=new MainForm(false,false))
            {
                var tabs=(TabControl)form.Controls.Find("WorkspaceTabs",true).Single();
                var feedback=(TextBox)form.Controls.Find("OperationFeedbackText",true).Single();
                check(feedback.Multiline && feedback.WordWrap && feedback.ReadOnly && feedback.ScrollBars==ScrollBars.Vertical &&
                    form.Controls.Find("ScanDetailsButton",true).Length==1,
                    "the workspace exposes selectable full feedback with vertical scrolling and a separate connection report");
                var finance=(TabControl)form.Controls.Find("ClubManagementSections",true).Single();
                check(finance.TabPages.Count==3 && finance.TabPages[0].Text=="资金与预算" && finance.TabPages[2].Text=="资料与票务","club management splits money, facilities and identity/tickets into separate sections");
                check(Field<TextBox>(form,"txtBalance").Parent!=Field<NumericUpDown>(form,"numCapacity").Parent,"financial and stadium inputs are no longer one long overflowing form");
                check(form.Controls.Find("PlayerAttributeToolbar",true).Length==1 && form.Controls.Find("StaffAttributeToolbar",true).Length==1,"player and staff editing both expose attribute filters");
                var attributes=Field<DataGridView>(form,"gridAttrs");
                for(int i=0;i<62;i++)attributes.Rows.Add("Field "+i,12,60,"已验证");
                ((ComboBox)form.Controls.Find("PlayerAttributeCategory",true).Single()).SelectedIndex=3;
                check(attributes.Rows.Count==62 && attributes.Rows[34].Visible && attributes.Rows[38].Visible && !attributes.Rows[7].Visible,"attribute filtering preserves row identities and values used by the writer");
                check(attributes.Rows.Cast<DataGridViewRow>().All(row=>Convert.ToInt32(row.Cells[1].Value)==12),"category changes do not modify pending attribute values");
                ((ComboBox)form.Controls.Find("PlayerAttributeCategory",true).Single()).SelectedIndex=0;
                check(attributes.Rows.Cast<DataGridViewRow>().All(row=>row.Visible),"all hidden categories can be restored without rebuilding the data");
                ((ComboBox)form.Controls.Find("PlayerAttributeCategory",true).Single()).SelectedIndex=3;
                var more=(Button)form.Controls.Find("MorePlayerActions",true).Single();
                var fill=more.ContextMenuStrip.Items.OfType<ToolStripMenuItem>().SelectMany(group=>group.DropDownItems.OfType<ToolStripMenuItem>())
                    .Single(item=>item.Tag is Button && ((Button)item.Tag).Name=="FillVerifiedPlayerAttributes");
                attributes.Rows[8].Cells[3].Value="待检查";
                fill.PerformClick();
                check(fill.Text=="将全部已验证基础属性填至极值" && !attributes.Rows[7].Visible &&
                    Convert.ToInt32(attributes.Rows[7].Cells[1].Value)==(FmLayout.LowerIsBetter(FmLayout.Attrs[7])?1:20) &&
                    Convert.ToInt32(attributes.Rows[8].Cells[1].Value)==12,
                    "the bulk-fill label matches the existing behavior including hidden verified attributes only");
                check(form.Controls.Find("GrowthSelectVisible",true).Single().Text=="勾选当前筛选结果" &&
                    form.Controls.Find("GrowthUnselectVisible",true).Single().Text=="取消勾选当前筛选结果",
                    "growth selection commands explicitly identify the current filtered results");
                var staff=Field<DataGridView>(form,"gridStaff");staff.Rows.Add("战术",15,"已验证");staff.Rows.Add("医疗",14,"已验证");
                ((TextBox)form.Controls.Find("StaffAttributeSearch",true).Single()).Text="战术";
                check(staff.Rows.Count==2 && staff.Rows[0].Visible && !staff.Rows[1].Visible,"staff attribute filtering preserves edit row indices");
                check(form.Controls.Find("SquadMoreActions",true).Length==1 && form.Controls.Find("ChangeDetails",true).Length==1 && form.Controls.Find("FreezeActions",true).Length==1,"squad, history and freeze pages each use the revised command layout");
                var competition=(TabControl)form.Controls.Find("CompetitionWorkspaceSections",true).Single();
                var scopes=(TabControl)form.Controls.Find("RegistrationScopeTabs",true).Single();
                check(competition.TabPages.Count==2 && competition.TabPages[0].Text=="报名与资格" && scopes.TabPages.Count==2,"registration is available in the main workspace beside prize money");
                check(scopes.TabPages[0].Text=="球员当季资格" && scopes.TabPages[1].Text=="赛事名额与培养","player eligibility and whole-competition limits share one manager with explicit scopes");
                check(!((ComboBox)form.Controls.Find("RegistrationPlayer",true).Single()).Enabled,"disconnected registration remains inspectable without enabling unknown writes");
                foreach(var size in new[]{new Size(1120,720),new Size(1360,860),new Size(1600,960)})
                {
                    form.Size=size;var formHandle=form.Handle;var tabHandle=tabs.Handle;
                    if(size.Width==1120)typeof(Form).GetMethod("OnLoad",Private).Invoke(form,new object[]{EventArgs.Empty});
                    for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;var pageHandle=tabs.TabPages[i].Handle;form.PerformLayout();tabs.PerformLayout();Application.DoEvents();check(tabs.TabPages[i].ClientSize.Width>500 && tabs.TabPages[i].ClientSize.Height>350,"each page retains useful space: "+size+" page "+i+" "+tabs.TabPages[i].ClientSize);}
                    tabs.SelectedIndex=4;form.PerformLayout();typeof(MainForm).GetMethod("LayoutGrowthWorkspace",Private).Invoke(form,null);
                    var split=(SplitContainer)form.Controls.Find("GrowthContent",true).Single();
                    check(split.Panel2Collapsed || split.Panel1.Width>=300 && split.Panel2.Width>=280,"growth settings collapse at narrow widths instead of squeezing the roster");
                    var refresh=form.Controls.Find("RefreshGrowthRoster",true).Single();
                    check(refresh.Right<=refresh.Parent.ClientSize.Width && refresh.Bottom<=refresh.Parent.ClientSize.Height,"local refresh remains visible at "+size);
                    var browse=(Button)form.Controls.Find("GrowthBrowseClubs",true).Single();
                    var queue=(Button)form.Controls.Find("GrowthViewQueue",true).Single();
                    var start=(Button)form.Controls.Find("GrowthStartStop",true).Single();
                    check(queue.Left>=browse.Right && queue.Right<=queue.Parent.ClientSize.Width && start.Right<=start.Parent.ClientSize.Width,
                        "both growth views and the counted start action fit at "+size);
                    typeof(Button).GetMethod("OnClick",Private).Invoke(queue,new object[]{EventArgs.Empty});
                    var remove=form.Controls.Find("GrowthRemovePlayer",true).Single();
                    check(Field<SplitContainer>(form,"splitGrow").Panel1Collapsed && remove.Right<=remove.Parent.ClientSize.Width && Field<DataGridView>(form,"gridGrow").Columns[14].Visible,
                        "the queue has its own full-width list, club column and removal action at "+size);
                    typeof(Button).GetMethod("OnClick",Private).Invoke(browse,new object[]{EventArgs.Empty});
                }
                using(var dialog=form.BuildRegistrationDialog(false))
                {
                    var scope=(TabControl)dialog.Controls.Find("RegistrationScopeTabs",true).Single();
                    check(dialog.Text=="报名与资格" && scope.TabPages.Count==2 && scope.SelectedIndex==1,"the detached registration dialog shares both scopes and the requested initial tab");
                    check(dialog.Controls.Find("PersonalEligibilityEmpty",true).Length==1,"the unified dialog explains its disconnected state without attaching to FM");
                }
            }
        }
    }
}
