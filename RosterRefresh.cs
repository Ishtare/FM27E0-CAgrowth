// Modified build v1.1.21, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    static class RosterPlayerIdentity
    {
        public static List<KeyValuePair<ulong,byte[]>> Guards(Player player,ulong module,ulong personType,ulong coachType,ulong coachExtra)
        {
            if(player==null || player.Uid<=0 || player.Person<0x10000)
                throw new InvalidDataException("球员 UID 或资料位置无效。");
            if(player.Extra!=0 && player.Extra!=coachExtra)
                throw new InvalidDataException("球员兼教练的资料位置未通过检查。");
            ulong type=player.Extra==0?personType:coachType;
            return new List<KeyValuePair<ulong,byte[]>>
            {
                new KeyValuePair<ulong,byte[]>(player.Person,BitConverter.GetBytes(module+type)),
                new KeyValuePair<ulong,byte[]>(player.Person+FmLayout.Uid,BitConverter.GetBytes(player.Uid))
            };
        }

        public static bool Matches(Func<ulong,int,byte[]> read,IEnumerable<KeyValuePair<ulong,byte[]>> guards)
        {
            return guards.All(guard=>
            {
                var actual=read(guard.Key,guard.Value.Length);
                return actual!=null && actual.SequenceEqual(guard.Value);
            });
        }
    }

    sealed class ClubRosterUpdate
    {
        public ulong Club;
        public readonly Dictionary<int,Player> Refreshed=new Dictionary<int,Player>();
        public readonly HashSet<int> Removed=new HashSet<int>();
        public GrowthDirectory Directory;
        public int CurrentPlayers;

        public List<Player> Merge(IEnumerable<Player> previous)
        {
            return previous.Where(p=>!Refreshed.ContainsKey(p.Uid) && !Removed.Contains(p.Uid))
                .Concat(Refreshed.Values).ToList();
        }
    }

    // The button reads the club's real squad vectors, including players missing from the last global scan.
    // For known departing/loaned players, the current contract and playing side are read again as well.
    sealed class ClubRosterReader
    {
        const ulong AddressLimit=0x800000000000;
        readonly Func<ulong,int,byte[]> read;
        readonly GrowthReadLayout layout;
        readonly Func<ulong,ulong,byte[],Player> decodePlayer;
        readonly Func<Player,bool> showPlayer;

        public ClubRosterReader(Func<ulong,int,byte[]> read,GrowthReadLayout layout,
            Func<ulong,ulong,byte[],Player> decodePlayer,Func<Player,bool> showPlayer=null)
        {
            this.read=read;
            this.layout=layout;
            this.decodePlayer=decodePlayer;
            this.showPlayer=showPlayer??(player=>true);
        }

        public ClubRosterUpdate ReadClub(Club club,IEnumerable<Player> previous,DateTime? today)
        {
            var frame=new EligibilityReader(read);
            if(club.Uid<=0 || frame.Ptr(club.Addr)!=layout.Module+layout.ClubVt ||
                frame.Int(club.Addr+12)!=club.Uid)
                throw new InvalidDataException("俱乐部身份未通过检查，请重新挂接当前存档。");

            var update=new ClubRosterUpdate{Club=club.Addr};
            ulong[] teams=ReadSquadVector(frame,club.Addr+layout.ClubTeams,64,"俱乐部分队");
            if(teams.Length==0 && club.Team!=0)
                throw new InvalidDataException("俱乐部分队资料尚未就绪，请稍后刷新。");
            var seenRoots=new HashSet<ulong>();
            foreach(ulong team in teams.Where(t=>t!=0).Distinct())
            {
                if(frame.Ptr(team)!=layout.Module+layout.TeamVt)
                    throw new InvalidDataException("分队类型未通过检查，请稍后刷新。");
                ulong owner=frame.Ptr(team+layout.TeamClub);
                if(owner<0x10000 || frame.Ptr(owner)!=layout.Module+layout.ClubVt)
                    throw new InvalidDataException("分队归属未通过检查，请稍后刷新。");
                foreach(ulong root in ReadSquadVector(frame,team+layout.TeamPlayers,500,"球队名单"))
                {
                    if(root==0 || !seenRoots.Add(root))continue;
                    Add(update,ReadRoot(frame,root,0));
                }
            }

            var sideSet=new HashSet<ulong>(teams);
            var localUids=new HashSet<int>(update.Refreshed.Keys);
            var unresolved=new HashSet<int>();
            foreach(var old in previous.Where(p=>p.Uid>0 && WasInClub(p,club.Addr,sideSet)))
            {
                if(localUids.Contains(old.Uid))continue;
                if(HasCurrentIdentity(old))Add(update,ReadPerson(frame,old.Person,old.Extra,old.Uid));
                else unresolved.Add(old.Uid);
            }
            unresolved.ExceptWith(update.Refreshed.Keys);
            foreach(var entry in FindPlayers(frame,unresolved,allowMissing:true))
            {
                if(entry.Value==0)update.Removed.Add(entry.Key);
                else Add(update,ReadRoot(frame,entry.Value,entry.Key));
            }

            Finish(update,frame,new[]{club},today);
            update.CurrentPlayers=update.Refreshed.Values.Count(p=>p.Club==club.Addr);
            return update;
        }

        public ClubRosterUpdate ReadPlayer(Player previous,DateTime? today)
        {
            var frame=new EligibilityReader(read);
            var update=new ClubRosterUpdate();
            Player current;
            if(HasCurrentIdentity(previous))current=ReadPerson(frame,previous.Person,previous.Extra,previous.Uid,false);
            else
            {
                var found=FindPlayers(frame,new HashSet<int>{previous.Uid},allowMissing:false);
                current=ReadRoot(frame,found[previous.Uid],previous.Uid,false);
            }
            Add(update,current);
            Finish(update,frame,new Club[0],today);
            if(!update.Refreshed.ContainsKey(previous.Uid))
                throw new InvalidDataException("当前球员已被名单筛选隐藏。");
            return update;
        }

        static bool WasInClub(Player player,ulong club,HashSet<ulong> teams)
        {
            var membership=player.Roster;
            return player.Club==club || membership!=null &&
                (membership.ContractClub==club || membership.PlayingClub==club || teams.Contains(membership.Team));
        }

        bool HasCurrentIdentity(Player player)
        {
            return RosterPlayerIdentity.Matches(read,RosterPlayerIdentity.Guards(
                player,layout.Module,layout.PersonVt,layout.CoachVt,layout.CoachExtra));
        }

        static void Add(ClubRosterUpdate update,Player player)
        {
            Player existing;
            if(update.Refreshed.TryGetValue(player.Uid,out existing) && existing.Person!=player.Person)
                throw new InvalidDataException("同一 UID 匹配到多名球员，暂不能确定当前记录。");
            update.Refreshed[player.Uid]=player;
        }

        void Finish(ClubRosterUpdate update,EligibilityReader frame,Club[] clubs,DateTime? today)
        {
            var listed=update.Refreshed.Values.ToArray();
            update.Directory=new GrowthDirectoryReader(frame.Bytes,layout).Capture(listed,clubs,today);
            foreach(var player in listed.Where(p=>!showPlayer(p)))
            {
                update.Refreshed.Remove(player.Uid);
                update.Removed.Add(player.Uid);
            }
            if(!frame.Matches(read))
                throw new InvalidDataException("游戏正在更新名单或合同，请稍后刷新。");
        }

        Player ReadRoot(EligibilityReader frame,ulong root,int uid,bool requireOwnership=true)
        {
            ulong person=root+layout.PlayerBefore;
            ulong extra=0;
            if(layout.PlayerRootVt!=0)
            {
                ulong type=frame.Ptr(root);
                if(type==layout.Module+layout.CoachRootVt)extra=layout.CoachExtra;
                else if(type!=layout.Module+layout.PlayerRootVt)
                    throw new InvalidDataException("名单中的球员对象类型未通过检查。");
            }
            else if(frame.Ptr(person)!=layout.Module+layout.PersonVt)extra=layout.CoachExtra;
            return ReadPerson(frame,person+extra,extra,uid,requireOwnership);
        }

        Player ReadPerson(EligibilityReader frame,ulong person,ulong extra,int expectedUid,bool requireOwnership=true)
        {
            var head=frame.Bytes(person,FmLayout.HeadLen);
            if(BitConverter.ToUInt64(head,0)!=layout.Module+(extra==0?layout.PersonVt:layout.CoachVt))
                throw new InvalidDataException("球员类型已变化，请稍后重新读取。");
            int uid=BitConverter.ToInt32(head,(int)FmLayout.Uid);
            if(uid<=0 || expectedUid!=0 && uid!=expectedUid)
                throw new InvalidDataException("名单中的球员 UID 未通过检查。");
            ulong tail=person-extra;
            if(tail<0x10000+FmLayout.BlockBeforePerson ||
                !FmLayout.ValidBlock(read(tail-FmLayout.BlockBeforePerson,FmLayout.PosCount+FmLayout.AttrCount)))
                throw new InvalidDataException("球员的属性和位置资料尚未通过检查。");
            var player=decodePlayer(person,extra,head);
            if(player==null || player.Uid!=uid || string.IsNullOrWhiteSpace(player.Name))
                throw new InvalidDataException("球员姓名或资料未能完整读取。");

            try
            {
                ulong contract=frame.Ptr(person+layout.PersonContract);
                if(contract==0)return player;
                if(contract<0x10000 || frame.Ptr(contract)!=layout.Module+layout.ContractVt || frame.Ptr(contract+8)!=person)
                    throw new InvalidDataException(player.DisplayName+" 的合同所属人尚未核对通过。");
                ClubOfTeam(frame,frame.Ptr(contract+layout.ContractTeam));
                player.Club=ClubOfTeam(frame,frame.Ptr(tail-layout.TeamBefore));
            }
            catch(InvalidDataException)
            {
                // General player details can still use a verified person. Growth/contract
                // previews require their own usable team or contract.
                if(requireOwnership)throw;
            }
            return player;
        }

        ulong ClubOfTeam(EligibilityReader frame,ulong team)
        {
            if(team==0)return 0;
            if(frame.Ptr(team)!=layout.Module+layout.TeamVt)
                throw new InvalidDataException("球员的当前球队与合同资料尚未同步。");
            ulong club=frame.Ptr(team+layout.TeamClub);
            if(club!=0 && frame.Ptr(club)!=layout.Module+layout.ClubVt)
                throw new InvalidDataException("球队的俱乐部归属尚未核对通过。");
            return club;
        }

        static ulong[] ReadSquadVector(EligibilityReader frame,ulong address,int max,string label)
        {
            var header=frame.Bytes(address,24);
            ulong begin=BitConverter.ToUInt64(header,0),end=BitConverter.ToUInt64(header,8),capacity=BitConverter.ToUInt64(header,16);
            if(begin==0 && end==0 && capacity==0)return new ulong[0];
            if(begin<0x10000 || begin%8!=0 || end<begin || capacity<end || capacity>=AddressLimit ||
                (capacity-begin)%8!=0 || (end-begin)%8!=0 || end-begin>(ulong)max*8 || capacity-begin>(ulong)max*32)
                throw new InvalidDataException(label+"的边界无效，请稍后刷新。");
            var bytes=frame.Bytes(begin,(int)(end-begin));
            return Enumerable.Range(0,bytes.Length/8).Select(i=>BitConverter.ToUInt64(bytes,i*8)).ToArray();
        }

        Dictionary<int,ulong> FindPlayers(EligibilityReader frame,HashSet<int> uids,bool allowMissing)
        {
            var found=new Dictionary<int,ulong>();
            if(uids.Count==0)return found;
            if(layout.PlayerDirectory==0 || layout.PlayerRootVt==0)
                throw new InvalidDataException("当前构建无法从球员索引重新定位 UID，请重新挂接核对。");

            var header=frame.Bytes(layout.Module+layout.PlayerDirectory,16);
            ulong begin=BitConverter.ToUInt64(header,0),end=BitConverter.ToUInt64(header,8);
            if(begin<0x10000 || end<=begin || end>=AddressLimit || (end-begin)%8!=0 || end-begin>8UL*500000)
                throw new InvalidDataException("球员索引尚未就绪，请稍后刷新。");
            bool incomplete=false;
            for(ulong offset=0;offset<end-begin;offset+=65536)
            {
                var data=frame.Bytes(begin+offset,(int)Math.Min(65536,end-begin-offset));
                for(int i=0;i<data.Length;i+=8)
                {
                    ulong root=BitConverter.ToUInt64(data,i);
                    if(root==0)continue;
                    if(root<0x10000 || root>=AddressLimit-layout.PlayerBefore-layout.CoachExtra-16)
                    {incomplete=true;continue;}
                    var typeBytes=read(root,8);
                    if(typeBytes==null || typeBytes.Length!=8){incomplete=true;continue;}
                    ulong type=BitConverter.ToUInt64(typeBytes,0);
                    if(type!=layout.Module+layout.PlayerRootVt && type!=layout.Module+layout.CoachRootVt)continue;
                    ulong person=root+layout.PlayerBefore+(type==layout.Module+layout.CoachRootVt?layout.CoachExtra:0);
                    var uidBytes=read(person+FmLayout.Uid,4);
                    if(uidBytes==null || uidBytes.Length!=4){incomplete=true;continue;}
                    int uid=BitConverter.ToInt32(uidBytes,0);
                    if(!uids.Contains(uid))continue;
                    ulong other;
                    if(found.TryGetValue(uid,out other) && other!=root)
                        throw new InvalidDataException("球员索引中有重复 UID，无法确认应使用哪条资料。");
                    found[uid]=root;
                }
            }
            foreach(int uid in uids.Where(uid=>!found.ContainsKey(uid)))
            {
                if(incomplete || !allowMissing)
                    throw new InvalidDataException("未能在当前存档中确认球员 UID "+uid+"。");
                found[uid]=0;
            }
            return found;
        }
    }
}
