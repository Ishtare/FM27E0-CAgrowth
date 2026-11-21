// Modified build v1.1.23, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// Read-only squad/loan metadata. See docs/growth-squads-support.md for upstream format evidence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Fm24Editor
{
    enum GrowthLoanState { Unknown, None, Active }
    sealed class GrowthMembership
    {
        public ulong Team, PlayingClub, ContractClub, ContractTeam, Contract;
        public GrowthLoanState Loan=GrowthLoanState.Unknown;
        public DateTime? LoanEnd;
        public string Note="合同或租借资料尚未读取";
    }
    sealed class GrowthTeam
    {
        public ulong Address, Club;
        public int Classification=-1, AgeLimit=-1;
        public string Name, Competition;
        public bool First;
        public string Key {get{return "team:address:"+Address;}}
    }
    sealed class GrowthRelatedClub
    {
        public ulong Club;
        public int Type;
    }
    sealed class GrowthDirectory
    {
        public readonly Dictionary<ulong,GrowthTeam> Teams=new Dictionary<ulong,GrowthTeam>();
        public readonly Dictionary<ulong,List<GrowthTeam>> ClubTeams=new Dictionary<ulong,List<GrowthTeam>>();
        public readonly Dictionary<ulong,List<GrowthRelatedClub>> Related=new Dictionary<ulong,List<GrowthRelatedClub>>();
        public readonly Dictionary<ulong,string> ClubNames=new Dictionary<ulong,string>();
        public string Note="分队资料尚未读取";
        public string ClubName(ulong address){string name;return ClubNames.TryGetValue(address,out name)?name:"俱乐部资料未载入";}
        public string TeamName(Player player)
        {GrowthTeam team;return player.Roster!=null && Teams.TryGetValue(player.Roster.Team,out team)?team.Name:"分队待确认";}
        public List<GrowthTeam> ForClub(ulong club){List<GrowthTeam> found;return ClubTeams.TryGetValue(club,out found)?found:new List<GrowthTeam>();}

        public void UpdateFrom(GrowthDirectory update, ulong refreshedClub)
        {
            foreach(var entry in update.Teams)Teams[entry.Key]=entry.Value;
            foreach(var entry in update.ClubNames)ClubNames[entry.Key]=entry.Value;
            foreach(var entry in update.Related)Related[entry.Key]=entry.Value;
            foreach(var entry in update.ClubTeams)
            {
                if(entry.Key==refreshedClub)
                {
                    ClubTeams[entry.Key]=entry.Value;
                    continue;
                }
                List<GrowthTeam> existing;
                if(!ClubTeams.TryGetValue(entry.Key,out existing))ClubTeams[entry.Key]=entry.Value;
                else
                {
                    foreach(var team in entry.Value)
                    {
                        int i=existing.FindIndex(t=>t.Address==team.Address);
                        if(i>=0)existing[i]=team;
                        else existing.Add(team);
                    }
                }
            }
        }
    }
    sealed class GrowthReadLayout
    {
        public ulong Module, PersonVt, CoachVt, TeamVt, ClubVt, ContractVt, LoanVt;
        public ulong TeamBefore, PlayerBefore, LoanHolder, PersonContract, ClubTeams, ClubName, ContractTeam, ContractExpiry;
        public ulong TeamPlayers=0x38, TeamClub=0x30, CoachExtra=0xF8, PlayerRootVt, CoachRootVt, PlayerDirectory;
        public bool Labels;
        public static GrowthReadLayout From(FmProcess fm)
        {
            var native=CurrentBuildProfile.Matches(fm)?CurrentBuildProfile.Active:null;
            return new GrowthReadLayout{Module=fm.Base,PersonVt=FmProcess.PersonVtRva,CoachVt=FmProcess.PlayerCoachVtRva,TeamVt=FmProcess.TeamVtRva,
                ClubVt=FmProcess.ClubVtRva,ContractVt=FmProcess.ContractVtRva,LoanVt=FmProcess.LoanContractVtRva,
                TeamBefore=FmLayout.TeamBeforePerson,PlayerBefore=FmLayout.PlayerObjBeforePerson,LoanHolder=FmLayout.PlayerLoanContract,
                PersonContract=FmLayout.ContractAfterPerson,ClubTeams=FmLayout.ClubTeams,ClubName=FmLayout.ClubName,
                ContractTeam=FmLayout.ContractTeam,ContractExpiry=FmLayout.ContractExpiry,
                TeamPlayers=FmLayout.TeamPlayers,TeamClub=FmLayout.TeamClub2,CoachExtra=FmLayout.PlayerCoachExtra,
                PlayerRootVt=native==null?0:native.Player,CoachRootVt=native==null?0:native.CoachPlayer,
                PlayerDirectory=native==null?0:native.PlayerDirectory,
                Labels=CurrentBuildProfile.Matches(fm) || fm.Version=="27.0.1+2494687"};
        }
    }
    sealed class GrowthDirectoryReader
    {
        readonly Func<ulong,int,byte[]> read;
        readonly GrowthReadLayout layout;
        readonly GrowthDirectory result=new GrowthDirectory();
        readonly HashSet<ulong> failedTeams=new HashSet<ulong>();
        public GrowthDirectoryReader(Func<ulong,int,byte[]> read,GrowthReadLayout layout){this.read=read;this.layout=layout;}
        string Text(EligibilityReader r,ulong address)
        {
            if(address==0)return null;int length=r.Int(address);
            if(length<1 || length>512)return null;string text=Encoding.UTF8.GetString(r.Bytes(address+4,length));
            return text.Any(c=>char.IsControl(c))?null:text;
        }
        string ClubName(EligibilityReader r,ulong club)
        {
            if(r.Ptr(club)!=layout.Module+layout.ClubVt)throw new InvalidDataException("俱乐部类型不匹配");
            string name;if(result.ClubNames.TryGetValue(club,out name))return name;
            name=Text(r,r.Ptr(club+layout.ClubName));if(!string.IsNullOrWhiteSpace(name))result.ClubNames[club]=name;
            return name;
        }
        GrowthTeam Team(ulong address)
        {
            GrowthTeam found;if(result.Teams.TryGetValue(address,out found))return found;
            if(address<0x10000 || failedTeams.Contains(address))return null;
            try
            {
                var r=new EligibilityReader(read);if(r.Ptr(address)!=layout.Module+layout.TeamVt)throw new InvalidDataException("球队类型不匹配");
                ulong club=r.Ptr(address+0x30);ClubName(r,club);
                var team=new GrowthTeam{Address=address,Club=club};
                if(layout.Labels){team.Classification=r.Int(address+0x28);team.AgeLimit=r.Byte(address+0x2C);}
                team.First=team.Classification==0 && team.AgeLimit==100;
                try{ulong comp=r.Ptr(address+0x50);if(comp>=0x10000)team.Competition=Text(r,r.Ptr(comp+0x40));}catch{}
                team.Name=team.First?"一线队":team.AgeLimit>=10 && team.AgeLimit<=23?"U"+team.AgeLimit+" 青年队":team.AgeLimit==100?"二队／预备队":"分队（类别待确认）";
                if(!r.Matches(read))throw new InvalidDataException("分队正在更新");
                result.Teams[address]=team;return team;
            }
            catch{failedTeams.Add(address);return null;}
        }
        GrowthMembership Membership(Player player,DateTime? today)
        {
            var info=new GrowthMembership();
            try
            {
                var r=new EligibilityReader(read);ulong vt=r.Ptr(player.Person);
                if(vt!=layout.Module+(player.Extra==0?layout.PersonVt:layout.CoachVt) || r.Int(player.Person+FmLayout.Uid)!=player.Uid)
                    throw new InvalidDataException("人物身份已变化");
                info.Team=r.Ptr(player.Tail-layout.TeamBefore);var current=Team(info.Team);
                if(current!=null)info.PlayingClub=current.Club;
                ulong contract=r.Ptr(player.Person+layout.PersonContract);
                if(contract==0){info.Note="无有效合同；不根据残留球队指针判定租借";return info;}
                if(r.Ptr(contract)!=layout.Module+layout.ContractVt || r.Ptr(contract+8)!=player.Person)
                    throw new InvalidDataException("合同归属未通过检查");
                info.Contract=contract;info.ContractTeam=r.Ptr(contract+layout.ContractTeam);
                var owner=Team(info.ContractTeam);if(owner==null || current==null)throw new InvalidDataException("合同或效力球队资料不完整");
                info.ContractClub=owner.Club;
                if(player.Extra!=0)
                {
                    if(!r.Matches(read))throw new InvalidDataException("球员兼教练的球队或合同资料正在变化");
                    info.Note="球员兼教练的租借合同暂未核实，分队按实际效力队显示";
                    return info;
                }
                ulong holder=r.Ptr(player.Tail-layout.PlayerBefore+layout.LoanHolder);
                ulong loan=holder==0?0:r.Ptr(holder);
                if(loan==0)
                {
                    if(owner.Club!=current.Club)throw new InvalidDataException("合同和效力俱乐部不同，租借资料待确认");
                    info.Loan=GrowthLoanState.None;info.Note="未发现当前租借合同";
                }
                else
                {
                    if(loan<0x10000 || r.Ptr(loan)!=layout.Module+layout.LoanVt || r.Ptr(loan+8)!=player.Person)
                        throw new InvalidDataException("当前租借合同未通过检查");
                    var borrowing=Team(r.Ptr(loan+layout.ContractTeam));
                    if(borrowing==null || borrowing.Club!=current.Club || owner.Club==current.Club)
                        throw new InvalidDataException("租借归属与当前效力球队不一致");
                    try{info.LoanEnd=Birthdays.Decode(BitConverter.ToUInt32(r.Bytes(loan+layout.ContractExpiry,4),0));}catch{info.LoanEnd=null;}
                    if(today.HasValue && info.LoanEnd.HasValue && info.LoanEnd.Value.Date<today.Value.Date)
                        throw new InvalidDataException("当前指针保留了已到期租借，需重新读取确认");
                    info.Loan=GrowthLoanState.Active;info.Note="租借合同、所属人及当前效力俱乐部已核对";
                }
                if(!r.Matches(read))throw new InvalidDataException("租借或合同资料正在变化");
            }
            catch(Exception ex){info.Loan=GrowthLoanState.Unknown;info.Note=ex.Message;}
            return info;
        }
        void ClubTeams(Club club)
        {
            try
            {
                var r=new EligibilityReader(read);ClubName(r,club.Addr);
                var header=r.Bytes(club.Addr+layout.ClubTeams,24);ulong begin=BitConverter.ToUInt64(header,0),end=BitConverter.ToUInt64(header,8),cap=BitConverter.ToUInt64(header,16);
                if(begin==0 && end==0)return;
                if(begin<0x10000 || end<begin || cap<end || (end-begin)%8!=0 || (cap-begin)%8!=0 || cap-begin>64*8)
                    throw new InvalidDataException("分队列表异常");
                var bytes=r.Bytes(begin,(int)(end-begin));var list=new List<GrowthTeam>();
                for(int i=0;i<bytes.Length;i+=8){var team=Team(BitConverter.ToUInt64(bytes,i));if(team!=null && list.All(t=>t.Address!=team.Address))list.Add(team);}
                if(r.Matches(read))result.ClubTeams[club.Addr]=list;
            }
            catch{}
        }
        void Affiliations(Club club)
        {
            if(!layout.Labels)return;
            try
            {
                var r=new EligibilityReader(read);var entries=r.Pointers(club.Addr+0x118);if(entries.Length>128)return;
                var related=new List<GrowthRelatedClub>();
                foreach(ulong entry in entries)
                {
                    var b=r.Bytes(entry,0x38);ulong parent=BitConverter.ToUInt64(b,0),feeder=BitConverter.ToUInt64(b,8);
                    // Only the actual parent -> feeder link is offered. An unclassified affiliate is never silently a B team.
                    if(parent!=club.Addr || feeder==club.Addr || feeder<0x10000 || b[0x30]==25)continue;
                    if(ClubName(r,feeder)!=null)related.Add(new GrowthRelatedClub{Club=feeder,Type=b[0x30]});
                }
                if(r.Matches(read))result.Related[club.Addr]=related.GroupBy(x=>x.Club).Select(g=>g.First()).ToList();
            }
            catch{}
        }
        public GrowthDirectory Capture(IList<Player> players,IList<Club> clubs,DateTime? today,CancellationToken token=default(CancellationToken))
        {
            foreach(var club in clubs)result.ClubNames[club.Addr]=club.Name;
            foreach(var club in clubs){token.ThrowIfCancellationRequested();ClubTeams(club);Affiliations(club);}
            foreach(var player in players){token.ThrowIfCancellationRequested();player.Roster=Membership(player,today);}
            foreach(var team in result.Teams.Values)
            {
                List<GrowthTeam> list;
                if(!result.ClubTeams.TryGetValue(team.Club,out list))result.ClubTeams[team.Club]=list=new List<GrowthTeam>();
                if(list.All(t=>t.Address!=team.Address))list.Add(team);
            }
            foreach(var list in result.ClubTeams.Values)
                foreach(var duplicate in list.GroupBy(t=>t.Name).Where(g=>g.Count()>1))
                    foreach(var team in duplicate)team.Name=team.Name+" · "+(string.IsNullOrEmpty(team.Competition)?"队伍 "+(list.IndexOf(team)+1):team.Competition);
            result.Note="已读取 "+result.Teams.Count+" 支分队；"+players.Count(p=>p.Roster.Loan==GrowthLoanState.Active)+" 名租借球员，"+players.Count(p=>p.Roster.Loan==GrowthLoanState.Unknown)+" 名租借状态待确认。";
            return result;
        }
    }
    enum GrowthSquadKind { All, Team, OutOnLoan, Related, Unknown }
    sealed class GrowthSquadChoice
    {
        public GrowthSquadKind Kind;
        public ulong Club, Team, SourceClub;
        public GrowthSquadChoice[] Parts;
        public string Key="all", Name;
        public override string ToString(){return Name;}
    }
    static class GrowthRosterView
    {
        public static List<GrowthSquadChoice> Choices(GrowthDirectory directory,Club club)
        {
            if(club==null)return new List<GrowthSquadChoice>();
            var list=new List<GrowthSquadChoice>{new GrowthSquadChoice{Kind=GrowthSquadKind.All,Club=club.Addr,Name="全部本队分队"}};
            foreach(var team in directory.ForClub(club.Addr).OrderByDescending(t=>t.First).ThenByDescending(t=>t.AgeLimit).ThenBy(t=>t.Name))
                list.Add(new GrowthSquadChoice{Kind=GrowthSquadKind.Team,Club=club.Addr,Team=team.Address,Key=team.Key,Name=team.Club==club.Addr?team.Name:directory.ClubName(team.Club)+" · "+team.Name});
            list.Add(new GrowthSquadChoice{Kind=GrowthSquadKind.OutOnLoan,Club=club.Addr,Key="loaned-out",Name="外租球员（已租出）"});
            list.Add(new GrowthSquadChoice{Kind=GrowthSquadKind.Unknown,Club=club.Addr,Key="unknown",Name="分队待确认"});
            List<GrowthRelatedClub> related;
            if(directory.Related.TryGetValue(club.Addr,out related))foreach(var child in related)
                list.Add(new GrowthSquadChoice{Kind=GrowthSquadKind.Related,Club=child.Club,Key="related:"+child.Club,Name="关联俱乐部："+directory.ClubName(child.Club)});
            foreach(var choice in list)choice.SourceClub=club.Addr;
            return list;
        }
        public static List<GrowthSquadChoice> ChoicesForClubs(GrowthDirectory directory,IList<Club> clubs)
        {
            if(clubs.Count==0)return new List<GrowthSquadChoice>();
            if(clubs.Count==1)return Choices(directory,clubs[0]);
            var individual=new List<GrowthSquadChoice>();
            foreach(var club in clubs)
                foreach(var choice in Choices(directory,club))
                {
                    choice.Name=club.Name+" · "+choice.Name;
                    choice.Key=club.Addr+"/"+choice.Key;
                    individual.Add(choice);
                }
            var choices=new List<GrowthSquadChoice>();
            foreach(var kind in new[]{GrowthSquadKind.All,GrowthSquadKind.OutOnLoan,GrowthSquadKind.Unknown})
            {
                string name=kind==GrowthSquadKind.All?"所选俱乐部 · 全部分队":
                    kind==GrowthSquadKind.OutOnLoan?"所选俱乐部 · 已租出":"所选俱乐部 · 分队待确认";
                choices.Add(new GrowthSquadChoice{Kind=kind,Key="combined/"+kind,Name=name,
                    Parts=individual.Where(choice=>choice.Kind==kind).ToArray()});
            }
            choices.AddRange(individual);
            return choices;
        }
        public static bool Contains(GrowthDirectory directory,GrowthSquadChoice scope,Player p)
        {
            if(scope==null)return false;var m=p.Roster;
            if(scope.Parts!=null)return scope.Parts.Any(part=>Contains(directory,part,p));
            if(scope.Kind==GrowthSquadKind.OutOnLoan)return m!=null && m.Loan==GrowthLoanState.Active && m.ContractClub==scope.Club && m.PlayingClub!=scope.Club;
            if(scope.Kind==GrowthSquadKind.Team)return m!=null && m.Team==scope.Team;
            bool atClub=p.Club==scope.Club || m!=null && m.Contract!=0 && directory.ForClub(scope.Club).Any(t=>t.Address==m.Team);
            if(scope.Kind==GrowthSquadKind.Unknown)return atClub && (m==null || !directory.Teams.ContainsKey(m.Team));
            return atClub;
        }
        public static string LoanLabel(Player p,ulong club)
        {
            var m=p.Roster;if(m==null || m.Loan==GrowthLoanState.Unknown)return "待确认";
            if(m.Loan==GrowthLoanState.None)return "未租借";
            if(m.ContractClub==club && m.PlayingClub!=club)return "租出";
            if(m.PlayingClub==club && m.ContractClub!=club)return "租入";
            return "租借";
        }
    }
}
