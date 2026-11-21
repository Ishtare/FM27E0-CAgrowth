using System;
using System.Collections.Generic;
using System.Linq;

namespace Fm24Editor
{
    static class CurrentBuildTests
    {
        sealed class Memory
        {
            readonly Dictionary<ulong,byte> bytes = new Dictionary<ulong,byte>();
            public void Put(ulong at, byte[] data) { for(int i=0;i<data.Length;i++) bytes[at+(ulong)i]=data[i]; }
            public void Q(ulong at, ulong value) { Put(at,BitConverter.GetBytes(value)); }
            public byte[] Read(ulong at,int n) { var result=new byte[n];for(int i=0;i<n;i++)if(!bytes.TryGetValue(at+(ulong)i,out result[i]))return null;return result; }
        }
        public static void Run(Action<bool,string> check)
        {
            ulong module=0x1000000, dateManager=0x2000000, retireManager=0x3000000;
            var memory=new Memory();memory.Put(module+EligibilityLayout.NonEuCheck,EligibilityLayout.NonEuSignature);
            check(CurrentBuildProfile.Supported(CurrentBuildProfile.Build,CurrentBuildProfile.ImageSize,memory.Read,module),"current build needs a matching native signature");
            check(!CurrentBuildProfile.Supported("27.0.2+changed",CurrentBuildProfile.ImageSize,memory.Read,module),"future game build does not inherit current offsets");
            check(!CurrentBuildProfile.Supported(CurrentBuildProfile.Build,CurrentBuildProfile.ImageSize+4096,memory.Read,module),"same version with a different engine image is not accepted");
            check(!CurrentBuildProfile.Supported(CurrentBuildProfile.Build,CurrentBuildProfile.ImageSize,(a,n)=>null,module),"unreadable signature keeps current adapter disabled");
            check(!CurrentBuildProfile.Supported(CurrentBuildProfile.Build,CurrentBuildProfile.ImageSize,(a,n)=>{throw new Exception("read failure");},module),"signature read exceptions fail closed");
            memory.Put(module+EligibilityLayout.NonEuCheck,new byte[7]);
            check(!CurrentBuildProfile.Supported(CurrentBuildProfile.Build,CurrentBuildProfile.ImageSize,memory.Read,module),"wrong native code is rejected even with matching version and image size");

            memory.Q(module+CurrentBuildProfile.DateRoot,dateManager);memory.Q(dateManager,module+CurrentBuildProfile.DateVtable);
            memory.Put(dateManager+0x7DC,BitConverter.GetBytes(((uint)2026<<16)|194u));
            check(CurrentBuildProfile.DateAddress(memory.Read,module)==dateManager+0x7DC,"date comes from the validated game manager without any transfer-listed player");
            memory.Q(dateManager,module+1);
            check(CurrentBuildProfile.DateAddress(memory.Read,module)==0,"date manager type change invalidates the date reader");
            memory.Q(dateManager,module+CurrentBuildProfile.DateVtable);memory.Put(dateManager+0x7DC,new byte[4]);
            check(CurrentBuildProfile.DateAddress(memory.Read,module)==0,"invalid packed date cannot enable age or retirement edits");
            memory.Put(dateManager+0x7DC,BitConverter.GetBytes(((uint)2026<<16)|194u));
            int roots=0;
            check(CurrentBuildProfile.DateAddress((a,n)=>{if(a==module+CurrentBuildProfile.DateRoot && ++roots>1)return BitConverter.GetBytes(dateManager+16);return memory.Read(a,n);},module)==0,"changing date ownership is rejected during the read set check");

            memory.Q(module+CurrentBuildProfile.RetirementRoot,retireManager);memory.Q(retireManager,module+CurrentBuildProfile.RetirementVtable);memory.Q(retireManager+8,module+CurrentBuildProfile.RetirementVtable+16);
            check(CurrentBuildProfile.RetirementManager(memory.Read,module)==retireManager,"retirement root requires both native type pointers");
            memory.Q(retireManager+8,module+CurrentBuildProfile.RetirementVtable+24);
            check(CurrentBuildProfile.RetirementManager(memory.Read,module)==0,"mismatched retirement secondary type is rejected");
            using(var fm=new FmProcess {Version=CurrentBuildProfile.Build,ImageSize=CurrentBuildProfile.ImageSize})
            {
                check(RetirementSnapshot.VtableFor(fm)==0,"build string alone cannot enable current retirement editing");
                fm.VerifiedCurrentBuild=true;
                check(RetirementSnapshot.VtableFor(fm)==CurrentBuildProfile.RetirementVtable,"verified current engine exposes the independently checked retirement manager");
                fm.Version="27.0.2+changed";
                check(RetirementSnapshot.VtableFor(fm)==0,"stale current-build flag cannot enable retirement on a later engine");
            }
            var head=new byte[0x100];head[0x19]=0x12;
            check(GenderFilter.Female(head,CurrentBuildProfile.Build)==null,"current gender layout requires verification");
            check(GenderFilter.Female(head,CurrentBuildProfile.Build,true)==true,"verified current female bit works for free agents too");
            head[0x19]=2;check(GenderFilter.Female(head,CurrentBuildProfile.Build,true)==false,"unrelated person flags do not hide male players");

            var fields=Profile.Fields().ToDictionary(f=>f,f=>f.GetValue(null) is Array?((Array)f.GetValue(null)).Clone():f.GetValue(null));
            try
            {
                CurrentBuildProfile.Apply();
                check(FmProcess.ContractVtRva==EligibilityLayout.ContractVt && FmProcess.FinanceVtRva==0x4DF4E38,"current adapter supplies contract and full-finance types independently of recovery samples");
                check(FmProcess.FinanceVtRva!=0x4DF4968,"compact finances are never treated as full editable finances");
                check(FmLayout.FacilityFin[2]==0 && FmLayout.FacilityFin[3]==0 && FmLayout.FacilityData[2]==0x12C && FmLayout.FacilityData[3]==0x12D,"coaching and recruitment use the club data fields");
                check(FmLayout.FinTicketPrice==0x10,"ticket field uses the measured current-finance layout");
            }
            finally { foreach(var field in fields)field.Key.SetValue(null,field.Value); }
        }
    }
}
