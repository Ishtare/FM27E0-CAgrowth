// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt. See docs/current-features.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    static class StatusValues
    {
        public static int Decode(byte[] bytes,int index)
        {
            if((FmLayout.StatusSize[index]!=1 && FmLayout.StatusSize[index]!=2) || bytes==null || bytes.Length!=FmLayout.StatusSize[index])return -1;
            int value=bytes.Length==1?bytes[0]:BitConverter.ToUInt16(bytes,0);
            return value<=FmLayout.StatusMax[index]?value:-1;
        }
        public static byte[] Encode(int value,int index)
        {
            if((FmLayout.StatusSize[index]!=1 && FmLayout.StatusSize[index]!=2) || value<0 || value>FmLayout.StatusMax[index])throw new ArgumentOutOfRangeException("value");
            return FmLayout.StatusSize[index]==1?new[]{(byte)value}:BitConverter.GetBytes((ushort)value);
        }
    }

    sealed class CurrentFieldSnapshot
    {
        public EligibilityReader Reader;
        public ulong Address;
        public byte[] Before;
        public static ulong Player(EligibilityReader r,ulong module,Player player)
        {
            ulong obj=player.Tail-0x288;
            var layout=CurrentBuildProfile.Active;
            ulong objectVt=player.Extra==0?layout.Player:layout.CoachPlayer;
            ulong personVt=player.Extra==0?layout.Person:layout.Coach;
            if((player.Extra!=0 && player.Extra!=0xF8) || r.Ptr(obj)!=module+objectVt || r.Ptr(player.Person)!=module+personVt || r.Int(player.Person+12)!=player.Uid)
                throw new InvalidDataException("球员身份或数据位置已变化，请重新扫描。");
            return obj;
        }
        public static CurrentFieldSnapshot Capture(Func<ulong,int,byte[]> read,ulong module,Player player,ulong address,int size)
        {
            var s=new CurrentFieldSnapshot {Reader=new EligibilityReader(read),Address=address};
            Player(s.Reader,module,player);s.Before=s.Reader.Bytes(address,size);
            if(!s.Reader.Matches(read))throw new InvalidDataException("游戏正在更新球员数据。");
            return s;
        }
        public List<EligibilityEdit> Plan(byte[] after,string label)
        {
            if(after==null || after.Length!=Before.Length)throw new ArgumentException("修改长度不一致。");
            return Before.SequenceEqual(after)?new List<EligibilityEdit>():new List<EligibilityEdit>{new EligibilityEdit{Address=Address,Before=Before,After=after,Label=label}};
        }
    }

    sealed class CurrentInjurySnapshot
    {
        public EligibilityReader Reader;
        public ulong Holder,Begin,End,Capacity;
        public int Count;
        public static CurrentInjurySnapshot Capture(Func<ulong,int,byte[]> read,ulong module,Player player)
        {
            var s=new CurrentInjurySnapshot {Reader=new EligibilityReader(read)};var r=s.Reader;
            ulong obj=CurrentFieldSnapshot.Player(r,module,player);s.Holder=r.Ptr(obj+0xF8);
            if(s.Holder!=0)
            {
                s.Begin=r.Ptr(s.Holder);s.End=r.Ptr(s.Holder+8);s.Capacity=r.Ptr(s.Holder+16);
                bool empty=s.Begin==0 && s.End==0 && s.Capacity==0;
                if(!empty && (s.Begin<0x10000 || s.End<s.Begin || s.Capacity<s.End || (s.End-s.Begin)%8!=0 || (s.Capacity-s.Begin)%8!=0 || s.Capacity-s.Begin>32*8))
                    throw new InvalidDataException("伤病列表结构未通过检查。");
                s.Count=(int)((s.End-s.Begin)/8);
                var seen=new HashSet<ulong>();
                for(ulong a=s.Begin;a<s.End;a+=8)
                {
                    ulong record=r.Ptr(a);if(!seen.Add(record))throw new InvalidDataException("伤病记录重复。");
                    var bytes=r.Bytes(record,0x38);ulong type=BitConverter.ToUInt64(bytes,8);
                    if(r.Ptr(type)!=module+CurrentBuildProfile.Active.Injury)throw new InvalidDataException("伤病类型未通过检查。");
                    RetirementSnapshot.Decode(BitConverter.ToUInt32(bytes,0x20));
                }
            }
            if(!r.Matches(read))throw new InvalidDataException("伤病列表已变化，请重新打开。");
            return s;
        }
        public List<EligibilityEdit> Plan()
        {
            return Count==0?new List<EligibilityEdit>():new List<EligibilityEdit>{new EligibilityEdit{Address=Holder+8,Before=BitConverter.GetBytes(End),After=BitConverter.GetBytes(Begin),Label="移除 "+Count+" 项当前伤病"}};
        }
    }

    sealed class CurrentShirtSnapshot
    {
        public EligibilityReader Reader;
        public ulong Contract,Address;
        public byte Number;
        public bool Loan;
        static ulong Club(EligibilityReader r,ulong module,ulong team)
        {
            if(r.Ptr(team)!=module+EligibilityLayout.TeamVt)throw new InvalidDataException("球队类型未通过检查。");
            ulong club=r.Ptr(team+0x30);if(club==0)return 0;
            if(r.Ptr(club)!=module+EligibilityLayout.ClubVt)throw new InvalidDataException("球队所属俱乐部未通过检查。");return club;
        }
        public static CurrentShirtSnapshot Capture(Func<ulong,int,byte[]> read,ulong module,Player player)
        {
            var s=new CurrentShirtSnapshot {Reader=new EligibilityReader(read)};var r=s.Reader;
            ulong obj=CurrentFieldSnapshot.Player(r,module,player),team=r.Ptr(obj+0x130),contract=r.Ptr(player.Person+0xA8);
            if(team==0 || contract==0)return null;
            if(r.Ptr(contract)!=module+EligibilityLayout.ContractVt || r.Ptr(contract+8)!=player.Person)throw new InvalidDataException("球员合同所属人不一致。");
            ulong currentClub=Club(r,module,team),parent=r.Ptr(contract+0x10);
            if(currentClub==0 || parent==0)return null;
            ulong parentClub=Club(r,module,parent);if(parentClub==0)return null;
            s.Contract=contract;
            if(parentClub!=currentClub)
            {
                ulong holder=r.Ptr(obj+0x338);if(holder==0)return null;
                ulong loan=r.Ptr(holder);if(loan==0)return null;
                if(r.Ptr(loan)!=module+CurrentBuildProfile.Active.Loan || r.Ptr(loan+8)!=player.Person || Club(r,module,r.Ptr(loan+0x10))!=currentClub)
                    throw new InvalidDataException("租借合同与球员当前俱乐部不一致。");
                s.Contract=loan;s.Loan=true;
            }
            s.Address=s.Contract+0x5D;s.Number=r.Byte(s.Address);
            if(s.Number!=255 && (s.Number<1 || s.Number>99))throw new InvalidDataException("球衣号码无效。");
            if(!r.Matches(read))throw new InvalidDataException("球员的合同或球队已变化。");
            return s;
        }
        public List<EligibilityEdit> Plan(int number)
        {
            if(number!=255 && (number<1 || number>99))throw new ArgumentException("球衣号码为 1–99，未设置使用 255。");
            return number==Number?new List<EligibilityEdit>():new List<EligibilityEdit>{new EligibilityEdit{Address=Address,Before=new[]{Number},After=new[]{(byte)number},Label=(Loan?"租借":"本队")+"球衣号码 "+Number+" → "+number}};
        }
    }

    sealed class CurrentTicketSnapshot
    {
        public EligibilityReader Reader;
        public ulong CountAddress,PriceAddress;
        public int SeasonTickets;
        public float Price;
        public static bool ValidPrice(float value){return !float.IsNaN(value) && !float.IsInfinity(value) && (value==-1 || value>=0 && value<=2000);}
        public static CurrentTicketSnapshot Capture(Func<ulong,int,byte[]> read,ulong module,Club club)
        {
            var s=new CurrentTicketSnapshot {Reader=new EligibilityReader(read)};var r=s.Reader;
            if(r.Ptr(club.Addr)!=module+EligibilityLayout.ClubVt || r.Ptr(club.Addr+0x150)!=club.Finance || r.Ptr(club.Finance)!=module+CurrentBuildProfile.Active.Finance || r.Ptr(club.Addr+0x100)!=club.Data)
                throw new InvalidDataException("俱乐部或完整财务对象已变化。");
            r.Int(club.Addr+12);s.CountAddress=club.Data+0xC0;s.PriceAddress=club.Finance+0x10;
            s.SeasonTickets=r.Int(s.CountAddress);s.Price=BitConverter.ToSingle(r.Bytes(s.PriceAddress,4),0);
            if(s.SeasonTickets<0 || s.SeasonTickets>300000 || !ValidPrice(s.Price))throw new InvalidDataException("票价或季票资料未通过检查。");
            if(!r.Matches(read))throw new InvalidDataException("票价或季票资料正在更新。");return s;
        }
        public List<EligibilityEdit> Plan(int? tickets,float? price)
        {
            if(tickets.HasValue && (tickets<0 || tickets>300000))throw new ArgumentOutOfRangeException("tickets");
            if(price.HasValue && (!ValidPrice(price.Value) || price<0))throw new ArgumentOutOfRangeException("price");
            var list=new List<EligibilityEdit>();
            if(tickets.HasValue && tickets!=SeasonTickets)list.Add(new EligibilityEdit{Address=CountAddress,Before=BitConverter.GetBytes(SeasonTickets),After=BitConverter.GetBytes(tickets.Value),Label="季票持有者 "+SeasonTickets+" → "+tickets});
            if(price.HasValue && price!=Price)list.Add(new EligibilityEdit{Address=PriceAddress,Before=BitConverter.GetBytes(Price),After=BitConverter.GetBytes(price.Value),Label="平均票价 £"+(Price<0?"未设置":Price.ToString("0.00"))+" → £"+price.Value.ToString("0.00")});
            return list;
        }
    }
}
