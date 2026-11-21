using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class GrowthSupportTests
    {
        static byte[] Positions(int index=12){var v=Enumerable.Repeat((byte)1,15).ToArray();v[index]=20;return v;}
        static void Reject(Action action,Action<bool,string> check,string message){bool failed=false;try{action();}catch{failed=true;}check(failed,message);}
        public static void Run(Action<bool,string> check)
        {
            var values=Enumerable.Repeat((byte)50,54).ToArray();values[36]=5;
            var options=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}};
            check(!options.Support.Enabled,"low-value support is off by default");
            var original=options.CreateState(Positions(),values,100,18).Allocate(values,120,160);
            check(original.After[36]==5 && original.After[34]>50 && original.After[38]>50,"disabled support keeps an unselected strength of one untouched");
            options.Support.Enabled=true;var state=options.CreateState(Positions(),values,100,18);
            check(state.SupportItems.Length==1 && state.SupportItems[0].Index==36 && state.SupportItems[0].Target==6,"age-18 CA-100 fixture receives a conservative strength floor, not a uniform average or 20");
            check(state.Allocate(values,100,160).RawAdded==0,"turning support on without earning CA gives no free attributes");
            var first=state.Allocate(values,101,160);var full=state.Allocate(values,130,160);
            check(first.After[36]>5 && first.After[36]<30 && full.After[36]==30,"support grows gradually and stops at its approved floor");
            check(full.After[34]>50 && full.After[38]>50 && full.After[2]==50,"selected speed attributes continue while unrelated finishing stays unchanged");
            check(full.ScoreAfter<=full.Limit && first.SupportSpent<=first.SupportLimit,"support and primary growth share the bounded whole-player budget");
            check(state.Allocate(first.After,101,160).RawAdded==0,"a repeated tick cannot spend the support or selected share twice");
            foreach(int field in new[]{34,36,37,38,42,46})
            {
                var low=Enumerable.Repeat((byte)50,54).ToArray();low[field]=5;
                var support=options.CreateState(Positions(),low,100,18);
                check(support.SupportItems.Any(i=>i.Index==field),"each foundation field, not only strength, participates in low-value detection: "+field);
                check(support.Allocate(low,110,160).After[field]>5,"each detected foundation receives bounded growth: "+field);
            }
            foreach(var test in new[]{new[]{12,2},new[]{12,52},new[]{3,5},new[]{3,9},new[]{3,20},new[]{0,21},new[]{0,11},new[]{7,7},new[]{7,18}})
            {
                var low=Enumerable.Repeat((byte)50,54).ToArray();low[test[1]]=5;
                check(options.CreateState(Positions(test[0]),low,100,18).SupportItems.Any(i=>i.Index==test[1]),"role-specific technical, mental and goalkeeping lows can be suggested: "+test[0]+"/"+test[1]);
            }
            var natural=(byte[])values.Clone();natural[36]=40;var afterNatural=state.Allocate(natural,110,160);
            check(afterNatural.After[36]==40 && afterNatural.SupportRawAdded==0,"natural improvement past the support floor is retained and consumes its share");
            var varied=(byte[])values.Clone();varied[2]=5;
            var defender=options.CreateState(Positions(3),varied,100,18);
            check(defender.SupportItems.All(i=>i.Index!=2),"a defender's low finishing alone is not diagnosed as a basic defect");
            var goalkeeper=options.CreateState(Positions(0),varied,100,18);
            check(goalkeeper.SupportItems.All(i=>i.Index!=2),"a goalkeeper's low finishing is not auto-normalized");
            foreach(int style in new[]{26,40,43,45,50,51})varied[style]=5;
            var stylePlan=options.CreateState(Positions(),varied,100,18);
            check(stylePlan.SupportItems.All(i=>!new[]{26,40,43,45,50,51}.Contains(i.Index)),"style, natural fitness, determination and leadership are not automatically averaged up");
            var weak=Enumerable.Repeat((byte)5,54).ToArray();
            check(options.CreateState(Positions(),weak,20,15).SupportItems.Length==0,"uniformly weak low-CA youth is not marked abnormal solely for having low values");
            var younger=options.CreateState(Positions(),values,100,15);
            check(younger.SupportItems[0].Target<state.SupportItems[0].Target,"age changes the conservative automatic floor");
            var excluded=options.Clone();excluded.Support.Excluded.Add(36);
            check(excluded.CreateState(Positions(),values,100,18).Allocate(values,130,160).After[36]==5,"a user can opt out of a particular suggested field");
            var edited=options.Clone();edited.Support.Targets[36]=8;
            var editedState=edited.CreateState(Positions(),values,100,18);var editedResult=editedState.Allocate(values,130,160);
            check(editedResult.After[36]==40,"an explicitly selected support goal is respected and bounded");
            var resumedValues=(byte[])values.Clone();resumedValues[36]=20;
            check(edited.CreateState(Positions(),resumedValues,110,18).SupportItems.Single().Target==8,"an approved goal survives restarting after the original low-value threshold was passed");
            var copied=edited.Clone();edited.Support.Targets[36]=10;edited.Support.Excluded.Add(36);
            check(copied.Support.Targets[36]==8 && !copied.Support.Excluded.Contains(36),"per-player target and exclusion collections are deep copied");
            var normalStrength=(byte[])values.Clone();normalStrength[36]=25;
            var another=copied.ForAnotherPlayer(Positions(),normalStrength,100,18);
            check(!another.Support.Targets.ContainsKey(36) && another.CreateState(Positions(),normalStrength,100,18).SupportItems.Length==0,"batch copying settings rechecks each player's lows instead of importing someone else's approval");
            check(copied.ForAnotherPlayer(Positions(),values,100,18).Support.Targets[36]==8,"batch copying can retain the chosen floor for another actually eligible low field");
            copied.Support.BudgetPercent=41;Reject(()=>copied.CreateState(Positions(),values,100,18),check,"overlarge support budget is rejected");
            copied.Support.BudgetPercent=25;copied.Support.Targets[36]=13;Reject(()=>copied.CreateState(Positions(),values,100,18),check,"support does not accept a floor above 12");
            copied.Support.Targets.Clear();copied.Support.Targets[24]=10;Reject(()=>copied.CreateState(Positions(),values,100,18),check,"foot preference cannot be smuggled into support targets");

            var memory=new GrowthFocusTests.Fixture();memory.Put(memory.Player.Block+15+36,new byte[]{5});
            var frame=memory.Capture();frame.Plan(options.CreateState(frame.Positions,frame.Attributes,frame.Ca,18),110);
            check(frame.Edits.Any(e=>e.Address==memory.Player.Block+15+36 && e.Label.Contains("低值补强")),"production plans explicitly label extra strength support");
            check(frame.Edits.Skip(1).All(e=>new[]{34UL,36UL,38UL}.Contains(e.Address-memory.Player.Block-15)),"production plans cannot write outside the selected/support union");
            string error;check(memory.Apply(frame,out error),"support applies through the existing atomic production transaction");
            var group=memory.Log(frame);check(GrowthUndo.Apply(memory.Read,memory.Write,group,out error) && memory.Read(memory.Player.Block+15+36,1)[0]==5,"one grouped undo restores support as well as main growth and CA");
            memory=new GrowthFocusTests.Fixture();memory.Put(memory.Player.Block+15+36,new byte[]{5});frame=memory.Capture();frame.Plan(options.CreateState(frame.Positions,frame.Attributes,100,18),110);
            string before=memory.Digest();memory.FailAt=3;check(!memory.Apply(frame,out error) && memory.Digest()==before,"a partial support transaction rolls back all owned writes");

            var random=new Random(118);
            for(int sample=0;sample<80;sample++)
            {
                var attrs=Enumerable.Range(0,54).Select(i=>(byte)random.Next(30,90)).ToArray();attrs[36]=(byte)random.Next(1,16);
                int ca=60+sample%60,cap=Math.Min(200,ca+50);var focus=options.CreateState(Positions(),attrs,ca,17+sample%6);
                var current=(byte[])attrs.Clone();
                for(int target=ca+1;target<=cap;target+=5)
                {
                    var allocation=focus.Allocate(current,target,cap);
                    check(allocation.ScoreAfter<=allocation.Limit || allocation.RawAdded==0,"randomized supported growth never adds beyond available whole-player budget");
                    check(Enumerable.Range(0,54).All(i=>allocation.After[i]>=current[i] && allocation.After[i]<=100),"all public/raw bounds hold across repeated support ticks");
                    check(Enumerable.Range(0,54).All(i=>i==34 || i==38 || focus.SupportItems.Any(s=>s.Index==i) || allocation.After[i]==attrs[i]),"randomized plans preserve every unrelated field");
                    current=allocation.After;
                }
            }
            using(var editor=new GrowthEditorPanel())
            {
                int changed=0;GrowthOptions result=null;editor.OptionsChanged+=(uid,value)=>{changed=uid;result=value;};
                editor.Bind(111,"Synthetic youth",new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}},Positions(),true,false,values,100,18);
                editor.LowSupport.Checked=true;
                check(changed==111 && result.Support.Enabled && result.Support.Targets.ContainsKey(36),"enabling support approves visible suggestions for the correct player without any game write");
                editor.Bind(222,"Different synthetic youth",new GrowthOptions(),Positions(),false,false,values,100,18);
                check(!editor.LowSupport.Checked,"support choices do not leak into another player's settings");
                editor.Mode.SelectedIndex=2;check(!editor.LowSupport.Enabled && !editor.ConfigureSupport.Enabled,"CA-only mode cannot silently gain a direct attribute supplement");
                editor.Bind(111,"Running synthetic youth",options,Positions(),true,true,values,100,18);
                check(!editor.LowSupport.Enabled && !editor.ConfigureSupport.Enabled && editor.Preview.Enabled,"running plans lock support changes while keeping the preview available");
            }
            using(var dialog=new GrowthSupportDialog(options.Support,GrowthProfile.All[8],values,100,18))
            {
                var grid=(DataGridView)dialog.Controls.Find("SupportAttributeTargets",true).Single();
                check(grid.Rows.Count==1 && (int)grid.Rows[0].Tag==36,"the configuration dialog lists actual suggestions rather than the entire attribute bank");
                grid.Rows[0].Cells[3].Value=8;
                var save=(Button)dialog.Controls.Find("SaveSupportOptions",true).Single();typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(save,new object[]{EventArgs.Empty});
                check(dialog.ResultOptions!=null && dialog.ResultOptions.Targets[36]==8 && options.Support.Targets.Count==0,"saving target edits returns an independent per-player draft");
            }
            using(var form=new MainForm(false,false))
            {
                memory=new GrowthFocusTests.Fixture();memory.Put(memory.Player.Block+15+36,new byte[]{5});frame=memory.Capture();var focus=options.CreateState(frame.Positions,frame.Attributes,frame.Ca,18);
                using(var preview=form.BuildGrowthPreview(memory.Player,frame,focus))
                {
                    var tabs=(TabControl)preview.Controls.Find("GrowthPreviewSections",true).Single();
                    check(tabs.TabPages.Count==2 && ((DataGridView)preview.Controls.Find("GrowthSupportPreview",true).Single()).Rows.Count==1,"preview separates primary training and explicit extra support");
                }
            }
        }
    }
}
