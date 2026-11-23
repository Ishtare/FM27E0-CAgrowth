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
        static readonly int[] Targets = {85,75,60,50};
        static readonly string[] Labels = {"蓝档 · 核心","绿档 · 重要","白档 · 基础","末档 · 其余","保留项"};
        readonly AttributeTier[] tiers;
        readonly int[][] members;

        public RoleAttributeTiers(GrowthProfile profile,PositionAbility model)
        {
            tiers = Enumerable.Range(0,54).Select(i => Classify(profile,model,i)).ToArray();
            members = Enumerable.Range(0,Targets.Length).Select(tier =>
                Enumerable.Range(0,tiers.Length).Where(i => (int)tiers[i]==tier)
                    .OrderByDescending(profile.Priority).ThenBy(i => i).ToArray()).ToArray();
        }

        public AttributeTier TierOf(int index) { return tiers[index]; }
        public int[] Members(AttributeTier tier) { return members[(int)tier]; }
        public static int Target(AttributeTier tier) { return Targets[(int)tier]; }
        public static string Label(AttributeTier tier) { return Labels[(int)tier]; }

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
