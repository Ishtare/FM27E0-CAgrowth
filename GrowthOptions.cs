// Modified build v1.1.27. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fm24Editor
{
    enum GrowthMode { Position, SelectedAttributes, CaOnly }

    sealed class GrowthOptions
    {
        public GrowthMode Mode = GrowthMode.Position;
        public int Profile = -1;
        public int DefendingProfile = -1, PossessionWeight = 50;
        public int[] Attributes = new int[0];
        public bool LimitUnselected;
        public GrowthSupportOptions Support = new GrowthSupportOptions();
        public GrowthOptions Clone()
        {
            return new GrowthOptions
            {
                Mode=Mode, Profile=Profile, DefendingProfile=DefendingProfile, PossessionWeight=PossessionWeight,
                Attributes=Attributes==null?new int[0]:Attributes.Distinct().OrderBy(i=>i).ToArray(),
                LimitUnselected=LimitUnselected, Support=(Support??new GrowthSupportOptions()).Clone()
            };
        }
        public GrowthProfile ResolveProfile(byte[] positions)
        {if(DefendingProfile<-2)throw new ArgumentOutOfRangeException("DefendingProfile");return GrowthProfile.Combine(GrowthProfile.Resolve(positions,Profile),DefendingProfile==-2?null:GrowthProfile.Resolve(positions,DefendingProfile),PossessionWeight);}
        public bool UsesAttributes { get { return Mode != GrowthMode.CaOnly; } }
        public bool LimitsUnselected { get { return Mode==GrowthMode.SelectedAttributes && LimitUnselected; } }
        internal ICollection<int> SupportScope { get { return LimitsUnselected?(Attributes??new int[0]):null; } }
        public GrowthOptions ForAnotherPlayer(byte[] positions,byte[] attributes,int ca,int? age)
        {
            var copy=Clone();if(!copy.UsesAttributes || !copy.Support.Enabled)return copy;
            var fresh=copy.Support.Clone();fresh.Targets.Clear();
            var eligible=new HashSet<int>(GrowthSupportPlanner.Suggest(copy.ResolveProfile(positions),attributes,ca,age,fresh,copy.SupportScope).Select(i=>i.Index));
            foreach(int index in copy.Support.Targets.Keys.Where(i=>!eligible.Contains(i)).ToArray())copy.Support.Targets.Remove(index);
            return copy;
        }
        public string Summary
        {
            get
            {
                if (Mode == GrowthMode.CaOnly) return "仅提升 CA";
                if (Mode == GrowthMode.SelectedAttributes) return "自选 " + (Attributes == null ? 0 : Attributes.Distinct().Count()) + " 项";
                if(DefendingProfile!=-2 && (Profile>=0 || DefendingProfile>=0))return "有球 / 无球综合";
                return Profile < 0 ? "按主要位置" : Profile < GrowthProfile.All.Length ? GrowthProfile.All[Profile].Name : "方向无效";
            }
        }
        public GrowthFocusState CreateState(byte[] positions, byte[] attributes, int ca, int? age = null)
        {
            if (Mode != GrowthMode.Position && Mode != GrowthMode.SelectedAttributes)
                throw new InvalidOperationException("当前模式不直接同步属性。");
            var selection=Mode==GrowthMode.SelectedAttributes?(Attributes??new int[0]):null;
            return new GrowthFocusState(positions,attributes,ca,Profile,selection,Support,age,
                DefendingProfile,PossessionWeight,LimitsUnselected);
        }
    }

    static class GrowthRoster
    {
        static bool SameScope(GrowTarget target,Player next)
        {
            if(target.ScopeClub==0)return next.Club==target.P.Club;
            if(next.Club==target.ScopeClub)return true;
            return next.Roster!=null && next.Roster.Contract!=0 && next.Roster.ContractClub==target.ScopeClub;
        }
        // Rebinding never constructs a target: only explicit user selection can add one.
        public static List<GrowTarget> Rebind(IEnumerable<GrowTarget> previous, IEnumerable<Player> current)
        {
            var unique = current.Where(p => p != null && p.Uid > 0).GroupBy(p => p.Uid)
                .Where(g => g.Select(p => p.Person).Distinct().Count() == 1).ToDictionary(g => g.Key, g => g.First());
            return Rebind(previous,uid=>{Player player;return unique.TryGetValue(uid,out player)?player:null;});
        }
        public static List<GrowTarget> Rebind(IEnumerable<GrowTarget> previous,Func<int,Player> resolve)
        {
            var result = new List<GrowTarget>();
            var used = new HashSet<int>();
            foreach (var target in previous)
            {
                if (target == null || target.P == null || target.P.Uid <= 0 || !used.Add(target.P.Uid)) continue;
                var next=resolve(target.P.Uid);
                if(next==null || !SameScope(target,next))continue;
                target.P = next;
                result.Add(target);
            }
            return result;
        }
    }
}
