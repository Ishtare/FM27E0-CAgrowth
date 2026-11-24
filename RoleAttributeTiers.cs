// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Linq;

namespace Fm24Editor
{
    enum AttributeTier { Core, Supporting, Foundation, Other, Protected }

    sealed class RoleAttributeTiers
    {
        static readonly int[] Preserved = {26,40,43,45,50,51};
        static readonly int[] Foundation = {34,36,37,38,42,46};
        static readonly int[] Basics = {7,17,18,22,23,28,29,52,53};
        static readonly int[] Targets = {75,70,55,50};
        static readonly int[] Floors = {60,60,40,30};
        const int CoreMilestone=85,SupportingMilestone=75,CoreMaximum=100,SupportingMaximum=85;
        static readonly string[] Labels = {"蓝档 · 核心","绿档 · 重要","白档 · 基础","末档 · 其余","保留项"};
        readonly GrowthProfile profile;
        readonly AttributeTier[] tiers;
        readonly int[][] members;

        public RoleAttributeTiers(GrowthProfile profile,PositionAbility model)
        {
            this.profile=profile;
            tiers = Enumerable.Range(0,54).Select(i => Classify(profile,model,i)).ToArray();
            members = Enumerable.Range(0,Targets.Length).Select(tier =>
                Enumerable.Range(0,tiers.Length).Where(i => (int)tiers[i]==tier)
                    .OrderByDescending(profile.WithinTierPriority).ThenByDescending(profile.Priority)
                    .ThenBy(i => i).ToArray()).ToArray();
        }

        public AttributeTier TierOf(int index) { return tiers[index]; }
        public int[] Members(AttributeTier tier) { return members[(int)tier]; }
        public static int Target(AttributeTier tier) { return Targets[(int)tier]; }
        public static int Floor(AttributeTier tier) { return Floors[(int)tier]; }
        public static int Milestone(AttributeTier tier)
        {
            if(tier==AttributeTier.Core)return CoreMilestone;
            if(tier==AttributeTier.Supporting)return SupportingMilestone;
            return Target(tier);
        }
        public static string Label(AttributeTier tier) { return Labels[(int)tier]; }

        public void RaiseDevelopment(byte[] values,AttributeBudget budget,Action<int[]> beforeBaseTier=null)
        {
            for(var tier=AttributeTier.Core;tier<=AttributeTier.Foundation;tier++)
            {
                var current=Members(tier);
                if(beforeBaseTier!=null)beforeBaseTier(current);
                if(!budget.Raise(values,current,Target(tier)))return;
            }
            RaiseMilestones(values,budget);
        }

        public void RaiseSurplus(byte[] values,AttributeBudget budget)
        {
            if(!RaiseMilestones(values,budget))return;

            var blue=Members(AttributeTier.Core);
            // These bands are already sorted by descending within-tier priority.
            foreach(var band in blue.GroupBy(profile.WithinTierPriority))
                budget.Raise(values,band.ToArray(),CoreMaximum);
            if(blue.All(i=>values[i]==CoreMaximum))
                budget.Raise(values,Members(AttributeTier.Supporting),SupportingMaximum);
        }

        bool RaiseMilestones(byte[] values,AttributeBudget budget)
        {
            return budget.Raise(values,Members(AttributeTier.Core),CoreMilestone) &&
                budget.Raise(values,Members(AttributeTier.Supporting),SupportingMilestone);
        }

        public static bool Editable(GrowthProfile profile,int index)
        {
            return GrowthProfile.Developable(index) && !GrowthProfile.IsSetPiece(index) &&
                profile.Cost(index)>0 && !Preserved.Contains(index);
        }

        static AttributeTier Classify(GrowthProfile profile,PositionAbility model,int index)
        {
            if(!Editable(profile,index) || !model.Counts(index))return AttributeTier.Protected;
            if(profile.Core.Contains(index))return AttributeTier.Core;
            if(profile.Supporting.Contains(index))return AttributeTier.Supporting;
            return Foundation.Contains(index) || Basics.Contains(index) ? AttributeTier.Foundation : AttributeTier.Other;
        }
    }

    sealed class AttributeBudget
    {
        readonly PositionAbility model;
        readonly int limit;

        public AttributeBudget(PositionAbility model,int limit)
        { this.model=model;this.limit=limit; }

        public bool Fits(byte[] attributes) { return model.Estimate(attributes)<=limit; }

        public bool Raise(byte[] values,int[] members,int target)
        { return Raise(values,members,i=>target); }

        public bool Raise(byte[] values,int[] members,Func<int,int> target)
        {
            int high=0;
            foreach(int index in members)high=Math.Max(high,target(index)-values[index]);
            if(high==0)return true;

            // Batch equal increments in the same tier. Measure the complete player at each trial.
            var trial=(byte[])values.Clone();
            Advance(values,trial,members,target,high);
            if(Fits(trial))
            {
                Array.Copy(trial,values,values.Length);
                return true;
            }

            int low=0;
            while(high-low>1)
            {
                int step=low+(high-low)/2;
                Advance(values,trial,members,target,step);
                if(Fits(trial))low=step;else high=step;
            }
            Advance(values,trial,members,target,low);
            Array.Copy(trial,values,values.Length);

            // Resolve the remaining 0.2-point steps in phase-priority order within this tier.
            bool raised;
            do
            {
                raised=false;
                foreach(int index in members)
                {
                    if(values[index]>=target(index))continue;
                    values[index]++;
                    if(Fits(values))raised=true;
                    else values[index]--;
                }
            }
            while(raised);
            return false;
        }

        static void Advance(byte[] before,byte[] after,int[] members,Func<int,int> target,int steps)
        {
            foreach(int index in members)
                after[index]=(byte)Math.Max(before[index],Math.Min(target(index),before[index]+steps));
        }

        public bool Lower(byte[] values,int[] members,Func<int,int> floor)
        {
            var before=(byte[])values.Clone();
            foreach(int index in members)values[index]=(byte)Math.Min(values[index],floor(index));
            if(!Fits(values))return false;
            Raise(values,members,i=>before[i]);
            return true;
        }
    }
}
