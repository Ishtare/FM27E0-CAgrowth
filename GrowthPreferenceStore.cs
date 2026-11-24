// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Fm24Editor
{
    sealed class GrowthPreferenceStore
    {
        internal const string FileName = "growth-preferences.xml";
        readonly string path;
        Dictionary<string,GrowthOptions> entries = new Dictionary<string,GrowthOptions>(StringComparer.Ordinal);

        public GrowthPreferenceStore(string path) { this.path=path; }

        // Require an identity that distinguishes generated players with the same UID in different saves.
        static string Key(Player player)
        {
            if(player==null || player.Uid<=0 || !player.Born.HasValue || string.IsNullOrWhiteSpace(player.Name))return null;
            return player.Uid.ToString(CultureInfo.InvariantCulture)+"|"+
                player.Born.Value.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"|"+player.Name;
        }

        public GrowthOptions Find(Player player)
        {
            string key=Key(player);
            GrowthOptions options;
            return key!=null && entries.TryGetValue(key,out options)?options.Clone():null;
        }

        public void Load() { entries=Index(ReadDocument()); }

        public void Save(IEnumerable<Player> players,IDictionary<int,GrowthOptions> preferences)
        {
            var updates=players.Select(player=>new {Player=player,Key=Key(player)})
                .Where(update=>update.Key!=null).ToArray();
            if(updates.Length==0)return;

            // Merge with the latest file under an exclusive lock. Publish the new file atomically.
            using(var locked=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                var document=ReadDocument();
                var keys=new HashSet<string>(updates.Select(update=>update.Key),StringComparer.Ordinal);
                document.Elements("Player").Where(entry=>keys.Contains((string)entry.Attribute("key"))).Remove();
                foreach(var update in updates)
                    document.Add(WriteOptions(update.Key,preferences[update.Player.Uid]));
                var next=Index(document);
                string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    document.Save(temp);
                    if(File.Exists(path))File.Replace(temp,path,null);
                    else File.Move(temp,path);
                    entries=next;
                }
                finally { if(File.Exists(temp))File.Delete(temp); }
            }
        }

        XElement ReadDocument()
        {
            try
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
                {
                    var root=XElement.Load(reader);
                    if(root.Name!="GrowthPreferences")throw new InvalidDataException("个人培养配置格式无法识别。");
                    return root;
                }
            }
            catch(FileNotFoundException) { return new XElement("GrowthPreferences"); }
        }

        static Dictionary<string,GrowthOptions> Index(XElement document)
        {
            var result=new Dictionary<string,GrowthOptions>(StringComparer.Ordinal);
            foreach(var entry in document.Elements("Player"))
            {
                string key=(string)entry.Attribute("key");
                if(string.IsNullOrEmpty(key))continue;
                try { result[key]=ReadOptions(entry); }
                catch(FormatException) { } // Leave malformed records in the file.
                catch(OverflowException) { }
                catch(InvalidDataException) { }
            }
            return result;
        }

        static GrowthOptions ReadOptions(XElement entry)
        {
            var options=new GrowthOptions
            {
                Mode=(GrowthMode)((int?)entry.Attribute("Mode")??(int)GrowthMode.CaOnly),
                Profile=(int?)entry.Attribute("Profile")??-1,
                DefendingProfile=(int?)entry.Attribute("DefendingProfile")??-1,
                PossessionWeight=(int?)entry.Attribute("PossessionWeight")??50,
                LimitUnselected=(bool?)entry.Attribute("LimitUnselected")??false,
                Attributes=ReadIndices((string)entry.Element("Attributes"))
            };
            if(!Enum.IsDefined(typeof(GrowthMode),options.Mode) || options.Profile< -1 || options.Profile>=GrowthProfile.All.Length ||
                options.DefendingProfile< -2 || options.DefendingProfile>=GrowthProfile.All.Length ||
                options.PossessionWeight<0 || options.PossessionWeight>100 || options.Attributes.Any(i=>!GrowthProfile.Developable(i)))
                throw new InvalidDataException("个人培养配置含有无效选项。");

            // Legacy records omit this attribute. A corrupt multiplier cannot discard the other options.
            int rate;
            if(int.TryParse((string)entry.Attribute("CaGrowthMultiplierOverride"),NumberStyles.Integer,CultureInfo.InvariantCulture,out rate) &&
                GrowthOptions.IsValidMultiplier(rate))options.CaGrowthMultiplierOverride=rate;

            var support=entry.Element("Support");
            if(support!=null)
            {
                options.Support.Enabled=(bool?)support.Attribute("Enabled")??false;
                options.Support.Threshold=(int?)support.Attribute("Threshold")??options.Support.Threshold;
                options.Support.BudgetPercent=(int?)support.Attribute("BudgetPercent")??options.Support.BudgetPercent;
                foreach(var target in support.Elements("Target"))
                    options.Support.Targets[(int?)target.Attribute("Index")??-1]=(int?)target.Attribute("Value")??0;
                foreach(int excluded in ReadIndices((string)support.Element("Excluded")))options.Support.Excluded.Add(excluded);
                options.Support.Validate();
            }
            return options;
        }

        static int[] ReadIndices(string text)
        {
            return string.IsNullOrEmpty(text)?new int[0]:
                text.Split(',').Select(item=>int.Parse(item,CultureInfo.InvariantCulture)).ToArray();
        }

        static XElement WriteOptions(string key,GrowthOptions options)
        {
            return new XElement("Player",new XAttribute("key",key),new XAttribute("Mode",(int)options.Mode),
                new XAttribute("Profile",options.Profile),new XAttribute("DefendingProfile",options.DefendingProfile),
                new XAttribute("PossessionWeight",options.PossessionWeight),new XAttribute("LimitUnselected",options.LimitUnselected),
                options.CaGrowthMultiplierOverride.HasValue?
                    new XAttribute("CaGrowthMultiplierOverride",options.CaGrowthMultiplierOverride.Value):null,
                new XElement("Attributes",string.Join(",",options.Attributes)),
                new XElement("Support",new XAttribute("Enabled",options.Support.Enabled),
                    new XAttribute("Threshold",options.Support.Threshold),new XAttribute("BudgetPercent",options.Support.BudgetPercent),
                    new XElement("Excluded",string.Join(",",options.Support.Excluded.OrderBy(i=>i))),
                    options.Support.Targets.OrderBy(pair=>pair.Key).Select(pair=>new XElement("Target",
                        new XAttribute("Index",pair.Key),new XAttribute("Value",pair.Value)))));
        }
    }
}
