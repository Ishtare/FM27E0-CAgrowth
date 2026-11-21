
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class GrowthLimitTests
    {
        const int Acceleration=34, Pace=38, Passing=7, Strength=36;

        static GrowthOptions Options(bool limit=true)
        {
            return new GrowthOptions
            {
                Mode=GrowthMode.SelectedAttributes, Attributes=new[]{Acceleration,Pace}, LimitUnselected=limit
            };
        }

        static void RunLimits(Action<bool,string> check)
        {
            var fixture=new GrowthFocusTests.Fixture();
            ulong attributes=fixture.Player.Block+15;
            fixture.Put(attributes+Strength,new byte[]{5});
            fixture.Put(attributes+Pace,new byte[]{5});
            var original=fixture.Capture();
            var options=Options();
            options.Support.Enabled=true;
            options.Support.Targets[Strength]=6;
            options.Support.Targets[Pace]=6;
            var state=options.CreateState(original.Positions,original.Attributes,original.Ca,19);

            check(!new GrowthOptions().LimitUnselected && options.Clone().LimitsUnselected &&
                !Options(false).CreateState(original.Positions,original.Attributes,original.Ca).HasLimits,
                "the unselected cap is opt-in and is carried by each player's options");
            check(state.LimitedAttributes.Contains(Passing) && state.LimitedAttributes.Contains(Strength) &&
                new[]{11,24,25,31,41,44,47,48,49,Acceleration,Pace}.All(i=>!state.LimitedAttributes.Contains(i)),
                "the cap is confined to applicable unchecked attributes");
            check(state.SupportItems.Select(i=>i.Index).SequenceEqual(new[]{Pace}),
                "an approved but unchecked low value is filtered out before allocation");
            var role=options.Clone();role.Mode=GrowthMode.Position;
            check(!role.CreateState(original.Positions,original.Attributes,original.Ca,19).HasLimits,
                "setting a cap in a custom scheme does not enable it for a role scheme");

            fixture.Put(attributes+Passing,new byte[]{57});
            fixture.Put(attributes+Acceleration,new byte[]{54});
            fixture.Put(attributes+24,new byte[]{62});
            var frame=fixture.Capture();
            frame.PlanAttributeLimits(state);
            check(frame.Edits.Count==1 && frame.Edits[0].Address==attributes+Passing &&
                frame.Edits[0].After[0]==50 && frame.Edits[0].Label.Contains("未勾选属性上限"),
                "a cap-only transaction changes just the excess value with its own label");
            string error;
            check(fixture.Apply(frame,out error) && fixture.Capture().Ca==100 &&
                fixture.Capture().Attributes[Passing]==50 &&
                fixture.Capture().Attributes[Acceleration]==54 && fixture.Capture().Attributes[24]==62,
                "cap-only transactions preserve CA, the selected fields and the protected bytes");
            check(GrowthUndo.Apply(fixture.Read,fixture.Write,fixture.Log(frame),out error) &&
                fixture.Capture().Attributes[Passing]==57,"one undo restores the state before the correction");

            fixture.Put(attributes+Passing,new byte[]{43});
            frame=fixture.Capture();
            frame.PlanAttributeLimits(state);
            check(frame.Edits.Count==0,"values below the original cap are left alone");
            fixture.Put(attributes+Passing,new byte[]{50});
            frame=fixture.Capture();
            frame.PlanAttributeLimits(state);
            check(frame.Edits.Count==0,"recovering up to the original cap is permitted");

            fixture.Put(attributes+Passing,new byte[]{56});
            frame=fixture.Capture();
            frame.Plan(state,130,true);
            check(frame.Edits.Count>2 && frame.Edits.Any(e=>e.Address==attributes+Passing) &&
                frame.Edits.All(e=>e.Address!=attributes+Strength),
                "scheduled growth integrates the cap and gives unchecked low values no support");
            string before=fixture.Digest();
            fixture.FailAt=fixture.Writes+2;
            check(!fixture.Apply(frame,out error) && fixture.Digest()==before,
                "a failed combined cap and scheduled growth rolls back CA and attributes");
            fixture.FailAt=0;
            check(fixture.Apply(frame,out error) && fixture.Capture().Attributes[Passing]==50 && fixture.Capture().Ca==130,
                "a successful day applies the selected gains and the correction together");
            check(GrowthUndo.Apply(fixture.Read,fixture.Write,fixture.Log(frame),out error) && fixture.Digest()==before,
                "the combined change can be undone as one group");

            frame=fixture.Capture();frame.PlanAttributeLimits(state);
            fixture.Date++;
            int writes=fixture.Writes;
            check(!fixture.Apply(frame,out error) && fixture.Writes==writes,"an expired game date cannot apply old caps");
            frame=fixture.Capture();frame.PlanAttributeLimits(state);
            fixture.Put(fixture.Player.Person+FmLayout.Uid,BitConverter.GetBytes(fixture.Player.Uid+1));
            check(!fixture.Apply(frame,out error) && fixture.Writes==writes,"a different live UID cannot receive the previous player's cap");
            fixture.Put(fixture.Player.Person+FmLayout.Uid,BitConverter.GetBytes(fixture.Player.Uid));
            fixture.Put(fixture.Player.Tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)160));
            frame=fixture.Capture();frame.PlanAttributeLimits(state);
            check(frame.Edits.Count==0,"a player at PA no longer has corrections applied");
        }

        static void RunEditor(Action<bool,string> check)
        {
            var fixture=new GrowthFocusTests.Fixture();
            var frame=fixture.Capture();
            using(var editor=new GrowthEditorPanel())
            {
                editor.Bind(fixture.Player.Uid,fixture.Player.Name,new GrowthOptions(),frame.Positions,true,false,frame.Attributes,frame.Ca,19);
                check(!editor.LimitUnselected.Enabled && !editor.LimitUnselected.Checked,"role mode shows an inactive limit switch");
                editor.Mode.SelectedIndex=(int)GrowthMode.SelectedAttributes;
                check(editor.Value.Attributes.Length==0 && editor.LimitUnselected.Enabled,"first manual custom choice starts with no inherited checkmarks");
                editor.SetCheckedAttributes(Pace);
                editor.LimitUnselected.Checked=true;
                check(editor.Value.LimitsUnselected && editor.Value.Attributes.SequenceEqual(new[]{Pace}),
                    "turning on the cap keeps the requested selection intact");
                editor.Bind(fixture.Player.Uid,fixture.Player.Name,editor.Value,frame.Positions,true,true,frame.Attributes,frame.Ca,19);
                check(editor.LimitUnselected.Checked && !editor.LimitUnselected.Enabled,"the active baseline cannot be changed during the run");
            }
            using(var form=new MainForm(false,false))
            {
                var state=Options().CreateState(frame.Positions,frame.Attributes,frame.Ca,19);
                using(var preview=form.BuildGrowthPreview(fixture.Player,frame,state))
                {
                    var grid=(DataGridView)preview.Controls.Find("GrowthLimitsPreview",true).Single();
                    check(grid.Rows.Count==state.LimitedAttributes.Count() &&
                        grid.Rows.Cast<DataGridViewRow>().Any(row=>Convert.ToString(row.Cells[0].Value)==Zh.Attr(FmLayout.Attrs[Passing])),
                        "the preview shows the exact guarded fields and opening ceilings");
                }
            }
        }

        static void RunTransactionChecks(Action<bool,string> check)
        {
            const ulong a=0x22000,b=0x22010;
            var bytes=new Dictionary<ulong,byte>{{a,10},{b,20}};
            var plan=new[]{
                new EligibilityEdit{Address=a,Before=new byte[]{10},After=new byte[]{11}},
                new EligibilityEdit{Address=b,Before=new byte[]{20},After=new byte[]{21}}
            };
            Func<ulong,int,byte[]> read=(at,n)=>new[]{bytes[at]};
            Func<ulong,byte[],bool> writer=(at,value)=>
            {
                bytes[at]=value[0];
                if(at==b && value[0]==21)bytes[a]=10;
                return true;
            };
            string error;
            check(!EligibilityTransaction.Apply(read,writer,plan,()=>true,out error) && bytes[a]==10 && bytes[b]==20 &&
                error.Contains("写入结束"),"final readback catches a previously written field reverting before the transaction ends");

            bool recovery=false;
            read=(at,n)=>
            {
                if(at==b && recovery)throw new IOException("Cannot read just this field during rollback.");
                return new[]{bytes[at]};
            };
            writer=(at,value)=>
            {
                if(at==b){recovery=true;return false;}
                bytes[at]=value[0];return true;
            };
            check(!EligibilityTransaction.Apply(read,writer,plan,()=>true,out error) && bytes[a]==10 &&
                error.Contains("部分数据无法恢复"),"rollback checks the remaining fields even when one read throws");
        }

        internal static void Run(Action<bool,string> check)
        {
            RunLimits(check);
            RunEditor(check);
            RunTransactionChecks(check);
        }
    }

    partial class MainForm
    {
        void CheckGrowthLimitRuntime(Action<bool,string> check,Club club,uint today,Action<uint> setDate)
        {
            growWatch.Clear();
            growthPreferences.Clear();
            var limited=GrowthPlayer(100);
            var regular=GrowthPlayer(101);
            var unselected=GrowthPlayer(102);
            const int passing=7;
            Func<Player,byte> value=p=>fm.Read(p.Block+15+passing,1)[0];
            Action<Player,byte> set=(p,v)=>fm.Write(p.Block+15+passing,new[]{v});
            foreach(var player in new[]{limited,regular})
            {
                var settings=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{38},LimitUnselected=player==limited};
                growWatch.Add(player.Person,new GrowTarget{P=player,Options=settings,ScopeClub=club.Addr});
                growthPreferences.Add(player.Uid,settings.Clone());
            }

            // Changing one plan must not re-read another visible player's displayed CA.
            var otherRow=gridGrow.Rows.Cast<DataGridViewRow>().Single(r=>((Player)r.Tag).Uid==regular.Uid);
            int shownCa=Convert.ToInt32(otherRow.Cells[3].Value);
            fm.Write(regular.Tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)(shownCa+1)));
            SaveGrowthOptions(limited.Uid,growthPreferences[limited.Uid]);
            check(Convert.ToInt32(otherRow.Cells[3].Value)==shownCa,"editing one scheme does not refresh other visible players' ability rows");
            fm.Write(regular.Tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)100));

            autoMonthKey=MonthKey(today);
            ToggleGrow();
            check(growOn && growthLimitTargets.Length==1,"the normal Start command arms only the opted-in player");
            int firstChange=changes.Count;
            foreach(var player in new[]{limited,regular,unselected})set(player,61);
            GrowTick();
            check(value(limited)==50 && value(regular)==61 && value(unselected)==61 &&
                changes.Count==firstChange+1 && changes.Last().What.Contains("未勾选属性上限"),
                "a same-day tick corrects only the explicitly limited player and logs the changed field");
            StopGrow(null);
            Undo(new List<Change>{changes.Last()});
            check(value(limited)==61 && changes.Last().Undone,"the normal Undo command reverses a cap correction after stopping growth");
            GrowTick();
            check(value(limited)==61 && growthLimitTargets.Length==0,"the scheduled check cannot write after stopping");

            ToggleGrow();
            check(growthLimitTargets.Length==1,"starting a second run uses a fresh snapshot");
            set(limited,56);
            GrowTick();
            check(value(limited)==56,"a decline below the new upper limit is retained");
            set(limited,61);
            GrowTick();
            check(value(limited)==61,"natural recovery to the starting value is retained");
            var previousFocus=growWatch[limited.Person].Focus;
            RefreshLocalClub(club);
            PerformanceMaintenanceTests.PumpUntil(()=>!rosterRefreshing);
            limited=GrowthPlayer(limited.Uid);
            check(growWatch[limited.Person].Focus==previousFocus && growthLimitTargets.Length==1,
                "normal roster refresh rebinds the player without replacing the original limits");
            set(limited,70);
            GrowTick();
            check(value(limited)==61,"excess after a normal roster reload is still capped to the start of the same run");
            set(limited,70);
            fm.Write(limited.Tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)160));
            GrowTick();
            check(value(limited)==70,"a same-day natural rise to PA releases the limits");
            setDate(today-1);
            GrowTick();
            check(!growOn && growthLimitTargets.Length==0 && value(limited)==70,"a date rewind clears the active cap without restoring a stale value");
            setDate(today);
            foreach(var player in new[]{limited,regular,unselected})
            {
                set(player,50);
                fm.Write(player.Tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)100));
            }

            growWatch.Clear();
            growthPreferences.Clear();
            foreach(var player in players)
            {
                var setting=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{38},LimitUnselected=true};
                growWatch.Add(player.Person,new GrowTarget{P=player,Options=setting,ScopeClub=club.Addr});
            }
            ToggleGrow();
            foreach(var player in players)set(player,60);
            GrowTick();
            int repaired=players.Count(p=>value(p)==50);
            check(repaired>0 && repaired<=32 && repaired<players.Count,"a large same-day cap pass yields after a bounded number of players");
            for(int i=0;i<players.Count && players.Any(p=>value(p)>50);i++)GrowTick();
            check(players.All(p=>value(p)==50),"round-robin cap checks eventually visit the entire queue");
            StopGrow(null);
            growWatch.Clear();
            growthPreferences.Clear();
            RefreshGrow();
        }
    }
}
