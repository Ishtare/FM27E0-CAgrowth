using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class AttributeOptimizerTests
    {
        static void Reject(Action action,Action<bool,string> check,string message){bool failed=false;try{action();}catch{failed=true;}check(failed,message);}
        static void Click(Control button){button.GetType().GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});}
        static void PreserveToPotential(GrowthOptions roles,Action<bool,string> check)
        {
            var fixture=new GrowthFocusTests.Fixture();
            fixture.Put(fixture.Player.Block+17,new byte[]{76});
            var initial=fixture.Capture();
            int required=new PositionAbility(initial.Positions).Estimate(initial.Attributes);
            short from=(short)(required-5), pa=(short)(required+38);
            ulong ability=fixture.Player.Tail-FmLayout.AbilityBeforePerson;
            fixture.Put(ability,BitConverter.GetBytes(from).Concat(BitConverter.GetBytes(pa)).ToArray());
            var frame=fixture.Capture();
            var fixedCa=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve);
            check(!fixedCa.CanApply && fixedCa.Conflict.Contains("同时升到 PA"),"an over-budget baseline points to its viable PA growth option");
            var grown=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve,true);
            check(grown.CanApply && grown.TargetCa==pa && grown.Decreased==0 && grown.AfterRca<=pa &&
                grown.AfterRca>=pa-1 && grown.AfterRca>required,"the PA budget supports fitting gains without lowering an existing attribute");
            check(grown.After[2]>frame.Attributes[2] && grown.After[2]-frame.Attributes[2]>grown.After[5]-frame.Attributes[5],
                "an already strong role attribute still grows faster than a background attribute");
            check(new[]{8,24,25,27,30,35,49}.All(i=>grown.After[i]==frame.Attributes[i]),"PA growth preserves set pieces, feet and protected values");

            frame.PlanOptimization(grown);
            string original=fixture.Digest(), error;
            fixture.FailAt=2;
            check(!fixture.Apply(frame,out error) && fixture.Digest()==original,"a failed attribute write also rolls back the simultaneous CA increase");
            fixture.FailAt=0;
            check(fixture.Apply(frame,out error) && fixture.Capture().Ca==pa && fixture.Capture().RawPa==pa &&
                fixture.Capture().Attributes.SequenceEqual(grown.After),"CA and the reviewed new attributes apply together");
            check(GrowthUndo.Apply(fixture.Read,fixture.Write,fixture.Log(frame),out error) && fixture.Digest()==original,
                "one undo restores both CA and all attributes");

            fixture.Put(ability+2,BitConverter.GetBytes((short)required));
            frame=fixture.Capture();
            var exact=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve,true);
            frame.PlanOptimization(exact);
            check(exact.BeforeRca==required && exact.After.SequenceEqual(frame.Attributes) && exact.CanApply &&
                frame.Edits.Count==1 && frame.Edits[0].Address==ability,"matching PA already can be applied as a CA-only increase");
            fixture.Put(ability+2,BitConverter.GetBytes((short)(required-1)));
            frame=fixture.Capture();
            var blocked=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve,true);
            check(!blocked.CanApply && blocked.Decreased==0 && blocked.Remaining== -1,"attributes that already exceed PA cannot be retained and further grown");
            Reject(()=>frame.PlanOptimization(blocked),check,"an over-PA plan cannot prepare a write");

            fixture.Put(ability+2,BitConverter.GetBytes((short)-80));
            frame=fixture.Capture();
            check(!AttributeOptimizer.CanRaiseToPotential(frame),"a range-coded PA is not treated as a verified target");
            Reject(()=>AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve,true),check,"an unknown PA cannot be applied");
            fixture.Put(ability+2,BitConverter.GetBytes(from));
            frame=fixture.Capture();
            check(!AttributeOptimizer.CanRaiseToPotential(frame),"a player at PA cannot request an increase");
        }
        public static void Run(Action<bool,string> check)
        {
            int inside=Array.FindIndex(GrowthProfile.All,p=>p.Name=="肋部中锋" && p.Phase=="持球");
            int outside=Array.FindIndex(GrowthProfile.All,p=>p.Group=="中锋" && p.Phase=="无球");
            var ip=GrowthProfile.All[inside];var oop=GrowthProfile.All[outside];
            var combined=GrowthProfile.Combine(ip,oop,50);
            check(combined.Core.Intersect(ip.Core).Any() && combined.Core.Intersect(oop.Core).Any(),"both possession phases contribute their core requirements");
            foreach(int i in Enumerable.Range(0,54))check(combined.Priority(i)==ip.Priority(i)*50+oop.Priority(i)*50 && combined.Cost(i)==ip.Cost(i)*50+oop.Cost(i)*50,"combined priorities and costs reflect the chosen phase weights: "+i);
            check(object.ReferenceEquals(GrowthProfile.Combine(ip,oop,100),ip) && object.ReferenceEquals(GrowthProfile.Combine(ip,oop,0),oop),"phase endpoints reproduce the individual role exactly");
            check(object.ReferenceEquals(GrowthProfile.Combine(ip,ip,50),ip),"matching phase profiles do not double-charge a baseline");
            Reject(()=>GrowthProfile.Combine(ip,GrowthProfile.All[0],50),check,"keeper and outfield roles cannot be mixed into a fictitious profile");
            var fixture=new GrowthFocusTests.Fixture();fixture.Put(fixture.Player.Block+15+36,new byte[]{5});var frame=fixture.Capture();
            var roles=new GrowthOptions{Profile=inside,DefendingProfile=outside,PossessionWeight=50};
            var state=roles.CreateState(frame.Positions,frame.Attributes,frame.Ca,18);
            check(state.Profile.Phase=="双阶段" && state.TargetAttributes.Intersect(ip.Core).Any() && state.TargetAttributes.Intersect(oop.Core).Any(),"actual growth states consume the combined role rather than only the selected tab");
            var supplemental=roles.Clone();supplemental.Mode=GrowthMode.SelectedAttributes;supplemental.Attributes=new[]{34,38};supplemental.Support.Enabled=true;
            var supplementalState=supplemental.CreateState(frame.Positions,frame.Attributes,frame.Ca,18);var supplementalPlan=supplementalState.Allocate(frame.Attributes,110,160);
            check(supplementalPlan.After[36]>frame.Attributes[36] && supplementalPlan.After[34]>frame.Attributes[34] && supplementalPlan.After[38]>frame.Attributes[38],"dual-phase scaling supports custom speed growth plus low-strength support together");
            check(supplementalPlan.ScoreAfter<=supplementalPlan.Limit && supplementalState.Allocate(supplementalPlan.After,110,160).RawAdded==0,"combined role budgets remain bounded and cannot be spent twice");
            var clone=roles.Clone();roles.DefendingProfile=-2;roles.PossessionWeight=90;
            check(clone.DefendingProfile==outside && clone.PossessionWeight==50,"per-player cloning preserves both role selections and the weight independently");roles=clone;
            var retained=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve);
            check(retained.Decreased==0 && retained.Increased>0 && retained.AfterRca<=frame.Ca && retained.BeforeRca<frame.Ca,"preserve spends actual headroom at fixed CA without lowering existing attributes");
            var redistributed=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Redistribute);
            check(redistributed.SourceCa==frame.Ca && redistributed.Increased>0 && redistributed.Decreased>0,"independent optimization can reallocate attributes immediately without changing current CA");
            check(redistributed.AfterRca<=frame.Ca && redistributed.After[36]>5,"redistribution stays inside its declared budget and improves a severe basic weakness");
            check(Enumerable.Range(0,54).All(i=>AttributeOptimizer.Editable(redistributed.Profile,i) || redistributed.After[i]==frame.Attributes[i]),"feet, hidden, style and irrelevant goalkeeping fields are protected");
            check(AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Redistribute).After.SequenceEqual(redistributed.After),"same input produces a stable optimizer result");
            var oneSide=roles.Clone();oneSide.PossessionWeight=100;var attackOnly=AttributeOptimizer.Create(frame,oneSide,AttributeOptimizationMode.Redistribute);
            oneSide.PossessionWeight=0;var defendOnly=AttributeOptimizer.Create(frame,oneSide,AttributeOptimizationMode.Redistribute);
            check(!attackOnly.After.SequenceEqual(defendOnly.After),"changing phase emphasis changes the actual allocated attributes");
            var caOnly=roles.Clone();caOnly.Mode=GrowthMode.CaOnly;caOnly.Attributes=new[]{34,38};
            check(AttributeOptimizer.Create(frame,caOnly,AttributeOptimizationMode.Redistribute).After.SequenceEqual(redistributed.After),"one-click optimization is independent of growth mode and custom growth checkboxes");
            Reject(()=>AttributeOptimizer.Create(frame,roles,(AttributeOptimizationMode)5),check,"unknown optimization modes are rejected");
            var tainted=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Redistribute);tainted.After[49]--;
            Reject(()=>frame.PlanOptimization(tainted),check,"an injected hidden-attribute modification cannot enter a production optimization plan");
            frame=fixture.Capture();frame.PlanOptimization(redistributed);
            check(frame.Edits.Count>0 && frame.Edits.All(e=>e.Address!=fixture.Player.Tail-FmLayout.AbilityBeforePerson),"fixed-CA production plans contain no CA or PA write");
            check(fixture.Writes==0,"generating and previewing an optimizer plan does not write memory");
            string error;var before=fixture.Digest();check(fixture.Apply(frame,out error),"optimizer plan applies through the guarded atomic transaction");
            check(GrowthUndo.Apply(fixture.Read,fixture.Write,fixture.Log(frame),out error) && fixture.Digest()==before,"one grouped undo restores all redistributed attributes");
            fixture=new GrowthFocusTests.Fixture();frame=fixture.Capture();var increasing=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve);frame.PlanOptimization(increasing);
            check(frame.Edits.Count>0 && frame.Edits.All(e=>e.Address!=fixture.Player.Tail-FmLayout.AbilityBeforePerson && e.Address!=fixture.Player.Tail-FmLayout.AbilityBeforePerson+2),"preserve mode leaves both CA and PA outside the write set");
            before=fixture.Digest();fixture.FailAt=3;check(!fixture.Apply(frame,out error) && fixture.Digest()==before,"a failed optimizer transaction rolls back all attribute changes together");
            fixture=new GrowthFocusTests.Fixture();frame=fixture.Capture();frame.PlanOptimization(AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Redistribute));fixture.Date++;
            check(!fixture.Apply(frame,out error) && fixture.Writes==0,"a stale game date invalidates the optimizer before any write");
            fixture=new GrowthFocusTests.Fixture();frame=fixture.Capture();frame.PlanOptimization(AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Redistribute));fixture.Put(fixture.Player.Block,new byte[]{20});
            check(!fixture.Apply(frame,out error) && fixture.Writes==0,"changing actual position data invalidates a prepared optimizer plan");
            var random=new Random(11801);
            for(int sample=0;sample<35;sample++)
            {
                fixture=new GrowthFocusTests.Fixture(position:sample%2==0?12:3);var attributes=Enumerable.Range(0,54).Select(i=>(byte)random.Next(5,96)).ToArray();fixture.Put(fixture.Player.Block+15,attributes);fixture.Put(fixture.Player.Tail-FmLayout.AbilityBeforePerson,BitConverter.GetBytes((short)(100+sample%60)));frame=fixture.Capture();
                var options=new GrowthOptions{PossessionWeight=sample%101};
                foreach(var mode in new[]{AttributeOptimizationMode.Preserve,AttributeOptimizationMode.Redistribute})
                {
                    var result=AttributeOptimizer.Create(frame,options,mode);if(result.Conflict==null) AttributeOptimizer.Validate(result); else Reject(()=>AttributeOptimizer.Validate(result),check,"an infeasible preview cannot be applied");
                    check((result.Conflict==null ? result.AfterRca<=frame.Ca : result.After.SequenceEqual(result.Before)) && result.After.All(v=>v>=1 && v<=100),"random distributions fit the position budget or report the original conflict");
                    check(Enumerable.Range(0,54).All(i=>AttributeOptimizer.Editable(result.Profile,i) || result.Before[i]==result.After[i]),"randomized optimization never touches protected fields");
                    if(mode==AttributeOptimizationMode.Preserve)check(result.Decreased==0,"randomized preserve mode never lowers a field");
                }
            }
            using(var selector=new DualRoleSelector("Test"))
            {
                int primary=-10,secondary=-10,weight=-1;selector.Changed+=(a,b,c)=>{primary=a;secondary=b;weight=c;};
                var positions=Enumerable.Repeat((byte)1,15).ToArray();positions[12]=20;selector.Bind(roles,positions,true);
                check(selector.InRole.Items.Cast<object>().All(o=>{int index=(int)o.GetType().GetField("Index").GetValue(o);return index<0 || GrowthProfile.All[index].Phase!="无球";}),"possession role dropdown excludes out-of-possession roles");
                check(selector.OutRole.Items.Cast<object>().All(o=>{int index=(int)o.GetType().GetField("Index").GetValue(o);return index<0 || GrowthProfile.All[index].Phase!="持球";}),"out-of-possession dropdown excludes possession roles");
                selector.Weight.Value=70;check(primary==inside && secondary==outside && weight==70,"editing one phase weight preserves both independent role selections");
                selector.OutPosition.SelectedIndex=0;check(secondary==-2 && !selector.OutRole.Enabled,"single-phase fallback is explicit and disables irrelevant fields");
            }
            fixture=new GrowthFocusTests.Fixture();frame=fixture.Capture();
            using(var dialog=new AttributeOptimizationDialog(fixture.Player,frame,roles))
            {
                check(dialog.SelectedMode==AttributeOptimizationMode.Preserve && dialog.Plan!=null && dialog.Plan.Decreased==0 && dialog.Plan.SourceCa==frame.Ca,"optimizer opens at current CA with a preserve-only preview");
                var mode=(ComboBox)dialog.Controls.Find("OptimizationMode",true).Single();
                mode.SelectedIndex=(int)AttributeOptimizationMode.Redistribute;
                check(dialog.Plan.Decreased>0,"redistribution requires an explicit choice and previews decreases");
                mode.SelectedIndex=(int)AttributeOptimizationMode.Preserve;
                check(dialog.Plan.Decreased==0 && dialog.Plan.CanApply,"preserve preview uses only measured headroom");
                var usePa=(CheckBox)dialog.Controls.Find("OptimizationUsePa",true).Single();
                check(usePa.Enabled && !usePa.Checked && dialog.Plan.TargetCa==frame.Ca,"increasing to PA is opt-in");
                usePa.Checked=true;
                check(dialog.Plan.TargetCa==frame.RawPa && dialog.Plan.Decreased==0 &&
                    dialog.Plan.AfterRca<=frame.RawPa,"checking PA refreshes the preserve-only plan on screen");
                var displayed=dialog.Plan;
                var filter=(CheckBox)dialog.Controls.Find("OptimizationChangesOnly",true).Single();
                var comparison=(DataGridView)dialog.Controls.Find("OptimizationComparison",true).Single();
                filter.Checked=false;
                check(object.ReferenceEquals(displayed,dialog.Plan) && comparison.Rows.Count==54,
                    "showing unchanged attributes uses the same reviewed plan");
                filter.Checked=true;
                check(object.ReferenceEquals(displayed,dialog.Plan) && comparison.Rows.Count==displayed.Increased,
                    "the comparison filter only changes displayed rows");
                bool requested=false;dialog.ApplyRequested+=plan=>{requested=plan.SourceCa==frame.Ca && plan.TargetCa==frame.RawPa &&
                    plan.AfterRca<=frame.RawPa;return true;};Click(dialog.Controls.Find("ApplyAttributeOptimization",true).Single());
                check(requested && fixture.Writes==0,"dialog routes the reviewed plan to its caller and never writes on its own");
            }
            PreserveToPotential(roles,check);
            var deadBall=new[]{8,27,30,35};
            foreach(int field in deadBall)fixture.Put(fixture.Player.Block+15+(ulong)field,new byte[]{5});
            frame=fixture.Capture();
            foreach(var mode in new[]{AttributeOptimizationMode.Preserve,AttributeOptimizationMode.Redistribute})
            {
                var optimization=AttributeOptimizer.Create(frame,roles,mode);
                check(deadBall.All(i=>optimization.Before[i]==optimization.After[i]),"automatic optimization reserves all four set-piece fields");
            }
            var support=new GrowthSupportOptions{Enabled=true};
            foreach(int field in deadBall)support.Targets[field]=10;
            var option=new GrowthOptions{Support=support};
            var automatic=option.CreateState(frame.Positions,frame.Attributes,frame.Ca,18);
            var growth=automatic.Allocate(frame.Attributes,frame.Cap,frame.Cap);
            check(deadBall.All(i=>growth.After[i]==frame.Attributes[i] && !automatic.TargetAttributes.Contains(i) && !automatic.SupportItems.Any(item=>item.Index==i)),"role growth and low-value support both leave set pieces at their current values");
            fixture.Put(fixture.Player.Block+15+27,new byte[]{30});
            frame=fixture.Capture();
            option.Mode=GrowthMode.SelectedAttributes;option.Attributes=new[]{27};
            var custom=option.CreateState(frame.Positions,frame.Attributes,frame.Ca,18);
            var selective=custom.Allocate(frame.Attributes,frame.Cap,frame.Cap);
            check(selective.After[27]>frame.Attributes[27] && deadBall.Where(i=>i!=27).All(i=>selective.After[i]==frame.Attributes[i]),"an explicitly checked set piece can still grow");
            using(var form=new MainForm(false,false))
                check(form.Controls.Find("PlayerOptimizeAttributes",true).Length==1 && form.Controls.Find("GrowthOptimizeAttributes",true).Length==1,"optimizer is accessible independently from player details and from the growth page");
        }
    }
}
