using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class WorkspaceLayoutTests
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Private).GetValue(form);}
        static Control Named(Control parent,string name){return parent.Controls.Find(name,true).Single();}

        const uint SwpNoMove=0x0002,SwpNoZOrder=0x0004,SwpNoActivate=0x0010,SwpNoSendChanging=0x0400;

        [DllImport("user32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);

        static void ResizeFixture(Action<bool,string> check,Form form,Size size,bool clientArea=false)
        {
            // WinForms caps Form.Size at the desktop's maximum tracking size. Suppressing
            // WM_WINDOWPOSCHANGING also bypasses native tracking limits on these hidden fixtures.
            var handle=form.Handle;
            var outerSize=clientArea?size+(form.Size-form.ClientSize):size;
            if(!SetWindowPos(handle,IntPtr.Zero,0,0,outerSize.Width,outerSize.Height,
                SwpNoMove|SwpNoZOrder|SwpNoActivate|SwpNoSendChanging))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            form.PerformLayout();
            var actual=clientArea?form.ClientSize:form.Size;
            check(actual==size,form.GetType().Name+" layout fixture expected "+size+" but received "+actual);
        }

        static void CheckContained(Action<bool,string> check,Control parent,string context,params Control[] controls)
        {
            foreach(var control in controls)
                check(control.Parent==parent && control.Width>0 && control.Height>0 && parent.ClientRectangle.Contains(control.Bounds),
                    context+" fits "+control.Name+" "+control.Bounds+" in "+parent.ClientRectangle);
            check(controls.SelectMany((first,index)=>controls.Skip(index+1)
                .Select(second=>!first.Bounds.IntersectsWith(second.Bounds))).All(separate=>separate),
                context+" keeps neighboring controls separate");
        }

        static void CheckToolbar(Action<bool,string> check,Control toolbar,string context,params Control[] controls)
        {
            CheckContained(check,toolbar,context,controls);
            foreach(var group in controls.OfType<FlowLayoutPanel>())
            {
                var fields=group.Controls.Cast<Control>().ToArray();
                CheckContained(check,group,context+" field group",fields);
                check(group.ClientSize.Height>=group.Padding.Vertical+fields.Max(field=>field.Height+field.Margin.Vertical),
                    context+" leaves room for the full input height and vertical margins");
            }
        }

        static void SelectPage(TabControl tabs,int index)
        {
            tabs.SelectedIndex=index;
            var handle=tabs.TabPages[index].Handle;
            tabs.PerformLayout();
            tabs.TabPages[index].PerformLayout();
            Application.DoEvents();
        }

        static Rectangle InParent(Control control,Control ancestor)
        {
            var bounds=control.Bounds;
            for(var parent=control.Parent;parent!=ancestor;parent=parent.Parent)bounds.Offset(parent.Location);
            return bounds;
        }

        static void CheckSearchGap(Action<bool,string> check,MainForm form,string inputName,string listName)
        {
            var input=Field<TextBox>(form,inputName);
            var list=Field<ListBox>(form,listName);
            int gap=list.Top-InParent(input,list.Parent).Bottom;
            check(gap>=6,inputName+" leaves a gap above its results ("+gap+" px)");
        }

        static void CheckHeader(Action<bool,string> check,MainForm form,bool needsWrap)
        {
            var header=Named(form,"AppHeader");
            var groups=header.Controls.Cast<Control>().ToArray();
            CheckToolbar(check,header,"app header",groups);
            bool sameRow=groups.Max(group=>group.Top)<groups.Min(group=>group.Bottom);
            check(sameRow!=needsWrap,"header groups stay in one row when they fit and wrap onto the next row when required");
        }

        static void CheckFeedbackLayout(Action<bool,string> check,MainForm form)
        {
            var panel=Named(form,"ScanStatusBar");
            var title=Named(panel,"OperationFeedbackTitle");
            var details=Named(panel,"ScanDetailsButton");
            var message=Named(panel,"OperationFeedbackText");
            CheckContained(check,panel,"operation feedback",title,details,message);
            check(Math.Abs(2*title.Top+title.Height-2*details.Top-details.Height)<=2 &&
                details.Top>=panel.Padding.Top+details.Margin.Top && message.Top-details.Bottom>=details.Margin.Bottom,
                "feedback title and details button have the same vertical center with breathing room");
            check(message.Height>=message.Font.Height && message.Height<=2*message.Font.Height+7,
                "scrollable feedback shows at most two lines of text by default");
        }

        static void CheckGrowthBatchLayout(Action<bool,string> check,MainForm form,Size size,float fontScale)
        {
            using(var dialog=form.BuildGrowthBatchSettingsDialog())
            using(var font=new Font(dialog.Font.FontFamily,dialog.Font.SizeInPoints*fontScale))
            {
                dialog.Font=font;
                ResizeFixture(check,dialog,size,clientArea:true);
                string context="batch settings at "+size+" and font "+fontScale;
                var scroll=Named(dialog,"GrowthBatchScroll");
                var footer=Named(dialog,"DialogActions");
                var fields=Named(scroll,"GrowthBatchFields");
                CheckContained(check,dialog,context,scroll,footer);
                CheckContained(check,footer,context+" actions",footer.Controls.Cast<Control>().ToArray());
                CheckContained(check,fields,context+" fields",
                    fields.Controls.Cast<Control>().Where(control=>control.Name!="GrowthBatchProblem").ToArray());
                check(((Panel)scroll).AutoScroll && fields.Right<=scroll.ClientSize.Width,
                    context+" fits horizontally and can scroll vertically");
                var table=(TableLayoutPanel)fields;
                var rowHeights=table.GetRowHeights();
                foreach(var input in fields.Controls.OfType<ComboBox>())
                {
                    int textWidth=input.Items.Cast<object>().Max(item=>TextRenderer.MeasureText(item.ToString(),input.Font).Width);
                    check(input.Height>=input.PreferredHeight && input.Width>=textWidth+SystemInformation.VerticalScrollBarWidth,
                        context+" shows the full "+input.Name+" font and choices");
                    check(rowHeights[table.GetRow(input)]>=input.Height+input.Margin.Vertical,
                        context+" reserves the full native input height and margins for "+input.Name);
                }
                if(fontScale>1f)
                {
                    var fixedFooter=footer.Bounds;
                    var problem=Named(fields,"GrowthBatchProblem");
                    problem.Text=string.Join(Environment.NewLine,Enumerable.Repeat("培养设置需要检查。请选择适用的自选属性后再提交。",8));
                    problem.Visible=true;
                    fields.PerformLayout();
                    dialog.PerformLayout();
                    CheckContained(check,fields,context+" with validation details",fields.Controls.Cast<Control>().ToArray());
                    check(fields.Height>scroll.ClientSize.Height && footer.Bounds==fixedFooter,
                        context+" keeps the footer fixed when wrapped validation details require scrolling");
                }
            }
        }

        static void CheckOptimizationLayout(Action<bool,string> check,Size size,float fontScale,bool floorConflict=false)
        {
            var fixture=new GrowthFocusTests.Fixture();
            var frame=fixture.Capture();
            if(floorConflict)frame.Ca=1;
            var initial=floorConflict?AttributeOptimizationMode.Redistribute:AttributeOptimizationMode.Preserve;
            using(var dialog=new AttributeOptimizationDialog(fixture.Player,frame,new GrowthOptions(),initial))
            using(var font=new Font(dialog.Font.FontFamily,dialog.Font.SizeInPoints*fontScale))
            {
                dialog.Font=font;
                ResizeFixture(check,dialog,size,clientArea:true);
                string context="attribute optimizer at "+size+" and font "+fontScale;
                var body=Named(dialog,"OptimizationBody");
                var footer=Named(dialog,"DialogActions");
                var split=(SplitContainer)Named(body,"OptimizationSplit");
                CheckContained(check,dialog,context,body,footer);
                CheckContained(check,footer,context+" actions",footer.Controls.Cast<Control>().ToArray());
                CheckContained(check,body,context+" body",Named(body,"OptimizationPlayer"),split);
                var settings=Named(split.Panel1,"OptimizationSettings");
                CheckContained(check,settings,context+" settings",settings.Controls.Cast<Control>()
                    .Where(control=>control.Name!="OptimizationSwitchMode").ToArray());
                var mode=(ComboBox)Named(settings,"OptimizationMode");
                int modeText=mode.Items.Cast<object>().Max(item=>TextRenderer.MeasureText(item.ToString(),mode.Font).Width);
                check(mode.Height>=mode.PreferredHeight && mode.Width>=modeText+SystemInformation.VerticalScrollBarWidth,
                    context+" renders the native mode choices in full");
                var roles=(DualRoleSelector)Named(settings,"OptimizerDualRoleSelector");
                var phases=(TabControl)Named(roles,"OptimizerRolePhaseTabs");
                SelectPage(phases,1);
                var outFields=roles.OutPosition.Parent;
                CheckContained(check,outFields,context+" out of possession",outFields.Controls.Cast<Control>().ToArray());
                SelectPage(phases,0);
                var inFields=roles.InPosition.Parent;
                CheckContained(check,inFields,context+" in possession",inFields.Controls.Cast<Control>().ToArray());

                var result=Named(split.Panel2,"OptimizationResults");
                var summary=Named(result,"OptimizationSummary");
                var gaps=(TextBox)Named(result,"OptimizationUnmetHighlights");
                var sections=(TabControl)Named(result,"OptimizationResultTabs");
                CheckContained(check,split.Panel2,context+" results column",result);
                CheckContained(check,result,context+" preview",summary,gaps,sections);
                check(gaps.ReadOnly && gaps.WordWrap && gaps.ScrollBars==ScrollBars.Vertical &&
                    gaps.Height>=2*gaps.Font.Height,context+" keeps the full unmet highlights scrollable outside the grid filter");
                var comparison=sections.TabPages[0];
                var grid=Named(comparison,"OptimizationComparison");
                CheckContained(check,comparison,context+" comparison",Named(comparison,"OptimizationComparisonFilter"),grid);
                SelectPage(sections,1);
                var details=sections.SelectedTab;
                var tierDetails=Named(details,"OptimizationTierDetails");
                CheckContained(check,details,context+" tier details",tierDetails);
                if(floorConflict)
                    check(summary.Text.Contains("超出预算") && tierDetails.Text.Contains("超出预算") &&
                        !Named(footer,"ApplyAttributeOptimization").Enabled,
                        context+" shows the full hard-floor shortfall and disables Apply");
            }
        }

        static void CheckGrowthPreviewLayout(Action<bool,string> check,MainForm form,Size size,float fontScale)
        {
            var fixture=new GrowthFocusTests.Fixture();
            var frame=fixture.Capture();
            var focus=new GrowthOptions().CreateState(frame.Positions,frame.Attributes,frame.Ca);
            using(var dialog=form.BuildGrowthPreview(fixture.Player,frame,focus))
            using(var font=new Font(dialog.Font.FontFamily,dialog.Font.SizeInPoints*fontScale))
            {
                dialog.Font=font;
                ResizeFixture(check,dialog,size,clientArea:true);
                var note=Named(dialog,"GrowthPreviewBudget");
                var pages=(TabControl)Named(dialog,"GrowthPreviewSections");
                var footer=Named(dialog,"DialogActions");
                string context="growth preview at "+size+" and font "+fontScale;
                CheckContained(check,dialog,context,note,pages,footer);
                SelectPage(pages,0);
                var grid=(DataGridView)Named(pages.SelectedTab,"GrowthPrimaryPreview");
                CheckContained(check,pages.SelectedTab,context+" goals",grid);
                check(note.Text.Contains("白 11") && note.Text.Contains("再补蓝 17") &&
                    grid.Columns[2].HeaderText=="基准／冲高",
                    context+" shows both progression steps with readable goals");
            }
        }

        static void CheckAccentStates(Action<bool,string> check,Button button)
        {
            var original=button.Enabled;
            try
            {
                button.Enabled=false;
                check(button.BackColor==Color.FromArgb(235,239,244) && button.ForeColor==Color.FromArgb(111,121,135),
                    button.Text+" uses gray colors while disabled");
                button.Enabled=true;
                check(button.BackColor==Color.FromArgb(37,99,210) && button.ForeColor==Color.White,
                    button.Text+" resumes blue with white text when enabled");
            }
            finally {button.Enabled=original;}
        }

        static void CheckFinderRoutes(Action<bool,string> check,TabControl tabs,int finderPage,int profilePage,
            object person,ListBox profileList,string searchName,string openName)
        {
            SelectPage(tabs,finderPage);
            var page=tabs.SelectedTab;
            var grid=page.Controls.OfType<WorkspaceGrid>().Single();
            var search=(Button)Named(page,searchName);
            var open=(Button)Named(page,openName);
            check(!open.Enabled && open.TabStop && open.Text=="打开资料",openName+" is a keyboard-reachable action for selected rows");
            search.Enabled=false;
            int row=grid.Rows.Add(person.ToString());
            grid.Rows[row].Tag=person;
            grid.CurrentCell=grid.Rows[row].Cells[0];
            grid.Rows[row].Selected=true;
            check(!open.Enabled,openName+" waits until the result list finishes loading");
            search.Enabled=true;
            check(open.Enabled,openName+" becomes available for the selected result");

            typeof(Button).GetMethod("OnClick",Private).Invoke(open,new object[]{EventArgs.Empty});
            check(tabs.SelectedIndex==profilePage && profileList.SelectedItem==person,openName+" opens the existing profile for the selected row");
            SelectPage(tabs,finderPage);
            bool handled=(bool)typeof(WorkspaceGrid).GetMethod("ProcessDialogKey",Private).Invoke(grid,new object[]{Keys.Enter});
            check(handled && tabs.SelectedIndex==profilePage && profileList.SelectedItem==person,openName+" opens the same profile on Enter");
            SelectPage(tabs,finderPage);
            typeof(DataGridView).GetMethod("OnCellDoubleClick",Private).Invoke(grid,new object[]{new DataGridViewCellEventArgs(0,row)});
            check(tabs.SelectedIndex==profilePage && profileList.SelectedItem==person,openName+" shares the original double-click route");
            SelectPage(tabs,finderPage);
            grid.Rows.Clear();
            check(!open.Enabled,openName+" is disabled when old results are cleared");
        }

        static void CheckRegistrationNavigation(Action<bool,string> check,MainForm form,TabControl workspace)
        {
            SelectPage(workspace,7);
            var scopes=(TabControl)Named(form,"RegistrationScopeTabs");
            SelectPage(scopes,1);
            check(scopes.SelectedTab.Controls.Find("RegistrationPlayerPicker",true).Length==0 &&
                Named(scopes.SelectedTab,"LeagueRuleScope").Text.Contains("影响所有参赛球队"),
                "the competition scope page keeps player controls out and explains the effect of a write");
            var reload=(Button)Named(form,"RegistrationReload");
            typeof(Button).GetMethod("OnClick",Private).Invoke(reload,new object[]{EventArgs.Empty});
            var refreshed=(TabControl)Named(form,"RegistrationScopeTabs");
            check(refreshed!=scopes && refreshed.SelectedIndex==1,"reloading rebuilds the data in the currently selected competition scope");
            SelectPage(refreshed,0);
            typeof(Button).GetMethod("OnClick",Private).Invoke(reload,new object[]{EventArgs.Empty});
            refreshed=(TabControl)Named(form,"RegistrationScopeTabs");
            check(refreshed.SelectedIndex==0 && Named(refreshed,"RegistrationPlayerPicker").Parent==refreshed.SelectedTab,
                "reloading the personal scope preserves its page and player picker");

            using(var league=new TabPage())
            using(var fixture=new FmProcess())
            {
                var competitions=new List<EligibilityCompetition>{
                    new EligibilityCompetition{Name="赛事甲",Uid=1,Address=0x10000},
                    new EligibilityCompetition{Name="赛事乙",Uid=2,Address=0x20000}};
                typeof(MainForm).GetMethod("BuildLeagueEligibility",Private).Invoke(form,new object[]{league,fixture,competitions,0UL,form});
                var selection=(ComboBox)Named(league,"LeagueCompetition");
                var notice=Named(league,"LeagueRuleScope");
                check(notice.Text.Contains("赛事甲 [1]"),"the competition scope identifies the initial selection");
                selection.SelectedIndex=1;
                check(notice.Text.Contains("赛事乙 [2]") && notice.Text.Contains("影响所有参赛球队"),
                    "the competition scope updates the selected identity without changing the write scope");
            }
        }

        public static void Run(Action<bool,string> check)
        {
            using(var fixture=new Form())
            {
                // Exercise the resize path beyond the actual host limit even on a wide desktop.
                var oversize=SystemInformation.MaxWindowTrackSize+new Size(32,32);
                ResizeFixture(check,fixture,oversize);
                ResizeFixture(check,fixture,oversize,clientArea:true);
            }
            CheckOptimizationLayout(check,new Size(928,590),1.4f);
            CheckOptimizationLayout(check,new Size(1056,700),1f);
            CheckOptimizationLayout(check,new Size(1280,780),1.4f);
            CheckOptimizationLayout(check,new Size(928,590),1.4f,floorConflict:true);
            using(var form=new MainForm(false,false))
            {
                CheckGrowthPreviewLayout(check,form,new Size(760,540),1.4f);
                CheckGrowthPreviewLayout(check,form,new Size(1056,700),1f);
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
                foreach(var action in new[]{Field<Button>(form,"btnApplyPlayer"),Field<Button>(form,"btnApplyClub"),
                    (Button)Named(form,"GrowthStartStop"),(Button)Named(form,"AttachAndScan")})
                    CheckAccentStates(check,action);
                foreach(var gridField in new[]{"gridAttrs","gridPos","gridStaff","gridGrow","gridFind","gridStaffFind","gridPrize"})
                {
                    var grid=Field<DataGridView>(form,gridField);
                    check(grid.BorderStyle==BorderStyle.FixedSingle && grid.AutoSizeColumnsMode==DataGridViewAutoSizeColumnsMode.Fill,
                        gridField+" keeps a clear frame and fills the available column width");
                }
                Field<Label>(form,"lblClub").Text="俱乐部名称 Club Name With a Long English Translation [UID 310000001]\r\n财务只读，完整的检查结果应可在此阅读。";
                Field<Label>(form,"lblStoredPounds").Text="原始英镑金额：余额 £2,147,483,647，转会预算 £120,000,000，下赛季预算 £145,000,000，周薪预算 £560,000／周";
                foreach(var size in new[]{new Size(1120,720),new Size(1360,860),new Size(1600,960)})
                {
                    ResizeFixture(check,form,size);
                    var tabHandle=tabs.Handle;
                    if(size.Width==1120)
                    {
                        typeof(Form).GetMethod("OnLoad",Private).Invoke(form,new object[]{EventArgs.Empty});
                        // Startup fits the real window to the desktop. Restore the test viewport.
                        ResizeFixture(check,form,size);
                    }
                    for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;var pageHandle=tabs.TabPages[i].Handle;form.PerformLayout();tabs.PerformLayout();Application.DoEvents();check(tabs.TabPages[i].ClientSize.Width>500 && tabs.TabPages[i].ClientSize.Height>350,"each page retains useful space: "+size+" page "+i+" "+tabs.TabPages[i].ClientSize);}
                    var header=Named(form,"ConnectionBar");
                    CheckContained(check,header,"connection "+size,Named(header,"AttachAndScan"),Named(header,"ScanProgress"),Named(header,"CurrencyDisplayHint"));
                    var footer=Named(form,"WorkspaceFooter");
                    CheckContained(check,footer,"footer "+size,Named(footer,"SaveReminder"),Named(footer,"OriginalAuthorCredit"));
                    CheckFeedbackLayout(check,form);
                    if(size.Width==1600)CheckHeader(check,form,false);

                    SelectPage(tabs,0);
                    var positions=Field<DataGridView>(form,"gridPos");
                    var attributeSplit=Field<SplitContainer>(form,"splitAttrs");
                    var toolbar=Named(tabs.SelectedTab,"PlayerAttributeToolbar");
                    var attributeBounds=InParent(attributes,attributeSplit);
                    var positionBounds=InParent(positions,attributeSplit);
                    check(toolbar.Parent==attributeSplit.Parent && toolbar.Bottom<=attributeSplit.Top &&
                        attributeBounds.Top==positionBounds.Top && attributeBounds.Bottom==positionBounds.Bottom,
                        "the two player grids start below the same toolbar and share their top and bottom edges at "+size);
                    var positionHandle=positions.Handle;
                    int contentWidth=positions.ClientSize.Width-2*SystemInformation.BorderSize.Width;
                    int columnWidth=positions.Columns.GetColumnsWidth(DataGridViewElementStates.Visible);
                    // The grid also reserves a pixel for the leading line inside its single-line frame.
                    check(positions.Rows.Count==0 && contentWidth-columnWidth==1 && positions.Columns[0].Width>positions.Columns[1].Width,
                        "an empty positions grid fills its frame and gives the position label more width at "+size);
                    CheckSearchGap(check,form,"txtPlayerSearch","lstPlayers");

                    foreach(var page in new[]{5,6})
                    {
                        SelectPage(tabs,page);
                        var filters=Named(tabs.SelectedTab,page==5?"PlayerFinderFilters":"StaffFinderFilters");
                        CheckToolbar(check,filters,"finder "+page+" "+size,filters.Controls.Cast<Control>().ToArray());
                    }

                    SelectPage(tabs,1);
                    var staffActions=Named(form,"StaffActions");
                    CheckToolbar(check,staffActions,"staff actions "+size,staffActions.Controls.Cast<Control>().ToArray());
                    var staffToolbar=Named(tabs.SelectedTab,"StaffAttributeToolbar");
                    check(staffToolbar.Bottom<=staff.Top && staff.Bottom<=staffActions.Top,
                        "the staff grid stays between its own toolbar and actions at "+size);
                    var staffSplit=Field<SplitContainer>(form,"splitStaff");
                    var staffListBounds=InParent(Field<ListBox>(form,"lstStaff"),staffSplit);
                    var staffGridBounds=InParent(staff,staffSplit);
                    check(staffListBounds.Top==staffGridBounds.Top && staffListBounds.Bottom==staffGridBounds.Bottom,
                        "the staff list and attribute grid share their top and bottom edges at "+size);
                    CheckSearchGap(check,form,"txtStaffSearch","lstStaff");

                    SelectPage(tabs,2);
                    var money=Named(finance,"ClubFinanceForm");
                    CheckContained(check,money,"club finance "+size,Field<TextBox>(form,"txtBalance"),Field<TextBox>(form,"txtTransfer"),
                        Field<TextBox>(form,"txtTransferNext"),Field<TextBox>(form,"txtWage"),Field<Label>(form,"lblStoredPounds"));
                    var clubActions=Named(form,"ClubActions");
                    CheckToolbar(check,clubActions,"club actions "+size,clubActions.Controls.Cast<Control>().ToArray());
                    check(finance.Bottom<=clubActions.Top && finance.TabPages.Cast<TabPage>().All(page=>page.AutoScroll),
                        "club forms scroll inside their tabs and below the identity while actions stay separate "+size);
                    CheckSearchGap(check,form,"txtClubSearch","lstClubs");

                    SelectPage(tabs,3);
                    CheckSearchGap(check,form,"txtSquadClubSearch","lstSquadClubs");

                    SelectPage(tabs,7);
                    SelectPage(scopes,0);
                    var registrationActions=Named(form,"RegistrationActions");
                    var playerPicker=Named(form,"RegistrationPlayerPicker");
                    CheckToolbar(check,registrationActions,"registration actions "+size,registrationActions.Controls.Cast<Control>().ToArray());
                    CheckContained(check,playerPicker,"registration picker "+size,playerPicker.Controls.Cast<Control>().ToArray());
                    SelectPage(competition,1);
                    CheckSearchGap(check,form,"txtPrizeSearch","lstPrizes");
                    SelectPage(competition,0);

                    SelectPage(tabs,4);form.PerformLayout();typeof(MainForm).GetMethod("LayoutGrowthWorkspace",Private).Invoke(form,null);
                    var split=(SplitContainer)form.Controls.Find("GrowthContent",true).Single();
                    check(split.Panel2Collapsed || split.Panel1.Width>=300 && split.Panel2.Width>=280,"growth settings collapse at narrow widths instead of squeezing the roster");
                    if(form.CurrentAutoScaleDimensions.Width<=96)
                    {
                        if(size.Width==1120)check(split.Panel2Collapsed,"a narrow roster uses the cultivation settings dialog");
                        if(size.Width==1600)check(!split.Panel2Collapsed,"a wide roster shows the settings inspector");
                    }
                    var growthActions=Named(form,"GrowthActions");
                    CheckToolbar(check,growthActions,"growth actions "+size,growthActions.Controls.Cast<Control>().ToArray());
                    var commands=Named(form,"GrowthCommandBar");
                    var singleActions=Named(form,"GrowthSingleActions");
                    CheckToolbar(check,commands,"growth command groups "+size,singleActions,growthActions);
                     var batchGroup=Named(form,"GrowthBatchSettingsGroup");
                     var batchButton=Named(batchGroup,"GrowthBatchSettings");
                     CheckContained(check,commands,"growth with batch settings "+size,singleActions,growthActions,batchGroup);
                     CheckContained(check,batchGroup,"batch entry "+size,batchButton);
                     check(batchButton.Width>=batchButton.PreferredSize.Width && batchButton.Height>=batchButton.PreferredSize.Height,
                         "the full-queue batch label has room at "+size);
                    check(Named(form,"GrowthEditPlayer").Parent==singleActions && Named(form,"GrowthStartStop").Parent==growthActions,
                        "player and whole-queue commands are visibly separate at "+size);
                    var scopeToolbar=Named(form,"GrowthScopeToolbar");
                    CheckToolbar(check,scopeToolbar,"growth scope "+size,scopeToolbar.Controls.Cast<Control>().ToArray());
                    CheckSearchGap(check,form,"txtGrowClubSearch","lstGrowClubs");
                    var rosterToolbar=Named(form,"GrowthRosterToolbar");
                    CheckToolbar(check,rosterToolbar,"growth roster "+size,Field<TextBox>(form,"txtGrowPlayerSearch"),
                        Named(rosterToolbar,"GrowthOnlyChecked"),Named(rosterToolbar,"GrowthSelectVisible"),
                        Named(rosterToolbar,"GrowthUnselectVisible"),Named(rosterToolbar,"GrowthDetailedColumns"));
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
                    CheckToolbar(check,rosterToolbar,"growth queue "+size,Field<TextBox>(form,"txtGrowPlayerSearch"),
                        remove,Named(rosterToolbar,"GrowthDetailedColumns"));
                    typeof(Button).GetMethod("OnClick",Private).Invoke(browse,new object[]{EventArgs.Empty});
                }
                ResizeFixture(check,form,new Size(1120,720));
                var minSize=form.MinimumSize;
                var appHeader=Named(form,"AppHeader");
                var headerGroups=appHeader.Controls.Cast<Control>().ToArray();
                int groupWidth=headerGroups.Max(group=>group.Width+group.Margin.Horizontal);
                form.MinimumSize=Size.Empty;
                ResizeFixture(check,form,new Size(groupWidth+appHeader.Padding.Horizontal+28,form.ClientSize.Height),clientArea:true);
                CheckHeader(check,form,true);
                form.MinimumSize=minSize;
                ResizeFixture(check,form,new Size(1120,720));
                var initialFeedback=feedback.Text;
                int singleLineHeight=feedback.Height;
                feedback.Text="第一行操作结果\r\n第二行原因\r\n第三行详情";
                CheckFeedbackLayout(check,form);
                check(feedback.Height>singleLineHeight && feedback.Lines.Length==3,
                    "multiline feedback grows to a second visible line and keeps the full scrollable text");
                feedback.Text=initialFeedback;
                SelectPage(tabs,5);
                var position=Field<ComboBox>(form,"fPos");
                var inputFont=position.Font;
                var inputMargin=position.Margin;
                int groupHeight=position.Parent.Height;
                using(var largerFont=new Font(inputFont.FontFamily,inputFont.SizeInPoints*1.4f,inputFont.Style))
                {
                    try
                    {
                        position.Font=largerFont;
                        position.Margin=new Padding(inputMargin.Left,inputMargin.Top+8,inputMargin.Right,inputMargin.Bottom+8);
                        var positionHandle=position.Handle;
                        var filters=Named(tabs.SelectedTab,"PlayerFinderFilters");
                        CheckToolbar(check,filters,"finder with larger inputs",filters.Controls.Cast<Control>().ToArray());
                        check(position.Parent.Height>groupHeight,"the position group grows when the input font and vertical margins increase");
                    }
                    finally
                    {
                        position.Font=inputFont;
                        position.Margin=inputMargin;
                    }
                }
                using(var editor=new GrowthEditorPanel{Dock=DockStyle.Fill})
                using(var dialog=form.BuildGrowthSettingsDialog(editor))
                {
                    ResizeFixture(check,dialog,new Size(415,380),clientArea:true);
                    var close=Named(dialog,"CloseGrowthSettings");
                    CheckContained(check,dialog,"compact growth dialog",editor,close);
                    check(editor.AutoScroll && editor.Controls[0].Height>editor.ClientSize.Height && close==dialog.AcceptButton,
                        "the narrow growth dialog scrolls the settings with its completion button outside the scroll region");
                }
                using(var currency=form.BuildCurrencyDialog())
                {
                    ResizeFixture(check,currency,new Size(540,290),clientArea:true);
                    var layout=Named(currency,"CurrencyForm");
                    var save=Named(currency,"SaveCurrency");
                    check(((Panel)layout.Parent).AutoScroll && layout.Height>layout.Parent.ClientSize.Height,
                        "currency settings keep a scroll region when the dialog cannot show the full form");
                    CheckContained(check,save.Parent,"compact currency footer",save,layout.Parent);
                }
                using(var dialog=form.BuildRegistrationDialog(false))
                {
                    var scope=(TabControl)dialog.Controls.Find("RegistrationScopeTabs",true).Single();
                    check(dialog.Text=="报名与资格" && scope.TabPages.Count==2 && scope.SelectedIndex==1,"the detached registration dialog shares both scopes and the requested initial tab");
                    check(dialog.Controls.Find("PersonalEligibilityEmpty",true).Length==1,"the unified dialog explains its disconnected state without attaching to FM");
                    ResizeFixture(check,dialog,new Size(570,420),clientArea:true);
                    SelectPage(scope,0);
                    var picker=Named(dialog,"RegistrationPlayerPicker");
                    CheckContained(check,picker,"compact registration picker",picker.Controls.Cast<Control>().ToArray());
                    var close=Named(dialog,"CloseRegistrationCenter");
                    CheckContained(check,dialog,"compact registration dialog",close,scope.Parent);
                    check(picker.Parent==scope.SelectedTab,"the compact dialog places the picker inside its personal tab");
                }
                var finderPlayer=new Player {Uid=901151,Name="Finder Test Player"};
                Field<List<Player>>(form,"players").Add(finderPlayer);
                CheckFinderRoutes(check,tabs,5,0,finderPlayer,Field<ListBox>(form,"lstPlayers"),"SearchPlayers","OpenPlayerFinderResult");
                var finderStaff=new Staff {Uid=901152,Name="Finder Test Staff"};
                Field<List<Staff>>(form,"staff").Add(finderStaff);
                CheckFinderRoutes(check,tabs,6,1,finderStaff,Field<ListBox>(form,"lstStaff"),"SearchStaff","OpenStaffFinderResult");
                CheckRegistrationNavigation(check,form,tabs);
                CheckGrowthBatchLayout(check,form,new Size(428,380),1.4f);
                CheckGrowthBatchLayout(check,form,new Size(550,460),1f);
                CheckGrowthBatchLayout(check,form,new Size(720,540),1f);
                WorkspaceThemeTests.Run(check,form);
            }
        }
    }
}
