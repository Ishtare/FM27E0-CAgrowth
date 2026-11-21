// Modified build v1.1.18, 2026-11-17. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        internal Form BuildGrowthPreview(Player player,GrowthStep frame,GrowthFocusState focus)
        {
            bool hasRoom=frame.Ca<frame.Cap;
            var allocation=hasRoom?focus.Allocate(focus.WithLimits(frame.Attributes),frame.Cap,frame.Cap):
                new GrowthAllocation{After=(byte[])frame.Attributes.Clone()};
            var dialog=new Form{Name="GrowthPreview",Width=880,Height=650,MinimumSize=new Size(760,540)};
            StyleWorkspaceDialog(dialog,"成长预览 · "+player.DisplayName);
            var note=new Label{Dock=DockStyle.Top,Height=100,Padding=new Padding(4),UseMnemonic=false,ForeColor=UiMuted,Text="方案："+(focus.CustomSelection?"自选 "+focus.TargetAttributes.Length+" 项属性":focus.Profile.Name)+"；CA "+frame.Ca+" → "+frame.Cap+"。\n"+
                (hasRoom?"下表为达到 PA 时的估算最终值，按档位逐步执行。":"当前 CA 已达 PA，没有新增成长空间。")+"\n主要培养与低值补强共用预算，未补到目标表示额度不足。\n"+
                (focus.HasLimits?"持续成长时回调未勾选项超过上限的部分。停止成长或达到 PA 后解除。":
                    "游戏自身训练仍可能调整其他属性。")};
            var tabs=new TabControl{Name="GrowthPreviewSections",Dock=DockStyle.Fill,TabIndex=0};var primary=new TabPage("主要培养");var support=new TabPage("低值补强");tabs.TabPages.Add(primary);tabs.TabPages.Add(support);
            var main=NewGrid();main.Name="GrowthPrimaryPreview";main.ReadOnly=true;foreach(string title in new[]{"属性","优先级","当前","预计最终","增幅"})main.Columns.Add(title,title);
            foreach(int i in focus.TargetAttributes)main.Rows.Add(Zh.Attr(FmLayout.Attrs[i]),focus.CustomSelection?"自选":focus.Profile.Core.Contains(i)?"关键":"重要",(frame.Attributes[i]/5.0).ToString("0.0"),(allocation.After[i]/5.0).ToString("0.0"),"+"+((allocation.After[i]-frame.Attributes[i])/5.0).ToString("0.0"));
            primary.Controls.Add(main);
            var extra=NewGrid();extra.Name="GrowthSupportPreview";extra.ReadOnly=true;foreach(string title in new[]{"属性","当前","目标下限","预计最终","原因"})extra.Columns.Add(title,title);extra.Columns[4].FillWeight=240;
            foreach(var item in focus.SupportItems)extra.Rows.Add(Zh.Attr(FmLayout.Attrs[item.Index]),(frame.Attributes[item.Index]/5.0).ToString("0.0"),item.Target,(allocation.After[item.Index]/5.0).ToString("0.0"),item.Reason);
            var supportNote=new Label{Name="GrowthSupportPreviewNote",Dock=DockStyle.Top,Height=84,Padding=new Padding(8),ForeColor=UiMuted,Text=focus.SupportItems.Length==0?"没有额外补强项：未启用，或没有符合条件的低值属性。\n可以在球员培养设置中开启并查看自动识别建议。":"补强只补到确认的下限。与主培养重合的属性可能继续按主方案增长。\n预算份额是新增估算额度的上限；自然增长也会占用，无法保证所有目标均能补齐。\n这里仅预览。如需取消某项或改目标，停止成长后打开“补强设置…”。"};
            support.Controls.Add(extra);support.Controls.Add(supportNote);
            if(focus.HasLimits)
            {
                var ceilings=new TabPage("未勾选项上限");
                var limitGrid=NewGrid();limitGrid.Name="GrowthLimitsPreview";limitGrid.ReadOnly=true;
                foreach(string title in new[]{"属性","当前","本轮上限"})limitGrid.Columns.Add(title,title);
                foreach(int index in focus.LimitedAttributes)
                    limitGrid.Rows.Add(Zh.Attr(FmLayout.Attrs[index]),(frame.Attributes[index]/5.0).ToString("0.0"),(focus.LimitFor(index)/5.0).ToString("0.0"));
                ceilings.Controls.Add(limitGrid);tabs.TabPages.Add(ceilings);
            }
            var actions=DialogActions(dialog,DialogDismiss("CloseGrowthPreview","关闭"));
            actions.TabIndex=1;
            dialog.Controls.Add(tabs);dialog.Controls.Add(note);dialog.Controls.Add(actions);
            PolishWorkspace(dialog);return dialog;
        }
    }
}
