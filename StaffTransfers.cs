// Modified build v1.1.28, 2026-11-21. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    enum StaffPost { Medical, Coaching, Recruitment, Manager, FootballDirector }

    sealed class StaffRole
    {
        public readonly byte Code;
        public readonly string Name;
        public readonly StaffPost Post;

        StaffRole(byte code, string name, StaffPost post) { Code = code; Name = name; Post = post; }

        // Job codes and club/team posts were cross-checked against the FM27 staff reference.
        public static readonly StaffRole[] All = {
            new StaffRole(16, "Head Coach", StaffPost.Manager),
            new StaffRole(20, "Assistant Coach", StaffPost.Coaching),
            new StaffRole(2, "Coach", StaffPost.Coaching),
            new StaffRole(34, "Goalkeeping Coach", StaffPost.Coaching),
            new StaffRole(26, "Fitness Coach", StaffPost.Coaching),
            new StaffRole(22, "Set Piece Coach", StaffPost.Coaching),
            new StaffRole(64, "Head of Youth Development", StaffPost.Coaching),
            new StaffRole(12, "Physio", StaffPost.Medical),
            new StaffRole(50, "Head Physio", StaffPost.Medical),
            new StaffRole(46, "Doctor", StaffPost.Medical),
            new StaffRole(38, "Chief Doctor", StaffPost.Medical),
            new StaffRole(48, "Sports Scientist", StaffPost.Medical),
            new StaffRole(40, "Head of Sports Science", StaffPost.Medical),
            new StaffRole(60, "Performance Analyst", StaffPost.Coaching),
            new StaffRole(62, "Head Performance Analyst", StaffPost.Coaching),
            new StaffRole(10, "Director of Football", StaffPost.FootballDirector),
            new StaffRole(14, "Scout", StaffPost.Recruitment),
            new StaffRole(44, "Chief Scout", StaffPost.Recruitment),
            new StaffRole(58, "Recruitment Analyst", StaffPost.Recruitment),
            new StaffRole(86, "Loan Manager", StaffPost.Recruitment),
            new StaffRole(88, "Technical Director", StaffPost.Recruitment)
        };
        static readonly Dictionary<int, StaffRole> ByCode = All.ToDictionary(role => (int)role.Code);
        static readonly Dictionary<int, string> OtherNames = new Dictionary<int, string> {
            { 1, "Player" }, { 4, "Chairperson" }, { 6, "Director" }, { 66, "Owner" }
        };

        public static StaffRole Find(int code)
        {
            StaffRole role;
            return ByCode.TryGetValue(code, out role) ? role : null;
        }

        public static string Title(int code)
        {
            var role = Find(code);
            if (role == null && code > 1 && code % 2 == 1)
            {
                var secondJob = Find(code - 1);
                if (secondJob != null) return "球员兼" + secondJob;
            }
            string name;
            return role != null ? role.ToString() : OtherNames.TryGetValue(code, out name) ? Zh.Z(name) : "代码 " + code;
        }

        public override string ToString() { return Zh.Z(Name); }
    }

    sealed class StaffEmployment
    {
        static readonly byte[] BasicName = Encoding.ASCII.GetBytes(".?AVBASIC_CONTRACT@db@@\0");

        public ulong Address, Type, Team, SeniorTeam;
        public byte Job;
        public int WeeklyWage;
        public bool Full;

        public bool VerifiesClubAssignment(Func<ulong, ulong> pointer, ulong module)
        {
            if (StaffRole.Find(Job) == null || !StaffTransferPlanner.IsPointer(Team) ||
                !StaffTransferPlanner.IsPointer(SeniorTeam)) return false;
            ulong teamType = module + FmProcess.TeamVtRva;
            if (pointer(Team) != teamType || pointer(SeniorTeam) != teamType) return false;
            ulong club = pointer(Team + FmLayout.TeamClub2);
            return StaffTransferPlanner.IsPointer(club) && pointer(club) == module + FmProcess.ClubVtRva &&
                pointer(SeniorTeam + FmLayout.TeamClub2) == club;
        }

        public static StaffEmployment Read(Func<ulong, int, byte[]> read, ulong module, ulong imageSize, ulong owner)
        {
            if (!StaffTransferPlanner.IsPointer(owner)) return null;
            var pointer = read(owner + FmLayout.StaffContract, 8);
            if (pointer == null || pointer.Length != 8) return null;
            ulong address = BitConverter.ToUInt64(pointer, 0);
            if (!StaffTransferPlanner.IsPointer(address)) return null;

            int length = (int)Math.Max(FmLayout.ContractJob + 1, Math.Max(FmLayout.ContractWage + 4,
                Math.Max(FmLayout.ContractTeam + 8, FmLayout.ContractTeam2 + 8)));
            var head = read(address, length);
            if (head == null || head.Length != length || BitConverter.ToUInt64(head, 8) != owner) return null;
            ulong type = BitConverter.ToUInt64(head, 0);
            bool full = FmProcess.ContractVtRva > 1 && type == module + FmProcess.ContractVtRva;
            if (!full && (!StaffTransferPlanner.LayoutSupported || !IsBasic(read, module, imageSize, type))) return null;

            return new StaffEmployment {
                Address = address, Type = type, Team = BitConverter.ToUInt64(head, (int)FmLayout.ContractTeam),
                SeniorTeam = BitConverter.ToUInt64(head, (int)FmLayout.ContractTeam2),
                Job = head[(int)FmLayout.ContractJob], WeeklyWage = BitConverter.ToInt32(head, (int)FmLayout.ContractWage),
                Full = full
            };
        }

        static bool InImage(ulong address, ulong module, ulong size, int length)
        {
            return address >= module && (ulong)length <= size && address - module <= size - (ulong)length;
        }

        // A basic FM27 contract has its own class and ends before any full-contract dates.
        static bool IsBasic(Func<ulong, int, byte[]> read, ulong module, ulong size, ulong table)
        {
            if (module < 0x10000 || table < module + 8 || !InImage(table, module, size, 8)) return false;
            var reference = read(table - 8, 8);
            if (reference == null || reference.Length != 8) return false;
            ulong locator = BitConverter.ToUInt64(reference, 0);
            if (!InImage(locator, module, size, 24)) return false;
            var data = read(locator, 24);
            if (data == null || data.Length != 24 || BitConverter.ToUInt32(data, 0) != 1 ||
                BitConverter.ToUInt32(data, 4) != 0 || BitConverter.ToUInt32(data, 20) != locator - module) return false;
            uint descriptor = BitConverter.ToUInt32(data, 12);
            ulong nameAt = module + descriptor + 16;
            if (descriptor == 0 || !InImage(nameAt, module, size, BasicName.Length)) return false;
            var name = read(nameAt, BasicName.Length);
            return name != null && name.SequenceEqual(BasicName);
        }
    }

    sealed class StaffTransferOrigin
    {
        public Staff Person;
        public StaffEmployment Employment;
        public ulong Club;
        public int ClubUid;

        public bool SameAs(StaffTransferOrigin other)
        {
            return other.Person == Person && other.Club == Club && other.ClubUid == ClubUid &&
                other.Employment.Address == Employment.Address && other.Employment.Type == Employment.Type &&
                other.Employment.Team == Employment.Team && other.Employment.SeniorTeam == Employment.SeniorTeam &&
                other.Employment.Job == Employment.Job;
        }
    }

    sealed class StaffTransferPlan
    {
        public StaffTransferOrigin Origin;
        public string SourceClubName;
        public Club Destination;
        public StaffRole Role;
        public EligibilityReader Snapshot;
        public List<EligibilityEdit> Edits = new List<EligibilityEdit>();

        public bool Apply(Func<ulong, int, byte[]> read, Func<ulong, byte[], bool> write, Func<bool> connected,
            bool undo, out string error)
        {
            var edits = undo ? Edits.AsEnumerable().Reverse().Select(edit => new EligibilityEdit {
                Address = edit.Address, Before = edit.After, After = edit.Before, Label = edit.Label
            }).ToList() : Edits;
            bool applied = EligibilityTransaction.Apply(read, write, edits,
                () => connected() && Snapshot.Matches(read, Edits), out error);
            if (error != null) error = error.Replace("注册资格", "职员转会").Replace("注册", "职员转会");
            return applied;
        }
    }

    sealed class StaffTransferPlanner
    {
        const ulong ClubUidOffset = 0x0C, ClubGenderOffset = 0xB9, ClubDirector = 0x148, TeamManager = 0x80;
        static readonly ulong[] DepartmentOffsets = { 0x60, 0x78, 0x90 };
        readonly Func<ulong, int, byte[]> read;
        readonly ulong module, imageSize;

        public StaffTransferPlanner(Func<ulong, int, byte[]> read, ulong module, ulong imageSize)
        {
            this.read = read; this.module = module; this.imageSize = imageSize;
        }

        public static bool LayoutSupported {
            get {
                return FmLayout.StaffContract == 0xA8 && FmLayout.ContractTeam == 0x10 &&
                    FmLayout.ContractTeam2 == 0x18 && FmLayout.ContractJob == 0x26 &&
                    FmLayout.ContractWage == 0x20 && FmLayout.StaffListBack == 0x100 &&
                    FmLayout.ClubTeams == 0x18 && FmLayout.TeamClub2 == 0x30 &&
                    FmLayout.ClubStaffLists.SequenceEqual(DepartmentOffsets);
            }
        }

        public static bool IsPointer(ulong address)
        {
            return address >= 0x10000 && address < 0x800000000000UL && address % 8 == 0;
        }

        static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }

        ulong ClubOfTeam(EligibilityReader data, ulong team)
        {
            Require(IsPointer(team) && data.Ptr(team) == module + FmProcess.TeamVtRva, "合同球队未通过身份检查，请重新扫描。");
            return data.Ptr(team + FmLayout.TeamClub2);
        }

        StaffTransferOrigin CaptureOrigin(Staff person, EligibilityReader data)
        {
            Require(LayoutSupported, "当前游戏构建的职员转会结构尚未核实。");
            Require(person != null && !person.Manager, "请选择一名由游戏控制的职员。");
            Require(person.Pers == person.Addr && person.Uid > 0 && IsPointer(person.Addr) &&
                data.Ptr(person.Addr) == module + FmProcess.StaffVtRva && data.Int(person.Addr + FmLayout.Uid) == person.Uid,
                "职员身份已变化，请重新挂接并扫描。");
            ulong link = data.Ptr(person.Addr + FmLayout.StaffContract);
            Require(link != 0, "该职员当前无俱乐部合同，职员转会需要已有合同。");
            var contract = StaffEmployment.Read(read, module, imageSize, person.Addr);
            Require(contract != null && contract.Address == link && data.Ptr(link) == contract.Type &&
                data.Ptr(link + 8) == person.Addr && data.Ptr(link + FmLayout.ContractTeam) == contract.Team &&
                data.Ptr(link + FmLayout.ContractTeam2) == contract.SeniorTeam && data.Byte(link + FmLayout.ContractJob) == contract.Job,
                "职员合同类型或归属未通过检查，请重新扫描。");
            Require(StaffRole.Find(contract.Job) != null, "当前职务「" + StaffRole.Title(contract.Job) + "」没有已核实的转会方式。");
            ulong club = ClubOfTeam(data, contract.Team);
            Require(IsPointer(club) && data.Ptr(club) == module + FmProcess.ClubVtRva &&
                ClubOfTeam(data, contract.SeniorTeam) == club, "合同所属俱乐部未通过检查。");
            int uid = data.Int(club + ClubUidOffset);
            Require(uid > 0, "合同所属俱乐部身份无效。");
            return new StaffTransferOrigin { Person = person, Employment = contract, Club = club, ClubUid = uid };
        }

        public StaffTransferOrigin ReadOrigin(Staff person)
        {
            var data = new EligibilityReader(read);
            var result = CaptureOrigin(person, data);
            Require(data.Matches(read), "游戏正在更新职员资料，请暂停后重试。");
            return result;
        }

        sealed class Roster
        {
            public ulong Header, Begin, End, Capacity;
            public ulong[] Entries;
            public byte[] Bytes;
        }

        static Roster CaptureRoster(EligibilityReader data, ulong header, int max, string label)
        {
            ulong begin = data.Ptr(header), end = data.Ptr(header + 8), capacity = data.Ptr(header + 16);
            bool empty = begin == 0 && end == 0 && capacity == 0;
            Require(empty || (IsPointer(begin) && IsPointer(end) && IsPointer(capacity) &&
                begin <= end && end <= capacity && capacity - begin <= (ulong)max * 8),
                label + "名单结构未通过检查，请重新扫描。");
            var bytes = data.Bytes(begin == 0 ? header : begin, (int)(end - begin));
            var entries = Enumerable.Range(0, bytes.Length / 8).Select(i => BitConverter.ToUInt64(bytes, i * 8)).ToArray();
            Require(entries.All(IsPointer) && entries.Distinct().Count() == entries.Length, label + "名单存在无效或重复记录。");
            return new Roster { Header = header, Begin = begin, End = end, Capacity = capacity, Entries = entries, Bytes = bytes };
        }

        static readonly string[] Departments = { "医疗部门", "教练部门", "球探与招募部门" };

        sealed class ClubStaff
        {
            public ulong Address;
            public byte Gender;
            public Roster Teams;
            public Roster[] Departments;
            public Dictionary<ulong, ulong> Posts = new Dictionary<ulong, ulong>();

            public ulong PostFor(StaffPost post, ulong team)
            {
                if (post == StaffPost.Manager) return team + TeamManager;
                return post == StaffPost.FootballDirector ? Address + ClubDirector : 0;
            }
        }

        ClubStaff CaptureClub(EligibilityReader data, ulong club, int uid, string label)
        {
            Require(IsPointer(club) && data.Ptr(club) == module + FmProcess.ClubVtRva &&
                uid > 0 && data.Int(club + ClubUidOffset) == uid, label + "身份已变化，请重新挂接并扫描。");
            var side = new ClubStaff { Address = club, Gender = data.Byte(club + ClubGenderOffset) };
            side.Teams = CaptureRoster(data, club + FmLayout.ClubTeams, 128, label + "的球队");
            Require(side.Teams.Entries.Length != 0, label + "没有读到所属球队。");
            foreach (ulong team in side.Teams.Entries)
            {
                Require(ClubOfTeam(data, team) == club, label + "的球队归属未通过检查。");
                side.Posts.Add(team + TeamManager, data.Ptr(team + TeamManager));
            }
            side.Posts.Add(club + ClubDirector, data.Ptr(club + ClubDirector));
            side.Departments = FmLayout.ClubStaffLists.Select((offset, i) =>
                CaptureRoster(data, club + offset, 512, label + "的" + Departments[i])).ToArray();
            return side;
        }

        static void AddEdit(StaffTransferPlan plan, ulong address, byte[] before, byte[] after, string label)
        {
            if (!before.SequenceEqual(after))
                plan.Edits.Add(new EligibilityEdit { Address = address, Before = before, After = after, Label = label });
        }

        static void SetPointer(StaffTransferPlan plan, ulong address, ulong before, ulong after, string label)
        {
            AddEdit(plan, address, BitConverter.GetBytes(before), BitConverter.GetBytes(after), label);
        }

        static void Remove(StaffTransferPlan plan, Roster roster, int index, string label)
        {
            SetPointer(plan, roster.Header + 8, roster.End, roster.End - 8, label + "人数");
            var after = (byte[])roster.Bytes.Clone();
            int at = index * 8;
            Buffer.BlockCopy(roster.Bytes, at + 8, after, at, after.Length - at - 8);
            AddEdit(plan, roster.Begin, roster.Bytes, after, label + "名单");
        }

        static void Append(StaffTransferPlan plan, EligibilityReader data, Roster roster, ulong person, string label)
        {
            Require(roster.Begin != 0, label + "当前尚无可写入的部门名单。");
            Require(roster.End < roster.Capacity, label + "当前没有可用空位。");
            AddEdit(plan, roster.End, data.Bytes(roster.End, 8), BitConverter.GetBytes(person), label + "名单");
            SetPointer(plan, roster.Header + 8, roster.End, roster.End + 8, label + "人数");
        }

        static bool Overlaps(ulong start, ulong end, ulong otherStart, ulong otherEnd)
        {
            return start < otherEnd && otherStart < end;
        }

        void VerifyDistinctBuffers(ClubStaff from, ClubStaff to, StaffTransferOrigin origin)
        {
            var rosters = from.Departments.Concat(new[] { from.Teams });
            if (from != to) rosters = rosters.Concat(to.Departments).Concat(new[] { to.Teams });
            var teams = from.Teams.Entries.Concat(to.Teams.Entries).Distinct().ToArray();
            ulong contract = origin.Employment.Address, person = origin.Person.Addr;
            ulong end = 0;
            foreach (var roster in rosters.Where(r => r.Capacity > r.Begin).OrderBy(r => r.Begin))
            {
                Require(roster.Begin >= end, "读到重叠的球队或职员名单，本次无法调整。");
                Require(!Overlaps(roster.Begin, roster.Capacity, module, module + imageSize) &&
                    !Overlaps(roster.Begin, roster.Capacity, contract, contract + (origin.Employment.Full ? 0xC8UL : 0x28UL)) &&
                    !Overlaps(roster.Begin, roster.Capacity, person - FmLayout.StaffListBack, person + FmLayout.StaffContract + 8) &&
                    !Overlaps(roster.Begin, roster.Capacity, from.Address, from.Address + 0x170) &&
                    !Overlaps(roster.Begin, roster.Capacity, to.Address, to.Address + 0x170) &&
                    teams.All(team => !Overlaps(roster.Begin, roster.Capacity, team, team + TeamManager + 8)),
                    "球队或职员名单指向其他记录，本次无法调整。");
                end = roster.Capacity;
            }
        }

        bool CorroboratesPost(StaffRole role, Club club)
        {
            if (!IsPointer(club.Addr) || !IsPointer(club.Team)) return false;
            var data = new EligibilityReader(read);
            try
            {
                ulong slot = role.Post == StaffPost.Manager ? club.Team + TeamManager : club.Addr + ClubDirector;
                ulong member = data.Ptr(slot);
                if (!IsPointer(member)) return false;
                ulong person = member + FmLayout.StaffListBack;
                if (data.Ptr(person) != module + FmProcess.StaffVtRva ||
                    data.Ptr(club.Addr) != module + FmProcess.ClubVtRva || data.Int(club.Addr + ClubUidOffset) != club.Uid) return false;
                var employment = StaffEmployment.Read(data.Bytes, module, imageSize, person);
                return employment != null && employment.Job == role.Code &&
                    ClubOfTeam(data, employment.Team) == club.Addr && ClubOfTeam(data, employment.SeniorTeam) == club.Addr &&
                    (role.Post != StaffPost.Manager || employment.Team == club.Team) && data.Matches(read);
            }
            catch (System.IO.InvalidDataException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        public StaffTransferPlan Plan(StaffTransferOrigin expected, Club target, StaffRole wanted, IEnumerable<Club> clubs)
        {
            Require(expected != null && target != null, "请选择目标俱乐部。");
            var data = new EligibilityReader(read);
            var origin = CaptureOrigin(expected.Person, data);
            Require(expected.SameAs(origin), "职员的合同、职务或所属俱乐部已变化，请重新打开。");
            var from = CaptureClub(data, origin.Club, origin.ClubUid, "原俱乐部");
            bool sameClub = target.Addr == origin.Club;
            Require(!sameClub || target.Uid == origin.ClubUid, "目标俱乐部身份已变化，请重新扫描。");
            var to = sameClub ? from : CaptureClub(data, target.Addr, target.Uid, "目标俱乐部");
            VerifyDistinctBuffers(from, to, origin);
            Require(from.Gender <= 1 && from.Gender == to.Gender, "两家俱乐部的男女足类别不同或未通过核对。");
            var contract = origin.Employment;
            Require(from.Teams.Entries.Contains(contract.Team) && from.Teams.Entries.Contains(contract.SeniorTeam),
                "原合同的球队不在俱乐部名单内。");
            var oldRole = StaffRole.Find(contract.Job);
            var role = wanted ?? oldRole;
            bool appointManager = oldRole != role && role.Post == StaffPost.Manager;
            ulong employer = sameClub && !appointManager ? contract.Team : to.Teams.Entries[0];
            ulong senior = sameClub && !appointManager ? contract.SeniorTeam : to.Teams.Entries[0];
            ulong person = origin.Person.Addr - FmLayout.StaffListBack;
            ulong oldPost = from.PostFor(oldRole.Post, contract.Team);
            ulong nextPost = to.PostFor(role.Post, employer);
            Require((oldPost == 0 || from.Posts[oldPost] == person) &&
                !from.Posts.Any(post => post.Key != oldPost && (post.Value == person || post.Value == origin.Person.Addr)),
                "原俱乐部的主教练或足球总监归属未通过核对。");
            if (!sameClub)
                Require(!to.Posts.Values.Contains(person) && !to.Posts.Values.Contains(origin.Person.Addr) &&
                    to.Departments.All(list => !list.Entries.Contains(person)),
                    "目标俱乐部已存在该职员的记录，请重新扫描。");
            Require(nextPost == 0 || nextPost == oldPost || to.Posts[nextPost] == 0,
                "目标" + role + "岗位已有职员，请选择其他职务或俱乐部。");
            // An existing holder identifies the dedicated slot when preserving a role. For a new
            // appointment, verify the field against another holder in the currently loaded game.
            if (nextPost != 0 && role != oldRole)
                Require(clubs.Where(club => club.Team != 0).OrderByDescending(club => club.HasFullFinance)
                    .Any(club => CorroboratesPost(role, club)),
                    "本次连接未读到可用于核对的" + role + "岗位，暂时无法任命。");

            var places = from.Departments.Select(list => Array.IndexOf(list.Entries, person)).ToArray();
            Require(oldPost != 0 || places.Any(place => place >= 0), "该职员不在原俱乐部已核实的职员名单中。");
            int department = (int)role.Post;
            bool keepLists = role == oldRole && (wanted == null || department >= Departments.Length || places[department] >= 0);
            var join = places.Select((place, i) => keepLists ? place >= 0 : i == department).ToArray();
            var plan = new StaffTransferPlan { Origin = origin, Destination = target, Role = role, Snapshot = data };
            for (int i = 0; i < places.Length; i++)
                if (places[i] >= 0 && (!sameClub || !join[i]))
                    Remove(plan, from.Departments[i], places[i], "原俱乐部" + Departments[i]);
            if (oldPost != 0 && oldPost != nextPost) SetPointer(plan, oldPost, person, 0, "原俱乐部" + oldRole + "岗位");
            SetPointer(plan, contract.Address + FmLayout.ContractTeam, contract.Team, employer, "合同所属球队");
            SetPointer(plan, contract.Address + FmLayout.ContractTeam2, contract.SeniorTeam, senior, "合同所属一线队");
            AddEdit(plan, contract.Address + FmLayout.ContractJob, new[] { contract.Job }, new[] { role.Code }, "合同职务");
            for (int i = 0; i < join.Length; i++)
                if (join[i] && (!sameClub || places[i] < 0))
                    Append(plan, data, to.Departments[i], person, "目标俱乐部" + Departments[i]);
            if (nextPost != 0 && nextPost != oldPost) SetPointer(plan, nextPost, 0, person, "目标俱乐部" + role + "岗位");

            Require(plan.Edits.Count != 0, "当前职务和所属俱乐部已经一致。");
            foreach (var edit in plan.Edits)
                foreach (var captured in data.Reads)
                {
                    ulong at = captured.Key.Item1, end = at + (ulong)captured.Key.Item2;
                    if (Overlaps(edit.Address, edit.Address + (ulong)edit.After.Length, at, end))
                        Require(at == edit.Address && captured.Value.SequenceEqual(edit.Before), "修改范围与其他职员资料重叠。");
                }
            Require(data.Matches(read), "游戏正在更新职员资料，请暂停后重新预览。");
            return plan;
        }
    }
}
