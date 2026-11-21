using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class ClubFacilitiesTests
    {
        internal sealed class Fixture
        {
            public const ulong Module=0x140000000,ClubAt=0x60000,Data=0x70000,Finance=0x80000;
            public readonly Club Club=new Club{Addr=ClubAt,Data=Data,Finance=Finance,Name="Offline Facility Club"};
            public readonly Dictionary<ulong,byte> Bytes=new Dictionary<ulong,byte>();
            public readonly HashSet<ulong> ReadAddresses=new HashSet<ulong>();
            public int Writes,FailAt;
            public Action<int> AfterWrite;
            public Fixture()
            {
                Put(ClubAt,new byte[0x180]);Put(Data,new byte[0x200]);Put(Finance,new byte[0x900]);
                Q(ClubAt,Module+0x4DDE298);I(ClubAt+12,123456);Q(ClubAt+0x100,Data);Q(ClubAt+0x150,Finance);Q(Finance,Module+0x4DF4E38);
                foreach(ulong offset in new ulong[]{0x120,0x12B,0x12C,0x12D})Put(Data+offset,new byte[]{10});Put(Finance+0x8A9,new byte[]{10});
                Put(Module+EligibilityLayout.NonEuCheck,EligibilityLayout.NonEuSignature);
            }
            public void Put(ulong at,byte[] data){for(int i=0;i<data.Length;i++)Bytes[at+(ulong)i]=data[i];}
            public void Q(ulong at,ulong value){Put(at,BitConverter.GetBytes(value));}
            public void I(ulong at,int value){Put(at,BitConverter.GetBytes(value));}
            public byte[] Read(ulong at,int size){ReadAddresses.Add(at);return Enumerable.Range(0,size).All(i=>Bytes.ContainsKey(at+(ulong)i))?Enumerable.Range(0,size).Select(i=>Bytes[at+(ulong)i]).ToArray():null;}
            public bool Write(ulong at,byte[] data){Writes++;if(Writes==FailAt)return false;Put(at,data);if(AfterWrite!=null)AfterWrite(Writes);return true;}
            public FacilityLayout Layout(bool verified=true,bool mapped=true,ulong[] data=null,ulong[] finance=null)
            {
                return new FacilityLayout(Read,CurrentBuildProfile.Build,Module,CurrentBuildProfile.ImageSize,verified,mapped,0x4DDE298,0x4DF4E38,0x100,0x150,
                    data??new ulong[]{0x120,0x12B,0x12C,0x12D,0},finance??new ulong[]{0,0,0,0,0x8A9});
            }
            public ClubFacilitySnapshot Capture(FacilityLayout layout=null){return new ClubFacilitySnapshot(Read,layout??Layout(),Club);}
            public string Digest(){return string.Join(";",Bytes.OrderBy(p=>p.Key).Select(p=>p.Key+":"+p.Value).ToArray());}
            public List<Change> Log(FacilityEditBatch batch)
            {return batch.Edits.Select(e=>new Change{Addr=e.Address,Old=(byte[])e.Before.Clone(),New=(byte[])e.After.Clone(),What=e.Label,Group=71,Facility=true,Guard=()=>batch.IdentityMatches(Read)}).ToList();}
        }
        static void Reject(Action action,Action<bool,string> check,string message){bool failed=false;try{action();}catch{failed=true;}check(failed,message);}
        static bool VisibleFlag(Control c){return (bool)typeof(Control).GetMethod("GetState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{2});}
        public static void Run(Action<bool,string> check)
        {
            var fixture=new Fixture();var layout=fixture.Layout();var good=fixture.Capture(layout);
            check(layout.VerifiedBuild && !layout.ProofFailed && good.Fields.All(f=>f.Editable && f.Value==10),"native proof and exact facility mapping enable individually owned in-range fields");
            fixture.Put(Fixture.Finance+0x8A9,new byte[]{0});var partial=fixture.Capture();
            check(partial.Fields.Take(4).All(f=>f.Editable) && partial.Fields[4].Status==FacilityStatus.Zero && partial.Fields[4].Value==-1,"an unconfirmed zero corporate field does not disable valid youth/training fields");
            Reject(()=>partial.Plan(new Dictionary<int,int>{{4,15}}),check,"zero-valued fields stay read-only rather than becoming an editable fake one");
            var coverage=new FacilityCoverage();for(int i=0;i<1620;i++)coverage.Add(good);for(int i=0;i<88;i++)coverage.Add(partial);
            check(coverage.Clubs==1708 && coverage.Complete==1620 && coverage.Valid==8452,"reported 1620/1708 complete clubs are counted separately from their valid individual fields");
            check(coverage.CanEnable(layout),"94.85 percent complete clubs do not globally disable known-build valid facility fields");
            var previous=Safety.Off;string previousNote=Safety.FacilityNote;
            try
            {
                Safety.Off=new Dictionary<string,string>();var report=new List<string>();Safety.AssessFacilities(report,coverage,layout);
                check(!Safety.Off.ContainsKey("facilities") && Safety.On("facilities") && Safety.On("ability"),"the production assessment enables checked facility fields without changing other feature gates");
                check(report.Any(s=>s.Contains("五项完整通过 1,620/1,708")) && report.Any(s=>s.Contains("商业设施") && s.Contains("原值0") && s.Contains("88")),"diagnostics identify the exact field/category instead of only the whole-club ratio");
                check(Safety.Summary().Contains("88 项只读") && !Safety.Summary().Contains("全部通过"),"partial availability is disclosed without claiming that every facility record passed");
                var scan=new ScanResult{FacilityNote=Safety.FacilityNote};
                check(AttachReport.Build(CurrentBuildProfile.Build,true,scan,Safety.Off,false).Contains("88 项只读"),"attachment details include per-field availability even when no whole feature is disabled");
                Safety.Off=new Dictionary<string,string>();report.Clear();var legacy=fixture.Layout(false);Safety.AssessFacilities(report,coverage,legacy);
                check(Safety.Off.ContainsKey("facilities") && !Safety.On("facilities") && report[0].Contains("95%"),"unverified builds retain the original global 95 percent requirement");
                var mostlyGood=new FacilityCoverage();for(int i=0;i<96;i++)mostlyGood.Add(good);for(int i=0;i<4;i++)mostlyGood.Add(partial);
                check(mostlyGood.CanEnable(legacy),"legacy profiles that genuinely pass the old threshold remain supported");
                var none=new FacilityCoverage();check(!none.CanEnable(layout),"zero sampled or verified fields cannot unlock the feature");
                Safety.Off=new Dictionary<string,string>{{"facilities","explicit existing refusal"}};Safety.AssessFacilities(new List<string>(),coverage,layout);
                check(Safety.Off["facilities"]=="explicit existing refusal","per-field coverage cannot erase an existing independent facility refusal");
            }
            finally{Safety.Off=previous;Safety.FacilityNote=previousNote;}
            foreach(byte raw in new byte[]{0,21,255})
            {
                fixture=new Fixture();fixture.Put(Fixture.Data+0x120,new[]{raw});var snapshot=fixture.Capture();
                check(!snapshot.Fields[0].Editable && snapshot.Fields.Skip(1).All(f=>f.Editable),"a single unsupported training byte is isolated: "+raw);
                Reject(()=>snapshot.Plan(new Dictionary<int,int>{{0,12}}),check,"unsupported original value is never silently normalized for editing: "+raw);
            }
            fixture=new Fixture();fixture.Bytes.Remove(Fixture.Data+0x12B);var missing=fixture.Capture();
            check(missing.Fields[1].Status==FacilityStatus.Unreadable && missing.Fields.Where((f,i)=>i!=1).All(f=>f.Editable),"one unreadable facility byte does not poison unrelated fields");
            fixture=new Fixture();fixture.Q(Fixture.ClubAt+0x100,0);var absent=fixture.Capture();
            check(absent.Fields.Take(4).All(f=>f.Status==FacilityStatus.Missing) && absent.Fields[4].Editable,"missing club data leaves independently owned corporate facilities available");
            fixture=new Fixture();fixture.Q(Fixture.ClubAt+0x100,Fixture.Data+0x100);var moved=fixture.Capture();
            check(moved.Fields.Take(4).All(f=>f.Status==FacilityStatus.Ownership) && moved.Fields[4].Editable,"changed data ownership is rejected per resource path");
            fixture=new Fixture();fixture.Q(Fixture.Finance,Fixture.Module+0x4DF4968);fixture.ReadAddresses.Clear();var compact=fixture.Capture();
            check(compact.Fields.Take(4).All(f=>f.Editable) && compact.Fields[4].Status==FacilityStatus.Ownership && !fixture.ReadAddresses.Contains(Fixture.Finance+0x8A9),"compact finances are never read at the full-finance corporate offset");
            fixture=new Fixture();fixture.Q(Fixture.ClubAt,Fixture.Module+1);check(fixture.Capture().Fields.All(f=>f.Status==FacilityStatus.Ownership),"an invalid club type cannot expose writable facilities");
            fixture=new Fixture();fixture.I(Fixture.ClubAt+12,0);check(fixture.Capture().Fields.All(f=>!f.Editable),"invalid club identity blocks every facility field");
            fixture=new Fixture();fixture.Put(Fixture.Module+EligibilityLayout.NonEuCheck,new byte[7]);layout=fixture.Layout();
            check(!layout.VerifiedBuild && layout.ProofFailed && fixture.Capture(layout).Fields.All(f=>!f.Editable),"lost native proof cannot fall back to permissive per-field editing");
            fixture=new Fixture();layout=fixture.Layout(true,false);var unmapped=fixture.Capture(layout);var unmappedCoverage=new FacilityCoverage();unmappedCoverage.Add(unmapped);
            check(!unmappedCoverage.CanEnable(layout) && unmapped.Fields.All(f=>f.Status==FacilityStatus.Unmapped),"a profile that omits facilities stays read-only even with matching native bytes");
            fixture=new Fixture();layout=fixture.Layout(data:new ulong[]{0,0x12B,0x12C,0x12D,0});var unlisted=fixture.Capture(layout);
            check(unlisted.Fields[0].Status==FacilityStatus.Unmapped && !layout.VerifiedBuild,"a zero mapping does not read the base of an unrelated data object");

            fixture=new Fixture();var snapshotGood=fixture.Capture();var batch=snapshotGood.Plan(new Dictionary<int,int>{{0,15},{4,18}});string error;
            check(batch.Edits.Count==2 && batch.Edits.All(e=>e.Before.Length==1 && e.After.Length==1),"facility plans touch only the exact requested bytes");
            string before=fixture.Digest();check(batch.Apply(fixture.Read,fixture.Write,()=>true,out error),"native per-field edits apply through the guarded production transaction");
            check(fixture.Read(Fixture.Data+0x120,1)[0]==15 && fixture.Read(Fixture.Finance+0x8A9,1)[0]==18 && fixture.Read(Fixture.Data+0x12B,1)[0]==10,"unselected facilities remain untouched");
            var group=fixture.Log(batch);check(FacilityTransactions.Undo(fixture.Read,fixture.Write,group,out error) && fixture.Digest()==before && group.All(c=>c.Undone),"a facility group undoes all requested fields and restores exact bytes");
            fixture=new Fixture();var dualLayout=fixture.Layout(false,true,finance:new ulong[]{0,0,0x8B2,0,0x8A9});fixture.Put(Fixture.Finance+0x8B2,new byte[]{10});
            var dual=fixture.Capture(dualLayout);batch=dual.Plan(new Dictionary<int,int>{{2,16}});
            check(dual.Fields[2].Editable && batch.Edits.Count==2,"legacy dual representations are both included in one plan");
            before=fixture.Digest();fixture.FailAt=2;check(!batch.Apply(fixture.Read,fixture.Write,()=>true,out error) && fixture.Digest()==before,"failure of the second copy restores the first copy atomically");
            fixture.FailAt=0;check(batch.Apply(fixture.Read,fixture.Write,()=>true,out error),"matching dual facility copies can be updated together");
            check(FacilityTransactions.Undo(fixture.Read,fixture.Write,fixture.Log(batch),out error) && fixture.Digest()==before,"dual-copy undo is atomic and exact");
            fixture.Put(Fixture.Finance+0x8B2,new byte[]{9});dual=fixture.Capture(dualLayout);
            check(dual.Fields[2].Status==FacilityStatus.Conflict && dual.Fields.Where((f,i)=>i!=2).All(f=>f.Editable),"inconsistent facility copies stay isolated and read-only");
            Reject(()=>dual.Plan(new Dictionary<int,int>{{2,15}}),check,"different copies cannot be overwritten as though the screen held one reliable value");

            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15},{4,18}});fixture.Put(Fixture.Data+0x120,new byte[]{11});int writes=fixture.Writes;
            check(!batch.Apply(fixture.Read,fixture.Write,()=>true,out error) && fixture.Writes==writes,"a field changed after preview is rejected before any write");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15}});fixture.I(Fixture.ClubAt+12,999);
            check(!batch.Apply(fixture.Read,fixture.Write,()=>true,out error) && fixture.Writes==0,"reused club memory with a different UID cannot receive a stale edit");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15}});fixture.Q(Fixture.ClubAt+0x100,Fixture.Data+0x100);
            check(!batch.Apply(fixture.Read,fixture.Write,()=>true,out error) && fixture.Writes==0,"changed data pointers invalidate facility plans");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{4,18}});fixture.Put(Fixture.Data+0x120,new byte[]{0});
            check(batch.Apply(fixture.Read,fixture.Write,()=>true,out error),"unrelated unselected facility changes do not invalidate an independently owned corporate edit");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15},{4,18}});fixture.AfterWrite=n=>{if(n==2)fixture.Put(Fixture.Data+0x120,new byte[]{10});};before=fixture.Digest();
            check(!batch.Apply(fixture.Read,fixture.Write,()=>true,out error) && fixture.Digest()==before,"final readback detects an earlier field reverting during the last write");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15},{4,18}});fixture.AfterWrite=n=>{if(n==2)fixture.Put(Fixture.Data+0x120,new byte[]{7});};
            check(!batch.Apply(fixture.Read,fixture.Write,()=>true,out error) && fixture.Read(Fixture.Data+0x120,1)[0]==7 && fixture.Read(Fixture.Finance+0x8A9,1)[0]==10 && error.Contains("重载"),"rollback preserves an intervening foreign value and reports incomplete recovery");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15},{4,18}});check(batch.Apply(fixture.Read,fixture.Write,()=>true,out error),"undo conflict fixture applies");group=fixture.Log(batch);fixture.Put(Fixture.Data+0x120,new byte[]{16});writes=fixture.Writes;
            check(!FacilityTransactions.Undo(fixture.Read,fixture.Write,group,out error) && fixture.Writes==writes && group.All(c=>!c.Undone),"stale facility undo refuses the whole group before writing");
            fixture=new Fixture();batch=fixture.Capture().Plan(new Dictionary<int,int>{{0,15},{4,18}});check(batch.Apply(fixture.Read,fixture.Write,()=>true,out error),"undo failure fixture applies");group=fixture.Log(batch);before=fixture.Digest();fixture.FailAt=fixture.Writes+2;
            check(!FacilityTransactions.Undo(fixture.Read,fixture.Write,group,out error) && fixture.Digest()==before && group.All(c=>!c.Undone),"partially failed undo restores the post-edit state instead of leaving mixed facilities");
            fixture=new Fixture();snapshotGood=fixture.Capture();Reject(()=>snapshotGood.Plan(new Dictionary<int,int>{{0,0}}),check,"zero cannot be a facility target");Reject(()=>snapshotGood.Plan(new Dictionary<int,int>{{0,21}}),check,"facility targets cannot exceed 20");
            check(snapshotGood.Plan(new Dictionary<int,int>{{0,10}}).Edits.Count==0,"an unchanged facility value produces no write");
            using(var form=new MainForm(false,false))
            {
                fixture=new Fixture();fixture.Put(Fixture.Finance+0x8A9,new byte[]{0});partial=fixture.Capture();form.BindFacilityEditors(partial,true,"");
                var controls=(NumericUpDown[])typeof(MainForm).GetField("numFacility",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                check(controls.Take(4).All(c=>c.Enabled && c.Value==10) && !controls[4].Enabled && !VisibleFlag(controls[4]),"UI exposes only valid numeric facilities and hides the fake one for an unavailable zero");
                check(((Label)form.Controls.Find("FacilityStatus4",true).Single()).Text.Contains("原值为 0") && form.Controls.Find("FacilityScopeNote",true).Length==1,"UI explains the raw zero and per-club support scope");
                form.BindFacilityEditors(fixture.Capture(),false,"构建未核实，仅显示");
                check(controls.All(c=>!c.Enabled) && ((Label)form.Controls.Find("FacilityStatus0",true).Single()).Text.Contains("仅显示"),"valid raw values alone cannot bypass the global version/core gates");
            }
        }
    }
}
