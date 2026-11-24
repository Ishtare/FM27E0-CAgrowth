using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Fm24Editor
{
    static class GrowthRoleCatalogTests
    {
        public sealed class Catalog
        {
            public int detail_presets { get; set; }
            public Preset[] roles { get; set; }
        }

        public sealed class Preset
        {
            public string group { get; set; }
            public string phase { get; set; }
            public string english { get; set; }
            public string chinese { get; set; }
            public int[] core { get; set; }
            public int[] support { get; set; }
            public Dictionary<string,int> within_tier_priority { get; set; }
        }

        public static void Run(Action<bool,string> check)
        {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","role-data","growth-roles.json");
            var data=new JavaScriptSerializer().Deserialize<Catalog>(File.ReadAllText(path));
            var catalog=GrowthProfile.All.Where(role=>role.Phase!="基本").ToArray();
            check(data.detail_presets==88 && data.roles.Length==88 && catalog.Length==88,
                "the source JSON and compiled catalog contain all 88 detailed role presets");

            var indices=Enumerable.Range(0,FmLayout.Attrs.Length).ToArray();
            var keys=indices.Select(i=>i.ToString(CultureInfo.InvariantCulture)).ToArray();
            for(int row=0;row<catalog.Length;row++)
            {
                var source=data.roles[row];
                var compiled=catalog[row];
                string context=source.group+" / "+source.phase+" / "+source.english;
                check(compiled.Group==source.group && compiled.Phase==source.phase &&
                    compiled.Name==source.chinese && compiled.English==source.english,
                    context+" has the same role identity in the JSON and compiler");
                check(compiled.Core.SequenceEqual(source.core) && compiled.Supporting.SequenceEqual(source.support),
                    context+" has the same core and supporting attribute lists");
                var priorities=source.within_tier_priority;
                check(priorities!=null && priorities.Keys.All(keys.Contains) &&
                    priorities.Values.All(value=>value==0 || value==1) && priorities.Values.Contains(1),
                    context+" has a valid nonempty binary within-tier priority map");
                var expected=keys.Select(key=>{int value;return priorities.TryGetValue(key,out value)?value:0;});
                check(expected.SequenceEqual(indices.Select(compiled.WithinTierPriority)),
                    context+" has identical within-tier priorities for every attribute");
            }

            foreach(var basic in GrowthProfile.All.Where(role=>role.Phase=="基本"))
            {
                var group=catalog.First(role=>role.Group==basic.Group);
                check(indices.Select(basic.WithinTierPriority).SequenceEqual(indices.Select(group.WithinTierPriority)),
                    basic.Group+" uses the same priorities with the automatic position preset");
            }
            var unranked=new GrowthProfile("Fixture",new[]{34},new int[0]);
            check(indices.All(i=>unranked.WithinTierPriority(i)==0),"an omitted priority map defaults to ordinary priority");
        }
    }
}
