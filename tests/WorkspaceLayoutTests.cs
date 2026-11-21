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
        static Control Named(Control parent,string name){return parent.Controls.Find(name,true).Single();}

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
                    form.Size=size;var formHandle=form.Handle;var tabHandle=tabs.Handle;
                    if(size.Width==1120)typeof(Form).GetMethod("OnLoad",Private).Invoke(form,new object[]{EventArgs.Empty});
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
                form.Size=new Size(1120,720);
                var minSize=form.MinimumSize;
                var appHeader=Named(form,"AppHeader");
                var headerGroups=appHeader.Controls.Cast<Control>().ToArray();
                int groupWidth=headerGroups.Max(group=>group.Width+group.Margin.Horizontal);
                form.MinimumSize=Size.Empty;
                form.ClientSize=new Size(groupWidth+appHeader.Padding.Horizontal+28,form.ClientSize.Height);
                CheckHeader(check,form,true);
                form.MinimumSize=minSize;
                form.Size=new Size(1120,720);
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
                    dialog.ClientSize=new Size(415,380);
                    var handle=dialog.Handle;
                    dialog.PerformLayout();
                    var close=Named(dialog,"CloseGrowthSettings");
                    CheckContained(check,dialog,"compact growth dialog",editor,close);
                    check(editor.AutoScroll && editor.Controls[0].Height>editor.ClientSize.Height && close==dialog.AcceptButton,
                        "the narrow growth dialog scrolls the settings with its completion button outside the scroll region");
                }
                using(var currency=form.BuildCurrencyDialog())
                {
                    currency.ClientSize=new Size(540,290);
                    var handle=currency.Handle;
                    currency.PerformLayout();
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
                    dialog.ClientSize=new Size(570,420);
                    var handle=dialog.Handle;
                    dialog.PerformLayout();
                    var picker=Named(dialog,"RegistrationPlayerPicker");
                    CheckContained(check,picker,"compact registration picker",picker.Controls.Cast<Control>().ToArray());
                    var close=Named(dialog,"CloseRegistrationCenter");
                    CheckContained(check,dialog,"compact registration dialog",close,picker.Parent);
                }
            }
        }
    }
}
