// Modified build notice: FM27E0-CAgrowth v1.1.21; changes dated 2026-11-19.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt. See docs/current-build.md.
using System;
using System.Linq;

namespace Fm24Editor
{
    static class CurrentBuildProfile
    {
        public const string Build = "27.0.1+2503222";
        public const ulong ImageSize = 0x185FA000, DateRoot = 0x580FF30, DateVtable = 0x4D908D8,
            RetirementRoot = 0x5812D50, RetirementVtable = 0x4FCFA88;

        static NativeEngineLayout active=NativeEngineLayout.Legacy;
        public static NativeEngineLayout Active {get{return active;}private set{active=value;}}
        public static void Reset(){Active=NativeEngineLayout.Legacy;}
        public static void Select(string version,ulong size)
        {var layout=NativeEngineLayout.For(version,size);if(layout==null)throw new ArgumentException("当前构建没有已核实的结构配置。");Active=layout;}
        public static bool Matches(FmProcess fm)
        {return fm!=null && fm.VerifiedCurrentBuild && object.ReferenceEquals(Active,NativeEngineLayout.For(fm.Version,fm.ImageSize));}

        public static bool Supported(string version, ulong imageSize, Func<ulong,int,byte[]> read, ulong moduleBase)
        {
            var layout=NativeEngineLayout.For(version,imageSize);
            return layout!=null && layout.Verify(read,moduleBase);
        }

        public static void Apply(){Apply(NativeEngineLayout.Legacy);}
        public static void Apply(NativeEngineLayout layout)
        {
            if(layout==null)throw new ArgumentNullException("layout");Active=layout;
            // Keep structural safety checks; select the actual types and measured fields.
            // Generic recovery can miss contracts when the first samples are unsigned players,
            // and must not mistake compact finances for the full editable finance object.
            FmProcess.PersonVtRva=layout.Person;FmProcess.PlayerCoachVtRva=layout.Coach;
            FmProcess.StaffVtRva=layout.Staff;FmProcess.ManagerVtRva=layout.Manager;
            FmProcess.ContractVtRva=layout.Contract;FmProcess.ClubVtRva=layout.Club;
            FmProcess.FinanceVtRva=layout.Finance;FmProcess.TeamVtRva=layout.Team;
            FmProcess.CityVtRva=layout.City;FmProcess.StadiumVtRva=layout.Stadium;
            FmProcess.StageVtRva=FmProcess.CompetitionVtRva=layout.Competition;
            FmProcess.CompRulesVtRva=layout.RulesHolder;FmProcess.SeasonVtRva=layout.Season;FmProcess.SeasonStageVtRva=layout.SeasonStage;
            FmLayout.FacilityData = new ulong[] { 0x120, 0x12B, 0x12C, 0x12D, 0 };
            FmLayout.FacilityFin = new ulong[] { 0, 0, 0, 0, layout.CorporateFacility };
            FmLayout.FinTicketPrice = 0x10;
            FmProcess.LoanContractVtRva=layout.Loan;
            if(layout.DirectDate!=0)FmProcess.CurrentDateRva=layout.DirectDate;
            FmLayout.StatusBeforePerson = new ulong[] { 0x2C, 0x30, 0x1C };
            FmLayout.StatusMax = new int[] { 10000, 10000, 20 };
            FmLayout.StatusSize = new int[] { 2, 2, 1 };
            FmLayout.MedicalBeforePerson = 0x190;
            FmLayout.JadedRested = 0;
            FmLayout.Groups = FmLayout.Groups.Concat(new[] { "status", "injury", "jaded" }).Distinct().ToArray();
        }

        public static ulong DateAddress(Func<ulong,int,byte[]> read, ulong moduleBase)
        {
            try
            {
                var reader = new EligibilityReader(read);
                ulong address=Active.CaptureDate(reader,moduleBase);
                return reader.Matches(read) ? address : 0;
            }
            catch { return 0; }
        }

        public static ulong RetirementManager(Func<ulong,int,byte[]> read, ulong moduleBase)
        {
            int gap;
            return RetirementManager(read,moduleBase,out gap);
        }

        public static ulong RetirementManager(Func<ulong,int,byte[]> read, ulong moduleBase,out int gap)
        {
            gap=0;
            try
            {
                var reader = new EligibilityReader(read);
                ulong manager=reader.Ptr(moduleBase+Active.RetirementRoot);
                int verifiedGap=Active.RetirementGap(reader,moduleBase,manager);
                if(!reader.Matches(read))return 0;
                gap=verifiedGap;
                return manager;
            }
            catch { return 0; }
        }
    }
}
