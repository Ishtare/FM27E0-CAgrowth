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
        bool CanSetGrowthBatch { get { return !growOn && fm!=null && growWatch.Count>0; } }

        void OpenGrowthBatchSettings()
        {
            if(!CanSetGrowthBatch)return;
            using(var dialog=BuildGrowthBatchSettingsDialog())dialog.ShowDialog(this);
        }

        static ComboBox GrowthBatchChoice(string name,int tabIndex)
        {
            return new ComboBox
            {
                Name=name,TabIndex=tabIndex,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,
                DropDownStyle=ComboBoxStyle.DropDownList,
                ForeColor=UiInk,BackColor=Color.White,DropDownWidth=380,Margin=new Padding(4,2,6,8)
            };
        }

        internal Form BuildGrowthBatchSettingsDialog()
        {
            var attached=fm;
            var queued=growWatch.Values.ToDictionary(t=>t.P.Person,t=>t.P.Uid);
            var selected=gridGrow.CurrentRow==null?null:gridGrow.CurrentRow.Tag as Player;
            var template=selected==null?new int[0]:OptionsFor(selected).Attributes;
            var dialog=new Form{Name="GrowthBatchSettingsDialog",MinimumSize=new Size(426,345)};
            StyleWorkspaceDialog(dialog,"批量设置整份培养名单");
            dialog.ClientSize=new Size(550,468);

            var mode=GrowthBatchChoice("GrowthBatchMode",0);
            mode.Items.Add("保留各自成长模式");
            mode.Items.AddRange(GrowthOptions.ModeNames);
            var limit=GrowthBatchChoice("GrowthBatchLimit",1);
            limit.Items.AddRange(new object[]{"保留各自上限设置","开启自选属性上限","关闭上限"});
            var speed=GrowthBatchChoice("GrowthBatchSpeed",2);
            speed.Items.Add("保留当前 "+GrowGear()+" 倍");
            speed.Items.AddRange(Enumerable.Range(2,9).Select(gear=>gear+" 倍").ToArray());
            mode.SelectedIndex=limit.SelectedIndex=speed.SelectedIndex=0;

            var scope=FieldLabel("作用范围为整份培养名单的 "+queued.Count+" 人，包含其他俱乐部和被筛选隐藏的球员。");
            scope.Name="GrowthBatchScope";
            var note=FieldLabel("属性上限只在自定义勾选属性模式生效。系统自动分配只推进 CA，由 FM 处理属性。\n"+
                (template.Length>0?"切到自选时，只为没有勾选项的球员沿用当前选中球员的 "+template.Length+" 项自选属性。":
                    "切到自选需要已有勾选项。可先在单人设置勾选属性，再以该球员作为模板。"));
            note.Name="GrowthBatchExplanation";
            var problem=FieldLabel("");
            problem.Name="GrowthBatchProblem";
            problem.ForeColor=UiErrorInk;
            problem.Visible=false;
            var fields=new TableLayoutPanel
            {
                Name="GrowthBatchFields",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                ColumnCount=1,Padding=new Padding(0,0,4,0),TabIndex=0
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var rows=new Control[]{scope,FieldLabel("成长模式"),mode,FieldLabel("限制未勾选属性上涨"),limit,
                FieldLabel("整份名单成长倍率"),speed,note,problem};
            fields.RowCount=rows.Length;
            for(int row=0;row<rows.Length;row++)
            {
                var input=rows[row] as ComboBox;
                var rowStyle=new RowStyle(input==null?SizeType.AutoSize:SizeType.Absolute);
                fields.RowStyles.Add(rowStyle);
                fields.Controls.Add(rows[row],0,row);
                if(input==null)continue;

                // Native combo boxes can grow after the table has measured its row.
                EventHandler fit=delegate
                {
                    int height=Math.Max(input.Height,input.PreferredHeight)+input.Margin.Vertical;
                    if(rowStyle.Height!=height)rowStyle.Height=height;
                };
                input.FontChanged+=fit;
                input.SizeChanged+=fit;
                input.MarginChanged+=fit;
                input.HandleCreated+=fit;
                fit(input,EventArgs.Empty);
            }
            EventHandler wrap=delegate
            {
                foreach(var label in new[]{scope,note,problem})
                {
                    int width=fields.ClientSize.Width-fields.Padding.Horizontal-label.Margin.Horizontal;
                    if(width>0 && label.MaximumSize.Width!=width)label.MaximumSize=new Size(width,0);
                }
            };
            fields.ClientSizeChanged+=wrap;
            wrap(fields,EventArgs.Empty);
            var scroll=new Panel{Name="GrowthBatchScroll",Dock=DockStyle.Fill,AutoScroll=true,TabIndex=0};
            scroll.Controls.Add(fields);

            var apply=new Button{Name="ApplyGrowthBatchSettings",Text="应用到整份培养名单",Enabled=false};
            var footer=DialogActions(dialog,DialogDismiss("CancelGrowthBatchSettings","取消"),apply);
            footer.TabIndex=1;
            footer.MinimumSize=new Size(0,44);
            footer.AutoSize=true;
            footer.AutoSizeMode=AutoSizeMode.GrowAndShrink;
            dialog.Controls.Add(scroll);
            dialog.Controls.Add(footer);
            dialog.Load+=delegate{FitWindowToWorkArea(dialog,this);};

            EventHandler update=delegate
            {
                problem.Visible=false;
                apply.Enabled=queued.Count>0 && (mode.SelectedIndex>0 || limit.SelectedIndex>0 || speed.SelectedIndex>0);
            };
            mode.SelectedIndexChanged+=update;
            limit.SelectedIndexChanged+=update;
            speed.SelectedIndexChanged+=update;
            apply.Click+=delegate
            {
                string feedback;
                int uid;
                if(fm!=attached || growWatch.Count!=queued.Count ||
                    growWatch.Values.Any(t=>!queued.TryGetValue(t.P.Person,out uid) || uid!=t.P.Uid))
                    feedback="培养名单或连接已变化，请关闭窗口后重新核对名单。";
                else
                {
                    GrowthMode? requestedMode=mode.SelectedIndex<=0?(GrowthMode?)null:(GrowthMode)(mode.SelectedIndex-1);
                    bool? requestedLimit=limit.SelectedIndex<=0?(bool?)null:limit.SelectedIndex==1;
                    int wantedGear=speed.SelectedIndex<=0?GrowGear():speed.SelectedIndex+1;
                    if(ApplyGrowthBatchSettings(requestedMode,requestedLimit,wantedGear,template,out feedback))
                    {
                        ShowOperationFeedback(FeedbackKind.Success,feedback);
                        dialog.DialogResult=DialogResult.OK;
                        return;
                    }
                }
                problem.Text=feedback;
                problem.Visible=true;
                scroll.ScrollControlIntoView(problem);
            };
            return dialog;
        }

        bool ApplyGrowthBatchSettings(GrowthMode? mode,bool? limit,int gear,int[] template,out string feedback)
        {
            if(!CanSetGrowthBatch)
            {
                feedback="请先停止成长并确认培养名单中的球员。";
                return false;
            }
            var prepared=new List<GrowTarget>();
            var errors=new List<string>();
            int skipped=0,currentGear=GrowGear();
            foreach(var target in growWatch.Values)
            {
                var options=target.Options.Clone();
                if(mode.HasValue)options.Mode=mode.Value;
                if(mode==GrowthMode.SelectedAttributes && options.Attributes.Length==0)options.Attributes=(int[])template.Clone();
                bool setLimit=limit.HasValue && (!limit.Value || options.Mode==GrowthMode.SelectedAttributes);
                if(limit==true && !setLimit)skipped++;
                if(!mode.HasValue && !setLimit && gear==currentGear)continue;
                if(setLimit)options.LimitUnselected=limit.Value;
                var next=new GrowTarget{P=target.P,ScopeClub=target.ScopeClub,Options=options};
                if(!RosterPlayerIdentity.Matches(fm.Read,PlayerGuards(next.P)))
                    errors.Add(next.P.DisplayName+"：球员身份已变化，请刷新名单。");
                else if(!ResetGrowthTarget(next,gear))
                    errors.Add(next.P.DisplayName+"："+next.Note);
                else prepared.Add(next);
            }
            if(errors.Count>0)
            {
                feedback="整份名单设置尚未应用。"+Environment.NewLine+string.Join(Environment.NewLine,errors);
                return false;
            }
            if(prepared.Count==0)
            {
                feedback="培养名单中没有可应用本次选项的球员。开启上限需要自定义勾选属性方案。";
                return false;
            }
            foreach(var target in prepared)
            {
                growWatch[target.P.Person]=target;
                growthPreferences[target.P.Uid]=target.Options.Clone();
            }
            cmbGrowSpeed.SelectedIndex=gear-2;
            RefreshGrow();
            BindGrowthEditor();
            feedback="整份培养名单共 "+growWatch.Count+" 人，已更新 "+prepared.Count+" 人。名单速度为 "+gear+" 倍。";
            if(mode.HasValue)feedback+=Environment.NewLine+"成长模式为"+GrowthOptions.ModeNames[(int)mode.Value]+"。";
            if(limit.HasValue)feedback+=Environment.NewLine+(limit.Value?
                "已为自选属性方案开启上限。"+(skipped>0?" 其他模式 "+skipped+" 人未启用。":""):
                "已关闭整份培养名单的未勾选属性上限。");
            return true;
        }
    }
}
