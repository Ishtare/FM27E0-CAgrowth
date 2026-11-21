// Modified build v1.1.23, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fm24Editor
{
    // These indexes describe the published roster. Game reads and pre-write identity checks stay independent.
    sealed class PlayerRosterIndex
    {
        readonly Dictionary<int,List<Player>> byUid = new Dictionary<int,List<Player>>();
        readonly Dictionary<ulong,List<Player>> byClub = new Dictionary<ulong,List<Player>>();
        readonly Dictionary<ulong,List<Player>> byTeam = new Dictionary<ulong,List<Player>>();
        readonly Dictionary<ulong,List<Player>> byOwner = new Dictionary<ulong,List<Player>>();
        static readonly Player[] Empty = new Player[0];

        public PlayerRosterIndex(IEnumerable<Player> players)
        {
            foreach (var player in players) Track(player,true);
        }

        static void Update<TKey>(Dictionary<TKey,List<Player>> map,TKey key,Player player,bool add)
        {
            List<Player> bucket;
            if (!map.TryGetValue(key,out bucket))
            {
                if (!add) return;
                map.Add(key,bucket = new List<Player>(1));
            }
            if (add) bucket.Add(player);
            else if (bucket.Remove(player) && bucket.Count == 0) map.Remove(key);
        }

        void Track(Player player,bool add)
        {
            Update(byUid,player.Uid,player,add);
            if (player.Club != 0) Update(byClub,player.Club,player,add);
            var member = player.Roster;
            if (member == null) return;
            if (member.Team != 0) Update(byTeam,member.Team,player,add);
            if (member.ContractClub != 0) Update(byOwner,member.ContractClub,player,add);
        }

        static IEnumerable<Player> Bucket<TKey>(Dictionary<TKey,List<Player>> map,TKey key)
        {
            List<Player> list;
            return map.TryGetValue(key,out list) ? (IEnumerable<Player>)list : Empty;
        }

        public IEnumerable<Player> ForUid(int uid) { return Bucket(byUid,uid); }
        public IEnumerable<Player> ForClub(ulong club) { return Bucket(byClub,club); }

        public Player Unique(int uid)
        {
            List<Player> matches;
            if (uid <= 0 || !byUid.TryGetValue(uid,out matches)) return null;
            var first = matches[0];
            for (int i = 1; i < matches.Count; i++)
                if (matches[i].Person != first.Person) return null;
            return first;
        }

        public IEnumerable<Player> ForScope(GrowthDirectory directory,GrowthSquadChoice scope)
        {
            if (scope == null) return Empty;
            if (scope.Parts != null) return CombinedSquads(directory,scope.Parts);
            if (scope.Kind == GrowthSquadKind.Team) return Bucket(byTeam,scope.Team);
            if (scope.Kind == GrowthSquadKind.OutOnLoan)
                return Bucket(byOwner,scope.Club).Where(p => GrowthRosterView.Contains(directory,scope,p));
            var attached = directory.ForClub(scope.Club).SelectMany(team => Bucket(byTeam,team.Address))
                .Where(p => p.Roster.Contract != 0);
            var members = ForClub(scope.Club).Concat(attached).Distinct();
            if (scope.Kind == GrowthSquadKind.Unknown)
                return members.Where(p => p.Roster == null || !directory.Teams.ContainsKey(p.Roster.Team));
            return members;
        }

        IEnumerable<Player> CombinedSquads(GrowthDirectory directory,IEnumerable<GrowthSquadChoice> parts)
        {
            var seen=new HashSet<int>();
            foreach(var part in parts)
                foreach(var player in ForScope(directory,part))
                {
                    if(!seen.Add(player.Uid))continue;
                    var current=Unique(player.Uid);
                    if(current!=null)yield return current;
                }
        }

        public void Apply(ClubRosterUpdate update)
        {
            foreach (int uid in update.Refreshed.Keys.Concat(update.Removed).Distinct())
            {
                List<Player> previous;
                if (byUid.TryGetValue(uid,out previous))
                    foreach (var player in previous.ToArray()) Track(player,false);
            }
            foreach (var player in update.Refreshed.Values) Track(player,true);
        }
    }
}
