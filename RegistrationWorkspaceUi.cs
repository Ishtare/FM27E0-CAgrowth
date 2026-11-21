// Modified build v1.1.17, 2026-11-16. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class RegistrationCountrySupport
    {
        public static string Country(int uid)
        {
            switch(uid){case 776:return "意大利";case 796:return "西班牙";case 769:return "法国";case 771:return "德国";case 765:return "英格兰";default:return "当前俱乐部国家（UID "+uid+"）";}
        }
        public static bool VerifiedHistoricalFlag(int uid){return uid==776;}
        public static string PersonalScope(int uid)
        {
            return Country(uid)+"：可按实际记录修复国内或欧战登记。"+(VerifiedHistoricalFlag(uid)?"已核实独立的当季历史非欧状态。":"该国家的独立历史非欧标记尚未核实；本项保持只读。非欧人数可在赛事规则页调整。");
        }
    }
    partial class MainForm
    {
        Panel registrationHost;
        internal Form BuildRegistrationDialog(bool preferPlayer)
        {
            var dialog=new Form{Name="RegistrationCenter",Text="报名与资格",Font=Font,AutoScaleMode=AutoScaleMode.Dpi,
                Width=1000,Height=760,MinimumSize=new Size(540,400),StartPosition=FormStartPosition.CenterParent};
            dialog.Controls.Add(BuildRegistrationContent(dialog,preferPlayer));
            var close=new Button{Name="CloseRegistrationCenter",Text="关闭",Dock=DockStyle.Bottom,DialogResult=DialogResult.Cancel};
            StyleButton(close);dialog.Controls.Add(close);dialog.CancelButton=close;
            dialog.Load+=delegate{FitWindowToWorkArea(dialog,this);};
            PolishWorkspace(dialog);return dialog;
        }
        void OpenRegistrationCenter(bool preferPlayer)
        {using(var dialog=BuildRegistrationDialog(preferPlayer))dialog.ShowDialog(this);}

        void MountRegistrationWorkspace(TabPage page,Control prizeContent)
        {
            page.Controls.Clear();var tabs=new TabControl{Name="CompetitionWorkspaceSections",Dock=DockStyle.Fill};
            var registration=new TabPage("报名与资格");var prize=new TabPage("赛事奖金");prize.Controls.Add(prizeContent);tabs.TabPages.Add(registration);tabs.TabPages.Add(prize);
            var bar=WrappingBar("RegistrationActions");
            bar.Padding=new Padding(4,4,4,0);
            var reload=new Button{Name="RegistrationReload",Text="读取报名资料",Width=130};var window=new Button{Name="LeagueRegistrationButton",Text="独立窗口…",Width=110};StyleButton(reload,true);StyleButton(window);bar.Controls.Add(reload);bar.Controls.Add(window);
            bar.Controls.Add(new Label{Text="球员当季资格与赛事名额在同一处管理",AutoSize=true,ForeColor=UiMuted,Padding=new Padding(8,8,0,0)});
            registrationHost=new Panel{Name="RegistrationWorkspace",Dock=DockStyle.Fill};registration.Controls.Add(registrationHost);registration.Controls.Add(bar);page.Controls.Add(tabs);
            reload.Click+=delegate{RefreshRegistrationWorkspace();};
            window.Click+=delegate{OpenRegistrationCenter(PreferPersonalRegistrationPage());};
            RefreshRegistrationWorkspace();
        }
        bool PreferPersonalRegistrationPage()
        {
            var scopes=registrationHost.Controls.Find("RegistrationScopeTabs",true).FirstOrDefault() as TabControl;
            return scopes==null || scopes.SelectedIndex!=1;
        }
        void RefreshRegistrationWorkspace()
        {
            if(registrationHost==null)return;
            bool preferPlayer=PreferPersonalRegistrationPage();
            foreach(Control old in registrationHost.Controls.Cast<Control>().ToArray()){registrationHost.Controls.Remove(old);old.Dispose();}
            registrationHost.Controls.Add(BuildRegistrationContent(this,preferPlayer));PolishWorkspace(registrationHost);
        }
        Control BuildRegistrationContent(Form owner,bool preferPlayer)
        {
            var root=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var attached=fm;
            bool supported=false;string unavailable="尚未连接存档。连接并扫描后，点击“读取报名资料”。";
            if(attached!=null)
                try
                {
                    supported=EligibilityLayout.Supported(attached.Version,attached.ImageSize,attached.Read,attached.Base);
                    if(!supported)unavailable="当前构建的报名数据尚未通过适配检查。本界面保持只读，其他功能按各自检查结果启用。";
                }
                catch(Exception ex){unavailable=ex.Message;}
            var picker=new TableLayoutPanel{Name="RegistrationPlayerPicker",Dock=DockStyle.Top,AutoSize=true,
                AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=4,Padding=new Padding(8,6,8,4)};
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            picker.Controls.Add(new Label{Text="当前球员",AutoSize=true,Anchor=AnchorStyles.Left},0,0);
            var search=new TextBox{Name="RegistrationPlayerSearch",Dock=DockStyle.Fill,Enabled=supported};
            var playerChoice=new ComboBox{Name="RegistrationPlayer",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Enabled=supported,DropDownWidth=500};
            var read=new Button{Name="RegistrationPlayerReload",Text="重新读取",AutoSize=true,Enabled=supported};
            StyleButton(read);picker.Controls.Add(search,1,0);picker.Controls.Add(playerChoice,2,0);picker.Controls.Add(read,3,0);
            var tabs=new TabControl{Name="RegistrationScopeTabs",Dock=DockStyle.Fill};var personal=new TabPage("球员当季资格"){Name="PersonalEligibilityPage",Padding=new Padding(8)};var league=new TabPage("赛事名额与培养"){Name="CompetitionEligibilityPage",Padding=new Padding(8)};tabs.TabPages.Add(personal);tabs.TabPages.Add(league);
            var personalContent=new Panel{Name="PersonalEligibilityContent",Dock=DockStyle.Fill};
            personal.Controls.Add(personalContent);
            personal.Controls.Add(picker);
            root.Controls.Add(tabs);
            Action showEmpty=()=>
            {
                foreach(Control child in personalContent.Controls.Cast<Control>().ToArray()){personalContent.Controls.Remove(child);child.Dispose();}
                personalContent.Controls.Add(new Label{Name="PersonalEligibilityEmpty",Dock=DockStyle.Fill,Padding=new Padding(16),ForeColor=UiMuted,Text=supported?"按中文、英文或 UID 搜索并选择球员。\n\n球员登记、该国已核实的历史状态与赛事人数分别显示作用范围。选择赛事规则页可查看联赛的非欧、外援、报名总人数和培养要求。":unavailable});
            };
            bool changing=false;
            Action loadPlayer=()=>
            {
                if(changing)return;var player=playerChoice.SelectedItem as Player;
                if(!supported || player==null){showEmpty();return;}
                foreach(Control child in personalContent.Controls.Cast<Control>().ToArray()){personalContent.Controls.Remove(child);child.Dispose();}
                try{BuildPersonalEligibility(personalContent,attached,player,owner);}catch(Exception ex){personalContent.Controls.Add(new Label{Dock=DockStyle.Fill,Padding=new Padding(16),Text=ex.Message,ForeColor=UiMuted});}
                PolishWorkspace(personalContent);
            };
            search.TextChanged+=delegate
            {
                if(!supported)return;var before=playerChoice.SelectedItem as Player;changing=true;playerChoice.Items.Clear();
                string query=search.Text.Trim();var candidates=query.Length==0?(loadedPlayer==null?new Player[0]:new[]{loadedPlayer}):PlayerNames.CanSearchPlayer(query)?players.Where(p=>p.Matches(query)).OrderBy(p=>p.MatchRank(query)).ThenBy(p=>p.Name).Take(200).ToArray():new Player[0];
                playerChoice.Items.AddRange(candidates.Cast<object>().ToArray());
                if(before!=null && playerChoice.Items.Contains(before))playerChoice.SelectedItem=before;else if(playerChoice.Items.Count>0)playerChoice.SelectedIndex=0;
                changing=false;loadPlayer();
            };
            playerChoice.SelectedIndexChanged+=delegate{loadPlayer();};read.Click+=delegate{loadPlayer();};
            if(supported)
            {
                ulong team=loadedPlayer==null?0:attached.ReadPtr(loadedPlayer.Tail-FmLayout.TeamBeforePerson);
                BuildLeagueEligibility(league,attached,EligibilityCompetitions(attached),team,owner);
                if(loadedPlayer!=null){playerChoice.Items.Add(loadedPlayer);playerChoice.SelectedIndex=0;}else showEmpty();
            }
            else
            {
                showEmpty();
                league.Controls.Add(new Label{Name="LeagueRuleScope",Dock=DockStyle.Fill,Padding=new Padding(16),UseMnemonic=false,
                    Text=LeagueScope(null)+"\r\n\r\n"+unavailable,ForeColor=UiMuted});
            }
            tabs.SelectedIndex=preferPlayer?0:1;return root;
        }
        void BuildPersonalEligibility(Control page,FmProcess attached,Player player,Form owner)
        {
            if(fm!=attached)throw new InvalidOperationException("游戏连接已变化，请重新读取报名资料。");
            player=RefreshSelectedPlayer(player);
            var snapshot=PlayerEligibilitySnapshot.Capture(attached.Read,attached.Base,player);
            string club=attached.ReadLenString(attached.ReadPtr(snapshot.Club+0xC0))??("俱乐部 "+snapshot.ClubUid);
            var layout=new TableLayoutPanel{Name="PersonalEligibilityForm",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                ColumnCount=1,RowCount=4,Padding=new Padding(4)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,166));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,120));
            layout.Controls.Add(new Label{AutoSize=true,Dock=DockStyle.Fill,UseMnemonic=false,Text="作用范围：仅所选球员  |  "+club+"  |  "+snapshot.Today.ToString("yyyy-MM-dd")+"\n"+RegistrationCountrySupport.PersonalScope(snapshot.NationUid),ForeColor=UiMuted},0,0);
            var overview=NewGrid();overview.Name="PersonalEligibilityOverview";overview.ReadOnly=true;overview.Columns.Add("item","项目");overview.Columns.Add("value","读取结果");overview.Columns.Add("scope","作用范围");overview.Columns[1].FillWeight=180;
            overview.Rows.Add("俱乐部国家",RegistrationCountrySupport.Country(snapshot.NationUid),"当前球员");
            overview.Rows.Add("当季历史非欧状态",snapshot.Italian?(snapshot.ItalyNonEu?"已标记":"未标记"):"该国独立历史标记尚未核实",snapshot.Italian?"已核实":"只读");
            overview.Rows.Add("国内赛事登记",(snapshot.Registered(8)?"已登记":"未登记")+"，取消登记记录 "+snapshot.Unregistrations(8),"当前球队");
            overview.Rows.Add("欧战登记",(snapshot.Registered(0)?"已登记":"未登记")+"，取消登记记录 "+snapshot.Unregistrations(0),"当前球队");layout.Controls.Add(overview,0,1);
            var choices=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
            var historical=new CheckBox{Name="ClearHistoricalNonEu",Text="清除该球员已核实的当季历史非欧认定",AutoSize=true,Enabled=snapshot.Italian && snapshot.ItalyNonEu,Checked=snapshot.Italian && snapshot.ItalyNonEu};
            var repair=new CheckBox{Name="RepairPlayerRegistration",Text="修复所选赛事类型的球员登记",AutoSize=true,Checked=!snapshot.Registered(8) || snapshot.Unregistrations(8)>0};
            var kind=new ComboBox{Name="PlayerRegistrationKind",DropDownStyle=ComboBoxStyle.DropDownList,Width=200};kind.Items.AddRange(new object[]{"国内赛事（各联赛）","欧战（UEFA）"});kind.SelectedIndex=0;
            var remove=new CheckBox{Text="清理所选赛事、当前球队的取消登记记录",AutoSize=true,Checked=true};choices.Controls.Add(historical);choices.Controls.Add(repair);choices.Controls.Add(kind);choices.Controls.Add(remove);layout.Controls.Add(choices,0,2);
            var preview=new TextBox{Name="EligibilityPreview",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(247,249,252)};layout.Controls.Add(preview,0,3);
            var apply=new Button{Name="ApplyPersonalEligibility",Text="应用当前球员的资格修复"};
            StyleButton(apply,true);
            var form=ScrollableForm(layout,apply);
            List<EligibilityEdit> pending=null;
            Action refresh=()=>
            {
                try
                {
                    pending=snapshot.Plan(attached.Read,historical.Checked,repair.Checked?(byte?)(kind.SelectedIndex==0?8:0):null,remove.Checked);
                    preview.Text=(pending.Count==0?"当前选择无需修改。":string.Join(Environment.NewLine,pending.Select(p=>p.Label).Distinct().ToArray()))+"\n\n登记记录与报名资格分别判断。非欧名额、培养要求等请切换到“赛事名额与培养”。工作许可和特殊条款不会因登记修复自动取得。";
                    apply.Enabled=pending.Count>0 && !Safety.Off.ContainsKey("core");
                }
                catch(Exception ex){pending=null;preview.Text=ex.Message;apply.Enabled=false;}
            };
            historical.CheckedChanged+=delegate{refresh();};repair.CheckedChanged+=delegate{refresh();};kind.SelectedIndexChanged+=delegate{refresh();};remove.CheckedChanged+=delegate{refresh();};
            apply.Click+=delegate
            {
                try
                {
                    if(pending==null)return;
                    if(SaveEligibilityChanges(attached,snapshot.Reader,pending,player.DisplayName+"：球员当季资格"))
                    {
                        if(loadedPlayer!=null && loadedPlayer.Uid==player.Uid)LoadPlayer(player);
                        page.Controls.Remove(form);form.Dispose();BuildPersonalEligibility(page,attached,player,owner);PolishWorkspace(page);
                    }
                }
                catch(Exception ex){ShowOperationMessage(FeedbackKind.Failed,ex.Message,"资格修复未完成");refresh();}
            };
            page.Controls.Add(form);refresh();
        }
    }
}
