// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Fm24Editor
{
    static class NativeEngineTests
    {
        sealed class Fixture
        {
            public const ulong Module=0x140000000,Obj=0x20000,Person=Obj+0x288,Team=0x30000,ClubAt=0x40000,Finance=0x50000,Data=0x60000,Nation=0x70000,Contract=0x80000,Retire=0x90000,Medical=0xA0000,InjuryList=0xB0000,Record=0xC0000,InjuryType=0xD0000,DateManager=0xE0000;
            public readonly Dictionary<ulong,byte> Bytes=new Dictionary<ulong,byte>();
            public readonly NativeEngineLayout Layout;
            public readonly Player Player=new Player{Person=Person,Uid=123,Name="Offline update fixture"};
            public readonly Club Club=new Club{Addr=ClubAt,Data=Data,Finance=Finance,Team=Team,Name="Offline update club"};
            public int Writes;
            public Fixture(NativeEngineLayout layout)
            {
                Layout=layout;foreach(var sig in layout.Signatures)Put(Module+sig.Rva,sig.Bytes);
                if(layout.PeTimestamp!=0)
                {
                    Put(Module,new byte[0x40]);Put(Module,new byte[]{0x4D,0x5A});I(Module+0x3C,0x100);
                    Put(Module+0x100,new byte[84]);I(Module+0x100,0x4550);
                    Put(Module+0x104,BitConverter.GetBytes((ushort)0x8664));
                    Put(Module+0x108,BitConverter.GetBytes(layout.PeTimestamp));
                    Put(Module+0x118,BitConverter.GetBytes((ushort)0x20B));I(Module+0x150,(int)layout.ImageSize);
                }
                Put(Obj,new byte[0x400]);Q(Obj,Module+layout.Player);Q(Person,Module+layout.Person);I(Person+8,42);I(Person+12,123);
                Q(Obj+0x130,Team);Put(Team,new byte[0x100]);Q(Team,Module+layout.Team);Q(Team+0x30,ClubAt);I(Team+0x28,0);Put(Team+0x2C,new byte[]{100});
                Put(ClubAt,new byte[0x180]);Q(ClubAt,Module+layout.Club);I(ClubAt+12,456);Q(ClubAt+0x150,Finance);Q(ClubAt+0x100,Data);Q(ClubAt+0xD8,Nation);
                Put(Finance,new byte[0x900]);Q(Finance,Module+layout.Finance);Put(Finance+0x10,BitConverter.GetBytes(-1f));
                Put(Data,new byte[0x200]);I(Data+0xC0,100);foreach(ulong offset in new ulong[]{0x120,0x12B,0x12C,0x12D})Put(Data+offset,new byte[]{10});Put(Finance+layout.CorporateFacility,new byte[]{10});
                Put(Nation,new byte[0x180]);Q(Nation,Module+layout.Nation);I(Nation+12,776);
                Put(Contract,new byte[0x70]);Q(Obj+0x330,Contract);Q(Contract,Module+layout.Contract);Q(Contract+8,Person);Q(Contract+0x10,Team);Put(Contract+0x5D,new byte[]{7});
                Put(Obj+0x25C,BitConverter.GetBytes((ushort)9999));Put(Obj+0x258,BitConverter.GetBytes((ushort)8000));Put(Obj+0x25A,BitConverter.GetBytes((short)200));Put(Obj+0x26C,new byte[]{15});
                Q(Obj+0xF8,Medical);Q(Medical,InjuryList);Q(Medical+8,InjuryList+8);Q(Medical+16,InjuryList+8);Q(InjuryList,Record);Put(Record,new byte[0x38]);Q(Record+8,InjuryType);Q(InjuryType,Module+layout.Injury);Put(Record+0x20,BitConverter.GetBytes((2026u<<16)|194u));
                if(layout.DirectDate!=0)Put(Module+layout.DirectDate,BitConverter.GetBytes((2026u<<16)|234u));
                else{Q(Module+layout.DateRoot,DateManager);Q(DateManager,Module+layout.DateType);Put(DateManager+0x7DC,BitConverter.GetBytes((2026u<<16)|234u));}
                Put(Retire,new byte[0x118]);Q(Module+layout.RetirementRoot,Retire);Q(Retire,Module+layout.RetirementType);
                ulong second=layout.RetirementSecondary==0?layout.RetirementType+24:layout.RetirementSecondary;
                Q(Retire+8,Module+second);
                if(layout.RetirementSecondary==0)
                {
                    Rtti(layout.RetirementType,0x44000,0,0x50000);
                    Rtti(second,0x44020,8,0x50000);
                }
                if(layout.GameUiRoot!=0)
                {
                    ulong ui=0xAA000;
                    Q(Module+layout.GameUiRoot,ui);Q(ui,Module+layout.GameUiType);Q(ui+0x100,3);
                }
                Put(Person+0x18,new byte[]{0xA5}); // Residual announcement, other flags must remain intact.
                ulong registry=0xF0000;Q(Obj+0x300,registry);Put(registry,new byte[24]);Put(Obj+0x278,new byte[]{0xAD});
            }
            public void Rtti(ulong type,int rva,int offset,int descriptor)
            {
                Q(Module+type-8,Module+(ulong)rva);
                Put(Module+(ulong)rva,new byte[24]);
                I(Module+(ulong)rva,1);I(Module+(ulong)rva+4,offset);
                I(Module+(ulong)rva+12,descriptor);I(Module+(ulong)rva+20,rva);
            }
            public void Put(ulong at,byte[] bytes){for(int i=0;i<bytes.Length;i++)Bytes[at+(ulong)i]=bytes[i];}
            public void Q(ulong at,ulong value){Put(at,BitConverter.GetBytes(value));}
            public void I(ulong at,int value){Put(at,BitConverter.GetBytes(value));}
            public byte[] Read(ulong at,int count){var result=new byte[count];for(int i=0;i<count;i++)if(!Bytes.TryGetValue(at+(ulong)i,out result[i]))return null;return result;}
            public bool Write(ulong at,byte[] bytes){Writes++;Put(at,bytes);return true;}
            public FacilityLayout Facilities(){return new FacilityLayout(Read,Layout.Version,Module,Layout.ImageSize,true,true,Layout.Club,Layout.Finance,0x100,0x150,new ulong[]{0x120,0x12B,0x12C,0x12D,0},new ulong[]{0,0,0,0,Layout.CorporateFacility});}
        }
        static bool Reject(Action action){try{action();return false;}catch{return true;}}

        static void FinanceCalibrationCases(Action<bool,string> check)
        {
            var layout=NativeEngineLayout.Steam2711;
            var data=new Fixture(layout);
            ulong module=Fixture.Module,full=module+layout.Finance,compact=module+layout.CompactFinance;
            const ulong legacyRva=0x4CF1A08,staleRva=0x4CF0CC8;
            ulong legacy=module+legacyRva;
            var originalFinOffsets=new ulong[]{0,0,0x8A6,0x8A7,0x8A9};
            FmLayout.FacilityFin=originalFinOffsets;
            FmLayout.FinTicketPrice=0x864;
            FmProcess.FinanceVtRva=staleRva;
            FmProcess.CompactFinanceVtRva=0;

            data.Rtti(layout.Finance,0x72000,0,0x73000);
            data.Rtti(layout.CompactFinance,0x72020,0,0x73080);
            data.Rtti(legacyRva,0x72040,0,0x73100);
            var fullName=Encoding.ASCII.GetBytes(".?AVCLUB_FINANCE@db@@\0");
            data.Put(module+0x73010,fullName);
            data.Put(module+0x73090,Encoding.ASCII.GetBytes(".?AVCLUB_FINANCE_BASE@db@@\0"));
            data.Put(module+0x73110,fullName);
            data.Q(full+0x98,module+0x80000);
            data.Q(full+0x148,module+0x80100);
            data.Q(full+0x498,module+0x80120);
            data.Put(module+0x80000,new byte[96]);
            data.Put(module+0x80000,new byte[]{0x41,0x57,0x41,0x56});
            data.Put(module+0x80020,new byte[]{0x8B,0x81,0xC4,0x07,0,0});
            var wageGetter=new byte[]{0x8B,0x81,0x08,0x08,0,0,0xC3};
            data.Put(module+0x80100,wageGetter);
            data.Put(module+0x80120,new byte[]{0x0F,0xB6,0x81,0xAD,0x08,0,0,0xC3});

            Func<ulong,int,ulong,ulong[]> objects=(start,count,table)=>
                Enumerable.Range(0,count).Select(i=>
                {
                    ulong at=start+(ulong)i*0x1000;
                    data.Q(at,table);
                    // Compact objects can coincidentally satisfy the older numeric check.
                    foreach(ulong offset in originalFinOffsets.Where(o=>o!=0))
                        data.Put(at+offset,new byte[]{(byte)(table==full?0:10)});
                    return at;
                }).ToArray();
            var fullObjects=objects(0x200000,6,full);
            var compactObjects=objects(0x300000,12,compact);
            var legacyObjects=objects(0x400000,6,legacy);
            var corrupt=(byte[])layout.Signatures[0].Bytes.Clone();
            corrupt[0]^=1;
            data.Put(module+layout.Signatures[0].Rva,corrupt);
            var report=new List<string>();
            using(var fm=new FmProcess{Base=module,Version=layout.Version,ImageSize=layout.ImageSize})
            {
                check(!CurrentBuildProfile.Supported(layout.Version,layout.ImageSize,data.Read,module),
                    "wrong Steam 27.1.1 native proof selects the generic calibration path");
                Calibration.RecoverClubFinance(fm,data.Read,fullObjects.Take(4).Concat(compactObjects),report);
                check(FmProcess.FinanceVtRva==staleRva && FmProcess.CompactFinanceVtRva==layout.CompactFinance,
                    "four full votes are insufficient even with a majority of compact objects");
                check(FmLayout.FinTicketPrice==0x864 && FmLayout.FacilityFin.SequenceEqual(originalFinOffsets),
                    "failed calibration does not change facility or ticket fields");

                Calibration.RecoverClubFinance(fm,data.Read,fullObjects.Take(5).Concat(compactObjects),report);
                check(FmProcess.FinanceVtRva==layout.Finance && FmProcess.CompactFinanceVtRva==layout.CompactFinance,
                    "generic calibration recovers the full 27.1.1 type without using compact votes");
                check(FmLayout.FacilityFin.SequenceEqual(new ulong[]{0,0,0,0,0x8AD}) && FmLayout.FinTicketPrice==0x10,
                    "current layout getters select the established facility and ticket fields");

                data.Q(Fixture.ClubAt+0xC0,Fixture.Data);
                var catalog=new ClubCatalogReader(data.Read,a=>a==Fixture.Data?"Fixture club":null,module,FmProcess.CompactFinanceVtRva);
                check(catalog.Read(new[]{Fixture.ClubAt},CancellationToken.None).Single().HasFullFinance,
                    "the calibrated full type reaches the existing club catalog finance pool");
                var ticket=CurrentTicketSnapshot.Capture(data.Read,module,data.Club,FmProcess.ClubVtRva,FmProcess.FinanceVtRva);
                check(ticket.Price==-1 && ticket.Plan(null,20).Count==1,
                    "verified generic finance uses the existing guarded unset ticket price path");
                data.Put(Fixture.Finance+0x10,BitConverter.GetBytes(float.PositiveInfinity));
                check(Reject(()=>CurrentTicketSnapshot.Capture(data.Read,module,data.Club,FmProcess.ClubVtRva,FmProcess.FinanceVtRva)),
                    "the generic ticket snapshot retains its value checks");
                data.Put(Fixture.Finance+0x10,BitConverter.GetBytes(-1f));
                data.Q(Fixture.Finance,compact);
                var light=catalog.Read(new[]{Fixture.ClubAt},CancellationToken.None).Single();
                check(light.CompactFinance && !light.HasFullFinance &&
                    Reject(()=>CurrentTicketSnapshot.Capture(data.Read,module,data.Club,FmProcess.ClubVtRva,FmProcess.FinanceVtRva)),
                    "the generic catalog and guarded ticket paths keep compact finance read-only");
                FmProcess.FinanceVtRva=layout.CompactFinance;
                var overlapping=new ClubCatalogReader(data.Read,a=>a==Fixture.Data?"Fixture club":null,module,FmProcess.CompactFinanceVtRva);
                check(!overlapping.Read(new[]{Fixture.ClubAt},CancellationToken.None).Single().HasFullFinance,
                    "a stale full type that overlaps the compact type cannot make a compact club editable");
                Calibration.RecoverClubFinance(fm,data.Read,compactObjects.Take(4),report);
                check(FmProcess.FinanceVtRva==0,"a verified compact type is never retained as the full type on failed recovery");

                FmProcess.FinanceVtRva=staleRva;
                FmLayout.FacilityFin=originalFinOffsets;
                FmLayout.FinTicketPrice=0x864;
                Calibration.RecoverClubFinance(fm,data.Read,legacyObjects.Take(5).Concat(fullObjects.Take(4)),report);
                check(FmProcess.FinanceVtRva==staleRva,"five votes below sixty percent remain ambiguous");
                Calibration.RecoverClubFinance(fm,data.Read,legacyObjects.Concat(fullObjects.Take(4)),report);
                check(FmProcess.FinanceVtRva==legacyRva && FmLayout.FacilityFin.SequenceEqual(originalFinOffsets) &&
                    FmLayout.FinTicketPrice==0x864,"legacy finance can still win at sixty percent using the old verified fields");

                data.Put(module+0x80100,new byte[wageGetter.Length]);
                foreach(var at in fullObjects)
                    foreach(ulong offset in originalFinOffsets.Where(o=>o!=0))data.Put(at+offset,new byte[]{10});
                FmProcess.FinanceVtRva=staleRva;
                Calibration.RecoverClubFinance(fm,data.Read,fullObjects,report);
                check(FmProcess.FinanceVtRva==staleRva,"a partial current-layout proof cannot use incidental legacy facility values");
                data.Put(module+0x80100,wageGetter);
                data.I(module+0x72000+20,0x72100);
                Calibration.RecoverClubFinance(fm,data.Read,fullObjects,report);
                check(FmProcess.FinanceVtRva==staleRva,"a finance vtable with invalid RTTI self-reference cannot vote");
            }
        }

        public static void Run(Action<bool,string> check)
        {
            var saved=Profile.Fields().ToDictionary(f=>f,f=>f.GetValue(null) is Array?((Array)f.GetValue(null)).Clone():f.GetValue(null));
            var previous=CurrentBuildProfile.Active;
            try
            {
                foreach(var layout in new[]{NativeEngineLayout.Legacy,NativeEngineLayout.Update271,NativeEngineLayout.GamePass271,NativeEngineLayout.Steam2711})
                {
                    var data=new Fixture(layout);CurrentBuildProfile.Apply(layout);
                    check(CurrentBuildProfile.Supported(layout.Version,layout.ImageSize,data.Read,Fixture.Module),"exact engine proof selects "+layout.Version);
                    check(!CurrentBuildProfile.Supported(layout.Version,layout.ImageSize+4096,data.Read,Fixture.Module),"same-version images cannot borrow a measured native layout");
                    check(!CurrentBuildProfile.Supported("27.1.1+future",layout.ImageSize,data.Read,Fixture.Module),"future engines remain unverified");
                    if(layout.PeTimestamp!=0)
                    {
                        data.I(Fixture.Module+0x108,0);
                        check(!CurrentBuildProfile.Supported(layout.Version,layout.ImageSize,data.Read,Fixture.Module),"timestamp-verified builds require their original PE timestamp");
                        data.I(Fixture.Module+0x108,(int)layout.PeTimestamp);
                        data.I(Fixture.Module+0x150,(int)layout.ImageSize+1);
                        check(!CurrentBuildProfile.Supported(layout.Version,layout.ImageSize,data.Read,Fixture.Module),"mismatched PE image size is rejected");
                        data.I(Fixture.Module+0x150,(int)layout.ImageSize);
                        check(!CurrentBuildProfile.Supported(layout.Version,NativeEngineLayout.Update271.ImageSize,data.Read,Fixture.Module),"other builds cannot borrow the Steam 27.1.0 image layout");
                    }
                    if(layout.GameUiRoot!=0)
                    {
                        check(layout.ReadSaveLoads(data.Read,Fixture.Module)==3,"the loaded-save marker is taken from the selected typed game UI");
                        data.Q(0xAA000+0x100,4);
                        check(layout.ReadSaveLoads(data.Read,Fixture.Module)==4,"reloading the game changes the save marker even at the same date");
                    }
                    foreach(var sig in layout.Signatures)
                    {
                        var changed=(byte[])sig.Bytes.Clone();changed[0]^=1;data.Put(Fixture.Module+sig.Rva,changed);
                        check(!CurrentBuildProfile.Supported(layout.Version,layout.ImageSize,data.Read,Fixture.Module),"each independent native anchor is required: "+sig.Rva.ToString("X"));
                        data.Put(Fixture.Module+sig.Rva,sig.Bytes);
                    }
                    check(FmProcess.PersonVtRva==layout.Person && FmProcess.ClubVtRva==layout.Club && FmProcess.FinanceVtRva==layout.Finance && FmProcess.LoanContractVtRva==layout.Loan,"scanner and native editing use one selected build");
                    check(EligibilityLayout.TeamVt==layout.Team && EligibilityLayout.RulesContainerVt==layout.RulesContainer && EligibilityLayout.NonEuCheck==layout.NonEuCheck,"eligibility does not retain the preceding build's classes");
                    ulong today=CurrentBuildProfile.DateAddress(data.Read,Fixture.Module);
                    check(today==(layout.DirectDate!=0?Fixture.Module+layout.DirectDate:Fixture.DateManager+0x7DC) && Birthdays.Decode(BitConverter.ToUInt32(data.Read(today,4),0))==new DateTime(2026,8,22),"date is independent of transfer-listed samples");
                    data.Put(today,new byte[4]);check(CurrentBuildProfile.DateAddress(data.Read,Fixture.Module)==0,"invalid current date never enables retirement or age");data.Put(today,BitConverter.GetBytes((2026u<<16)|234u));
                    int reads=0;check(CurrentBuildProfile.DateAddress((a,n)=>a==today && ++reads>1?BitConverter.GetBytes((2026u<<16)|235u):data.Read(a,n),Fixture.Module)==0,"changing current date fails its complete read-set check");
                    int gap;
                    check(CurrentBuildProfile.RetirementManager(data.Read,Fixture.Module,out gap)==Fixture.Retire,"retirement singleton uses its measured typed root");
                    var retirement=RetirementSnapshot.Capture(data.Read,Fixture.Retire,Fixture.Module+layout.RetirementType,data.Player,gap);
                    check(retirement.HasAnnouncement && retirement.StillCurrent(data.Read),"pending-announcement snapshot accepts the measured secondary type");
                    var repair=retirement.Plan(new DateTime(2026,8,22),null);check(repair.Count==1 && repair[0].New[0]==0xA4,"retirement residual repair clears only the announcement bit");
                    data.Q(Fixture.Retire+8,Fixture.Module+layout.RetirementType+(gap==16?24UL:16UL));
                    check(!retirement.StillCurrent(data.Read) && CurrentBuildProfile.RetirementManager(data.Read,Fixture.Module)==0,"a different build's secondary spacing is rejected");data.Q(Fixture.Retire+8,Fixture.Module+layout.RetirementType+(ulong)gap);
                    if(layout.RetirementSecondary==0)
                    {
                        data.Rtti(layout.RetirementType+(ulong)gap,0x44020,8,0x50010);
                        check(CurrentBuildProfile.RetirementManager(data.Read,Fixture.Module)==0,"Game Pass retirement rejects a different RTTI descriptor with the same spacing");
                        data.Rtti(layout.RetirementType+(ulong)gap,0x44020,8,0x50000);
                    }
                    check(CurrentFieldSnapshot.Player(new EligibilityReader(data.Read),Fixture.Module,data.Player)==Fixture.Obj,"status ownership resolves the selected player's two types");
                    check(CurrentInjurySnapshot.Capture(data.Read,Fixture.Module,data.Player).Count==1,"medical records require the selected build's injury type");
                    var wrong=layout==NativeEngineLayout.Legacy?NativeEngineLayout.Update271:NativeEngineLayout.Legacy;
                    data.Q(Fixture.InjuryType,Fixture.Module+wrong.Injury);check(Reject(()=>CurrentInjurySnapshot.Capture(data.Read,Fixture.Module,data.Player)),"foreign-build injury types are rejected");data.Q(Fixture.InjuryType,Fixture.Module+layout.Injury);
                    var shirt=CurrentShirtSnapshot.Capture(data.Read,Fixture.Module,data.Player);check(shirt.Number==7 && !shirt.Loan,"own-club shirt numbers use the selected contract type");
                    var ticket=CurrentTicketSnapshot.Capture(data.Read,Fixture.Module,data.Club);check(ticket.SeasonTickets==100 && ticket.Price==-1,"full finance ticket sentinels retain their meaning");
                    data.Q(Fixture.Finance,Fixture.Module+layout.CompactFinance);check(Reject(()=>CurrentTicketSnapshot.Capture(data.Read,Fixture.Module,data.Club)),"compact finance cannot expose full-finance ticket fields");data.Q(Fixture.Finance,Fixture.Module+layout.Finance);
                    var facility=new ClubFacilitySnapshot(data.Read,data.Facilities(),data.Club);check(facility.Layout.VerifiedBuild && facility.Fields.All(f=>f.Editable),"all five owned facilities use the selected native proof");
                    var plan=facility.Plan(new Dictionary<int,int>{{0,15},{4,12}});string error;
                    check(plan.Apply(data.Read,data.Write,()=>true,out error) && data.Read(Fixture.Data+0x120,1)[0]==15 && data.Read(Fixture.Finance+layout.CorporateFacility,1)[0]==12,"two independently guarded facility paths apply on "+layout.Channel+" "+layout.Version);
                    data.Put(Fixture.Finance+layout.CorporateFacility,new byte[]{0});facility=new ClubFacilitySnapshot(data.Read,data.Facilities(),data.Club);
                    check(facility.Fields[0].Editable && facility.Fields[4].Status==FacilityStatus.Zero,"new layout preserves per-field read-only facility handling");
                    var eligibility=PlayerEligibilitySnapshot.Capture(data.Read,Fixture.Module,data.Player);var nonEu=eligibility.Plan(data.Read,true,null,false);
                    check(eligibility.Italian && eligibility.ItalyNonEu && nonEu.Count==1 && nonEu[0].After[0]==0xA5,"same-season non-EU repair preserves other player flags");
                    if(layout==NativeEngineLayout.Steam2711)
                        check(!layout.PlayerRegistrationEditing && Reject(()=>eligibility.Plan(data.Read,false,8,true)) &&
                            Reject(()=>eligibility.Plan(data.Read,false,0,true)),
                            "Steam 27.1.1 exposes Italian flag edits while unverified individual registration edits are blocked");
                    using(var fm=new FmProcess{Version=layout.Version,ImageSize=layout.ImageSize,VerifiedCurrentBuild=true})
                    {
                        check(CurrentBuildProfile.Matches(fm) && RetirementSnapshot.VtableFor(fm)==layout.RetirementType,"player retirement is available for the selected verified build");
                        check(GrowthReadLayout.From(fm).Labels,"actual squad and youth labels are enabled on the verified new build");
                        var head=new byte[0x100];head[0x19]=0x10;check(GenderFilter.Female(head,layout.Version,true)==true && GenderFilter.Female(head,layout.Version)==null,"gender still requires native verification");
                        fm.ImageSize++;check(!CurrentBuildProfile.Matches(fm) && RetirementSnapshot.VtableFor(fm)==0,"stale verified flags cannot cross engine images");
                    }
                }
                CurrentBuildProfile.Apply(NativeEngineLayout.Update271);
                EligibilityTests.RunCountryCases(check);
                EligibilityTests.RunSelectedBuildRules(check);
                CurrentBuildProfile.Apply(NativeEngineLayout.GamePass271);
                using(var steam=new FmProcess{Version=NativeEngineLayout.Update271.Version,ImageSize=NativeEngineLayout.Update271.ImageSize,VerifiedCurrentBuild=true})
                    check(!CurrentBuildProfile.Matches(steam),"active Xbox and Steam layouts are distinct even when the version text agrees");
                EligibilityTests.RunCountryCases(check);
                EligibilityTests.RunSelectedBuildRules(check);
                CurrentBuildProfile.Apply(NativeEngineLayout.Steam2711);
                EligibilityTests.RunSelectedBuildRules(check);
                FinanceCalibrationCases(check);
                CurrentBuildProfile.Reset();check(EligibilityLayout.ClubVt==NativeEngineLayout.Legacy.Club,"resetting a connection clears the new build's global layout");
            }
            finally{CurrentBuildProfile.Apply(previous);foreach(var field in saved)field.Key.SetValue(null,field.Value);}
        }
    }
}
