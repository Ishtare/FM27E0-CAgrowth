// Modified build notice: FM27E0-CAgrowth v1.1.12; changes dated 2026-11-15.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt. Independently implemented with the
// published rule representation credited in docs/registration-evidence.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    sealed class EligibilityCompetition
    {
        public ulong Address;
        public int Uid;
        public string Name;
        public override string ToString(){return Name+" ["+Uid+"]";}
        static readonly Dictionary<int,string> SearchShortcuts=new Dictionary<int,string> {{11,"英超 EPL Premier League"},{12,"英冠 Championship"},{32,"意甲 Serie A"},{67,"西甲 LaLiga La Liga"},{68,"西乙 LaLiga2"},{16,"法甲 Ligue 1 Ligue1"},{17,"法乙 Ligue 2 Ligue2"},{22,"德甲 Bundesliga"},{23,"德乙 Bundesliga 2"}};
        public bool Matches(string query)
        {
            query=(query??"").Trim();if(query.Length==0)return true;
            int uid;if(PlayerNames.TryUid(query,out uid))return uid==Uid;
            string aliases;
            // Search shortcuts only. Rules and writable identities always come from the loaded save.
            SearchShortcuts.TryGetValue(Uid,out aliases);
            return ((Name??"")+" "+aliases).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;
        }
    }
    sealed class LeagueEligibilitySnapshot
    {
        public EligibilityReader Reader;
        public EligibilityCompetition Competition;
        public int Limit;
        public int RuleCode;
        public string RuleLabel {get{return Labels[RuleCode];}}
        public bool IsMinimum {get{return MinimumCodes.Contains(RuleCode);}}
        public string DirectionLabel {get{return IsMinimum?"至少":"至多";}}
        public int RelaxedLimit {get{return IsMinimum?0:1000;}}
        public int MinimumValue {get{return RuleCode==0x09?1:0;}}
        public static readonly Dictionary<int,string> Labels=new Dictionary<int,string> {
            {0x02,"首发外援人数上限"},{0x03,"替补席外援人数上限"},
            {0x05,"首发非欧球员人数上限"},{0x06,"替补席非欧球员人数上限"},
            {0x12,"非欧球员注册名单上限"},{0x15,"外援注册名单上限"},
            {0x04,"外援比赛名单上限"},{0x07,"非欧球员比赛名单上限"},
            {0x1E,"每赛季从国外引进非欧球员上限"},
            {0x09,"报名名单总人数上限"},{0x67,"非本土培养球员名单上限"},
            {0x0B,"俱乐部培养球员最低人数（忽略租借期）"},
            {0x26,"俱乐部培养球员最低人数"},{0x27,"本国培养球员最低人数"},
            {0x91,"俱乐部培养球员最低人数（0–21 岁阶段）"},
            {0x92,"本国培养球员最低人数（0–21 岁阶段）"},
            {0xFD,"俱乐部或本国培养球员最低人数（0–21 岁阶段）"},
            {0xD4,"本国籍球员最低人数"}};
        public static readonly HashSet<int> MinimumCodes=new HashSet<int> {0x0B,0x26,0x27,0x91,0x92,0xD4,0xFD};
        static readonly HashSet<int> ForeignCodes=new HashSet<int> {0x02,0x03,0x04,0x05,0x06,0x07,0x12,0x15,0x1E};
        public ulong[] Addresses;
        public readonly HashSet<int> SharedIds=new HashSet<int>();
        static Dictionary<string,ulong> Dictionary(EligibilityReader r,ulong header)
        {
            var values=new Dictionary<string,ulong>(StringComparer.Ordinal);
            foreach(ulong row in r.Vector(header,16))
            {
                string key=Encoding.ASCII.GetString(r.Bytes(row,4));
                if(values.ContainsKey(key))throw new InvalidDataException("非欧规则包含重复索引，暂不修改。");
                values.Add(key,r.Ptr(row+8));
            }
            return values;
        }
        static Dictionary<string,ulong> Node(EligibilityReader r,ulong node){return Dictionary(r,r.Ptr(node+8));}
        static bool TryIdentity(EligibilityReader r,Dictionary<string,ulong> entry,out int uid)
        {
            uid=0;ulong node,value;
            if(!entry.TryGetValue("pmoc",out node))return false;
            if(!Node(r,node).TryGetValue("pmoc",out value))throw new InvalidDataException("赛事规则身份记录不完整。");
            uid=r.Int(value+8);return true;
        }
        static List<ulong> RuleValues(EligibilityReader r,Dictionary<string,ulong> entry,int code)
        {
            ulong list;if(!entry.TryGetValue("rsqs",out list))return new List<ulong>();
            var result=new List<ulong>();
            foreach(ulong item in r.Pointers(r.Ptr(list+8)))
            {
                var fields=Node(r,item);ulong type,value;
                if(fields.TryGetValue("epyt",out type) && fields.TryGetValue("ulav",out value) && r.Int(type+8)==code)result.Add(value+8);
            }
            return result;
        }
        public static int[] Codes(Func<ulong,int,byte[]> read,ulong moduleBase,EligibilityCompetition competition)
        {
            try
            {
                var r=new EligibilityReader(read);
                if(r.Ptr(competition.Address)!=moduleBase+EligibilityLayout.CompetitionVt || r.Int(competition.Address+12)!=competition.Uid)return new int[0];
                ulong holder=r.Ptr(competition.Address+0xB0);
                if(r.Ptr(holder)!=moduleBase+EligibilityLayout.RulesHolderVt)return new int[0];
                byte index=r.Byte(holder+0x31);ulong groups=r.Ptr(r.Ptr(holder+0x370)+0x1E0);
                var group=r.Pointers(groups+0x60).Where(a=>r.Byte(a+0x120)==index).ToArray();
                if(group.Length!=1)return new int[0];
                return r.Pointers(r.Ptr(group[0]+0x70)).Select(a=>(int)r.Short(a+0x1C)).Where(Labels.ContainsKey).Distinct().ToArray();
            }
            catch{return new int[0];}
        }
        public static string Overview(Func<ulong,int,byte[]> read,ulong moduleBase,EligibilityCompetition competition)
        {
            var r=new EligibilityReader(read);
            if(r.Ptr(competition.Address)!=moduleBase+EligibilityLayout.CompetitionVt || r.Int(competition.Address+12)!=competition.Uid)
                throw new InvalidDataException("赛事身份已变化，请重新读取。");
            ulong holder=r.Ptr(competition.Address+0xB0);
            if(r.Ptr(holder)!=moduleBase+EligibilityLayout.RulesHolderVt)throw new InvalidDataException("该赛事没有可核实的报名规则资料。");
            byte index=r.Byte(holder+0x31);ulong groups=r.Ptr(r.Ptr(holder+0x370)+0x1E0);
            var group=r.Pointers(groups+0x60).Where(a=>r.Byte(a+0x120)==index).ToArray();
            if(group.Length!=1)throw new InvalidDataException("该赛事报名规则分组不唯一，暂不能读取。");
            var rows=r.Pointers(r.Ptr(group[0]+0x70)).Where(a=>Labels.ContainsKey(r.Short(a+0x1C))).ToArray();
            if(rows.Length==0)return "没有读取到本版已识别的外援、非欧或本国／青训人数规则；这不表示所有报名限制均不存在。";
            var lines=new List<string>();
            if(!rows.Any(a=>ForeignCodes.Contains(r.Short(a+0x1C))))
                lines.Add("当前读取到报名总人数或本国／青训要求，未发现直接的外援或非欧人数上限。");
            foreach(ulong at in rows)
            {
                int code=r.Short(at+0x1C);bool minimum=MinimumCodes.Contains(code);
                string state=r.Int(at)!=0?"（附加条件，只读）":r.Byte(at+0x1E)!=(minimum?1:255)?"（方向未核实，只读）":"";
                lines.Add(Labels[code]+"："+(minimum?"至少 ":"至多 ")+r.Int(at+0x18)+" 人"+state);
            }
            var totals=rows.Where(a=>r.Short(a+0x1C)==0x09 && r.Int(a)==0 && r.Byte(a+0x1E)==255).ToArray();
            var trained=rows.Where(a=>(r.Short(a+0x1C)==0x27 || r.Short(a+0x1C)==0x92) && r.Int(a)==0 && r.Byte(a+0x1E)==1).ToArray();
            if(totals.Length==1 && trained.Length==1)
            {
                int total=r.Int(totals[0]+0x18),home=r.Int(trained[0]+0x18);
                if(total>0 && home>=0 && home<=total)
                    lines.Add("按总名额与本国培养保留位计算：非本土培养名额至多 "+(total-home)+" 人（"+total+" − "+home+"）。其他规则仍分别判断。");
            }
            if(!r.Matches(read))throw new InvalidDataException("赛事规则正在更新，请暂停游戏后重新读取。");
            return string.Join("\r\n",lines.ToArray());
        }
        public static LeagueEligibilitySnapshot Capture(Func<ulong,int,byte[]> read,ulong moduleBase,EligibilityCompetition competition,IList<EligibilityCompetition> peers,int ruleCode=0x12)
        {
            if(!Labels.ContainsKey(ruleCode))throw new ArgumentException("该外援规则类型尚未核实。");
            var s=new LeagueEligibilitySnapshot {Reader=new EligibilityReader(read),Competition=competition,RuleCode=ruleCode};var r=s.Reader;
            if(r.Ptr(competition.Address)!=moduleBase+EligibilityLayout.CompetitionVt || r.Int(competition.Address+12)!=competition.Uid)
                throw new InvalidDataException("赛事身份已变化，请重新打开。");
            EligibilityLayout.CaptureDate(r,moduleBase);
            ulong holder=r.Ptr(competition.Address+0xB0);
            if(r.Ptr(holder)!=moduleBase+EligibilityLayout.RulesHolderVt)throw new InvalidDataException("该赛事的注册规则尚未核实。");
            byte index=r.Byte(holder+0x31);ulong common=r.Ptr(holder+0x370),groups=r.Ptr(common+0x1E0);
            var group=r.Pointers(groups+0x60).Where(a=>r.Byte(a+0x120)==index).ToArray();
            if(group.Length!=1)throw new InvalidDataException("赛事规则分组不唯一，当前保持只读。");
            var primary=r.Pointers(r.Ptr(group[0]+0x70)).Where(a=>r.Short(a+0x1C)==ruleCode).ToArray();
            if(primary.Length==0)throw new InvalidOperationException("这项赛事没有采用所选的通用外援人数规则；特殊资格仍需单独处理。");
            if(primary.Length!=1 || r.Int(primary[0])!=0)throw new InvalidOperationException("所选规则存在多条或按球队等条件划分的分支，当前仅查看各条现值，不改动这些条件。");
            // +0x1F is independent metadata; only +0x1E is the min/max direction.
            if(r.Byte(primary[0]+0x1E)!=(s.IsMinimum?1:255))throw new InvalidDataException("所选规则的最低／最高人数方向未通过检查，暂不修改。");
            ulong primaryValue=primary[0]+0x18;s.Limit=r.Int(primaryValue);
            if(s.Limit<s.MinimumValue || s.Limit>1000)throw new InvalidDataException("报名规则人数超出本版核实范围。");
            ulong nation=r.Ptr(competition.Address+0x60);
            if(r.Ptr(nation)!=moduleBase+EligibilityLayout.NationVt)throw new InvalidDataException("赛事国家规则结构尚未核实。");
            ulong root=r.Ptr(nation+0x108);
            if(r.Ptr(root)!=moduleBase+EligibilityLayout.RulesContainerVt)throw new InvalidDataException("赛事规则容器未通过检查。");
            var sources=new List<Dictionary<string,ulong>> {Dictionary(r,root+0x570)};
            sources.AddRange(r.Pointers(root+0x588).Select(n=>Node(r,n)));
            var copies=new List<ulong>();var sourceEntries=new List<Dictionary<string,ulong>>();
            foreach(var source in sources)
            {
                ulong list;if(!source.TryGetValue("lrxf",out list))continue;
                var entries=r.Pointers(r.Ptr(list+8));if(index>=entries.Length)continue;
                var own=Node(r,entries[index]);int id;
                if(TryIdentity(r,own,out id) && id!=competition.Uid)continue;
                copies.AddRange(RuleValues(r,own,ruleCode));
                sourceEntries.AddRange(entries.Select(n=>Node(r,n)));
            }
            s.Addresses=new[]{primaryValue}.Concat(copies).ToArray();
            if(s.Addresses.Length!=3 || s.Addresses.Distinct().Count()!=3 || s.Addresses.Any(a=>r.Int(a)!=s.Limit))
                throw new InvalidDataException("报名规则的运行值与两份保存数据不一致，当前保持只读。");
            foreach(var entry in sourceEntries)
            {
                int id;if(TryIdentity(r,entry,out id) && id!=competition.Uid && RuleValues(r,entry,ruleCode).Any(s.Addresses.Contains))s.SharedIds.Add(id);
            }
            if(peers!=null)
                foreach(var peer in peers.Where(p=>p.Uid!=competition.Uid))
                {
                    try
                    {
                        // Only actual sharing adds guards; unrelated competitions remain independent.
                        var probe=new EligibilityReader(read);ulong ph=probe.Ptr(peer.Address+0xB0);
                        if(probe.Ptr(ph)!=moduleBase+EligibilityLayout.RulesHolderVt || probe.Ptr(ph+0x370)!=common || probe.Byte(ph+0x31)!=index)continue;
                        if(probe.Ptr(peer.Address)!=moduleBase+EligibilityLayout.CompetitionVt || probe.Int(peer.Address+12)!=peer.Uid)throw new InvalidDataException("共享赛事身份已变化。");
                        s.SharedIds.Add(peer.Uid);foreach(var item in probe.Reads)r.Reads[item.Key]=item.Value;
                    }
                    catch{ /* A peer with no readable rules does not establish sharing; the complete source copies are still checked above. */ }
                }
            if(!r.Matches(read))throw new InvalidDataException("赛事规则正在更新，请暂停后重试。");
            return s;
        }
        public List<EligibilityEdit> Plan(Func<ulong,int,byte[]> read,int limit)
        {
            if(limit<MinimumValue || limit>1000)throw new ArgumentOutOfRangeException("limit",RuleCode==0x09?"总报名人数应在 1–1000 之间，不使用含义未核实的 0 或负数。":"人数应在 0–1000 之间。最低人数设为 0 表示不再要求；上限设为 0 表示零名额。");
            if(!Reader.Matches(read))throw new InvalidOperationException("报名规则预览已失效，请重新打开。");
            return limit==Limit?new List<EligibilityEdit>():Addresses.Select(a=>new EligibilityEdit {Address=a,Before=BitConverter.GetBytes(Limit),After=BitConverter.GetBytes(limit),Label=Competition.Name+"："+RuleLabel+"（"+DirectionLabel+"） "+Limit+" → "+limit}).ToList();
        }
    }
}
