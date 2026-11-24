using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class GrowthFocusTests
    {
        internal sealed class Fixture
        {
            public Dictionary<ulong,byte> Data = new Dictionary<ulong,byte>();
            public Player Player;
            public uint Date = (2026u << 16) | 200;
            public int Writes, FailAt;
            public Action<int> AfterWrite;
            public List<KeyValuePair<ulong,byte[]>> Guards;
            public Fixture(ulong extra = 0, int position = 12, short pa = 160)
            {
                Player = new Player { Person = 0x40000 + extra, Extra = extra, Uid = 123456, Name = "Offline Growth Fixture", Club = 0x70000 };
                Guards = new List<KeyValuePair<ulong,byte[]>> {
                    new KeyValuePair<ulong,byte[]>(Player.Person, BitConverter.GetBytes(0xABCDEF00UL)),
                    new KeyValuePair<ulong,byte[]>(Player.Person + FmLayout.Uid, BitConverter.GetBytes(Player.Uid)) };
                foreach (var pair in Guards) Put(pair.Key, pair.Value);
                var block = Enumerable.Repeat((byte)1, 15).Concat(Enumerable.Repeat((byte)50, 54)).ToArray(); block[position] = 20;
                Put(Player.Block, block); Put(Player.Tail - FmLayout.AbilityBeforePerson, BitConverter.GetBytes((short)100).Concat(BitConverter.GetBytes(pa)).ToArray());
                Put(Player.Tail - FmLayout.TeamBeforePerson, BitConverter.GetBytes(0x60000UL)); Put(0x60000 + FmLayout.TeamClub, BitConverter.GetBytes(Player.Club));
            }
            public void Put(ulong address, byte[] bytes) { for (int i = 0; i < bytes.Length; i++) Data[address + (ulong)i] = bytes[i]; }
            public byte[] Read(ulong address, int length)
            { return Enumerable.Range(0, length).All(i => Data.ContainsKey(address + (ulong)i)) ? Enumerable.Range(0, length).Select(i => Data[address + (ulong)i]).ToArray() : null; }
            public bool Write(ulong address, byte[] value)
            {
                Writes++; if (Writes == FailAt) return false;
                Put(address, value); if (AfterWrite != null) AfterWrite(Writes); return true;
            }
            public GrowthStep Capture() { return GrowthStep.Capture(Read, () => Date, Player, Guards); }
            public GrowthStep Plan(int desired = 110)
            {
                var frame = Capture(); var state = new GrowthFocusState(frame.Positions, frame.Attributes, frame.Ca, -1);
                frame.Plan(state, desired); return frame;
            }
            public bool Apply(GrowthStep frame, out string error)
            { return EligibilityTransaction.Apply(Read, Write, frame.Edits, () => frame.Matches(Read, () => Date), out error, () => frame.IdentityMatches(Read)); }
            public List<Change> Log(GrowthStep frame)
            {
                return frame.Edits.Select(e => new Change { Addr = e.Address, Old = (byte[])e.Before.Clone(), New = (byte[])e.After.Clone(),
                    Group = 12, Growth = true, Guard = () => frame.IdentityMatches(Read), What = e.Label }).ToList();
            }
            public string Digest() { return string.Join(";", Data.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value).ToArray()); }
        }
        static void Reject(Action action, Action<bool,string> check, string message)
        { bool rejected = false; try { action(); } catch { rejected = true; } check(rejected, message); }
        static byte[] Positions(int main)
        { var result = Enumerable.Repeat((byte)1, 15).ToArray(); result[main] = 20; return result; }
        static void CheckWingerRecommendation(Action<bool,string> check)
        {
            const int marking=5, targetCa=109;
            var fixture=new Fixture(0,9);
            ulong attributes=fixture.Player.Block+15;
            ulong ability=fixture.Player.Tail-FmLayout.AbilityBeforePerson;
            var values=Enumerable.Repeat((byte)56,54).ToArray();
            values[marking]=35;
            fixture.Put(attributes,values);
            fixture.Put(ability,BitConverter.GetBytes((short)103));
            var frame=fixture.Capture();
            var model=new PositionAbility(frame.Positions);
            var focus=new GrowthFocusState(frame.Positions,frame.Attributes,frame.Ca,-1);
            var candidate=focus.Allocate(frame.Attributes,targetCa,frame.Cap);
            check(model.Estimate(frame.Attributes)==103 && model.Estimate(candidate.After)>targetCa,
                "a winger with marking seven reproduces the relative budget exceeding estimated recommended CA");
            frame.Plan(focus,targetCa);
            check(frame.Allocation.RawAdded>0 && frame.Allocation.RawAdded<candidate.RawAdded &&
                model.Estimate(frame.Allocation.After)<=targetCa,
                "the directed write retains improvements within estimated recommended CA");
            check(frame.Allocation.After[marking]==35 &&
                frame.Allocation.After.Zip(values,(after,before)=>after>=before).All(preserved=>preserved),
                "fitting directed increases keeps marking and all existing attributes intact");
            string error;
            check(fixture.Apply(frame,out error),"apply the fitted winger plan to offline memory");
            frame=fixture.Capture();
            check(frame.Ca==targetCa && model.Estimate(frame.Attributes)<=frame.Ca && frame.Attributes[marking]==35,
                "read back the new CA and recommendation without a regression to marking");

            var overspent=Enumerable.Repeat((byte)62,54).ToArray();
            overspent[marking]=35;
            fixture.Put(attributes,overspent);
            frame=fixture.Capture();
            focus=new GrowthFocusState(frame.Positions,frame.Attributes,frame.Ca,-1);
            frame.Plan(focus,targetCa+1);
            check(model.Estimate(overspent)>targetCa+1 && frame.Edits.Count==1 &&
                frame.Edits[0].Address==ability && frame.Allocation.After.SequenceEqual(overspent),
                "when original attributes already exceed the next budget, CA may catch up without adding or taking away attributes");
        }

        static void CheckTieredRoleGrowth(Action<bool,string> check)
        {
            const int marking=5,acceleration=34,strength=36;
            var fixture=new Fixture(position:11,pa:200);
            var values=Enumerable.Repeat((byte)50,54).ToArray();
            values[marking]=67;
            values[acceleration]=15;
            values[strength]=20;
            fixture.Put(fixture.Player.Block+15,values);
            var model=new PositionAbility(Positions(11));
            int startingRca=model.Estimate(values);
            int startingCa=startingRca+8;
            ulong ability=fixture.Player.Tail-FmLayout.AbilityBeforePerson;
            fixture.Put(ability,BitConverter.GetBytes((short)startingCa));

            int winger=Array.FindIndex(GrowthProfile.All,p=>p.Group=="边锋" && p.Phase=="持球" && p.English=="Winger");
            var support=new GrowthSupportOptions{Enabled=true,BudgetPercent=40};
            support.Targets[acceleration]=12;
            support.Targets[strength]=8;
            var frame=fixture.Capture();
            var focus=new GrowthFocusState(frame.Positions,values,startingCa,winger,support:support,age:18);
            var blue=focus.RoleTiers.Members(AttributeTier.Core);
            var green=focus.RoleTiers.Members(AttributeTier.Supporting);
            var white=focus.RoleTiers.Members(AttributeTier.Foundation);
            check(focus.RoleTiers.TierOf(marking)==AttributeTier.Other && white.Contains(strength) &&
                focus.SupportItems.Any(item=>item.Index==acceleration) && focus.SupportItems.Any(item=>item.Index==strength),
                "the role fixture has protected background and support in both a higher and a lower tier");

            frame.Plan(focus,startingCa+6);
            var first=frame.Allocation;
            check(first.RawAdded>0 && blue.Any(i=>first.After[i]>values[i]) &&
                green.Concat(white).All(i=>first.After[i]==values[i]),
                "a low green attribute or foundation supplement cannot take the early blue budget");
            check(first.SupportAdded[acceleration]>0 && first.SupportAdded[strength]==0 &&
                first.SupportAdded[acceleration]<=first.After[acceleration]-values[acceleration] &&
                first.RawAdded==first.After.Zip(values,(after,before)=>after-before).Sum(),
                "overlapping blue growth and approved support account for the same acceleration increase once");
            string original=fixture.Digest(),error;
            check(fixture.Apply(frame,out error) && GrowthUndo.Apply(fixture.Read,fixture.Write,fixture.Log(frame),out error) &&
                fixture.Digest()==original,"an exact-RCA role step and its CA can be undone together");

            var natural=(byte[])values.Clone();
            foreach(int index in focus.RoleTiers.Members(AttributeTier.Other))natural[index]=100;
            check(model.Estimate(natural)>=startingRca+6 && focus.AllocateForGrowth(natural,startingCa+6,200).RawAdded==0,
                "natural increases in unrelated attributes consume the same cumulative RCA allowance");
            check(focus.AllocateForGrowth(values,startingCa,200).RawAdded==0,"an unchanged CA goal grants no role growth");

            bool trainedGreen=false,trainedWhite=false,advancedBlue=false,advancedGreen=false;
            foreach(int desired in new[]{startingCa+6,startingCa+16,135,150,165,180,200}.Where(ca=>ca>startingCa).Distinct().OrderBy(ca=>ca))
            {
                var step=fixture.Capture();
                step.Plan(focus,desired);
                var allocation=step.Allocation;
                int limit=Math.Min(desired,startingRca+desired-startingCa);
                bool grewGreen=green.Any(i=>allocation.After[i]>step.Attributes[i]);
                bool grewWhite=white.Any(i=>allocation.After[i]>step.Attributes[i]);
                check(allocation.RecommendationLimit==limit && allocation.RecommendationAfter==model.Estimate(allocation.After) &&
                    allocation.RecommendationAfter<=limit,"consecutive role steps use the original RCA plus cumulative CA at "+desired);
                check((!grewGreen || blue.All(i=>allocation.After[i]>=75)) &&
                    (!grewWhite || green.All(i=>allocation.After[i]>=70)), "later tiers wait for earlier base goals at "+desired);
                bool raisedBlueBeyondBase=blue.Any(i=>allocation.After[i]>Math.Max(75,(int)step.Attributes[i]));
                bool raisedGreenBeyondBase=green.Any(i=>allocation.After[i]>Math.Max(70,(int)step.Attributes[i]));
                check((!raisedBlueBeyondBase || green.All(i=>allocation.After[i]>=70) && white.All(i=>allocation.After[i]>=55)) &&
                    (!raisedGreenBeyondBase || blue.All(i=>allocation.After[i]>=85) && white.All(i=>allocation.After[i]>=55)),
                    "the second pass waits for the complete base and advances blue before green at "+desired);
                check(blue.All(i=>allocation.After[i]<=Math.Max(85,(int)values[i])) &&
                    green.All(i=>allocation.After[i]<=Math.Max(75,(int)values[i])),
                    "directed growth stops at the milestone instead of distributing redistribution-only surplus at "+desired);
                check(allocation.After[marking]==67 && allocation.After.Zip(step.Attributes,(after,before)=>after>=before).All(ok=>ok),
                    "incremental role growth preserves existing values, including a T4 value above ten");
                check(focus.AllocateForGrowth(allocation.After,desired,200).RawAdded==0,
                    "re-evaluating an applied tier budget at the same CA cannot grant it again");
                check(fixture.Apply(step,out error),"apply and read back the consecutive exact-RCA growth step at "+desired);
                trainedGreen|=grewGreen;trainedWhite|=grewWhite;
                advancedBlue|=raisedBlueBeyondBase;advancedGreen|=raisedGreenBeyondBase;
            }
            check(trainedGreen && trainedWhite,"successive CA increases unlock both supporting and foundation targets");
            check(advancedBlue && advancedGreen,"successive CA increases also unlock both milestone stages");

            using(var form=new MainForm(false,false))
            using(var dialog=form.BuildGrowthPreview(fixture.Player,frame,focus))
            {
                var rows=(DataGridView)dialog.Controls.Find("GrowthPrimaryPreview",true).Single();
                var preview=dialog.Controls.Find("GrowthPreviewBudget",true).Single();
                var goals=rows.Rows.Cast<DataGridViewRow>().Select(row=>(string)row.Cells[2].Value).ToArray();
                check(preview.Text.Contains("累计预算上限") && goals.Contains("11") &&
                    goals.Contains("15 → 17") && goals.Contains("14 → 15"),
                    "the role preview reports the shared RCA ceiling and shows the foundation targets");
            }
        }

        public static void Run(Action<bool,string> check)
        {
            CheckWingerRecommendation(check);
            CheckTieredRoleGrowth(check);
            var values = Enumerable.Repeat((byte)50, 54).ToArray();
            var striker = new GrowthFocusState(Positions(12), values, 100, -1);
            var defender = new GrowthFocusState(Positions(3), values, 100, -1);
            var keeper = new GrowthFocusState(Positions(0), values, 100, -1);
            check(striker.Profile.Name == "前锋" && defender.Profile.Name == "中后卫" && keeper.Profile.Name == "门将", "position-based profiles identify strikers, defenders and keepers");
            check(GrowthProfile.Resolve(Positions(5), -1).Name == "后腰" && GrowthProfile.Resolve(Positions(7), -1).Name == "中场" &&
                GrowthProfile.Resolve(Positions(10), -1).Name == "前腰" && GrowthProfile.Resolve(Positions(14), -1).Name == "翼卫", "midfield and wingback directions stay distinct");
            check(GrowthProfile.Resolve(Positions(3), 8).Name == "前锋", "explicit direction overrides automatic position selection");
            Reject(() => GrowthProfile.Resolve(Enumerable.Repeat((byte)1,15).ToArray(), -1), check, "unknown main position requires an explicit direction");
            check(GrowthProfile.Resolve(Enumerable.Repeat((byte)1,15).ToArray(), 8).Name == "前锋", "explicit direction supports low-familiarity fixtures");
            var forward = striker.Allocate(values, 110, 160); var back = defender.Allocate(values, 110, 160); var goalkeeper = keeper.Allocate(values, 110, 160);
            check(forward.After[2] > values[2] && forward.After[6] > values[6] && forward.After[52] > values[52], "striker gains prioritise finishing, off-ball movement and composure");
            check(back.After[5] > values[5] && back.After[9] > values[9] && back.After[20] > values[20], "defenders prioritise marking, tackling and positioning");
            check(goalkeeper.After[11] > values[11] && goalkeeper.After[21] > values[21] && goalkeeper.After[19] > values[19], "keepers prioritise handling, reflexes and one-on-ones");
            check(forward.After[11] == values[11] && goalkeeper.After[2] == values[2], "outfield and goalkeeper-only attributes cannot leak between profiles");
            check(striker.Profile.Core.Average(i => forward.After[i] - values[i]) > striker.Profile.Supporting.Average(i => forward.After[i] - values[i]), "key attributes have stronger priority than supporting ones");
            check(new[]{24,25,31,41,44,47,48,49}.All(i => forward.After[i] == values[i] && goalkeeper.After[i] == values[i]), "feet, eccentricity and all hidden attributes remain byte-identical");
            check(striker.Allocate(forward.After, 110, 160).RawAdded == 0, "the same CA budget cannot be spent again");
            var natural = Enumerable.Repeat((byte)55, 54).ToArray();
            check(striker.Allocate(natural, 110, 160).RawAdded == 0, "natural development consumes the existing growth budget");
            check(striker.Allocate(values,100,160).RawAdded == 0, "no CA increase grants no free attribute points");
            check(striker.Allocate(values,110,160).After.SequenceEqual(forward.After), "growth allocation is deterministic");
            Reject(() => striker.Allocate(values,161,160), check, "allocation cannot exceed PA");
            Reject(() => striker.Allocate(values,99,160), check, "allocation cannot run behind its baseline");
            Reject(() => striker.Allocate(values,201,201), check, "CA/PA above 200 is refused");
            var invalid = (byte[])values.Clone(); invalid[0] = 0;
            Reject(() => new GrowthFocusState(Positions(12),invalid,100,-1), check, "zero-valued attribute records are refused");
            var saturated = Enumerable.Repeat((byte)100,54).ToArray();
            check(new GrowthFocusState(Positions(12),saturated,190,-1).Allocate(saturated,200,200).RawAdded == 0, "saturated players receive no values above 20");
            var mutablePositions=Positions(12);var mutableValues=(byte[])values.Clone();var immutable=new GrowthFocusState(mutablePositions,mutableValues,100,-1);
            mutablePositions[12]=1;mutableValues[2]=1;
            check(immutable.SamePositions(Positions(12)) && immutable.Allocate(values,110,160).After.SequenceEqual(forward.After), "caller mutations cannot rewrite the growth baseline");
            var random = new Random(1729);
            for (int profile=0;profile<GrowthProfile.All.Length;profile++)
                for (int n=0;n<35;n++)
                {
                    var baseline=Enumerable.Range(0,54).Select(i=>(byte)random.Next(1,101)).ToArray();int ca=random.Next(40,191),cap=random.Next(ca,201);
                    var state=new GrowthFocusState(Positions(12),baseline,ca,profile);var result=state.Allocate(baseline,cap,cap);
                    check(result.After.All(v=>v>=1 && v<=100) && Enumerable.Range(0,54).All(i=>result.After[i]>=baseline[i]), "all profiles preserve bounds and never reduce attributes");
                    check(result.ScoreAfter<=result.Limit, "allocated proxy budget never exceeds the CA target");
                    check(Enumerable.Range(0,54).All(i=>state.Profile.Priority(i)>0 || result.After[i]==baseline[i]), "untargeted fields remain unchanged for varied players");
                    check(state.Allocate(result.After,cap,cap).RawAdded==0, "repeated budget evaluation is idempotent");
                }

            string error;var fixture=new Fixture();var snapshot=fixture.Plan();string original=fixture.Digest();
            check(snapshot.Edits.Count>1 && snapshot.Allocation.RawAdded>0, "production growth plan pairs CA with actual key-attribute edits");
            check(snapshot.Edits.First().Address==fixture.Player.Tail-FmLayout.AbilityBeforePerson, "CA is increased before attribute writes");
            check(fixture.Apply(snapshot,out error), "guarded production growth transaction applies to copied memory");
            check(BitConverter.ToInt16(fixture.Read(fixture.Player.Tail-FmLayout.AbilityBeforePerson,2),0)==110 && snapshot.Matches(fixture.Read,()=>fixture.Date), "readback accepts only the completed planned state");
            var log=fixture.Log(snapshot);
            check(GrowthUndo.Apply(fixture.Read,fixture.Write,log,out error) && fixture.Digest()==original && log.All(c=>c.Undone), "undo restores CA and all attributes as one complete group");

            var failure=new Fixture();var failurePlan=failure.Plan();string prior=failure.Digest();failure.FailAt=2;
            check(!failure.Apply(failurePlan,out error) && failure.Digest()==prior && error.Contains("已取消"), "attribute-write failure rolls back the paired CA increase");
            var drift=new Fixture();var driftPlan=drift.Plan();byte originalHidden=drift.Read(drift.Player.Block+15+41,1)[0];
            drift.AfterWrite=n=>{if(n==1)drift.Put(drift.Player.Block+15+41,new[]{(byte)(originalHidden+1)});};
            check(!drift.Apply(driftPlan,out error) && BitConverter.ToInt16(drift.Read(drift.Player.Tail-FmLayout.AbilityBeforePerson,2),0)==100, "unrelated concurrent drift cancels the allocation and recovers only owned CA");
            check(drift.Read(drift.Player.Block+15+41,1)[0]==originalHidden+1 && !error.Contains("无法恢复"), "rollback preserves an unrelated external change");
            var finalDrift=new Fixture();var finalPlan=finalDrift.Plan();int last=finalPlan.Edits.Count;
            finalDrift.AfterWrite=n=>{if(n==last)finalDrift.Date++;};
            check(!finalDrift.Apply(finalPlan,out error) && BitConverter.ToInt16(finalDrift.Read(finalDrift.Player.Tail-FmLayout.AbilityBeforePerson,2),0)==100, "a date change after the final write is detected and rolled back");

            foreach (string change in new[]{"uid","club","pa","date","position","otherattribute"})
            {
                var stale=new Fixture();var plan=stale.Plan();
                if(change=="uid")stale.Put(stale.Player.Person+FmLayout.Uid,BitConverter.GetBytes(543210));
                if(change=="club")stale.Put(0x60000+FmLayout.TeamClub,BitConverter.GetBytes(0x80000UL));
                if(change=="pa")stale.Put(stale.Player.Tail-FmLayout.AbilityBeforePerson+2,BitConverter.GetBytes((short)90));
                if(change=="date")stale.Date++;
                if(change=="position")stale.Put(stale.Player.Block+3,new byte[]{19});
                if(change=="otherattribute")stale.Put(stale.Player.Block+15+41,new byte[]{51});
                check(!stale.Apply(plan,out error) && stale.Writes==0,"stale growth preview refuses all writes: "+change);
            }
            var coach=new Fixture(0xA0);var coachPlan=coach.Plan();
            check(coachPlan.Edits.First().Address==coach.Player.Tail-FmLayout.AbilityBeforePerson && coach.Apply(coachPlan,out error), "player-coach growth uses the player tail rather than the person header");
            var negative=new Fixture(0,12,-95);var negativeFrame=negative.Capture();
            check(negativeFrame.Cap==190, "negative PA ranges retain the existing upper-cap convention");
            Reject(()=>new Fixture(0,12,0).Capture(),check,"invalid PA cannot create a growth baseline");
            var moved=new Fixture();var movedFrame=moved.Capture();var movedState=new GrowthFocusState(Positions(3),values,100,-1);
            Reject(()=>movedFrame.Plan(movedState,110),check,"changed positions invalidate the old growth direction");
            Reject(()=>movedFrame.Plan(striker,160+1),check,"production plan independently refuses above-PA CA");
            check(GrowthStep.DayOnly((2026u<<16)|200)==GrowthStep.DayOnly((2026u<<16)|0x600|200),"time-of-day bits do not trigger duplicate daily growth");
            Reject(()=>GrowthStep.DayOnly((2026u<<16)|366),check,"non-leap day 366 is not a valid growth clock");

            var undoStale=new Fixture();var undoFrame=undoStale.Plan();check(undoStale.Apply(undoFrame,out error),"prepare a real copied-data growth undo");
            var undoLog=undoStale.Log(undoFrame);var edited=undoFrame.Edits.Last();undoStale.Put(edited.Address,new[]{(byte)(edited.After[0]+1)});undoStale.Writes=0;
            check(!GrowthUndo.Apply(undoStale.Read,undoStale.Write,undoLog,out error) && undoStale.Writes==0 && undoLog.All(c=>!c.Undone),"one externally changed attribute refuses the entire undo without partial restoration");
            var undoFailure=new Fixture();var undoFailureFrame=undoFailure.Plan();check(undoFailure.Apply(undoFailureFrame,out error),"prepare undo failure fixture");
            var undoFailureLog=undoFailure.Log(undoFailureFrame);string appliedDigest=undoFailure.Digest();undoFailure.Writes=0;undoFailure.FailAt=2;
            check(!GrowthUndo.Apply(undoFailure.Read,undoFailure.Write,undoFailureLog,out error) && undoFailure.Digest()==appliedDigest && undoFailureLog.All(c=>!c.Undone),"failed group undo rolls back its own partial restoration");

            using(var form=new MainForm(false,false))
            {
                var mode=form.Controls.Find("GrowthFocusMode",true).OfType<ComboBox>().Single();var direction=form.Controls.Find("GrowthFocusProfile",true).OfType<ComboBox>().Single();
                var editor=form.Controls.Find("GrowthEditorPanel",true).OfType<GrowthEditorPanel>().Single();
                check(mode.SelectedIndex==0 && mode.Items.Count==3 && !mode.Enabled,"growth editor starts with no implicit player selection");
                editor.Bind(123,"Offline Fixture",new GrowthOptions(),Positions(12),false,false);
                check(direction.Items.Count>2 && editor.PositionGroup.Items.Count==11,"position and role choices are separate and include detailed presets");
                check(form.Controls.Find("GrowthPreviewButton",true).Length==1,"growth preview is accessible without applying data");
                mode.SelectedIndex=2;check(!direction.Enabled,"CA-only mode disables the irrelevant direction control");
                mode.SelectedIndex=0;check(direction.Enabled,"key-attribute mode restores direction selection");
            }
        }
    }
}
