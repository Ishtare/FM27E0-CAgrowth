// Modified build v1.1.22, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Fm24Editor
{
    // An entry identifies a person; translated surnames are never used to identify people.
    class OfficialName
    {
        public int Uid;
        public string English, Chinese, Aliases, Source;
        public DateTime? Born;
    }
    static class PlayerNames
    {
        static List<OfficialName> entries = new List<OfficialName>();
        static HashSet<string> singleNames=new HashSet<string>(StringComparer.Ordinal);
        public static string LoadNote = "";
        public static string CoverageNote = "";
        public static int Count { get { return entries.Count; } }
        [ThreadStatic] static string previousQuery,previousQueryKey;
        public static string QueryKey(string value)
        {if(!object.ReferenceEquals(value,previousQuery) || previousQueryKey==null){previousQuery=value;previousQueryKey=Normalize(value);}return previousQueryKey;}
        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var b = new StringBuilder();
            foreach (char ch in value.Normalize(NormalizationForm.FormKD))
            {
                char c=char.ToLowerInvariant(ch);
                // These letters do not decompose under FormKD (for example Martin Odegaard / Ødegaard).
                switch(c)
                {
                    case 'ø':b.Append('o');break;case 'ł':b.Append('l');break;
                    case 'đ':case 'ð':b.Append('d');break;case 'ı':b.Append('i');break;
                    case 'æ':b.Append("ae");break;case 'œ':b.Append("oe");break;
                    case 'ß':b.Append("ss");break;case 'þ':b.Append("th");break;
                    default:if(char.IsLetterOrDigit(c))b.Append(c);break;
                }
            }
            return b.ToString();
        }
        static string[] Tokens(string value)
        {
            var text=new StringBuilder();
            foreach(char ch in (value??""))text.Append(char.IsLetterOrDigit(ch)||CharUnicodeInfo.GetUnicodeCategory(ch)==UnicodeCategory.NonSpacingMark?ch:' ');
            return text.ToString().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(n=>n.Length>0).ToArray();
        }
        public static bool CompatibleFullNames(string a,string b)
        {
            var x=Tokens(a);var y=Tokens(b);
            if(x.Length>y.Length){var temp=x;x=y;y=temp;}
            // Birthday plus at least two complete English name tokens, same first/last token and order.
            // A surname, initial or birthday alone can never bind a translated identity.
            if(x.Length<2 || x.Any(n=>n.Length<2) || x.Distinct().Count()<2 || x[0]!=y.FirstOrDefault() || x[x.Length-1]!=y.LastOrDefault())return false;
            int next=0;foreach(string token in y)if(next<x.Length && token==x[next])next++;
            return next==x.Length;
        }
        static string AliasUnion(params string[] groups)
        {
            return string.Join("|",groups.Where(g=>!string.IsNullOrEmpty(g)).SelectMany(g=>g.Split('|')).Where(n=>Normalize(n).Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
        [ThreadStatic] static string previousUidQuery;
        [ThreadStatic] static int previousUid;
        public static bool TryUid(string query,out int uid)
        {
            if (object.ReferenceEquals(query,previousUidQuery))
            {
                uid=previousUid;
                return uid>0;
            }
            string value=(query??"").Trim().Normalize(NormalizationForm.FormKC);
            if(value.StartsWith("UID",StringComparison.OrdinalIgnoreCase))value=value.Substring(3).TrimStart(':','：',' ','#');
            else if(value.StartsWith("#"))value=value.Substring(1);
            if (!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out uid) || uid<1) uid=0;
            previousUidQuery=query;
            previousUid=uid;
            return uid>0;
        }
        public static bool CanSearch(string query) { int uid;return TryUid(query,out uid) || Normalize(query).Length >= 2; }
        public static bool CanSearchPlayer(string query)
        {
            int uid;if(TryUid(query,out uid))return true;
            string q=Normalize(query);
            return CanSearch(q) || (q.Length==1 && q[0]>='\u3400' && q[0]<='\u9fff' && singleNames.Contains(q));
        }
        public static bool Matches(string names, string query)
        {
            string q = Normalize(query);
            if(q.Length==1 && q[0]>='\u3400' && q[0]<='\u9fff')return (names??"").Split('|').Any(n=>Normalize(n)==q);
            return q.Length > 0 && (names ?? "").Split('|').Any(n => Normalize(n).IndexOf(q, StringComparison.Ordinal) >= 0);
        }
        public static int MatchRank(string names,string query)
        {
            string q=Normalize(query);var values=(names??"").Split('|').Select(Normalize).ToArray();
            return values.Any(n=>n==q)?0:values.Any(n=>n.StartsWith(q,StringComparison.Ordinal))?1:2;
        }
        public static void RememberBirthday(Player player,string directory)
        {
            if(player.FallbackName!=null)return; // Approximate/composed labels never become verified identity records.
            if(player.ChineseName==null || !player.Born.HasValue || string.IsNullOrEmpty(player.NameSource))return;
            var entry=new OfficialName {Uid=player.Uid,English=player.Name,Born=player.Born,Chinese=player.ChineseName,Aliases=player.NameAliases,Source=player.NameSource};
            if(entries.Any(e=>e.Uid==entry.Uid && e.Born==entry.Born && e.English==entry.English && e.Chinese==entry.Chinese))return;
            entries=entries.Concat(new[]{entry}).ToList();
            try
            {
                Func<string,string> cell=value=>(value??"").Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');
                string folder=Path.Combine(directory,"names");Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder,"birthday-identities.tsv"),string.Join("\t",new[]{entry.Uid.ToString(CultureInfo.InvariantCulture),cell(entry.English),entry.Born.Value.ToString("yyyy-MM-dd"),cell(entry.Chinese),cell(entry.Aliases),cell(entry.Source)})+Environment.NewLine,Encoding.UTF8);
            }
            catch(Exception ex){LoadNote="年龄修改后的姓名对应暂未保存："+ex.Message;}
        }
        public static List<OfficialName> Read(TextReader reader)
        {
            var result = new List<OfficialName>(); string line; int number = 0;
            while ((line = reader.ReadLine()) != null)
            {
                number++; if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("uid\t",StringComparison.OrdinalIgnoreCase)) continue;
                var c = line.Split('\t'); int uid; DateTime born; Uri url;
                if (c.Length != 6 || !int.TryParse(c[0], out uid) || uid < 0 || string.IsNullOrWhiteSpace(c[1]) || string.IsNullOrWhiteSpace(c[3]) ||
                    !Uri.TryCreate(c[5], UriKind.Absolute, out url) || (url.Scheme != "https" && url.Scheme != "http"))
                    throw new FormatException("姓名对照第 " + number + " 行格式不正确，须包含 6 列及来源网址。");
                DateTime? birthday = null;
                if (c[2].Length > 0)
                {
                    if (!DateTime.TryParseExact(c[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out born)) throw new FormatException("姓名对照第 " + number + " 行出生日期无效。");
                    birthday = born;
                }
                if (!c[3].Any(ch => ch >= '\u3400' && ch <= '\u9fff')) throw new FormatException("姓名对照第 " + number + " 行缺少中文姓名。");
                result.Add(new OfficialName {Uid=uid,English=c[1].Trim(),Born=birthday,Chinese=c[3].Trim(),Aliases=c[4].Trim(),Source=c[5].Trim()});
            }
            return result;
        }
        public static void Load(string directory) { Load(directory,true); }
        public static void Load(string directory,bool initializeFullBank)
        {
            var loaded = new List<OfficialName>(); var notes = new List<string>();
            using (var stream = typeof(PlayerNames).Assembly.GetManifestResourceStream("FM27E0.official-names.tsv"))
                if (stream != null) using(var reader = new StreamReader(stream,Encoding.UTF8)) loaded.AddRange(Read(reader));
            string folder = Path.Combine(directory,"names");
            if (Directory.Exists(folder))
                foreach(string file in Directory.GetFiles(folder,"*.tsv").OrderBy(x=>x,StringComparer.Ordinal))
                    try { using(var reader=new StreamReader(file,Encoding.UTF8,true)) loaded.AddRange(Read(reader)); }
                    catch(Exception ex) { notes.Add(Path.GetFileName(file)+"："+ex.Message); }
            entries=loaded;
            singleNames=new HashSet<string>(entries.SelectMany(e=>(e.Chinese+"|"+e.Aliases).Split('|')).Select(Normalize).Where(n=>n.Length==1 && n[0]>='\u3400' && n[0]<='\u9fff'),StringComparer.Ordinal);
            if(initializeFullBank)NameFallback.Initialize();
            LoadNote=string.Join("\n",notes.ToArray());
            using(var stream=typeof(PlayerNames).Assembly.GetManifestResourceStream("FM27E0.name-coverage.txt"))
                CoverageNote=stream==null?"":new StreamReader(stream,Encoding.UTF8).ReadToEnd();
        }
        public static void ApplyOfficial(IList<Player> players)
        {
            foreach(var p in players) { p.ChineseName=null; p.NameSource=null; p.NameAliases=null;p.FallbackName=null; }
            var byName = new Dictionary<string,List<Player>>(StringComparer.Ordinal);
            var byUid = new Dictionary<int,List<Player>>();
            var byBirthday = new Dictionary<DateTime,List<Player>>();
            foreach(var p in players)
            {
                List<Player> same;
                if(!byUid.TryGetValue(p.Uid,out same)) byUid[p.Uid]=same=new List<Player>(); same.Add(p);
                if(p.Born.HasValue)
                {if(!byBirthday.TryGetValue(p.Born.Value.Date,out same))byBirthday[p.Born.Value.Date]=same=new List<Player>();same.Add(p);}
                foreach(string name in p.OriginalNameKeys())
                { if(!byName.TryGetValue(name,out same))byName[name]=same=new List<Player>(); same.Add(p); }
            }
            foreach(var entry in entries)
            {
                List<Player> candidates;
                if(entry.Uid>0) { if(!byUid.TryGetValue(entry.Uid,out candidates))continue; }
                else
                {
                    candidates=new List<Player>();
                    foreach(string name in entry.English.Split('|'))
                    { List<Player> same;if(byName.TryGetValue(Normalize(name),out same))candidates.AddRange(same); }
                }
                // An explicit UID plus the exact original English name remains valid after an age edit.
                // Entries without UID still use birthday and uniqueness to disambiguate names.
                var matches=candidates.Distinct().Where(p=>entry.Uid>0 || !entry.Born.HasValue || p.Born==entry.Born).Where(p=>
                    entry.English.Split('|').Any(n=>Array.IndexOf(p.OriginalNameKeys(),Normalize(n))>=0)).ToList();
                if(matches.Count==0 && entry.Uid==0 && entry.Born.HasValue && byBirthday.TryGetValue(entry.Born.Value.Date,out candidates))
                    matches=candidates.Where(p=>entry.English.Split('|').Any(n=>(p.Search??p.Name).Split('|').Any(original=>CompatibleFullNames(n,original)))).Distinct().ToList();
                if(matches.Count!=1)continue; // Missing/ambiguous identities are left in their original form.
                var target=matches[0];
                string preferred=FMPersonChinese.NameDictionary.IsChineseName(entry.Chinese)?entry.Chinese:
                    (entry.Aliases??"").Split('|').FirstOrDefault(FMPersonChinese.NameDictionary.IsChineseName)??entry.Chinese;
                if(target.ChineseName==null || (FMPersonChinese.NameDictionary.HasForeignLetters(target.ChineseName) && FMPersonChinese.NameDictionary.IsChineseName(preferred)))
                {target.ChineseName=preferred;target.NameSource=entry.Source;}
                // Keep one valid source URL for the preferred display name; all source rows remain in the TSV.
                target.NameAliases=AliasUnion(target.NameAliases,entry.Chinese,entry.Aliases,entry.English);
            }
        }
        public static void Apply(IList<Player> players)
        {
            ApplyOfficial(players);
            foreach(var player in players.Where(p=>p.ChineseName==null || FMPersonChinese.NameDictionary.HasForeignLetters(p.ChineseName)))
            {
                var result=NameFallback.Translate(player);if(result==null)continue;
                string previous=player.ChineseName;player.FallbackName=result;player.ChineseName=result.Chinese;
                player.NameAliases=AliasUnion(player.NameAliases,previous,result.Chinese);
            }
            singleNames=new HashSet<string>(singleNames.Concat(players.Where(p=>p.ChineseName!=null).Select(p=>Normalize(p.ChineseName))).Where(n=>n.Length==1 && n[0]>='\u3400' && n[0]<='\u9fff'),StringComparer.Ordinal);
        }
        public static string Description(Player p)
        {
            if(p.FallbackName!=null)return NameFallback.Description(p.FallbackName);
            return p.ChineseName==null ? "暂无官方中文译名对照，保留游戏原名；可以用英文或原名搜索。" : "中文译名来源："+new Uri(p.NameSource).Host+"（完整来源见随附姓名对照表）";
        }
        public static void ApplyStaff(IList<Staff> staff)
        {
            var views=new List<Player>();
            foreach(var person in staff)
            {
                var parts=person.NameParts;
                var view=new Player {Uid=person.Uid,Name=person.Name,Search=person.Search,Born=person.Born,NameNationUid=person.NameNationUid};
                if(parts!=null)view.NameParts=new PersonNameParts {First=parts.First,Last=parts.Last,Common=parts.Common,Full=parts.Full,Search=person.Search};
                person.NameView=view;views.Add(view);
            }
            Apply(views);
        }
    }

    partial class MainForm
    {
        bool loadingNameCatalog;
        async void ShowNameCatalog()
        {
            if(loadingNameCatalog)return;
            loadingNameCatalog=true;
            try{await System.Threading.Tasks.Task.Run((Action)NameFallback.Initialize);}
            finally{loadingNameCatalog=false;}
            if(IsDisposed || Disposing)return;
            var dialog=new Form {Text="球员中英文姓名对照",Width=800,Height=650,StartPosition=FormStartPosition.CenterParent};
            var info=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
            Action refresh=()=>info.Text="已核对人物姓名对照 "+PlayerNames.Count+" 条；游戏姓名组成词条 "+NameFallback.WordRows.ToString("N0")+" 条，完整／条件词条 "+NameFallback.ContextRows.ToString("N0")+" 条。\r\n"+
                "当前中文显示 "+players.Count(p=>p.ChineseName!=null)+" / "+players.Count+"：人物对照 "+players.Count(p=>p.ChineseName!=null && p.FallbackName==null)+"，词库／组合 "+players.Count(p=>p.FallbackName!=null && !p.FallbackName.Automatic)+"，含自动音译 "+players.Count(p=>p.FallbackName!=null && p.FallbackName.Automatic)+"。\r\n\r\n"+
                "职员中文显示："+staff.Count(s=>s.NameView!=null && s.NameView.ChineseName!=null)+" / "+staff.Count+"，使用相同的姓名规则与双语搜索。\r\n\r\n"+
                "〔词库〕是游戏完整词条，〔组合〕是姓名组成词条的组合，〔音译〕含近似读音规则，不声称是已确认的人物官方全名。已有译名优先，缺失姓名及未来新组合自动补充。\r\n\r\n"+
                "支持中文或英文、完整或部分姓名；忽略英文重音、大小写、空格和姓名间隔符。至少输入 2 个字符；一字中文名按完整姓名匹配。\r\n"+
                "未命中人物对照时使用词库组合或音译；不会把另一人的已确认全名套用给同名人物。\r\n\r\n"+
                "优先人物对照包括五大联赛 96 队、中超 16 队、48 支国家队及其他欧洲 16 队，并单独补入本泽马。完整词库和自动补全不受这些球队名单限制。原对照数量、来源和待补项目可点击下方查看。\r\n\r\n"+
                "可导入 UTF-8 制表符分隔的 .tsv 对照表，每行 6 列：\r\n球员 UID（未知填 0）／英文姓名／出生日期（可留空）／中文译名／其他搜索名（用 | 分隔）／来源网址\r\n\r\n"+
                "优先采用已确认译名，也支持有来源的可靠惯用名。自动音译会单独标识。程序只影响修改器显示与检索，保留游戏原名。\r\n\r\n"+PlayerNames.LoadNote+NameFallback.LoadNote;
            var coverage=new Button {Text="查看姓名库收录范围…",Dock=DockStyle.Bottom,Height=34};
            coverage.Click+=delegate
            {
                using(var window=new Form {Text="中文姓名库收录范围",Width=850,Height=650,StartPosition=FormStartPosition.CenterParent})
                {
                    window.Controls.Add(new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=PlayerNames.CoverageNote});window.ShowDialog(dialog);
                }
            };
            var import=new Button {Text="导入姓名对照…",Dock=DockStyle.Bottom,Height=34};
            import.Click+=delegate
            {
                using(var picker=new OpenFileDialog {Filter="姓名对照表 (*.tsv)|*.tsv",Title="选择有官方来源的姓名对照表"})
                if(picker.ShowDialog(dialog)==DialogResult.OK)
                try
                {
                    using(var reader=new StreamReader(picker.FileName,Encoding.UTF8,true)) { if(PlayerNames.Read(reader).Count==0)throw new FormatException("对照表中没有有效记录。"); }
                    string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"names");Directory.CreateDirectory(folder);
                    File.Copy(picker.FileName,Path.Combine(folder,"import-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".tsv"));
                    PlayerNames.Load(AppDomain.CurrentDomain.BaseDirectory);PlayerNames.Apply(players);PlayerNames.ApplyStaff(staff);FilterPlayers();FilterStaff();LoadGrow();LoadSquad();refresh();
                }
                catch(Exception ex) { MessageBox.Show("导入失败："+ex.Message,"姓名对照"); }
            };
            dialog.Controls.Add(info);dialog.Controls.Add(coverage);dialog.Controls.Add(import);refresh();dialog.ShowDialog(this);
        }
    }
}
