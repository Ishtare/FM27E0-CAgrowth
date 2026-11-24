// Modified build v1.1.21, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// The Steam layout was measured in the installed engine. The Game Pass layout uses the pinned public reference.
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    sealed class NativeEngineSignature
    {
        public readonly ulong Rva;
        public readonly byte[] Bytes;
        public NativeEngineSignature(ulong rva,params byte[] bytes){Rva=rva;Bytes=(byte[])bytes.Clone();}
    }
    sealed class NativeEngineLayout
    {
        public readonly string Version;
        public readonly ulong ImageSize,Person,Player,Coach,CoachPlayer,Staff,Manager,Contract,Club,Finance,CompactFinance,Team,City,Stadium,Nation,Competition,RulesHolder,RulesContainer,Season,SeasonStage,Loan,Injury;
        public readonly ulong DateRoot,DateType,DirectDate,RetirementRoot,RetirementType,RetirementSecondary,NonEuCheck;
        public readonly NativeEngineSignature[] Signatures;
        public readonly string Channel;
        public readonly uint PeTimestamp;
        public readonly ulong CorporateFacility, PlayerDirectory, GameUiRoot, GameUiType;
        public readonly bool PlayerRegistrationEditing;
        NativeEngineLayout(string version,ulong size,ulong[] types,ulong dateRoot,ulong dateType,ulong directDate,ulong retirementRoot,ulong retirementType,ulong retirementSecondary,ulong nonEu,params NativeEngineSignature[] signatures)
            : this(version,size,types,dateRoot,dateType,directDate,retirementRoot,retirementType,retirementSecondary,nonEu,"Steam",0,0x8A9,0x5820810,0,0,true,signatures)
        {
        }
        NativeEngineLayout(string version,ulong size,ulong[] types,ulong dateRoot,ulong dateType,ulong directDate,ulong retirementRoot,ulong retirementType,ulong retirementSecondary,ulong nonEu,
            string channel,uint peTimestamp,ulong corporateFacility,ulong playerDirectory,ulong gameUiRoot,ulong gameUiType,
            bool playerRegistrationEditing,params NativeEngineSignature[] signatures)
        {
            Version=version;ImageSize=size;Person=types[0];Player=types[1];Coach=types[2];CoachPlayer=types[3];Staff=types[4];Manager=types[5];Contract=types[6];Club=types[7];Finance=types[8];CompactFinance=types[9];
            Team=types[10];City=types[11];Stadium=types[12];Nation=types[13];Competition=types[14];RulesHolder=types[15];RulesContainer=types[16];Season=types[17];SeasonStage=types[18];Loan=types[19];Injury=types[20];
            DateRoot=dateRoot;DateType=dateType;DirectDate=directDate;RetirementRoot=retirementRoot;RetirementType=retirementType;RetirementSecondary=retirementSecondary;NonEuCheck=nonEu;Signatures=signatures;
            Channel=channel;PeTimestamp=peTimestamp;CorporateFacility=corporateFacility;PlayerDirectory=playerDirectory;GameUiRoot=gameUiRoot;GameUiType=gameUiType;
            PlayerRegistrationEditing=playerRegistrationEditing;
        }
        public static readonly NativeEngineLayout Legacy=new NativeEngineLayout("27.0.1+2503222",0x185FA000,
            new ulong[]{0x4E2BA18,0x4E2B4E8,0x50901E8,0x508FCB8,0x4E908B8,0x4DC4938,0x4C04B08,0x4DDE298,0x4DF4E38,0x4DF4968,0x4DE51A8,0x4EEB5B8,0x4EF6828,0x4DE36F8,0x4DE3B78,0x4DE6068,0x4DF0228,0x4ED3C48,0x4ED6218,0x4ECDCF8,0x4DB4278},
            0x580FF30,0x4D908D8,0,0x5812D50,0x4FCFA88,0x4FCFA98,0x162C88B,
            new NativeEngineSignature(0x162C88B,0xF6,0x81,0x78,0x02,0,0,8));
        public static readonly NativeEngineLayout Update271=new NativeEngineLayout("27.1.0+2505117",0x18878000,
            new ulong[]{0x4E89908,0x4E893D8,0x50EB158,0x50EAC28,0x4EECF78,0x4E21778,0x4C5DFA8,0x4E3C1C8,0x4E52C38,0x4E52768,0x4E42F38,0x4F478C8,0x4F52C08,0x4E41708,0x4E41B88,0x4E43DF8,0x4E4E018,0x4F30088,0x4F32658,0x4F2A0F8,0x4DF3DB8},
            0,0,0x58268E0,0x586F850,0x5036E30,0x5036E48,0x16758CB,"Steam",0,0x8AD,0x587B8B8,0x587C470,0x4C3ED08,true,
            new NativeEngineSignature(0x16758CB,0xF6,0x81,0x78,0x02,0,0,8),
            // Current packed date passed to the native year/day comparator (0x16E66D0).
            new NativeEngineSignature(0x23A2A5,0x48,0x8D,0x0D,0x34,0xC6,0x5E,0x05,0x4C,0x89,0xE2,0xE8,0x1C,0xC4,0x4A,0x01),
            // Factory stores the typed retirement manager at module+0x586F850.
            new NativeEngineSignature(0xC20AF5,0x48,0x89,0x35,0x54,0xED,0xC4,0x04),
            // The native player reset writes fitness 10000 to the same two-byte field.
            new NativeEngineSignature(0x16445A3,0x66,0xC7,0x86,0x5C,0x02,0,0,0x10,0x27),
            new NativeEngineSignature(0x147FBF0,0x0F,0xB6,0x81,0xAD,0x08,0,0));

        // Measured Windows Game Pass layout. See docs/xbox-pc-and-roster.md for the pinned public reference.
        public static readonly NativeEngineLayout GamePass271=new NativeEngineLayout("27.1.0+2505117",0x179AD000,
            new ulong[]{0x4A95638,0x4A95108,0x4C9F1E8,0x4C9ECB8,0x4AF4318,0x4A47238,0x4897AA8,0x4A5E628,0x4A707B8,0x4A702E8,0x4A63288,0x4B40878,0x4B47FE8,0x4A621A8,0x4A62638,0x4A63F58,0x4A6C0C8,0x4B2DF88,0x4B301E8,0x4B2A708,0x4A1C0C8},
            0x53EAD60,0x4A26E28,0,0x53EDBA0,0x4BFB820,0,0,"Xbox PC / Game Pass",0x6AF5C8C6,0x8AD,0x53FAE88,0x53FBA40,0x4878858,true,
            new NativeEngineSignature(0x48785B3,Encoding.ASCII.GetBytes("27.1.0+2505117")));

        // Steam 27.1.1 has its own engine identity, types and native code proofs.
        public static readonly NativeEngineLayout Steam2711=new NativeEngineLayout("27.1.1+2511875",0x1849A000,
            new ulong[]{0x4E89908,0x4E893D8,0x50EB028,0x50EAAF8,0x4EECF78,0x4E21778,0x4C5DFB8,0x4E3C1C8,0x4E52C38,0x4E52768,0x4E42F38,0x4F478C8,0x4F52B58,0x4E41708,0x4E41B88,0x4E43DF8,0x4E4E018,0x4F30088,0x4F32658,0x4F2A0F8,0x4DF3DB8},
            // Individual domestic/UEFA registrations were readable. The live save had no actionable edit to verify.
            0,0,0x58268E0,0x586F870,0x5036CF0,0x5036D08,0x1675D2B,"Steam",0x6AFF57A2,0x8AD,0x587B8B8,0x587C470,0x4C3ED08,false,
            new NativeEngineSignature(0x1675D2B,0xF6,0x81,0x78,0x02,0,0,8),
            new NativeEngineSignature(0x23A6E5,0x48,0x8D,0x0D,0xF4,0xC1,0x5E,0x05,0x4C,0x89,0xE2,0xE8,0x3C,0xC4,0x4A,0x01),
            new NativeEngineSignature(0xC20E95,0x48,0x89,0x35,0xD4,0xE9,0xC4,0x04),
            new NativeEngineSignature(0x1644A03,0x66,0xC7,0x86,0x5C,0x02,0,0,0x10,0x27),
            new NativeEngineSignature(0x1480050,0x0F,0xB6,0x81,0xAD,0x08,0,0,0xC3));

        static readonly NativeEngineLayout[] Layouts={Legacy,Update271,GamePass271,Steam2711};
        public static NativeEngineLayout For(string version,ulong size)
        {return Layouts.FirstOrDefault(p=>p.Version==version && p.ImageSize==size);}
        public static bool KnownVersion(string version){return Layouts.Any(layout=>layout.Version==version);}
        public bool Verify(Func<ulong,int,byte[]> read,ulong module)
        {
            if(read==null)return false;
            try
            {
                if(PeTimestamp!=0)
                {
                    var dos=read(module,64);
                    if(dos==null || dos.Length!=64 || dos[0]!=0x4D || dos[1]!=0x5A)return false;
                    uint pe=BitConverter.ToUInt32(dos,0x3C);
                    if(pe<64 || pe>4096)return false;
                    var header=read(module+pe,84);
                    if(header==null || header.Length!=84 || BitConverter.ToUInt32(header,0)!=0x4550 ||
                        BitConverter.ToUInt16(header,4)!=0x8664 || BitConverter.ToUInt32(header,8)!=PeTimestamp ||
                        BitConverter.ToUInt16(header,24)!=0x20B || BitConverter.ToUInt32(header,80)!=ImageSize)return false;
                }
                return Signatures.All(s=>{var bytes=read(module+s.Rva,s.Bytes.Length);return bytes!=null && bytes.SequenceEqual(s.Bytes);});
            }
            catch{return false;}
        }
        public ulong? ReadSaveLoads(Func<ulong,int,byte[]> read,ulong module)
        {
            if(GameUiRoot==0)return null;
            try
            {
                var r=new EligibilityReader(read);
                ulong ui=r.Ptr(module+GameUiRoot);
                if(r.Ptr(ui)!=module+GameUiType)return null;
                ulong loads=r.Ptr(ui+0x100);
                return r.Matches(read)?(ulong?)loads:null;
            }
            catch{return null;}
        }
        public int RetirementGap(EligibilityReader r,ulong module,ulong manager)
        {
            ulong primary=r.Ptr(manager),secondary=r.Ptr(manager+8);
            if(primary!=module+RetirementType)throw new InvalidDataException("退役管理器类型未通过检查。");
            if(RetirementSecondary!=0)
            {
                if(secondary!=module+RetirementSecondary)throw new InvalidDataException("退役管理器副类型未通过检查。");
                return (int)(RetirementSecondary-RetirementType);
            }
            // The Game Pass reference identifies the primary type. Validate the live secondary type
            // by its MSVC RTTI descriptor and +8 subobject offset before using the two indexes.
            if((secondary!=primary+16 && secondary!=primary+24) ||
                RttiDescriptor(r,module,primary,0)!=RttiDescriptor(r,module,secondary,8))
                throw new InvalidDataException("退役管理器副类型未通过检查。");
            return (int)(secondary-primary);
        }
        uint RttiDescriptor(EligibilityReader r,ulong module,ulong table,uint offset)
        {
            ulong locator=r.Ptr(table-8);
            if(locator<module || locator-module>ImageSize-24)throw new InvalidDataException("退役类型资料无效。");
            var bytes=r.Bytes(locator,24);
            uint descriptor=BitConverter.ToUInt32(bytes,12);
            if(BitConverter.ToUInt32(bytes,0)!=1 || BitConverter.ToUInt32(bytes,4)!=offset || descriptor==0 ||
                descriptor>=ImageSize || BitConverter.ToUInt32(bytes,20)!=locator-module)
                throw new InvalidDataException("退役类型资料无效。");
            return descriptor;
        }
        public ulong CaptureDate(EligibilityReader reader,ulong module)
        {
            ulong address;
            if(DirectDate!=0)address=module+DirectDate;
            else
            {
                ulong manager=reader.Ptr(module+DateRoot);
                if(reader.Ptr(manager)!=module+DateType)throw new InvalidDataException("当前游戏日期未通过构建检查。");
                address=manager+0x7DC;
            }
            Birthdays.Decode(BitConverter.ToUInt32(reader.Bytes(address,4),0));return address;
        }
    }
}
