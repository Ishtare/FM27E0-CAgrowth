// Modified build v1.1.23, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        readonly Dictionary<int,GrowthOptions> growthPreferences = new Dictionary<int,GrowthOptions>();
        readonly GrowthPreferenceStore savedGrowthPreferences;
        readonly GrowthEditorPanel growEditor = new GrowthEditorPanel();

        GrowthOptions OptionsFor(Player player)
        {
            GrowthOptions value;
            if(player!=null && growthPreferences.TryGetValue(player.Uid,out value))return value.Clone();
            return (savedGrowthPreferences==null?null:savedGrowthPreferences.Find(player))??
                new GrowthOptions{Mode=GrowthMode.CaOnly};
        }
        void LoadGrowthPreferences()
        {
            if(savedGrowthPreferences==null)return;
            try{savedGrowthPreferences.Load();}
            catch(Exception ex){LogLine("个人培养配置无法读取："+ex.Message);}
        }
        void PersistGrowthPreferences(IEnumerable<Player> selected)
        {
            if(savedGrowthPreferences==null)return;
            try{savedGrowthPreferences.Save(selected,growthPreferences);}
            catch(Exception ex){LogLine("个人培养修改已在本次会话生效，无法保存到本地："+ex.Message);}
        }
        Player GrowthPlayer(int uid)
        {
            return RosterIndex.Unique(uid);
        }
        void ConnectGrowthEditor(GrowthEditorPanel editor)
        {
            editor.OptionsChanged+=SaveGrowthOptions;
            editor.WatchRequested+=uid=>
            {
                ToggleGrowthPlayer(uid);
                if(editor==growEditor)BindGrowthEditor();
                else if(showGrowthQueue && !growWatch.Values.Any(t=>t.P.Uid==uid))
                {
                    var dialog=editor.FindForm();
                    if(dialog!=null && dialog!=this)dialog.Close();
                }
                else BindGrowthEditor(editor,GrowthPlayer(uid));
            };
            editor.ApplyCheckedRequested+=ApplyCheckedGrowthOptions;
            editor.PreviewRequested+=PreviewGrowthFocus;
        }
        void SaveGrowthOptions(int uid,GrowthOptions options)
        {
            if(growOn || uid<=0)return;
            var player=GrowthPlayer(uid);
            if(player==null)return;
            GrowTarget target;
            var preferred=options.Clone();
            if(growWatch.TryGetValue(player.Person,out target))
            {
                target.Options=preferred;
                ResetGrowthTarget(target);
                preferred=target.Options.Clone();
            }
            growthPreferences[uid]=preferred;
            PersistGrowthPreferences(new[]{player});
            RefreshGrowthOptionRow(player,target);
        }
        void ToggleGrowthPlayer(int uid)
        {
            if(growOn || fm==null)return;
            var player=GrowthPlayer(uid);if(player==null)return;
            var selected=growWatch.Values.FirstOrDefault(t=>t.P.Uid==uid);
            if(selected==null)WatchPlayer(player);else growWatch.Remove(selected.P.Person);
            LoadGrow();
        }
        void ApplyCheckedGrowthOptions(int uid,GrowthOptions options)
        {
            if(growOn || fm==null)return;
            if(growWatch.Count==0){MessageBox.Show("先勾选需要培养的球员。","应用培养设置");return;}
            var errors=new List<string>();
            var prepared=new Dictionary<int,GrowthOptions>();
            foreach(var target in growWatch.Values)
                try
                {
                    var individual=options.Clone();
                    if(options.UsesAttributes)
                    {
                        var frame=GrowthStep.Capture(fm.Read,fm.ReadGameDate,target.P,PlayerGuards(target.P));int? age=GrowthSupportPlanner.Age(target.P.Born,Birthdays.Decode(frame.Day));
                        if(target.P.Uid!=uid)individual=options.ForAnotherPlayer(frame.Positions,frame.Attributes,frame.Ca,age);
                        individual.CreateState(frame.Positions,frame.Attributes,frame.Ca,age);
                    }
                    individual.CaGrowthMultiplierOverride=target.Options.CaGrowthMultiplierOverride;
                    prepared[target.P.Uid]=individual;
                }
                catch(Exception ex){errors.Add(target.P.DisplayName+"："+ex.Message);}
            if(errors.Count>0)
            {
                const string message="设置尚未应用，请先处理：";
                ShowOperationFeedback(FeedbackKind.Failed,message+Environment.NewLine+string.Join(Environment.NewLine,errors));
                MessageBox.Show(message+"\n"+string.Join("\n",errors.Take(8)),"培养设置不适用");
                return;
            }
            foreach(var target in growWatch.Values)
            {target.Options=prepared[target.P.Uid];ResetGrowthTarget(target);growthPreferences[target.P.Uid]=target.Options.Clone();}
            PersistGrowthPreferences(growWatch.Values.Select(target=>target.P));
            RefreshGrow();BindGrowthEditor();
            ShowOperationFeedback(FeedbackKind.Success,"培养方案已应用到整份名单的 "+growWatch.Count+" 人。各球员保留原倍率。开始成长后才写入游戏。");
        }
        void BindGrowthEditor()
        {
            if(growContent==null || growContent.Panel2Collapsed)return;
            var player=gridGrow.CurrentRow==null?null:gridGrow.CurrentRow.Tag as Player;
            BindGrowthEditor(growEditor,player);
        }
        void BindGrowthEditor(GrowthEditorPanel editor,Player player)
        {
            if(player==null){editor.Bind(0,null,new GrowthOptions(),null,false,growOn,globalMultiplier:GrowGear());return;}
            GrowTarget target;growWatch.TryGetValue(player.Person,out target);
            byte[] positions=null,attributes=null;
            int ca=0;
            if(fm!=null && Safety.On("core"))
            {
                if(Safety.On("ability"))
                {
                    var block=fm.Read(player.Block,69);
                    if(block!=null && block.Length==69)
                    {
                        positions=block.Take(15).ToArray();
                        var values=block.Skip(15).ToArray();
                        var ability=fm.Read(player.Tail-FmLayout.AbilityBeforePerson,4);
                        if(ability!=null && ability.Length==4 && values.All(v=>v>=1 && v<=100))
                        { ca=BitConverter.ToInt16(ability,0);attributes=values; }
                    }
                }
                if(positions==null)positions=fm.Read(player.Block,15);
            }
            editor.Bind(player.Uid,player.DisplayName,target==null?OptionsFor(player):target.Options,
                positions,target!=null,growOn,attributes,ca,GrowthSupportPlanner.Age(player.Born,GrowthToday()),GrowGear());
        }
        void OpenGrowthEditor()
        {
            var player=gridGrow.CurrentRow==null?null:gridGrow.CurrentRow.Tag as Player;
            if(player==null){MessageBox.Show("先在名单中选择球员。","球员培养设置");return;}
            try{player=RefreshSelectedPlayer(player);}
            catch(Exception ex){MessageBox.Show(ex.Message,"球员培养设置");return;}
            if(!growContent.Panel2Collapsed){BindGrowthEditor(growEditor,player);growEditor.Focus();return;}
            using(var editor=new GrowthEditorPanel{Dock=DockStyle.Fill})
            using(var dialog=BuildGrowthSettingsDialog(editor))
            {
                ConnectGrowthEditor(editor);BindGrowthEditor(editor,player);
                dialog.ShowDialog(this);
            }
            BindGrowthEditor();
        }

        internal Form BuildGrowthSettingsDialog(GrowthEditorPanel editor)
        {
            var dialog=new Form{Name="GrowthSettingsDialog",Text="球员培养设置",Font=Font,
                AutoScaleMode=AutoScaleMode.Dpi,Width=430,Height=820,MinimumSize=new Size(400,360),
                StartPosition=FormStartPosition.CenterParent};
            var close=new Button{Name="CloseGrowthSettings",Text="完成",Dock=DockStyle.Bottom,DialogResult=DialogResult.OK};
            StyleButton(close,WorkspaceButtonRole.Primary);
            dialog.Controls.Add(editor);dialog.Controls.Add(close);
            dialog.AcceptButton=close;
            dialog.Load+=delegate{FitWindowToWorkArea(dialog,this);};
            return dialog;
        }
        bool ResetGrowthTarget(GrowTarget target,int? gear=null)
        {
            if(target.Options==null)target.Options=OptionsFor(target.P);
            target.Focus=null;target.UseFocus=target.Options.UsesAttributes;target.AttributeRawAdded=0;
            target.Elapsed=0;target.Added=0;target.Done=false;target.Note="";
            try
            {
                if(fm==null || !Safety.On("ability"))throw new InvalidOperationException("CA/PA 尚未通过检查。");
                if(target.UseFocus)
                {
                    if(!Safety.On("core") || !Safety.On("date"))throw new InvalidOperationException("属性或游戏日期尚未通过检查。");
                    var frame=GrowthStep.Capture(fm.Read,fm.ReadGameDate,target.P,PlayerGuards(target.P));
                    target.StartCa=frame.Ca;target.Cap=frame.Cap;
                    target.Focus=target.Options.CreateState(frame.Positions,frame.Attributes,frame.Ca,GrowthSupportPlanner.Age(target.P.Born,Birthdays.Decode(frame.Day)));
                    foreach(var item in target.Focus.SupportItems)target.Options.Support.Targets[item.Index]=item.Target;
                }
                else
                {
                    var value=fm.Read(target.P.Tail-FmLayout.AbilityBeforePerson,4);
                    if(value==null || value.Length!=4)throw new InvalidOperationException("CA/PA 无法读取。");
                    target.StartCa=BitConverter.ToInt16(value,0);target.Cap=PaTop(BitConverter.ToInt16(value,2));
                    if(target.StartCa<1 || target.StartCa>200 || target.Cap<1 || target.Cap>200)throw new InvalidOperationException("CA/PA 超出范围。");
                }
                target.Span=GrowSpanFor(target.StartCa,target.Cap,target.Options.ResolveMultiplier(gear??GrowGear()));
                target.Done=target.StartCa>=target.Cap;target.Note=target.Done?"已在 PA":"";return true;
            }
            catch(Exception ex){target.Done=true;target.Note="已停止："+ex.Message;return false;}
        }
        void RefreshGrowthDirection(DataGridViewRow row,GrowTarget target)
        {
            if(gridGrow.Columns.Count<=9)return;
            var player=row.Tag as Player;var options=target==null?OptionsFor(player):target.Options;
            SetGrowthCell(row,9,(options.Mode==GrowthMode.Position && target!=null && target.Focus!=null?target.Focus.Profile.Name:options.Summary)+
                (options.LimitsUnselected?" + 上限":"")+(options.UsesAttributes && options.Support.Enabled?" + 补强":""));
            string chosen=options.Mode==GrowthMode.SelectedAttributes?"仅同步："+string.Join("、",options.Attributes.Where(i=>i>=0 && i<54).Select(i=>Zh.Attr(FmLayout.Attrs[i])).ToArray()):"";
            row.Cells[9].ToolTipText=chosen+(target==null?"":"\n本轮同步增量合计 "+(target.AttributeRawAdded/5.0).ToString("0.0")+" 属性点，不等同于 CA 点数。");
            SetGrowthCell(row,growMultiplierColumn.Index,options.MultiplierLabel(GrowGear()));
        }
        bool ApplyFocusGrowth(GrowTarget target,byte[] expectedAbility,int desiredCa,bool enforceLimits,out string error)
        {
            error = null;
            try
            {
                if (target.Focus == null) throw new InvalidOperationException("请停止后重新开始培养，以建立属性基准。");
                var attached = fm;
                var frame = GrowthStep.Capture(attached.Read, attached.ReadGameDate, target.P, PlayerGuards(target.P));
                if (expectedAbility == null || expectedAbility.Length != 4 ||
                    frame.Ca != BitConverter.ToInt16(expectedAbility, 0) || frame.RawPa != BitConverter.ToInt16(expectedAbility, 2))
                    throw new InvalidOperationException("CA/PA 在读取期间发生变化，本次已停止。");
                frame.Plan(target.Focus,desiredCa,enforceLimits);
                if(!CommitGrowthStep(attached,frame,out error))return false;
                target.AttributeRawAdded += frame.Allocation.RawAdded;
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
        bool ApplyCaOnlyGrowth(Player player,List<KeyValuePair<ulong,byte[]>> guards,byte[] ability,int wanted,out string error)
        {
            var attached=fm;
            ulong? save=scannedSaveLoads;
            ulong address=player.Tail-FmLayout.AbilityBeforePerson;
            var before=ability.Take(2).ToArray();
            var expectedPa=ability.Skip(2).ToArray();
            var edit=new EligibilityEdit{Address=address,Before=before,After=BitConverter.GetBytes((short)wanted),
                Label=player.DisplayName+"：CA 成长 "+BitConverter.ToInt16(before,0)+" → "+wanted};
            Func<bool> identity=()=>fm==attached && Safety.On("ability") &&
                (!save.HasValue || SaveLoadCount(attached)==save) && RosterPlayerIdentity.Matches(attached.Read,guards);
            Func<bool> current=()=>
            {
                if(!identity())return false;
                var pa=attached.Read(address+2,2);
                return pa!=null && pa.SequenceEqual(expectedPa);
            };
            return CommitGrowthEdits(attached,new[]{edit},current,identity,out error);
        }

        bool CommitGrowthStep(FmProcess attached,GrowthStep frame,out string error)
        {
            ulong? save=scannedSaveLoads;
            Func<bool> currentConnection=()=>fm==attached && Safety.On("core") && Safety.On("ability") &&
                (!save.HasValue || SaveLoadCount(attached)==save);
            Func<bool> guard=()=>currentConnection() && Safety.On("date") && frame.Matches(attached.Read,attached.ReadGameDate);
            Func<bool> identity=()=>currentConnection() && frame.IdentityMatches(attached.Read);
            return CommitGrowthEdits(attached,frame.Edits,guard,identity,out error);
        }

        bool CommitGrowthEdits(FmProcess attached,IList<EligibilityEdit> edits,Func<bool> guard,Func<bool> identity,out string error)
        {
            error=null;
            if(edits.Count==0)return true;
            if(!EligibilityTransaction.Apply(attached.Read,attached.Write,edits,guard,out error,identity))
            {
                error=(error??"成长写入失败。").Replace("注册资格","成长").Replace("注册","成长");
                return false;
            }
            RecordGrowthChanges(edits,identity);
            return true;
        }
        void RecordGrowthChanges(IList<EligibilityEdit> edits,Func<bool> undoGuard)
        {
            RecordGroupedChanges(edits, undoGuard, null);
        }
        void PreviewGrowthFocus(int uid,GrowthOptions options)
        {
            var player=GrowthPlayer(uid);
            if(player==null || fm==null){MessageBox.Show("先连接游戏并选择球员。","成长预览");return;}
            if(!options.UsesAttributes){MessageBox.Show("当前为仅加速成长模式。修改器只推进 CA，属性由 FM 系统分配。","成长预览");return;}
            try
            {
                if(!Safety.On("ability") || !Safety.On("date"))throw new InvalidOperationException("CA/PA 或日期尚未通过检查。");
                player=RefreshSelectedPlayer(player);
                var frame=GrowthStep.Capture(fm.Read,fm.ReadGameDate,player,PlayerGuards(player));
                GrowTarget target;growWatch.TryGetValue(player.Person,out target);
                var focus=growOn && target!=null && target.Focus!=null?target.Focus:options.CreateState(frame.Positions,frame.Attributes,frame.Ca,GrowthSupportPlanner.Age(player.Born,Birthdays.Decode(frame.Day)));
                using(var dialog=BuildGrowthPreview(player,frame,focus))dialog.ShowDialog(this);
            }
            catch(Exception ex){MessageBox.Show(ex.Message,"成长预览不可用");}
        }
    }
}
