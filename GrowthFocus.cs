// Modified build v1.1.27. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Fm24Editor
{
    sealed class GrowthProfile
    {
        public readonly string Name;
        public readonly string Group, Phase, English;
        public readonly int[] Core, Supporting;
        int[] compositeCosts,compositePriorities;
        public int Scale {get{return compositeCosts==null?1:100;}}
        internal GrowthProfile(string name, int[] core, int[] supporting, string group = null, string phase = "基本", string english = "")
        { Name = name; Core = core; Supporting = supporting; Group = group ?? (name=="前锋"?"中锋":name); Phase=phase; English=english; }
        public string DisplayName {get{return Phase=="基本"?Name+"（基本方案）":Name+"（"+Phase+"）";}}

        static readonly GrowthProfile[] Basic = {
            new GrowthProfile("门将", new[]{21,19,11,46,20,17,53,18,12,13}, new[]{14,15,16,7,22,52,39,36,34,37,42}),
            new GrowthProfile("中后卫", new[]{5,9,20,17,53,39,3,36}, new[]{18,52,43,34,38,7,22,46,42,28}),
            new GrowthProfile("边后卫", new[]{9,5,20,34,38,37,29,0}, new[]{17,53,18,7,22,1,6,28,42,46}),
            new GrowthProfile("翼卫", new[]{34,38,37,29,0,1,6}, new[]{9,20,17,18,7,22,28,42,46}),
            new GrowthProfile("后腰", new[]{9,20,17,18,7,53,28}, new[]{5,36,37,29,22,23,52,10,42,46}),
            new GrowthProfile("中场", new[]{7,10,18,22,23,28,29}, new[]{17,20,9,37,42,52,53,6,4,34}),
            new GrowthProfile("前腰", new[]{7,10,22,23,18,6,52}, new[]{1,17,26,4,34,38,42,46,28,37,2}),
            new GrowthProfile("边锋", new[]{1,34,38,0,6,22,23}, new[]{7,10,18,52,17,2,29,37,42,46}),
            new GrowthProfile("前锋", new[]{2,6,52,17,22,34,38}, new[]{18,23,1,3,39,36,46,53,42,7,29})
        };
        public static readonly GrowthProfile[] All = Basic.Concat(GrowthRoleCatalog.Create()).ToArray();
        public static readonly string[] Groups = {"门将","中后卫","边后卫","翼卫","后腰","中场","前腰","边前卫","边锋","中锋"};
        static readonly int[] PositionProfile = {0,1,2,1,2,4,9,5,9,7,6,7,8,3,3};
        static readonly int[] Excluded = {24,25,31,41,44,47,48,49};
        static readonly int[] Goalkeeping = {11,12,13,14,15,16,19,21,32,33};
        // Penalties, corners, long throws, free kicks.
        public static bool IsSetPiece(int index) { return index==8 || index==27 || index==30 || index==35; }
        public IEnumerable<int> AutomaticTargets { get { return Core.Concat(Supporting).Where(i=>!IsSetPiece(i)).Distinct(); } }
        public static bool Developable(int attribute) { return attribute >= 0 && attribute < 54 && !Excluded.Contains(attribute); }
        public int Cost(int attribute)
        {
            if(compositeCosts!=null)return attribute>=0 && attribute<54?compositeCosts[attribute]:0;
            if (!Developable(attribute) || (Group != "门将" && Goalkeeping.Contains(attribute))) return 0;
            return Core.Contains(attribute) ? 3 : Supporting.Contains(attribute) ? 2 : 1;
        }
        public int Priority(int attribute) { return compositePriorities!=null?(attribute>=0 && attribute<54?compositePriorities[attribute]:0):Core.Contains(attribute) ? 6 : Supporting.Contains(attribute) ? 2 : 0; }
        public static GrowthProfile Combine(GrowthProfile possession,GrowthProfile defending,int possessionWeight)
        {
            if(possessionWeight<0 || possessionWeight>100)throw new ArgumentOutOfRangeException("possessionWeight");
            if(defending==null || possessionWeight==100)return possession;
            if(possessionWeight==0)return defending;
            if(object.ReferenceEquals(possession,defending))return possession;
            if((possession.Group=="门将")!=(defending.Group=="门将"))throw new InvalidDataException("有球和无球方案不能混用门将与非门将位置。");
            var core=possession.Core.Concat(defending.Core).Distinct().ToArray();
            var supporting=possession.Supporting.Concat(defending.Supporting).Distinct().Except(core).ToArray();
            var combined=new GrowthProfile("有球："+possession.Name+" / 无球："+defending.Name,core,supporting,possession.Group,"双阶段");
            combined.compositeCosts=Enumerable.Range(0,54).Select(i=>possession.Cost(i)*possessionWeight+defending.Cost(i)*(100-possessionWeight)).ToArray();
            combined.compositePriorities=Enumerable.Range(0,54).Select(i=>possession.Priority(i)*possessionWeight+defending.Priority(i)*(100-possessionWeight)).ToArray();
            return combined;
        }
        // selected == -1 means the highest familiarity, with a stable first-position tie break.
        public static GrowthProfile Resolve(byte[] positions, int selected)
        {
            if (positions == null || positions.Length != 15 || positions.Any(v => v < 1 || v > 20))
                throw new InvalidDataException("球员位置数据未通过检查。");
            if (selected >= 0 && selected < All.Length) return All[selected];
            if (selected != -1) throw new ArgumentOutOfRangeException("selected");
            byte best = positions.Max();
            if (best < 12) throw new InvalidDataException("主要位置不明确，请先指定培养方向。");
            return All[PositionProfile[Array.IndexOf(positions, best)]];
        }
    }

    sealed class GrowthAllocation
    {
        public byte[] Before, After;
        public int RawAdded, ChangedAttributes;
        public long ScoreBefore, ScoreAfter, Limit;
        public long SelectedLimit, SelectedScoreAfter;
        public int SupportRawAdded;
        public readonly int[] SupportAdded = new int[54];
        public long SupportLimit, SupportSpent;
    }

    // This is a bounded development proxy, not FM's unpublished RCA formula. Its scale is
    // anchored to the player's initial attributes and CA. Natural changes consume the same
    // budget, so every tick cannot grant the whole CA increase again.
    sealed class GrowthFocusState
    {
        public readonly GrowthProfile Profile;
        public readonly int StartCa;
        readonly byte[] positions, baseline;
        readonly HashSet<int> selected;
        readonly int[] limitedAttributes;
        readonly int[] costs = new int[54], priorities = new int[54];
        readonly long baselineScore;
        readonly long selectedBaselineScore;
        public readonly GrowthSupportItem[] SupportItems;
        readonly int supportPercent;
        public GrowthFocusState(byte[] positionValues, byte[] attributes, int ca, int selectedProfile,
            IEnumerable<int> selectedAttributes=null,GrowthSupportOptions support=null,int? age=null,
            int defendingProfile=-2,int possessionWeight=50,bool limitUnselected=false)
        {
            ValidateAttributes(attributes);
            if (ca < 1 || ca > 200) throw new InvalidDataException("CA 必须在 1–200 范围内。");
            if(defendingProfile<-2)throw new ArgumentOutOfRangeException("defendingProfile");
            Profile = GrowthProfile.Combine(GrowthProfile.Resolve(positionValues, selectedProfile),defendingProfile==-2?null:GrowthProfile.Resolve(positionValues,defendingProfile),possessionWeight);
            if (selectedAttributes != null)
            {
                selected = new HashSet<int>(selectedAttributes);
                if (selected.Count == 0) throw new InvalidDataException("请至少勾选一项需要加速的属性。");
                if (selected.Any(i => !GrowthProfile.Developable(i) || Profile.Cost(i) <= 0))
                    throw new InvalidDataException("所选属性不适用于当前培养方向，惯用脚与隐藏属性不参与成长。");
            }
            StartCa = ca; positions = (byte[])positionValues.Clone(); baseline = (byte[])attributes.Clone();
            limitUnselected=limitUnselected && selected!=null;
            limitedAttributes=limitUnselected?Enumerable.Range(0,baseline.Length)
                .Where(i=>GrowthProfile.Developable(i) && Profile.Cost(i)>0 && !selected.Contains(i)).ToArray():new int[0];
            for(int i=0;i<54;i++)
            {
                costs[i]=Profile.Cost(i);
                priorities[i]=selected==null?(GrowthProfile.IsSetPiece(i)?0:Profile.Priority(i)):selected.Contains(i)?6:0;
            }
            baselineScore = Score(baseline);
            selectedBaselineScore = selected==null?baselineScore:SelectedScore(baseline);
            if (baselineScore <= 0) throw new InvalidDataException("无法建立属性成长预算。");
            var supportOptions=(support??new GrowthSupportOptions()).Clone();supportOptions.Validate();
            supportPercent=supportOptions.BudgetPercent;
            SupportItems=supportOptions.Enabled?GrowthSupportPlanner.Suggest(Profile,baseline,ca,age,supportOptions,
                limitUnselected?selected:null).Where(i=>i.Included).ToArray():new GrowthSupportItem[0];
        }
        public static void ValidateAttributes(byte[] values)
        {
            if (values == null || values.Length != 54 || values.Any(v => v < 1 || v > 100))
                throw new InvalidDataException("球员属性数据未通过检查。");
        }
        public bool SamePositions(byte[] current) { return current != null && positions.SequenceEqual(current); }
        internal bool HasLimits { get { return limitedAttributes.Length>0; } }
        internal IEnumerable<int> LimitedAttributes { get { return limitedAttributes; } }
        internal byte LimitFor(int index) { return baseline[index]; }
        internal bool ExceedsLimits(byte[] values)
        {
            if(values==null || values.Length!=baseline.Length)return false;
            foreach(int index in limitedAttributes)
                if(values[index]>baseline[index])return true;
            return false;
        }
        internal byte[] WithLimits(byte[] values)
        {
            if(!ExceedsLimits(values))return values;
            var capped=(byte[])values.Clone();
            foreach(int index in limitedAttributes)
                capped[index]=Math.Min(capped[index],baseline[index]);
            return capped;
        }
        public bool CustomSelection { get { return selected != null; } }
        public int[] TargetAttributes { get { return selected==null?Profile.AutomaticTargets.ToArray():selected.OrderBy(i=>i).ToArray(); } }
        public int[] AllTargetAttributes { get { return TargetAttributes.Concat(SupportItems.Select(i=>i.Index)).Distinct().ToArray(); } }
        public int Priority(int attribute) { return attribute>=0 && attribute<54?priorities[attribute]:0; }
        public long Score(byte[] values)
        { long result=0;for(int i=0;i<54;i++)result+=(long)values[i]*costs[i];return result; }
        long SelectedScore(byte[] values)
        { long result=0;for(int i=0;i<54;i++)if(priorities[i]>0)result+=(long)values[i]*costs[i];return result; }
        public GrowthAllocation Allocate(byte[] current, int targetCa, int cap)
        {
            ValidateAttributes(current);
            if (cap < 1 || cap > 200 || targetCa < StartCa || targetCa > cap)
                throw new InvalidDataException("属性成长目标必须在起始 CA 与 PA 上限之间。");
            var result = new GrowthAllocation { Before = (byte[])current.Clone(), After = (byte[])current.Clone(),
                Limit = baselineScore * targetCa / StartCa, ScoreBefore = Score(current) };
            long remaining = result.Limit - result.ScoreBefore;
            result.SelectedLimit=selectedBaselineScore*targetCa/StartCa;
            result.SupportLimit=(result.Limit-baselineScore)*supportPercent/100;
            result.SupportSpent=SupportItems.Sum(item=>(long)Math.Max(0,current[item.Index]-baseline[item.Index])*costs[item.Index]);
            long supportRoom=Math.Min(remaining,result.SupportLimit-result.SupportSpent);
            while(supportRoom>0)
            {
                int index=-1,gap=0;
                foreach(var item in SupportItems)
                {
                    int difference=item.Target*5-result.After[item.Index];
                    if(difference<=0 || costs[item.Index]>supportRoom)continue;
                    if(difference>gap || difference==gap && item.Index<index)
                    { index=item.Index;gap=difference; }
                }
                if(index<0)break;
                result.After[index]++;result.SupportAdded[index]++;result.SupportRawAdded++;result.RawAdded++;
                supportRoom-=costs[index];remaining-=costs[index];result.SupportSpent+=costs[index];
            }
            if(selected!=null)remaining=Math.Min(remaining,result.SelectedLimit-SelectedScore(result.After));
            while (remaining > 0)
            {
                int best = -1, bestScore = -1;
                for (int i = 0; i < 54; i++)
                {
                    int cost = costs[i], priority = priorities[i];
                    if (priority == 0 || cost <= 0 || cost > remaining || result.After[i] >= 100) continue;
                    int score = priority * (101 - result.After[i]);
                    if (score > bestScore) { best = i; bestScore = score; }
                }
                if (best < 0) break;
                result.After[best]++; remaining -= costs[best]; result.RawAdded++;
            }
            result.ScoreAfter = Score(result.After);
            result.SelectedScoreAfter = SelectedScore(result.After);
            result.ChangedAttributes = Enumerable.Range(0, 54).Count(i => result.Before[i] != result.After[i]);
            return result;
        }
    }

    static class GrowthUndo
    {
        public static bool Apply(Func<ulong,int,byte[]> read, Func<ulong,byte[],bool> write, IList<Change> group, out string error)
        {
            error = null;
            if (group == null || group.Count == 0 || group.Any(c => !c.Growth || c.Undone || c.Group <= 0) || group.Select(c => c.Group).Distinct().Count() != 1)
            { error = "成长撤销组不完整。"; return false; }
            var edits = group.Reverse().Select(c => new EligibilityEdit { Address = c.Addr, Before = c.New, After = c.Old, Label = c.What }).ToList();
            var guards=group.Select(c=>c.Guard).Where(g=>g!=null).Distinct().ToArray();
            Func<bool> identity = () => guards.All(check=>check());
            if (!EligibilityTransaction.Apply(read, write, edits, identity, out error)) return false;
            foreach (var change in group) change.Undone = true;
            return true;
        }
    }

    // Captures the full before-state and keeps its expected payload for transaction checks.
    sealed class GrowthStep
    {
        readonly List<EligibilityEdit> edits = new List<EligibilityEdit>();
        public readonly IList<EligibilityEdit> Edits;
        public GrowthAllocation Allocation;
        public byte[] Positions, Attributes;
        public int Ca, Cap, PlayerUid;
        public short RawPa;
        public uint Day;
        public string PlayerName;
        readonly List<KeyValuePair<ulong, byte[]>> identity = new List<KeyValuePair<ulong, byte[]>>();
        ulong blockAddress, abilityAddress;
        byte[] block, ability;
        byte[] expectedBlock, expectedAbility;

        public GrowthStep() { Edits=edits.AsReadOnly(); }

        static byte[] Read(Func<ulong,int,byte[]> read, ulong address, int count)
        {
            if (address < 0x10000 || address > 0x7FFFFFFFFFFFUL || count < 1 || address + (ulong)count > 0x800000000000UL)
                throw new InvalidDataException("成长数据地址异常，请重新扫描。");
            byte[] value = read(address, count);
            if (value == null || value.Length != count) throw new InvalidDataException("成长数据未能完整读取。");
            return value;
        }
        static byte[] CaptureBytes(Func<ulong,int,byte[]> read,ulong address,int count)
        { return (byte[])Read(read,address,count).Clone(); }
        public static uint DayOnly(uint value)
        {
            int year = (int)(value >> 16), day = (int)(value & 0x1FF);
            if (year < 1900 || year > 2200 || day < 1 || day > (DateTime.IsLeapYear(year) ? 366 : 365))
                throw new InvalidDataException("游戏日期无效，成长已停止。");
            return value & 0xFFFF01FFu;
        }
        static int PaCap(short raw)
        {
            if (raw == 0 || raw > 200 || raw < -100) throw new InvalidDataException("PA 超出已支持范围。");
            return raw > 0 ? raw : Math.Min(200, -2 * raw);
        }
        public static GrowthStep Capture(Func<ulong,int,byte[]> read, Func<uint?> readDay, Player player,
                                         IList<KeyValuePair<ulong,byte[]>> guards, bool individualPlayer=false)
        {
            if (player == null || player.Uid <= 0 || player.Tail < 0x10000 + FmLayout.AbilityBeforePerson ||
                FmLayout.PosCount != 15 || FmLayout.AttrCount != 54 || FmLayout.AttrStart != 15)
                throw new InvalidDataException("此球员属性布局未通过成长检查。");
            if (guards == null || guards.Count < 2) throw new InvalidDataException("缺少球员身份检查。");
            uint? date = readDay();
            if (!date.HasValue) throw new InvalidDataException("游戏日期无法读取。");
            var result = new GrowthStep { blockAddress = player.Block, abilityAddress = player.Tail - FmLayout.AbilityBeforePerson,
                Day = DayOnly(date.Value), PlayerName = player.DisplayName, PlayerUid = player.Uid };
            foreach (var guard in guards)
            {
                if (guard.Value == null || !Read(read, guard.Key, guard.Value.Length).SequenceEqual(guard.Value))
                    throw new InvalidDataException(guard.Key==player.Person?"球员类型已变化，请重新读取当前球员。":"球员 UID 已变化，请重新定位当前球员。");
                result.identity.Add(new KeyValuePair<ulong,byte[]>(guard.Key, (byte[])guard.Value.Clone()));
            }
            ulong teamAddress = player.Tail - FmLayout.TeamBeforePerson;
            byte[] teamBytes = CaptureBytes(read, teamAddress, 8);
            ulong team = BitConverter.ToUInt64(teamBytes, 0);
            result.identity.Add(new KeyValuePair<ulong,byte[]>(teamAddress, teamBytes));
            if(team!=0)
            {
                byte[] club = CaptureBytes(read, team + FmLayout.TeamClub, 8);
                if (BitConverter.ToUInt64(club,0)!=player.Club || (!individualPlayer && player.Club==0))
                    throw new InvalidDataException("球员已离队或所属球队发生变化。");
                result.identity.Add(new KeyValuePair<ulong,byte[]>(team + FmLayout.TeamClub,club));
            }
            else if(!individualPlayer || player.Club!=0) throw new InvalidDataException("球员所属球队已变化。");
            if(player.Roster!=null && player.Roster.Contract!=0 && player.Roster.ContractClub!=0)
            {
                byte[] contractBytes=CaptureBytes(read,player.Person+FmLayout.ContractAfterPerson,8);
                ulong contract=BitConverter.ToUInt64(contractBytes,0);
                if(contract!=player.Roster.Contract || BitConverter.ToUInt64(Read(read,contract+8,8),0)!=player.Person)
                    throw new InvalidDataException("合同归属已变化，请重新读取分队资料。");
                byte[] ownerTeamBytes=CaptureBytes(read,contract+FmLayout.ContractTeam,8);ulong ownerTeam=BitConverter.ToUInt64(ownerTeamBytes,0);
                byte[] ownerClub=CaptureBytes(read,ownerTeam+FmLayout.TeamClub,8);
                if(BitConverter.ToUInt64(ownerClub,0)!=player.Roster.ContractClub)throw new InvalidDataException("球员合同所属俱乐部已变化。");
                result.identity.Add(new KeyValuePair<ulong,byte[]>(player.Person+FmLayout.ContractAfterPerson,contractBytes));
                result.identity.Add(new KeyValuePair<ulong,byte[]>(contract+8,BitConverter.GetBytes(player.Person)));
                result.identity.Add(new KeyValuePair<ulong,byte[]>(contract+FmLayout.ContractTeam,ownerTeamBytes));
                result.identity.Add(new KeyValuePair<ulong,byte[]>(ownerTeam+FmLayout.TeamClub,ownerClub));
            }
            result.block = CaptureBytes(read, result.blockAddress, 69);
            result.ability = CaptureBytes(read, result.abilityAddress, 4);
            result.Positions = result.block.Take(15).ToArray();
            result.Attributes = result.block.Skip(15).ToArray();
            GrowthFocusState.ValidateAttributes(result.Attributes);
            if (result.Positions.Any(v => v < 1 || v > 20)) throw new InvalidDataException("球员位置数据异常。");
            result.Ca = BitConverter.ToInt16(result.ability, 0); result.RawPa = BitConverter.ToInt16(result.ability, 2);
            result.Cap = PaCap(result.RawPa);
            if (result.Ca < 1 || result.Ca > 200) throw new InvalidDataException("CA 超出范围。");
            if (!result.Matches(read, readDay)) throw new InvalidDataException("游戏正在更新球员资料，请暂停后重试。");
            return result;
        }
        public void Plan(GrowthFocusState focus,int targetCa,bool enforceLimits=false)
        {
            if (Edits.Count != 0) throw new InvalidOperationException("成长计划不能重复创建。");
            if (targetCa <= Ca || targetCa > Cap || targetCa > 200)
                throw new InvalidDataException("本次 CA 必须提高且不超过当前 PA。");
            if (!focus.SamePositions(Positions)) throw new InvalidDataException("位置数据已变化，请停止后重新开始培养。");
            var input=enforceLimits?focus.WithLimits(Attributes):Attributes;
            Allocation=focus.Allocate(input,targetCa,Cap);
            if(Enumerable.Range(0,Attributes.Length).Any(i=>Allocation.After[i]<input[i]))
                throw new InvalidDataException("成长方案出现低于当前值的属性，已停止应用。");
            AddCaEdit(targetCa,PlayerName+"：CA 成长 "+Ca+" → "+targetCa);
            for (int i = 0; i < 54; i++)
            {
                if (Allocation.After[i] != Attributes[i])
                {
                    string label=Allocation.SupportAdded[i]>0?"低值补强／成长":focus.CustomSelection?"自选属性成长":"角色属性成长";
                    AddAttributeEdit(i,Allocation.After[i],Allocation.After[i]<Attributes[i]?
                        "未勾选属性上限":label+"（"+focus.Profile.Name+"）");
                }
            }
        }
        public void PlanAttributeLimits(GrowthFocusState focus)
        {
            if(Edits.Count!=0)throw new InvalidOperationException("成长计划不能重复创建。");
            if(Ca>=Cap || !focus.HasLimits)return;
            if(!focus.SamePositions(Positions))throw new InvalidDataException("位置数据已变化，请停止后重新开始培养。");
            AddReviewedAttributes(focus.WithLimits(Attributes),"未勾选属性上限");
        }
        public void PlanOptimization(AttributeOptimizationResult result)
        {
            if(Edits.Count!=0)throw new InvalidOperationException("计划不能重复应用。");AttributeOptimizer.Validate(result);
            if(result.SourceCa!=Ca || result.RawPa!=RawPa || result.Cap!=Cap ||
               !result.Before.SequenceEqual(Attributes) || !result.Positions.SequenceEqual(Positions))
                throw new InvalidDataException("属性优化与读取的球员资料不一致。");
            string label="一键属性优化（"+(result.Mode==AttributeOptimizationMode.Preserve?"保留特点":"重新分配")+"）";
            AddReviewedCa(result.TargetCa,label);
            AddReviewedAttributes(result.After,label);
        }
        public void PlanRestore(AttributeRestoreSource source,bool restoreCa,bool extraFields,int uid)
        {
            if(Edits.Count!=0 || source==null || source.Uid!=uid || PlayerUid!=uid ||
               (!source.FromDatabase && (!source.GameDate.HasValue || source.GameDate.Value>Birthdays.Decode(Day))) ||
               (restoreCa && !source.CanRestoreCa(Cap)))
                throw new InvalidDataException("恢复来源、球员身份或可恢复的 CA 范围已变化。");
            var after=RestoredAttributes(source,extraFields);
            string label=source.FromDatabase?"恢复数据库默认值":"早期存档恢复";
            if(restoreCa) AddReviewedCa(source.Ca.Value,label);
            AddReviewedAttributes(after,label);
        }
        public byte[] RestoredAttributes(AttributeRestoreSource source,bool extraFields)
        {
            if(source.FromDatabase)
            {
                if(source.Attributes==null || source.Attributes.Length!=54 ||
                   source.Attributes.Any(v=>v>100 || v%5!=0))
                    throw new InvalidDataException("原版数据库的属性范围未通过检查。");
            }
            else GrowthFocusState.ValidateAttributes(source.Attributes);
            var after=(byte[])source.Attributes.Clone();
            for(int i=0;i<after.Length;i++) if(after[i]==0) after[i]=Attributes[i];
            if(!extraFields) foreach(int i in new[]{24,25,41,44,47,48,49}) after[i]=Attributes[i];
            return after;
        }
        void AddReviewedCa(int target,string label)
        {
            if(target!=Ca)AddCaEdit(target,PlayerName+"："+label+" CA "+Ca+" → "+target);
        }
        void AddCaEdit(int target,string description)
        {
            var after=BitConverter.GetBytes((short)target);
            expectedAbility=(byte[])ability.Clone();
            Array.Copy(after,expectedAbility,after.Length);
            edits.Add(new EligibilityEdit { Address=abilityAddress, Before=new[]{ability[0],ability[1]},
                After=after, Label=description });
        }
        void AddReviewedAttributes(byte[] after,string label)
        {
            for(int i=0;i<54;i++)
                if(after[i]!=Attributes[i])AddAttributeEdit(i,after[i],label);
        }
        void AddAttributeEdit(int index,byte after,string label)
        {
            if(expectedBlock==null)expectedBlock=(byte[])block.Clone();
            int offset=15+index;
            expectedBlock[offset]=after;
            edits.Add(new EligibilityEdit{Address=blockAddress+(ulong)offset,Before=new[]{Attributes[index]},
                After=new[]{after},Label=PlayerName+"："+label+" "+Zh.Attr(FmLayout.Attrs[index])+
                    " "+(Attributes[index]/5.0).ToString("0.0")+" → "+(after/5.0).ToString("0.0")});
        }
        bool GuardsMatch(Func<ulong,int,byte[]> read)
        {
            foreach(var entry in identity)
                if(!Read(read,entry.Key,entry.Value.Length).SequenceEqual(entry.Value))return false;
            return true;
        }
        public bool IdentityMatches(Func<ulong,int,byte[]> read)
        {
            try
            {
                if(!GuardsMatch(read))return false;
                byte[] current = Read(read, blockAddress, 15);
                for(int i=0;i<current.Length;i++)if(current[i]!=block[i])return false;
                current=Read(read,abilityAddress+2,2);
                return current[0]==ability[2] && current[1]==ability[3];
            }
            catch { return false; }
        }
        static bool PayloadMatches(Func<ulong,int,byte[]> read,ulong address,byte[] before,byte[] after)
        {
            byte[] current = Read(read, address, before.Length);
            for (int i = 0; i < before.Length; i++)
                if(current[i]!=before[i] && (after==null || current[i]!=after[i]))return false;
            return true;
        }
        public bool Matches(Func<ulong,int,byte[]> read, Func<uint?> readDay)
        {
            try
            {
                uint? date = readDay();
                return date.HasValue && DayOnly(date.Value)==Day && GuardsMatch(read) &&
                    PayloadMatches(read,blockAddress,block,expectedBlock) &&
                    PayloadMatches(read,abilityAddress,ability,expectedAbility);
            }
            catch { return false; }
        }
    }
}
