using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class GrowthMultiplierTests
    {
        public static void Run(Action<bool,string> check)
        {
            var settings=new GrowthOptions{Mode=GrowthMode.SelectedAttributes,Attributes=new[]{34,38}};
            check(!settings.CaGrowthMultiplierOverride.HasValue && settings.ResolveMultiplier(4)==4 && settings.ResolveMultiplier(7)==7,
                "an absent individual multiplier follows the current global multiplier");
            settings.CaGrowthMultiplierOverride=10;
            var clone=settings.Clone();
            settings.CaGrowthMultiplierOverride=2;
            check(clone.ResolveMultiplier(4)==10 && settings.ResolveMultiplier(4)==2 && clone.Attributes.SequenceEqual(settings.Attributes),
                "minimum and maximum overrides are accepted independently and cloned with the rest of the plan");
            foreach(int rate in new[]{int.MinValue,-2,0,1,11,int.MaxValue})
                check(RejectsRate(()=>settings.CaGrowthMultiplierOverride=rate) && settings.CaGrowthMultiplierOverride==2,
                    "an invalid individual rate leaves the old value intact: "+rate);
            check(RejectsRate(()=>new GrowthOptions().ResolveMultiplier(0)),"the global fallback uses the same range check");

            using(var editor=new GrowthEditorPanel())
            {
                var emitted=new List<GrowthOptions>();
                editor.OptionsChanged+=(uid,options)=>emitted.Add(options);
                editor.Bind(101,"Fixture",settings,null,true,false,globalMultiplier:4);
                check(emitted.Count==0 && editor.PersonalMultiplier.Text=="2x" && editor.FollowGlobal.Enabled,
                    "binding the personal settings displays the saved rate without writing preferences");
                editor.PersonalMultiplier.SelectedIndex=editor.PersonalMultiplier.Items.Count-1;
                check(emitted.Count==1 && emitted.Last().CaGrowthMultiplierOverride==10 &&
                    emitted.Last().Mode==GrowthMode.SelectedAttributes && emitted.Last().Attributes.SequenceEqual(new[]{34,38}),
                    "selecting a personal rate emits one preference edit and keeps the existing mode and attributes");
                editor.SetGlobalMultiplier(6);
                check(emitted.Count==1 && editor.PersonalMultiplier.Text=="10x" &&
                    (string)editor.PersonalMultiplier.Items[0]=="跟随全局(6x)",
                    "changing the global rate refreshes the follow-global option without editing a custom rate");
                editor.FollowGlobal.PerformClick();
                check(emitted.Count==2 && !emitted.Last().CaGrowthMultiplierOverride.HasValue &&
                    emitted.Last().ResolveMultiplier(6)==6 && !editor.FollowGlobal.Enabled,
                    "the reset action restores follow-global for just the selected player");
                editor.Bind(102,"Fixture B",clone,null,true,true,globalMultiplier:6);
                check(emitted.Count==2 && !editor.PersonalMultiplier.Enabled && !editor.FollowGlobal.Enabled,
                    "a running growth plan locks the personal multiplier and its reset action");
            }
            CheckStoredOptions(check);
        }

        static bool RejectsRate(Action change)
        {
            try { change(); return false; }
            catch(ArgumentOutOfRangeException) { return true; }
        }

        static void CheckStoredOptions(Action<bool,string> check)
        {
            var player=new Player{Uid=101,Name="Player A",Born=new DateTime(2005,1,2)};
            var other=new Player{Uid=101,Name="Player B",Born=new DateTime(2005,1,2)};
            string directory=Path.Combine(Path.GetTempPath(),"FM27-growth-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,GrowthPreferenceStore.FileName);
            const string format="<GrowthPreferences><Player key=\"101|20050102|Player A\" Mode=\"1\" Profile=\"3\" "+
                "DefendingProfile=\"-2\" PossessionWeight=\"75\" LimitUnselected=\"true\"{0}><Attributes>34,38</Attributes>"+
                "<Support Enabled=\"true\" Threshold=\"4\" BudgetPercent=\"30\"><Target Index=\"34\" Value=\"8\" />"+
                "<Excluded>36</Excluded></Support></Player></GrowthPreferences>";
            try
            {
                string legacy=string.Format(format,"");
                File.WriteAllText(path,legacy);
                var store=new GrowthPreferenceStore(path);
                store.Load();
                var found=store.Find(player);
                check(found!=null && found.ResolveMultiplier(4)==4 && !found.CaGrowthMultiplierOverride.HasValue &&
                    found.Mode==GrowthMode.SelectedAttributes && found.Profile==3 && found.DefendingProfile==-2 &&
                    found.PossessionWeight==75 && found.LimitUnselected && found.Attributes.SequenceEqual(new[]{34,38}) &&
                    found.Support.Enabled && found.Support.Threshold==4 && found.Support.BudgetPercent==30 &&
                    found.Support.Targets[34]==8 && found.Support.Excluded.SetEquals(new[]{36}) && File.ReadAllText(path)==legacy,
                    "an older-shape record reads additively without rewriting any of its mode, role, selection, ceiling or support data");
                check(store.Find(other)==null,"matching UIDs with different player names do not share saved overrides");
                foreach(string invalid in new[]{"-2","0","1","11","2147483648","NaN","Infinity","2.5","1e1"})
                {
                    File.WriteAllText(path,string.Format(format," CaGrowthMultiplierOverride=\""+invalid+"\""));
                    store.Load();
                    var safe=store.Find(player);
                    check(safe!=null && !safe.CaGrowthMultiplierOverride.HasValue && safe.ResolveMultiplier(6)==6 &&
                        safe.Mode==GrowthMode.SelectedAttributes && safe.Support.Targets[34]==8,
                        "invalid saved multiplier falls back while preserving the rest of the plan: "+invalid);
                }
                var concurrent=new GrowthPreferenceStore(path);
                concurrent.Load();
                found.CaGrowthMultiplierOverride=10;
                store.Save(new[]{player},new Dictionary<int,GrowthOptions>{{player.Uid,found}});
                concurrent.Save(new[]{other},new Dictionary<int,GrowthOptions>{{other.Uid,new GrowthOptions
                    {Mode=GrowthMode.CaOnly,CaGrowthMultiplierOverride=2}}});
                var reloaded=new GrowthPreferenceStore(path);
                reloaded.Load();
                check(reloaded.Find(player).ResolveMultiplier(4)==10 && reloaded.Find(other).ResolveMultiplier(4)==2 &&
                    reloaded.Find(player).Mode==GrowthMode.SelectedAttributes && reloaded.Find(player).Support.Excluded.Contains(36),
                    "round-tripping and saving from another reader preserves individual multipliers and other preferences");
                found.CaGrowthMultiplierOverride=null;
                store.Save(new[]{player},new Dictionary<int,GrowthOptions>{{player.Uid,found}});
                reloaded.Load();
                var reset=reloaded.Find(player);
                check(reset.ResolveMultiplier(7)==7 && reloaded.Find(other).ResolveMultiplier(7)==2 &&
                    reset.LimitUnselected && reset.Support.Targets[34]==8 && reset.Attributes.SequenceEqual(new[]{34,38}),
                    "reset follows a new global rate after reload without removing the original individual settings");
                using(var form=new MainForm(false,false,new GrowthPreferenceStore(path)))
                    form.CheckSavedGrowthPreferences(check,player);
            }
            finally
            {
                File.Delete(path);
                File.Delete(path+".lock");
                Directory.Delete(directory);
            }
        }

    }

    partial class MainForm
    {
        internal void CheckSavedGrowthPreferences(Action<bool,string> check,Player player)
        {
            players=new List<Player>{player};
            rosterIndex=new PlayerRosterIndex(players);
            var original=OptionsFor(player);
            check(original.Mode==GrowthMode.SelectedAttributes && original.ResolveMultiplier(GrowGear())==4 && growWatch.Count==0,
                "the workspace reads the stored player plan without restoring or starting the previous roster");
            original.CaGrowthMultiplierOverride=8;
            SaveGrowthOptions(player.Uid,original);
            growthPreferences.Clear();
            savedGrowthPreferences.Load();
            var saved=OptionsFor(player);
            check(saved.CaGrowthMultiplierOverride==8 && saved.Mode==original.Mode && saved.Attributes.SequenceEqual(original.Attributes),
                "the existing single-player settings path persists the chosen rate alongside the existing plan");
        }

        void CheckPersonalGrowthMultipliers(Action<bool,string> check,uint today,Action<uint> setDate,Action<ulong,int> putInt)
        {
            var selected=new[]{GrowthPlayer(100),GrowthPlayer(101),GrowthPlayer(102)};
            var blocks=selected.Select(player=>fm.Read(player.Block,69)).ToArray();
            var addresses=selected.Select(player=>player.Tail-FmLayout.AbilityBeforePerson).ToArray();
            int history=changes.Count;
            foreach(var player in selected)WatchPlayer(player);
            SaveGrowthOptions(100,new GrowthOptions{Mode=GrowthMode.CaOnly,CaGrowthMultiplierOverride=10});
            SaveGrowthOptions(101,new GrowthOptions{Mode=GrowthMode.CaOnly,CaGrowthMultiplierOverride=2});
            LoadGrow();
            Func<string[]> labels=()=>selected.Select(player=>(string)gridGrow.Rows.Cast<DataGridViewRow>()
                .Single(row=>((Player)row.Tag).Uid==player.Uid).Cells["GrowthMultiplier"].Value).ToArray();
            check(labels().SequenceEqual(new[]{"自定义(10x)","自定义(2x)","全局(4x)"}) && changes.Count==history,
                "the roster distinguishes three individual plans without writing to the game");
            cmbGrowSpeed.SelectedIndex=6-GrowthOptions.MinMultiplier;
            check(labels().SequenceEqual(new[]{"自定义(10x)","自定义(2x)","全局(6x)"}),
                "the roster follows changes to the global rate for unconfigured players only");
            cmbGrowSpeed.SelectedIndex=GrowthOptions.DefaultMultiplier-GrowthOptions.MinMultiplier;
            ToggleGrow();
            check(growOn && selected.Select(player=>growWatch[player.Person].Span).SequenceEqual(new[]{240,1200,600}),
                "starting growth schedules each player from his own multiplier and the same CA/PA gap");

            uint next=today+20;
            autoMonthKey=MonthKey(next);
            setDate(next);GrowTick();
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            check(addresses.Select(address=>fm.ReadI32(address)).SequenceEqual(new int?[]{(160<<16)|110,(160<<16)|102,(160<<16)|104}) &&
                selected.Select((player,index)=>fm.Read(player.Block,69).SequenceEqual(blocks[index])).All(equal=>equal),
                "the same elapsed days produce distinct CA gains, with all player attributes and positions untouched in automatic mode");
            putInt(addresses[0],(112<<16)|110);
            BeginGrowthBatch(1200,false,DayKey(next));
            PerformanceMaintenanceTests.PumpUntil(()=>pendingGrowth==null);
            check(addresses.Select(address=>fm.ReadI32(address)).SequenceEqual(new int?[]{(112<<16)|112,(160<<16)|160,(160<<16)|160}) &&
                changes.Skip(history).All(change=>addresses.Contains(change.Addr) && change.New.Length==2),
                "personal rates still observe the live PA, with only guarded two-byte CA writes");
            StopGrow(null);
            growWatch.Clear();growthPreferences.Clear();
            setDate(today);
            for(int index=0;index<addresses.Length;index++)putInt(addresses[index],(160<<16)|100);
            LoadGrow();
        }
    }
}
