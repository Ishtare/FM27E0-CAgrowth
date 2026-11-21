// Modified build v1.1.18, 2026-11-17. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    sealed class GrowthSupportOptions
    {
        public bool Enabled;
        public int Threshold = 3, BudgetPercent = 25;
        public readonly Dictionary<int,int> Targets = new Dictionary<int,int>();
        public readonly HashSet<int> Excluded = new HashSet<int>();
        public GrowthSupportOptions Clone()
        {
            var copy=new GrowthSupportOptions{Enabled=Enabled,Threshold=Threshold,BudgetPercent=BudgetPercent};
            foreach(var pair in Targets)copy.Targets[pair.Key]=pair.Value;
            foreach(int index in Excluded)copy.Excluded.Add(index);return copy;
        }
        public void Validate()
        {
            if(Threshold<1 || Threshold>5 || BudgetPercent<5 || BudgetPercent>40)
                throw new InvalidDataException("补强识别阈值为 1–5，预算份额为 5–40%。");
            if(Targets.Any(p=>!GrowthProfile.Developable(p.Key) || p.Value<1 || p.Value>12) || Excluded.Any(i=>!GrowthProfile.Developable(i)))
                throw new InvalidDataException("补强目标或属性无效；下限目标为 1–12。");
        }
    }
    sealed class GrowthSupportItem
    {
        public int Index, Current, Target;
        public bool Included;
        public string Reason;
    }
    static class GrowthSupportPlanner
    {
        // A conservative, user-adjustable heuristic, not an official FM rating or a diagnosis of corrupt data.
        static readonly int[] Foundation={34,36,37,38,42,46};
        static readonly int[] Style={26,40,43,45,50,51};
        public static int? Age(DateTime? born,DateTime? today)
        {
            if(!born.HasValue || !today.HasValue || born.Value.Date>today.Value.Date)return null;
            int age=today.Value.Year-born.Value.Year;
            if(born.Value.Date.AddYears(age)>today.Value.Date)age--;
            return age>=1 && age<=120?(int?)age:null;
        }
        public static List<GrowthSupportItem> Suggest(GrowthProfile profile,byte[] values,int ca,int? age,
            GrowthSupportOptions options,ICollection<int> scope=null)
        {
            options=options??new GrowthSupportOptions();options.Validate();GrowthFocusState.ValidateAttributes(values);
            if(ca<1 || ca>200)throw new InvalidDataException("补强需要有效的当前 CA。");
            var reference=profile.AutomaticTargets.Where(i=>profile.Cost(i)>0 && !Style.Contains(i)).Select(i=>(int)values[i]).OrderBy(i=>i).ToArray();
            int median=reference.Length==0?0:reference[reference.Length/2];
            int caFloor=ca<30?3:ca<60?4:ca<90?5:ca<120?6:7;
            int ageFloor=!age.HasValue?5:age<16?4:age<18?5:age<21?6:7;
            int automaticFloor=Math.Min(caFloor,ageFloor);
            var result=new List<GrowthSupportItem>();
            for(int i=0;i<54;i++)
            {
                if(scope!=null && !scope.Contains(i))continue;
                if(profile.Cost(i)<=0 || GrowthProfile.IsSetPiece(i) || Style.Contains(i) || !(Foundation.Contains(i) || profile.Core.Contains(i)))continue;
                int explicitGoal;bool approved=options.Targets.TryGetValue(i,out explicitGoal);
                bool low=values[i]<=options.Threshold*5 && values[i]+15<=median;
                if(!approved && !low)continue;
                int goal=approved?explicitGoal:automaticFloor;
                if(values[i]>=goal*5 && !approved)continue;
                result.Add(new GrowthSupportItem{Index=i,Current=values[i],Target=goal,Included=!options.Excluded.Contains(i),
                    Reason=(Foundation.Contains(i)?"基础身体项":"当前角色关键项")+(approved?"；已确认下限，达标停止":"明显偏低")+
                        "；参考年龄 "+(age.HasValue?age.Value.ToString():"未知")+"、CA "+ca+"及角色组合"});
            }
            return result;
        }
    }
}
