using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Fm24Editor
{
    static class ClubFinanceTests
    {
        public static void Run(Action<bool, string> check)
        {
            using (var form = new MainForm(false, false)) form.CheckClubFinance(check);
        }
    }

    partial class MainForm
    {
        internal void CheckClubFinance(Action<bool, string> check)
        {
            // The editor reads and writes only records allocated in the regression process.
            const int bytes = 0x8000;
            var records = Marshal.AllocHGlobal(bytes);
            var priorSafety = Safety.Off;
            freezeTimer.Stop();
            try
            {
                Marshal.Copy(new byte[bytes], 0, records, bytes);
                ulong module = (ulong)records.ToInt64();
                fm = new FmProcess { Base = module, H = new IntPtr(-1), Version = "offline-club-finance" };
                Safety.Off = new Dictionary<string, string> { { "clubname", "outside this fixture" } };
                var first = TestFinanceClub(module + 0x1000, 701, "Finance Alpha");
                var second = TestFinanceClub(module + 0x3000, 702, "Finance Beta");
                var compact = new Club { Addr = module + 0x5000, Uid = 703, Name = "Finance Compact", CompactFinance = true };
                clubs = new List<Club> { first, second, compact };
                FilterClubs();
                lstClubs.SelectedItem = first;
                CheckClubFinanceAmounts(first, check);
                CheckClubFinanceOutcomes(first, check);
                CheckClubFinanceFreezes(first, second, check);
                CheckClubFinanceSelection(first, second, compact, check);
            }
            finally
            {
                fm = null;
                Safety.Off = priorSafety;
                Marshal.FreeHGlobal(records);
            }
        }

        Club TestFinanceClub(ulong address, int uid, string name)
        {
            ulong finance = address + 0x800;
            Marshal.WriteInt64(new IntPtr((long)address), (long)(fm.Base + FmProcess.ClubVtRva));
            Marshal.WriteInt32(new IntPtr((long)address + 12), uid);
            Marshal.WriteInt64(new IntPtr((long)(address + FmLayout.ClubFinance)), (long)finance);
            Marshal.WriteInt64(new IntPtr((long)finance), (long)(fm.Base + FmProcess.FinanceVtRva));
            var club = new Club { Addr = address, Uid = uid, Name = name, Finance = finance };
            SetTestFinances(club);
            return club;
        }

        void SetTestFinances(Club club, int budget = 5000000)
        {
            int[] amounts = { 10000000, budget, 6000000, 120000, 110000 };
            var fields = FinFields;
            for (int index = 0; index < amounts.Length; index++)
                Marshal.WriteInt32(new IntPtr((long)(club.Finance + fields[index])), amounts[index]);
        }

        int?[] TestFinanceValues(Club club)
        {
            return FinFields.Select(field => fm.ReadI32(club.Finance + field)).ToArray();
        }

        bool FinanceLogHas(string text, int followedByItem)
        {
            return lstLog.Items.Cast<object>().Skip(followedByItem).Any(item => item.ToString().Contains(text));
        }

        void CheckClubFinanceAmounts(Club club, Action<bool, string> check)
        {
            ulong transfer = club.Finance + FmLayout.FinTransfer;
            txtCur.Text = "¥";
            txtRate.Text = "8.5";
            LoadClub(club);
            txtTransfer.Text = "68,000,000";
            ApplyClub();
            check(TestFinanceValues(club).SequenceEqual(new int?[] { 10000000, 8000000, 6000000, 120000, 110000 }),
                "normal ApplyClub stores only the remaining transfer budget in pounds");
            var change = changes.Last();
            check(change.Addr == transfer && change.What.Contains("回读通过") && txtTransfer.Text == "68,000,000",
                "the editor refreshes the converted amount and adds a verified undo entry");
            check(feedbackTitle.Text.EndsWith(" · 成功") && lblStatus.Text.Contains("剩余转会预算：已写入 £8,000,000，回读通过。"),
                "successful write feedback shows the complete verified amount");
            lstLog.ClearSelected();
            lstLog.SelectedItem = change;
            UndoSelected();
            check(change.Undone && fm.ReadI32(transfer) == 5000000 && txtTransfer.Text == "42,500,000",
                "the existing selected-undo command restores the transfer budget and its display");

            SetTestFinances(club, 0);
            txtRate.Text = "1e308";
            LoadClub(club);
            double ignored;
            check(!ParseMoney(txtBalance.Text, out ignored), "the untouched balance text can be unparseable");
            int reads = 0;
            txtTransfer.Text = "1" + new string('0', 308);
            ApplyClub(_ => false, address =>
            {
                if (address != transfer) throw new InvalidOperationException("Read an unrelated finance draft.");
                reads++;
                return fm.ReadI32(address);
            });
            check(reads > 0 && fm.ReadI32(transfer) == 1 && fm.ReadI32(club.Finance + FmLayout.FinBalance) == 10000000,
                "a single changed field ignores the untouched text and reads only the pending field before commit");

            txtCur.Text = "£";
            SetTestFinances(club);
            LoadClub(club);
            int prior = changes.Count, logCount = lstLog.Items.Count;
            txtBalance.Text = "11,000,000";
            txtTransfer.Text = "7,000,000";
            txtWage.Text = "invalid";
            ApplyClub();
            check(changes.Count == prior && TestFinanceValues(club).SequenceEqual(new int?[] { 10000000, 5000000, 6000000, 120000, 110000 }),
                "a later invalid edited field prevents every pending amount from being written");
            check(FinanceLogHas("俱乐部结余 本次有金额未通过校验", logCount) &&
                FinanceLogHas("剩余转会预算 本次有金额未通过校验", logCount) && FinanceLogHas("周薪预算 金额格式无效", logCount),
                "preflight gives every dirty field its own reason");
            check(feedbackTitle.Text.EndsWith(" · 未完成") && lblStatus.Lines.Length==4 &&
                lblStatus.Lines[3]=="周薪预算：金额格式无效。",
                "the visible failure feedback retains the last field's full reason as well as the earlier fields");

            LoadClub(club);
            foreach (string entered in new[] { "2147483648", "-2147483649", "2147483647.25", "1,000,000,000,000,000,000,000" })
            {
                txtTransfer.Text = entered;
                ApplyClub();
                check(fm.ReadI32(transfer) == 5000000 && changes.Count == prior && lblStatus.Text.Contains("超出"),
                    "converted value outside Int32 is refused before rounding or casting: " + entered);
            }
            foreach (int limit in new[] { int.MaxValue, int.MinValue })
            {
                txtTransfer.Text = Money(limit);
                ApplyClub();
                check(fm.ReadI32(transfer) == limit && lblStatus.Text.Contains("回读通过"), "the inclusive Int32 limit is writable: " + limit);
            }
            SetTestFinances(club);
            txtCur.Text = "¥";
            txtRate.Text = "1e-310";
            LoadClub(club);
            txtTransfer.Text = "100";
            ApplyClub();
            check(fm.ReadI32(transfer) == 5000000 && lblStatus.Text.Contains("有限数值"),
                "a division that produces infinity is rejected");
            txtCur.Text = "£";
            LoadClub(club);
            prior = changes.Count;
            txtTransfer.Text = "5,000,000.2";
            ApplyClub();
            check(changes.Count == prior && lblStatus.Text.Contains("折算后同值"), "equal rounded amounts report an explicit skip");

            txtBalance.Text = "11,000,000";
            txtTransferNext.Text = "7,000,000";
            txtWage.Text = "130,000";
            ApplyClub();
            check(TestFinanceValues(club).SequenceEqual(new int?[] { 11000000, 5000000, 7000000, 130000, 110000 }),
                "balance, next-season transfer budget and weekly wage budget keep their own fields and units");
            var siblingChanges = changes.Skip(prior).ToList();
            Undo(siblingChanges);
            check(siblingChanges.Count == 3 && siblingChanges.All(item => item.Undone) &&
                TestFinanceValues(club).SequenceEqual(new int?[] { 10000000, 5000000, 6000000, 120000, 110000 }),
                "the existing undo restores all three neighboring fields");
        }

        void CheckClubFinanceOutcomes(Club club, Action<bool, string> check)
        {
            ulong transfer = club.Finance + FmLayout.FinTransfer;
            int count = changes.Count;
            txtTransfer.Text = "7,000,000";
            ApplyClub(_ => false, address => address == transfer ? (int?)null : fm.ReadI32(address));
            check(changes.Count == count && lblStatus.Text.Contains("旧值读取失败"), "failed old-value read is reported without writing");
            Marshal.WriteInt32(new IntPtr((long)transfer), 5500000);
            ApplyClub();
            check(changes.Count == count && fm.ReadI32(transfer) == 5500000 && lblStatus.Text.Contains("旧值已变更"),
                "a changed live value prevents stale submission");
            LoadClub(club);
            txtTransfer.Text = "7,000,000";
            int logCount = lstLog.Items.Count, errorCode = 0;
            var invalidHandle = new FmProcess { H = IntPtr.Zero };
            ApplyClub(_ => false, null, delegate(ulong address, byte[] payload, out int error)
            {
                bool success = invalidHandle.Write(address, payload, out error);
                errorCode = error;
                return success;
            });
            check(changes.Count == count && errorCode != 0 && fm.ReadI32(transfer) == 5500000 &&
                FinanceLogHas("写入失败（Windows 错误码 " + errorCode, logCount),
                "a real failed Win32 call reports its code and creates no undo record");
            txtTransfer.Text = "7,000,000";
            ApplyClub(_ => false, null, delegate(ulong address, byte[] payload, out int error)
            {
                return fm.Write(address, BitConverter.GetBytes(6000000), out error);
            });
            check(changes.Count == count && lblStatus.Text.Contains("回读不一致") &&
                fm.ReadI32(transfer) == 6000000 && txtTransfer.Text == "6,000,000",
                "successful write API status still requires equal readback before recording success");

            ulong balance=club.Finance+FmLayout.FinBalance;
            txtBalance.Text="11,000,000";
            txtTransfer.Text="7,000,000";
            int attempts=0;
            ApplyClub(_=>false,null,delegate(ulong address,byte[] payload,out int error)
            {
                attempts++;
                return address==transfer?invalidHandle.Write(address,payload,out error):fm.Write(address,payload,out error);
            });
            check(attempts==2 && changes.Count==count+1 && changes.Last().Addr==balance && fm.ReadI32(transfer)==6000000,
                "a partial finance result writes each edited target at most once and records only the verified change");
            check(feedbackTitle.Text.EndsWith(" · 部分成功") && lblStatus.Lines.Any(line=>line.Contains("俱乐部结余：已写入")) &&
                lblStatus.Lines.Any(line=>line.Contains("剩余转会预算：写入失败（Windows 错误码 ")),
                "partial feedback includes both the confirmed write and the full failure detail");
            SetTestFinances(club);
            LoadClub(club);
        }

        void CheckClubFinanceFreezes(Club first, Club second, Action<bool, string> check)
        {
            ulong transfer = first.Finance + FmLayout.FinTransfer;
            ulong wage = first.Finance + FmLayout.FinWageBudget;
            check(FreezeMoney(first) == 3, "the existing club freeze registers the transfer, balance and wage floors");
            var budgetFreeze = freezes.Single(floor => floor.Addr == transfer);
            var wageFreeze = freezes.Single(floor => floor.Addr == wage);
            txtTransfer.Text = "4,000,000";
            txtWage.Text = "125,000";
            string confirmText = "";
            int count = changes.Count;
            ApplyClub(message => { confirmText = message; return false; });
            check(confirmText.Contains("当前冻结下限 £5,000,000") && confirmText.Contains("£4,000,000") &&
                changes.Count == count && fm.ReadI32(transfer) == 5000000 && fm.ReadI32(wage) == 120000 &&
                BitConverter.ToInt32(budgetFreeze.Value, 0) == 5000000 && lblStatus.Text.Contains("已取消"),
                "cancelling the freeze request preserves the whole submitted batch and its floors");

            txtWage.Text = "120,000";
            ApplyClub(message => true, null, delegate(ulong address, byte[] payload, out int error)
            {
                return fm.Write(address, BitConverter.GetBytes(4500000), out error);
            });
            check(changes.Count == count && lblStatus.Text.Contains("回读不一致") &&
                BitConverter.ToInt32(budgetFreeze.Value, 0) == 5000000,
                "approved reduction preserves the active floor when the budget does not read back as requested");
            FreezeTick();
            check(fm.ReadI32(transfer) == 5000000, "the unchanged floor still applies after an unverified write");
            LoadClub(first);
            txtTransfer.Text = "4,000,000";
            txtWage.Text = "125,000";
            ApplyClub(message => true);
            FreezeTick();
            check(fm.ReadI32(transfer) == 4000000 && BitConverter.ToInt32(budgetFreeze.Value, 0) == 4000000 &&
                budgetFreeze.What.Contains("4,000,000") && fm.ReadI32(wage) == 125000 &&
                BitConverter.ToInt32(wageFreeze.Value, 0) == 120000,
                "approval updates just the lowered floor after readback and the next freeze tick keeps the new budget");
            var budgetChange = changes.Skip(count).Single(item => item.Addr == transfer);
            lstLog.ClearSelected();
            lstLog.SelectedItem = budgetChange;
            UndoSelected();
            check(budgetChange.Undone && fm.ReadI32(transfer) == 5000000 && !freezes.Contains(budgetFreeze) && freezes.Contains(wageFreeze),
                "undo of a frozen budget restores its old value and stops only its overlapping freeze");

            FreezeMoney(first);
            count = changes.Count;
            txtTransfer.Text = "4,000,000";
            ApplyClub(message => { lstClubs.SelectedItem = second; return true; });
            check(changes.Count == count && fm.ReadI32(transfer) == 5000000 && loadedClub == second &&
                lblStatus.Text.Contains("选择或财务草稿已变化"), "selection changes during confirmation cancel the prepared transfer edit");
            StopAllFreezes(null);
            lstClubs.SelectedItem = first;
        }

        void CheckClubFinanceSelection(Club first, Club second, Club compact, Action<bool, string> check)
        {
            ulong transfer = first.Finance + FmLayout.FinTransfer;
            int count = changes.Count;
            Safety.Off["finance"] = "99 家具备完整财务的俱乐部（至少需要 100 家）";
            LoadClub(first);
            check(txtTransfer.ReadOnly && lblClub.Text.Contains("全局财务检查未通过"),
                "the finance-wide gate displays its original reason in the club pane");
            txtTransfer.Text = "7,000,000";
            ApplyClub();
            check(changes.Count == count && fm.ReadI32(transfer) == 5000000 && lblStatus.Text.Contains("财务只读"),
                "a read-only finance draft cannot write");
            check(feedbackTitle.Text.EndsWith(" · 只读拦截") && lblStatus.Text.Contains(Safety.Off["finance"]),
                "read-only finance feedback preserves the exact threshold reason");
            Safety.Off.Remove("finance");
            Safety.Off["core"] = "模拟核心数据检查失败";
            LoadClub(first);
            check(txtTransfer.ReadOnly && !btnApplyClub.Enabled && lblClub.Text.Contains("全局安全检查未通过"),
                "the core gate disables submission and shows the matching reason");
            txtTransfer.Text = "7,000,000";
            ApplyClub();
            check(changes.Count == count && fm.ReadI32(transfer) == 5000000 && lblStatus.Text.Contains("全局安全检查未通过"),
                "the core safety gate also rejects direct submission");
            Safety.Off.Remove("core");
            LoadClub(first);

            var firstPreview = loadedFin;
            txtTransfer.Text = "7,000,000";
            lstClubs.SelectedItem = second;
            check(loadedClub == second && loadedFin != firstPreview && txtTransfer.Text == Money(5000000),
                "switching clubs clears the previous draft and reloads the newly selected club");
            loadedClub = first;
            loadedFin = firstPreview;
            txtTransfer.Text = "7,000,000";
            ApplyClub();
            check(changes.Count == count && fm.ReadI32(transfer) == 5000000 && loadedFin == null &&
                lblStatus.Text.Contains("选择或财务草稿已变化"),
                "submit refuses a deliberate mismatch of list selection and loaded preview");
            LoadClub(second);
            txtTransfer.Text = "7,000,000";
            txtClubSearch.Text = "Nothing matched";
            check(lstClubs.SelectedItem == null && loadedClub == null && loadedFin == null &&
                txtTransfer.Text == "" && !btnApplyClub.Enabled, "losing the selected club in a filter clears the draft and disables submit");
            txtClubSearch.Text = "";
            lstClubs.SelectedItem = compact;
            check(loadedFin == null && txtTransfer.ReadOnly && !btnApplyClub.Enabled && lblClub.Text.Contains("简化财务"),
                "a compact-finance club stays read-only with its reason visible");
            ApplyClub();
            check(changes.Count == count && lblStatus.Text.Contains("简化财务"), "the compact-finance guard also rejects direct invocation");
        }
    }
}
