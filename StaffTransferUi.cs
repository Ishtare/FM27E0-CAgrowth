// Modified build v1.1.28, 2026-11-21. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class StaffTransferDialog : Form
    {
        internal readonly TextBox Search = new TextBox { Name = "StaffTransferClubSearch", Dock = DockStyle.Fill };
        internal readonly ListBox Targets = new ListBox { Name = "StaffTransferClubs", Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        internal readonly ComboBox Roles = new ComboBox { Name = "StaffTransferRoles", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        internal readonly Button PreviewButton = new Button { Name = "PreviewStaffTransfer", Text = "预览…", Width = 106 };

        public StaffTransferDialog(StaffTransferOrigin origin, Club source, IEnumerable<Club> clubs, bool changeJob)
        {
            Name = "StaffTransferDialog";
            MainForm.StyleWorkspaceDialog(this, "职员转会与职务");
            ClientSize = new Size(560, 440);
            MinimumSize = new Size(500, 410);
            MaximizeBox = false;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, TabIndex = 0 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 46, 23, 31 })
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 23, 32 })
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            Search.TabIndex = 0;
            Targets.TabIndex = 1;
            Roles.TabIndex = 2;
            table.Controls.Add(new Label { Dock = DockStyle.Fill, AutoEllipsis = true,
                Text = origin.Person.DisplayName + "\r\n" + source.Name + " · " + StaffRole.Title(origin.Employment.Job) }, 0, 0);
            table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "目标俱乐部（可按名称或 UID 搜索）" }, 0, 1);
            table.Controls.Add(Search, 0, 2);
            table.Controls.Add(Targets, 0, 3);
            table.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "到任职务" }, 0, 4);
            table.Controls.Add(Roles, 0, 5);
            var actions = MainForm.DialogActions(this, MainForm.DialogDismiss("CancelStaffTransfer", "取消"), PreviewButton);
            actions.TabIndex = 1;
            Controls.Add(table);
            Controls.Add(actions);
            AcceptButton = PreviewButton;

            Roles.Items.Add("保留原职务（" + StaffRole.Title(origin.Employment.Job) + "）");
            Roles.Items.AddRange(StaffRole.All);
            Roles.SelectedIndex = changeJob ? Array.IndexOf(StaffRole.All, StaffRole.Find(origin.Employment.Job)) + 1 : 0;
            var candidates = clubs.Where(club => club.Team != 0 && club.Addr != source.Addr).ToArray();
            Targets.Items.Add(source);
            Targets.SelectedIndex = 0;
            Search.TextChanged += delegate
            {
                string query = Search.Text.Trim();
                var selected = Targets.SelectedItem as Club;
                Targets.BeginUpdate();
                try
                {
                    Targets.Items.Clear();
                    if (query.Length == 0 || ClubSearch.Matches(source, query)) Targets.Items.Add(source);
                    if (PlayerNames.CanSearch(query))
                        Targets.Items.AddRange(candidates.Where(club => ClubSearch.Matches(club, query)).Take(200).ToArray());
                    if (selected != null && Targets.Items.Contains(selected)) Targets.SelectedItem = selected;
                    else if (Targets.Items.Count > 0) Targets.SelectedIndex = 0;
                }
                finally { Targets.EndUpdate(); }
                PreviewButton.Enabled = Targets.SelectedItem is Club;
            };
            Shown += delegate { if (changeJob) Roles.Focus(); else Search.Focus(); };
        }
    }

    partial class MainForm
    {
        int staffFinderRevision;

        Club StaffSourceClub(FmProcess connected, StaffTransferOrigin origin)
        {
            var club = clubs.FirstOrDefault(item => item.Addr == origin.Club && item.Uid == origin.ClubUid);
            if (club != null) return club;
            string name = connected.ReadLenString(connected.ReadPtr(origin.Club + FmLayout.ClubName));
            return new Club { Addr = origin.Club, Uid = origin.ClubUid, Name = name ?? ("俱乐部 UID " + origin.ClubUid) };
        }

        void MoveStaffDialog(bool changeJob = false)
        {
            const string title = "职员转会与职务";
            var person = loadedStaff;
            if (person == null || fm == null) return;
            try
            {
                VerifyRosterConnection();
                if (!Safety.On("staffmove"))
                    throw new InvalidOperationException("本次俱乐部职员名单检查未通过。详见「连接详情」或「变更记录」。");

                var connected = fm;
                var save = scannedSaveLoads;
                Func<bool> current = () => fm == connected && btnScan.Enabled && !closingEditor &&
                    Safety.On("staffmove") && (!save.HasValue || SaveLoadCount(connected) == save);
                var planner = new StaffTransferPlanner(connected.Read, connected.Base, connected.ImageSize);
                var origin = planner.ReadOrigin(person);
                var source = StaffSourceClub(connected, origin);
                using (var dialog = new StaffTransferDialog(origin, source, clubs, changeJob))
                {
                    dialog.PreviewButton.Click += delegate
                    {
                        try
                        {
                            if (!current()) throw new InvalidOperationException("当前连接或存档已变化，请重新挂接并扫描。");
                            var plan = planner.Plan(origin, dialog.Targets.SelectedItem as Club, dialog.Roles.SelectedItem as StaffRole, clubs);
                            string summary = person.DisplayName + "\n" + source.Name + " · " + StaffRole.Title(origin.Employment.Job) +
                                "\n转到 " + plan.Destination.Name + " · " + plan.Role;
                            string terms = origin.Employment.Full ? "保留原工资和合同期限。" : "沿用原简式合同。";
                            if (MessageBox.Show(dialog, summary + "\n\n" + terms + "\n确认应用此项修改？",
                                title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

                            string error;
                            if (!plan.Apply(connected.Read, connected.Write, current, false, out error))
                            {
                                LogLine("职员转会未完成：" + summary.Replace("\n", "  ") + "。" + error);
                                MessageBox.Show(dialog, error, title);
                                return;
                            }

                            plan.SourceClubName = source.Name;
                            RecordGroupedChanges(plan.Edits, current, plan);
                            person.SetClub(plan.Destination.Name);
                            RefreshStaffAfterTransfer(person);
                            lblStatus.Text = person.DisplayName + " · " + plan.Destination.Name + " · " + plan.Role +
                                " 已更新。请重新打开 FM 的职员名单，必要时推进一天。";
                            dialog.Close();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(dialog, ex.Message.Replace("注册", "职员转会"), title);
                        }
                    };
                    dialog.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.Replace("注册", "职员转会"), title);
            }
        }

        void InvalidateStaffFinderResults()
        {
            staffFinderRevision++;
            gridStaffFind.Rows.Clear();
            lblStaffFind.Text = "可按职务、俱乐部和属性重新搜索。";
        }

        void RefreshStaffAfterTransfer(Staff person)
        {
            InvalidateStaffFinderResults();
            if (person != null && loadedStaff == person && !person.Matches(txtStaffSearch.Text))
                txtStaffSearch.Text = "UID " + person.Uid;
            FilterStaff();
        }
    }
}
