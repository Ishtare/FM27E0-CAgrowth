using System;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class PositionAbilityTests
    {
        // Independent reference results from FM27 Scout 7fb1217, ca.ts, with the final
        // display ceiling removed. Generation rule below uses synthetic input only.
        static readonly int[,] Reference = {
            {1,22,113,168,182,228,1,51,85,127,208,276,1,23,118,164,190,228,1,61,84,126,214,262},
            {1,28,99,146,188,235,1,41,83,118,192,245,1,30,87,143,198,214,2,48,75,128,199,240},
            {1,23,84,141,191,222,3,44,76,132,198,242,1,26,91,144,183,223,2,36,80,135,185,249},
            {1,32,85,148,192,220,1,34,77,136,192,249,1,15,96,156,172,226,1,38,79,125,187,251},
            {1,17,95,148,186,228,1,34,82,131,187,245,1,22,97,143,190,228,1,46,86,118,191,249},
            {1,23,96,143,184,230,3,48,86,124,195,250,1,30,101,145,185,213,1,51,75,130,197,247},
            {1,24,97,141,181,222,1,43,74,125,198,243,1,29,98,150,187,216,1,47,75,124,184,243},
            {1,26,98,151,188,217,1,48,77,124,192,245,1,18,93,152,177,223,2,53,85,132,187,247},
            {1,17,90,150,183,224,1,43,84,127,187,245,1,20,91,145,180,223,1,41,86,124,195,249},
            {1,18,95,151,178,221,1,36,84,125,193,249,1,29,95,138,181,227,1,39,83,121,193,240},
            {1,28,120,176,186,223,1,62,85,123,210,262,1,29,117,171,181,220,10,66,76,123,218,265},
            {1,26,88,143,191,220,1,48,84,132,196,239,1,30,87,144,176,219,2,39,81,129,193,249},
            {1,28,101,150,187,216,2,53,79,125,200,256,1,34,105,156,178,218,9,49,76,124,195,258},
            {1,22,97,156,177,221,1,33,75,128,188,251,1,25,99,138,191,228,1,45,84,119,192,248},
            {1,22,96,142,189,227,1,45,84,119,188,247,1,20,92,140,186,220,1,47,74,132,197,245},
        };
        static byte[] Positions(int primary) { var p=Enumerable.Repeat((byte)1,15).ToArray();p[primary]=20;return p; }
        public static void Run(Action<bool,string> check)
        {
            var uniform=Enumerable.Repeat((byte)65,54).ToArray();uniform[24]=100;uniform[25]=50;
            var p=Positions(12);check(new PositionAbility(p).Estimate(uniform)==135,"published editor example, single striker");
            uniform[25]=100;check(new PositionAbility(p).Estimate(uniform)==151,"published two-footed striker");
            uniform[25]=50;p[3]=20;var multi=new PositionAbility(p);
            int limiting;check(multi.Estimate(uniform,out limiting)==144 && limiting==3 && multi.CombinationCost==25,"published striker/centre-back example");
            p[3]=1;p[10]=20;check(new PositionAbility(p).Estimate(uniform)==135,"published striker/attacking midfielder");
            p[10]=1;p[12]=15;check(new PositionAbility(p).Estimate(uniform)==130,"published 15 familiarity");
            for(int slot=0;slot<15;slot++) for(int n=0;n<24;n++)
            {
                p=Positions(slot);p[slot]=(byte)(n%4==1?15:20);
                if(n%4>=2) p[(slot+5)%15]=(byte)(n%4==2?16:20);
                var attrs=Enumerable.Range(0,54).Select(i=>(byte)Math.Max(1,Math.Min(100,
                    22+(n%6)*14+(i*17+n*11+slot*7)%31-15))).ToArray();
                attrs[24]=new byte[]{100,48,79,100}[n%4];attrs[25]=new byte[]{50,100,80,100}[n%4];
                var m=new PositionAbility(p);
                check(m.Estimate(attrs)==Reference[slot,n],"reference for position "+slot+" distribution "+n+" expected "+Reference[slot,n]+" got "+m.Estimate(attrs));
                var frame=new GrowthStep{Ca=new[]{35,90,140,175,195,200}[n%6],Cap=200,RawPa=200,Positions=p,Attributes=attrs};
                foreach(AttributeOptimizationMode mode in Enum.GetValues(typeof(AttributeOptimizationMode)))
                {
                    var plan=AttributeOptimizer.Create(frame,new GrowthOptions(),mode);
                    check(plan.BeforeRca==Reference[slot,n] && (plan.Conflict==null ?
                        plan.AfterRca<=frame.Ca && plan.AfterRca==m.Estimate(plan.After):
                        plan.After.SequenceEqual(attrs) && !plan.CanApply),"absolute budget at every real position and CA band");
                    if(plan.Conflict==null) AttributeOptimizer.Validate(plan);
                }
                if(AttributeOptimizer.CanRaiseToPotential(frame))
                {
                    var toPa=AttributeOptimizer.Create(frame,new GrowthOptions(),AttributeOptimizationMode.Preserve,true);
                    check(toPa.TargetCa==200 && toPa.Decreased==0 &&
                        (toPa.BeforeRca<=200 ? toPa.CanApply && toPa.AfterRca<=200 : !toPa.CanApply),
                        "PA preview handles retained and overspent distributions at actual position "+slot);
                    if(toPa.CanApply) AttributeOptimizer.Validate(toPa);
                }
            }
            var expensive=new GrowthStep{Ca=195,Cap=200,Attributes=Enumerable.Repeat((byte)95,54).ToArray(),Positions=Positions(12)};
            using(var d=new AttributeOptimizationDialog(new Player{Name="高 CA 测试"},expensive,new GrowthOptions()))
            {
                var keep=d.Plan;check(keep.BeforeRca>200 && keep.Remaining< -5 && keep.Conflict!=null && !keep.CanApply,"195/200 display ceiling cannot conceal overspend");
                var summary=(Label)d.Controls.Find("OptimizationSummary",true).Single();
                check(summary.Text.Contains("未封顶") && summary.Text.Contains("超额") && summary.Text.Contains("195"),"preview explains the actual budget and the unbounded estimate");
                ((ComboBox)d.Controls.Find("OptimizationMode",true).Single()).SelectedIndex=1;
                check(d.Plan.AfterRca<=195 && d.Plan.AfterRca>=194 && d.Plan.Decreased>0 && d.Plan.CanApply,"redistribution fixes the capped regression at current CA");
                check(d.Controls.Find("RestoreFromEarlySave",true).Length==1,"restore reachable even when preserve blocks");
            }
            // Current CA 140, existing estimate 145. Fixture has full ability, not just the display attributes.
            var above=new GrowthStep{Ca=140,Cap=190,Positions=Positions(12),Attributes=Enumerable.Repeat((byte)67,54).ToArray()};
            above.Attributes[24]=100;above.Attributes[25]=55;
            for(int f=1;f<=99;f++){above.Attributes[25]=(byte)f;if(new PositionAbility(above.Positions).Estimate(above.Attributes)==145)break;}
            var blocked=AttributeOptimizer.Create(above,new GrowthOptions(),AttributeOptimizationMode.Preserve);
            check(blocked.BeforeRca==145 && blocked.Remaining== -5 && !blocked.CanApply && blocked.After.SequenceEqual(above.Attributes),"140/145 original overspend is reported without altering attributes");
            bool denied=false;try{AttributeOptimizer.Validate(blocked);}catch(System.IO.InvalidDataException){denied=true;}check(denied,"infeasible plan cannot enter a transaction");
        }
    }
}
