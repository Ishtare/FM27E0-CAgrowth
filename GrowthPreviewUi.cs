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
            if(!focus.SamePositions(frame.Positions))
                throw new InvalidOperationException("位置数据已变化，请停止后重新开始培养。");
            bool hasRoom=frame.Ca<frame.Cap;
            var allocation=hasRoom?focus.AllocateForGrowth(focus.WithLimits(frame.Attributes),frame.Cap,frame.Cap):
                new GrowthAllocation{After=(byte[])frame.Attributes.Clone()};
            var dialog=new Form{Name="GrowthPreview",Width=880,Height=650,MinimumSize=new Size(760,540)};
            StyleWorkspaceDialog(dialog,"成长预览 · "+player.DisplayName);
            var targets=focus.TargetAttributes;
            string budget=allocation.RecommendationLimit.HasValue
                ?"预计 RCA "+allocation.RecommendationBefore+" → "+allocation.RecommendationAfter+
                    "，累计预算上限 "+allocation.RecommendationLimit+"。"
                :focus.CustomSelection?"主要培养与低值补强共用预算，按实际位置核对估算 RCA。":"定向成长保留其余属性的现值。";
            if(!focus.CustomSelection)budget+="先补蓝 15、绿 14、白 11，再补蓝 17、绿 15。";
            var note=new Label{Name="GrowthPreviewBudget",Dock=DockStyle.Top,Padding=new Padding(4),UseMnemonic=false,ForeColor=UiMuted,
                Text="方案："+(focus.CustomSelection?"自选 "+targets.Length+" 项属性":focus.Profile.Name)+"；CA "+frame.Ca+" → "+frame.Cap+"。\n"+
                (hasRoom?"下表为达到 PA 时的估算最终值。":"当前 CA 已达 PA。")+"\n"+budget+"\n"+
                (focus.HasLimits?"持续成长时回调未勾选项超过上限的部分。停止成长或达到 PA 后解除。":
                    "游戏自身训练仍可能调整其他属性。")};
            WrapDockedLabel(note);
            var tabs=new TabControl{Name="GrowthPreviewSections",Dock=DockStyle.Fill,TabIndex=0};var primary=new TabPage("主要培养");var support=new TabPage("低值补强");tabs.TabPages.Add(primary);tabs.TabPages.Add(support);
            var main=NewGrid();main.Name="GrowthPrimaryPreview";main.ReadOnly=true;foreach(string title in new[]{"属性","优先级","基准／冲高","当前","预计最终","增幅"})main.Columns.Add(title,title);
            foreach(int i in targets)
            {
                var tier=focus.CustomSelection?AttributeTier.Protected:focus.RoleTiers.TierOf(i);
                string goal="—";
                string priority="自选";
                if(!focus.CustomSelection)
                {
                    int target=RoleAttributeTiers.Target(tier),milestone=RoleAttributeTiers.Milestone(tier);
                    goal=(target/5).ToString();
                    if(milestone>target)goal+=" → "+(milestone/5);
                    priority=RoleAttributeTiers.Label(tier)+(focus.Profile.WithinTierPriority(i)>0?" · 优先":"");
                }
                main.Rows.Add(Zh.Attr(FmLayout.Attrs[i]),priority,goal,
                    (frame.Attributes[i]/5.0).ToString("0.0"),(allocation.After[i]/5.0).ToString("0.0"),
                    ((allocation.After[i]-frame.Attributes[i])/5.0).ToString("+0.0;-0.0;0.0"));
            }
            primary.Controls.Add(main);
            var extra=NewGrid();extra.Name="GrowthSupportPreview";extra.ReadOnly=true;foreach(string title in new[]{"属性","当前","目标下限","预计最终","原因"})extra.Columns.Add(title,title);extra.Columns[4].FillWeight=240;
            foreach(var item in focus.SupportItems)extra.Rows.Add(Zh.Attr(FmLayout.Attrs[item.Index]),(frame.Attributes[item.Index]/5.0).ToString("0.0"),item.Target,(allocation.After[item.Index]/5.0).ToString("0.0"),item.Reason);
            var supportNote=new Label{Name="GrowthSupportPreviewNote",Dock=DockStyle.Top,Padding=new Padding(8),ForeColor=UiMuted,Text=focus.SupportItems.Length==0?"没有额外补强项。可以在球员培养设置中查看识别建议。":"补强只补到确认的下限，与主培养重合的增量共用同一预算。自然增长也会占用额度。\n如需调整，停止成长后打开“补强设置…”。"};
            WrapDockedLabel(supportNote);
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
