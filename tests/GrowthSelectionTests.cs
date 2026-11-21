using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class GrowthSelectionTests
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static Player Person(int uid,ulong address,ulong club=0x80000)
        {return new Player{Uid=uid,Person=address,Club=club,Name="Offline fixture "+uid,Search="Offline fixture "+uid};}
        static byte[] Positions(int main=12){var values=Enumerable.Repeat((byte)1,15).ToArray();values[main]=20;return values;}
        static void Reject(Action action,Action<bool,string> check,string message)
        {bool rejected=false;try{action();}catch{rejected=true;}check(rejected,message);}
        public static void Run(Action<bool,string> check)
        {
            var a=new GrowTarget{P=Person(1,0x11000),Elapsed=27,Added=4,Span=600,Options=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}}};
            var c=new GrowTarget{P=Person(3,0x33000),Elapsed=27,Added=3,Span=500,Options=new GrowthOptions{Mode=GrowthMode.CaOnly}};
            var refreshed=new[]{Person(1,0x111000),Person(2,0x222000),Person(3,0x333000),Person(4,0x444000)};
            var kept=GrowthRoster.Rebind(new[]{a,c},refreshed);
            check(kept.Count==2 && kept.Select(t=>t.P.Uid).OrderBy(i=>i).SequenceEqual(new[]{1,3}),"refresh preserves a manually selected subset and never selects unchecked or newly signed players");
            check(object.ReferenceEquals(kept[0],a) && a.P.Person==0x111000 && a.Elapsed==27 && a.Added==4 && a.Span==600,"rebinding changes only the player reference and preserves growth progress");
            check(a.Options.Mode==GrowthMode.SelectedAttributes && a.Options.Attributes.SequenceEqual(new[]{34,38}) && c.Options.Mode==GrowthMode.CaOnly,"different per-player growth choices survive refresh");
            check(GrowthRoster.Rebind(new GrowTarget[0],refreshed).Count==0,"an empty selection stays empty after a scan");
            check(GrowthRoster.Rebind(new[]{a},new[]{Person(1,0xAAAA0,0x99000)}).Count==0,"a transferred player is removed rather than replaced by another roster member");
            check(GrowthRoster.Rebind(new[]{a},new[]{Person(9,a.P.Person)}).Count==0,"pointer reuse cannot bind an unchecked different UID");
            check(GrowthRoster.Rebind(new[]{a},new[]{Person(1,0xB0000),Person(1,0xC0000)}).Count==0,"ambiguous duplicate identities are refused");
            check(GrowthRoster.Rebind(new[]{a},new[]{Person(1,0xD0000),Person(1,0xD0000)}).Count==1,"duplicate reports of the same object do not create duplicate targets");
            for(int scan=0;scan<15;scan++)
            {
                kept=GrowthRoster.Rebind(kept,Enumerable.Range(1,8).Select(i=>Person(i,(ulong)(0x100000+scan*0x10000+i*0x1000))));
                check(kept.Count==2 && kept.All(t=>t.P.Uid==1 || t.P.Uid==3),"repeated monthly refresh cannot expand the manual selection");
            }

            var values=Enumerable.Repeat((byte)50,54).ToArray();
            var selected=new[]{34,38};var focused=new GrowthFocusState(Positions(),values,100,-1,selected);selected[0]=2;
            var allocation=focused.Allocate(values,110,160);
            check(allocation.After[34]>50 && allocation.After[38]>50,"independent attribute selection grows acceleration and pace");
            check(Enumerable.Range(0,54).All(i=>i==34 || i==38 || allocation.After[i]==50),"every unselected attribute remains byte-identical");
            check(allocation.After[34]<=55 && allocation.After[38]<=55,"a narrow selection does not absorb the entire player's CA budget");
            check(allocation.SelectedScoreAfter<=allocation.SelectedLimit && focused.Allocate(allocation.After,110,160).RawAdded==0,"the selected share is bounded and cannot be spent twice");
            var other=new GrowthFocusState(Positions(7),values,100,-1,new[]{7,10,18,22});var otherGrowth=other.Allocate(values,130,160);
            check(new[]{7,10,18,22}.All(i=>otherGrowth.After[i]>50) && otherGrowth.After[34]==50 && otherGrowth.After[38]==50,"passing, vision, decisions and touch are selectable without a speed preset");
            var physical=new GrowthFocusState(Positions(3),values,100,-1,new[]{36,37,39,42,46,50});
            check(physical.Allocate(values,120,160).ChangedAttributes==6,"all six chosen physical fields participate independently");
            Reject(()=>new GrowthFocusState(Positions(),values,100,-1,new int[0]),check,"clearing all attributes cannot silently fall back to a whole-role preset");
            foreach(int invalid in new[]{-1,24,25,31,41,44,47,48,49,54})
                Reject(()=>new GrowthFocusState(Positions(),values,100,-1,new[]{invalid}),check,"invalid, hidden and foot fields cannot enter custom growth");
            Reject(()=>new GrowthFocusState(Positions(),values,100,-1,new[]{21}),check,"goalkeeper fields require a goalkeeper direction");
            var goalkeeper=new GrowthFocusState(Positions(0),values,100,-1,new[]{11,19,21});
            check(goalkeeper.Allocate(values,120,160).ChangedAttributes==3,"keeper-specific attributes remain selectable for goalkeeper plans");
            var natural=(byte[])values.Clone();natural[34]=natural[38]=60;
            check(focused.Allocate(natural,120,160).RawAdded==0,"natural improvement of selected fields consumes their reserved share");
            var copied=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{38,34,38}};var clone=copied.Clone();copied.Attributes[0]=7;
            check(clone.Attributes.SequenceEqual(new[]{34,38}),"per-player preferences copy and deduplicate their own selection arrays");
            var memory=new GrowthFocusTests.Fixture();var frame=memory.Capture();frame.Plan(new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}}.CreateState(frame.Positions,frame.Attributes,frame.Ca),110);
            check(frame.Edits.Skip(1).All(e=>e.Address==memory.Player.Block+15+34 || e.Address==memory.Player.Block+15+38),"actual production write plans contain only the selected attribute addresses");
            string error;check(memory.Apply(frame,out error),"a custom-attribute production transaction applies to copied memory");

            check(GrowthProfile.All.Length==98 && GrowthProfile.All.Count(p=>p.Phase!="基本")==88,"all position groups have both basic and detailed role presets");
            foreach(string group in GrowthProfile.Groups)
                check(GrowthProfile.All.Any(p=>p.Group==group && p.Phase=="持球") && GrowthProfile.All.Any(p=>p.Group==group && p.Phase=="无球"),"each position offers both possession phases: "+group);
            var channel=GrowthProfile.All.Single(p=>p.Name=="肋部中锋" && p.Phase=="持球");var deep=GrowthProfile.All.Single(p=>p.Name=="拖后中锋");var poacher=GrowthProfile.All.Single(p=>p.Name=="抢点型前锋");
            check(channel.Core.Contains(1) && channel.Core.Contains(34) && poacher.Core.Contains(17) && poacher.Core.Contains(53),"channel forwards and poachers have different key requirements");
            check(deep.Core.Contains(36) && !deep.Core.SequenceEqual(channel.Core) && !deep.Core.SequenceEqual(poacher.Core),"deep-lying forwards retain a separate hold-up and linking profile");
            foreach(var profile in GrowthProfile.All)
                check(profile.Core.All(i=>GrowthProfile.Developable(i) && profile.Cost(i)>0) && profile.Supporting.All(i=>GrowthProfile.Developable(i) && profile.Cost(i)>0),"every detailed preset targets only supported positive-cost fields");

            using(var editor=new GrowthEditorPanel())
            {
                int changedUid=0;GrowthOptions changed=null;editor.OptionsChanged+=(uid,opt)=>{changedUid=uid;changed=opt;};
                editor.Bind(101,"Offline player",new GrowthOptions(),Positions(),false,false);editor.Speed.PerformClick();
                check(changedUid==101 && changed.Mode==GrowthMode.SelectedAttributes && changed.Attributes.SequenceEqual(new[]{34,38}),"speed is a two-attribute shortcut for the bound player, not the only available selection");
                editor.SetCheckedAttributes(7,10,18,36);
                check(changed.Attributes.SequenceEqual(new[]{7,10,18,36}) && editor.Value.Attributes.Length==4,"the editor accepts arbitrary technical, mental and physical selections");
                editor.Bind(101,"Offline player",new GrowthOptions(),Positions(),true,false);
                var technical=(CheckedListBox)editor.Controls.Find("GrowthAttributes0",true).Single();int corner=technical.Items.Cast<object>().Select((item,index)=>new{item,index}).Single(x=>x.item.ToString()==Zh.Attr("Corners|V")).index;
                technical.SetItemChecked(corner,true);
                check(editor.Value.Mode==GrowthMode.SelectedAttributes && editor.Value.Attributes.Contains(27),"editing any role checkbox switches to a real custom selection without requiring a preset shortcut");
                editor.ClearAttributes.PerformClick();check(editor.Value.Mode==GrowthMode.SelectedAttributes && editor.Value.Attributes.Length==0,"clearing checkboxes stays explicitly empty");
                editor.Bind(202,"Another player",new GrowthOptions{Mode=GrowthMode.CaOnly},Positions(3),true,false);
                check(editor.Value.Mode==GrowthMode.CaOnly && !editor.Direction.Enabled,"selecting a different player loads that player's own options");
                editor.Bind(101,"Offline player",new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{7,10}},Positions(),true,true);
                check(!editor.Mode.Enabled && !editor.Speed.Enabled && editor.Preview.Enabled,"a running plan prevents edits while retaining read-only preview");
            }

            using(var form=new MainForm(false,false))
            {
                var watch=(Dictionary<ulong,GrowTarget>)typeof(MainForm).GetField("growWatch",Private).GetValue(form);
                var selectedTarget=new GrowTarget{P=Person(501,0x50000),Elapsed=31,Added=7,Span=600,Options=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{7,34}}};
                watch.Add(selectedTarget.P.Person,selectedTarget);
                var result=new ScanResult();result.Players.AddRange(new[]{Person(501,0x150000),Person(502,0x160000),Person(503,0x170000)});result.Clubs.Add(new Club{Addr=0x80000,Name="Offline Club"});
                form.ApplyAutomaticScanResult(result);
                check(watch.Count==1 && watch.Values.Single().P.Uid==501 && watch.Values.Single().P.Person==0x150000,"the actual automatic-scan application preserves a subset without attaching to FM");
                check(selectedTarget.Elapsed==31 && selectedTarget.Added==7 && selectedTarget.Options.Attributes.SequenceEqual(new[]{7,34}),"the real UI refresh retains the selected player's options and progress");
                watch.Clear();form.ApplyAutomaticScanResult(result);check(watch.Count==0,"the production refresh cannot reselect a manually cleared queue");
            }
        }
    }
}
