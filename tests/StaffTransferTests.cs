using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Fm24Editor
{
    static class StaffTransferTests
    {
        const ulong Module = 0x100000000, ImageSize = 0x20000000;
        const ulong Owner = 0x20100, Person = Owner - 0x100, Contract = 0x30000;
        const ulong Source = 0x41000, Destination = 0x42000, HomeTeam = 0x51000, Youth = 0x52000, AwayTeam = 0x53000;
        const ulong HomeLists = 0x61000, AwayLists = 0x62000, Teams = 0x71000;
        const ulong BasicType = Module + 0x15000, BasicLocator = Module + 0x15100, BasicDescriptor = Module + 0x15200;
        const ulong Other = 0x82000, Existing = 0x83000;
        const ulong Office = 0x84000, OfficeTeam = 0x85000, Witness = 0x86000, WitnessContract = 0x87000;

        sealed class Save
        {
            readonly Dictionary<ulong, byte> bytes = new Dictionary<ulong, byte>();
            public readonly Staff Staff = new Staff { Name = "Example Employee", Search = "Example Employee", Uid = 7001, Addr = Owner, Pers = Owner };
            public readonly Club From = new Club { Name = "源俱乐部", Addr = Source, Uid = 701, Team = HomeTeam, Finance = 0x80000 };
            public readonly Club To = new Club { Name = "东方巨龙足球俱乐部", ShortName = "Oriental Dragon", Addr = Destination, Uid = 702, Team = AwayTeam };
            public readonly Club Example = new Club { Name = "现任岗位样本", Addr = Office, Uid = 703, Team = OfficeTeam };
            public int Writes, FailWrite;
            public bool FailAfterStore;
            public Action<int> OnWrite;

            public Save(StaffRole role, bool basic = false, bool youth = false)
            {
                Staff.SetClub(From.Name);
                Q(Owner, Module + FmProcess.StaffVtRva);
                Put(Owner + FmLayout.Uid, BitConverter.GetBytes(Staff.Uid));
                Q(Owner + FmLayout.StaffContract, Contract);
                Put(Contract, new byte[basic ? 0x28 : 0xC8]);
                Q(Contract, basic ? BasicType : Module + FmProcess.ContractVtRva);
                Q(Contract + 8, Owner);
                Q(Contract + 0x10, youth ? Youth : HomeTeam);
                Q(Contract + 0x18, HomeTeam);
                Put(Contract + 0x20, BitConverter.GetBytes(2400));
                Put(Contract + 0x26, new[] { role.Code, (byte)0xA5 });
                if (basic)
                {
                    Q(BasicType - 8, BasicLocator);
                    Put(BasicLocator, new byte[24]);
                    Put(BasicLocator, BitConverter.GetBytes(1));
                    Put(BasicLocator + 12, BitConverter.GetBytes((uint)(BasicDescriptor - Module)));
                    Put(BasicLocator + 20, BitConverter.GetBytes((uint)(BasicLocator - Module)));
                    Put(BasicDescriptor + 16, Encoding.ASCII.GetBytes(".?AVBASIC_CONTRACT@db@@\0"));
                    Put(Contract + 0x28, new byte[] { 0x39, 0x34, 0x33, 0x32, 0x31, 0x53, 0x35, 0x36 });
                }
                else Put(Contract + 0x48, BitConverter.GetBytes((2028u << 16) | 182));

                ClubData(From, Teams, HomeTeam, Youth);
                ClubData(To, Teams + 0x100, AwayTeam);
                for (int department = 0; department < 3; department++)
                {
                    if (department == (int)role.Post)
                        List(Source, department, 4, Other, Person, Existing);
                    else List(Source, department, 4, Other);
                    List(Destination, department, 4, Existing);
                }
                if (role.Post == StaffPost.Manager) Q((youth ? Youth : HomeTeam) + 0x80, Person);
                if (role.Post == StaffPost.FootballDirector) Q(Source + 0x148, Person);
            }

            void ClubData(Club club, ulong buffer, params ulong[] teams)
            {
                Put(club.Addr, new byte[0x170]);
                Q(club.Addr, Module + FmProcess.ClubVtRva);
                Put(club.Addr + 0x0C, BitConverter.GetBytes(club.Uid));
                Vector(club.Addr + 0x18, buffer, 4, teams);
                foreach (ulong team in teams)
                {
                    Put(team, new byte[0x88]);
                    Q(team, Module + FmProcess.TeamVtRva);
                    Q(team + 0x30, club.Addr);
                }
            }

            void Vector(ulong header, ulong buffer, int capacity, ulong[] items)
            {
                Q(header, buffer); Q(header + 8, buffer + (ulong)items.Length * 8); Q(header + 16, buffer + (ulong)capacity * 8);
                Put(buffer, new byte[capacity * 8]);
                for (int i = 0; i < items.Length; i++) Q(buffer + (ulong)i * 8, items[i]);
            }

            public void List(ulong club, int department, int capacity, params ulong[] items)
            {
                Vector(club + FmLayout.ClubStaffLists[department],
                    (club == Source ? HomeLists : AwayLists) + (ulong)department * 0x100, capacity, items);
            }

            public ulong[] Roster(ulong club, int department)
            {
                ulong header = club + FmLayout.ClubStaffLists[department];
                ulong first = Ptr(header), end = Ptr(header + 8);
                return Enumerable.Range(0, (int)((end - first) / 8)).Select(i => Ptr(first + (ulong)i * 8)).ToArray();
            }

            public void Q(ulong at, ulong value) { Put(at, BitConverter.GetBytes(value)); }
            public ulong Ptr(ulong at) { return BitConverter.ToUInt64(Read(at, 8), 0); }

            public void Put(ulong at, byte[] values)
            { for (int i = 0; i < values.Length; i++) bytes[at + (ulong)i] = values[i]; }

            public byte[] Read(ulong at, int count)
            {
                var values = new byte[count];
                for (int i = 0; i < count; i++)
                    if (!bytes.TryGetValue(at + (ulong)i, out values[i])) return null;
                return values;
            }

            public bool Write(ulong at, byte[] values)
            {
                Writes++;
                if (Writes != FailWrite || FailAfterStore) Put(at, values);
                if (OnWrite != null) OnWrite(Writes);
                return Writes != FailWrite;
            }

            public Dictionary<ulong, byte> Copy() { return new Dictionary<ulong, byte>(bytes); }
            public bool EqualsSnapshot(Dictionary<ulong, byte> old)
            { return bytes.Count == old.Count && old.All(pair => bytes.ContainsKey(pair.Key) && bytes[pair.Key] == pair.Value); }

            public StaffTransferPlanner Planner() { return new StaffTransferPlanner(Read, Module, ImageSize); }
            public void ShowPost(StaffRole role)
            {
                ClubData(Example, Teams + 0x200, OfficeTeam);
                Q(role.Post == StaffPost.Manager ? OfficeTeam + 0x80 : Office + 0x148, Witness);
                Q(Witness + 0x100, Module + FmProcess.StaffVtRva);
                Q(Witness + 0x100 + FmLayout.StaffContract, WitnessContract);
                Put(WitnessContract, new byte[0x28]);
                Q(WitnessContract, Module + FmProcess.ContractVtRva);
                Q(WitnessContract + 8, Witness + 0x100);
                Q(WitnessContract + 0x10, OfficeTeam);
                Q(WitnessContract + 0x18, OfficeTeam);
                Put(WitnessContract + 0x26, new[] { role.Code });
            }

            public StaffTransferPlan Plan(StaffRole role = null, bool sameClub = false)
            {
                var planner = Planner();
                return planner.Plan(planner.ReadOrigin(Staff), sameClub ? From : To, role, new[] { From, To, Example });
            }
        }

        static bool Rejects(Action operation, string message)
        {
            try { operation(); return false; }
            catch (InvalidOperationException ex) { return ex.Message.Contains(message); }
            catch (InvalidDataException ex) { return ex.Message.Contains(message); }
        }

        public static void Run(Action<bool, string> check)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            Profile.Load("27.1.0+2505117", dir);
            try
            {
                TestJobs(check);
                TestNewRole(check);
                TestUnsafeMoves(check);
                TestTransactions(check);
                TestControls(check);
            }
            finally { Profile.Load("", dir); }
        }

        static void TestJobs(Action<bool, string> check)
        {
            check(StaffTransferPlanner.LayoutSupported, "FM27.1 uses the verified staff transfer layout");
            check(StaffRole.All.Length == 21 && StaffRole.All.GroupBy(role => role.Code).All(group => group.Count() == 1), "the searchable/transferable job list has 21 distinct choices");
            check(StaffRole.Title(12) == "理疗师" && StaffRole.Title(50) == "首席理疗师" &&
                StaffRole.Title(46) == "队医" && StaffRole.Title(38) == "首席队医", "physios and doctors have distinct localized titles");
            foreach (var role in StaffRole.All)
            {
                bool basic = role.Code == 46 || role.Code == 38;
                var save = new Save(role, basic, role.Post == StaffPost.Manager);
                var before = save.Copy();
                var plan = save.Plan();
                check(plan.Origin.Employment.Full != basic && plan.Role == role && save.EqualsSnapshot(before), role + " preview reads the correct contract and never writes");
                string error;
                check(plan.Apply(save.Read, save.Write, () => true, false, out error) && error == null, role + " may move with its existing job");
                check(save.Ptr(Contract + 0x10) == AwayTeam && save.Ptr(Contract + 0x18) == AwayTeam &&
                    save.Read(Contract + 0x26, 2).SequenceEqual(new[] { role.Code, (byte)0xA5 }), role + " contracts change both team references and preserve the job/flags");
                check(save.Read(Contract + 0x20, 4).SequenceEqual(BitConverter.GetBytes(2400)) &&
                    save.Read(Contract + (basic ? 0x28UL : 0x48UL), basic ? 8 : 4).SequenceEqual(
                        Enumerable.Range(0, basic ? 8 : 4).Select(i => before[Contract + (basic ? 0x28UL : 0x48UL) + (ulong)i])),
                    role + " wage, expiry or neighbouring basic contract stay unchanged");
                if (role.Post == StaffPost.Manager)
                    check(save.Ptr(Youth + 0x80) == 0 && save.Ptr(AwayTeam + 0x80) == Person, "a youth manager leaves his own team slot and joins the senior team");
                else if (role.Post == StaffPost.FootballDirector)
                    check(save.Ptr(Source + 0x148) == 0 && save.Ptr(Destination + 0x148) == Person, "a football director moves between dedicated posts");
                else
                {
                    int department = (int)role.Post;
                    check(save.Roster(Source, department).SequenceEqual(new[] { Other, Existing }) &&
                        save.Roster(Destination, department).SequenceEqual(new[] { Existing, Person }), role + " leaves the old list and joins the correct new list");
                }
                check(plan.Apply(save.Read, save.Write, () => true, true, out error) && save.EqualsSnapshot(before), role + " restores the exact original snapshot on group undo");
            }
        }

        static void TestNewRole(Action<bool, string> check)
        {
            var coach = StaffRole.Find(26);
            var physio = StaffRole.Find(12);
            var save = new Save(coach, false, true);
            save.List(Source, 0, 4, Other, Person);
            save.List(Destination, 0, 2); // A valid empty vector with already allocated capacity.
            var baseline = save.Copy();
            string error;
            var keep = save.Plan();
            check(keep.Apply(save.Read, save.Write, () => true, false, out error) &&
                save.Roster(Destination, 0).Contains(Person) && save.Roster(Destination, 1).Contains(Person) &&
                !save.Roster(Source, 0).Contains(Person) && !save.Roster(Source, 1).Contains(Person),
                "keeping the original fitness role moves both verified departments");
            check(keep.Apply(save.Read, save.Write, () => true, true, out error) && save.EqualsSnapshot(baseline), "both department lists restore together");
            var change = save.Plan(physio);
            check(change.Apply(save.Read, save.Write, () => true, false, out error) &&
                save.Roster(Destination, 0).SequenceEqual(new[] { Person }) && !save.Roster(Destination, 1).Contains(Person) &&
                save.Read(Contract + 0x26, 1)[0] == physio.Code, "moving as a physio uses the empty medical roster and drops previous coaching membership");
            check(change.Apply(save.Read, save.Write, () => true, true, out error) && save.EqualsSnapshot(baseline), "a cross-role move undoes all writes");
            change = save.Plan(physio, true);
            check(change.Apply(save.Read, save.Write, () => true, false, out error) &&
                save.Ptr(Contract + 0x10) == Youth && save.Ptr(Contract + 0x18) == HomeTeam &&
                save.Roster(Source, 0).Count(p => p == Person) == 1 && !save.Roster(Source, 1).Contains(Person),
                "a same-club change uses the existing correct department entry and keeps the youth contract");
            check(change.Apply(save.Read, save.Write, () => true, true, out error) && save.EqualsSnapshot(baseline), "same-club role change also restores both memberships");
            check(Rejects(() => save.Plan(null, true), "已经一致"), "a same-club no-op is detected before writing");

            check(Rejects(() => save.Plan(StaffRole.Find(16), true), "未读到可用于核对"), "an uncorroborated empty manager slot cannot receive a new appointment");
            save.ShowPost(StaffRole.Find(16));
            baseline = save.Copy();
            change = save.Plan(StaffRole.Find(16), true);
            check(change.Apply(save.Read, save.Write, () => true, false, out error) &&
                save.Ptr(Contract + 0x10) == HomeTeam && save.Ptr(HomeTeam + 0x80) == Person && save.Ptr(Youth + 0x80) == 0 &&
                !save.Roster(Source, 0).Contains(Person) && !save.Roster(Source, 1).Contains(Person),
                "new senior manager is appointed from youth staff and vacates old departments");
            check(change.Apply(save.Read, save.Write, () => true, true, out error) && save.EqualsSnapshot(baseline), "manager appointment can be undone");

            save = new Save(StaffRole.Find(16), false, true);
            save.ShowPost(StaffRole.Find(10));
            change = save.Plan(StaffRole.Find(10));
            check(change.Apply(save.Read, save.Write, () => true, false, out error) &&
                save.Ptr(Youth + 0x80) == 0 && save.Ptr(Destination + 0x148) == Person && save.Ptr(AwayTeam + 0x80) == 0,
                "moving from manager to football director updates exactly the two appropriate posts");

            save = new Save(physio, true);
            save.List(Source, 0, 4, Other);
            save.List(Source, 1, 4, Other, Person);
            change = save.Plan(physio, true);
            check(change.Apply(save.Read, save.Write, () => true, false, out error) &&
                save.Roster(Source, 0).Contains(Person) && !save.Roster(Source, 1).Contains(Person),
                "explicitly reapplying a role repairs its mismatched department from an older role-only edit");
        }

        static void TestUnsafeMoves(Action<bool, string> check)
        {
            var doctor = StaffRole.Find(46);
            var save = new Save(doctor, true);
            save.List(Destination, 0, 1, Existing);
            check(Rejects(() => save.Plan(), "医疗部门当前没有可用空位") && save.Writes == 0, "a full medical list refuses the move before writes");
            save.Put(Destination + 0x60, new byte[24]);
            check(Rejects(() => save.Plan(), "尚无可写入的部门名单") && save.Writes == 0, "an unallocated medical list is distinguished from a full list");
            save.List(Destination, 0, 4, Person);
            check(Rejects(() => save.Plan(), "已存在该职员"), "a duplicate at the destination is rejected");
            save.List(Destination, 0, 4, Existing);
            save.Put(Destination + 0x0C, BitConverter.GetBytes(999));
            check(Rejects(() => save.Plan(), "身份已变化"), "stale club UID is detected before writes");
            save.Put(Destination + 0x0C, BitConverter.GetBytes(save.To.Uid));
            save.Put(Destination + 0xB9, new byte[] { 1 });
            check(Rejects(() => save.Plan(), "男女足类别"), "the staff member stays in the same club category");
            save.Put(Destination + 0xB9, new byte[] { 0 });
            save.Q(AwayTeam + 0x30, Source);
            check(Rejects(() => save.Plan(), "球队归属"), "a target team must actually belong to the selected club");
            save.Q(AwayTeam + 0x30, Destination);
            save.Q(Destination + 0x68, AwayLists + 7);
            check(Rejects(() => save.Plan(), "名单结构"), "a malformed vector is rejected");
            save.List(Destination, 0, 4, Existing);
            save.Q(Destination + 0x60, HomeLists); save.Q(Destination + 0x68, HomeLists); save.Q(Destination + 0x70, HomeLists + 8);
            check(Rejects(() => save.Plan(), "重叠"), "two clubs must not alias a writable vector buffer");
            save.Q(Destination + 0x60, Contract + 0x10); save.Q(Destination + 0x68, Contract + 0x10); save.Q(Destination + 0x70, Contract + 0x18);
            check(Rejects(() => save.Plan(), "指向其他记录"), "an empty destination cannot point into a contract field");
            save.List(Destination, 0, 4, Existing);
            save.Q(AwayTeam + 0x80, Existing);
            check(Rejects(() => save.Plan(StaffRole.Find(16)), "岗位已有职员"), "a staff move cannot evict the destination's manager");
            save.Q(Destination + 0x148, Existing);
            check(Rejects(() => save.Plan(StaffRole.Find(10)), "岗位已有职员"), "a staff move cannot evict the destination's football director");
            save.Q(HomeTeam + 0x80, Person);
            check(Rejects(() => save.Plan(), "主教练或足球总监归属"), "a doctor with an unexpected dedicated post is refused");
            save.Q(HomeTeam + 0x80, 0);
            save.List(Source, 0, 4, Other, Person, Person);
            check(Rejects(() => save.Plan(), "重复记录"), "a repeated source entry is rejected");
            save.List(Source, 0, 4, Other);
            check(Rejects(() => save.Plan(), "不在原俱乐部"), "a contract by itself cannot establish staff list membership");

            save = new Save(doctor, true);
            save.Put(BasicDescriptor + 16, new byte[] { 0 });
            check(Rejects(() => save.Plan(), "合同类型"), "the BASIC_CONTRACT class must be identified by its exact native type");
            save = new Save(doctor, true);
            save.Q(Contract + 8, Owner + 8);
            check(Rejects(() => save.Plan(), "合同类型"), "a mismatched contract owner is refused");
            save = new Save(doctor, true);
            save.Q(Owner + FmLayout.StaffContract, 0);
            check(Rejects(() => save.Plan(), "无俱乐部合同"), "a free agent cannot be moved using a missing contract");
            save = new Save(doctor, true);
            save.Staff.Manager = true;
            check(Rejects(() => save.Plan(), "由游戏控制"), "player-controlled managers cannot be edited through transfer");
            save = new Save(doctor, true);
            save.Put(Contract + 0x26, new byte[] { 35 });
            check(Rejects(() => save.Plan(), "没有已核实"), "a player-coach is not transferred as regular staff");
        }

        static void TestTransactions(Action<bool, string> check)
        {
            var fromRole = StaffRole.Find(12);
            var toRole = StaffRole.Find(60);
            int edits = new Save(fromRole).Plan(toRole).Edits.Count;
            string error;
            for (int failure = 1; failure <= edits; failure++)
            {
                var save = new Save(fromRole);
                var before = save.Copy();
                var plan = save.Plan(toRole);
                save.FailWrite = failure;
                save.FailAfterStore = failure % 2 == 0;
                check(!plan.Apply(save.Read, save.Write, () => true, false, out error) && save.EqualsSnapshot(before) &&
                    error.Contains("本次修改已取消"), "every failed forward write rolls back the employment and both lists");
            }
            var verified = new Save(fromRole);
            var start = verified.Copy();
            var transfer = verified.Plan(toRole);
            verified.Q(Destination + 0x88, AwayLists + 0x108);
            check(!transfer.Apply(verified.Read, verified.Write, () => true, false, out error) && verified.Writes == 0, "a changed capacity after confirmation forces a fresh preview");
            verified.Q(Destination + 0x88, AwayLists + 0x120);
            check(!transfer.Apply(verified.Read, verified.Write, () => false, false, out error) && verified.Writes == 0, "disconnecting or loading another save prevents all writes");
            check(transfer.Apply(verified.Read, verified.Write, () => true, false, out error), "valid preview can still be applied when the connection is verified");
            int count = verified.Writes;
            verified.Put(Owner + FmLayout.Uid, BitConverter.GetBytes(7002));
            check(!transfer.Apply(verified.Read, verified.Write, () => true, true, out error) && verified.Writes == count, "a stale person identity disallows group undo");
            verified.Put(Owner + FmLayout.Uid, BitConverter.GetBytes(7001));
            var changed = verified.Copy();
            verified.FailWrite = count + 3;
            check(!transfer.Apply(verified.Read, verified.Write, () => true, true, out error) && verified.EqualsSnapshot(changed), "a failed group undo rolls back to the applied state");
            check(transfer.Apply(verified.Read, verified.Write, () => true, true, out error) && verified.EqualsSnapshot(start), "a successful undo can be retried after a fully recovered failure");

            verified = new Save(fromRole);
            start = verified.Copy();
            transfer = verified.Plan(toRole);
            var first = transfer.Edits[0];
            verified.OnWrite = write =>
            {
                if (write == transfer.Edits.Count) verified.Put(first.Address, first.Before);
            };
            check(!transfer.Apply(verified.Read, verified.Write, () => true, false, out error) &&
                verified.EqualsSnapshot(start) && error.Contains("写入结束"), "a changed earlier write is caught in final verification and the other writes roll back");
        }

        static void TestControls(Action<bool, string> check)
        {
            var save = new Save(StaffRole.Find(46), true);
            var origin = save.Planner().ReadOrigin(save.Staff);
            using (var dialog = new StaffTransferDialog(origin, save.From, new[] { save.From, save.To }, false))
            {
                check(dialog.Roles.Items.OfType<StaffRole>().Contains(StaffRole.Find(12)) &&
                    dialog.Roles.Items.OfType<StaffRole>().Contains(StaffRole.Find(46)) && dialog.Roles.SelectedIndex == 0,
                    "the transfer screen offers doctor and physio and defaults to keeping the job");
                dialog.Search.Text = "Oriental Dragon";
                check(dialog.Targets.SelectedItem == save.To && !save.To.HasFullFinance && dialog.PreviewButton.Enabled,
                    "a patched club with verified teams is selectable without full financial data");
                dialog.Search.Text = "Missing Club";
                check(dialog.Targets.Items.Count == 0 && !dialog.PreviewButton.Enabled, "missing destination disables preview");
                dialog.Search.Text = "UID 701";
                check(dialog.Targets.SelectedItem == save.From, "the original club can be selected for an internal role change");
            }
            using (var form = new MainForm(false, false))
            {
                check(form.Controls.Find("StaffTransfer", true).OfType<Button>().Single().Text.Contains("职务"), "the normal Staff tab exposes the new transfer entry point");
                var field = typeof(MainForm).GetField("sfJob", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var job = (ComboBox)field.GetValue(form);
                check(job.Items.OfType<StaffRole>().SequenceEqual(StaffRole.All), "staff search and transfer use the same complete role catalog");
                form.CheckContractOnlyStaffDetail(check);
            }
            save.Staff.SetClub(save.To.Name);
            check(!save.Staff.Matches(save.From.Name) && save.Staff.Matches(save.To.Name), "staff search drops the old club after transfer");
            save.Staff.SetClub(save.From.Name);
            check(save.Staff.Matches(save.From.Name) && !save.Staff.Matches(save.To.Name) &&
                save.Staff.Matches("Example Employee"), "staff name search and original club work after undo");
        }
    }

    partial class MainForm
    {
        internal void CheckContractOnlyStaffDetail(Action<bool, string> check)
        {
            // The test uses the regression process's own memory to exercise the real profile view.
            const int length = 0x4000;
            IntPtr memory = Marshal.AllocHGlobal(length);
            var savedSafety = Safety.Off;
            try
            {
                Marshal.Copy(new byte[length], 0, memory, length);
                ulong module = (ulong)memory.ToInt64();
                fm = new FmProcess { Base = module, ImageSize = length, H = new IntPtr(-1), Version = "offline-staff-detail" };
                Safety.Off = new Dictionary<string, string> { { "staffcontract", "full contract check disabled for test" } };
                Action<int, int> number = (at, value) => Marshal.WriteInt32(memory, at, value);
                Action<int, ulong> pointer = (at, value) => Marshal.WriteInt64(memory, at, (long)value);
                const int person = 0x1100, contract = 0x1500, team = 0x1800, club = 0x2000;
                const int table = 0x2508, locator = 0x2600, descriptor = 0x2700;
                pointer(person, module + FmProcess.StaffVtRva);
                pointer(person + (int)FmLayout.StaffContract, module + contract);
                number(person + (int)FmLayout.Uid, 7001);
                pointer(contract, module + table);
                pointer(contract + 8, module + person);
                pointer(contract + 0x10, module + team); pointer(contract + 0x18, module + team);
                Marshal.WriteByte(memory, contract + 0x26, 46);
                pointer(table - 8, module + locator); number(locator, 1);
                number(locator + 12, descriptor); number(locator + 20, locator);
                var name = Encoding.ASCII.GetBytes(".?AVBASIC_CONTRACT@db@@\0");
                Marshal.Copy(name, 0, new IntPtr((long)module + descriptor + 16), name.Length);
                pointer(team, module + FmProcess.TeamVtRva); pointer(team + 0x30, module + club);
                pointer(club, module + FmProcess.ClubVtRva); number(club + 0x0C, 704);
                var doctor = new Staff { Addr = module + person, Pers = module + person, Uid = 7001, Name = "Doctor", ContractOnly = true };

                LoadStaff(doctor);
                check(loadedStaff == doctor && loadedStaffBlock == null && btnStaffMove.Enabled, "a verified doctor with missing attributes is selectable for transfer");
                check(gridStaff.Rows.Count == 2 && gridStaff.Rows[0].ReadOnly && gridStaff.Rows[staffRoleRow].ReadOnly &&
                    gridStaff.Rows[staffRoleRow].Cells[1].Value.ToString() == "队医", "the normal profile shows the doctor role without invented contract dates or editable attributes");
                number(person + (int)FmLayout.Uid, 7002);
                LoadStaff(doctor);
                check(loadedStaff == null && !btnStaffMove.Enabled, "a recycled person slot disables transfer from the normal UI");

                number(person + (int)FmLayout.Uid, 7001);
                pointer(person, module + FmProcess.PlayerCoachVtRva);
                var attrs = Enumerable.Repeat((byte)10, FmLayout.StaffBlockLen).ToArray();
                Marshal.Copy(attrs, 0, new IntPtr((long)module + person - (long)FmLayout.StaffBlockBefore), attrs.Length);
                Marshal.WriteByte(memory, contract + 0x26, 35);
                LoadStaff(doctor);
                check(loadedStaff == doctor && loadedStaffBlock != null && !btnStaffMove.Enabled &&
                    gridStaff.Rows[staffRoleRow].Cells[1].Value.ToString() == "球员兼门将教练", "player-coaches retain readable attributes and cannot enter the ordinary transfer planner");
            }
            finally
            {
                fm.H = IntPtr.Zero;
                fm = null;
                Safety.Off = savedSafety;
                Marshal.FreeHGlobal(memory);
            }
        }
    }
}
