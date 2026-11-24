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
            // This budget reaches the second pass after the supporting and foundation base.
            short from=(short)(required-5), pa=(short)(required+60);
            ulong ability=fixture.Player.Tail-FmLayout.AbilityBeforePerson;
            fixture.Put(ability,BitConverter.GetBytes(from).Concat(BitConverter.GetBytes(pa)).ToArray());
            var frame=fixture.Capture();
            var fixedCa=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve);
            check(!fixedCa.CanApply && fixedCa.Conflict.Contains("同时升到 PA"),"an over-budget baseline points to its viable PA growth option");
            var grown=AttributeOptimizer.Create(frame,roles,AttributeOptimizationMode.Preserve,true);
            check(grown.CanApply && grown.TargetCa==pa && grown.Decreased==0 && grown.AfterRca<=pa &&
                grown.AfterRca>=pa-1 && grown.AfterRca>required,"the PA budget supports fitting gains without lowering an existing attribute");
            const int finishing=2,longShots=4,marking=5;
            check(grown.After[finishing]>frame.Attributes[finishing] && grown.After[longShots]==frame.Attributes[longShots] &&
                grown.Tiers.TierOf(longShots)==AttributeTier.Other && grown.Tiers.TierOf(marking)==AttributeTier.Supporting &&
                grown.After[marking]>=70 && grown.Tiers.Members(AttributeTier.Foundation).All(i=>grown.After[i]>=55),
                "an already strong role attribute grows in the second pass after the base, and genuine background fields stay intact");
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

        static void CheckTieredRedistribution(Action<bool,string> check)
        {
            string[] names={"Inside Forward","Winger","Ball-Playing Centre-Back"};
            int[] slots={11,11,3};
            for(int role=0;role<names.Length;role++)
            {
                string english=names[role];
                int profile=Array.FindIndex(GrowthProfile.All,p=>p.English==english && p.Phase=="持球" &&
                    p.Group==(role==2?"中后卫":"边锋"));
                var options=new GrowthOptions{Profile=profile,DefendingProfile=-2};
                var original=Enumerable.Repeat((byte)50,54).ToArray();
                original[24]=100;
                original[26]=original[43]=original[45]=75;
                foreach(int ca in new[]{135,150,165,180})
                {
                    var positions=Enumerable.Repeat((byte)1,15).ToArray();
                    positions[slots[role]]=20;
                    var frame=new GrowthStep{Ca=ca,Cap=200,RawPa=200,Positions=positions,Attributes=original};
                    var plan=AttributeOptimizer.Create(frame,options,AttributeOptimizationMode.Redistribute);
                    var blue=plan.Tiers.Members(AttributeTier.Core);
                    var green=plan.Tiers.Members(AttributeTier.Supporting);
                    var white=plan.Tiers.Members(AttributeTier.Foundation);
                    var other=plan.Tiers.Members(AttributeTier.Other);
                    string context=english+" at CA "+ca;
                    check(plan.Conflict==null && plan.AfterRca<=ca && plan.AfterRca>=ca-1,context+" uses the real position budget");
                    check(blue.All(i=>plan.After[i]>=60) && green.All(i=>plan.After[i]>=60) &&
                        white.All(i=>plan.After[i]>=40) && other.All(i=>plan.After[i]>=30),context+" keeps the normal tier floors");
                    check((!white.Any(i=>plan.After[i]<55) || other.All(i=>plan.After[i]==30)) &&
                        (!green.Any(i=>plan.After[i]<70) || white.All(i=>plan.After[i]==40) && other.All(i=>plan.After[i]==30)) &&
                        (!blue.Any(i=>plan.After[i]<75) || green.All(i=>plan.After[i]==60)),context+" downgrades the lower tiers first");
                    check(other.All(i=>plan.After[i]<=50) && green.All(i=>plan.After[i]<=85) &&
                        (!green.Any(i=>plan.After[i]>75) || blue.All(i=>plan.After[i]==100)),context+" spends surplus on blue before green");
                    if(ca==180)
                    {
                        // This wide role exhausts the extra budget on pace and acceleration first.
                        bool blueSurplus=english=="Winger"?
                            blue.OrderBy(i=>i).Select(i=>plan.After[i]).SequenceEqual(new byte[]{86,86,85,86,100,99,85}):
                            blue.All(i=>plan.After[i]>85);
                        check(white.All(i=>plan.After[i]==55) && other.All(i=>plan.After[i]==50) &&
                            blueSurplus && green.All(i=>plan.After[i]>=75),context+" reaches all goals and gives surplus to blue");
                    }
                    check(Enumerable.Range(0,54).Where(i=>plan.Tiers.TierOf(i)==AttributeTier.Protected).All(i=>plan.After[i]==original[i]),
                        context+" preserves cost-bearing locked fields, both feet, and set pieces");
                }

                var alt=(byte[])original.Clone();
                alt[25]=100;
                var alternatePositions=Enumerable.Repeat((byte)1,15).ToArray();
                alternatePositions[slots[role]]=alternatePositions[12]=20;
                var alternate=new GrowthStep{Ca=165,Cap=200,RawPa=200,Positions=alternatePositions,Attributes=alt};
                var fitted=AttributeOptimizer.Create(alternate,options,AttributeOptimizationMode.Redistribute);
                check(fitted.Conflict==null && fitted.AfterRca<=165 && fitted.AfterRca==new PositionAbility(alternatePositions).Estimate(fitted.After)
                    && fitted.After[24]==100 && fitted.After[25]==100,english+" accounts for two fully developed feet and a natural striker position");
            }

            var lowPositions=Enumerable.Repeat((byte)1,15).ToArray();
            lowPositions[11]=20;
            var low=new GrowthStep{Ca=60,Cap=160,RawPa=160,Positions=lowPositions,Attributes=Enumerable.Repeat((byte)50,54).ToArray()};
            var lowOptions=new GrowthOptions{DefendingProfile=-2};
            var lowPlan=AttributeOptimizer.Create(low,lowOptions,AttributeOptimizationMode.Redistribute);
            var floors=(byte[])low.Attributes.Clone();
            var minimums=new byte[]{60,60,40,30};
            for(int tier=0;tier<minimums.Length;tier++)
                foreach(int i in lowPlan.Tiers.Members((AttributeTier)tier))floors[i]=minimums[tier];
            int required=new PositionAbility(lowPositions).Estimate(floors);
            check(required>60 && lowPlan.Conflict!=null && !lowPlan.CanApply && lowPlan.After.SequenceEqual(low.Attributes) &&
                lowPlan.Conflict.Contains("仍需 "+required+" 点 RCA") &&
                lowPlan.Conflict.Contains("超出预算 "+(required-low.Ca)+" 点"),
                "when all tier floors exceed the budget, the blocked preview reports the exact shortfall and preserves the original");
            Reject(()=>low.PlanOptimization(lowPlan),check,"the fixed-floor conflict cannot prepare any writes");
            check(low.Edits.Count==0,"a blocked plan has no edits");
            low.Ca=required-1;
            var shortByOne=AttributeOptimizer.Create(low,lowOptions,AttributeOptimizationMode.Redistribute);
            check(!shortByOne.CanApply && shortByOne.Conflict.Contains("超出预算 1 点"),"one RCA point below the firm floors still blocks");
            low.Ca=required;
            var exactFloor=AttributeOptimizer.Create(low,lowOptions,AttributeOptimizationMode.Redistribute);
            check(exactFloor.CanApply && exactFloor.AfterRca==required &&
                Enumerable.Range(0,54).All(i=>exactFloor.After[i]>=floors[i]),
                "the exact floor cost can be applied after all lower-priority options have been tried");
        }

        static byte[] NaturalPosition(string group)
        {
            var slots=new[]{0,3,2,13,5,7,10,6,9,12};
            var positions=Enumerable.Repeat((byte)1,15).ToArray();
            positions[slots[Array.IndexOf(GrowthProfile.Groups,group)]]=20;
            return positions;
        }

        static byte[] WithTiers(byte[] original,RoleAttributeTiers tiers,params byte[] goals)
        {
            var values=(byte[])original.Clone();
            for(int tier=0;tier<goals.Length;tier++)
                foreach(int i in tiers.Members((AttributeTier)tier))values[i]=goals[tier];
            return values;
        }

        static void CheckEntireCatalog(Action<bool,string> check)
        {
            var targets=new[]{75,70,55,50};
            var floors=new[]{60,60,40,30};
            check(Enumerable.Range(0,4).All(i=>RoleAttributeTiers.Target((AttributeTier)i)==targets[i] &&
                RoleAttributeTiers.Floor((AttributeTier)i)==floors[i]),
                "the four approved baseline targets and floors are expressed in raw attribute steps");
            var original=Enumerable.Repeat((byte)50,54).ToArray();
            original[25]=100;
            for(int index=0;index<GrowthProfile.All.Length;index++)
            {
                var profile=GrowthProfile.All[index];
                if(profile.Phase=="基本")continue;
                var positions=NaturalPosition(profile.Group);
                var model=new PositionAbility(positions);
                var opts=new GrowthOptions{Profile=index,DefendingProfile=-2};
                foreach(int ca in new[]{135,150,165,180})
                {
                    var frame=new GrowthStep{Ca=ca,Cap=200,RawPa=200,Positions=positions,Attributes=original};
                    var plan=AttributeOptimizer.Create(frame,opts,AttributeOptimizationMode.Redistribute);
                    var values=plan.After;
                    var tiers=plan.Tiers;
                    var blue=tiers.Members(AttributeTier.Core);
                    var green=tiers.Members(AttributeTier.Supporting);
                    var white=tiers.Members(AttributeTier.Foundation);
                    var other=tiers.Members(AttributeTier.Other);
                    string context=profile.Group+" "+profile.English+" "+profile.Phase+" at CA "+ca;
                    check(plan.CanApply && plan.AfterRca==model.Estimate(values) && plan.AfterRca<=ca,
                        context+" fits the actual RCA budget");
                    check(Enumerable.Range(0,54).All(i=>tiers.TierOf(i)==AttributeTier.Protected?
                            values[i]==original[i]:values[i]>=floors[(int)tiers.TierOf(i)]) &&
                        white.All(i=>values[i]<=55) && other.All(i=>values[i]<=50) && green.All(i=>values[i]<=85),
                        context+" respects every floor, protection, and non-blue surplus cap");

                    if(model.Estimate(WithTiers(original,tiers,75,70,55,50))<=ca)
                    {
                        check(blue.All(i=>values[i]>=75) && green.All(i=>values[i]>=70) &&
                            white.All(i=>values[i]==55) && other.All(i=>values[i]==50),
                            context+" keeps an affordable base in all four tiers");
                        check((!green.Any(i=>values[i]>70) || blue.All(i=>values[i]>=85)) &&
                            (!blue.Any(i=>values[i]>85) || green.All(i=>values[i]>=75)) &&
                            (!green.Any(i=>values[i]>75) || blue.All(i=>values[i]==100)),
                            context+" advances in the order blue 17, green 15, blue 20, green 17");
                        check(model.Estimate(WithTiers(original,tiers,85,75,55,50))>ca ||
                            blue.All(i=>values[i]>=85) && green.All(i=>values[i]>=75),
                            context+" reaches both milestones whenever the position budget supports them");
                    }
                    else
                    {
                        check((!white.Any(i=>values[i]<55) || other.All(i=>values[i]==30)) &&
                            (!green.Any(i=>values[i]<70) || white.All(i=>values[i]==40) && other.All(i=>values[i]==30)) &&
                            (!blue.Any(i=>values[i]<75) || green.All(i=>values[i]==60)) &&
                            blue.All(i=>values[i]<=75) && green.All(i=>values[i]<=70),
                            context+" exhausts each lower tier before downgrading the next");
                    }
                    AttributeOptimizer.Validate(plan);
                }
            }
        }

        static void CheckWithinTierPriority(Action<bool,string> check)
        {
            var groups=new[]{"门将","中后卫","边后卫","中场","边锋","中锋"};
            var roles=new[]{"Ball-Playing Goalkeeper","Ball-Playing Centre-Back","Wing-Back","Channel Midfielder","Winger","Channel Forward"};
            for(int n=0;n<groups.Length;n++)
            {
                var preset=GrowthProfile.All.Single(p=>p.Group==groups[n] && p.Phase=="持球" && p.English==roles[n]);
                var model=new PositionAbility(NaturalPosition(preset.Group));
                var tiers=new RoleAttributeTiers(preset,model);
                var ordinary=new GrowthProfile(preset.Name,preset.Core,preset.Supporting,preset.Group,preset.Phase,preset.English);
                var unweighted=new RoleAttributeTiers(ordinary,model);
                var blue=tiers.Members(AttributeTier.Core);
                var preferred=blue.Where(i=>preset.WithinTierPriority(i)>0).ToArray();
                check(preferred.Length>0 && preferred.Length<blue.Length && blue.Take(preferred.Length).SequenceEqual(preferred) &&
                    Enumerable.Range(0,54).All(i=>tiers.TierOf(i)==unweighted.TierOf(i)),
                    groups[n]+" only changes order within eligible tiers");

                var neutral=Enumerable.Repeat((byte)50,54).ToArray();
                neutral[25]=100;
                var milestones=WithTiers(neutral,tiers,85,75,55,50);
                var fullyPreferred=(byte[])milestones.Clone();
                foreach(int i in preferred)fullyPreferred[i]=100;
                int limit=model.Estimate(fullyPreferred);
                var ordinaryResult=(byte[])milestones.Clone();
                unweighted.RaiseSurplus(ordinaryResult,new AttributeBudget(model,limit));
                tiers.RaiseSurplus(milestones,new AttributeBudget(model,limit));
                check(preferred.All(i=>milestones[i]==100) && preferred.Any(i=>ordinaryResult[i]<milestones[i]) &&
                    blue.Except(preferred).All(i=>milestones[i]>=85) && blue.Any(i=>milestones[i]<100) &&
                    tiers.Members(AttributeTier.Supporting).All(i=>milestones[i]==75) && model.Estimate(milestones)<=limit,
                    groups[n]+" favors the designated blue attributes only after blue 17 and green 15");
            }

            var attack=new GrowthProfile("Inside",new[]{0},new[]{34},"边锋",withinTierPriority:new[]{34});
            var defence=new GrowthProfile("Outside",new[]{34},new[]{0},"边锋",withinTierPriority:new[]{0,34});
            var both=GrowthProfile.Combine(attack,defence,70);
            check(both.Core.OrderBy(i=>i).SequenceEqual(new[]{0,34}) &&
                both.WithinTierPriority(34)==30 && both.WithinTierPriority(0)==0,
                "a lower phase tier contributes no explicit priority to a promoted blue attribute");

            var centered=new GrowthProfile("Single step",new[]{2,34},new int[0],"中锋",withinTierPriority:new[]{34});
            var price=new PositionAbility(NaturalPosition("中锋"));
            var order=new RoleAttributeTiers(centered,price).Members(AttributeTier.Core);
            var raw=Enumerable.Repeat((byte)55,54).ToArray();
            raw[25]=100;
            raw[52]=43;
            var finishing=(byte[])raw.Clone();finishing[2]++;
            var acceleration=(byte[])raw.Clone();acceleration[34]++;
            var bothSteps=(byte[])acceleration.Clone();bothSteps[2]++;
            check(price.Estimate(raw)==97 && price.Estimate(finishing)==97 && price.Estimate(acceleration)==97 &&
                price.Estimate(bothSteps)==98,"the marginal budget can afford either 0.2 step but cannot afford both");
            var budget=new AttributeBudget(price,97);
            var numericOrder=(byte[])raw.Clone();
            budget.Raise(numericOrder,new[]{2,34},56);
            budget.Raise(raw,order,56);
            check(numericOrder.SequenceEqual(finishing) && raw.SequenceEqual(acceleration),
                "the same budget goes to the explicitly ranked peer instead of the lower attribute index");
        }

        static void CheckPreserveMilestones(Action<bool,string> check)
        {
            int profile=Array.FindIndex(GrowthProfile.All,p=>p.Group=="边锋" && p.Phase=="持球" && p.English=="Winger");
            var options=new GrowthOptions{Profile=profile,DefendingProfile=-2};
            var positions=NaturalPosition("边锋");
            var model=new PositionAbility(positions);
            var tiers=new RoleAttributeTiers(GrowthProfile.All[profile],model);
            var original=WithTiers(Enumerable.Repeat((byte)50,54).ToArray(),tiers,65,60,50,65);
            original[25]=100;
            var milestones=WithTiers(original,tiers,85,75,55);
            var frame=new GrowthStep{Ca=model.Estimate(original),RawPa=200,Cap=200,Positions=positions,Attributes=original};
            var plan=AttributeOptimizer.Create(frame,options,AttributeOptimizationMode.Preserve,true);
            var focus=options.CreateState(positions,original,frame.Ca);
            var grown=focus.AllocateForGrowth(original,200,200);
            check(model.Estimate(milestones)<200 && plan.CanApply && plan.After.SequenceEqual(milestones) &&
                grown.After.SequenceEqual(milestones) && plan.Remaining>0,
                "preserve and directed growth use both passes and leave surplus beyond the milestones unspent");
        }

        public static void Run(Action<bool,string> check)
        {
            CheckTieredRedistribution(check);
            CheckEntireCatalog(check);
            CheckWithinTierPriority(check);
            CheckPreserveMilestones(check);
            int inside=Array.FindIndex(GrowthProfile.All,p=>p.Name=="肋部中锋" && p.Phase=="持球");
            int outside=Array.FindIndex(GrowthProfile.All,p=>p.Group=="中锋" && p.Phase=="无球");
            var ip=GrowthProfile.All[inside];var oop=GrowthProfile.All[outside];
            var combined=GrowthProfile.Combine(ip,oop,50);
            check(combined.Core.Intersect(ip.Core).Any() && combined.Core.Intersect(oop.Core).Any(),"both possession phases contribute their core requirements");
            foreach(int i in Enumerable.Range(0,54))check(combined.Priority(i)==ip.Priority(i)*50+oop.Priority(i)*50 && combined.Cost(i)==ip.Cost(i)*50+oop.Cost(i)*50,"combined priorities and costs reflect the chosen phase weights: "+i);
            check(object.ReferenceEquals(GrowthProfile.Combine(ip,oop,100),ip) && object.ReferenceEquals(GrowthProfile.Combine(ip,oop,0),oop),"phase endpoints reproduce the individual role exactly");
            check(object.ReferenceEquals(GrowthProfile.Combine(ip,ip,50),ip),"matching phase profiles do not double-charge a baseline");
            var dormant=new GrowthOptions{Profile=int.MaxValue,DefendingProfile=outside,PossessionWeight=0};
            var knownPositions=Enumerable.Repeat((byte)1,15).ToArray();
            knownPositions[12]=20;
            check(object.ReferenceEquals(dormant.ResolveProfile(knownPositions),oop),"a zero-weight in-possession selection is ignored");
            dormant.Profile=inside;dormant.DefendingProfile=int.MaxValue;dormant.PossessionWeight=100;
            check(object.ReferenceEquals(dormant.ResolveProfile(knownPositions),ip),"a zero-weight out-of-possession selection is ignored");
            var highAttack=new RoleAttributeTiers(GrowthProfile.Combine(ip,oop,99),new PositionAbility(knownPositions));
            var highDefend=new RoleAttributeTiers(GrowthProfile.Combine(ip,oop,1),new PositionAbility(knownPositions));
            check(Enumerable.Range(0,54).All(i=>highAttack.TierOf(i)==highDefend.TierOf(i)) &&
                highAttack.Members(AttributeTier.Core).Intersect(oop.Core.Except(ip.Core)).Any(),
                "phase weights only rank attributes within a tier, including the minority phase's core items");
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
                var misses=(TextBox)dialog.Controls.Find("OptimizationUnmetHighlights",true).Single();
                var details=(TextBox)dialog.Controls.Find("OptimizationTierDetails",true).Single();
                check(misses.Text.Contains("蓝档") && misses.Text.Contains("绿档") && misses.Text.Contains("未达标") &&
                    details.Text.Contains("达标") && details.Text.Contains("预计") && details.Text.Contains("被压低项") && details.Text.Contains("保留项"),
                    "the preview reports unmet role targets and tier outcomes independently of the changes-only grid");
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
