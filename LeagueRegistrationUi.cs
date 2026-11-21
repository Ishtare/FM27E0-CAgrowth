// Modified build notice: FM27E0-CAgrowth v1.1.17; changes dated 2026-11-16.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        void OpenLeagueRegistrationDialog()
        { OpenRegistrationCenter(false); }

        internal static string LeagueRuleInstruction(LeagueEligibilitySnapshot snapshot)
        {
            if(snapshot.RuleCode==0x09)return "这是报名名单总人数上限（1–1000 人）。提高人数可放宽总容量；U21 豁免、工作许可和培养要求仍分别判断。";
            return snapshot.IsMinimum
                ?"这是最低人数要求。降低人数可放宽；设为 0 表示不再要求这一类球员。培养经历与国籍分别判断。"
                :"这是人数上限。提高人数可放宽；设为 0 表示零名额。快捷按钮填入 1000 人，仍需点击应用，不会删除整套赛事规则。";
        }

        void BuildLeagueEligibility(TabPage page,FmProcess attached,List<EligibilityCompetition> competitions,ulong team,Form dialog)
        {
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(8)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float height in new[]{72f,36f,44f})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,40));layout.RowStyles.Add(new RowStyle(SizeType.Percent,60));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            var note=new Label {Dock=DockStyle.Fill,Text="作用范围：所选赛事的所有球队。可调整非欧、外援、培养要求和报名总人数。\r\n球员当季认定和登记请切换到“球员当季资格”。工作许可和特殊条款分别判断。\r\n支持按中文、英文名称或 UID 查找已加载赛事。",UseMnemonic=false};
            layout.Controls.Add(note,0,0);
            var selector=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));selector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,145));selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            selector.Controls.Add(new Label {Text="搜索赛事：",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
            var filter=new TextBox {Name="LeagueCompetitionSearch",Dock=DockStyle.Fill};
            var selected=new ComboBox {Name="LeagueCompetition",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
            selector.Controls.Add(filter,1,0);selector.Controls.Add(selected,2,0);layout.Controls.Add(selector,0,1);
            var bar=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));
            var ruleType=new ComboBox {Name="LeagueRuleType",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DisplayMember="Value"};
            var number=new NumericUpDown {Name="LeagueRuleNumber",Minimum=0,Maximum=1000,Dock=DockStyle.Fill,Enabled=false};
            var relax=new Button {Name="RelaxLeagueRule",Text="放宽所选规则",Dock=DockStyle.Fill,Enabled=false};
            bar.Controls.Add(ruleType,0,0);bar.Controls.Add(number,1,0);bar.Controls.Add(relax,2,0);layout.Controls.Add(bar,0,2);
            var overview=new TextBox {Name="LeagueRuleOverview",Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(245,248,252)};
            var status=new TextBox {Name="LeagueRulePreview",Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White};
            layout.Controls.Add(overview,0,3);layout.Controls.Add(status,0,4);
            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var refresh=new Button {Text="重新读取赛事规则",Dock=DockStyle.Fill};
            var apply=new Button {Name="ApplyLeagueRule",Text="应用所选报名规则",Dock=DockStyle.Fill,Enabled=false};
            actions.Controls.Add(refresh,0,0);actions.Controls.Add(apply,1,0);layout.Controls.Add(actions,0,5);
            LeagueEligibilitySnapshot snapshot=null;bool changing=false;
            Action preview=()=>
            {
                apply.Enabled=false;relax.Enabled=false;
                if(snapshot==null)return;
                string shared=snapshot.SharedIds.Count==0?"未发现其他已加载赛事共享这一规则。":"同时影响："+string.Join("、",snapshot.SharedIds.Select(id=>{var c=competitions.FirstOrDefault(x=>x.Uid==id);return c==null?"赛事 "+id:c.Name;}).ToArray());
                status.Text=snapshot.RuleLabel+"\r\n当前要求："+snapshot.DirectionLabel+" "+snapshot.Limit+" 人；目标："+snapshot.DirectionLabel+" "+number.Value+" 人。\r\n"+
                    LeagueRuleInstruction(snapshot)+"\r\n\r\n"+shared+"\r\n运行值与两份保存数据已核对一致。此处只预览，点击应用并确认后才写入。\r\n"+
                    "提高上限或降低最低要求后，仍需在 FM 中完成报名并核对；其他报名人数、工作许可和赛事资格仍分别判断。";
                bool core= !Safety.Off.ContainsKey("core");apply.Enabled=number.Value!=snapshot.Limit && core;relax.Enabled=number.Value!=snapshot.RelaxedLimit && core;
                relax.Text=snapshot.IsMinimum?"最低人数设为 0":"上限提高至 1000";
            };
            Action load=()=>
            {
                snapshot=null;apply.Enabled=false;relax.Enabled=false;number.Enabled=false;
                if(selected.SelectedItem==null){status.Text="没有匹配的已加载赛事。请更换搜索条件；未加载赛事不凭空生成规则。";return;}
                if(ruleType.SelectedItem==null){status.Text="当前赛事没有列出本版已核实的通用人数规则。特殊条件或其他报名限制仍可能存在。";return;}
                try
                {
                    int code=((KeyValuePair<int,string>)ruleType.SelectedItem).Key;
                    snapshot=LeagueEligibilitySnapshot.Capture(attached.Read,attached.Base,(EligibilityCompetition)selected.SelectedItem,competitions,code);
                    changing=true;number.Minimum=snapshot.MinimumValue;number.Value=snapshot.Limit;changing=false;number.Enabled=true;preview();
                }
                catch(Exception ex){changing=false;status.Text=ex.Message;}
            };
            Action reload=()=>
            {
                changing=true;ruleType.Items.Clear();overview.Clear();
                var competition=selected.SelectedItem as EligibilityCompetition;
                if(competition!=null)
                {
                    try{overview.Text=LeagueEligibilitySnapshot.Overview(attached.Read,attached.Base,competition);}
                    catch(Exception ex){overview.Text=ex.Message;}
                    foreach(int code in LeagueEligibilitySnapshot.Codes(attached.Read,attached.Base,competition))
                        ruleType.Items.Add(new KeyValuePair<int,string>(code,LeagueEligibilitySnapshot.Labels[code]));
                    if(ruleType.Items.Count>0)ruleType.SelectedIndex=0;
                }
                changing=false;load();
            };
            number.ValueChanged+=delegate{if(!changing)preview();};relax.Click+=delegate{if(snapshot!=null)number.Value=snapshot.RelaxedLimit;};
            selected.SelectedIndexChanged+=delegate{if(!changing)reload();};ruleType.SelectedIndexChanged+=delegate{if(!changing)load();};refresh.Click+=delegate{reload();};
            apply.Click+=delegate
            {
                try{if(snapshot==null)return;var plan=snapshot.Plan(attached.Read,(int)number.Value);SaveEligibilityChanges(attached,snapshot.Reader,plan,"报名规则（影响整项赛事）");reload();}
                catch(Exception ex){MessageBox.Show(ex.Message,"赛事规则未完成");reload();}
            };
            filter.TextChanged+=delegate
            {
                var before=selected.SelectedItem as EligibilityCompetition;changing=true;selected.Items.Clear();
                selected.Items.AddRange(competitions.Where(c=>c.Matches(filter.Text)).Cast<object>().ToArray());
                if(before!=null && selected.Items.Contains(before))selected.SelectedItem=before;else if(selected.Items.Count>0)selected.SelectedIndex=0;
                changing=false;reload();
            };
            selected.Items.AddRange(competitions.Cast<object>().ToArray());page.Controls.Add(layout);
            ulong current=team==0?0:attached.ReadPtr(team+0x50);var own=competitions.FirstOrDefault(c=>c.Address==current);
            if(own!=null)selected.SelectedItem=own;else if(selected.Items.Count>0)selected.SelectedIndex=0;else load();
        }
    }
}
