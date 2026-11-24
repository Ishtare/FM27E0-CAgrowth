// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Fm24Editor
{
    // Discover clubs from their records in the loaded save, independently of their finances.
    sealed class ClubCatalogReader
    {
        const ulong AddressLimit=0x800000000000UL;
        const int UidOffset=12;
        readonly Func<ulong,int,byte[]> read;
        readonly Func<ulong,string> readName;
        readonly ulong clubType, teamType, fullFinanceType, compactFinanceType;
        readonly int headerLength;

        public ClubCatalogReader(Func<ulong,int,byte[]> read,Func<ulong,string> readName,ulong module,ulong compactFinanceRva)
        {
            this.read=read;
            this.readName=readName;
            clubType=module+FmProcess.ClubVtRva;
            teamType=module+FmProcess.TeamVtRva;
            fullFinanceType=FmProcess.FinanceVtRva==0?0:module+FmProcess.FinanceVtRva;
            compactFinanceType=compactFinanceRva==0?0:module+compactFinanceRva;
            headerLength=checked((int)new[]{FmLayout.ClubTeams+24,FmLayout.ClubFinance+8,FmLayout.ClubData+8,
                FmLayout.ClubName+8,FmLayout.ClubShortName+8,16UL}.Max());
        }

        static bool IsAddress(ulong address)
        {
            return address>=0x10000 && address<AddressLimit;
        }

        ulong PointerAt(ulong address)
        {
            if(!IsAddress(address))return 0;
            var bytes=read(address,8);
            return bytes!=null && bytes.Length==8?BitConverter.ToUInt64(bytes,0):0;
        }

        bool OwnsTeam(ulong team,ulong club)
        {
            return IsAddress(team) && PointerAt(team)==teamType && PointerAt(team+FmLayout.TeamClub2)==club;
        }

        bool FindOwnedTeam(ulong address,byte[] header,bool needsCorroboration,out ulong firstTeam)
        {
            firstTeam=0;
            int offset=(int)FmLayout.ClubTeams;
            ulong begin=BitConverter.ToUInt64(header,offset),end=BitConverter.ToUInt64(header,offset+8);
            ulong capacity=BitConverter.ToUInt64(header,offset+16);
            if(!IsAddress(begin) || begin%8!=0 || end<=begin || capacity<end || !IsAddress(capacity) ||
                (end-begin)%8!=0 || (capacity-begin)%8!=0 || end-begin>64*8 || capacity-begin>64*32)return false;

            ulong first=PointerAt(begin);
            if(OwnsTeam(first,address))
            {
                firstTeam=first;
                return true;
            }
            // A later verified squad can establish the club's identity. Keep first-team
            // fields unavailable if the first entry itself could not be verified.
            if(needsCorroboration)
                for(ulong slot=begin+8;slot<end;slot+=8)
                    if(OwnsTeam(PointerAt(slot),address))return true;
            return false;
        }

        string NameAt(byte[] header,ulong offset)
        {
            ulong address=BitConverter.ToUInt64(header,(int)offset);
            if(!IsAddress(address))return null;
            var value=readName(address);
            return string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)?null:value;
        }

        Club ReadClub(ulong address)
        {
            if(!IsAddress(address))return null;
            var header=read(address,headerLength);
            if(header==null || header.Length!=headerLength || BitConverter.ToUInt64(header,0)!=clubType)return null;
            int uid=BitConverter.ToInt32(header,UidOffset);
            if(uid<=0)return null;
            ulong finance=BitConverter.ToUInt64(header,(int)FmLayout.ClubFinance);
            ulong financeType=PointerAt(finance);
            bool compact=compactFinanceType!=0 && financeType==compactFinanceType;
            bool full=!compact && fullFinanceType!=0 && financeType==fullFinanceType;
            ulong firstTeam;
            bool hasOwnedTeam=FindOwnedTeam(address,header,!full && !compact,out firstTeam);
            if(!full && !compact && !hasOwnedTeam)return null;
            var fullName=NameAt(header,FmLayout.ClubName);
            var shortName=NameAt(header,FmLayout.ClubShortName);
            if(fullName==null && shortName==null)return null;
            return new Club{Addr=address,Uid=uid,Name=fullName??shortName,ShortName=shortName,
                Finance=full?finance:0,CompactFinance=compact,Team=firstTeam,Data=BitConverter.ToUInt64(header,(int)FmLayout.ClubData)};
        }

        public List<Club> Read(IEnumerable<ulong> hits,CancellationToken token)
        {
            var clubs=new List<Club>();
            foreach(var address in hits.Distinct())
            {
                token.ThrowIfCancellationRequested();
                var club=ReadClub(address);
                if(club!=null)clubs.Add(club);
            }
            return clubs;
        }

        public static bool MatchesIdentity(FmProcess fm,Club club)
        {
            if(fm==null || club==null || club.Uid<=0 || !IsAddress(club.Addr))return false;
            var identity=fm.Read(club.Addr,UidOffset+4);
            return identity!=null && BitConverter.ToUInt64(identity,0)==fm.Base+FmProcess.ClubVtRva &&
                BitConverter.ToInt32(identity,UidOffset)==club.Uid;
        }

        public static bool MatchesFullFinance(FmProcess fm,Club club)
        {
            return club!=null && club.HasFullFinance && MatchesIdentity(fm,club) &&
                fm.ReadPtr(club.Addr+FmLayout.ClubFinance)==club.Finance &&
                fm.ReadPtr(club.Finance)==fm.Base+FmProcess.FinanceVtRva;
        }
    }

    static class ClubSearch
    {
        public const string Cue="俱乐部名称／UID…";
        static readonly CompareInfo NameComparison=CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions NameOptions=CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace|CompareOptions.IgnoreWidth;
        // The patch author uses 东方巨龙. The full and abbreviated names are also in FPF records.
        static readonly string[] DragonNames={"东方巨龙","东方巨龙足球俱乐部","东方龙","东方龙足球俱乐部",
            "Oriental Dragon","Oriental Dragon FC","Oriental Dragon F.C.","Oriental Dragon Football Club"};

        static bool Contains(string value,string query)
        {
            return !string.IsNullOrEmpty(value) && NameComparison.IndexOf(value,query,NameOptions)>=0;
        }

        public static bool Matches(Club club,string query)
        {
            query=(query??"").Trim();
            if(query.Length==0)return true;
            int uid;
            // Preserve number fragments such as 1907 in club names. Explicit UID queries are exact.
            if(PlayerNames.TryUid(query,out uid) && (uid==club.Uid || !query.All(char.IsDigit)))return uid==club.Uid;
            return MatchesName(club.Name,club.ShortName,query);
        }

        public static bool MatchesName(string fullName,string shortName,string query)
        {
            query=(query??"").Trim();
            if(query.Length==0)return true;
            if(Contains(fullName,query) || Contains(shortName,query))return true;
            if(!DragonNames.Contains(fullName,StringComparer.OrdinalIgnoreCase) &&
                !DragonNames.Contains(shortName,StringComparer.OrdinalIgnoreCase))return false;
            return DragonNames.Any(name=>Contains(name,query));
        }
    }
}
