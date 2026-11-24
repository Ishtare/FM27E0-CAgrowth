// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// FM27€0 - free, open-source live editor for Football Manager 27 (based on FM24€0).
// Copyright (C) 2026 Anze Alexander
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version, with the additional terms in ADDITIONAL-TERMS.txt (keep the "Original by" credit; modified versions
// must not pass as the original). It is distributed WITHOUT ANY WARRANTY; see the LICENSE file for details.
//
// Edits players, contracts and club finances in a running fm.exe.
// The memory layout was mapped read-only from the game itself (see class FmLayout). Changes show in game after the next day.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Version information shown in the exe's Properties > Details
[assembly: AssemblyTitle("FM27€0 - modified build with CA growth (live editor for FM27)")]
[assembly: AssemblyDescription("Free, open-source live editor for Football Manager 27 (GPL-3.0-or-later). Modified build: CA growth tab added")]
[assembly: AssemblyProduct("FM27€0 - modified build")]
[assembly: AssemblyCompany("Anze Alexander")]
[assembly: AssemblyCopyright("Copyright (C) 2026 Anze Alexander")]
[assembly: AssemblyVersion(Fm24Editor.MainForm.Version + ".0")]
[assembly: AssemblyFileVersion(Fm24Editor.MainForm.Version + ".0")]
[assembly: AssemblyInformationalVersion(Fm24Editor.MainForm.Version)]

namespace Fm24Editor
{
    // Chinese labels for this build. Only what is shown is translated: the names that double as keys in the code
    // (offset tables like "Crossing|V", safety groups like "ability") stay English and are translated where printed.
    static partial class Zh
    {
        static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // tabs
            { "Players", "球员" }, { "Staff", "职员" }, { "Club finances", "俱乐部财务" }, { "Squad", "球队" },
            { "CA growth", "CA 成长" }, { "Find players", "查找球员" }, { "Find staff", "查找职员" },
            { "Prize money", "奖金" }, { "Freeze", "冻结" }, { "Change log", "变更记录" },
            // top bar
            { "Attach && scan", "挂接并扫描" },
            { "Start FM27, load your save, then click Attach & scan.", "先启动 FM27 并载入存档，然后点“挂接并扫描”。" },
            { "Currency...", "货币..." }, { "Reload", "重新加载" },
            // shared column / field names
            { "Name", "姓名" }, { "Age", "年龄" }, { "CA", "CA" }, { "PA", "PA" }, { "PA (cap)", "PA（上限）" },
            { "CA to PA", "距 PA" }, { "Added", "累计增加" }, { "Status", "状态" }, { "Note", "备注" },
            { "Value", "数值" }, { "Value (1-20)", "数值（1-20）" }, { "Raw (1-100)", "原始值（1-100）" },
            { "Attribute", "属性" }, { "Attribute 1", "属性一" }, { "Attribute 2", "属性二" }, { "Positions", "位置" },
            { "Position", "位置" }, { "Position / status", "位置 / 状态" }, { "Club", "俱乐部" }, { "Club:", "俱乐部：" },
            { "Nation", "国籍" }, { "Job", "职务" }, { "Wage ($/yr)", "工资（$/年）" }, { "Contract ends", "合同到期" },
            { "Place", "名次" }, { "Player", "球员" }, { "(none)", "（无）" }, { "Any", "不限" }, { "and", "和" },
            { "at least", "不低于" }, { "to", "到" }, { "Listed", "已挂牌" }, { "Free agents", "自由球员" },
            { "Transfer listed", "已挂牌" }, { "Unemployed only", "仅无业" },
            // buttons
            { "Apply changes", "应用修改" }, { "Search", "搜索" }, { "Release", "解约" }, { "Swap with...", "交换..." },
            { "Move to club...", "转到俱乐部..." }, { "Details && traits...", "详情与特质..." }, { "Bonuses && clauses...", "奖金与条款..." },
            { "Create player...", "新建球员..." }, { "Sponsors...", "赞助商..." }, { "Heal injury", "治疗伤病" },
            { "Heal all injuries", "治疗所有伤病" }, { "All visible to 20", "将全部已验证基础属性填至极值" },
            { "All attributes to 20", "所有属性拉到 20" }, { "Max all verified attributes", "已验证属性全拉满" },
            { "Max condition, sharpness && morale", "体能、状态与心情拉满" },
            { "Freeze fit && happy", "冻结体能与心情" }, { "Freeze squad fit, sharp && happy", "冻结全队体能、状态与心情" },
            { "Freeze money", "冻结资金" }, { "Stop all", "全部停止" }, { "Stop selected", "停止所选" },
            { "Undo selected", "撤销所选" }, { "Undo all changes from this session", "撤销本次会话的全部修改" },
            // search boxes
            { "Search player (at least 3 letters):", "搜索球员（至少 2 个字符）：" },
            { "Search staff by name or club (at least 3 letters). Type \"manager\" to edit yourself:", "按姓名或俱乐部搜职员（至少 2 个字符）。输入 manager 编辑你自己：" },
            { "Search staff by name or club (at least 3 letters).\nType \"manager\" to edit yourself:", "按姓名或俱乐部搜职员（至少 2 个字符）。\n输入 manager 编辑你自己：" },
            { "Search club:", "搜索俱乐部：" }, { "Search competition:", "搜索赛事：" },
            { "Contract ends by (year, 0 = any)", "合同在（年，0 为不限）" }, { "CA at least", "CA 不低于" },
            { "PA at least", "PA 不低于" },
            { "Set filters and click Search. Double-click a player to edit him. Up to 1,000 results, best potential first.",
              "设好条件点「搜索」，双击球员即可编辑。最多列出 1000 人，按潜力从高到低。" },
            { "Set filters and click Search. Double-click someone to edit them on the Staff tab. Up to 1,000 results, best first.",
              "设好条件点「搜索」，双击职员即可在「职员」页编辑。最多列出 1000 人，从高到低。" },
            { "Select one or more changes (Ctrl/Shift+click) to undo just those. Double-click undoes one.",
              "选中一条或几条（按住 Ctrl / Shift 点击）可以只撤销这些，双击撤销一条。" },
            { "当前没有冻结项目，可从球员、球队或俱乐部页面添加。",
              "当前没有冻结项。可以用球员页的「冻结体能与心情」、球队页的「冻结全队体能、状态与心情」或财务页的「冻结资金」。" },
            { "Changes are written immediately but FM shows them after the next in-game day. Back up your save before editing.",
              "修改会立刻写入，但 FM 要到下一个游戏日才显示。动手前请先备份存档。" },            // club tab
            { "Balance ($)", "结余（$）" }, { "Transfer budget remaining ($)", "剩余转会预算（$）" },
            { "Next season transfer budget ($)", "下赛季转会预算（$）" }, { "Wage budget ($ per week)", "周薪预算（$／周）" },
            { "Current wage spend", "当前周薪总额" }, { "Stadium capacity", "球场容量" },
            { "Used capacity (0 = whole stadium)", "已用座位（0 为整个球场）" }, { "Season ticket holders", "季票持有者" },
            { "Average ticket price ($)", "平均票价（$）" }, { "Club name (not longer than now)", "俱乐部全名（不长于原名）" },
            { "Short name (not longer than now)", "俱乐部简称（不长于原名）" },
            { "Training facilities (1-20)", "训练设施（1-20）" }, { "Youth facilities (1-20)", "青训设施（1-20）" },
            { "Junior coaching (1-20)", "青年队教练水平（1-20）" }, { "Youth recruitment (1-20)", "青训招募（1-20）" },
            { "Corporate facilities (1-20)", "商业设施（1-20）" }, { "Reputation (0-10000)", "声望（0-10000）" },
            // money rows: "{C}" is the currency symbol, replaced when the setting changes
            { "Balance ({C})", "结余（{C}）" }, { "Transfer budget remaining ({C})", "剩余转会预算（{C}）" },
            { "Next season transfer budget ({C})", "下赛季转会预算（{C}）" }, { "Wage budget ({C} per week)", "周薪预算（{C}／周）" },
            { "Average ticket price ({C})", "平均票价（{C}）" },
            // staff jobs and attributes
            { "Assistant Coach", "助理教练" }, { "Coach", "教练" }, { "Fitness Coach", "体能教练" },
            { "Goalkeeping Coach", "门将教练" }, { "Set Piece Coach", "定位球教练" }, { "Head Physio", "首席理疗师" },
            { "Physio", "理疗师" }, { "Doctor", "队医" }, { "Chief Doctor", "首席队医" },
            { "Chief Scout", "首席球探" }, { "Scout", "球探" }, { "Loan Manager", "租借主管" },
            { "Performance Analyst", "表现分析师" }, { "Recruitment Analyst", "招募分析师" },
            { "Head Performance Analyst", "首席表现分析师" }, { "Director of Football", "足球总监" },
            { "Sports Scientist", "运动科学家" }, { "Technical Director", "技术总监" }, { "Owner", "俱乐部所有者" },
            { "Head of Sports Science", "运动科学主管" }, { "Head of Youth Development", "青训主管" },
            { "Attacking", "进攻" }, { "Defending", "防守" }, { "Fitness", "体能" }, { "Mental", "心理" },
            { "Set Pieces", "定位球" }, { "Tactical", "战术" }, { "Technical", "技术" },
            { "Working With Youngsters", "培养年轻球员" }, { "GK Distribution", "门将出球" },
            { "GK Handling", "门将手控球" }, { "GK Shot Stopping", "门将扑救" },
            { "Determination", "意志力" }, { "Level of Discipline", "纪律要求" }, { "Motivating", "激励" },
            { "People Management", "人员管理" }, { "Judging Player Ability", "判断球员能力" },
            { "Judging Player Potential", "判断球员潜力" }, { "Judging Staff Ability", "判断职员能力" },
            { "Negotiating", "谈判" }, { "Tactical Knowledge", "战术知识" }, { "Analysing Data", "数据分析" },
            { "Physiotherapy", "理疗" }, { "Sports Science", "运动科学" },
            // personality
            { "Adaptability", "适应力" }, { "Ambition", "雄心" }, { "Loyalty", "忠诚" }, { "Pressure", "抗压" },
            { "Professionalism", "职业素养" }, { "Sportsmanship", "体育精神" }, { "Temperament", "情绪控制" },
            { "Controversy", "争议性" },
            // player attributes (the same words FM's Chinese UI uses)
            { "Crossing", "传中" }, { "Dribbling", "盘带" }, { "Finishing", "射门" }, { "Heading", "头球" },
            { "Long Shots", "远射" }, { "Marking", "抢截" }, { "Off The Ball", "无球跑动" }, { "Passing", "传球" },
            { "Penalty Taking", "点球" }, { "Tackling", "抢断" }, { "Vision", "视野" },
            { "Handling (GK)", "手控球（门将）" }, { "Aerial Reach (GK)", "制空范围（门将）" },
            { "Command Of Area (GK)", "禁区指挥（门将）" }, { "Communication (GK)", "沟通（门将）" },
            { "Kicking (GK)", "开球（门将）" }, { "Throwing (GK)", "手抛球（门将）" },
            { "Anticipation", "预判" }, { "Decisions", "决断" }, { "One On Ones (GK)", "一对一（门将）" },
            { "Positioning", "位置感" }, { "Reflexes (GK)", "反应（门将）" }, { "First Touch", "停球" },
            { "Technique", "技术" }, { "Left Foot", "左脚" }, { "Right Foot", "右脚" }, { "Flair", "天赋" },
            { "Corners", "角球" }, { "Teamwork", "团队合作" }, { "Work Rate", "工作投入" },
            { "Long Throws", "长掷界外球" }, { "Eccentricity (GK)", "冒进（门将）" },
            { "Rushing Out (GK)", "出击（门将）" }, { "Punching (GK)", "击球（门将）" },
            { "Acceleration", "爆发力" }, { "Free Kick Taking", "任意球" }, { "Strength", "强壮" },
            { "Stamina", "耐力" }, { "Pace", "速度" }, { "Jumping Reach", "弹跳" }, { "Leadership", "领导力" },
            { "Dirtiness (hidden)", "小动作（隐藏）" }, { "Balance", "平衡" }, { "Bravery", "勇敢" },
            { "Consistency (hidden)", "稳定性（隐藏）" }, { "Aggression", "侵略性" }, { "Agility", "灵活" },
            { "Important Matches (hidden)", "大赛发挥（隐藏）" },
            { "Injury Proneness (hidden)", "易伤倾向（隐藏）" },
            { "Versatility (hidden)", "多位置适应（隐藏）" }, { "Natural Fitness", "自然体能" },
            { "Composure", "冷静" }, { "Concentration", "专注" },
            // positions
            { "GK", "门将" }, { "SW", "清道夫" }, { "DL", "左后卫" }, { "DC", "中后卫" }, { "DR", "右后卫" },            { "DM", "后腰" }, { "ML", "左前卫" }, { "MC", "中场" }, { "MR", "右前卫" }, { "AML", "左翼" },
            { "AMC", "前腰" }, { "AMR", "右翼" }, { "ST", "前锋" }, { "WBL", "左翼卫" }, { "WBR", "右翼卫" },
            // status / caps / reputation rows
            { "Condition %", "体能 %" }, { "Match sharpness %", "比赛状态 %" }, { "Morale %", "士气 %" },
            { "International caps", "国家队出场" }, { "U21 caps", "U21 出场" },
            { "International goals", "国家队进球" }, { "U21 goals", "U21 进球" },
            { "Current reputation", "当前声望" }, { "Home reputation", "国内声望" }, { "World reputation", "世界声望" },
            // small numeric rows on the players and staff pages
            { "Squad status", "球队定位" }, { "Squad number (255 = none)", "球衣号码（255 为无）" },
            { "Jadedness (-500 = fully rested)", "疲劳度（-500 为完全恢复）" },
            { "Height cm", "身高（厘米）" }, { "Weight kg", "体重（公斤）" },
            // squad status names
            { "Star Player", "核心球员" }, { "Important Player", "重要球员" }, { "Regular Starter", "常规首发" },
            { "Squad Player", "一线队球员" }, { "Impact Sub", "轮换替补" }, { "Fringe Player", "边缘球员" },
            { "Emergency Backup", "应急替补" }, { "Breakthrough Prospect", "突破型新秀" },
            { "Future Prospect", "未来之星" }, { "Hot Prospect", "重点新秀" }, { "Youngster", "青年球员" },
            { "Regular", "常规球员" }, { "First-Choice Goalkeeper", "首选门将" }, { "Cup Goalkeeper", "杯赛门将" },
            { "Domestic Cup Goalkeeper", "国内杯赛门将" }, { "Continental Cup Goalkeeper", "洲际杯赛门将" },
            { "Backup", "替补" }, { "Surplus to Requirements", "不在计划内" }, { "Scouting Required", "待考察" },
            { "Out On Loan", "外租" },
        };

        // the safety groups as they appear in the attach report and in the Change log checks
        public static readonly Dictionary<string, string> Groups = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "core", "球员（属性、位置、姓名、资料）" }, { "status", "体能、比赛状态和士气" },
            { "ability", "CA/PA" }, { "personality", "性格" }, { "injury", "伤病" },
            { "contract", "工资、合同和条款" }, { "transfer", "转会状态" }, { "date", "挂牌（游戏日期）" },
            { "looks", "外貌" }, { "birthplace", "出生地" }, { "finance", "俱乐部资金" },
            { "facilities", "设施" }, { "reputation", "俱乐部声望" }, { "staff", "职员" },
            { "body", "身高和体重" }, { "playerrep", "球员声望" }, { "squadstatus", "球队定位" },
            { "staffrep", "职员声望" }, { "swap", "球员交换和转会" }, { "staffcontract", "职员合同" },
            { "squadnumber", "球衣号码" }, { "othernat", "其他国籍" }, { "stadium", "球场容量" },
            { "clubname", "俱乐部名称" }, { "jaded", "疲劳度" }, { "prize", "奖金" },
            { "caps", "国家队出场" }, { "tickets", "票价和季票" }, { "sponsor", "赞助合同" },
            { "staffmove", "职员转会" },
        };
        public static string G(string key) { string t; return Groups.TryGetValue(key, out t) ? t : key; }

        // exact lookup for control texts and short labels
        public static string Z(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t;
            return Map.TryGetValue(s, out t) || Extra.TryGetValue(s, out t) ? t : s;
        }

        // "Crossing|V" -> 传中, flags stay where they are
        public static string Attr(string entry)
        {
            if (entry == null) return null;
            int bar = entry.IndexOf('|');
            return Z(bar < 0 ? entry : entry.Substring(0, bar));
        }

        // translates the visible texts of a form or dialog after it has been built
        public static void Tree(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if ((c is Label || c is Button || c is CheckBox || c is RadioButton || c is GroupBox || c is TabPage || c is LinkLabel) && c.Text.Length > 0)
                    c.Text = Z(c.Text);
                var grid = c as DataGridView;
                if (grid != null) foreach (DataGridViewColumn col in grid.Columns) col.HeaderText = Z(col.HeaderText);
                var view = c as ListView;
                if (view != null) foreach (ColumnHeader h in view.Columns) h.Text = Z(h.Text);
                var combo = c as ComboBox;
                if (combo != null)
                    for (int i = 0; i < combo.Items.Count; i++)
                    {
                        var s = combo.Items[i] as string;
                        if (s != null) combo.Items[i] = Z(s);
                    }
                var checks = c as CheckedListBox;
                if (checks != null)
                    for (int i = 0; i < checks.Items.Count; i++)
                    {
                        var value = checks.Items[i] as string;
                        if (value != null) { var state = checks.GetItemCheckState(i); checks.Items[i] = Z(value); checks.SetItemCheckState(i, state); }
                    }
                Tree(c);
            }
        }
    }

    static class Native
    {
        public const uint PROCESS_VM_READ = 0x10, PROCESS_VM_WRITE = 0x20, PROCESS_VM_OPERATION = 0x08, PROCESS_QUERY_INFORMATION = 0x400;
        public const uint MEM_COMMIT = 0x1000, MEM_PRIVATE = 0x20000, MEM_IMAGE = 0x1000000, PAGE_WRITABLE = 0x04 | 0x08 | 0x40 | 0x80, PAGE_GUARD = 0x100, PAGE_NOACCESS = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        public struct MBI { public ulong BaseAddress, AllocationBase; public uint AllocationProtect, __a; public ulong RegionSize; public uint State, Protect, Type, __b; }

        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MBI info, IntPtr size);
    }

    class FmProcess : IDisposable
    {
        // Class pointers (RVA inside fm.exe) that identify each object type. FM24 Steam build.
        public static ulong PersonVtRva = 0x5A4EE78, ClubVtRva = 0x5A51F38, FinanceVtRva = 0x5A54048, ContractVtRva = 0x5667C88, StaffVtRva = 0x5A4C358, TeamVtRva = 0x5A78848, PlayerCoachVtRva = 0x5C7ECB8, CityVtRva = 0x5A51998;
        public static ulong CompactFinanceVtRva = 0;
        // the human manager's person object; his attributes use the staff layout, based ManagerAttrsBefore bytes before it
        public static ulong ManagerVtRva = 0x5A6A388;
        // loan contract: same layout as a contract (+0x08 person, +0x10 team, +0x55 squad number), reached via player object +0x348
        public static ulong LoanContractVtRva = 0x5C78638;
        public static ulong StadiumVtRva = 0x5A78248;
        // competitions: stage -> competition (name +0x40) and stage -> rules, whose +0x1C0 vector holds prize money by position
        public static ulong StageVtRva = 0x5A41AE8, CompetitionVtRva = 0x5A54A38, CompRulesVtRva = 0x5A42EB8;
        // FM27: this season's copy of a league's prize money (the rules hold the template for later seasons):
        // season object +SeasonComp -> competition, +SeasonStage -> stage object (SeasonStageVtRva) whose +StagePrizes vector
        // holds the paid places only. 0x1 = not used (FM24).
        public static ulong SeasonVtRva = 0x1, SeasonStageVtRva = 0x1;
        // Current game date (u32: day of year in the low 9 bits + time of day, then the year) - followed FM's header across a day change
        public static ulong CurrentDateRva = 0x631D5BC;

        public IntPtr H;
        public ulong Base, ImageSize;
        public int Pid;
        public string EngineModule = "";
        public string EngineFilePath;
        public bool VerifiedCurrentBuild;
        public long ScanRegionsSeen, ScanChunksRead, ScanChunksFailed, ScanBytesRead;
        public readonly Dictionary<int, long> ScanReadErrors = new Dictionary<int, long>();
        public int RegionQueryError;
        public ulong RegionQueryStoppedAt;
        public string Version = "";   // fm.exe product version, e.g. 24.4.2+2081827

        public static FmProcess Attach()
        {
            var procs = Process.GetProcessesByName("fm");
            string processIssue = ScanDiagnostics.ProcessIssue(procs.Length);
            if (processIssue != null) throw new Exception(processIssue);
            var p = procs[0];
            var fp = new FmProcess();
            fp.Pid = p.Id;
            // FM27 is a Unity build: fm.exe only starts it and the game engine lives in game_plugin.dll
            ProcessModule mod;
            try
            {
                mod = null;
                foreach (ProcessModule m in p.Modules) if (string.Equals(m.ModuleName, "game_plugin.dll", StringComparison.OrdinalIgnoreCase)) mod = m;
            }
            catch (System.ComponentModel.Win32Exception ex) { throw new Exception(Denied(ex.NativeErrorCode)); }
            if (mod == null) throw new Exception("已找到 fm.exe，但尚未找到 FM27 引擎 game_plugin.dll。请等待游戏启动并载入存档后重试；本次未使用启动程序代替游戏引擎进行扫描。");
            fp.EngineModule = mod.ModuleName;
            fp.Base = (ulong)(long)mod.BaseAddress;
            fp.ImageSize = (ulong)mod.ModuleMemorySize;
            try
            {
                fp.EngineFilePath=mod.FileName;
                fp.Version=FileVersionInfo.GetVersionInfo(fp.EngineFilePath).ProductVersion??"";
            }
            catch { }
            fp.H = Native.OpenProcess(Native.PROCESS_VM_READ | Native.PROCESS_VM_WRITE | Native.PROCESS_VM_OPERATION | Native.PROCESS_QUERY_INFORMATION, false, p.Id);
            if (fp.H == IntPtr.Zero) throw new Exception(Denied(Marshal.GetLastWin32Error()));
            return fp;
        }

        // Windows refuses (error 5) when FM runs with more rights than the editor, e.g. Steam or FM started as administrator
        static string Denied(int err)
        {
            if (err != 5) return "无法打开 fm.exe（错误代码 " + err + ").";
            return "Windows 拒绝访问 fm.exe。\n\n当 FM 或 Steam 以管理员身份运行时，编辑器也需要相同权限。请右键编辑器，选择「以管理员身份运行」。" +
                   "如果仍无法挂接，请检查安全软件的拦截记录，详见 ANTIVIRUS.md。";
        }

        public void Dispose() { if (H != IntPtr.Zero) Native.CloseHandle(H); H = IntPtr.Zero; }

        public byte[] Read(ulong addr, int n)
        {
            var b = new byte[n]; IntPtr got;
            if (!Native.ReadProcessMemory(H, (IntPtr)(long)addr, b, (IntPtr)n, out got) || (long)got != n) return null;
            return b;
        }

        public bool Write(ulong addr, byte[] data)
        {
            int error;
            return Write(addr, data, out error);
        }

        public bool Write(ulong addr, byte[] data, out int error)
        {
            IntPtr written;
            if (!Native.WriteProcessMemory(H, (IntPtr)(long)addr, data, (IntPtr)data.Length, out written))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }
            const int partialCopy = 299;
            error = (long)written == data.Length ? 0 : partialCopy;
            return error == 0;
        }

        public ulong ReadPtr(ulong addr) { var b = Read(addr, 8); return b == null ? 0 : BitConverter.ToUInt64(b, 0); }

        public int? ReadI32(ulong addr) { var b = Read(addr, 4); if (b == null) return null; return BitConverter.ToInt32(b, 0); }
        public uint? ReadU32(ulong addr) { var b = Read(addr, 4); if (b == null) return null; return BitConverter.ToUInt32(b, 0); }

        public uint? ReadGameDate()
        {
            if (!VerifiedCurrentBuild) return ReadU32(Base + CurrentDateRva);
            ulong address = CurrentBuildProfile.DateAddress(Read, Base);
            return address == 0 ? null : ReadU32(address);
        }

        // Name-table entry: first 8 bytes point to an FM string
        public string ReadNameEntry(ulong entry) { return entry < 0x10000 ? null : ReadLenString(ReadPtr(entry)); }

        // Person names: full name string, first, last and common name entries (entries may be empty); offsets in FmLayout
        public void ReadPersonNames(byte[] head, out string display, out string search)
        {
            PersonNameParts parts;ReadPersonNames(head,out display,out search,out parts);
        }
        public void ReadPersonNames(byte[] head,out string display,out string search,out PersonNameParts parts)
        { ReadPersonNames(head,ReadNameEntry,out display,out search,out parts); }
        public void ReadPersonNames(byte[] head,Func<ulong,string> entryName,out string display,out string search,out PersonNameParts parts)
        {
            string full = ReadLenString(BitConverter.ToUInt64(head, (int)FmLayout.FullName));
            string first = entryName(BitConverter.ToUInt64(head, (int)FmLayout.FirstNameEntry));
            string last = entryName(BitConverter.ToUInt64(head, (int)FmLayout.LastNameEntry));
            string common = entryName(BitConverter.ToUInt64(head, (int)FmLayout.CommonNameEntry));
            string fl = string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrEmpty(s)).ToArray());
            display = !string.IsNullOrEmpty(common) ? common : (fl.Length > 0 ? fl : full);
            search = string.Join(" | ", new[] { common, fl, full }.Where(s => !string.IsNullOrEmpty(s)).ToArray());
            parts=new PersonNameParts {First=first,Last=last,Common=common,Full=full,Search=search};
        }

        // FM strings: int32 byte length followed by UTF-8 text
        public string ReadLenString(ulong p)
        {
            if (p < 0x10000) return null;
            var len = ReadI32(p);
            if (len == null || len <= 0 || len > 256) return null;
            var b = Read(p + 4, len.Value);
            return b == null ? null : Encoding.UTF8.GetString(b);
        }

        // Committed, readable memory regions of the given type (MEM_PRIVATE = the game's heap, MEM_IMAGE = fm.exe itself)
        public List<KeyValuePair<ulong, ulong>> Regions(uint type, ulong from, ulong to, bool writableOnly = false)
        {
            var regions = new List<KeyValuePair<ulong, ulong>>();
            ulong a = from;
            Native.MBI m;
            var sz = (IntPtr)Marshal.SizeOf(typeof(Native.MBI));
            RegionQueryError = 0; RegionQueryStoppedAt = 0;
            while (a < to)
            {
                if (Native.VirtualQueryEx(H, (IntPtr)(long)a, out m, sz) == IntPtr.Zero)
                {
                    RegionQueryError = Marshal.GetLastWin32Error(); RegionQueryStoppedAt = a; break;
                }
                ulong next = m.BaseAddress + m.RegionSize;
                if (m.State == Native.MEM_COMMIT && m.Type == type && m.Protect != 0 && (m.Protect & (Native.PAGE_GUARD | Native.PAGE_NOACCESS)) == 0 &&
                    (!writableOnly || (m.Protect & Native.PAGE_WRITABLE) != 0))
                    regions.Add(new KeyValuePair<ulong, ulong>(m.BaseAddress, m.RegionSize));
                if (next <= a) break;
                a = next;
            }
            return regions;
        }

        // Reads all private memory in chunks: onChunk(buffer, valid length, address of buffer[0])
        public void ScanRegions(Action<byte[], int, ulong> onChunk, Action<double> progress)
        {
            var regions = Regions(Native.MEM_PRIVATE, 0x10000, 0x7FFFFFFF0000UL);
            ScanRegionsSeen=regions.Count;ScanChunksRead=ScanChunksFailed=ScanBytesRead=0;
            ScanReadErrors.Clear();
            ulong total = 0; foreach (var r in regions) total += r.Value;
            const int Chunk = 16 << 20;
            var buf = new byte[Chunk];
            ulong done = 0;
            foreach (var r in regions)
            {
                for (ulong off = 0; off < r.Value; off += Chunk)
                {
                    int n = (int)Math.Min((ulong)Chunk, r.Value - off);
                    IntPtr got;
                    if (Native.ReadProcessMemory(H, (IntPtr)(long)(r.Key + off), buf, (IntPtr)n, out got))
                    {ScanChunksRead++;ScanBytesRead+=(long)got;onChunk(buf,(int)(long)got,r.Key+off);}
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        ScanChunksFailed++; long count; ScanReadErrors.TryGetValue(error, out count); ScanReadErrors[error] = count + 1;
                    }
                    done += (ulong)n;
                    progress(total == 0 ? 1 : (double)done / total);
                }
            }
        }

        public string ScanSummary(string label)
        {
            string errors = ScanReadErrors.Count == 0 ? "无" : string.Join("、", ScanReadErrors.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(5).Select(x => x.Key + "×" + x.Value).ToArray());
            return label + "：可读区域 " + ScanRegionsSeen + "，成功块 " + ScanChunksRead + "，失败块 " + ScanChunksFailed + "，读取字节 " + ScanBytesRead + "；读取错误 " + errors +
                (RegionQueryStoppedAt == 0 ? "；区域查询完成。" : "；区域查询停止于 0x" + RegionQueryStoppedAt.ToString("X") + "，错误 " + RegionQueryError + "（超过进程地址上限也可能结束查询）。");
        }

        // Finds every 8-byte aligned qword in private memory equal to one of the targets.
        public void ScanQwords(ulong[] targets, Action<int, ulong> onHit, Action<double> progress)
        {
            ScanRegions((buf, len, at) =>
            {
                for (int i = 0; i + 8 <= len; i += 8)
                {
                    ulong v = BitConverter.ToUInt64(buf, i);
                    for (int t = 0; t < targets.Length; t++)
                        if (v == targets[t]) onHit(t, at + (ulong)i);
                }
            }, progress);
        }

        public bool InImage(ulong v) { return v - Base < ImageSize; }
    }

    class Player
    {
        public ulong Person;
        public ulong Extra;     // player-coaches: bytes of staff data between the player object and the person (0 for players)
        public string Name, Search;
        public string ChineseName, NameAliases, NameSource;
        public PersonNameParts NameParts;
        public int NameNationUid;
        public NameFallbackResult FallbackName;
        public bool? Female;
        string originalSource,searchOriginal,searchAliases,searchChinese;
        string[] originalKeys;
        public string[] OriginalNameKeys()
        {
            string source=Search??Name;
            if(originalKeys==null || !object.ReferenceEquals(source,originalSource))
            {
                originalSource=source;
                originalKeys=(source??"").Split('|').Select(PlayerNames.Normalize).Where(s=>s.Length>0).Distinct().ToArray();
            }
            return originalKeys;
        }
        string[] searchKeys;
        string[] SearchKeys()
        {
            string original=Search??Name;
            if(searchKeys==null || !object.ReferenceEquals(searchOriginal,original) || !object.ReferenceEquals(searchAliases,NameAliases) || !object.ReferenceEquals(searchChinese,ChineseName))
            {
                searchOriginal=original;searchAliases=NameAliases;searchChinese=ChineseName;
                searchKeys=string.IsNullOrEmpty(NameAliases) && string.IsNullOrEmpty(ChineseName) ? OriginalNameKeys() :
                    OriginalNameKeys().Concat((NameAliases+"|"+ChineseName).Split('|').Select(PlayerNames.Normalize).Where(s=>s.Length>0)).Distinct().ToArray();
            }
            return searchKeys;
        }
        public int MatchRank(string query) { int uid;if(PlayerNames.TryUid(query,out uid) && uid==Uid)return 0;string q=PlayerNames.QueryKey(query);var keys=SearchKeys();return keys.Any(s=>s==q)?0:keys.Any(s=>s.StartsWith(q,StringComparison.Ordinal))?1:2; }
        public string DisplayName { get { return ChineseName == null || ChineseName==Name ? Name : ChineseName + (FallbackName==null?"":FallbackName.Badge) + " / " + Name; } }
        public bool Matches(string query)
        {
            int uid;
            if(PlayerNames.TryUid(query,out uid))return uid==Uid;
            string q=PlayerNames.QueryKey(query);
            if(q.Length==0)return false;
            var keys=SearchKeys();
            if(q.Length==1 && q[0]>='\u3400' && q[0]<='\u9fff')return Array.IndexOf(keys,q)>=0;
            foreach(string key in keys)
                if(key.IndexOf(q,StringComparison.Ordinal)>=0)return true;
            return false;
        }
        public int Uid;
        public ulong Club;
        public GrowthMembership Roster;
        public DateTime? Born;
        // end of the player object; the *BeforePerson offsets count back from here (it is the person itself for a plain player)
        public ulong Tail { get { return Person - Extra; } }
        public ulong Block { get { return Tail - FmLayout.BlockBeforePerson; } }
        public override string ToString() { return DisplayName + (Born.HasValue ? "  (" + Born.Value.ToString("yyyy-MM-dd") + ")" : ""); }
    }

    class Staff
    {
        public ulong Addr;
        public ulong Pers;      // object with the names and personality: the staff object itself, or the human manager's person object
        public bool Manager;    // the human manager (you)
        public bool ContractOnly;   // verified staff identity and employment, with no validated attribute block
        public string Name, Search, ClubName;
        public PersonNameParts NameParts;
        public int NameNationUid;
        public Player NameView;
        public string DisplayName {get{return NameView!=null && NameView.Name==Name?NameView.DisplayName:Name;}}
        public int Uid;
        public bool Matches(string query) { int uid;if(PlayerNames.TryUid(query,out uid))return uid==Uid;if(NameView!=null && NameView.Name==Name){NameView.Search=Search;return NameView.Matches(query);}return PlayerNames.Matches(Search, query); }
        public void SetClub(string club)
        {
            if (ClubName == club) return;
            string suffix = " | " + ClubName;
            if (ClubName != null && Search != null && Search.EndsWith(suffix, StringComparison.Ordinal))
                Search = Search.Substring(0, Search.Length - suffix.Length);
            ClubName = club;
            if (club != null) Search = (Search ?? Name) + " | " + club;
        }
        public DateTime? Born;
        public override string ToString() { return DisplayName + (Manager ? "  （你，主教练）" : "") + (Born.HasValue ? "  (" + Born.Value.ToString("yyyy-MM-dd") + ")" : "") + (ClubName != null ? "  - " + ClubName : ""); }
    }

    class Club
    {
        public ulong Addr, Finance, Data, Team;
        public int Uid;
        public string Name, ShortName;
        public bool CompactFinance;
        public bool HasFullFinance {get{return Finance>=0x10000;}}
        public override string ToString() { return Name; }
    }

    static class FmLayout
    {
        // Player block sits directly in front of the person object: 15 position ratings (1-20) + 54 attributes (1-100, shown /5).
        public static ulong BlockBeforePerson = 0x70;
        public static int PosCount = 15, AttrCount = 54, AttrStart = 15;

        // Player object (person - 0x278) +0xF8 -> medical object whose first three qwords are the current-injury list
        // (begin, end, capacity of injury pointers). FM empties it (end = begin) when a player recovers.
        public static ulong MedicalBeforePerson = 0x278 - 0xF8;

        // Player object +0x130 -> team object; team +0x30 -> club object (current club)
        public static ulong TeamBeforePerson = 0x278 - 0x130, TeamClub = 0x30;

        // u16 status values before the person object. Verified with tag tests: condition and sharpness show after a day,
        // morale immediately. Morale words: ~1660 Fairly Poor, ~2685 Okay, ~3450 Good, ~3970 Very Good, ~4475 Excellent,
        // ~4700 Superb, 5000+ Perfect (game-wide max seen 5373).
        // Contract: player object +0x340 (= person + 0xC8). Wage int32 pounds per week at +0x18, expiry packed date at +0x40.
        // Verified: wage 10000 showed "$650K p/a", expiry 2028 showed "until 6/30/2028" (immediately).
        public static ulong ContractAfterPerson = 0x340 - 0x278, ContractWage = 0x18, ContractExpiry = 0x40;

        // Ability: int16 CA at person-0x78, int16 PA at person-0x76 (1-200; FM uses negative PA for random ranges).
        // Matched every player's level (correlation 0.85 with attributes); PA 200 raised a prospect's value 40K-425K -> 2.4M-6M.
        // Editing CA does not change attributes; FM grows attributes toward CA/PA over time through training.
        public static ulong AbilityBeforePerson = 0x78;

        // 8 personality bytes (1-20) at person+0x78. Ambition (coach report "extremely ambitious") and Professionalism
        // (Personality "Model Professional") verified by tag tests; the rest follow FM order and fit Ronaldo's known profile.
        public static ulong PersonalityAfterPerson = 0x78;
        public static readonly string[] Personality = { "Adaptability|", "Ambition|V", "Loyalty|", "Pressure|", "Professionalism|V", "Sportsmanship|", "Temperament|", "Controversy|" };

        // Person details (verified on screen, show immediately): name-table entries at +0x58 first, +0x60 last, +0x68 common;
        // birth date (u16 day of year, u16 year) at +0x44; nation pointer at +0x70 (nation name at nation+0x18);
        // 8 bytes of trait bits at +0xF0 (bit = byte*8+bit, names below).
        // Looks (FM's person setter, rsi = person+0xA0: Peth -0xF, Phcl -0xE, Phln -0xD, Pskt -0xC). Shown in the 3D match view.
        // Ranges seen over all 79k persons: ethnicity 0-10, hair colour 0-18, hair length 0-6 (1 = short for 96%), skin tone 0-19 (255 = not set).
        // Birthplace: person +0x88 -> city object (class fm+0x5A51998): name string at +0x18, nation object at +0x20.
        // Verified: Joe Ward Chelmsford -> Liverpool showed in Overview > Information after the next day.
        public static ulong BirthCity = 0x88, CityName = 0x18, CityNation = 0x20;
        // Other nationalities: the player object +0x2F8 points at an object whose first vector (+0x00/+0x08/+0x10) holds 16-byte
        // records {target ptr, u16 0, u8 kind, u8 relation, ...}: kind 8 = nation, relation 9 = other nationality (Musiala: England
        // and Nigeria; the same list carries favoured clubs and people, kinds 1/3/4). Changing a record's nation showed in FM's
        // Information page after the next day (Nigerian -> Brazilian).
        public static ulong PlayerRelations = 0x2F8, RelSize = 16;
        public const byte RelKindNation = 8, RelOtherNationality = 9;

        public static ulong Ethnicity = 0x91, HairColour = 0x92, HairLength = 0x93, SkinTone = 0x94;
        public static readonly string[] EthnicityNames = { "Northern European", "Mediterranean/Hispanic", "North African/Middle Eastern", "African/Caribbean",
            "Asian", "South East Asian", "Pacific Islander", "Native American", "Native Australian", "Mixed Race", "East Asian" };

        // bytes read from the start of a person object for names, nation and birth date
        public static int HeadLen = 0x100;
        // Feature groups a profile has mapped (empty = all); the others stay read-only. Calibrate 0 = never re-find class pointers.
        public static string[] Groups = new string[0];
        public static int Calibrate = 1;
        // 0 = never search for the game date again, 1 = search fm's data for date values with time-of-day bits 0 (FM24),
        // 2 = search fm's code for the date (FM27 keeps time bits in it, so its data holds too many look-alikes)
        public static int DateCalibrate = 1;

        public static ulong Uid = 0x0C, FullName = 0x48, FirstNameEntry = 0x58, LastNameEntry = 0x60, CommonNameEntry = 0x68, BirthDate = 0x44, Nation = 0x70, NationName = 0x18, Traits = 0xF0;
        public static readonly Dictionary<int, string> TraitNames = new Dictionary<int, string> {
            { 0, "Runs With Ball Down Left" },
            { 1, "Runs With Ball Down Right" },
            { 2, "Runs With Ball Down Center" },
            { 3, "Gets Into Opposition Area" },
            { 4, "Moves Into Channels" },
            { 5, "Gets Forward Whenever Possible" },
            { 6, "Plays Short Simple Passes" },
            { 7, "Tries Killer Balls Often" },
            { 8, "Shoots From Distance" },
            { 9, "Shoots With Power" },
            { 10, "Places Shots" },
            { 11, "Curls Ball" },
            { 12, "Likes to Round Keeper" },
            { 13, "Likes to Try To Break Offside Trap" },
            { 14, "Uses Outside Of Foot" },
            { 15, "Marks Opponent Tightly" },
            { 16, "Winds Up Opponents" },
            { 17, "Argues With Officials" },
            { 18, "Plays with Back To Goal" },
            { 19, "Comes Deep To Get Ball" },
            { 20, "Plays One-Twos" },
            { 21, "Likes To Lob Keeper" },
            { 22, "Dictates Tempo" },
            { 23, "Attempts Overhead Kicks" },
            { 24, "Looks For Pass Rather Than Attempting To Score" },
            { 25, "Plays No Through Balls" },
            { 26, "Stops Play" },
            { 27, "Knocks Ball Past Opponent" },
            { 28, "Moves Ball To Right Foot Before Dribble Attempt" },
            { 29, "Moves Ball To Left Foot Before Dribble Attempt" },
            { 30, "Dwells On Ball" },
            { 31, "Arrives Late In Opponents' Area" },
            { 32, "Tries To Play Way Out Of Trouble" },
            { 33, "Stays Back At All Times" },
            { 34, "Avoids Using Weaker Foot" },
            { 35, "Tries Tricks" },
            { 36, "Tries Long Range Free Kicks" },
            { 37, "Dives Into Tackles" },
            { 38, "Does Not Dive Into Tackles" },
            { 39, "Cuts Inside From Both Wings" },
            { 40, "Hugs Line" },
            { 41, "Gets Crowd Going" },
            { 42, "Tries First Time Shots" },
            { 43, "Tries Long Range Passes" },
            { 44, "Likes Ball Played Into Feet" },
            { 45, "Hits Free Kick With Power" },
            { 46, "Likes To Beat Man Repeatedly" },
            { 47, "Likes To Switch Ball To Other Flank" },
            { 50, "Possesses Long Flat Throw" },
            { 51, "Runs With Ball Often" },
            { 52, "Runs With Ball Rarely" },
            { 54, "Does not Move Into Channels" },
            { 55, "Uses Long Throw To Start Counter Attacks" },
            { 56, "Refrains From Taking Long Shots" },
            { 57, "Cuts Inside From Left Wing" },
            { 58, "Cuts Inside From Right Wing" },
            { 59, "Crosses Early" },
            { 60, "Brings Ball Out of Defense" },
            { 63, "Plays Ball With Feet" },
        };

        // Attributes where a LOW value is good; "max" buttons set these to 1
        public static bool LowerIsBetter(string name) { return name.StartsWith("Injury Proneness") || name.StartsWith("Dirtiness"); }

        // Staff (non-player) attributes: name | offset BEFORE the staff object | scale. Scale 5 = stored 1-100 and shown /5 like
        // player attributes, scale 1 = stored 1-20. All tag-tested on screen (Determination matched by value only).
        // Staff objects share the person layout: names +0x58/+0x60/+0x68, birth date +0x44, personality +0x78.
        public static ulong StaffBlockBefore = 0xE0;
        public static int StaffBlockLen = 0x34;
        public static string[] StaffAttrs = {
            "Attacking|BE|5", "Defending|BD|5", "Fitness|BC|5", "Mental|BB|5", "Set Pieces|AD|5", "Tactical|B9|5", "Technical|BA|5",
            "Working With Youngsters|D4|1", "GK Distribution|B6|5", "GK Handling|B7|5", "GK Shot Stopping|C5|5",
            "Determination|D3|5", "Level of Discipline|DC|1", "Motivating|C1|5", "People Management|C2|5",
            "Judging Player Ability|C4|5", "Judging Player Potential|C3|5", "Judging Staff Ability|AE|5", "Negotiating|AF|5", "Tactical Knowledge|BF|5",
            "Analysing Data|B4|5", "Physiotherapy|C0|5", "Sports Science|B1|5" };

        public static bool ValidStaffBlock(byte[] b) { return ValidStaffBlock(b, 0); }

        // Same check on a staff block starting at b[off] (used by the calibration scan on raw memory)
        public static bool ValidStaffBlock(byte[] b, int off)
        {
            if (b == null || off < 0 || b.Length < off + StaffBlockLen) return false;
            if (staffIdx == null || staffIdxFor != StaffAttrs)
            {
                staffIdx = StaffAttrs.Select(a => { var p = a.Split('|'); return new[] { (int)(StaffBlockBefore - Convert.ToUInt64(p[1], 16)), p[2] == "5" ? 100 : 20 }; }).ToArray();
                staffIdxFor = StaffAttrs;
            }
            foreach (var x in staffIdx) { int v = b[off + x[0]]; if (v < 1 || v > x[1]) return false; }
            return true;
        }
        static int[][] staffIdx; static string[] staffIdxFor;

        public static readonly string[] StatusNames = { "Condition %", "Match sharpness %", "Morale %" };
        public static ulong[] StatusBeforePerson = { 0x80, 0x84, 0x1A };
        public static int[] StatusMax = { 10000, 10000, 5300 };
        public static int[] StatusSize = { 2, 2, 2 };
        // Jadedness: signed int16 at person-0x82 (player setter tag Pjad). Rested players sit at -500, regular starters
        // around 0-300; 3000 made FM show "Exhausted and in need of a long rest" straight away.
        public static ulong JadedBeforePerson = 0x82; public static int JadedRested = -500;
        // International record: four bytes at person +0x164 = caps, U21 caps, goals, U21 goals (Hourihane 36 / 8 / 1 / 0, Ronaldo
        // 200 caps 123 goals). FM's profile shows a change at once and it is kept in the save (tested 50 caps / 5 goals).
        public static ulong Caps = 0x164;
        // where each of CapsNames sits from Caps, and its size in bytes (FM24: four bytes in a row; FM27: u16 caps, u16 goals, then the U21 bytes)
        public static int[] CapsAt = { 0, 1, 2, 3 }, CapsSize = { 1, 1, 1, 1 };
        public static readonly string[] CapsNames = { "International caps", "U21 caps", "International goals", "U21 goals" };

        public static readonly string[] Positions = { "GK", "SW", "DL", "DC", "DR", "DM", "ML", "MC", "MR", "AML", "AMC", "AMR", "ST", "WBL", "WBR" };

        // Internal storage order. V = verified on screen (3 players + tag tests); the other three hidden ones follow FM's database order.
        public static readonly string[] Attrs = {
            "Crossing|V", "Dribbling|V", "Finishing|V", "Heading|V", "Long Shots|V", "Marking|V", "Off The Ball|V", "Passing|V", "Penalty Taking|V", "Tackling|V", "Vision|V",
            "Handling (GK)|V", "Aerial Reach (GK)|V", "Command Of Area (GK)|V", "Communication (GK)|V", "Kicking (GK)|V", "Throwing (GK)|V",
            "Anticipation|V", "Decisions|V", "One On Ones (GK)|V", "Positioning|V", "Reflexes (GK)|V", "First Touch|V", "Technique|V", "Left Foot|V", "Right Foot|V",
            "Flair|V", "Corners|V", "Teamwork|V", "Work Rate|V", "Long Throws|V", "Eccentricity (GK)|V", "Rushing Out (GK)|V", "Punching (GK)|V",
            "Acceleration|V", "Free Kick Taking|V", "Strength|V", "Stamina|V", "Pace|V", "Jumping Reach|V", "Leadership|V", "Dirtiness (hidden)|", "Balance|V", "Bravery|V",
            "Consistency (hidden)|V", "Aggression|V", "Agility|V", "Important Matches (hidden)|V", "Injury Proneness (hidden)|V", "Versatility (hidden)|", "Natural Fitness|V",
            "Determination|V", "Composure|V", "Concentration|V" };

        // Club object -> name pointers and finance object; finance object -> int32 pounds
        public static ulong ClubName = 0xC0, ClubShortName = 0xC8, ClubFinance = 0x150, ClubData = 0x100;

        // Facilities, 1-20 (marker-tested on Derby's Club Info > Facilities screen). Each has its byte in the club data
        // object (club +0x100) and/or the finance object; the screen reads the finance copy where there is one, FM's own setter
        // writes both, so we do too. 0 = no copy there. Youth Level is recalculated by FM and is not editable.
        public static readonly string[] FacilityNames = { "Training facilities", "Youth facilities", "Junior coaching", "Youth recruitment", "Corporate facilities" };
        public static ulong[] FacilityData = { 0x120, 0x12B, 0x12C, 0x12D, 0 };
        public static ulong[] FacilityFin = { 0, 0, 0x8B2, 0x8B3, 0x8B5 };

        // Reputation: u16 0-10000 on the club's FIRST TEAM object (club +0x18 -> list of teams, [0] = first team; team +0x30 -> club).
        // Marker-tested: Derby 5398 = "National" 2.5 stars, 9500 = "Worldwide" 5 stars (General tab, after the next day).
        // Man City 9400, Barcelona 8950, Oxford 4800. (Club data +0xFC looks similar but the screen does not use it.)
        public static ulong ClubTeams = 0x18, TeamClub2 = 0x30, TeamReputation = 0xA8;
        // Stadium: first team +0x78 -> stadium (name +0x38): +0x6C capacity, +0x70 seats, +0x74 used capacity. Tested on Pride Park:
        // capacity 45,000 and used 40,000 showed in Club Info after the next day ("45,000 all-seater (40,000 capacity used...)").
        public static ulong TeamStadium = 0x78, StadiumCapacity = 0x6C, StadiumSeats = 0x70, StadiumUsed = 0x74, StadiumName = 0x38;
        // Tickets: season ticket holders = int32 at club data +0xC0, average match ticket price = float (pounds) at finance +0x870.
        // Club Info shows both at once; FM derives the average season ticket price from the match price (about x21.6).
        // Checked on Derby (20,000 / £31.05 = $39) and Barcelona (50,000 / £55.74 = $70).
        public static ulong DataSeasonTickets = 0xC0, FinTicketPrice = 0x870;
        // Sponsors: club +0x130 -> {begin, end, cap} of pointers to 0x18-byte deals: +0 start and +4 end date (u16 day | flags,
        // u16 year), +8 total value and +0xC value per season (int32 pounds), +0x12 type. Finances > Sponsors and Others shows a
        // change at once (Derby main kit £487,043 -> £1,000,000 showed $1.2M). Expired deals keep their total with 0 per season.
        public static ulong ClubSponsors = 0x130, DealEnd = 4, DealTotal = 8, DealSeason = 0xC, DealType = 0x12;
        public static readonly Dictionary<int, string> SponsorTypes = new Dictionary<int, string> { { 1, "Main Kit" }, { 4, "General" }, { 11, "Back of Jersey" }, { 13, "Training Kit" }, { 18, "Other (not listed by FM)" }, { 19, "Shirt Sleeve" }, { 22, "Kit Manufacturer" } };
        // Prize money: stage +0x70 -> competition (+0x40 name), stage +0xC8 -> rules, rules +0x1C0 = std::vector<int32> pounds by
        // finishing position (league tables) or per round/group place (cups). Found by bisecting every 25,000 in memory against the
        // League One rules screen (1st $31K); 1st and 2nd both verified on that screen.
        public static ulong SeasonComp = 0x70, SeasonStage = 0xF0, StagePrizes = 0x1E0;
        public static int PrizeTablesMin = 5;   // safety check: prize tables a scan must find
        public static ulong StageTag = 0x48, StageCompetition = 0x70, StageRules = 0xC8, RulesPrizes = 0x1C0, CompetitionName = 0x40;
        // FM27: a first team points at its league competition (used by calibration to find the competition class again)
        public static ulong TeamCompetition = 0x60;
        // Swapping players: the team keeps its players in a vector (+0x38 begin, +0x40 end, +0x48 capacity) of player objects,
        // which start PlayerObjBeforePerson bytes before the person. The player object points at the team he plays for
        // (+0x130) and a second team (+0x138, often the same); the contract +0x10 points at the owning club's team.
        // Moving one player (tested in game: Sheehan Bolton -> Derby lasted a day change, a match day, a save and a reload) needs a
        // free place in the new list; a swap exchanges the two entries, so neither list changes size.
        public static ulong TeamPlayers = 0x38, PlayerObjBeforePerson = 0x278, PlayerTeam = 0x130, PlayerTeam2 = 0x138;
        // Player-coaches (FM builds one for many senior players when a save is loaded, ~7,800 in a standard game) keep the same
        // player object, then 0xF0 bytes of staff attributes, then the person (player-coach class): the person is 0x368 after
        // the object instead of 0x278. Player fields count back from the end of the object, person fields forward from the person
        // (relations +0x80 and the loan slot +0xD0 are person fields). Verified on Hourihane, Waghorn and five more at Derby.
        public static ulong PlayerCoachExtra = 0xF0;

        // Transfer status, found by diffing FM's own Transfer Status panel: contract +0x48 = date transfer-listed
        // (empty date 1 Jan 1900 when not listed), +0x4F flags (bit 0 transfer listed, bit 1 loan listed, other bits are FM's own),
        // +0x50 listing code (4 = listed by the club, 0 = none). Asking price = int32 pounds at player object +0x1D4
        // (= person - 0xA4), -1 = not set. FM writes and resets exactly these when you list/unlist in game.
        public static ulong ContractListedDate = 0x48, ContractListFlags = 0x4F, ContractListCode = 0x50, AskingBeforePerson = 0xA4;
        public static uint EmptyDate = 0x076C0001;

        // Height (cm) and weight (kg): u16 at person -0x12A and -0x12C (player setter tags Phei/Pwei store to r14-0x12A/-0x12C,
        // r14 = person). Read back for Ronaldo 187/82 and Joe Ward 175/68, matching FM's 5'9" / 149 lbs for Ward.
        public static ulong HeightBeforePerson = 0x12A, WeightBeforePerson = 0x12C;   // weight 0 = the game has none (FM27)

        // Player reputation 0-10000, int16 before the person object: current -0x7C, home -0x7E, world -0x7A (the player setter
        // reads PCRP from r14-0x7C, PHRP from r14-0x7E, PWRP from r14-0x7A). Ward: current 5368, home 5350, world 2759 = "National".
        // Confirmed in game: height 169 -> 200 showed 6'7" and all three at 9500 showed "Worldwide", both after the next day.
        public static ulong[] RepBeforePerson = { 0x7C, 0x7E, 0x7A };
        public static readonly string[] RepNames = { "Current reputation", "Home reputation", "World reputation" };

        // Squad status (FM: agreed playing time) = byte at contract +0x4C. Found by matching 10 Derby players' "Agreed" line,
        // then every value 0-24 written on Joe Ward and read off his profile (shows at once). +0x4E is usually an equal copy and
        // +0x4D the previous status; FM's screen reads +0x4C only, so only that is written. 4/6, 7/8, 9/21 and 16/19 show the
        // same name; 0 "-", 23 "Scouting Required" and 24 "Out On Loan" are FM's own and not offered.
        public static ulong ContractSquadStatus = 0x4C;
        // Squad number: byte at contract +0x55, 255 = no number (from the player setter, tag Psnu: FM looks up the contract for the
        // team and stores the number there). Matched FM's Squad Numbers screen for every Derby player who is not on loan; a loan
        // player's number lives with the loan, not in this (parent club) contract.
        // bonuses and clauses: std::vector of 8-byte records in the contract
        public static ulong ContractClauses = 0x60;
        public static ulong ContractSquadNumber = 0x55, PlayerLoanContract = 0x348;
        public static readonly Dictionary<int, string> SquadStatusNames = new Dictionary<int, string> {
            { 1, "Star Player" }, { 2, "Important Player" }, { 3, "Regular Starter" }, { 4, "Squad Player" }, { 5, "Impact Sub" }, { 6, "Squad Player" },
            { 7, "Fringe Player" }, { 8, "Fringe Player" }, { 9, "Emergency Backup" }, { 10, "Breakthrough Prospect" }, { 11, "Future Prospect" },
            { 12, "Hot Prospect" }, { 13, "Youngster" }, { 14, "Regular" }, { 15, "First-Choice Goalkeeper" }, { 16, "Cup Goalkeeper" },
            { 17, "Domestic Cup Goalkeeper" }, { 18, "Continental Cup Goalkeeper" }, { 19, "Cup Goalkeeper" }, { 20, "Backup" },
            { 21, "Emergency Backup" }, { 22, "Surplus to Requirements" }, { 23, "Scouting Required" }, { 24, "Out On Loan" } };
        public static readonly int[] SquadStatusOffered = { 1, 2, 3, 4, 5, 7, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 20, 22 };

        // Staff employer: staff +0xC8 -> contract, contract +0x10 -> team, team +0x30 -> club (same chain as players' contracts)
        public static ulong StaffContract = 0xC8, ContractTeam = 0x10;
        // FM27 department vectors belong to the club itself: medical, coaching, and recruitment.
        // A roster pointer addresses the staff object StaffListBack bytes before the contract's owner.
        public static ulong[] ClubStaffLists = new ulong[0];
        public static ulong StaffListBack = 0x100, ContractTeam2 = 0x18;
        public static ulong ContractJob = 0x1C;
        // Human manager: attributes = staff block based at person - 0x358 (screen-tested: Attacking changed after a time step)
        public static ulong ManagerAttrsBefore = 0x358;
        // Staff and manager reputation, int16 0-10000 before the staff object: current, home, world. World sets the stars on the
        // profile (screen-tested: Barker 1544 -> 9000 = 1 -> 5 stars, manager 5043 -> 2000 = 3 -> 1.5 stars, after the next day).
        public static ulong[] StaffRepBefore = { 0x28, 0x26, 0x24 };
        public static ulong FinBalance = 0x14, FinTransfer = 0x7CC, FinTransferNext = 0x7D4, FinWageBudget = 0x810, FinWageSpend = 0x81C;

        public static bool ValidBlock(byte[] b) { return ValidBlock(b, 0); }

        public static bool ValidBlock(byte[] b, int off)
        {
            if (b == null || off < 0 || b.Length < off + Math.Max(PosCount, AttrStart + AttrCount)) return false;
            bool has20 = false;
            for (int k = 0; k < PosCount; k++) { byte v = b[off + k]; if (v < 1 || v > 20) return false; if (v == 20) has20 = true; }
            for (int k = 0; k < AttrCount; k++) { byte v = b[off + AttrStart + k]; if (v < 1 || v > 100) return false; }
            return has20;
        }
    }

    // Game-version profile. Every FM-specific number is a static field of FmProcess or FmLayout, and the values written in
    // the code are FM24's (the built-in profile). A text file in a "profiles" folder next to the exe can override any of them
    // for another FM version: one "Field = value" per line (field names as in the code, numbers hex 0x.. or decimal, arrays
    // as comma lists, # starts a comment), plus
    //   name = FM23
    //   match = 23.              fm.exe product versions the profile is for (prefix)
    //   tested = 23.5.1+12345    exact versions it was tested on (comma list)
    // Run the exe with --dump-profile to write the built-in profile as a starting point.
    static class Profile
    {
        public const string BuiltInName = "FM27 built-in profile", BuiltInMatch = "27.";
        static readonly string[] BuiltInTested = { "27.0.0+2486695" };
        public static string Name = BuiltInName, Match = BuiltInMatch, Source;   // Source = profile file, null when built in
        public static string[] Tested = BuiltInTested;
        static Dictionary<FieldInfo, object> builtIn;

        public static IEnumerable<FieldInfo> Fields()
        {
            var types = new[] { typeof(ulong), typeof(uint), typeof(int), typeof(ulong[]), typeof(int[]), typeof(string[]) };
            foreach (var t in new[] { typeof(FmProcess), typeof(FmLayout) })
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (!f.IsInitOnly && !f.IsLiteral && types.Contains(f.FieldType)) yield return f;
        }

        static object Copy(object v) { var a = v as Array; return a != null ? a.Clone() : v; }

        // FM27 (game_plugin.dll), mapped on the demo. Only the listed groups are on; everything else stays read-only until mapped.
        // vs FM24: attribute block person-0x138 (same 15 + 54 layout), names/nation/personality 8 bytes earlier, birth date +0x88,
        // traits +0xC0, CA/PA person-0x24 with condition, sharpness, jadedness and reputations next to it as in FM24.
        // Class pointers not found yet are 0x1 so they match nothing. Calibrate 1: after a patch the class pointers are found again
        // (tested by giving all 11 of them wrong values); the game date and the prize money classes are not, they go read-only.
        const string Fm27 = @"
name = FM27 built-in profile
match = 27.
tested = 27.0.0+2486695
Groups = core, personality, ability, contract, transfer, date, squadstatus, squadnumber, othernat, finance, facilities, reputation, staff, staffcontract, staffrep, playerrep, caps, body, looks, birthplace, stadium, clubname, tickets, sponsor, swap, prize, staffmove
Calibrate = 1
PersonVtRva = 0x4D27758
ClubVtRva = 0x4CDA1F8
FinanceVtRva = 0x4CF0CC8
ContractVtRva = 0x4B00AE8
StaffVtRva = 0x4D8BE78
TeamVtRva = 0x4CE1108
PlayerCoachVtRva = 0x4F75F38
CityVtRva = 0x4DE29C8
ManagerVtRva = 0x4CC08D8
LoanContractVtRva = 0x4DC91F8
StadiumVtRva = 0x4DEDC18
StageVtRva = 0x4CDFAD8
CompetitionVtRva = 0x4CDFAD8
CompRulesVtRva = 0x4CE1FC8
StageCompetition = 0
StageRules = 0xB0
RulesPrizes = 0x648
CompetitionName = 0x40
SeasonVtRva = 0x4DCF148
SeasonStageVtRva = 0x4DD1718
PrizeTablesMin = 3
CurrentDateRva = 0x56AD378
BlockBeforePerson = 0x138
FullName = 0x40
FirstNameEntry = 0x50
LastNameEntry = 0x58
CommonNameEntry = 0x60
Nation = 0x68
PersonalityAfterPerson = 0x70
BirthCity = 0x80
BirthDate = 0x88
Ethnicity = 0x8D
HairColour = 0x8E
HairLength = 0x8F
SkinTone = 0x90
Traits = 0xC0
AbilityBeforePerson = 0x24
RepBeforePerson = 0x28, 0x2A, 0x26
JadedBeforePerson = 0x2E
AskingBeforePerson = 0x50
HeightBeforePerson = 0x5A
WeightBeforePerson = 0
Caps = 0x134
CapsAt = 0, 4, 2, 5
CapsSize = 2, 1, 2, 1
PlayerObjBeforePerson = 0x288
PlayerCoachExtra = 0xF8
DealType = 0x14
FacilityData = 0x120, 0x12B, 0x12C, 0x12D, 0
TeamBeforePerson = 0x158
MedicalBeforePerson = 0x168
PlayerRelations = 0x300
PlayerLoanContract = 0x338
ContractAfterPerson = 0xA8
ContractWage = 0x20
ContractJob = 0x26
ContractExpiry = 0x48
ContractListedDate = 0x50
ContractSquadStatus = 0x54
ContractListFlags = 0x57
ContractListCode = 0x58
ContractSquadNumber = 0x5D
ContractClauses = 0x68
StaffContract = 0xA8
StaffBlockBefore = 0xF0
StaffAttrs = Attacking|CE|5, Defending|CD|5, Fitness|CC|5, Possession|CB|5, Set Pieces|BD|5, Tactical|C9|5, Technical|CA|5, Working With Youngsters|E4|1, GK Distribution|C6|5, GK Handling|C7|5, GK Shot Stopping|D5|5, Determination|E3|5, Authority|EC|1, Motivating|D1|5, People Management|D2|5, Judging Player Ability|D4|5, Judging Player Potential|D3|5, Judging Staff Ability|BE|5, Negotiating|BF|5, Tactical Knowledge|CF|5, Analysing Data|C4|5, Physiotherapy|D0|5, Sports Science|C1|5
StaffRepBefore = 0x2A, 0x2C, 0x28
ManagerAttrsBefore = 0x340
FinBalance = 0xC
FinTransfer = 0x7C4
FinTransferNext = 0x7CC
FinWageBudget = 0x808
FinWageSpend = 0x814
FacilityFin = 0, 0, 0x8A6, 0x8A7, 0x8A9
FinTicketPrice = 0x864
StadiumCapacity = 0x7C
StadiumSeats = 0x80
StadiumUsed = 0x84
StadiumName = 0x40
DateCalibrate = 2
ClubStaffLists = 0x60, 0x78, 0x90
";

        // Resets to the built-in values (FM27's on top for a 27.x game), then applies the profile file whose "match" fits this
        // version best (a file wins over the built-in profile on an equal match). Throws with the file name on a bad profile file.
        // The Xbox / PC Game Pass build reports the same version but is a different game_plugin.dll: its class pointers and
        // game date differ, the field offsets are the same. Told apart by the module size; applied on top of Fm27.
        const ulong XboxImageSize = 0x19436000;
        const string Fm27Xbox = @"
name = FM27 built-in profile (Xbox / Game Pass)
PersonVtRva = 0x4CD4848
ClubVtRva = 0x4C87B68
FinanceVtRva = 0x4C9DD78
ContractVtRva = 0x4ABE5F8
StaffVtRva = 0x4D393C8
TeamVtRva = 0x4C8EA78
PlayerCoachVtRva = 0x4F1F258
CityVtRva = 0x4D8D1E8
ManagerVtRva = 0x4C6E268
LoanContractVtRva = 0x4D76AF8
StadiumVtRva = 0x4D950D8
StageVtRva = 0x4C8D438
CompetitionVtRva = 0x4C8D438
CompRulesVtRva = 0x4C8F958
SeasonVtRva = 0x4D7A7B8
SeasonStageVtRva = 0x4D7CA18
CurrentDateRva = 0x56237C8
";

        public static void Load(string version, string exeDir, ulong imageSize = 0)
        {
            if (builtIn == null) builtIn = Fields().ToDictionary(f => f, f => Copy(f.GetValue(null)));
            foreach (var kv in builtIn) kv.Key.SetValue(null, Copy(kv.Value));
            CurrentBuildProfile.Reset();
            Name = BuiltInName; Match = BuiltInMatch; Tested = BuiltInTested; Source = null;
            // FM27 is the only built-in profile: the field initialisers hold FM24's numbers and the FM27 text goes on top
            Apply(Parse(Fm27.Split('\n')), null); Source = null;
            if (imageSize == XboxImageSize) { var t = Tested; Apply(Parse(Fm27Xbox.Split('\n')), null); Source = null; Tested = t; }
            int best = version.StartsWith(Match) ? Match.Length : 0; string bestFile = null;
            var dir = Path.Combine(exeDir, "profiles");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.txt").OrderBy(x => x))
                {
                    string m;
                    if (Parse(File.ReadAllLines(f)).TryGetValue("match", out m) && m.Length > 0 && version.StartsWith(m) && m.Length >= best) { best = m.Length; bestFile = f; }
                }
            if (bestFile != null) Apply(Parse(File.ReadAllLines(bestFile)), bestFile);
        }

        public static void Load(FmProcess fm, string exeDir)
        {
            Load(fm.Version, exeDir, fm.ImageSize);
            fm.VerifiedCurrentBuild = CurrentBuildProfile.Supported(fm.Version, fm.ImageSize, fm.Read, fm.Base);
            if(fm.VerifiedCurrentBuild)
            {
                CurrentBuildProfile.Select(fm.Version,fm.ImageSize);
                if(Source==null)
                {
                    CurrentBuildProfile.Apply(CurrentBuildProfile.Active);
                    Name="FM27 "+CurrentBuildProfile.Active.Channel+" "+fm.Version+"（按构建逐项检查）";
                    Tested=Tested.Concat(new[]{fm.Version}).Distinct().ToArray();
                }
            }
        }

        public static bool Matches(string version) { return version.StartsWith(Match); }
        public static bool IsTested(string version) { return Tested.Contains(version); }

        static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            var d = new Dictionary<string, string>();
            foreach (var raw in lines)
            {
                var line = raw; int h = line.IndexOf('#'); if (h >= 0) line = line.Substring(0, h);
                int i = line.IndexOf('='); if (i < 0) continue;
                d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            return d;
        }

        // file = null for a built-in profile
        static void Apply(Dictionary<string, string> profile, string file)
        {
            var fields = Fields().ToDictionary(f => f.Name);
            string label = file == null ? "built-in" : Path.GetFileName(file);
            Name = file == null ? "built-in" : Path.GetFileNameWithoutExtension(file); Source = file; Tested = new string[0];
            foreach (var kv in profile)
            {
                if (kv.Key == "name") { Name = kv.Value; continue; }
                if (kv.Key == "match") { Match = kv.Value; continue; }
                if (kv.Key == "tested") { Tested = SplitList(kv.Value); continue; }
                // Read old custom profiles after replacing the guessed adjacent-club scan.
                if (kv.Key == "ClubStaffGroups" || kv.Key == "ClubStaffGroupStride") continue;
                FieldInfo f;
                if (!fields.TryGetValue(kv.Key, out f)) throw new Exception("Profile " + label + ": unknown field " + kv.Key);
                try { f.SetValue(null, ToValue(f.FieldType, kv.Value)); }
                catch (Exception ex) { throw new Exception("Profile " + label + ": bad value for " + kv.Key + " (" + ex.Message + ")"); }
            }
        }

        static string[] SplitList(string v) { return v.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray(); }

        static long Num(string v)
        {
            v = v.Trim(); bool neg = v.StartsWith("-"); if (neg) v = v.Substring(1);
            long n = v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? long.Parse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : long.Parse(v, CultureInfo.InvariantCulture);
            return neg ? -n : n;
        }

        static object ToValue(Type t, string v)
        {
            if (t == typeof(ulong)) return (ulong)Num(v);
            if (t == typeof(uint)) return checked((uint)Num(v));
            if (t == typeof(int)) return checked((int)Num(v));
            if (t == typeof(ulong[])) return SplitList(v).Select(x => (ulong)Num(x)).ToArray();
            if (t == typeof(int[])) return SplitList(v).Select(x => checked((int)Num(x))).ToArray();
            return SplitList(v);
        }

        public static string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# FM27€0 profile: field names as in the source, numbers hex (0x..) or decimal, arrays as comma lists.");
            sb.AppendLine("name = " + Name);
            sb.AppendLine("match = " + Match);
            sb.AppendLine("tested = " + string.Join(", ", Tested));
            foreach (var f in Fields())
            {
                var v = f.GetValue(null); string s;
                if (v is ulong) s = "0x" + ((ulong)v).ToString("X");
                else if (v is uint) s = "0x" + ((uint)v).ToString("X");
                else if (v is ulong[]) s = string.Join(", ", ((ulong[])v).Select(x => "0x" + x.ToString("X")).ToArray());
                else if (v is int[]) s = string.Join(", ", ((int[])v).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray());
                else if (v is string[]) s = string.Join(", ", (string[])v);
                else s = Convert.ToString(v, CultureInfo.InvariantCulture);
                sb.AppendLine(f.Name + " = " + s);
            }
            return sb.ToString();
        }
    }

    class ScanResult
    {
        public PlayerRosterIndex RosterIndex;
        public void PrepareDisplayLists()
        {
            if (RosterIndex != null) return;
            Players = Players.OrderBy(p => p.Name).ToList();
            Clubs = Clubs.OrderBy(c => c.Name).ToList();
            Staff = Staff.OrderBy(s => s.Name).ToList();
            Prizes = Prizes.OrderBy(p => p.Name).ToList();
            RosterIndex = new PlayerRosterIndex(Players);
        }
        public string FacilityNote="";
        public ulong? SaveLoads;
        public GrowthDirectory Growth = new GrowthDirectory();
        public readonly List<string> Diagnostics = new List<string>();
        public ulong RetirementManager;
        public int RawPlayers, HiddenWomen, HiddenWomenClubs;
        public bool GenderVerified;
        public string GenderNote = "";
        public List<Player> Players = new List<Player>();
        public List<Staff> Staff = new List<Staff>();
        public List<Club> Clubs = new List<Club>();
        public Dictionary<string, ulong> FirstNames = new Dictionary<string, ulong>(), LastNames = new Dictionary<string, ulong>(), CommonNames = new Dictionary<string, ulong>();
        public Dictionary<ulong, string> Nations = new Dictionary<ulong, string>();
        public Dictionary<string, ulong> Cities = new Dictionary<string, ulong>();
        public Dictionary<ulong, string> CityNames = new Dictionary<ulong, string>();
        public List<PrizeTable> Prizes = new List<PrizeTable>();
        public int PlayerCoaches;
        public int StageCount, StageOk;
    }

    class PrizeTable
    {
        public ulong Vector;    // address of the vector header (begin, end, cap) inside the rules object
        public ulong Comp;      // competition object
        public List<ulong> SeasonVectors = new List<ulong>();   // this season's copies (FM27), paid places only
        public string Name;
        public override string ToString() { return Name; }
    }

    // Finds the class pointers again when a patch moved them, from the objects themselves: the person class is the pointer
    // with the most valid player blocks in front of it, the staff class the one with the most valid staff blocks, and the
    // others are followed from there (contract, team, club, finance, city). This needs the field offsets to be unchanged;
    // the safety checks after the scan catch it when they are not.
    static class Calibration
    {
        // Recover player mappings only for a player-core problem. League and club
        // availability must not replace an otherwise valid player mapping.
        public static string Verify(FmProcess fm, ScanResult r)
        {
            return ScanRecovery.PlayerIssue(r);
        }

        static void Count(Dictionary<ulong, int> cnt, Dictionary<ulong, List<ulong>> samples, ulong v, ulong addr, int keep)
        {
            int n; cnt.TryGetValue(v, out n); cnt[v] = n + 1;
            List<ulong> l; if (!samples.TryGetValue(v, out l)) samples[v] = l = new List<ulong>();
            if (l.Count < keep) l.Add(addr);
        }

        public static bool LooksLikePerson(FmProcess fm, ulong a)
        {
            var head = fm.Read(a, FmLayout.HeadLen); if (head == null) return false;
            string name, search; fm.ReadPersonNames(head, out name, out search);
            int day = BitConverter.ToUInt16(head, (int)FmLayout.BirthDate) & 0x1FF, year = BitConverter.ToUInt16(head, (int)FmLayout.BirthDate + 2);
            return !string.IsNullOrEmpty(name) && year > 1900 && year < 2100 && day >= 1 && day <= 366;
        }

        // Most common in-image value with at least 60% of the votes and 5 votes; 0 when there is no clear winner
        static ulong Mode(FmProcess fm, List<ulong> votes, out int count)
        {
            count = 0;
            var g = votes.Where(fm.InImage).GroupBy(x => x).OrderByDescending(x => x.Count()).FirstOrDefault();
            if (g == null || g.Count() < 5 || g.Count() < votes.Count * 0.6) return 0;
            count = g.Count(); return g.Key;
        }

        static void Set(List<string> report, FmProcess fm, string label, ref ulong field, ulong vt, int count)
        {
            if (vt == 0) { report.Add("校准：" + label + " class not found, kept fm+0x" + field.ToString("X")); return; }
            ulong rva = vt - fm.Base;
            report.Add("校准：" + label + " 对象位于 fm+0x" + rva.ToString("X") + (rva == field ? " (same as the profile)" : "（原配置位于 fm+0x" + field.ToString("X") + ")") + ", " + count.ToString("N0", CultureInfo.InvariantCulture) + " 已找到");
            field = rva;
        }

        internal static void RecoverClubFinance(FmProcess fm, Func<ulong,int,byte[]> read, IEnumerable<ulong> finances, List<string> report)
        {
            var candidates = new ClubFinanceCalibration(read, fm.Base, fm.ImageSize);
            foreach (ulong finance in finances) candidates.Observe(finance);
            int hits;
            ulong table = Mode(fm, candidates.FullVotes, out hits);
            if (table == 0 && candidates.IsCompact(fm.Base + FmProcess.FinanceVtRva))
            {
                FmProcess.FinanceVtRva = 0;
                report.Add("校准：原完整财务位置实际是简化财务，已清除该映射。");
            }
            Set(report, fm, "Finance", ref FmProcess.FinanceVtRva, table, hits);
            ulong compact = Mode(fm, candidates.CompactVotes, out hits);
            if (compact != 0) Set(report, fm, "Compact finance", ref FmProcess.CompactFinanceVtRva, compact, hits);
            if (table == 0 || !candidates.CurrentFields(table)) return;
            FmLayout.FacilityFin = new ulong[] { 0, 0, 0, 0, ClubFinanceCalibration.CorporateOffset };
            FmLayout.FinTicketPrice = ClubFinanceCalibration.TicketOffset;
            report.Add("校准：已核对完整财务的转会预算、周薪预算和商业设施原生取值。设施与票价继续按项目检查。");
        }

        public static List<string> Run(FmProcess fm, Action<double> progress)
        {
            var report = new List<string>();
            var personCnt = new Dictionary<ulong, int>(); var staffCnt = new Dictionary<ulong, int>();
            var personSamples = new Dictionary<ulong, List<ulong>>(); var staffSamples = new Dictionary<ulong, List<ulong>>();
            var mgrCnt = new Dictionary<ulong, int>(); var mgrSamples = new Dictionary<ulong, List<ulong>>();
            int pb = (int)FmLayout.BlockBeforePerson, sb = (int)FmLayout.StaffBlockBefore, mb = (int)(FmLayout.ManagerAttrsBefore + FmLayout.StaffBlockBefore);
            var pcCnt = new Dictionary<ulong, int>(); var pcSamples = new Dictionary<ulong, List<ulong>>();
            int pcb = pb + (int)FmLayout.PlayerCoachExtra;
            fm.ScanRegions((buf, len, at) =>
            {
                for (int i = 0; i + 8 <= len; i += 8)
                {
                    ulong v = BitConverter.ToUInt64(buf, i);
                    if (!fm.InImage(v)) continue;
                    if (FmLayout.ValidBlock(buf, i - pb)) Count(personCnt, personSamples, v, at + (ulong)i, 2000);
                    if (FmLayout.ValidStaffBlock(buf, i - sb)) Count(staffCnt, staffSamples, v, at + (ulong)i, 50);
                    // the human manager: staff attributes ManagerAttrsBefore further back (only a handful of these exist)
                    if (i - mb >= 0 && FmLayout.ValidStaffBlock(buf, i - mb)) Count(mgrCnt, mgrSamples, v, at + (ulong)i, 10);
                    // player-coaches: a player block PlayerCoachExtra further back than for a player
                    if (FmLayout.ValidBlock(buf, i - pcb)) Count(pcCnt, pcSamples, v, at + (ulong)i, 200);
                }
            }, progress);

            // person: most valid player blocks, and the objects really are named people
            report.Add(fm.ScanSummary("校准内存"));
            report.Add("校准候选：" + personCnt.Count + " 组属性候选，共 " + personCnt.Values.Sum(x => (long)x) + " 个；按数量核对前 5 组，至少 1000 个候选且身份抽样通过率达到 90%。");
            ulong person = 0;
            foreach (var kv in personCnt.OrderByDescending(x => x.Value).Take(5))
            {
                var s = personSamples[kv.Key].Take(200).ToList();
                int identities = kv.Value >= 1000 ? s.Count(a => LooksLikePerson(fm, a)) : -1;
                report.Add(ScanDiagnostics.Candidate(kv.Key - fm.Base, kv.Value, s.Count, identities));
                if (kv.Value >= 1000 && identities >= s.Count * 0.9) { person = kv.Key; break; }
            }
            Set(report, fm, "Person", ref FmProcess.PersonVtRva, person, person == 0 ? 0 : personCnt[person]);
            if (person == 0) { report.Add("重新识别失败：没有找到通过检查的球员对象；可能是存档未载入完成、读取失败或当前数据布局尚未适配。不能仅据此判断为联赛补丁冲突。"); return report; }

            // staff: most valid staff blocks; player-coaches: another class whose every sample is a valid, named staff member
            var staffCands = staffCnt.Where(kv => kv.Key != person).OrderByDescending(kv => kv.Value)
                .Where(kv => staffSamples[kv.Key].All(a => LooksLikePerson(fm, a))).ToList();
            ulong staff = staffCands.Count > 0 && staffCands[0].Value >= 50 ? staffCands[0].Key : 0;
            Set(report, fm, "Staff", ref FmProcess.StaffVtRva, staff, staff == 0 ? 0 : staffCnt[staff]);

            // followed from the people found
            var contractVotes = new List<ulong>(); var clubVotes = new List<ulong>(); var cityVotes = new List<ulong>(); var clubSet = new HashSet<ulong>();
            foreach (var p in personSamples[person])
            {
                ulong c = fm.ReadPtr(p + FmLayout.ContractAfterPerson);
                if (c > 0x10000 && fm.ReadPtr(c + 8) == p) contractVotes.Add(fm.ReadPtr(c));
                ulong t = fm.ReadPtr(p - FmLayout.TeamBeforePerson), cl = t > 0x10000 ? fm.ReadPtr(t + FmLayout.TeamClub) : 0;
                if (cl > 0x10000 && !string.IsNullOrEmpty(fm.ReadLenString(fm.ReadPtr(cl + FmLayout.ClubName)))) { clubVotes.Add(fm.ReadPtr(cl)); clubSet.Add(cl); }
                ulong ct = fm.ReadPtr(p + FmLayout.BirthCity);
                if (ct > 0x10000 && !string.IsNullOrEmpty(fm.ReadLenString(fm.ReadPtr(ct + FmLayout.CityName)))) cityVotes.Add(fm.ReadPtr(ct));
            }
            int n;
            ulong contract = Mode(fm, contractVotes, out n); Set(report, fm, "Contract", ref FmProcess.ContractVtRva, contract, n);
            // player-coaches: another staff-layout class whose members mostly hold a contract pointing back at them (a third
            // class with birth dates but no names or contracts outnumbers them in a long-running save)
            ulong coach = 0;
            if (staff != 0 && contract != 0)
                foreach (var kv in staffCands.Skip(1))
                {
                    var sm = staffSamples[kv.Key];
                    int withC = sm.Count(a => { ulong c = fm.ReadPtr(a + FmLayout.StaffContract); return c > 0x10000 && fm.ReadPtr(c) == contract && fm.ReadPtr(c + 8) == a; });
                    if (withC >= sm.Count / 2 && withC >= 5) { coach = kv.Key; break; }
                }
            // FM27 player-coaches often have no contract of their own: then the class with player blocks PlayerCoachExtra back
            // and a valid staff block in front, whose objects are named people
            if (coach == 0)
                foreach (var kv in pcCnt.Where(x => x.Key != person && x.Key != staff && staffCnt.ContainsKey(x.Key)).OrderByDescending(x => x.Value))
                {
                    var sm = pcSamples[kv.Key];
                    if (kv.Value >= 5 && sm.Count(a => LooksLikePerson(fm, a)) >= sm.Count * 0.9) { coach = kv.Key; break; }
                }
            Set(report, fm, "Player-coach", ref FmProcess.PlayerCoachVtRva, coach, coach == 0 ? 0 : Math.Max(staffCnt[coach], pcCnt.ContainsKey(coach) ? pcCnt[coach] : 0));
            ulong city = Mode(fm, cityVotes, out n); Set(report, fm, "City", ref FmProcess.CityVtRva, city, n);
            ulong club = Mode(fm, clubVotes, out n); Set(report, fm, "Club", ref FmProcess.ClubVtRva, club, n);
            var financeObjects = new List<ulong>(); var teamVotes = new List<ulong>();
            foreach (var cl in clubSet)
            {
                if (fm.ReadPtr(cl) != club) continue;
                financeObjects.Add(fm.ReadPtr(cl + FmLayout.ClubFinance));
                ulong t0 = fm.ReadPtr(fm.ReadPtr(cl + FmLayout.ClubTeams));
                if (t0 > 0x10000 && fm.ReadPtr(t0 + FmLayout.TeamClub2) == cl) teamVotes.Add(fm.ReadPtr(t0));
            }
            if (!CurrentBuildProfile.Matches(fm) || Profile.Source != null) RecoverClubFinance(fm, fm.Read, financeObjects, report);
            ulong team = Mode(fm, teamVotes, out n); Set(report, fm, "Team", ref FmProcess.TeamVtRva, team, n);

            // loan contracts: player object +0x348 -> slot -> loan contract, which points back at the person
            var loanVotes = new List<ulong>();
            foreach (var p in personSamples[person])
            {
                ulong slot = fm.ReadPtr(p - FmLayout.PlayerObjBeforePerson + FmLayout.PlayerLoanContract); if (slot < 0x10000) continue;
                ulong lc = fm.ReadPtr(slot);
                if (lc > 0x10000 && fm.ReadPtr(lc + 8) == p) loanVotes.Add(fm.ReadPtr(lc));
            }
            ulong loan = Mode(fm, loanVotes, out n); Set(report, fm, "Loan contract", ref FmProcess.LoanContractVtRva, loan, n);

            // stadiums: first team +0x78, an object with a readable name at +0x38
            var stadVotes = new List<ulong>();
            foreach (var cl in clubSet)
            {
                ulong t0 = fm.ReadPtr(fm.ReadPtr(cl + FmLayout.ClubTeams)); if (t0 < 0x10000 || fm.ReadPtr(t0 + FmLayout.TeamClub2) != cl) continue;
                ulong sd = fm.ReadPtr(t0 + FmLayout.TeamStadium);
                if (sd > 0x10000 && !string.IsNullOrEmpty(fm.ReadLenString(fm.ReadPtr(sd + FmLayout.StadiumName)))) stadVotes.Add(fm.ReadPtr(sd));
            }
            ulong stadium = Mode(fm, stadVotes, out n); Set(report, fm, "Stadium", ref FmProcess.StadiumVtRva, stadium, n);

            // the human manager: a named person with a birth date, staff attributes before him and a contract pointing back
            // at him (FM's empty template manager has no contract). Not one of the classes found above.
            ulong manager = 0; int mgrN = 0;
            foreach (var kv in mgrCnt.Where(x => x.Key != person && x.Key != staff && x.Key != coach && x.Value <= 10))
            {
                int good = mgrSamples[kv.Key].Count(a => LooksLikePerson(fm, a) && fm.ReadPtr(fm.ReadPtr(a + FmLayout.StaffContract) + 8) == a &&
                                                       fm.ReadPtr(fm.ReadPtr(a + FmLayout.StaffContract)) == contract);
                if (good > 0) { manager = kv.Key; mgrN = good; break; }
            }
            Set(report, fm, "Manager", ref FmProcess.ManagerVtRva, manager, mgrN);
            if (FmLayout.StageCompetition == 0) Prizes(fm, clubSet, report, progress);
            return report;
        }

        // Prize money (FM27): a first team points at its league (+TeamCompetition), a competition with a name that holds its
        // rules object at +StageRules. This season's copies are objects pointing at a competition (+SeasonComp) and at a stage
        // object (+SeasonStage) with the paid places as a vector of int32 at +StagePrizes; they take a second pass over memory.
        static void Prizes(FmProcess fm, HashSet<ulong> clubSet, List<string> report, Action<double> progress)
        {
            var compVotes = new List<ulong>(); var comps = new HashSet<ulong>(); int n;
            foreach (var cl in clubSet)
            {
                ulong t0 = fm.ReadPtr(fm.ReadPtr(cl + FmLayout.ClubTeams)); if (t0 < 0x10000 || fm.ReadPtr(t0 + FmLayout.TeamClub2) != cl) continue;
                ulong cp = fm.ReadPtr(t0 + FmLayout.TeamCompetition);
                if (cp > 0x10000 && !string.IsNullOrEmpty(fm.ReadLenString(fm.ReadPtr(cp + FmLayout.CompetitionName)))) { compVotes.Add(fm.ReadPtr(cp)); comps.Add(cp); }
            }
            ulong comp = Mode(fm, compVotes, out n);
            comps.RemoveWhere(c => fm.ReadPtr(c) != comp);
            Set(report, fm, "Competition", ref FmProcess.CompetitionVtRva, comp, comps.Count);
            if (comp == 0) return;
            FmProcess.StageVtRva = FmProcess.CompetitionVtRva;
            var rulesVotes = comps.Select(c => fm.ReadPtr(c + FmLayout.StageRules)).Where(r => r > 0x10000).Select(r => fm.ReadPtr(r)).ToList();
            ulong rules = Mode(fm, rulesVotes, out n); Set(report, fm, "Competition rules", ref FmProcess.CompRulesVtRva, rules, n);

            var stages = new Dictionary<ulong, List<ulong>>();   // season class -> its stage objects
            int sc = (int)FmLayout.SeasonComp, ss = (int)FmLayout.SeasonStage;
            fm.ScanRegions((buf, len, at) =>
            {
                for (int i = 0; i + Math.Max(sc, ss) + 8 <= len; i += 8)
                {
                    ulong v = BitConverter.ToUInt64(buf, i);
                    if (!fm.InImage(v) || !comps.Contains(BitConverter.ToUInt64(buf, i + sc))) continue;
                    ulong stg = BitConverter.ToUInt64(buf, i + ss); if (stg < 0x10000) continue;
                    List<ulong> l; if (!stages.TryGetValue(v, out l)) stages[v] = l = new List<ulong>();
                    l.Add(stg);
                }
            }, progress);
            ulong season = 0, stage = 0; int best = 0;
            foreach (var kv in stages)
            {
                var ok = kv.Value.Where(stg =>
                {
                    // a heap object holding a heap vector of prize money: amounts 0 to 1,000,000,000 pounds, some above 0
                    if (fm.InImage(stg)) return false;
                    ulong b = fm.ReadPtr(stg + FmLayout.StagePrizes), e = fm.ReadPtr(stg + FmLayout.StagePrizes + 8);
                    if (b < 0x10000 || fm.InImage(b) || e <= b || e - b > 400 || (e - b) % 4 != 0) return false;
                    var vals = fm.Read(b, (int)(e - b)); if (vals == null) return false;
                    bool money = false;
                    for (int k = 0; k + 4 <= vals.Length; k += 4) { int m = BitConverter.ToInt32(vals, k); if (m < 0 || m > 1000000000) return false; if (m > 0) money = true; }
                    return money;
                }).Select(stg => fm.ReadPtr(stg)).Where(fm.InImage).GroupBy(x => x).OrderByDescending(g => g.Count()).FirstOrDefault();
                if (ok != null && ok.Count() > best) { best = ok.Count(); season = kv.Key; stage = ok.Key; }
            }
            if (best < 2) season = stage = 0;   // only leagues reached from the clubs scanned count, often just a few
            Set(report, fm, "Season", ref FmProcess.SeasonVtRva, season, best);
            Set(report, fm, "Season stage", ref FmProcess.SeasonStageVtRva, stage, best);
        }

        static bool ValidDate(uint v) { int y = (int)(v >> 16), d = (int)(v & 0x1FF); return y >= 1990 && y <= 2200 && d >= 1 && d <= 366; }
        static int DayNumber(uint v) { return (int)(v >> 16) * 366 + (int)(v & 0x1FF); }

        // The current game date: a valid date in fm.exe's own data, on or after the most recent transfer-listing date in the
        // game (FM stamps a listing with that day's date) and within a year of it. anchor = 0 when nobody is listed.
        public static bool DateFits(uint v, uint anchor)
        {
            if (!ValidDate(v)) return false;
            if (anchor == 0) return true;
            int diff = DayNumber(v) - DayNumber(anchor);
            return diff >= 0 && diff <= 366;
        }

        // Looks for the date in fm.exe's writable data. FM keeps today's date in more than one place, with the time-of-day
        // bits at 0 (FM24: fm+0x631D5BC and fm+0x642F458, while unrelated values that merely look like dates mostly have
        // time bits set), so time-0 values are preferred and the day most addresses agree on wins. 0 when nothing fits.
        public static ulong FindDate(FmProcess fm, uint anchor, out string note)
        {
            note = "";
            if (anchor == 0) { note = "no transfer-listed player to compare with"; return 0; }
            if (FmLayout.DateCalibrate == 2) return FindDateInCode(fm, anchor, out note);
            var hits = new Dictionary<int, List<ulong>>(); var hits0 = new Dictionary<int, List<ulong>>();
            foreach (var r in fm.Regions(Native.MEM_IMAGE, fm.Base, fm.Base + fm.ImageSize, true))
            {
                var b = fm.Read(r.Key, (int)Math.Min(r.Value, 256UL << 20)); if (b == null) continue;
                for (int i = 0; i + 4 <= b.Length; i += 4)
                {
                    uint v = BitConverter.ToUInt32(b, i);
                    if (!DateFits(v, anchor)) continue;
                    int key = DayNumber(v); List<ulong> l; var d = (v & 0xFE00) == 0 ? hits0 : hits;
                    if (!d.TryGetValue(key, out l)) d[key] = l = new List<ulong>();
                    l.Add(r.Key + (ulong)i);
                }
            }
            if (hits0.Count > 0) hits = hits0;
            var best = hits.OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key).FirstOrDefault();
            note = hits.Count + " candidate date(s)" + (hits == hits0 ? " with time 0" : "") + ", " + (best.Value == null ? 0 : best.Value.Count) + " address(es) agree";
            return best.Value == null ? 0 : best.Value[0] - fm.Base;
        }

        // Walks the executable sections of the engine module, 16 MB at a time (with an overlap so no instruction is cut)
        static void ForCode(FmProcess fm, Action<byte[], int, ulong> onChunk)
        {
            var hdr = fm.Read(fm.Base, 0x1000); if (hdr == null) return;
            int pe = BitConverter.ToInt32(hdr, 0x3C), ns = BitConverter.ToUInt16(hdr, pe + 6), opt = BitConverter.ToUInt16(hdr, pe + 20);
            const int Chunk = 16 << 20, Over = 16;
            for (int j = 0; j < ns; j++)
            {
                int o = pe + 24 + opt + 40 * j; if (o + 40 > hdr.Length) break;
                uint size = BitConverter.ToUInt32(hdr, o + 8), va = BitConverter.ToUInt32(hdr, o + 12), ch = BitConverter.ToUInt32(hdr, o + 36);
                if ((ch & 0x20) == 0) continue;
                for (uint off = 0; off < size; off += Chunk)
                {
                    int n = (int)Math.Min((ulong)Chunk + Over, size - off);
                    var b = fm.Read(fm.Base + va + off, n); if (b != null) onChunk(b, n, va + off);
                }
            }
        }

        // FM27: the game date is the variable FM's code sets to the empty date (mov dword [rip+x], EmptyDate) and then reads
        // thousands of times. A few other dates are set the same way, so the one that fits the latest transfer listing and
        // has by far the most references in the code wins (FM27 Xbox: 7,430 against 3).
        static ulong FindDateInCode(FmProcess fm, uint anchor, out string note)
        {
            var init = new HashSet<ulong>(); var e = BitConverter.GetBytes(FmLayout.EmptyDate);
            ForCode(fm, (b, n, rva) =>
            {
                for (int i = 0; i + 10 <= n; i++)
                    if (b[i] == 0xC7 && b[i + 1] == 0x05 && b[i + 6] == e[0] && b[i + 7] == e[1] && b[i + 8] == e[2] && b[i + 9] == e[3])
                        init.Add((ulong)((long)rva + i + 10 + BitConverter.ToInt32(b, i + 2)));
            });
            var cands = init.Where(t => t < fm.ImageSize && DateFits(fm.ReadU32(fm.Base + t) ?? 0, anchor)).ToList();
            note = init.Count + " date variable(s) in the code, " + cands.Count + " fit";
            if (cands.Count == 0) return 0;
            var refs = cands.ToDictionary(t => t, t => 0);
            ForCode(fm, (b, n, rva) =>
            {
                for (int i = 0; i + 4 <= n; i++)   // the chunk overlap can count a reference twice, which does not matter here
                {
                    ulong t = (ulong)((long)rva + i + 4 + BitConverter.ToInt32(b, i)); int c;
                    if (refs.TryGetValue(t, out c)) refs[t] = c + 1;
                }
            });
            var order = refs.OrderByDescending(kv => kv.Value).ToList();
            note += ", " + order[0].Value + " code references" + (order.Count > 1 ? " (next " + order[1].Value + ")" : "");
            if (order[0].Value < 100 || (order.Count > 1 && order[0].Value < order[1].Value * 10)) { note += ", no clear winner"; return 0; }
            return order[0].Key;
        }
    }

    // Checks run after every scan on a sample of what was found. Each group of features can only be edited when its checks
    // pass, so a patch that moved a field makes that part read-only instead of writing to the wrong place.
    static class Safety
    {
        public static string FacilityNote="";
        public static Dictionary<string, string> Off = new Dictionary<string, string>();
        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string> {
            { "core", "players (attributes, positions, names, details)" }, { "status", "condition, sharpness and morale" }, { "ability", "CA/PA" },
            { "personality", "personality" }, { "injury", "injuries" }, { "contract", "wages, contracts and clauses" }, { "transfer", "transfer status" },
            { "date", "transfer listing (game date)" }, { "looks", "looks" }, { "birthplace", "place of birth" }, { "finance", "club money" },
            { "facilities", "facilities" }, { "reputation", "club reputation" }, { "staff", "staff" },
            { "body", "height and weight" }, { "playerrep", "player reputation" }, { "squadstatus", "squad status" }, { "staffrep", "staff reputation" }, { "swap", "swap and move players" }, { "staffcontract", "staff contracts" }, { "squadnumber", "squad numbers" }, { "othernat", "other nationalities" }, { "stadium", "stadium capacity" }, { "clubname", "club names" }, { "jaded", "jadedness" }, { "prize", "prize money" }, { "caps", "international caps" }, { "tickets", "ticket prices and season tickets" }, { "sponsor", "sponsorship deals" }, { "staffmove", "moving staff to another club" } };

        public static bool On(string group) { return !Off.ContainsKey("core") && !Off.ContainsKey(group); }

        // a player with a contract that points back at him (free agents have none; their old team pointer can stay behind)
        public static bool HasContract(FmProcess fm, ulong person)
        {
            ulong c = fm.ReadPtr(person + FmLayout.ContractAfterPerson);
            return c > 0x10000 && fm.ReadPtr(c) == fm.Base + FmProcess.ContractVtRva && fm.ReadPtr(c + 8) == person;
        }

        static void Check(List<string> rep, string group, string what, int ok, int total, double min)
        {
            bool pass = total > 0 && ok >= total * min;
            rep.Add((pass ? "检查通过：" : "检查失败：") + Zh.G(group) + "：" + what + " " + ok.ToString("N0", CultureInfo.InvariantCulture) + "/" + total.ToString("N0", CultureInfo.InvariantCulture));
            if (!pass && !Off.ContainsKey(group)) Off[group] = what;
        }

        static void Need(List<string> rep, string group, string what, bool pass)
        {
            rep.Add((pass ? "检查通过：" : "检查失败：") + Zh.G(group) + "：" + what);
            if (!pass && !Off.ContainsKey(group)) Off[group] = what;
        }

        static bool ValidPacked(uint v) { int y = (int)(v >> 16), d = (int)(v & 0x1FF); return y >= 1900 && y <= 2200 && d >= 1 && d <= 366; }
        internal static void AssessFacilities(List<string> report,FacilityCoverage coverage,FacilityLayout layout)
        {
            report.AddRange(coverage.Report(layout));
            if(!coverage.CanEnable(layout) && !Off.ContainsKey("facilities"))Off["facilities"]=coverage.BlockedReason(layout);
            FacilityNote=layout.VerifiedBuild && coverage.CanEnable(layout) && coverage.Valid<coverage.Clubs*5?
                "本次设施逐项检查："+coverage.Valid.ToString("N0",CultureInfo.InvariantCulture)+" 项有效，"+(coverage.Clubs*5-coverage.Valid).ToString("N0",CultureInfo.InvariantCulture)+" 项只读；以当前俱乐部逐项结果为准。":"";
        }

        public static List<string> Run(FmProcess fm, ScanResult r)
        {
            Off = new Dictionary<string, string>();
            FacilityNote="";r.FacilityNote="";
            var rep = new List<string>();
            Need(rep, "core", "FM 版本 " + fm.Version + " (" + Profile.Name + " 适用于 " + Profile.Match + "x)", Profile.Matches(fm.Version));
            Need(rep, "core", r.Players.Count.ToString("N0", CultureInfo.InvariantCulture) + " 名球员（至少需要 1000 人）", r.Players.Count >= 1000);
            int step = Math.Max(1, r.Players.Count / 4000);
            var sample = r.Players.Where((x, i) => i % step == 0).ToList();
            int n = sample.Count;

            Check(rep, "core", "出生日期有效", sample.Count(p => p.Born.HasValue), n, 0.95);
            Check(rep, "core", "国籍指向有效国家", sample.Count(p => !string.IsNullOrEmpty(fm.ReadLenString(fm.ReadPtr(fm.ReadPtr(p.Person + FmLayout.Nation) + FmLayout.NationName)))), n, 0.95);

            int stOk = 0, condOn = 0, abOk = 0, perOk = 0, injN = 0, injOk = 0, lookOk = 0, cityN = 0, cityOk = 0, askOk = 0, bodyOk = 0, prepOk = 0, prepOn = 0;
            foreach (var p in sample)
            {
                bool s = true;
                for (int k = 0; k < FmLayout.StatusBeforePerson.Length; k++) { int value=StatusValues.Decode(fm.Read(p.Tail-FmLayout.StatusBeforePerson[k],FmLayout.StatusSize[k]),k); if(value<0)s=false;else if(k==0 && value>0)condOn++; }
                if (s) stOk++;
                var ab = fm.Read(p.Tail - FmLayout.AbilityBeforePerson, 4);
                if (ab != null) { int ca = BitConverter.ToInt16(ab, 0), pa = BitConverter.ToInt16(ab, 2); if (ca >= 1 && ca <= 200 && pa != 0 && pa >= -200 && pa <= 200) abOk++; }
                var pe = fm.Read(p.Person + FmLayout.PersonalityAfterPerson, 8);
                if (pe != null && pe.All(x => x >= 1 && x <= 20)) perOk++;
                if(fm.VerifiedCurrentBuild)
                {
                    injN++;try{CurrentInjurySnapshot.Capture(fm.Read,fm.Base,p);injOk++;}catch{}
                }
                else
                {
                    ulong med = fm.ReadPtr(p.Tail - FmLayout.MedicalBeforePerson); var mv = med > 0x10000 ? fm.Read(med, 24) : null;
                    if (med != 0) injN++;
                    if (mv != null) { ulong b0 = BitConverter.ToUInt64(mv, 0), e0 = BitConverter.ToUInt64(mv, 8), c0 = BitConverter.ToUInt64(mv, 16); if (b0 <= e0 && e0 <= c0 && e0 - b0 < 0x400 && (e0 - b0) % 8 == 0) injOk++; }
                }
                var lk = fm.Read(p.Person + FmLayout.Ethnicity, 4);
                if (lk != null && lk[0] <= 10 && lk[1] <= 18 && lk[2] <= 6 && (lk[3] <= 19 || lk[3] == 255)) lookOk++;
                ulong ct = fm.ReadPtr(p.Person + FmLayout.BirthCity);
                if (ct != 0) { cityN++; if (fm.ReadPtr(ct) == fm.Base + FmProcess.CityVtRva) cityOk++; }
                var ask = fm.ReadI32(p.Tail - FmLayout.AskingBeforePerson);
                if (ask != null && ask >= -1) askOk++;
                var hb = fm.Read(p.Tail - FmLayout.HeightBeforePerson, 2); var wb = FmLayout.WeightBeforePerson == 0 ? new byte[] { 70, 0 } : fm.Read(p.Tail - FmLayout.WeightBeforePerson, 2);
                if (hb != null && wb != null) { int w = BitConverter.ToUInt16(wb, 0), h = BitConverter.ToUInt16(hb, 0); if (h >= 140 && h <= 220 && w >= 40 && w <= 130) bodyOk++; }
                bool rOk = true, rAny = false;
                foreach (var o in FmLayout.RepBeforePerson) { var rb = fm.Read(p.Tail - o, 2); int v = rb == null ? -1 : BitConverter.ToInt16(rb, 0); if (v < 0 || v > 10000) rOk = false; if (v > 0) rAny = true; }
                if (rOk) prepOk++; if (rAny) prepOn++;
            }
            Check(rep, "status", "体能、比赛状态和士气在有效范围内", stOk, n, 0.99);
            Check(rep, "status", "体能大于 0", condOn, n, 0.5);
            Check(rep, "ability", "CA 在 1-200 范围内且已设置 PA", abOk, n, 0.99);
            Check(rep, "personality", "全部 8 项性格值在 1-20 范围内", perOk, n, 0.99);
            Check(rep, "injury", "医疗记录包含可读取的伤病列表", injOk, injN, 0.95);
            Check(rep, "looks", "外貌数值在有效范围内", lookOk, n, 0.99);
            Check(rep, "birthplace", "球员设有出生地", cityN, n, 0.5);
            Check(rep, "birthplace", "出生地指向有效城市", cityOk, cityN, 0.95);
            Need(rep, "birthplace", r.Cities.Count.ToString("N0", CultureInfo.InvariantCulture) + " 座城市（至少需要 100 座）", r.Cities.Count >= 100);
            Check(rep, "transfer", "要价为 -1 或更高", askOk, n, 0.99);
            Check(rep, "body", "身高在 140-220 厘米、体重在 40-130 公斤范围内", bodyOk, n, 0.99);
            Check(rep, "playerrep", "声望在 0-10000 范围内", prepOk, n, 0.99);
            Check(rep, "playerrep", "至少一项声望大于 0", prepOn, n, 0.5);

            // contracts (and the most recent listing date, over every player, for the game date check)
            int withC = 0, cOk = 0, listOk = 0, sqOk = 0; uint anchor = 0;
            foreach (var p in r.Players)
            {
                ulong c = fm.ReadPtr(p.Person + FmLayout.ContractAfterPerson);
                if (c < 0x10000 || fm.ReadPtr(c) != fm.Base + FmProcess.ContractVtRva || fm.ReadPtr(c + 8) != p.Person) continue;
                var cb = fm.Read(c, (int)Math.Max(FmLayout.ContractListCode, FmLayout.ContractExpiry) + 4); if (cb == null) continue;
                uint listed = BitConverter.ToUInt32(cb, (int)FmLayout.ContractListedDate);
                if ((cb[(int)FmLayout.ContractListFlags] & 1) != 0 && ValidPacked(listed) && (anchor == 0 || (listed >> 16) * 366 + (listed & 0x1FF) > (anchor >> 16) * 366 + (anchor & 0x1FF))) anchor = listed;
                if (withC++ % step != 0) continue;
                int wage = BitConverter.ToInt32(cb, (int)FmLayout.ContractWage);
                if (wage >= 0 && wage <= 10000000 && ValidPacked(BitConverter.ToUInt32(cb, (int)FmLayout.ContractExpiry))) cOk++;
                if (listed == FmLayout.EmptyDate || ValidPacked(listed)) listOk++;
                var sq = fm.Read(c + FmLayout.ContractSquadStatus, 1); if (sq != null && FmLayout.SquadStatusNames.ContainsKey(sq[0])) sqOk++;
            }
            int cSample = (withC + step - 1) / step;
            Check(rep, "contract", "球员有有效合同", withC, r.Players.Count, 0.3);
            Check(rep, "contract", "工资和合同到期日有效", cOk, cSample, 0.99);
            Check(rep, "transfer", "挂牌日期有效", listOk, cSample, 0.99);
            Check(rep, "squadstatus", "球队定位代码有效", sqOk, cSample, 0.99);
            // squad numbers: own-club contracts hold 1-99 or 255 (none)
            {
                if(fm.VerifiedCurrentBuild)
                {
                    int total=0,valid=0;
                    foreach(var pl in r.Players.Where((x,i)=>i%Math.Max(1,r.Players.Count/4000)==0))
                    {
                        try{var number=CurrentShirtSnapshot.Capture(fm.Read,fm.Base,pl);if(number!=null){total++;valid++;}}
                        catch{total++;}
                    }
                    Check(rep,"squadnumber","本队及租借合同的球衣号码有效",valid,total,0.99);
                }
                else
                {
                int snN = 0, snOk = 0;
                foreach (var pl in r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 3000) == 0))
                {
                    ulong po = pl.Tail - FmLayout.PlayerObjBeforePerson, t = fm.ReadPtr(po + FmLayout.PlayerTeam);
                    ulong c = fm.ReadPtr(pl.Person + FmLayout.ContractAfterPerson);
                    if (t < 0x10000 || c < 0x10000 || fm.ReadPtr(c) != fm.Base + FmProcess.ContractVtRva || fm.ReadPtr(c + FmLayout.ContractTeam) != t) continue;
                    snN++; var b = fm.Read(c + FmLayout.ContractSquadNumber, 1);
                    if (b != null && (b[0] == 255 || (b[0] >= 1 && b[0] <= 99))) snOk++;
                }
                Check(rep, "squadnumber", "球衣号码为 1-99 或未设置", snOk, snN, 0.99);
                // loan contracts behind player +0x348 point back at the player and hold 1-99 or 255
                int lnN = 0, lnOk = 0;
                foreach (var pl in r.Players)
                {
                    ulong slot = fm.ReadPtr(pl.Person - FmLayout.PlayerObjBeforePerson + FmLayout.PlayerLoanContract); if (slot < 0x10000) continue;
                    ulong lc = fm.ReadPtr(slot); if (lc < 0x10000) continue;
                    if (++lnN > 400) break;
                    var b = fm.Read(lc + FmLayout.ContractSquadNumber, 1);
                    if (fm.ReadPtr(lc) == fm.Base + FmProcess.LoanContractVtRva && fm.ReadPtr(lc + 8) == pl.Person && b != null && (b[0] == 255 || (b[0] >= 1 && b[0] <= 99))) lnOk++;
                }
                Check(rep, "squadnumber", "租借合同包含有效球衣号码", lnOk, Math.Min(lnN, 400), 0.95);
                }
            }
            // other nationalities: relation list looks like a vector of 16-byte records, nation records point at nations
            {
                int onN = 0, onOk = 0;
                foreach (var pl in r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 1500) == 0))
                {
                    ulong natVt = fm.ReadPtr(fm.ReadPtr(pl.Person + FmLayout.Nation)); if (natVt < 0x10000 || !HasContract(fm, pl.Person)) continue;
                    onN++; ulong b, e, c; List<int> idx;
                    if (MainForm.ReadRelations(fm, pl.Person, natVt, out b, out e, out c, out idx)) onOk++;
                }
                Check(rep, "othernat", "球员关系列表可读，国籍记录指向有效国家", onOk, onN, 0.98);
            }
            if (!Safety.On("contract")) { Off["transfer"] = "合同检查未通过"; Off["squadstatus"] = "合同检查未通过"; }

            uint today = fm.ReadGameDate() ?? 0;
            string anchorText = anchor == 0 ? "没有转会挂牌球员" : "最近挂牌日期 " + DateText(anchor);
            if (fm.VerifiedCurrentBuild) Need(rep, "date", "游戏日期 " + DateText(today) + "（已核对的当前日期入口）", ValidPacked(today));
            else if (Calibration.DateFits(today, anchor)) Need(rep, "date", "游戏日期 " + DateText(today) + " (" + anchorText + ")", true);
            else
            {
                string note = "当前配置未启用校准"; ulong rva = FmLayout.Calibrate == 0 || FmLayout.DateCalibrate == 0 ? 0 : Calibration.FindDate(fm, anchor, out note);
                if (rva != 0)
                {
                    uint found = fm.ReadU32(fm.Base + rva) ?? 0;
                    rep.Add("校准：游戏日期位于 fm+0x" + rva.ToString("X") + "（原配置位于 fm+0x" + FmProcess.CurrentDateRva.ToString("X") + "), " + DateText(found) + ", " + note);
                    FmProcess.CurrentDateRva = rva;
                    Need(rep, "date", "游戏日期 " + DateText(found) + " (" + anchorText + ")", true);
                }
                else Need(rep, "date", "游戏日期（" + anchorText + ", " + note + ")", false);
            }

            // Keep field coverage measured on clubs that have editable full-finance records.
            var fullClubs=r.Clubs.Where(c=>c.HasFullFinance).ToList();
            Need(rep, "finance", fullClubs.Count.ToString("N0", CultureInfo.InvariantCulture) + " 家具备完整财务的俱乐部（至少需要 100 家）", fullClubs.Count >= 100);
            int facN = 0, repN = 0, repOk = 0;
            var facilityLayout=FacilityLayout.From(fm);var facilityCoverage=new FacilityCoverage();
            foreach (var c in fullClubs.Where((x, i) => i % Math.Max(1, fullClubs.Count / 2000) == 0))
            {
                facN++;facilityCoverage.Add(new ClubFacilitySnapshot(fm.Read,facilityLayout,c));
                if (c.Team != 0) { repN++; var b = fm.Read(c.Team + FmLayout.TeamReputation, 2); if (b != null && BitConverter.ToUInt16(b, 0) <= 10000) repOk++; }
            }
            AssessFacilities(rep,facilityCoverage,facilityLayout);
            r.FacilityNote=FacilityNote;
            Check(rep, "reputation", "俱乐部包含一线队", repN, facN, 0.5);
            Check(rep, "reputation", "声望在 0-10000 范围内", repOk, repN, 0.99);

            Need(rep, "staff", r.Staff.Count.ToString("N0", CultureInfo.InvariantCulture) + " 名职员（至少需要 100 人）", r.Staff.Count >= 100);
            var ss = r.Staff.Where(s => !s.ContractOnly).Where((x, i) => i % Math.Max(1, r.Staff.Count / 2000) == 0).ToList();
            Check(rep, "staff", "性格值在 1-20 范围内", ss.Count(s => { var pe = fm.Read(s.Pers + FmLayout.PersonalityAfterPerson, 8); return pe != null && pe.All(x => x >= 1 && x <= 20); }), ss.Count, 0.99);
            // swap: players are in their team's player list
            {
                int okN = 0, mvN = 0;
                foreach (var pl in r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 300) == 0))
                {
                    ulong po = pl.Tail - FmLayout.PlayerObjBeforePerson, t = fm.ReadPtr(po + FmLayout.PlayerTeam);
                    if (t < 0x10000 || fm.ReadPtr(t) != fm.Base + FmProcess.TeamVtRva) continue;
                    // players whose contract ended keep their old team pointer but are no longer in its list (free agents)
                    if (!HasContract(fm, pl.Person)) continue;
                    mvN++;
                    ulong b = fm.ReadPtr(t + FmLayout.TeamPlayers), e = fm.ReadPtr(t + FmLayout.TeamPlayers + 8);
                    if (b < 0x10000 || e < b || e - b > 8 * 500) continue;
                    var v = fm.Read(b, (int)(e - b));
                    for (int k = 0; v != null && k < v.Length / 8; k++) if (BitConverter.ToUInt64(v, 8 * k) == po) { okN++; break; }
                }
                Check(rep, "swap", "球员在所属球队名单中", okN, mvN, 0.95);
            }
            // staff contracts: same class as player contracts, pointing back at the staff member
            {
                int scWith = 0, scOk = 0;
                foreach (var s in ss.Where(x => !x.Manager))
                {
                    // some staff hold another kind of job here (a small record of another class, no wage): only contracts are judged
                    ulong c = fm.ReadPtr(s.Addr + FmLayout.StaffContract); if (c < 0x10000 || fm.ReadPtr(c) != fm.Base + FmProcess.ContractVtRva) continue;
                    scWith++;
                    var w = fm.ReadI32(c + FmLayout.ContractWage); var ex = fm.Read(c + FmLayout.ContractExpiry, 4);
                    if (fm.ReadPtr(c + 8) == s.Addr && w >= 0 && w < 10000000 && ex != null && BitConverter.ToUInt16(ex, 2) >= 1900 && BitConverter.ToUInt16(ex, 2) < 2200) scOk++;
                }
                Check(rep, "staffcontract", "职员合同的所属人、工资和到期日有效", scOk, scWith, 0.95);
            }
            // Full and basic staff contracts share the owner and team fields.
            if (StaffTransferPlanner.LayoutSupported)
            {
                int lN = 0, lOk = 0;
                foreach (var cl in r.Clubs.Where(c => c.Team != 0).OrderByDescending(c => c.HasFullFinance).Take(150))
                    foreach (var off in FmLayout.ClubStaffLists)
                    {
                        var header = fm.Read(cl.Addr + off, 24); if (header == null) continue;
                        ulong b = BitConverter.ToUInt64(header, 0), e = BitConverter.ToUInt64(header, 8), cap = BitConverter.ToUInt64(header, 16);
                        if (!StaffTransferPlanner.IsPointer(b) || e <= b || e > cap || cap - b > 8 * 512 || (e - b) % 8 != 0) continue;
                        var v = fm.Read(b, (int)(e - b)); if (v == null) continue;
                        for (int k = 0; k < Math.Min(v.Length / 8, 8); k++)
                        {
                            lN++;
                            ulong st = BitConverter.ToUInt64(v, 8 * k) + FmLayout.StaffListBack;
                            var employment = StaffEmployment.Read(fm.Read, fm.Base, fm.ImageSize, st);
                            if (employment != null && StaffTransferPlanner.IsPointer(employment.Team) &&
                                fm.ReadPtr(employment.Team + FmLayout.TeamClub2) == cl.Addr) lOk++;
                        }
                    }
                Check(rep, "staffmove", "俱乐部职员名单与合同所属俱乐部一致", lOk, lN, 0.95);
            }
            else Need(rep, "staffmove", "当前构建的职员转会结构尚未核实", false);
            // stadiums behind first teams: class matches, 0 <= used <= capacity < 250,000 (used 0 = the whole stadium, 159 of 489 clubs)
            {
                int sdN = 0, sdOk = 0;
                foreach (var c in fullClubs.Where(x => x.Team != 0))
                {
                    sdN++; ulong sd = fm.ReadPtr(c.Team + FmLayout.TeamStadium);
                    if (sd < 0x10000 || fm.ReadPtr(sd) != fm.Base + FmProcess.StadiumVtRva) continue;
                    int? cap = fm.ReadI32(sd + FmLayout.StadiumCapacity), used = fm.ReadI32(sd + FmLayout.StadiumUsed);
                    if (cap != null && used != null && used >= 0 && used <= cap && cap < 250000) sdOk++;
                }
                Check(rep, "stadium", "一线队的球场及容量有效", sdOk, sdN, 0.95);
            }
            // tickets: season ticket holders 0-300,000 and an average ticket price of £0-2,000
            bool checkedTickets = ClubFinanceCalibration.CheckedTickets(fm);
            Check(rep, "tickets", "季票数量和票价有效", fullClubs.Count(c =>
            {
                if(checkedTickets){try{CurrentTicketSnapshot.Capture(fm,c);return true;}catch{return false;}}
                if (c.Data < 0x10000) return false;
                int? stn = fm.ReadI32(c.Data + FmLayout.DataSeasonTickets); var f = fm.Read(c.Finance + FmLayout.FinTicketPrice, 4);
                if (stn == null || f == null) return false; float p = BitConverter.ToSingle(f, 0);
                return stn >= 0 && stn <= 300000 && p >= 0 && p <= 2000;
            }), fullClubs.Count, 0.95);
            // sponsorship deals: plausible dates and 0 <= value per season <= total
            {
                int dN = 0, dOk = 0;
                foreach (var c in fullClubs)
                    foreach (var d in MainForm.SponsorDeals(fm, c.Addr))
                    {
                        dN++; var b = fm.Read(d, 0x18); if (b == null) continue;
                        int y0 = BitConverter.ToUInt16(b, 2), y1 = BitConverter.ToUInt16(b, 6), tot = BitConverter.ToInt32(b, 8), sea = BitConverter.ToInt32(b, 12);
                        if (y0 >= 1900 && y0 <= y1 && y1 <= 2100 && sea >= 0 && sea <= tot) dOk++;
                    }
                Check(rep, "sponsor", "赞助合同有效", dOk, dN, 0.97);
                Need(rep, "sponsor", dN.ToString("N0", CultureInfo.InvariantCulture) + " 份赞助合同（至少需要 50 份）", dN >= 50);
            }
            // club names: length-prefixed strings
            Check(rep, "clubname", "俱乐部名称可读", fullClubs.Count(c => !string.IsNullOrEmpty(fm.ReadLenString(fm.ReadPtr(c.Addr + FmLayout.ClubName)))), fullClubs.Count, 0.99);
            Check(rep, "jaded", "疲劳度在 -500 至 5000 范围内", r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 3000) == 0).Count(pl => { var b = fm.Read(pl.Tail - FmLayout.JadedBeforePerson, 2); return b != null && BitConverter.ToInt16(b, 0) >= -500 && BitConverter.ToInt16(b, 0) <= 5000; }),
                r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 3000) == 0).Count(), 0.99);
            Check(rep, "prize", "赛事阶段指向有效规则", r.StageOk, r.StageCount, 0.9);
            Check(rep, "caps", "国家队进球不超过出场数", r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 3000) == 0).Count(pl => { var v = MainForm.ReadCaps(fm, pl.Person); return v != null && v[2] <= v[0] && v[3] <= v[1]; }),
                r.Players.Where((x, i) => i % Math.Max(1, r.Players.Count / 3000) == 0).Count(), 0.99);
            Need(rep, "prize", r.Prizes.Count.ToString("N0", CultureInfo.InvariantCulture) + " 份奖金表（最低数量为 " + FmLayout.PrizeTablesMin + ")", r.Prizes.Count >= FmLayout.PrizeTablesMin);
            Check(rep, "staffrep", "职员声望在 0-10000 范围内", ss.Count(s => FmLayout.StaffRepBefore.All(o => { var v = fm.Read(s.Addr - o, 2); return v != null && BitConverter.ToInt16(v, 0) >= 0 && BitConverter.ToInt16(v, 0) <= 10000; })), ss.Count, 0.99);
            // a profile that maps only part of the game keeps the rest read-only, whatever its checks said
            if (FmLayout.Groups.Length > 0)
                foreach (var g in Labels.Keys)
                    if (g != "core" && !FmLayout.Groups.Contains(g))
                    {
                        if (!Off.ContainsKey(g)) rep.Add("尚未映射：" + Zh.G(g) + " (" + Profile.Name + ")");
                        Off[g] = "not mapped for FM " + fm.Version.Split('+')[0] + " yet";
                    }
            return rep;
        }

        static string DateText(uint v) { return ValidPacked(v) ? new DateTime((int)(v >> 16), 1, 1).AddDays((v & 0x1FF) - 1).ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "invalid"; }

        // a partial profile whose mapped groups all passed: everything read-only is just not mapped yet
        public static bool Partial() { return FmLayout.Groups.Length > 0 && Off.Values.All(v => v.StartsWith("not mapped")); }

        // One line for the status bar
        public static string Summary()
        {
            if (Off.Count == 0) return FacilityNote.Length>0?"已通过项目可编辑。"+FacilityNote:"安全检查全部通过。";
            if (Off.ContainsKey("core")) return "只读：" + Off["core"] + "。";
            if (Partial()) return "已通过项目可编辑；尚未适配（只读）：" + string.Join("、", Off.Keys.Select(k => Zh.G(k)).ToArray()) + "。";
            return "已通过项目可编辑；只读项目：" + string.Join("、", Off.Keys.Select(k => Zh.G(k)).ToArray()) + "。详情见「连接详情…」。";
        }
    }

    class Change
    {
        public ulong Addr;
        public byte[] Old, New;
        public string What;
        public DateTime When;
        public bool Undone;
        public bool Growth;
        public bool Facility;
        public StaffTransferPlan StaffTransfer;
        public Func<bool> Guard;
        public int Group;       // writes that only make sense together (a player swap); undone together
        // the Change log list shows this; undone changes stay listed, marked
        public override string ToString()
        {
            return (Undone ? "[已撤销] " : "") + When.ToString("HH:mm:ss") + "  " + What + "  @" + Addr.ToString("X") + " " + BitConverter.ToString(Old) + " -> " + BitConverter.ToString(New);
        }
    }

    // A value kept from dropping below a floor by rewriting it twice a second (it may still go up, e.g. match income).
    // The guards are (address, expected bytes) pairs that identify the owning object; when one no longer matches,
    // FM has freed or moved it and the freeze stops.
    class Freeze
    {
        public ulong Addr;
        public byte[] Value;
        public bool Signed;
        public string What;
        public List<KeyValuePair<ulong, byte[]>> Guards = new List<KeyValuePair<ulong, byte[]>>();
        public int Rewrites;
        public override string ToString() { return What + "   （已恢复 " + Rewrites + " 次）"; }
    }

    // A player watched for CA growth. Growth follows a natural curve instead of a fixed step per day: it starts on
    // the day he is watched, runs for a target number of days (one or more seasons) and he is at his ceiling at the
    // end of it, like a youngster FM itself develops. Elapsed counts the in-game days already paid for.
    class GrowTarget
    {
        public ulong ScopeClub;
        public Player P;
        public int StartCa, Cap, Added, Elapsed, Span;
        public GrowthOptions Options = new GrowthOptions();
        public bool UseFocus;
        public GrowthFocusState Focus;
        public int AttributeRawAdded;
        public bool Done;
        public string Note = "";
        public override string ToString() { return P.DisplayName + "  CA " + StartCa + " → " + (StartCa + Added) + "，上限 " + Cap + (Note.Length > 0 ? "  (" + Note + ")" : ""); }
    }

    partial class MainForm : Form
    {
        public const string Version = "1.1.34";
        public const string HomePage = "https://sortitoutsi.net/content/78059/fm27eur0-v100-live-editor-for-fm27";
        FmProcess fm;
        bool forceCalibrate, readOnlyWarned;
        List<Player> players = new List<Player>();
        PlayerRosterIndex rosterIndex;
        bool fillingPlayerResults;
        long personDetailsRevision;
        PlayerRosterIndex RosterIndex { get { return rosterIndex ?? (rosterIndex = new PlayerRosterIndex(players)); } }
        List<Club> clubs = new List<Club>();
        List<Staff> staff = new List<Staff>();
        List<Change> changes = new List<Change>();
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "changes.log");

        Button btnScan = new Button { Text = "挂接并扫描", Width = 120 };
        readonly TextBox lblStatus = new TextBox
        {
            Name = "OperationFeedbackText", AccessibleName = "操作反馈全文", ReadOnly = true,
            Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
            Text = "先启动 FM27 并载入存档，然后点“挂接并扫描”。"
        };
        ProgressBar progress = new ProgressBar { Width = 200, Height = 18 };
        TextBox txtRate = new TextBox { Text = "1.3476", Width = 70 };   // FM27: $ per GBP (Finances: payroll budget / weekly wage budget / 52)
        TextBox txtCur = new TextBox { Text = "$", Width = 30 };
        Button btnCalibrate = new Button { Text = "货币...", Width = 85 };
        Label lblMoney = new Label { AutoSize = true, Padding = new Padding(10, 5, 0, 0) };
        Label lblStoredPounds = new Label { AutoSize = true, MaximumSize = new Size(470, 0), ForeColor = UiMuted };
        List<KeyValuePair<Label, string>> curLabels = new List<KeyValuePair<Label, string>>();

        TextBox txtPlayerSearch = NewSearchBox();
        ListBox lstPlayers = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        DataGridView gridAttrs = NewGrid();
        DataGridView gridPos = NewGrid();
        Label lblPlayer = new Label { Dock = DockStyle.Top, Height = 82, UseMnemonic = false, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold) };
        Button btnHeal = new Button { Text = "治疗伤病", Width = 100, Enabled = false };
        // buttons that only work when injuries or condition/morale are mapped (switched after each scan)
        Button btnHealAll, btnFresh, btnFreezeAll, btnFreeze;
        Button btnClauses = new Button { Text = "奖金与条款...", Width = 140, Enabled = false };
        Button btnDetails = new Button { Text = "详情与特质...", Width = 130 };
        Button btnCreate = new Button { Text = "新建球员...", Width = 120 };
        Button btnSwap = new Button { Text = "交换...", Width = 110, Enabled = false };
        Button btnMove = new Button { Text = "转到俱乐部...", Width = 120, Enabled = false };
        Button btnStaffMove;
        Button btnRelease = new Button { Text = "解约", Width = 80, Enabled = false };
        Dictionary<string, ulong> firstNames = new Dictionary<string, ulong>(), lastNames = new Dictionary<string, ulong>(), commonNames = new Dictionary<string, ulong>();
        Dictionary<ulong, string> nations = new Dictionary<ulong, string>();
        Dictionary<string, ulong> cities = new Dictionary<string, ulong>();
        Dictionary<ulong, string> cityNames = new Dictionary<ulong, string>();
        byte[] loadedBlock;
        int[] loadedStatus = new int[0];
        byte[] loadedPersonality;
        ulong loadedContract; int loadedWage; uint loadedExpiry;
        int loadedAbilityRow, loadedCa = -1, loadedPa = -1;
        int loadedTransferRow = -1, loadedAsk = -1;
        // extra number rows on the Players tab (height, weight, reputations): address, width in bytes, range, value when loaded
        class NumRow { public int Row; public string Name; public ulong Addr; public int Bytes, Min, Max, Old; }
        List<NumRow> loadedNums = new List<NumRow>();
        int loadedSquadRow = -1, loadedSquad = -1;
        byte loadedListFlags;
        Player loadedPlayer;

        TextBox txtClubSearch = NewSearchBox();
        ListBox lstClubs = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        Label lblClub = new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold) };
        TextBox txtBalance = new TextBox { Width = 160 }, txtTransfer = new TextBox { Width = 160 }, txtTransferNext = new TextBox { Width = 160 }, txtWage = new TextBox { Width = 160 };
        Label lblWageYear = new Label { AutoSize = true }, lblWageSpend = new Label { AutoSize = true };
        Club loadedClub;
        int?[] loadedFin;
        bool filteringClubs;
        NumericUpDown[] numFacility = FmLayout.FacilityNames.Select(x => new NumericUpDown { Minimum = 1, Maximum = 20, Width = 60 }).ToArray();
        int[] loadedFacility;
        NumericUpDown numReputation = new NumericUpDown { Minimum = 0, Maximum = 10000, Increment = 100, Width = 80 };
        int loadedReputation = -1;
        NumericUpDown numCapacity = new NumericUpDown { Minimum = 0, Maximum = 250000, Increment = 1000, Width = 90, ThousandsSeparator = true };
        NumericUpDown numUsedCap = new NumericUpDown { Minimum = 0, Maximum = 250000, Increment = 1000, Width = 90, ThousandsSeparator = true };
        NumericUpDown numSeasonTickets = new NumericUpDown { Minimum = 0, Maximum = 300000, Increment = 1000, Width = 90, ThousandsSeparator = true };
        TextBox txtTicketPrice = new TextBox { Width = 90 };
        int loadedSeasonTickets = -1; float loadedTicketPrice = -1;
        TextBox txtClubName = new TextBox { Width = 260 }, txtClubShort = new TextBox { Width = 160 };
        ulong loadedStadium; int loadedCapacity, loadedSeats, loadedUsed; string loadedClubName, loadedClubShort;

        SplitContainer splitPlayers, splitAttrs, splitClubs, splitPrize;
        TextBox txtSquadClubSearch = NewSearchBox();
        ListBox lstSquadClubs = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        ListBox lstSquad = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        Label lblSquad = new Label { Dock = DockStyle.Top, Height = 24, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold) };
        SplitContainer splitSquad;

        ListBox lstLog = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font(FontFamily.GenericMonospace, 9), SelectionMode = SelectionMode.MultiExtended, HorizontalScrollbar = true };

        TextBox txtStaffSearch = NewSearchBox();
        ListBox lstStaff = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        DataGridView gridStaff = NewGrid();
        Label lblStaff = new Label { Dock = DockStyle.Top, Height = 24, Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold) };
        SplitContainer splitStaff;
        Staff loadedStaff;
        byte[] loadedStaffBlock, loadedStaffPers;
        short[] loadedStaffRep;
        ulong loadedStaffContract; int loadedStaffWage, loadedStaffContractRow; uint loadedStaffExpiry;
        int staffRoleRow = -1;
        bool filteringStaff;

        List<Freeze> freezes = new List<Freeze>();
        ListBox lstFreeze = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
        Label lblFreeze = new Label { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4, 4, 4, 0), UseMnemonic = false };
        System.Windows.Forms.Timer freezeTimer = new System.Windows.Forms.Timer { Interval = 500 };
        TabPage freezePage;

        // CA growth (BuildGrowTab): players watched, plus the game date the last step was paid for
        TextBox txtGrowClubSearch = NewSearchBox();
        ListBox lstGrowClubs = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        DataGridView gridGrow = NewGrid();
        Label lblGrow = new Label { Dock = DockStyle.Top, Height = 46, Padding = new Padding(4, 4, 4, 0), UseMnemonic = false };
        // nine speed gears (2x-10x, default 4x); K (in-game days per CA point) = 40 / gear
        ComboBox cmbGrowSpeed = new ComboBox { Width = 84, DropDownStyle = ComboBoxStyle.DropDownList };
        // the bottom control row, locked while growth is running
        FlowLayoutPanel growButtons;
        Button btnGrowToggle = new Button { Text = "开始成长", Width = 130 };
        SplitContainer splitGrow;
        Dictionary<ulong, GrowTarget> growWatch = new Dictionary<ulong, GrowTarget>();
        bool growOn, growFilling;
        long growDay;
        TabPage growPage, squadPage;
        bool squadRosterDirty, fillingSquadClubs;
        // Monthly refresh rebinds existing watched players; only explicit user selection can add a target.
        // automatically afterwards; this is always on, no switch in the UI
        bool autoScanning;
        long autoMonthKey;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        static TextBox NewSearchBox()
        {
            return new TextBox { Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 9.5F) };
        }

        // Grey hint text shown while the box is empty (EM_SETCUEBANNER)
        static void SetHint(TextBox t, string hint) { SendMessage(t.Handle, 0x1501, (IntPtr)1, hint); }

        static WorkspaceGrid NewGrid()
        {
            var g = new WorkspaceGrid
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            StyleWorkspaceGrid(g);
            return g;
        }

        public MainForm(bool autoScan, bool calibrate, GrowthPreferenceStore savedGrowthPreferences = null)
        {
            this.savedGrowthPreferences = savedGrowthPreferences;
            PlayerNames.Load(AppDomain.CurrentDomain.BaseDirectory, false);
            forceCalibrate = calibrate;
            Text = "FM27E0 汉化修改器 v" + Version;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Font=UiFont;
            AutoScaleMode=AutoScaleMode.Dpi;
            var workArea=Screen.PrimaryScreen.WorkingArea;
            MinimumSize=new Size(Math.Min(1060,workArea.Width-24),Math.Min(690,workArea.Height-24));
            Size=new Size(Math.Min(1440,workArea.Width-32),Math.Min(900,workArea.Height-32));
            BackColor=UiSurface;ForeColor=UiInk;
            StartPosition = FormStartPosition.CenterScreen;

            var top = BuildHeader();

            var tabs = new ContentTabs { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildPlayersTab());
            tabs.TabPages.Add(BuildStaffTab());
            tabs.TabPages.Add(BuildClubsTab());
            tabs.TabPages.Add(squadPage = BuildSquadTab());
            tabs.TabPages.Add(growPage = BuildGrowTab());
            tabs.SelectedIndexChanged+=delegate
            {
                if(tabs.SelectedTab==squadPage && squadRosterDirty)LoadSquad();
                if(tabs.SelectedTab==growPage && growthRosterDirty)LoadGrow();
                else if(tabs.SelectedTab==growPage && growthValuesDirty)RefreshGrow();
            };
            tabs.TabPages.Add(BuildFinderTab());
            tabs.TabPages.Add(BuildStaffFinderTab());
            tabs.TabPages.Add(BuildPrizeTab());
            tabs.TabPages.Add(freezePage = BuildFreezeTab());
            tabs.TabPages.Add(BuildLogTab());
            freezeTimer.Tick += delegate { FreezeTick(); GrowTick(); };
            freezeTimer.Start();

            var foot = new TableLayoutPanel { Name="WorkspaceFooter", Dock=DockStyle.Bottom, ColumnCount=2, RowCount=1,
                AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink };
            foot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foot.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // the licence asks for the author's credit and the original download link, not for the donation link,
            // so the Ko-fi button the original window carried is not here
            var credit = new LinkLabel { Name="OriginalAuthorCredit", Text="Original by Anze Alexander (sortitoutsi.net)",
                AutoSize=true, Anchor=AnchorStyles.Right, Padding=new Padding(0,4,12,0) };
            credit.LinkClicked += delegate { try { Process.Start(HomePage); } catch { } };
            foot.Controls.Add(new Label
            {
                Name="SaveReminder", AutoSize=true, Dock=DockStyle.Fill, Padding=new Padding(6,4,4,0),
                Text = "修改随 FM 保存写入存档；部分信息需重新打开资料或推进日期后刷新。"
            },0,0);
            foot.Controls.Add(credit,1,0);

            Controls.Add(BuildWorkspace(tabs));
            Controls.Add(top);
            Controls.Add(foot);
            Controls.Add(BuildStatusBar());

            btnScan.Click += delegate { StartScan(); };
            if (autoScan) Shown += delegate { StartScan(); };
            LoadSettings();
            LoadGrowthPreferences();
            RefreshCurrency();
            Zh.Tree(this);   // Chinese labels for everything built above
            PolishWorkspace(this);



            btnCalibrate.Click += delegate { Calibrate(); };
            FormClosed += delegate { SaveSettings(); };
            Load += delegate
            {
                FitWindowToWorkArea(this,this);
                SetHint(txtPlayerSearch, "中文 / 英文 / UID（如 #735216）");
                SetHint(txtClubSearch, ClubSearch.Cue);
                SetInitialSplitter(splitPlayers,260);
                SetInitialSplitter(splitClubs,230);
                SetInitialSplitter(splitStaff,260);
                SetHint(txtStaffSearch, "姓名 / 俱乐部 / UID…");
                SetInitialSplitter(splitSquad,230);
                SetHint(txtGrowClubSearch, ClubSearch.Cue);
                SetHint(txtGrowPlayerSearch, "搜索球员姓名或 UID…");
                SetInitialSplitter(splitGrow,194);
                LayoutGrowthWorkspace();
                SetInitialSplitter(splitPrize,380);
                SetHint(txtSquadClubSearch, ClubSearch.Cue);
                SetInitialSplitter(splitAttrs,Math.Max(200,splitAttrs.Width-290),0.64);
                Zh.Tree(this);
            };
        }

        void SetInitialSplitter(SplitContainer split,int preferred,double maximumFraction=0.38)
        {
            int available=split.ClientSize.Width-split.SplitterWidth;
            int upper=available-split.Panel2MinSize;
            if(upper<split.Panel1MinSize)return;
            int scaled=LayoutPixels(preferred);
            split.SplitterDistance=Math.Max(split.Panel1MinSize,Math.Min(upper,
                Math.Min(scaled,(int)(available*maximumFraction))));
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing && !closingEditor)
            {
                closingEditor=true;
                freezeTimer.Dispose();
                growthBatchTimer.Dispose();
                rosterWorker.Close(fm);
            }
            base.Dispose(disposing);
        }

        TabPage BuildPlayersTab()
        {
            var page = new TabPage("球员");
            var split = splitPlayers = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(lstPlayers);
            split.Panel1.Controls.Add(SearchWithListSpacing(txtPlayerSearch));
            split.Panel1.Controls.Add(new Label { Text = "球员搜索：中文 / 英文 / UID", Dock = DockStyle.Top, Height = 24, Padding = new Padding(0, 4, 0, 0) });

            gridAttrs.Columns.Add("attr", "属性");
            gridAttrs.Columns.Add("value", "数值（1-20）");
            gridAttrs.Columns.Add("raw", "原始值（1-100）");
            gridAttrs.Columns.Add("note", "备注");
            gridAttrs.Columns[0].ReadOnly = true; gridAttrs.Columns[2].ReadOnly = true; gridAttrs.Columns[3].ReadOnly = true;
            gridAttrs.Columns[0].FillWeight = 140;
            gridAttrs.Columns[1].FillWeight = 120;
            gridAttrs.Columns[3].FillWeight = 90;

            gridPos.Columns.Add("pos", "位置 / 状态");
            gridPos.Columns.Add("value", "数值");
            gridPos.Columns[0].ReadOnly = true;
            gridPos.Columns[0].FillWeight = 170;
            gridPos.Columns[1].FillWeight = 75;

            var right = splitAttrs = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            right.Panel1.Controls.Add(gridAttrs);
            right.Panel2.Controls.Add(gridPos);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(4) };
            var btnApply = btnApplyPlayer;
            var btnReload = new Button { Text = "重新加载", Width = 80 };
            var btnMax = new Button { Name = "FillVerifiedPlayerAttributes", Text = "将全部已验证基础属性填至极值" };
            buttons.Controls.Add(btnApply); buttons.Controls.Add(btnReload); buttons.Controls.Add(btnMax); buttons.Controls.Add(btnHeal); buttons.Controls.Add(btnClauses); buttons.Controls.Add(btnDetails); buttons.Controls.Add(btnCreate); buttons.Controls.Add(btnSwap); buttons.Controls.Add(btnMove); buttons.Controls.Add(btnRelease); buttons.Controls.Add(btnRetirement); buttons.Controls.Add(btnPlayerAge);
            btnPlayerAge.Click += delegate { if (loadedPlayer != null) OpenBirthdayDialog(loadedPlayer.Person, loadedPlayer.DisplayName); };
            btnRetirement.Click += delegate { OpenRetirementDialog(); };
            btnFreeze = new Button { Text = "冻结体能与心情", Width = 130 };
            buttons.Controls.Add(btnFreeze);
            BuildPlayerActions(buttons,btnApply,btnReload,btnMax);
            btnFreeze.Click += delegate
            {
                if (loadedPlayer == null) return;
                int n = FreezeStatus(loadedPlayer);
                lblStatus.Text = loadedPlayer.DisplayName + ": " + n + " 项数值已冻结为 100%，详见「冻结」页。";
            };

            split.Panel2.Controls.Add(right);
            split.Panel2.Controls.Add(BuildPlayerAttributeToolbar());
            split.Panel2.Controls.Add(buttons);
            WrapDockedLabel(lblPlayer);
            split.Panel2.Controls.Add(lblPlayer);
            page.Controls.Add(split);

            txtPlayerSearch.TextChanged += delegate { FilterPlayers(); };
            lstPlayers.SelectedIndexChanged += delegate { if(!fillingPlayerResults)LoadPlayer(lstPlayers.SelectedItem as Player); };
            btnReload.Click += delegate { LoadPlayer(loadedPlayer ?? lstPlayers.SelectedItem as Player); };
            btnApply.Click += delegate { ApplyPlayer(); };
            btnHeal.Click += delegate { HealPlayer(); };
            btnClauses.Click += delegate { EditClauses(); };
            btnDetails.Click += delegate { EditDetails(); };
            btnCreate.Click += delegate { CreatePlayer(); };
            btnSwap.Click += delegate { SwapPlayerDialog(); };
            btnMove.Click += delegate { MovePlayerDialog(); };
            btnRelease.Click += delegate { ReleasePlayer(loadedPlayer); };
            btnMax.Click += delegate
            {
                for (int k = 0; k < Math.Min(FmLayout.AttrCount, gridAttrs.Rows.Count); k++)
                    if ((string)gridAttrs.Rows[k].Cells[3].Value == "已验证") gridAttrs.Rows[k].Cells[1].Value = FmLayout.LowerIsBetter(FmLayout.Attrs[k]) ? 1 : 20;
            };
            return page;
        }

        TabPage BuildClubsTab()
        { return BuildClubManagementTab(); }

        void AddCurRow(TableLayoutPanel t, int row, string template, Control c)
        {
            t.Controls.Add(CurLabel(new Label { AutoSize = true, Padding = new Padding(0, 6, 10, 0) }, template), 0, row);
            t.Controls.Add(c, 1, row);
        }

        static void AddRow(TableLayoutPanel t, int row, string label, Control c)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 10, 0) }, 0, row);
            t.Controls.Add(c, 1, row);
        }

        TabPage BuildSquadTab()
        {
            var page = new TabPage("球队");
            var split = splitSquad = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(lstSquadClubs);
            split.Panel1.Controls.Add(SearchWithListSpacing(txtSquadClubSearch));
            split.Panel1.Controls.Add(new Label { Text = "俱乐部：", Dock = DockStyle.Top, Height = 22, Padding = new Padding(0, 4, 0, 0) });

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(4) };
            btnHealAll = new Button { Text = "治疗所有伤病", Width = 130 };
            var btnMaxAll = new Button { Text = "已验证属性全拉满", Width = 190 };
            btnFresh = new Button { Text = "体能、状态与心情拉满", Width = 230 };
            buttons.Controls.Add(btnHealAll); buttons.Controls.Add(btnMaxAll); buttons.Controls.Add(btnFresh);
            btnFreezeAll = new Button { Text = "冻结全队体能、状态与心情", Width = 220 };
            buttons.Controls.Add(btnFreezeAll);
            btnFreezeAll.Click += delegate
            {
                var squad = SquadPlayers();
                if (squad.Count == 0) return;
                int n = 0;
                foreach (var p in squad) n += FreezeStatus(p);
                lblStatus.Text = n + " 项数值已冻结为 100%，涉及球员数：" + squad.Count + "。详见「冻结」页。";
            };
            btnFresh.Click += delegate
            {
                int done = 0;
                foreach (var p in SquadPlayers())
                {
                    var s = ReadStatus(p); bool any = false;
                    for (int k = 0; k < s.Length; k++)
                        if (s[k] >= 0 && s[k] < FmLayout.StatusMax[k] && WriteStatus(p, k, s[k], FmLayout.StatusMax[k], p.Name + ": " + FmLayout.StatusNames[k] + " -> 100")) any = true;
                    if (Rest(p)) any = true;
                    if (any) done++;
                }
                lblStatus.Text = done + " 名球员已恢复。士气立即显示，体能和比赛状态在下一游戏日显示。";
            };
            BuildSquadActions(buttons,btnHealAll,btnMaxAll,btnFresh,btnFreezeAll);
            btnRefreshSquad.Click+=delegate{RefreshLocalClub(lstSquadClubs.SelectedItem as Club);};
            split.Panel2.Controls.Add(lstSquad);
            split.Panel2.Controls.Add(buttons);
            WrapDockedLabel(lblSquad);
            split.Panel2.Controls.Add(lblSquad);
            page.Controls.Add(split);

            txtSquadClubSearch.TextChanged += delegate { FilterSquadClubs(); };
            lstSquadClubs.SelectedIndexChanged += delegate { if(!fillingSquadClubs){LoadSquad();UpdateRosterRefreshButtons();} };
            lstSquad.DoubleClick += delegate
            {
                var row = lstSquad.SelectedItem as SquadRow; if (row == null) return;
                txtPlayerSearch.Text = row.P.Name; ((TabControl)page.Parent).SelectedIndex = 0;
                lstPlayers.SelectedItem = row.P; LoadPlayer(row.P);
            };
            btnHealAll.Click += delegate
            {
                int healed = 0;
                foreach (var p in SquadPlayers()) if (Heal(p)) healed++;
                LoadSquad();
                lblStatus.Text = healed + " 名球员已治疗，在 FM 中推进一天后查看。";
            };
            btnMaxAll.Click += delegate
            {
                var squad = SquadPlayers();
                if (squad.Count == 0) return;
                if (MessageBox.Show("将全部 " + FmLayout.Attrs.Count(x => x.EndsWith("|V")) + " 项已验证属性拉到 20？涉及球员数：" + squad.Count + " 人。",
                        "属性拉满", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                int done = 0;
                foreach (var p in squad) if (MaxAttributes(p)) done++;
                lblStatus.Text = done + " 名球员属性已拉满，在 FM 中推进一天后查看。";
            };
            return page;
        }

        class SquadRow
        {
            public Player P; public int Injuries; public string Loan;
            public override string ToString() { return P.DisplayName + (Loan=="租入"?"   [租入]":"") + (Injuries > 0 ? "   [受伤]" : ""); }
        }

        List<Player> SquadPlayers()
        {
            var c = lstSquadClubs.SelectedItem as Club;
            return c == null ? new List<Player>() : RosterIndex.ForClub(c.Addr).ToList();
        }

        void FilterSquadClubs()
        {
            var selected=lstSquadClubs.SelectedItem as Club;
            var q = txtSquadClubSearch.Text.Trim();
            fillingSquadClubs=true;
            lstSquadClubs.BeginUpdate();
            try
            {
                lstSquadClubs.Items.Clear();
                lstSquadClubs.Items.AddRange(clubs.Where(c=>ClubSearch.Matches(c,q)).ToArray());
                if(selected!=null)lstSquadClubs.SelectedItem=lstSquadClubs.Items.Cast<Club>().FirstOrDefault(c=>c.Addr==selected.Addr);
            }
            finally{lstSquadClubs.EndUpdate();fillingSquadClubs=false;}
            LoadSquad();
            UpdateRosterRefreshButtons();
        }

        bool RosterPageHidden(TabPage page)
        {
            var tabs=page==null?null:page.Parent as TabControl;
            return IsHandleCreated && tabs!=null && tabs.SelectedTab!=page;
        }

        void LoadSquad()
        {
            if (fm == null) return;
            if(RosterPageHidden(squadPage)){squadRosterDirty=true;return;}
            squadRosterDirty=false;
            var squad = SquadPlayers();
            var selected=lstSquad.SelectedItem as SquadRow;
            var c = lstSquadClubs.SelectedItem as Club;
            lstSquad.BeginUpdate();
            lstSquad.Items.Clear();
            foreach (var p in squad.OrderBy(x => x.Name))
                lstSquad.Items.Add(new SquadRow { P=p, Injuries=InjuryCount(p), Loan=GrowthRosterView.LoanLabel(p,c.Addr) });
            if(selected!=null)lstSquad.SelectedItem=lstSquad.Items.Cast<SquadRow>().FirstOrDefault(row=>row.P.Uid==selected.P.Uid);
            lstSquad.EndUpdate();
            lblSquad.Text = c == null ? "" : c.Name + ": " + squad.Count + " 名球员，" + lstSquad.Items.Cast<SquadRow>().Count(r => r.Injuries > 0) + " 人受伤（双击球员编辑）";
        }

        bool MaxAttributes(Player p)
        {
            var b = fm.Read(p.Block, FmLayout.PosCount + FmLayout.AttrCount);
            if (!FmLayout.ValidBlock(b)) return false;
            var attrs = new byte[FmLayout.AttrCount];
            Array.Copy(b, FmLayout.AttrStart, attrs, 0, attrs.Length);
            var updated = (byte[])attrs.Clone();
            for (int k = 0; k < FmLayout.AttrCount; k++) if (FmLayout.Attrs[k].EndsWith("|V")) updated[k] = (byte)(FmLayout.LowerIsBetter(FmLayout.Attrs[k]) ? 5 : 100);
            if (updated.SequenceEqual(attrs)) return false;
            return WriteLogged(p.Block + (ulong)FmLayout.AttrStart, updated, p.Name + "：已验证属性全部调整至 20");
        }

        // ---------- player finder ----------
        // Read-only search over every player FM has loaded. Values are read fresh when Search is clicked.

        NumericUpDown fAgeMin = new NumericUpDown { Minimum = 14, Maximum = 45, Value = 15, Width = 50 }, fAgeMax = new NumericUpDown { Minimum = 14, Maximum = 45, Value = 45, Width = 50 };
        NumericUpDown fCa = new NumericUpDown { Minimum = 0, Maximum = 200, Width = 55 }, fPa = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 150, Width = 55 };
        ComboBox fPos = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        TextBox fNation = new TextBox { Width = 110 }, fClub = new TextBox { Width = 130 };
        NumericUpDown fExpires = new NumericUpDown { Minimum = 0, Maximum = 2100, Width = 60 };
        CheckBox fListed = new CheckBox { Text = "已挂牌", AutoSize = true }, fFree = new CheckBox { Text = "自由球员", AutoSize = true };
        WorkspaceGrid gridFind = NewGrid();
        Label lblFind = new Label { Dock = DockStyle.Top, Height = 22, Padding = new Padding(4, 4, 0, 0), UseMnemonic = false };

        // ---------- prize money ----------
        List<PrizeTable> prizes = new List<PrizeTable>();
        ListBox lstPrizes = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        TextBox txtPrizeSearch = new TextBox { Dock = DockStyle.Top };
        DataGridView gridPrize = new WorkspaceGrid { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false };
        PrizeTable loadedPrize; int[] loadedPrizeVals;

        TabPage BuildPrizeTab()
        {
            var page = new TabPage("赛事与报名");
            var split = splitPrize = new SplitContainer { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(lstPrizes); split.Panel1.Controls.Add(SearchWithListSpacing(txtPrizeSearch));
            split.Panel1.Controls.Add(new Label { Text = "搜索赛事：", Dock = DockStyle.Top, Height = 22, Padding = new Padding(0, 4, 0, 0) });
            gridPrize.Columns.Add("pos", "名次"); gridPrize.Columns.Add("amount", "奖金");
            gridPrize.Columns[0].ReadOnly = true;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(4) };
            var btnApply = new Button { Text = "应用修改", Width = 120 };
            StyleButton(btnApply,WorkspaceButtonRole.Primary);
            var btnReload = new Button { Text = "重新加载", Width = 80 };
            buttons.Controls.Add(btnApply); buttons.Controls.Add(btnReload);
            var note = new Label { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(4), ForeColor = UiMuted,
                Text = "各名次（联赛）或各阶段名次（杯赛）的奖金，跟 FM 在赛事规则里显示的一致。" +
                       "改了会影响这项赛事里的所有俱乐部。金额用你设的货币显示，FM 内部按英镑存。" };
            WrapDockedLabel(note);
            split.Panel2.Controls.Add(gridPrize); split.Panel2.Controls.Add(buttons); split.Panel2.Controls.Add(note);
            page.Controls.Add(split);
            txtPrizeSearch.TextChanged += delegate { FilterPrizes(); };
            lstPrizes.SelectedIndexChanged += delegate { LoadPrize(lstPrizes.SelectedItem as PrizeTable); };
            btnReload.Click += delegate { LoadPrize(loadedPrize); };
            btnApply.Click += delegate { ApplyPrize(); };
            MountRegistrationWorkspace(page,split);
            return page;
        }

        void FilterPrizes()
        {
            var q = txtPrizeSearch.Text.Trim();
            lstPrizes.BeginUpdate(); lstPrizes.Items.Clear();
            foreach (var p in prizes.Where(x => q.Length == 0 || x.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0)) lstPrizes.Items.Add(p);
            lstPrizes.EndUpdate();
        }

        int[] ReadPrizeVals(PrizeTable p)
        {
            ulong b0 = fm.ReadPtr(p.Vector), e0 = fm.ReadPtr(p.Vector + 8);
            if (b0 < 0x10000 || e0 <= b0 || e0 - b0 > 400) return null;
            var raw = fm.Read(b0, (int)(e0 - b0)); if (raw == null) return null;
            var v = new int[raw.Length / 4]; for (int k = 0; k < v.Length; k++) v[k] = BitConverter.ToInt32(raw, 4 * k);
            return v;
        }

        static string Ordinal(int n) { int m = n % 100; string suf = m >= 11 && m <= 13 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th"; return n + suf; }

        void LoadPrize(PrizeTable p)
        {
            if (p == null || fm == null) return;
            var v = ReadPrizeVals(p);
            if (v == null) { MessageBox.Show("这项赛事的数据已经移位，请重新点「挂接并扫描」。", "奖金"); return; }
            loadedPrize = p; loadedPrizeVals = v;
            gridPrize.Columns[1].HeaderText = "奖金（" + Cur() + ")";
            gridPrize.Rows.Clear();
            for (int k = 0; k < v.Length; k++) gridPrize.Rows.Add(Ordinal(k + 1), Money(v[k] * Rate()));
            gridPrize.Columns[1].ReadOnly = !Safety.On("prize");
        }

        void ApplyPrize()
        {
            if (loadedPrize == null || loadedPrizeVals == null) return;
            if (!Safety.On("prize"))
            {
                ShowOperationMessage(FeedbackKind.ReadOnly,"这个 fm.exe 的奖金数据是只读的。","奖金");
                return;
            }
            var cur = ReadPrizeVals(loadedPrize);
            ulong b0 = fm.ReadPtr(loadedPrize.Vector);
            int written = 0, skipped = 0, failed = 0, laterOnly = 0; double r = Rate();
            for (int k = 0; k < loadedPrizeVals.Length && k < gridPrize.Rows.Count; k++)
            {
                string t = Convert.ToString(gridPrize.Rows[k].Cells[1].Value); double money;
                if (t == Money(loadedPrizeVals[k] * r) || !ParseMoney(t, out money)) continue;
                int pounds = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(money / r)));
                if (pounds == loadedPrizeVals[k]) continue;
                if (cur == null || k >= cur.Length || cur[k] != loadedPrizeVals[k]) { skipped++; continue; }
                string what = loadedPrize.Name + ": " + Ordinal(k + 1) + " 名奖金 £" + loadedPrizeVals[k].ToString("N0", CultureInfo.InvariantCulture) + " -> £" + pounds.ToString("N0", CultureInfo.InvariantCulture);
                if (!WriteLogged(b0 + (ulong)(4 * k), BitConverter.GetBytes(pounds), what)) { failed++; continue; }
                written++;
                // FM27 pays this season from its own copy, which only lists the places that pay something
                bool thisSeason = loadedPrize.SeasonVectors.Count == 0;
                foreach (var sv in loadedPrize.SeasonVectors)
                {
                    ulong sb = fm.ReadPtr(sv), se = fm.ReadPtr(sv + 8);
                    if (sb < 0x10000 || (ulong)(4 * k + 4) > se - sb) continue;
                    var old = fm.ReadI32(sb + (ulong)(4 * k));
                    if (old == loadedPrizeVals[k] && WriteLogged(sb + (ulong)(4 * k), BitConverter.GetBytes(pounds), what + "（本赛季）")) thisSeason = true;
                }
                if (!thisSeason) laterOnly++;
            }
            LoadPrize(loadedPrize);
            ShowOperationFeedback(WriteFeedbackKind(written,skipped+failed),
                written + " 项奖金已修改" + (skipped > 0 ? "，" + skipped + " 项因游戏数据变化而跳过" : "") +
                (failed > 0 ? "，" + failed + " 项写入失败" : "") +
                (laterOnly > 0 ? ". " + laterOnly + " 项从下赛季起生效（本赛季仅能修改原本设有奖金的名次）" : "") +
                "。" + (written > 0 ? "在下一游戏日的赛事规则界面查看。" : ""));
        }

        TabPage BuildFinderTab()
        {
            var page = new TabPage("查找球员");
            fPos.Items.Add("不限"); fPos.Items.AddRange(FmLayout.Positions); fPos.SelectedIndex = 0;
            var bar=WrappingBar("PlayerFinderFilters");
            bar.Controls.Add(FieldGroup(FieldLabel("姓名（中文／英文）"),fName));
            bar.Controls.Add(FieldGroup(FieldLabel("年龄"),fAgeMin,FieldLabel("到"),fAgeMax));
            bar.Controls.Add(FieldGroup(FieldLabel("位置"),fPos));
            bar.Controls.Add(FieldGroup(FieldLabel("CA 不低于"),fCa));
            bar.Controls.Add(FieldGroup(FieldLabel("PA 不低于"),fPa));
            bar.Controls.Add(FieldGroup(FieldLabel("国籍"),fNation));
            bar.Controls.Add(FieldGroup(FieldLabel("俱乐部"),fClub));
            bar.Controls.Add(FieldGroup(FieldLabel("合同在（年，0 为不限）"),fExpires));
            bar.Controls.Add(FieldGroup(fListed,fFree));
            var btn = new Button { Name="SearchPlayers", Text="搜索", Width=90 };
            bar.Controls.Add(btn);
            foreach (var c in new[] { "姓名", "年龄", "俱乐部", "国籍", "位置", "CA", "PA", "合同到期", "年薪（" + Cur() + "／年）", "已挂牌" }) gridFind.Columns.Add(c, c);
            gridFind.ReadOnly = true; gridFind.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridFind.Columns[0].FillWeight = 170; gridFind.Columns[2].FillWeight = 140;
            gridFind.Columns[8].DefaultCellStyle.Format = "N0"; gridFind.Columns[8].DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            lblFind.Text = "最多显示 1,000 名球员，按潜力排序。选中结果后点击「打开资料」、双击或按 Enter。";
            WrapDockedLabel(lblFind);
            page.Controls.Add(gridFind); page.Controls.Add(lblFind); page.Controls.Add(bar);
            OrganizeFinderToolbar(bar,btn);
            btn.Click += delegate { RunFinder(btn); };
            AddFinderOpenButton<Player>(bar,btn,gridFind,"OpenPlayerFinderResult",p=>
            {
                txtPlayerSearch.Text = p.Name; ((TabControl)page.Parent).SelectedIndex = 0;
                lstPlayers.SelectedItem = p; LoadPlayer(p);
            });
            return page;
        }

        // Negative PA is FM's random range: -n means 2n-30 to 2n (e.g. -95 = 160-190), settled when the game starts
        static int PaTop(int pa) { return pa > 0 ? pa : Math.Min(200, -2 * pa); }

        TextBox fName = new TextBox { Width = 155 };
        class FindRow { public Player P; public int Age, Ca, Pa, Wage; public string Club, Nation, Pos, Ends; public bool Listed; }

        void RunFinder(Button btn)
        {
            if (fm == null || players.Count == 0) { MessageBox.Show("请先点「挂接并扫描」。", "查找球员"); return; }
            int ageMin = (int)fAgeMin.Value, ageMax = (int)fAgeMax.Value, caMin = (int)fCa.Value, paMin = (int)fPa.Value, pos = fPos.SelectedIndex - 1, endYear = (int)fExpires.Value;
            string nameQuery = fName.Text.Trim();
            if (nameQuery.Length > 0 && !PlayerNames.CanSearchPlayer(nameQuery)) { MessageBox.Show("姓名请至少输入 2 个字符；一字中文名须为已收录的完整姓名。", "查找球员"); return; }
            string nat = fNation.Text.Trim(), club = fClub.Text.Trim(); bool listed = fListed.Checked, free = fFree.Checked;
            uint today = Safety.On("date") ? (fm.ReadGameDate() ?? 0) : 0;
            DateTime now = today != 0 ? FromPacked(today) : DateTime.Today;
            var all = players.ToList(); var natNames = nations; double rate = Rate();
            btn.Enabled = false; lblFind.Text = "正在搜索，共 " + all.Count.ToString("N0", CultureInfo.InvariantCulture) + " 名球员……";
            var th = new Thread(() =>
            {
                var res = new List<FindRow>(); var clubName = new Dictionary<ulong, string>();
                foreach (var p in all)
                {
                    if (nameQuery.Length > 0 && !p.Matches(nameQuery)) continue;
                    if (!p.Born.HasValue) continue;
                    int age = Birthdays.AgeOn(p.Born.Value, now);
                    if (age < ageMin || age > ageMax) continue;
                    var ab = fm.Read(p.Tail - FmLayout.AbilityBeforePerson, 4); if (ab == null) continue;
                    int ca = BitConverter.ToInt16(ab, 0), pa = BitConverter.ToInt16(ab, 2);
                    if (ca < caMin || PaTop(pa) < paMin) continue;
                    var b = fm.Read(p.Block, FmLayout.PosCount); if (b == null) continue;
                    if (pos >= 0 && b[pos] < 15) continue;
                    if (free && p.Club != 0) continue;
                    string cn = "";
                    if (p.Club != 0 && !clubName.TryGetValue(p.Club, out cn)) clubName[p.Club] = cn = fm.ReadLenString(fm.ReadPtr(p.Club + FmLayout.ClubName)) ?? "";
                    if (club.Length > 0 && cn.IndexOf(club, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                    string nn; natNames.TryGetValue(fm.ReadPtr(p.Person + FmLayout.Nation), out nn); nn = nn ?? "";
                    if (nat.Length > 0 && nn.IndexOf(nat, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                    ulong c = Safety.On("contract") ? fm.ReadPtr(p.Person + FmLayout.ContractAfterPerson) : 0;
                    byte[] cb = c > 0x10000 && fm.ReadPtr(c) == fm.Base + FmProcess.ContractVtRva && fm.ReadPtr(c + 8) == p.Person ? fm.Read(c, (int)FmLayout.ContractListFlags + 1) : null;
                    DateTime? ends = cb == null ? (DateTime?)null : FromPacked(BitConverter.ToUInt32(cb, (int)FmLayout.ContractExpiry));
                    if (endYear > 0 && (ends == null || ends.Value.Year > endYear)) continue;
                    bool isListed = cb != null && Safety.On("transfer") && (cb[FmLayout.ContractListFlags] & 1) != 0;
                    if (listed && !isListed) continue;
                    var ps = Enumerable.Range(0, FmLayout.PosCount).Where(k => b[k] >= 15).OrderByDescending(k => b[k]).Select(k => Zh.Z(FmLayout.Positions[k])).Take(4).ToArray();
                    res.Add(new FindRow { P = p, Age = age, Ca = ca, Pa = pa, Club = p.Club == 0 ? "（自由球员）" : cn, Nation = nn, Pos = string.Join(" ", ps), Listed = isListed,
                        Ends = ends == null ? "" : ends.Value.ToString("yyyy-MM-dd"), Wage = cb == null ? 0 : BitConverter.ToInt32(cb, (int)FmLayout.ContractWage) });
                }
                var top = res.OrderByDescending(r => PaTop(r.Pa)).ThenByDescending(r => r.Ca).Take(1000).ToList();
                BeginInvoke((Action)(() =>
                {
                    gridFind.Rows.Clear();
                    gridFind.Columns[8].HeaderText = "年薪（" + Cur() + "／年）";
                    foreach (var r in top)
                    {
                        // numbers stay numbers so the column headers sort them correctly
                        int i = gridFind.Rows.Add(r.P.DisplayName, r.Age, r.Club, r.Nation, r.Pos, r.Ca, PaTop(r.Pa), r.Ends, r.Wage > 0 ? Math.Round(r.Wage * rate * 52) : (object)null, r.Listed ? "是" : "");
                        gridFind.Rows[i].Tag = r.P;
                        if (r.Pa < 0) gridFind.Rows[i].Cells[6].ToolTipText = "随机区间 " + Math.Max(1, -2 * r.Pa - 30) + "-" + PaTop(r.Pa) + "（存储值为 " + r.Pa + ")";
                    }
                    lblFind.Text = res.Count.ToString("N0", CultureInfo.InvariantCulture) + " 名球员符合条件" + (res.Count > 1000 ? "，显示排名靠前的 1000 人" : "") + "。选中结果后可打开资料。";
                    btn.Enabled = true;
                }));
            });
            th.IsBackground = true; th.Start();
        }

        // ---------- staff finder ----------
        // Read-only search over every coach, scout, physio and analyst, by job and by up to two attributes.

        ComboBox sfJob = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
        ComboBox sfAttr1 = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList }, sfAttr2 = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
        NumericUpDown sfMin1 = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 15, Width = 45 }, sfMin2 = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 1, Width = 45 };
        NumericUpDown sfAgeMin = new NumericUpDown { Minimum = 16, Maximum = 90, Value = 16, Width = 50 }, sfAgeMax = new NumericUpDown { Minimum = 16, Maximum = 90, Value = 90, Width = 50 };
        TextBox sfClub = new TextBox { Width = 130 };
        CheckBox sfFree = new CheckBox { Text = "仅无业", AutoSize = true };
        WorkspaceGrid gridStaffFind = NewGrid();
        Label lblStaffFind = new Label { Dock = DockStyle.Top, Height = 22, Padding = new Padding(4, 4, 0, 0), UseMnemonic = false };

        TabPage BuildStaffFinderTab()
        {
            var page = new TabPage("查找职员");
            sfJob.Items.Add("不限"); sfJob.Items.AddRange(StaffRole.All); sfJob.SelectedIndex = 0;
            var names = FmLayout.StaffAttrs.Select(a => a.Split('|')[0]).ToArray();
            sfAttr1.Items.Add("（不限属性）"); sfAttr1.Items.AddRange(names); sfAttr1.SelectedIndex = 0;
            sfAttr2.Items.Add("（无）"); sfAttr2.Items.AddRange(names); sfAttr2.SelectedIndex = 0;
            var bar=WrappingBar("StaffFinderFilters");
            bar.Controls.Add(FieldGroup(FieldLabel("职务"),sfJob));
            bar.Controls.Add(FieldGroup(sfAttr1,FieldLabel("不低于"),sfMin1));
            bar.Controls.Add(FieldGroup(FieldLabel("和"),sfAttr2,FieldLabel("不低于"),sfMin2));
            bar.Controls.Add(FieldGroup(FieldLabel("年龄"),sfAgeMin,FieldLabel("到"),sfAgeMax));
            bar.Controls.Add(FieldGroup(FieldLabel("俱乐部"),sfClub));
            bar.Controls.Add(sfFree);
            var btn = new Button { Name="SearchStaff", Text="搜索", Width=90 };
            bar.Controls.Add(btn);
            foreach (var c in new[] { "姓名", "年龄", "俱乐部", "职务", "属性一", "属性二", "Determination", "年薪（" + Cur() + "／年）" }) gridStaffFind.Columns.Add(c, c);
            gridStaffFind.ReadOnly = true; gridStaffFind.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridStaffFind.Columns[0].FillWeight = 170; gridStaffFind.Columns[2].FillWeight = 140; gridStaffFind.Columns[3].FillWeight = 130;
            gridStaffFind.Columns[7].DefaultCellStyle.Format = "N0"; gridStaffFind.Columns[7].DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            lblStaffFind.Text = "最多显示 1,000 名职员。选中结果后点击「打开资料」、双击或按 Enter。";
            WrapDockedLabel(lblStaffFind);
            page.Controls.Add(gridStaffFind); page.Controls.Add(lblStaffFind); page.Controls.Add(bar);
            OrganizeFinderToolbar(bar,btn);
            btn.Click += delegate { RunStaffFinder(btn); };
            AddFinderOpenButton<Staff>(bar,btn,gridStaffFind,"OpenStaffFinderResult",st=>
            {
                txtStaffSearch.Text = "UID " + st.Uid;
                ((TabControl)page.Parent).SelectedIndex = 1;
                lstStaff.SelectedItem = st;
            });
            return page;
        }

        void RunStaffFinder(Button btn)
        {
            if (fm == null || staff.Count == 0) { MessageBox.Show("请先点「挂接并扫描」。", "查找职员"); return; }
            if (!Safety.On("core")) { MessageBox.Show("请先完成游戏数据检查。", "查找职员"); return; }
            var attrs = FmLayout.StaffAttrs.Select(a => a.Split('|')).ToArray();
            int a1 = sfAttr1.SelectedIndex - 1, a2 = sfAttr2.SelectedIndex - 1, min1 = (int)sfMin1.Value, min2 = (int)sfMin2.Value, ageMin = (int)sfAgeMin.Value, ageMax = (int)sfAgeMax.Value;
            var role = sfJob.SelectedItem as StaffRole;
            if ((a1 >= 0 || a2 >= 0) && !Safety.On("staff"))
            { MessageBox.Show("本次职员属性检查未通过，可使用职务、俱乐部等条件搜索。", "查找职员"); return; }
            string club = sfClub.Text.Trim(); bool free = sfFree.Checked;
            bool ageLimited = sfAgeMin.Value > sfAgeMin.Minimum || sfAgeMax.Value < sfAgeMax.Maximum;
            var connected = fm;
            int revision = staffFinderRevision;
            uint today = Safety.On("date") ? (fm.ReadGameDate() ?? 0) : 0;
            DateTime now = today != 0 ? FromPacked(today) : DateTime.Today;
            var all = staff.Where(x => !x.Manager).ToList(); double rate = Rate();
            int det = Array.FindIndex(attrs, a => a[0] == "Determination");
            Func<byte[], int, int> val = (b, k) => StaffDisplay(b[(int)(FmLayout.StaffBlockBefore - Convert.ToUInt64(attrs[k][1], 16))], attrs[k][2]);
            gridStaffFind.Columns[4].HeaderText = a1 >= 0 ? Zh.Z(attrs[a1][0]) : "";
            gridStaffFind.Columns[5].HeaderText = a2 >= 0 ? Zh.Z(attrs[a2][0]) : "";
            btn.Enabled = false; lblStaffFind.Text = "正在搜索，共 " + all.Count.ToString("N0", CultureInfo.InvariantCulture) + " 名职员……";
            var th = new Thread(() =>
            {
                var res = new List<object[]>();
                foreach (var st in all)
                {
                    int? age = st.Born.HasValue ? (int?)Birthdays.AgeOn(st.Born.Value, now) : null;
                    if (age.HasValue ? age < ageMin || age > ageMax : ageLimited) continue;
                    string employer = st.ClubName;
                    if (club.Length > 0 && (employer == null || employer.IndexOf(club, StringComparison.CurrentCultureIgnoreCase) < 0)) continue;
                    var b = connected.Read(st.Addr - FmLayout.StaffBlockBefore, FmLayout.StaffBlockLen);
                    bool attributesAvailable = FmLayout.ValidStaffBlock(b);
                    if (!attributesAvailable && (a1 >= 0 || a2 >= 0)) continue;
                    int v1 = a1 >= 0 ? val(b, a1) : 0, v2 = a2 >= 0 ? val(b, a2) : 0;
                    if ((a1 >= 0 && v1 < min1) || (a2 >= 0 && v2 < min2)) continue;
                    var employment = StaffEmployment.Read(connected.Read, connected.Base, connected.ImageSize, st.Addr);
                    if (role != null && (employment == null || employment.Job != role.Code)) continue;
                    bool unemployed = employment == null && employer == null && connected.ReadI32(st.Addr + FmLayout.Uid) == st.Uid &&
                        connected.ReadPtr(st.Addr + FmLayout.StaffContract) == 0;
                    if (free && !unemployed) continue;
                    res.Add(new object[] { st, age, employer ?? (unemployed ? "（无业）" : ""),
                        employment != null ? StaffRole.Title(employment.Job) : employer != null ? "其他职务" : "",
                        a1 >= 0 ? (object)v1 : null, a2 >= 0 ? (object)v2 : null,
                        attributesAvailable && det >= 0 ? (object)val(b, det) : null,
                        employment == null ? null : (object)Math.Round(employment.WeeklyWage * rate * 52) });
                }
                var top = res.OrderByDescending(r => ((int?)r[4] ?? 0) + ((int?)r[5] ?? 0)).ThenByDescending(r => (int?)r[4]).Take(1000).ToList();
                if (closingEditor || IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)(() =>
                {
                    btn.Enabled = true;
                    if (fm != connected || revision != staffFinderRevision || closingEditor) return;
                    gridStaffFind.Rows.Clear();
                    gridStaffFind.Columns[7].HeaderText = "年薪（" + Cur() + "／年）";
                    foreach (var r in top)
                    {
                        var st = (Staff)r[0];
                        int i = gridStaffFind.Rows.Add(st.DisplayName, r[1], r[2], r[3], r[4], r[5], r[6], r[7]);
                        gridStaffFind.Rows[i].Tag = st;
                    }
                    lblStaffFind.Text = res.Count.ToString("N0", CultureInfo.InvariantCulture) + " 名职员符合条件" + (res.Count > 1000 ? "，显示排名靠前的 1000 人" : "") + "。选中结果后可打开资料。";
                }));
            });
            th.IsBackground = true; th.Start();
        }

        TabPage BuildLogTab()
        { return BuildReadableLogTab(); }

        // ---------- scanning ----------

        async void StartScan()
        {
            if(closingEditor)return;
            if(rosterWorker.Busy)
            {
                reconnectAfterRead=true;
                rosterWorker.Cancel();
                lblStatus.Text="正在结束上一次读取，随后重新挂接…";
                return;
            }
            if(!btnScan.Enabled)return;
            btnScan.Enabled = false;
            btnStaffMove.Enabled = false;
            InvalidateStaffFinderResults();
            autoMonthKey=0;
            scannedSaveLoads=null;
            genderFilterVerified=false;
            UpdateRosterRefreshButtons();
            StopAllFreezes("因重新扫描而停止");
            StopGrow("因重新扫描而停止");
            ShowClubCatalogEntry(null,null);
            Safety.Off["core"] = "本次连接与扫描尚未完成";
            lastScanReport = "修改器 v" + Version + "；正在开始本次连接与扫描，尚无本次检查结果。";
            try
            {
                if (fm != null) fm.Dispose();
                fm = FmProcess.Attach();
                Profile.Load(fm, AppDomain.CurrentDomain.BaseDirectory);
            }
            catch (Exception ex)
            {
                if (fm != null) fm.Dispose(); fm = null;
                Safety.Off["core"] = "本次连接未成功";
                lastScanReport = "修改器 v" + Version + "；本次连接失败，全部编辑功能保持只读。\r\n" + ex.Message;
                lblStatus.Text = "本次连接失败；可从「连接详情…」复制诊断。";
                LogLine(lastScanReport);
                MessageBox.Show(lastScanReport, "挂接游戏"); btnScan.Enabled = true; return;
            }

            lblStatus.Text = "正在扫描游戏数据……";
            var scanTimer = Stopwatch.StartNew();
            try
            {
                var calib = new List<string>();
                var checks = new List<string>();
                var connection=fm;
                IProgress<int> percent=new Progress<int>(pct=>{if(!closingEditor && fm==connection && !btnScan.Enabled)progress.Value=pct;});
                IProgress<string> status=new Progress<string>(msg=>{if(!closingEditor && fm==connection && !btnScan.Enabled)lblStatus.Text=msg;});
                var r=await rosterWorker.Run(token=>
                {
                    int lastPct=-1;
                    Action<double> prog=f=>
                    {
                        token.ThrowIfCancellationRequested();
                        int pct=Math.Min(100,(int)(f*100));
                        if(pct==lastPct)return;
                        lastPct=pct;
                        percent.Report(pct);
                    };
                    var result=ScanCalibrated(prog,calib,status.Report,token);
                    token.ThrowIfCancellationRequested();
                    checks=Safety.Run(connection,result);
                    result.PrepareDisplayLists();
                    return result;
                });
                if(closingEditor || IsDisposed)return;
                growthDirectory=r.Growth;growthSquadViews.Clear();growthLoanViews.Clear();
                checkedGrowthClubs.Clear();combinedGrowthScopeKey="";combinedGrowthLoan=0;
                browsePlayerUid=queuePlayerUid=0;
                scannedSaveLoads=r.SaveLoads;
                genderFilterVerified=r.GenderVerified;
                retirementManager = r.RetirementManager;
                players = r.Players; rosterIndex = r.RosterIndex;
                clubs = r.Clubs; staff = r.Staff;
                prizes = r.Prizes; FilterPrizes();
                firstNames = r.FirstNames; lastNames = r.LastNames; commonNames = r.CommonNames; nations = r.Nations; cities = r.Cities; cityNames = r.CityNames;
                readOnlyWarned = false;
                bool tested = Profile.IsTested(fm.Version);
                LogLine("挂接：" + fm.Version + "，配置 " + Profile.Name + (tested ? "（测试过的版本）" : "（未测试的版本）"));
                foreach (var line in calib.Concat(r.Diagnostics).Concat(checks)) LogLine(line);
                LogLine(r.GenderNote);
                lblStatus.Text = "FM " + fm.Version.Split('+')[0] + "（" + Profile.Name + (tested ? "" : "，未测试版本") + "）：" +
                    players.Count.ToString("N0") + " 名球员，" + staff.Count.ToString("N0") + " 名职员，" + clubs.Count.ToString("N0") + " 家俱乐部。" +
                    Safety.Summary() + " " + r.GenderNote + "。 扫描耗时 " + scanTimer.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " 秒。";
                btnScan.Enabled = true;
                btnHealAll.Enabled = Safety.On("injury");
                btnFresh.Enabled = btnFreezeAll.Enabled = btnFreeze.Enabled = Safety.On("status");
                FilterPlayers(); FilterClubs(); FilterSquadClubs(); FilterStaff();
                // the old Player objects are stale now, so the growth watch starts empty
                growWatch.Clear(); growthPreferences.Clear(); StopGrow("重新扫描后旧对象已失效");
                FilterGrowClubs();
                txtPlayerSearch.Focus();
                lastScanReport=AttachReport.Build(fm.Version,tested,r,Safety.Off,Safety.Partial(),calib)+"\r\n\r\n"+string.Join("\r\n",calib.Concat(r.Diagnostics).Concat(checks));
                if (AttachReport.ShouldPopup(fm,Safety.Off))
                    MessageBox.Show(AttachReport.Build(fm.Version,tested,r,Safety.Off,Safety.Partial(),calib),"FM27E0 汉化改版 v"+Version+" - 挂接报告");
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                if(closingEditor || IsDisposed)return;
                Safety.Off["core"]="本次扫描未完成";
                lastScanReport="扫描未完成。"+ex.Message;
                lblStatus.Text=lastScanReport;
                LogLine(lastScanReport);
            }
            finally
            {
                if(!closingEditor && !IsDisposed)btnScan.Enabled=true;
                FinishRosterRead();
            }
        }

        // Scan with the profile's class pointers; when they do not fit this fm.exe, find them again and scan once more
        ScanResult ScanCalibrated(Action<double> prog, List<string> calib, Action<string> status,CancellationToken token=default(CancellationToken))
        {
            if (FmLayout.Calibrate == 0) return ScanAll(prog,token);
            return ScanRecovery.Read(()=>ScanAll(prog,token),()=>Calibration.Run(fm,prog),forceCalibrate,calib,status);
        }

        static void RememberNameEntry(Dictionary<string,ulong> names,string name,byte[] head,ulong offset)
        {
            if(!string.IsNullOrEmpty(name) && !names.ContainsKey(name))
                names.Add(name,BitConverter.ToUInt64(head,(int)offset));
        }

        // One pass over the game's memory with the current profile's class pointers
        ScanResult ScanAll(Action<double> prog,CancellationToken token=default(CancellationToken))
        {
            var r = new ScanResult();
            ulong? saveLoads=SaveLoadCount(fm);
            r.Diagnostics.Add("引擎版本："+fm.Version+"；模块大小：0x"+fm.ImageSize.ToString("X")+"；配置："+Profile.Name);
            r.Diagnostics.Add(ScanDiagnostics.Connection(fm.Pid, fm.EngineModule, FmLayout.Calibrate != 0, Profile.Name, Profile.Source));
            r.Diagnostics.Add("结构适配："+(CurrentBuildProfile.Matches(fm)?CurrentBuildProfile.Active.Channel+" "+CurrentBuildProfile.Active.Version+"，构建校验通过":"各项功能按通用配置的检查结果决定"));
            var personHits = new List<ulong>(); var clubHits = new List<ulong>(); var staffHits = new List<ulong>(); var cityHits = new List<ulong>();
            // staff: regular non-players and player-coaches (same attribute layout; e.g. a goalkeeping coach who is also registered as a player)
            var managerHits = new List<ulong>();
            ulong[] targets = { fm.Base + FmProcess.PersonVtRva, fm.Base + FmProcess.ClubVtRva, fm.Base + FmProcess.StaffVtRva, fm.Base + FmProcess.PlayerCoachVtRva, fm.Base + FmProcess.CityVtRva, fm.Base + FmProcess.ManagerVtRva };
            var stageHits = new List<ulong>();
            var seasonHits = new List<ulong>();
            targets = targets.Concat(new[] { fm.Base + FmProcess.StageVtRva, fm.Base + FmProcess.SeasonVtRva }).ToArray();
            ulong retirementVtable = RetirementSnapshot.VtableFor(fm);
            if (retirementVtable != 0) targets = targets.Concat(new[] { fm.Base + retirementVtable }).ToArray();
            var retirementManagers = new List<ulong>();
            var coachHits = new List<ulong>();
            fm.ScanQwords(targets, (t, addr) => { if (t == 8) retirementManagers.Add(addr); else if (t == 0) personHits.Add(addr); else if (t == 1) clubHits.Add(addr); else if (t == 4) cityHits.Add(addr); else if (t == 5) managerHits.Add(addr); else if (t == 6) stageHits.Add(addr); else if (t == 7) seasonHits.Add(addr); else { staffHits.Add(addr); if (t == 3) coachHits.Add(addr); } }, prog);
            r.Diagnostics.Add(fm.ScanSummary("内存扫描"));
            // prize money tables: one per rules object, named after the competition and the stage tag
            var seenVec = new HashSet<ulong>();
            foreach (var st in stageHits)
            {
                // StageCompetition 0: the competition object holds the rules itself (FM27 league tables)
                bool self = FmLayout.StageCompetition == 0;
                ulong comp = self ? st : fm.ReadPtr(st + FmLayout.StageCompetition), rules = fm.ReadPtr(st + FmLayout.StageRules);
                if (comp < 0x10000 || fm.ReadPtr(comp) != fm.Base + FmProcess.CompetitionVtRva) continue;
                // most FM27 competitions have no rules object of their own, and friendlies, youth and international ones use other rule classes
                if (self && (rules < 0x10000 || fm.ReadPtr(rules) != fm.Base + FmProcess.CompRulesVtRva)) continue;
                r.StageCount++;
                if (rules < 0x10000 || fm.ReadPtr(rules) != fm.Base + FmProcess.CompRulesVtRva) continue;
                r.StageOk++;
                ulong vec = rules + FmLayout.RulesPrizes, b0 = fm.ReadPtr(vec), e0 = fm.ReadPtr(vec + 8);
                if (b0 < 0x10000 || e0 <= b0 || e0 - b0 > 400 || (e0 - b0) % 4 != 0 || (self && e0 - b0 < 8) || !seenVec.Add(b0)) continue;
                string cname = fm.ReadLenString(fm.ReadPtr(comp + FmLayout.CompetitionName)) ?? "?";
                var tb = self ? null : fm.Read(st + FmLayout.StageTag, 4); string tag = self ? "leag" : tb == null ? "" : Encoding.ASCII.GetString(tb.Reverse().ToArray()).Trim('\0');
                string stage = tag == "leag" || tag == "lge1" ? "联赛排名" : tag == "NULL" || tag.Length == 0 ? "阶段" : tag;
                r.Prizes.Add(new PrizeTable { Vector = vec, Comp = comp, Name = cname + " - " + stage + " (" + (e0 - b0) / 4 + " 个名次）" });
            }
            // this season's copies, matched to their competition's table
            foreach (var h in seasonHits)
            {
                ulong comp = fm.ReadPtr(h + FmLayout.SeasonComp), stg = fm.ReadPtr(h + FmLayout.SeasonStage);
                if (stg < 0x10000 || fm.ReadPtr(stg) != fm.Base + FmProcess.SeasonStageVtRva) continue;
                if (comp < 0x10000 || fm.ReadPtr(comp) != fm.Base + FmProcess.CompetitionVtRva) continue;
                ulong v = stg + FmLayout.StagePrizes, b1 = fm.ReadPtr(v), e1 = fm.ReadPtr(v + 8);
                if (b1 < 0x10000 || e1 <= b1 || e1 - b1 > 400 || (e1 - b1) % 4 != 0) continue;
                var pt = r.Prizes.FirstOrDefault(x => x.Comp == comp);
                if (pt != null) { if (!pt.SeasonVectors.Contains(v)) pt.SeasonVectors.Add(v); continue; }
                // after a save is loaded FM27 keeps no template for most leagues: then this season's table is the only one
                var vals = fm.Read(b1, (int)(e1 - b1)); if (vals == null || !seenVec.Add(b1)) continue;
                bool money = false; for (int k = 0; k + 4 <= vals.Length; k += 4) if (BitConverter.ToInt32(vals, k) > 0) money = true;
                if (!money) continue;
                string cname = fm.ReadLenString(fm.ReadPtr(comp + FmLayout.CompetitionName)) ?? "?";
                r.Prizes.Add(new PrizeTable { Vector = v, Comp = comp, Name = cname + " - 本赛季（" + (e1 - b1) / 4 + " 个获奖名次）" });
            }

            var nf = r.FirstNames; var nl = r.LastNames; var nc = r.CommonNames; var nat = r.Nations;
            // A name-table entry is shared by many people. Keep this cache within this one scan.
            var entryNames=new Dictionary<ulong,string>();
            Func<ulong,string> entryName=entry=>
            {
                if(entry<0x10000)return null;
                string name;
                if(entryNames.TryGetValue(entry,out name))return name;
                name=fm.ReadNameEntry(entry);
                if(!string.IsNullOrEmpty(name))entryNames.Add(entry,name);
                return name;
            };
            var nameNationIds=new Dictionary<ulong,int>();
            Func<ulong,int> nameNationUid=nation=>
            {
                int uid;if(nameNationIds.TryGetValue(nation,out uid))return uid;
                var value=nation>=0x10000 && CurrentBuildProfile.Matches(fm) && fm.ReadPtr(nation)==fm.Base+EligibilityLayout.NationVt?fm.ReadI32(nation+12):null;
                uid=value.HasValue && value.Value>0?value.Value:0;nameNationIds[nation]=uid;return uid;
            };
            int invalidBlocks=0,unreadableHeads=0,unnamedPlayers=0;
            foreach (var p in personHits)
            {
                token.ThrowIfCancellationRequested();
                var b = fm.Read(p - FmLayout.BlockBeforePerson, FmLayout.PosCount + FmLayout.AttrCount);
                if (!FmLayout.ValidBlock(b)) {invalidBlocks++;continue;}
                var head = fm.Read(p, FmLayout.HeadLen);
                if (head == null) {unreadableHeads++;continue;}
                ulong n = BitConverter.ToUInt64(head, (int)FmLayout.Nation);
                if (n > 0x10000 && !nat.ContainsKey(n)) { var s = fm.ReadLenString(fm.ReadPtr(n + FmLayout.NationName)); if (!string.IsNullOrEmpty(s)) nat[n] = s; }
                string name, search;PersonNameParts parts;
                fm.ReadPersonNames(head,entryName,out name,out search,out parts);
                RememberNameEntry(nf,parts.First,head,FmLayout.FirstNameEntry);
                RememberNameEntry(nl,parts.Last,head,FmLayout.LastNameEntry);
                RememberNameEntry(nc,parts.Common,head,FmLayout.CommonNameEntry);
                if (string.IsNullOrEmpty(name)) {unnamedPlayers++;continue;}
                var pl = new Player { Person = p, Name = name, Search = search,NameParts=parts,NameNationUid=nameNationUid(n), Uid = BitConverter.ToInt32(head, (int)FmLayout.Uid), Female = GenderFilter.Female(head, fm.Version, fm.VerifiedCurrentBuild), Born = BornFrom(head) };
                ulong team = fm.ReadPtr(p - FmLayout.TeamBeforePerson);
                if (team > 0x10000 && Safety.HasContract(fm, p)) pl.Club = fm.ReadPtr(team + FmLayout.TeamClub);   // free agents keep a stale team pointer
                r.Players.Add(pl);
            }
            // player-coaches are players too (they also stay on the Staff tab for their coaching side)
            foreach (var p in coachHits)
            {
                ulong tail = p - FmLayout.PlayerCoachExtra;
                if (!FmLayout.ValidBlock(fm.Read(tail - FmLayout.BlockBeforePerson, FmLayout.PosCount + FmLayout.AttrCount))) continue;
                var head = fm.Read(p, FmLayout.HeadLen); if (head == null) continue;
                string name, search;PersonNameParts parts;
                fm.ReadPersonNames(head,entryName,out name,out search,out parts);
                if (string.IsNullOrEmpty(name)) continue;
                var pl = new Player { Person = p, Extra = FmLayout.PlayerCoachExtra, Name = name, Search = search,NameParts=parts,NameNationUid=nameNationUid(BitConverter.ToUInt64(head,(int)FmLayout.Nation)), Uid = BitConverter.ToInt32(head, (int)FmLayout.Uid), Female = GenderFilter.Female(head, fm.Version, fm.VerifiedCurrentBuild), Born = BornFrom(head) };
                ulong team = fm.ReadPtr(tail - FmLayout.TeamBeforePerson);
                if (team > 0x10000 && Safety.HasContract(fm, p)) pl.Club = fm.ReadPtr(team + FmLayout.TeamClub);
                r.Players.Add(pl);
                r.PlayerCoaches++;
            }

            var staffClub = new Dictionary<Staff, ulong>();
            foreach (var a in staffHits)
            {
                token.ThrowIfCancellationRequested();
                bool attributesAvailable = FmLayout.ValidStaffBlock(fm.Read(a - FmLayout.StaffBlockBefore, FmLayout.StaffBlockLen));
                if (!attributesAvailable)
                {
                    if (fm.ReadPtr(a) != fm.Base + FmProcess.StaffVtRva) continue;
                    var employment = StaffEmployment.Read(fm.Read, fm.Base, fm.ImageSize, a);
                    if (employment == null || !employment.VerifiesClubAssignment(fm.ReadPtr, fm.Base)) continue;
                }
                var head = fm.Read(a, FmLayout.HeadLen); if (head == null) continue;
                string name, search;PersonNameParts parts;
                fm.ReadPersonNames(head,entryName,out name,out search,out parts);
                int uid = BitConverter.ToInt32(head, (int)FmLayout.Uid);
                if (uid <= 0 || string.IsNullOrEmpty(name)) continue;
                var st = new Staff { Addr = a, Pers = a, Uid = uid, Name = name, Search = search, NameParts=parts,
                    NameNationUid=nameNationUid(BitConverter.ToUInt64(head,(int)FmLayout.Nation)), Born = BornFrom(head),
                    ContractOnly = !attributesAvailable };
                ulong sc = fm.ReadPtr(a + FmLayout.StaffContract), stTeam = sc > 0x10000 ? fm.ReadPtr(sc + FmLayout.ContractTeam) : 0;
                ulong stClub = stTeam > 0x10000 ? fm.ReadPtr(stTeam + FmLayout.TeamClub2) : 0;
                if (stClub > 0x10000) staffClub[st] = stClub;
                r.Staff.Add(st);
            }
            // the human manager: names and personality in his person object, attributes in the staff layout before it
            foreach (var m in managerHits)
            {
                ulong a = m - FmLayout.ManagerAttrsBefore;
                if (!FmLayout.ValidStaffBlock(fm.Read(a - FmLayout.StaffBlockBefore, FmLayout.StaffBlockLen))) continue;
                var head = fm.Read(m, FmLayout.HeadLen); if (head == null) continue;
                string name, search;PersonNameParts parts;
                fm.ReadPersonNames(head,entryName,out name,out search,out parts);
                // FM also keeps an empty template manager (named after a real player, no birth date): skip it
                if (string.IsNullOrEmpty(name) || BornFrom(head) == null) continue;
                r.Staff.Add(new Staff { Addr = a, Pers = m, Uid = BitConverter.ToInt32(head,(int)FmLayout.Uid), Manager = true, Name = name, Search = search + " | manager | you",NameParts=parts,NameNationUid=nameNationUid(BitConverter.ToUInt64(head,(int)FmLayout.Nation)), Born = BornFrom(head) });
            }

            ulong compactFinance=CurrentBuildProfile.Matches(fm)?CurrentBuildProfile.Active.CompactFinance:FmProcess.CompactFinanceVtRva;
            var catalog=new ClubCatalogReader(fm.Read,fm.ReadLenString,fm.Base,compactFinance);
            r.Clubs=catalog.Read(clubHits,token);
            var clubNames = new Dictionary<ulong, string>(); foreach (var c in r.Clubs) clubNames[c.Addr] = c.Name;
            foreach (var c in cityHits)
            {
                string cn = fm.ReadLenString(fm.ReadPtr(c + FmLayout.CityName)); if (string.IsNullOrEmpty(cn)) continue;
                string nn; nat.TryGetValue(fm.ReadPtr(c + FmLayout.CityNation), out nn);
                string label = cn + " (" + (nn ?? "?") + ")";
                if (r.Cities.ContainsKey(label)) continue;   // same name in the same nation: keep the first
                r.Cities[label] = c; r.CityNames[c] = label;
            }
            foreach (var kv in staffClub)
            {
                string cn;
                if (clubNames.TryGetValue(kv.Value, out cn)) kv.Key.SetClub(cn);
            }
            r.RetirementManager = fm.VerifiedCurrentBuild ? CurrentBuildProfile.RetirementManager(fm.Read, fm.Base) : retirementManagers.Count == 1 ? retirementManagers[0] : 0;
            r.Diagnostics.Add("球员扫描：类型命中 "+personHits.Count+"，属性检查排除 "+invalidBlocks+"，资料无法读取 "+unreadableHeads+"，姓名无法读取 "+unnamedPlayers+"，双重身份候选 "+coachHits.Count+"。球员类型位置：0x"+FmProcess.PersonVtRva.ToString("X"));
            int verifiedClubs=r.Clubs.Count;
            GenderFilter.Apply(fm, r);
            int fullClubs=r.Clubs.Count(c=>c.HasFullFinance),compactClubs=r.Clubs.Count(c=>c.CompactFinance);
            r.Diagnostics.Add("俱乐部扫描：类型命中 "+clubHits.Count+"，通过资料核对 "+verifiedClubs+" 家，隐藏女足 "+r.HiddenWomenClubs+
                " 家，收录 "+r.Clubs.Count+" 家，其中完整财务 "+fullClubs+
                " 家、简化财务 "+compactClubs+" 家、其他已核对球队归属 "+(r.Clubs.Count-fullClubs-compactClubs)+" 家。");
            int contractOnlyStaff = r.Staff.Count(s => s.ContractOnly);
            if (contractOnlyStaff > 0) r.Diagnostics.Add("职员扫描：另有 " + contractOnlyStaff + " 名职员通过身份及合同核对，可查找；属性不可编辑。");
            try
            {
                DateTime? today=null;try{var packed=fm.ReadGameDate();if(packed.HasValue)today=Birthdays.Decode(packed.Value);}catch{}
                r.Growth=new GrowthDirectoryReader(fm.Read,GrowthReadLayout.From(fm)).Capture(r.Players,r.Clubs,today,token);
                r.Diagnostics.Add(r.Growth.Note);
            }
            catch(Exception ex){r.Diagnostics.Add("分队与租借资料暂不可用："+ex.Message);}
            r.Diagnostics.Add("名单筛选：原始球员 "+r.RawPlayers+"，隐藏女足 "+r.HiddenWomen+"，保留球员 "+r.Players.Count+"；俱乐部 "+r.Clubs.Count+"；未按国家或联赛过滤球员。");
            token.ThrowIfCancellationRequested();
            PlayerNames.Apply(r.Players);
            token.ThrowIfCancellationRequested();
            PlayerNames.ApplyStaff(r.Staff);
            if(saveLoads.HasValue && SaveLoadCount(fm)==saveLoads)r.SaveLoads=saveLoads;
            return r;
        }

        // Headless run of attach, (calibration,) scan and safety checks, used during development: --check out.txt [--calibrate]
        public static int Check(string outPath, bool calibrate)
        {
            var lines = new List<string>();
            try
            {
                var f = new MainForm(false, calibrate);
                f.fm = FmProcess.Attach();
                Profile.Load(f.fm, AppDomain.CurrentDomain.BaseDirectory);
                lines.Add("fm.exe " + f.fm.Version + ", profile " + Profile.Name + (Profile.IsTested(f.fm.Version) ? " (tested)" : " (untested)"));
                var sw = Stopwatch.StartNew();
                var r = f.ScanCalibrated(x => { }, lines, x => lines.Add(x));
                lines.Add("scan " + sw.Elapsed.TotalSeconds.ToString("F1") + "s: " + r.Players.Count + " players (" + r.PlayerCoaches + " player-coaches), " + r.Staff.Count + " staff, " + r.Clubs.Count + " clubs, " + r.Cities.Count + " towns");
                lines.Add("verify: " + (Calibration.Verify(f.fm, r) ?? "class pointers fit")); sw.Restart();
                lines.AddRange(r.Diagnostics);
                lines.AddRange(Safety.Run(f.fm, r));
                lines.Add("checks " + sw.Elapsed.TotalSeconds.ToString("F1") + "s");
                lines.Add(Safety.Summary());
            }
            catch (Exception ex) { lines.Add("ERROR " + ex); }
            File.WriteAllLines(outPath, lines.ToArray());
            return 0;
        }

        static DateTime? BornFrom(byte[] head)
        {
            int day = BitConverter.ToUInt16(head, (int)FmLayout.BirthDate) & 0x1FF, year = BitConverter.ToUInt16(head, (int)FmLayout.BirthDate + 2);
            if (year > 1900 && year < 2100 && day >= 1 && day <= 366) return new DateTime(year, 1, 1).AddDays(day - 1);
            return null;
        }

        void FilterPlayers()
        { FilterPlayerResults(null); }

        void FilterPlayerResults(Player selected)
        {
            var q = txtPlayerSearch.Text.Trim();
            fillingPlayerResults=true;
            lstPlayers.BeginUpdate();
            try
            {
                lstPlayers.Items.Clear();
                if(!PlayerNames.CanSearchPlayer(q))return;
                int uid;
                var matches=PlayerNames.TryUid(q,out uid)?RosterIndex.ForUid(uid):players.Where(x=>x.Matches(q));
                lstPlayers.Items.AddRange(matches.OrderBy(x=>x.MatchRank(q)).ThenBy(x=>x.Name).Take(500).Cast<object>().ToArray());
                if(selected!=null && lstPlayers.Items.Contains(selected))lstPlayers.SelectedItem=selected;
            }
            finally{lstPlayers.EndUpdate();fillingPlayerResults=false;}
        }

        void FilterClubs()
        {
            var q = txtClubSearch.Text.Trim();
            var selected = lstClubs.SelectedItem as Club;
            filteringClubs = true;
            lstClubs.BeginUpdate();
            try
            {
                lstClubs.Items.Clear();
                lstClubs.Items.AddRange(clubs.Where(c=>ClubSearch.Matches(c,q)).ToArray());
                if (selected != null && lstClubs.Items.Contains(selected)) lstClubs.SelectedItem = selected;
            }
            finally { lstClubs.EndUpdate(); filteringClubs = false; }
            var current = lstClubs.SelectedItem as Club;
            if (current != loadedClub) LoadClub(current);
        }

        // ---------- players ----------

        void LoadPlayer(Player p)
        {
            if (p == null || fm == null) return;
            try
            {
                if(Safety.On("core"))p=RefreshSelectedPlayer(p);
                else if(!RosterPlayerIdentity.Matches(fm.Read,PlayerGuards(p)))
                    throw new InvalidDataException("球员类型或 UID 未通过检查。");
            }
            catch(Exception ex)
            {
                ClearPlayerPreview("球员资料暂不可用。"+ex.Message.Replace("注册数据","球员资料"));
                return;
            }
            var b = fm.Read(p.Block, FmLayout.PosCount + FmLayout.AttrCount);
            if (!FmLayout.ValidBlock(b)) { ClearPlayerPreview("球员属性尚未完整读取，请稍后重新读取。"); return; }
            loadedPlayer = p;
            btnApplyPlayer.Enabled=Safety.On("core");
            btnOptimizeAttributes.Enabled=Safety.On("core") && Safety.On("ability") && Safety.On("date");
            try { p.Born = Birthdays.Decode(fm.ReadU32(p.Person + FmLayout.BirthDate).Value); } catch { } loadedBlock = b;
            int injuries = Safety.On("injury") ? InjuryCount(p) : 0;
            lblPlayer.Text = p.ToString() + "  " + DisplayAge(p.Born) + "   [UID " + p.Uid + "]" + "\n" + PlayerNames.Description(p) + "\n" + RetirementStatus(p) + (!Safety.On("injury") ? "" : injuries > 0 ? "   受伤（" + injuries + ")" : "   健康");
            btnHeal.Enabled = injuries > 0;

            gridAttrs.Rows.Clear();
            for (int k = 0; k < FmLayout.AttrCount; k++)
            {
                var parts = FmLayout.Attrs[k].Split('|');
                int raw = b[FmLayout.AttrStart + k];
                gridAttrs.Rows.Add(Zh.Attr(parts[0]), ToDisplay(raw), raw, parts[1] == "V" ? "已验证" : "");
            }
            loadedPersonality = Safety.On("personality") ? fm.Read(p.Person + FmLayout.PersonalityAfterPerson, 8) : null;
            if (loadedPersonality != null)
                for (int k = 0; k < 8; k++)
                    gridAttrs.Rows.Add(Zh.Attr(FmLayout.Personality[k].Split((char)124)[0]), loadedPersonality[k], loadedPersonality[k],
                        FmLayout.Personality[k].EndsWith("|V") ? "性格，已验证" : "性格，高度可能");
            gridPos.Rows.Clear();
            for (int k = 0; k < FmLayout.PosCount; k++) gridPos.Rows.Add(Zh.Z(FmLayout.Positions[k]) + "（1-20）", b[k]);
            loadedStatus = ReadStatus(p);
            for (int k = 0; k < loadedStatus.Length; k++) gridPos.Rows.Add(Zh.Z(FmLayout.StatusNames[k]), ToPercent(loadedStatus[k], k));

            loadedContract = ReadContract(p);
            if (loadedContract != 0)
            {
                loadedWage = fm.ReadI32(loadedContract + FmLayout.ContractWage) ?? 0;
                var e = fm.Read(loadedContract + FmLayout.ContractExpiry, 4);
                loadedExpiry = e == null ? 0 : BitConverter.ToUInt32(e, 0);
                gridPos.Rows.Add("工资（" + Cur() + "／年）  [£" + Money(loadedWage) + "／周]", Money(loadedWage * Rate() * 52));
                gridPos.Rows.Add("合同到期（年-月-日）", FromPacked(loadedExpiry).ToString("yyyy-MM-dd"));
            }
            btnClauses.Enabled = loadedContract != 0;
            btnSwap.Enabled = btnMove.Enabled = btnRelease.Enabled = Safety.On("swap");

            loadedAbilityRow = gridPos.Rows.Count;
            var ab = Safety.On("ability") ? fm.Read(p.Tail - FmLayout.AbilityBeforePerson, 4) : null;
            loadedCa = ab == null ? -1 : BitConverter.ToInt16(ab, 0);
            loadedPa = ab == null ? -1 : BitConverter.ToInt16(ab, 2);
            if (ab != null)
            {
                gridPos.Rows.Add("当前能力（1-200）", loadedCa);
                gridPos.Rows.Add("潜力上限（1-200）", loadedPa);
            }

            loadedTransferRow = -1;
            var lf = loadedContract != 0 && Safety.On("transfer") ? fm.Read(loadedContract + FmLayout.ContractListFlags, 1) : null;
            if (lf != null)
            {
                loadedListFlags = lf[0];
                loadedAsk = fm.ReadI32(p.Tail - FmLayout.AskingBeforePerson) ?? -1;
                loadedTransferRow = gridPos.Rows.Count;
                gridPos.Rows.Add("已挂牌出售（是／否）", (loadedListFlags & 1) != 0 ? "是" : "否");
                gridPos.Rows.Add("已挂牌租借（是／否）", (loadedListFlags & 2) != 0 ? "是" : "否");
                gridPos.Rows.Add("要价（" + Cur() + "，留空为未设置）", loadedAsk < 0 ? "" : Money(loadedAsk * Rate()));
            }

            loadedSquadRow = -1;
            var sq = loadedContract != 0 && Safety.On("squadstatus") ? fm.Read(loadedContract + FmLayout.ContractSquadStatus, 1) : null;
            if (sq != null)
            {
                loadedSquad = sq[0];
                loadedSquadRow = gridPos.Rows.Count;
                gridPos.Rows.Add("球队定位", "");
                var cell = new DataGridViewComboBoxCell { FlatStyle = FlatStyle.Flat };
                foreach (var v in FmLayout.SquadStatusOffered) cell.Items.Add(Zh.Z(FmLayout.SquadStatusNames[v]));
                string curName = Zh.Z(SquadStatusText(loadedSquad));
                if (!cell.Items.Contains(curName)) cell.Items.Add(curName);
                cell.Value = curName;
                gridPos.Rows[loadedSquadRow].Cells[1] = cell;
            }

            loadedNums.Clear();
            Action<string, ulong, int, int, int> num = (name, addr, bytes, min, max) =>
            {
                var raw = fm.Read(addr, bytes); if (raw == null) return;
                int v = bytes == 1 ? raw[0] : BitConverter.ToInt16(raw, 0);
                if (bytes == 2 && max > short.MaxValue) v = BitConverter.ToUInt16(raw, 0);
                loadedNums.Add(new NumRow { Row = gridPos.Rows.Count, Name = name, Addr = addr, Bytes = bytes, Min = min, Max = max, Old = v });
                gridPos.Rows.Add(Zh.Z(name) + "（" + min + "-" + max + "）", v);
            };
            loadedShirt=null;
            if(fm.VerifiedCurrentBuild && Safety.On("squadnumber"))
            {
                try{loadedShirt=CurrentShirtSnapshot.Capture(fm.Read,fm.Base,p);if(loadedShirt!=null)num("球衣号码（255 为无）",loadedShirt.Address,1,1,255);}catch{}
            }
            else if (loadedContract != 0 && Safety.On("squadnumber"))
            {
                ulong team = fm.ReadPtr(p.Tail - FmLayout.PlayerObjBeforePerson + FmLayout.PlayerTeam);
                if (team > 0x10000 && fm.ReadPtr(loadedContract + FmLayout.ContractTeam) == team)
                    num("球衣号码（255 为无）", loadedContract + FmLayout.ContractSquadNumber, 1, 1, 255);
                else
                {
                    // on loan: the number belongs to the loan contract (verified: Adams 32, Bird 26, Blackett-Taylor 27, John-Jules 19)
                    ulong lc = LoanContract(p.Person);
                    if (lc != 0 && fm.ReadPtr(lc + FmLayout.ContractTeam) == team) num("球衣号码（255 为无）", lc + FmLayout.ContractSquadNumber, 1, 1, 255);
                }
            }
            if (Safety.On("jaded")) num("疲劳度（"+FmLayout.JadedRested+" 为恢复值）", p.Tail - FmLayout.JadedBeforePerson, 2, -500, 5000);
            if (Safety.On("body"))
            {
                num("身高（厘米）", p.Tail - FmLayout.HeightBeforePerson, 2, 140, 220);
                if (FmLayout.WeightBeforePerson != 0) num("体重（公斤）", p.Tail - FmLayout.WeightBeforePerson, 2, 40, 130);
            }
            if (Safety.On("playerrep"))
                for (int k = 0; k < FmLayout.RepBeforePerson.Length; k++) num(FmLayout.RepNames[k], p.Tail - FmLayout.RepBeforePerson[k], 2, 0, 10000);
            if (Safety.On("caps"))
                for (int k = 0; k < FmLayout.CapsNames.Length; k++) num(FmLayout.CapsNames[k], p.Person + FmLayout.Caps + (ulong)FmLayout.CapsAt[k], FmLayout.CapsSize[k], 0, FmLayout.CapsSize[k] == 1 ? 255 : 999);
            ApplyPlayerAttributeFilter();
        }

        // caps, U21 caps, goals, U21 goals (null when unreadable)
        public static int[] ReadCaps(FmProcess fm, ulong person)
        {
            var b = fm.Read(person + FmLayout.Caps, 8); if (b == null) return null;
            return Enumerable.Range(0, FmLayout.CapsNames.Length).Select(k => FmLayout.CapsSize[k] == 1 ? (int)b[FmLayout.CapsAt[k]] : BitConverter.ToUInt16(b, FmLayout.CapsAt[k])).ToArray();
        }

        // the loan contract of a player out on loan, or 0
        ulong LoanContract(ulong person)
        {
            ulong slot = fm.ReadPtr(person - FmLayout.PlayerObjBeforePerson + FmLayout.PlayerLoanContract); if (slot < 0x10000) return 0;   // a person field (+0xD0)
            ulong lc = fm.ReadPtr(slot);
            return lc > 0x10000 && fm.ReadPtr(lc) == fm.Base + FmProcess.LoanContractVtRva && fm.ReadPtr(lc + 8) == person ? lc : 0;
        }

        static string SquadStatusText(int v) { string n; return FmLayout.SquadStatusNames.TryGetValue(v, out n) ? n : "(code " + v + ")"; }

        // ---------- details & traits ----------
        // Names come from FM's own name lists (collected during the scan), so only names that already exist in the
        // database can be chosen; nationality likewise from existing nations.

        // Reads a player's relation list; idx = byte offsets (from begin) of the other-nationality records. False when it does not
        // look like the expected vector or a nationality record does not point at a nation.
        internal static bool ReadRelations(FmProcess fm, ulong person, ulong natVt, out ulong begin, out ulong end, out ulong cap, out List<int> idx)
        {
            idx = new List<int>(); begin = end = cap = 0;
            ulong ro = fm.ReadPtr(person - FmLayout.PlayerObjBeforePerson + FmLayout.PlayerRelations);
            if (ro < 0x10000) return false;
            begin = fm.ReadPtr(ro); end = fm.ReadPtr(ro + 8); cap = fm.ReadPtr(ro + 16);
            if (begin == 0 && end == 0) return true;   // empty list
            if (begin < 0x10000 || end < begin || cap < end || (end - begin) % FmLayout.RelSize != 0 || end - begin > FmLayout.RelSize * 400) return false;
            var items = fm.Read(begin, (int)(end - begin)); if (items == null) return false;
            for (int k = 0; k < items.Length; k += (int)FmLayout.RelSize)
            {
                if (items[k + 10] != FmLayout.RelKindNation || items[k + 11] != FmLayout.RelOtherNationality) continue;
                if (fm.ReadPtr(BitConverter.ToUInt64(items, k)) != natVt) return false;
                idx.Add(k);
            }
            return true;
        }

        void EditDetails()
        {
            if (loadedPlayer == null || fm == null) return;
            var p = loadedPlayer;
            var head = fm.Read(p.Person, 0x100);
            if (head == null) { MessageBox.Show("读不到这名球员，请重新点「挂接并扫描」。", "球员详情"); return; }
            ulong oFirst = BitConverter.ToUInt64(head, (int)FmLayout.FirstNameEntry), oLast = BitConverter.ToUInt64(head, (int)FmLayout.LastNameEntry);
            ulong oCommon = BitConverter.ToUInt64(head, (int)FmLayout.CommonNameEntry), oNation = BitConverter.ToUInt64(head, (int)FmLayout.Nation);
            uint oDob = BitConverter.ToUInt32(head, (int)FmLayout.BirthDate);
            var oTraits = fm.Read(p.Person + FmLayout.Traits, 8);
            if (oTraits == null) return;

            var dlg = new Form { Width = 640, Height = 850 };
            StyleWorkspaceDialog(dlg,"详情与特质 · "+p.DisplayName);
            var t = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Height = 412, TabIndex = 0 };
            Func<Dictionary<string, ulong>, string, TextBox> nameBox = (dict, cur) =>
            {
                var tb = new TextBox { Width = 300, Text = cur ?? "", AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.CustomSource };
                var src = new AutoCompleteStringCollection(); src.AddRange(dict.Keys.ToArray()); tb.AutoCompleteCustomSource = src; return tb;
            };
            var first = nameBox(firstNames, fm.ReadNameEntry(oFirst));
            var last = nameBox(lastNames, fm.ReadNameEntry(oLast));
            var common = nameBox(commonNames, fm.ReadNameEntry(oCommon));
            var dob = new TextBox { Width = 120, Text = FromPacked(oDob).ToString("yyyy-MM-dd") };
            var nat = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var kv in nations.OrderBy(k => k.Value)) nat.Items.Add(kv.Value);
            string curNat; if (nations.TryGetValue(oNation, out curNat)) nat.SelectedItem = curNat;
            AddRow(t, 0, "名字", first);
            AddRow(t, 1, "姓氏", last);
            AddRow(t, 2, "常用名（可留空）", common);
            AddRow(t, 3, "出生日期（年-月-日）", dob);
            AddRow(t, 4, "国籍", nat);

            // looks
            byte oEth = head[(int)FmLayout.Ethnicity], oHairC = head[(int)FmLayout.HairColour], oHairL = head[(int)FmLayout.HairLength], oSkin = head[(int)FmLayout.SkinTone];
            var eth = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            eth.Items.AddRange(FmLayout.EthnicityNames);
            if (oEth < FmLayout.EthnicityNames.Length) eth.SelectedIndex = oEth; else eth.Items.Add("(code " + oEth + ")");
            if (eth.SelectedIndex < 0) eth.SelectedIndex = eth.Items.Count - 1;
            var skin = new NumericUpDown { Minimum = 0, Maximum = 19, Width = 60, Value = Math.Min((int)oSkin, 19), Enabled = oSkin != 255 };
            var hairC = new NumericUpDown { Minimum = 0, Maximum = 18, Width = 60, Value = Math.Min((int)oHairC, 18) };
            var hairL = new NumericUpDown { Minimum = 0, Maximum = 6, Width = 60, Value = Math.Min((int)oHairL, 6) };
            AddRow(t, 5, "族裔", eth);
            AddRow(t, 6, "肤色（0 浅 - 19 深）", skin);
            AddRow(t, 7, "发色（0-18）", hairC);
            AddRow(t, 8, "头发长度（0 光头，1 短发……6）", hairL);
            ulong oCity = BitConverter.ToUInt64(head, (int)FmLayout.BirthCity);
            string curCity; if (!cityNames.TryGetValue(oCity, out curCity)) curCity = "";
            var city = nameBox(cities, curCity);
            AddRow(t, 9, "出生地（城市（国家））", city);
            // other nationalities (up to two shown)
            ulong natVt = fm.ReadPtr(oNation), rb = 0, re = 0, rc = 0; List<int> rIdx = null;
            bool otherOk = Safety.On("othernat") && natVt > 0x10000 && ReadRelations(fm, p.Person, natVt, out rb, out re, out rc, out rIdx);
            var oOther = new List<ulong>();
            if (otherOk) foreach (var k in rIdx) oOther.Add(fm.ReadPtr(rb + (ulong)k));
            var others = new ComboBox[2];
            for (int k = 0; k < 2; k++)
            {
                var cb = others[k] = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = otherOk };
                cb.Items.Add("（无）");
                foreach (var kv in nations.OrderBy(x => x.Value)) cb.Items.Add(kv.Value);
                string nm; cb.SelectedItem = k < oOther.Count && nations.TryGetValue(oOther[k], out nm) ? nm : "（无）";
                if (k < oOther.Count && !nations.ContainsKey(oOther[k])) { cb.Items.Add("（不在列表中）"); cb.SelectedItem = "（不在列表中）"; }
                AddRow(t, 10 + k, "其他国籍 " + (k + 1), cb);
            }
            bool looksOk = Safety.On("looks"), cityOk = Safety.On("birthplace");
            eth.Enabled = hairC.Enabled = hairL.Enabled = looksOk; if (!looksOk) skin.Enabled = false;
            city.Enabled = cityOk;

            var traits = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
            var bits = new List<int>();
            for (int bit = 0; bit < 64; bit++)
            {
                bool on = ((oTraits[bit / 8] >> (bit % 8)) & 1) == 1; string nm;
                if (!FmLayout.TraitNames.TryGetValue(bit, out nm)) { if (!on) continue; nm = "未命名特质（字节 " + bit / 8 + "，位 " + bit % 8 + ")"; }
                bits.Add(bit); traits.Items.Add(Zh.Z(nm), on);
            }
            var lbl = new Label { Dock = DockStyle.Top, Height = 22, Padding = new Padding(8, 4, 0, 0), Text = "球员特质（勾选添加，取消勾选移除）：" };
            var ok = new Button { Text = "应用" };
            var note = new Label { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8, 2, 8, 0), ForeColor = UiMuted,
                Text = "姓名和城市须来自 FM 数据库，可输入文字查看建议。姓名立即显示；出生地在下一游戏日显示；外貌在 3D 比赛中显示。" };
            traits.TabIndex=1;
            var actions=DialogActions(dlg,DialogDismiss("CancelPlayerDetails","取消"),ok);
            actions.TabIndex=2;
            dlg.Controls.Add(traits); dlg.Controls.Add(lbl); dlg.Controls.Add(t); dlg.Controls.Add(note); dlg.Controls.Add(actions);

            ok.Click += delegate
            {
                ulong nFirst = oFirst, nLast = oLast, nCommon = oCommon, nNation = oNation; uint nDob = oDob;
                Func<TextBox, Dictionary<string, ulong>, ulong, bool, ulong?> pick = (tb, dict, old, optional) =>
                {
                    var s = tb.Text.Trim();
                    if (s.Length == 0) return optional ? 0UL : (ulong?)null;
                    if (s == (fm.ReadNameEntry(old) ?? "")) return old;
                    ulong e; return dict.TryGetValue(s, out e) ? e : (ulong?)null;
                };
                var f1 = pick(first, firstNames, oFirst, false); var l1 = pick(last, lastNames, oLast, false); var c1 = pick(common, commonNames, oCommon, true);
                if (f1 == null || l1 == null || c1 == null) { MessageBox.Show("请从建议列表中选择姓名，FM 只能使用数据库中已有的姓名。", "球员详情"); return; }
                nFirst = f1.Value; nLast = l1.Value; nCommon = c1.Value;
                DateTime d;
                if (!DateTime.TryParseExact(dob.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) { MessageBox.Show("出生日期应采用 年-月-日 格式，例如 2005-06-30。", "球员详情"); return; }
                nDob = ((uint)d.Year << 16) | (oDob & 0xFE00u) | (uint)d.DayOfYear;
                if (nat.SelectedItem != null) nNation = nations.First(kv => kv.Value == (string)nat.SelectedItem).Key;
                var nTraits = (byte[])oTraits.Clone();
                for (int i = 0; i < bits.Count; i++)
                {
                    int bit = bits[i]; bool on = traits.GetItemChecked(i);
                    if (on) nTraits[bit / 8] |= (byte)(1 << (bit % 8)); else nTraits[bit / 8] &= (byte)~(1 << (bit % 8));
                }

                var cur = fm.Read(p.Person, 0x100); var curT = fm.Read(p.Person + FmLayout.Traits, 8);
                if (cur == null || curT == null || !cur.Skip(0x44).Take(0x30).SequenceEqual(head.Skip(0x44).Take(0x30)) || !cur.Skip(0x91).Take(4).SequenceEqual(head.Skip(0x91).Take(4)) || BitConverter.ToUInt64(cur, (int)FmLayout.BirthCity) != oCity || !curT.SequenceEqual(oTraits))
                { MessageBox.Show("打开窗口后，游戏已更新这名球员的数据。请关闭窗口并重新打开。", "球员详情"); return; }
                int n = 0;
                Action<ulong, ulong, ulong, string> w8 = (off, oldv, newv, what) => { if (oldv != newv && WriteLogged(p.Person + off, BitConverter.GetBytes(newv), p.Name + ": " + what)) n++; };
                w8(FmLayout.FirstNameEntry, oFirst, nFirst, "名字 → " + first.Text.Trim());
                w8(FmLayout.LastNameEntry, oLast, nLast, "姓氏 → " + last.Text.Trim());
                w8(FmLayout.CommonNameEntry, oCommon, nCommon, "常用名 → " + (common.Text.Trim().Length == 0 ? "（无）" : common.Text.Trim()));
                w8(FmLayout.Nation, oNation, nNation, "国籍 → " + nat.SelectedItem);
                if (nDob != oDob && WriteLogged(p.Person + FmLayout.BirthDate, BitConverter.GetBytes(nDob), p.Name + "：出生日期 → " + dob.Text.Trim())) n++;
                if (!nTraits.SequenceEqual(oTraits) && WriteLogged(p.Person + FmLayout.Traits, nTraits, p.Name + "：特质已修改")) n++;
                Action<ulong, byte, int, string> w1 = (off, oldv, newv, what) => { if (oldv != newv && WriteLogged(p.Person + off, new[] { (byte)newv }, p.Name + ": " + what + " " + oldv + " -> " + newv)) n++; };
                if (looksOk && eth.SelectedIndex >= 0 && eth.SelectedIndex < FmLayout.EthnicityNames.Length) w1(FmLayout.Ethnicity, oEth, eth.SelectedIndex, "ethnicity");
                if (skin.Enabled) w1(FmLayout.SkinTone, oSkin, (int)skin.Value, "肤色");
                if (looksOk) w1(FmLayout.HairColour, oHairC, (int)hairC.Value, "发色");
                if (looksOk) w1(FmLayout.HairLength, oHairL, (int)hairL.Value, "头发长度");
                string cityText = city.Text.Trim(); ulong nCity;
                if (cityOk && cityText.Length > 0 && cityText != curCity)
                {
                    if (!cities.TryGetValue(cityText, out nCity)) { MessageBox.Show("请从建议列表中选择出生地（城市（国家））。", "球员详情"); return; }
                    if (WriteLogged(p.Person + FmLayout.BirthCity, BitConverter.GetBytes(nCity), p.Name + "：出生地 → " + cityText)) n++;
                }
                if (otherOk) n += ApplyOtherNationalities(p, natVt, oOther, others);
                if(n>0)personDetailsRevision++;
                lblStatus.Text = n + " 项资料修改已写入，球员：" + p.Name + "。在 FM 中立即显示（其他国籍在下一游戏日显示）。";
                dlg.Close();
            };
            Zh.Tree(dlg);
            dlg.ShowDialog(this);
        }

        // Changes other-nationality records in place, removes surplus ones (the rest of the list moves up) and adds new ones
        // only into free places of the list (nothing is allocated). One undo group.
        int ApplyOtherNationalities(Player p, ulong natVt, List<ulong> oldList, ComboBox[] boxes)
        {
            var want = new List<ulong>();
            for (int i = 0; i < boxes.Length; i++)
            {
                var s = boxes[i].SelectedItem as string; if (s == null || s == "（无）") continue;
                ulong nat = s == "（不在列表中）" && i < oldList.Count ? oldList[i] : nations.First(kv => kv.Value == s).Key;
                if (!want.Contains(nat)) want.Add(nat);
            }
            // keep any nationality records beyond the two shown
            for (int i = boxes.Length; i < oldList.Count; i++) want.Add(oldList[i]);
            if (want.SequenceEqual(oldList)) return 0;
            ulong b, e, c; List<int> idx;
            if (!ReadRelations(fm, p.Person, natVt, out b, out e, out c, out idx) || !idx.Select(k => fm.ReadPtr(b + (ulong)k)).SequenceEqual(oldList))
            { MessageBox.Show("游戏已更新 " + p.Name + " 的国籍信息。本次没有修改其其他国籍，请重新打开后再试。", "球员详情"); return 0; }
            int adds = Math.Max(0, want.Count - idx.Count);
            if ((long)(c - e) < adds * (long)FmLayout.RelSize)
            { MessageBox.Show(p.Name + " 的国籍列表暂时没有空位。可以修改或移除已有国籍，或在游戏推进后重试。", "球员详情"); return 0; }
            ulong ro = fm.ReadPtr(p.Person - FmLayout.PlayerObjBeforePerson + FmLayout.PlayerRelations);
            int n = 0; curGroup = ++lastGroup;
            try
            {
                string what = p.Name + "：其他国籍 → " + (want.Count == 0 ? "（无）" : string.Join(", ", want.Select(x => nations.ContainsKey(x) ? nations[x] : x.ToString("X")).ToArray()));
                // 1. change records in place
                for (int i = 0; i < Math.Min(idx.Count, want.Count); i++)
                    if (oldList[i] != want[i] && WriteLogged(b + (ulong)idx[i], BitConverter.GetBytes(want[i]), what)) n++;
                // 2. remove surplus records, last first so the earlier offsets stay valid
                for (int i = idx.Count - 1; i >= want.Count; i--)
                {
                    ulong at = b + (ulong)idx[i]; var tail = fm.Read(at + FmLayout.RelSize, (int)(e - at - FmLayout.RelSize));
                    var buf = new byte[e - at]; if (tail != null) tail.CopyTo(buf, 0);
                    if (!WriteLogged(at, buf, what + "（列表）")) return n;
                    e -= FmLayout.RelSize;
                    if (!WriteLogged(ro + 8, BitConverter.GetBytes(e), what + "（数量）")) return n;
                    n++;
                }
                // 3. add new records into free places (same layout as FM's own nationality records)
                for (int i = idx.Count; i < want.Count; i++)
                {
                    var rec = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, FmLayout.RelKindNation, FmLayout.RelOtherNationality, 0x64, 0x50, 0, 0xFF };
                    if (idx.Count > 0) { var tmpl = fm.Read(b + (ulong)idx[0], (int)FmLayout.RelSize); if (tmpl != null) Array.Copy(tmpl, 8, rec, 8, 8); }
                    BitConverter.GetBytes(want[i]).CopyTo(rec, 0);
                    if (!WriteLogged(e, rec, what + "（列表）")) return n;
                    e += FmLayout.RelSize;
                    if (!WriteLogged(ro + 8, BitConverter.GetBytes(e), what + "（数量）")) return n;
                    n++;
                }
            }
            finally { curGroup = 0; }
            return n;
        }

        // ---------- create player ----------
        // Rebuilds the loaded ("shell") player in one go: identity, positions, attributes suited to the position,
        // CA/PA, personality and fitness. Only existing FM names/nations can be used; everything is logged for undo.

        // attribute indices (FmLayout.Attrs order) that matter most for each position group
        static readonly int[] KeyGk = { 11, 12, 13, 14, 15, 16, 19, 21, 32, 33, 17, 20, 18, 52, 53, 46, 39 };
        static readonly int[] KeyDef = { 5, 9, 3, 20, 17, 43, 36, 39, 52, 53, 18, 28, 38, 34 };
        static readonly int[] KeyWb = { 0, 5, 9, 20, 17, 37, 29, 38, 34, 28, 46, 22 };
        static readonly int[] KeyMid = { 7, 10, 22, 23, 18, 28, 29, 37, 17, 52, 6, 1, 4, 9 };
        static readonly int[] KeyWide = { 1, 0, 6, 22, 23, 26, 34, 38, 46, 42, 10, 7, 4, 37 };
        static readonly int[] KeyAtt = { 2, 6, 22, 23, 52, 17, 34, 38, 3, 1, 36, 39, 46, 18 };
        static readonly int[] GkOnly = { 11, 12, 13, 14, 15, 16, 19, 21, 31, 32, 33 };

        static int[] KeyFor(int pos)
        {
            switch (pos)
            {
                case 0: return KeyGk;
                case 1: case 2: case 3: case 4: return KeyDef;
                case 13: case 14: return KeyWb;
                case 5: case 7: return KeyMid;
                case 6: case 8: case 9: case 11: return KeyWide;
                case 10: return KeyMid.Concat(new[] { 26, 1, 4 }).ToArray();
                default: return KeyAtt;
            }
        }

        void CreatePlayer()
        {
            if (loadedPlayer == null || fm == null) { MessageBox.Show("请先选择要重建的球员，建议使用本队履历较少的青年或预备队球员。", "新建球员"); return; }
            var p = loadedPlayer;
            var dlg = new Form { Text = "新建球员 - 重建 " + p.DisplayName, Width = 640, Height = 690, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), AutoScroll = true };
            Func<Dictionary<string, ulong>, TextBox> nameBox = dict =>
            {
                var tb = new TextBox { Width = 300, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.CustomSource };
                var src = new AutoCompleteStringCollection(); src.AddRange(dict.Keys.ToArray()); tb.AutoCompleteCustomSource = src; return tb;
            };
            var first = nameBox(firstNames); var last = nameBox(lastNames); var common = nameBox(commonNames);
            var dob = new TextBox { Width = 120, Text = DateTime.Today.AddYears(-17).ToString("yyyy-MM-dd") };
            var nat = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var kv in nations.OrderBy(k => k.Value)) nat.Items.Add(kv.Value);
            ulong curNat = fm.ReadPtr(p.Person + FmLayout.Nation); string curNatName;
            if (nations.TryGetValue(curNat, out curNatName)) nat.SelectedItem = curNatName;
            var pos = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var s in FmLayout.Positions) pos.Items.Add(s); pos.SelectedItem = "前锋";
            var extra = new CheckedListBox { Width = 300, Height = 90, CheckOnClick = true, MultiColumn = true, ColumnWidth = 60 };
            foreach (var s in FmLayout.Positions) extra.Items.Add(s);
            var foot = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList }; foot.Items.AddRange(new object[] { "右脚", "左脚", "双脚" }); foot.SelectedIndex = 0;
            var level = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            level.Items.AddRange(new object[] { "天才新星（逐渐成长）", "世界级球星（立即成型）", "优秀职业球员", "自定义" }); level.SelectedIndex = 0;
            var attr = new NumericUpDown { Minimum = 1, Maximum = 20, Value = 12, Width = 60 };
            var ca = new NumericUpDown { Minimum = 1, Maximum = 200, Value = 110, Width = 70 };
            var pa = new NumericUpDown { Minimum = 1, Maximum = 200, Value = 195, Width = 70 };
            level.SelectedIndexChanged += delegate
            {
                if (level.SelectedIndex == 0) { attr.Value = 12; ca.Value = 110; pa.Value = 195; }
                else if (level.SelectedIndex == 1) { attr.Value = 17; ca.Value = 175; pa.Value = 190; }
                else if (level.SelectedIndex == 2) { attr.Value = 13; ca.Value = 125; pa.Value = 135; }
            };
            var pers = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            pers.Items.AddRange(new object[] { "职业球员楷模", "平衡", "保持当前性格" }); pers.SelectedIndex = 0;
            var fresh = new CheckBox { Text = "恢复体能、比赛状态和士气；治疗伤病", Checked = true, AutoSize = true };

            int r = 0;
            AddRow(t, r++, "名字", first);
            AddRow(t, r++, "姓氏", last);
            AddRow(t, r++, "常用名（可留空）", common);
            AddRow(t, r++, "出生日期（年-月-日）", dob);
            AddRow(t, r++, "国籍", nat);
            AddRow(t, r++, "主要位置", pos);
            AddRow(t, r++, "其他位置（勾选）", extra);
            AddRow(t, r++, "惯用脚", foot);
            AddRow(t, r++, "球员水平", level);
            AddRow(t, r++, "属性水平（1-20）", attr);
            AddRow(t, r++, "当前能力（1-200）", ca);
            AddRow(t, r++, "潜在能力（1-200）", pa);
            AddRow(t, r++, "性格", pers);
            t.Controls.Add(fresh, 1, r++);
            var info = new Label { AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = UiMuted,
                Text = "姓名须来自 FM 姓名库，可输入文字查看建议。原球员的历史履历会保留，建议选择履历较少的青年球员。" +
                       "之后可在「详情与特质」设置特质，修改可以从「变更记录」撤销。" };
            t.Controls.Add(info, 0, r++); t.SetColumnSpan(info, 2);
            var ok = new Button { Text = "创建", Dock = DockStyle.Bottom, Height = 34 };
            dlg.Controls.Add(t); dlg.Controls.Add(ok);

            ok.Click += delegate
            {
                ulong eFirst, eLast, eCommon = 0;
                if (!firstNames.TryGetValue(first.Text.Trim(), out eFirst) || !lastNames.TryGetValue(last.Text.Trim(), out eLast))
                { MessageBox.Show("请从建议列表选择名字和姓氏，FM 只能使用已有姓名。", "新建球员"); return; }
                if (common.Text.Trim().Length > 0 && !commonNames.TryGetValue(common.Text.Trim(), out eCommon))
                { MessageBox.Show("FM 中没有这个常用名，请从建议列表选择或留空。", "新建球员"); return; }
                DateTime d;
                if (!DateTime.TryParseExact(dob.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) { MessageBox.Show("出生日期应采用 年-月-日 格式，例如 2005-06-30。", "新建球员"); return; }
                if (nat.SelectedItem == null) { MessageBox.Show("请选择国籍。", "新建球员"); return; }
                ulong eNat = nations.First(kv => kv.Value == (string)nat.SelectedItem).Key;

                var block = fm.Read(p.Block, FmLayout.PosCount + FmLayout.AttrCount);
                var head = fm.Read(p.Person, 0x80);
                if (!FmLayout.ValidBlock(block) || head == null) { MessageBox.Show("这名球员的数据已经移位，请重新点「挂接并扫描」。", "新建球员"); return; }

                // positions: main 20, extra 18, the rest 1
                int main = pos.SelectedIndex; var nb = (byte[])block.Clone();
                for (int k = 0; k < FmLayout.PosCount; k++) nb[k] = (byte)(k == main ? 20 : extra.GetItemChecked(k) ? 18 : 1);
                // attributes suited to the position(s)
                int L = (int)attr.Value; bool keeper = main == 0;
                var keys = new HashSet<int>(KeyFor(main)); for (int k = 0; k < FmLayout.PosCount; k++) if (extra.GetItemChecked(k)) foreach (var i in KeyFor(k)) keys.Add(i);
                for (int k = 0; k < FmLayout.AttrCount; k++)
                {
                    int v = keys.Contains(k) ? L + 3 : L;
                    if (GkOnly.Contains(k) && !keeper) v = 2;
                    if (keeper && !keys.Contains(k) && k <= 10) v = Math.Max(3, L - 6);
                    nb[FmLayout.AttrStart + k] = (byte)(Math.Max(1, Math.Min(20, v)) * 5);
                }
                Action<int, int> set = (k, v) => nb[FmLayout.AttrStart + k] = (byte)(Math.Max(1, Math.Min(20, v)) * 5);
                set(24, foot.SelectedIndex == 1 || foot.SelectedIndex == 2 ? 20 : Math.Max(6, L - 6));   // left foot
                set(25, foot.SelectedIndex == 0 || foot.SelectedIndex == 2 ? 20 : Math.Max(6, L - 6));   // right foot
                set(41, 3); set(44, Math.Min(20, L + 3)); set(47, Math.Min(20, L + 3)); set(48, 3); set(49, 14); set(51, Math.Min(20, L + 4)); set(50, Math.Min(20, L + 3));

                int n = 0; string who = first.Text.Trim() + " " + last.Text.Trim();
                if (WriteLogged(p.Block, nb, p.Name + " -> " + who + "：位置和属性")) n++;
                Action<ulong, ulong, string> w8 = (off, v, what) => { if (BitConverter.ToUInt64(head, (int)off) != v && WriteLogged(p.Person + off, BitConverter.GetBytes(v), who + ": " + what)) n++; };
                w8(FmLayout.FirstNameEntry, eFirst, "名字");
                w8(FmLayout.LastNameEntry, eLast, "姓氏");
                w8(FmLayout.CommonNameEntry, eCommon, "常用名");
                w8(FmLayout.Nation, eNat, "国籍 " + nat.SelectedItem);
                uint oDob = BitConverter.ToUInt32(head, (int)FmLayout.BirthDate);
                uint nDob = ((uint)d.Year << 16) | (oDob & 0xFE00u) | (uint)d.DayOfYear;
                if (nDob != oDob && WriteLogged(p.Person + FmLayout.BirthDate, BitConverter.GetBytes(nDob), who + "：出生日期 " + dob.Text.Trim())) n++;
                var abil = new byte[4]; BitConverter.GetBytes((short)ca.Value).CopyTo(abil, 0); BitConverter.GetBytes((short)pa.Value).CopyTo(abil, 2);
                if (Safety.On("ability") && WriteLogged(p.Tail - FmLayout.AbilityBeforePerson, abil, who + ": CA " + ca.Value + " / PA " + pa.Value)) n++;
                if (pers.SelectedIndex < 2 && Safety.On("personality"))
                {
                    // Adaptability, Ambition, Loyalty, Pressure, Professionalism, Sportsmanship, Temperament, Controversy
                    var pv = pers.SelectedIndex == 0 ? new byte[] { 14, 18, 15, 16, 20, 15, 16, 2 } : new byte[] { 12, 12, 12, 12, 12, 12, 12, 6 };
                    if (WriteLogged(p.Person + FmLayout.PersonalityAfterPerson, pv, who + "：性格 " + pers.SelectedItem)) n++;
                }
                if (fresh.Checked)
                {
                    var s = ReadStatus(p);
                    for (int k = 0; k < s.Length; k++) if (s[k] >= 0 && s[k] < FmLayout.StatusMax[k] && WriteStatus(p, k, s[k], FmLayout.StatusMax[k], who + ": " + FmLayout.StatusNames[k] + " -> 100")) n++;
                    if (Heal(p)) n++;
                }
                lblStatus.Text = who + "，重建自 " + p.Name + " (" + n + " 项修改）。姓名、国籍和年龄立即显示；属性在下一游戏日显示。";
                if(n>0)personDetailsRevision++;
                dlg.Close();
                LoadPlayer(p);
            };
            Zh.Tree(dlg);
            dlg.ShowDialog(this);
        }

        // ---------- bonuses & clauses ----------
        // Contract +0x60/+0x68/+0x70 = begin/end/capacity of 8-byte entries {int32 amount (pounds, -1 = none), int16 value, u16 type}.
        // FM shows names from the type; verified: 0, 4, 5, 18, 22, 32, 33, 34, 38, 39.
        static readonly Dictionary<int, string> ClauseNames = new Dictionary<int, string> {
            { 0, "最低解约金条款" }, { 18, "最低解约金条款（国外俱乐部）" },
            { 4, "升级后加薪（%）" }, { 5, "降级后降薪（%）" }, { 22, "俱乐部可选续约（年）" },
            { 32, "出场费" }, { 33, "进球奖金" }, { 34, "零封奖金" }, { 38, "未使用替补费" }, { 39, "助攻奖金" } };

        static string ClauseName(int t) { string n; return ClauseNames.TryGetValue(t, out n) ? n : "类型 " + t + "（未知）"; }

        // ---------- swap two players between clubs ----------
        // Each player takes the other's place in his team's player list, and their team pointers and contracts' club teams are
        // exchanged. Neither list changes size, so nothing is allocated. Contract terms and history stay with the player.
        // Tested in game (Joe Ward Derby <-> Aaron Morley Bolton): showed straight away, lasted two days, a match, a save and a reload.
        // League registration is a separate list: the incoming player shows "未注册" until FM's next registration window.

        bool TeamVector(ulong team, out ulong begin, out ulong end, out ulong cap)
        {
            begin = fm.ReadPtr(team + FmLayout.TeamPlayers); end = fm.ReadPtr(team + FmLayout.TeamPlayers + 8); cap = fm.ReadPtr(team + FmLayout.TeamPlayers + 16);
            return begin > 0x10000 && end >= begin && cap >= end && (end - begin) / 8 <= 500 && (end - begin) % 8 == 0;
        }

        string ClubNameOf(ulong club) { return clubs.Where(c => c.Addr == club).Select(c => c.Name).DefaultIfEmpty("（无俱乐部）").First(); }

        void SwapPlayerDialog()
        {
            var p = loadedPlayer; if (p == null || fm == null) return;
            var f = new Form { Width = 520, Height = 520, MaximizeBox = false };
            StyleWorkspaceDialog(f,"交换球员 · "+p.DisplayName);
            var search = new TextBox { Dock = DockStyle.Top };
            var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            var info = new Label { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(4),
                Text = p.DisplayName + " (" + ClubNameOf(p.Club) + "）将与所选球员互换球队。" +
                       "双方保留原合同（工资、期限和条款）。修改可以从「变更记录」撤销。" };
            var ok = new Button { Text = "交换球员" };
            var searchArea=SearchWithListSpacing(search);searchArea.TabIndex=0;list.TabIndex=1;
            var actions=DialogActions(f,DialogDismiss("CancelPlayerSwap","取消"),ok);
            actions.TabIndex=2;
            f.Controls.Add(list); f.Controls.Add(searchArea); f.Controls.Add(new Label { Text = "搜索球员（中文或英文，一字名须完整匹配）：", Dock = DockStyle.Top, Height = 22 });
            f.Controls.Add(info); f.Controls.Add(actions);
            search.TextChanged += delegate
            {
                list.BeginUpdate(); list.Items.Clear();
                var q = search.Text.Trim();
                if (PlayerNames.CanSearchPlayer(q))
                    foreach (var o in players.Where(x => x != p && x.Club != 0 && x.Matches(q)).OrderBy(x => x.MatchRank(q)).ThenBy(x => x.Name).Take(200))
                        list.Items.Add(new KeyValuePair<string, Player>(o + "  " + ClubNameOf(o.Club), o));
                list.DisplayMember = "Key";
                list.EndUpdate();
            };
            ok.Click += delegate
            {
                if (list.SelectedItem == null) { MessageBox.Show("请先选择球员。", "交换球员"); return; }
                if (SwapPlayers(p, ((KeyValuePair<string, Player>)list.SelectedItem).Value)) f.Close();
            };
            Zh.Tree(f);
            f.ShowDialog(this);
        }

        // Everything the swap needs about one player, checked before anything is written
        class SwapSide { public Player P; public ulong Obj, Team, Club, Team2, Contract, ContractTeam, ListAddr; }

        SwapSide SwapInfo(Player p)
        {
            ulong teamVt = fm.Base + FmProcess.TeamVtRva;
            var s = new SwapSide { P = p, Obj = p.Tail - FmLayout.PlayerObjBeforePerson };
            s.Team = fm.ReadPtr(s.Obj + FmLayout.PlayerTeam);
            if (s.Team < 0x10000 || fm.ReadPtr(s.Team) != teamVt || !Safety.HasContract(fm, p.Person)) { MessageBox.Show(p.Name + " 目前是自由球员，只有在俱乐部效力的球员才能交换、转会或解约。", "交换球员"); return null; }
            s.Club = fm.ReadPtr(s.Team + FmLayout.TeamClub2);
            s.Team2 = fm.ReadPtr(s.Obj + FmLayout.PlayerTeam2);
            if (s.Team2 < 0x10000 || fm.ReadPtr(s.Team2) != teamVt || fm.ReadPtr(s.Team2 + FmLayout.TeamClub2) != s.Club) s.Team2 = 0;
            ulong c = fm.ReadPtr(p.Person + FmLayout.ContractAfterPerson);
            if (c > 0x10000 && fm.ReadPtr(c) == fm.Base + FmProcess.ContractVtRva) { s.Contract = c; s.ContractTeam = fm.ReadPtr(c + FmLayout.ContractTeam); }
            if (s.ContractTeam > 0x10000 && fm.ReadPtr(s.ContractTeam + FmLayout.TeamClub2) != s.Club) { MessageBox.Show(p.Name + " 正处于租借中，请先在 FM 中结束租借再试。", "交换球员"); return null; }
            ulong b, e, cap;
            if (!TeamVector(s.Team, out b, out e, out cap)) { MessageBox.Show("球队数据异常，球员：" + p.Name + "。请重新「挂接并扫描」。", "交换球员"); return null; }
            var v = fm.Read(b, (int)(e - b));
            for (int k = 0; v != null && k < v.Length / 8; k++) if (BitConverter.ToUInt64(v, 8 * k) == s.Obj) { s.ListAddr = b + (ulong)(8 * k); break; }
            if (s.ListAddr == 0) { MessageBox.Show(p.Name + " 不在所属球队名单中，请重新「挂接并扫描」。", "交换球员"); return null; }
            return s;
        }

        bool SwapPlayers(Player p1, Player p2)
        {
            var a = SwapInfo(p1); if (a == null) return false;
            var b = SwapInfo(p2); if (b == null) return false;
            if (a.Team == b.Team) { MessageBox.Show(p1.Name + " 与 " + p2.Name + " 已在同一支球队效力。", "交换球员"); return false; }
            string ca = ClubNameOf(a.Club), cb = ClubNameOf(b.Club);
            if (MessageBox.Show("交换 " + p1.Name + " (" + ca + "）与 " + p2.Name + " (" + cb + "）立即交换球队？\n\n双方保留原合同和工资。联赛注册需在下一个注册窗口完成。请先备份存档。",
                    "交换球员", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;

            curGroup = ++lastGroup;
            try
            {
                string what = "已交换 " + p1.Name + " (" + ca + ") <-> " + p2.Name + " (" + cb + ")";
                // 1. each takes the other's place in the team lists
                if (!WriteLogged(a.ListAddr, BitConverter.GetBytes(b.Obj), what + " (" + ca + "名单）")) return false;
                if (!WriteLogged(b.ListAddr, BitConverter.GetBytes(a.Obj), what + " (" + cb + "名单）")) return false;
                // 2. their team pointers and the contracts' club teams
                foreach (var x in new[] { new[] { a, b }, new[] { b, a } })
                {
                    SwapSide me = x[0], other = x[1];
                    string who = what + " (" + me.P.Name;
                    WriteLogged(me.Obj + FmLayout.PlayerTeam, BitConverter.GetBytes(other.Team), who + "球队）");
                    if (me.Team2 != 0) WriteLogged(me.Obj + FmLayout.PlayerTeam2, BitConverter.GetBytes(other.Team2 != 0 ? other.Team2 : other.Team), who + "第二球队）");
                    if (me.ContractTeam > 0x10000) WriteLogged(me.Contract + FmLayout.ContractTeam, BitConverter.GetBytes(other.ContractTeam > 0x10000 ? other.ContractTeam : other.Team), who + "合同俱乐部）");
                }
            }
            finally { curGroup = 0; }
            p1.Club = b.Club; p2.Club = a.Club;
            rosterIndex = null;
            LogLine("球员交换：" + p1.Name + " " + ca + " -> " + cb + ", " + p2.Name + " " + cb + " -> " + ca);
            lblStatus.Text = p1.Name + " 与 " + p2.Name + " 已交换俱乐部，FM 中立即显示。";
            LoadPlayer(p1);
            return true;
        }

        // ---------- move a player to another club ----------
        // Only into a team whose player list already has a free place (FM keeps spare room after its own transfers; right after
        // a save is loaded every list is exactly full). Nothing is allocated: he is taken out of his old list (the rest moves up),
        // written into the free place and his team pointers and contract club are repointed, like the swap.

        List<ulong> ClubTeamList(ulong club)
        {
            var l = new List<ulong>();
            ulong b = fm.ReadPtr(club + FmLayout.ClubTeams), e = fm.ReadPtr(club + FmLayout.ClubTeams + 8);
            if (b < 0x10000 || e < b || e - b > 8 * 64) return l;
            for (ulong a = b; a < e; a += 8)
            {
                ulong t = fm.ReadPtr(a);
                if (t > 0x10000 && fm.ReadPtr(t) == fm.Base + FmProcess.TeamVtRva && fm.ReadPtr(t + FmLayout.TeamClub2) == club) l.Add(t);
            }
            return l;
        }

        void MovePlayerDialog()
        {
            var p = loadedPlayer; if (p == null || fm == null) return;
            var f = new Form { Width = 520, Height = 520, MaximizeBox = false };
            StyleWorkspaceDialog(f,"转移球员 · "+p.DisplayName);
            var search = new TextBox { Dock = DockStyle.Top };
            var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            var teamBox = new ComboBox { Dock = DockStyle.Bottom, DropDownStyle = ComboBoxStyle.DropDownList };
            var info = new Label { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(4),
                Text = "请选择俱乐部和球队。球员立即加盟并保留原合同。目标球队的" +
                       "球员名单须有空位：FM 在游戏内完成转会后会分配空位，刚读取存档时可能没有空位。" +
                       "没有空位时可使用「交换」功能。修改可以从「变更记录」撤销。" };
            var ok = new Button { Text = "转移" };
            var searchArea=SearchWithListSpacing(search);searchArea.TabIndex=0;list.TabIndex=1;teamBox.TabIndex=2;
            var actions=DialogActions(f,DialogDismiss("CancelPlayerTransfer","取消"),ok);
            actions.TabIndex=3;
            f.Controls.Add(list); f.Controls.Add(searchArea); f.Controls.Add(new Label { Text = "搜索俱乐部（至少 2 个字符）：", Dock = DockStyle.Top, Height = 22 });
            f.Controls.Add(teamBox); f.Controls.Add(info); f.Controls.Add(actions);
            search.TextChanged += delegate
            {
                list.BeginUpdate(); list.Items.Clear();
                var q = search.Text.Trim();
                if (PlayerNames.CanSearch(q)) list.Items.AddRange(clubs.Where(c=>c.HasFullFinance && ClubSearch.Matches(c,q)).Take(200).ToArray());
                list.EndUpdate();
            };
            list.SelectedIndexChanged += delegate
            {
                teamBox.Items.Clear();
                var c = list.SelectedItem as Club; if (c == null) return;
                var ts = ClubTeamList(c.Addr);
                for (int k = 0; k < ts.Count; k++)
                {
                    ulong b, e, cap; if (!TeamVector(ts[k], out b, out e, out cap)) continue;
                    long n = (long)((e - b) / 8), room = (long)((cap - e) / 8);
                    teamBox.Items.Add(new KeyValuePair<string, ulong>((k == 0 ? "一线队" : "球队 " + (k + 1) + "（预备队或青年队）") + ": " + n + " 名球员，" +
                        (room > 0 ? room + " 个空位" : "当前没有空位"), ts[k]));
                }
                teamBox.DisplayMember = "Key";
                if (teamBox.Items.Count > 0) teamBox.SelectedIndex = 0;
            };
            ok.Click += delegate
            {
                var c = list.SelectedItem as Club;
                if (c == null || teamBox.SelectedItem == null) { MessageBox.Show("请先选择俱乐部和球队。", "转移"); return; }
                if (MovePlayer(p, c, ((KeyValuePair<string, ulong>)teamBox.SelectedItem).Value)) f.Close();
            };
            Zh.Tree(f);
            f.ShowDialog(this);
        }

        bool MovePlayer(Player p, Club dstClub, ulong dst)
        {
            var s = SwapInfo(p); if (s == null) return false;
            if (s.Team == dst) { MessageBox.Show(p.Name + " 已在该球队效力。", "转移"); return false; }
            ulong sb, se, sc, db, de, dc;
            if (!TeamVector(s.Team, out sb, out se, out sc) || !TeamVector(dst, out db, out de, out dc)) { MessageBox.Show("球队数据异常，请重新「挂接并扫描」。", "转移"); return false; }
            if (de >= dc) { MessageBox.Show("目标球队的球员名单暂时没有空位。可等待游戏内转会后分配空位，或使用「交换」将 " + p.Name + " 与目标球队的球员交换。", "转移"); return false; }
            string to = dstClub.Name, from = ClubNameOf(s.Club);
            if (MessageBox.Show("转移 " + p.Name + "，从 " + from + " to " + to + "？\n\n球员保留原合同和工资，联赛注册需在下一个注册窗口完成。请先备份存档。",
                    "转移", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
            ulong dstClubTeam = ClubTeamList(dstClub.Addr).DefaultIfEmpty(dst).First();   // contracts point at the club's first team

            curGroup = ++lastGroup;
            try
            {
                string what = "已转移 " + p.Name + " " + from + " -> " + to;
                // 1. take him out of the old list (the rest moves up one place)
                int n = (int)((se - sb) / 8), idx = (int)((s.ListAddr - sb) / 8);
                var srcList = fm.Read(sb, n * 8); if (srcList == null) return false;
                var tail = new byte[(n - idx) * 8];
                Array.Copy(srcList, (idx + 1) * 8, tail, 0, (n - idx - 1) * 8);
                if (!WriteLogged(s.ListAddr, tail, what + " (" + from + "名单）")) return false;
                if (!WriteLogged(s.Team + FmLayout.TeamPlayers + 8, BitConverter.GetBytes(se - 8), what + " (" + from + "数量）")) return false;
                // 2. into the free place of the new list
                if (!WriteLogged(de, BitConverter.GetBytes(s.Obj), what + " (" + to + "名单）")) return false;
                if (!WriteLogged(dst + FmLayout.TeamPlayers + 8, BitConverter.GetBytes(de + 8), what + " (" + to + "数量）")) return false;
                // 3. his team pointers and the contract's club team
                WriteLogged(s.Obj + FmLayout.PlayerTeam, BitConverter.GetBytes(dst), what + "（球队）");
                if (s.Team2 != 0) WriteLogged(s.Obj + FmLayout.PlayerTeam2, BitConverter.GetBytes(dst), what + "（第二球队）");
                if (s.ContractTeam > 0x10000) WriteLogged(s.Contract + FmLayout.ContractTeam, BitConverter.GetBytes(dstClubTeam), what + "（合同俱乐部）");
            }
            finally { curGroup = 0; }
            p.Club = dstClub.Addr;
            rosterIndex = null;
            LogLine("球员转移：" + p.Name + " " + from + " -> " + to);
            lblStatus.Text = p.Name + " 已转移至 " + to + "。FM 中立即显示。";
            LoadPlayer(p);
            return true;
        }

        // ---------- release a player ----------
        // A free agent has no team pointers (player +0x130/+0x138 = 0) and no contract (person +0xC8 = 0). Releasing takes him out of
        // his team's list (the rest moves up), clears both team pointers and the contract pointer. The contract object itself is
        // left alone, so Undo can point back at it.

        bool ReleasePlayer(Player p)
        {
            if (p == null || fm == null) return false;
            var s = SwapInfo(p); if (s == null) return false;
            ulong sb, se, sc;
            if (!TeamVector(s.Team, out sb, out se, out sc)) { MessageBox.Show("球队数据异常，请重新「挂接并扫描」。", "解约"); return false; }
            string from = ClubNameOf(s.Club);
            if (MessageBox.Show("解约 " + p.Name + "，从 " + from + "？球员将终止合同并成为自由球员，包括本队在内的任何俱乐部都可签约。\n\n请先备份存档。",
                    "解约", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
            curGroup = ++lastGroup;
            try
            {
                string what = "已解约 " + p.Name + "，从 " + from;
                int n = (int)((se - sb) / 8), idx = (int)((s.ListAddr - sb) / 8);
                var list = fm.Read(sb, n * 8); if (list == null) return false;
                var tail = new byte[(n - idx) * 8];
                Array.Copy(list, (idx + 1) * 8, tail, 0, (n - idx - 1) * 8);
                if (!WriteLogged(s.ListAddr, tail, what + " (" + from + "名单）")) return false;
                if (!WriteLogged(s.Team + FmLayout.TeamPlayers + 8, BitConverter.GetBytes(se - 8), what + " (" + from + "数量）")) return false;
                WriteLogged(s.Obj + FmLayout.PlayerTeam, BitConverter.GetBytes(0UL), what + "（球队）");
                if (fm.ReadPtr(s.Obj + FmLayout.PlayerTeam2) != 0) WriteLogged(s.Obj + FmLayout.PlayerTeam2, BitConverter.GetBytes(0UL), what + "（第二球队）");
                if (s.Contract != 0) WriteLogged(p.Person + FmLayout.ContractAfterPerson, BitConverter.GetBytes(0UL), what + "（合同）");
            }
            finally { curGroup = 0; }
            p.Club = 0;
            rosterIndex = null;
            LogLine("球员解约：" + p.Name + "，从 " + from);
            lblStatus.Text = p.Name + " 现已成为自由球员。";
            LoadPlayer(p);
            return true;
        }

        void EditClauses()
        {
            if (loadedPlayer == null || loadedContract == 0) return;
            var v = fm.Read(loadedContract + FmLayout.ContractClauses, 24); if (v == null) return;
            ulong begin = BitConverter.ToUInt64(v, 0), end = BitConverter.ToUInt64(v, 8), cap = BitConverter.ToUInt64(v, 16);
            if (end < begin || cap < end || end - begin > 0x400) { MessageBox.Show("无法读取这份合同的条款。", "合同条款"); return; }
            var orig = end > begin ? fm.Read(begin, (int)(end - begin)) : new byte[0];
            double r = Rate();

            var dlg = new Form { Width = 720, Height = 420 };
            StyleWorkspaceDialog(dlg,"奖金与条款 · "+loadedPlayer.Name);
            var grid = NewGrid();grid.TabIndex=0;
            var typeCol = new DataGridViewComboBoxColumn { HeaderText = "类型", FillWeight = 200 };
            foreach (var kv in ClauseNames.OrderBy(k => k.Value)) typeCol.Items.Add(kv.Value);
            grid.Columns.Add(typeCol);
            grid.Columns.Add("amount", "金额（" + Cur() + ")");
            grid.Columns.Add("value", "数值（%、年）");
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "移除", FillWeight = 50 });
            int n = orig.Length / 8;
            for (int k = 0; k < n; k++)
            {
                int amt = BitConverter.ToInt32(orig, 8 * k), val = BitConverter.ToInt16(orig, 8 * k + 4), ty = BitConverter.ToUInt16(orig, 8 * k + 6);
                string name = ClauseName(ty); if (!typeCol.Items.Contains(name)) typeCol.Items.Add(name);
                grid.Rows.Add(name, amt < 0 ? "" : Money(amt * r), val < 0 ? "" : val.ToString(), false);
            }
            var info = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(4), ForeColor = UiMuted,
                Text = "可修改金额、数值或类型，或勾选「移除」。金额使用当前显示货币（游戏内按英镑存储）。" +
                       "当前可容纳 " + ((cap - begin) / 8) + " 项条款。要添加解约金条款，可修改已有奖金项的类型。" };
            var ok = new Button { Text = "应用" };
            var actions=DialogActions(dlg,DialogDismiss("CancelContractClauses","取消"),ok);
            actions.TabIndex=1;
            dlg.Controls.Add(grid); dlg.Controls.Add(info); dlg.Controls.Add(actions);
            ok.Click += delegate
            {
                var outp = new List<byte>(); var log = new List<string>();
                for (int k = 0; k < n; k++)
                {
                    var row = grid.Rows[k];
                    if ((row.Cells[3].Value as bool?) == true) { log.Add("已移除 " + ClauseName(BitConverter.ToUInt16(orig, 8 * k + 6))); continue; }
                    string tname = Convert.ToString(row.Cells[0].Value);
                    int ty = ClauseNames.Where(kv => kv.Value == tname).Select(kv => kv.Key).DefaultIfEmpty(BitConverter.ToUInt16(orig, 8 * k + 6)).First();
                    double dollars; int amt = -1, val = -1;
                    string at = Convert.ToString(row.Cells[1].Value), vt = Convert.ToString(row.Cells[2].Value);
                    if (at.Trim().Length > 0 && ParseMoney(at, out dollars)) amt = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(dollars / r)));
                    if (vt.Trim().Length > 0 && !int.TryParse(vt, out val)) val = -1;
                    // keep the original pound amount when the displayed dollar text was not touched (avoids rounding drift)
                    int oamt = BitConverter.ToInt32(orig, 8 * k); if (oamt >= 0 && at == Money(oamt * r)) amt = oamt;
                    outp.AddRange(BitConverter.GetBytes(amt)); outp.AddRange(BitConverter.GetBytes((short)val)); outp.AddRange(BitConverter.GetBytes((ushort)ty));
                }
                var cur = fm.Read(begin, orig.Length);
                if (cur == null || !cur.SequenceEqual(orig)) { MessageBox.Show("打开窗口后，游戏已更新这份合同，请重新加载后再试。", "合同条款"); return; }
                var newList = outp.ToArray();
                if (newList.Length > 0 && !newList.SequenceEqual(orig.Take(newList.Length)))
                    WriteLogged(begin, newList, loadedPlayer.Name + "：奖金与条款已修改");
                if (newList.Length < orig.Length)
                    WriteLogged(loadedContract + FmLayout.ContractClauses + 8, BitConverter.GetBytes(begin + (ulong)newList.Length), loadedPlayer.Name + ": " + string.Join(", ", log.ToArray()));
                lblStatus.Text = "合同条款已更新，球员：" + loadedPlayer.Name + "。请在 FM 中重新打开球员的合同页查看。";
                dlg.Close();
            };
            Zh.Tree(dlg);
            dlg.ShowDialog(this);
        }

        // Player object +0x340 -> contract object (class fm.exe+0x5667C88, +0x08 points back to the person)
        ulong ReadContract(Player p)
        {
            if (!Safety.On("contract")) return 0;
            ulong c = fm.ReadPtr(p.Person + FmLayout.ContractAfterPerson);
            if (c < 0x10000 || fm.ReadPtr(c) != fm.Base + FmProcess.ContractVtRva || fm.ReadPtr(c + 8) != p.Person) return 0;
            return c;
        }

        // FM date: low 9 bits day of year (1-based), bits 9-15 time of day, high 16 bits year
        static DateTime FromPacked(uint v)
        {
            int year = (int)(v >> 16), day = (int)(v & 0x1FF);
            if (year < 1900 || year > 2200 || day < 1 || day > 366) return new DateTime(1900, 1, 1);
            return new DateTime(year, 1, 1).AddDays(day - 1);
        }

        int[] ReadStatus(Player p)
        {
            if (!Safety.On("status")) return new int[0];
            var s = new int[FmLayout.StatusNames.Length];
            for (int k = 0; k < s.Length; k++) s[k]=StatusValues.Decode(fm.Read(p.Tail-FmLayout.StatusBeforePerson[k],FmLayout.StatusSize[k]),k);
            return s;
        }

        // Fully rested (jadedness -500) when the player is more tired than that
        bool Rest(Player p)
        {
            if (!Safety.On("jaded")) return false;
            ulong a = p.Tail - FmLayout.JadedBeforePerson; var b = fm.Read(a, 2); if (b == null) return false;
            short v = BitConverter.ToInt16(b, 0);
            if(v<=FmLayout.JadedRested)return false;
            var data=BitConverter.GetBytes((short)FmLayout.JadedRested);
            if(fm.VerifiedCurrentBuild)
            {
                try{var snapshot=CurrentFieldSnapshot.Capture(fm.Read,fm.Base,p,a,2);var plan=snapshot.Plan(data,p.DisplayName+"：恢复疲劳度");return CommitCurrent("jaded",plan,()=>snapshot.Reader.Matches(fm.Read,plan));}
                catch{return false;}
            }
            return WriteLogged(a,data,p.Name+"：疲劳度 "+v+" -> "+FmLayout.JadedRested);
        }

        static int ToPercent(int raw, int k) { return raw < 0 ? 0 : (int)Math.Round(raw * 100.0 / FmLayout.StatusMax[k]); }

        // Writes one status value (condition / sharpness / morale) if the game has not changed it since 'expected'
        bool WriteStatus(Player p, int k, int expected, int raw, string what)
        {
            ulong a = p.Tail - FmLayout.StatusBeforePerson[k];
            var cur = fm.Read(a,FmLayout.StatusSize[k]);
            if(StatusValues.Decode(cur,k)!=expected)return false;
            byte[] value=StatusValues.Encode(raw,k);
            if(fm.VerifiedCurrentBuild)
            {
                try
                {
                    var snapshot=CurrentFieldSnapshot.Capture(fm.Read,fm.Base,p,a,value.Length);
                    if(!snapshot.Before.SequenceEqual(cur))return false;
                    var plan=snapshot.Plan(value,what);return CommitCurrent("status",plan,()=>snapshot.Reader.Matches(fm.Read,plan));
                }
                catch{return false;}
            }
            return WriteLogged(a,value,what);
        }

        // Current-injury list of a player: medical object address, list begin and end (0 when unavailable)
        bool ReadInjuryList(Player p, out ulong med, out ulong begin, out ulong end)
        {
            begin = end = 0; med = 0;
            if (!Safety.On("injury")) return false;
            med = fm.ReadPtr(p.Tail - FmLayout.MedicalBeforePerson);
            if (med < 0x10000) return false;
            var v = fm.Read(med, 24);
            if (v == null) return false;
            begin = BitConverter.ToUInt64(v, 0); end = BitConverter.ToUInt64(v, 8);
            ulong cap = BitConverter.ToUInt64(v, 16);
            return begin <= end && end <= cap && end - begin < 0x400 && (end - begin) % 8 == 0;
        }

        int InjuryCount(Player p)
        {
            if(fm!=null && fm.VerifiedCurrentBuild){try{return CurrentInjurySnapshot.Capture(fm.Read,fm.Base,p).Count;}catch{return 0;}}
            ulong med, begin, end;
            return ReadInjuryList(p, out med, out begin, out end) ? (int)((end - begin) / 8) : 0;
        }

        void HealPlayer()
        {
            if (loadedPlayer == null) return;
            if (Heal(loadedPlayer)) lblStatus.Text = loadedPlayer.Name + " 已治疗，请在 FM 中推进一天后查看。";
            LoadPlayer(loadedPlayer);
        }

        // Empties the player's current-injury list (end = begin), as FM does when a player recovers
        bool Heal(Player p)
        {
            if(fm!=null && fm.VerifiedCurrentBuild)
            {
                if(!Safety.On("injury"))return false;
                try{var snapshot=CurrentInjurySnapshot.Capture(fm.Read,fm.Base,p);var plan=snapshot.Plan();return plan.Count>0 && CommitCurrent("injury",plan,()=>snapshot.Reader.Matches(fm.Read,plan));}
                catch(Exception ex){LogLine(p.DisplayName+"：治疗停止："+ex.Message);return false;}
            }
            ulong med, begin, end;
            if (!Safety.On("injury") || !ReadInjuryList(p, out med, out begin, out end) || end == begin) return false;
            int n = (int)((end - begin) / 8);
            return WriteLogged(med + 8, BitConverter.GetBytes(begin), p.Name + "：已治疗 " + n + " 项伤病");
        }

        static int ToDisplay(int raw) { return Math.Max(1, Math.Min(20, (int)Math.Round(raw / 5.0, MidpointRounding.AwayFromZero))); }

        void ApplyPlayer()
        {
            if (loadedPlayer == null || loadedBlock == null || fm==null || !Safety.On("core"))
            {
                string reason;
                if (fm!=null && Safety.Off.TryGetValue("core",out reason))
                    ShowOperationFeedback(FeedbackKind.ReadOnly,"球员资料只读。"+reason);
                else ShowOperationFeedback(FeedbackKind.Information,"请选择球员并读取资料后再提交。");
                return;
            }
            var selected=loadedPlayer;
            try
            {
                if(!object.ReferenceEquals(RefreshSelectedPlayer(selected),selected) || ReadContract(selected)!=loadedContract)
                {
                    LoadPlayer(selected);
                    ShowOperationFeedback(FeedbackKind.Failed,"球员资料已更新，请核对当前数值后重新编辑。");
                    return;
                }
            }
            catch(Exception ex)
            {
                string reason="球员资料需要重新读取。"+ex.Message.Replace("注册数据","球员资料");
                ClearPlayerPreview(reason);
                ShowOperationFeedback(FeedbackKind.Failed,reason);
                return;
            }
            var current = fm.Read(loadedPlayer.Block, FmLayout.PosCount + FmLayout.AttrCount);
            if (!FmLayout.ValidBlock(current))
            {
                ShowOperationMessage(FeedbackKind.Failed,"这名球员的数据已经移位，请重新点「挂接并扫描」。","球员");
                return;
            }

            int written = 0, skipped = 0;
            for (int k = 0; k < FmLayout.AttrCount; k++)
            {
                int v;
                if (!int.TryParse(Convert.ToString(gridAttrs.Rows[k].Cells[1].Value), out v)) continue;
                v = Math.Max(1, Math.Min(20, v));
                int oldRaw = loadedBlock[FmLayout.AttrStart + k];
                if (v == ToDisplay(oldRaw)) continue;
                if (current[FmLayout.AttrStart + k] != oldRaw) { skipped++; continue; }
                if (WriteLogged(loadedPlayer.Block + (ulong)(FmLayout.AttrStart + k), new[] { (byte)(v * 5) },
                    loadedPlayer.Name + ": " + Zh.Attr(FmLayout.Attrs[k]) + " " + ToDisplay(oldRaw) + " -> " + v)) written++;
            }
            if (loadedPersonality != null)
            {
                var curP = fm.Read(loadedPlayer.Person + FmLayout.PersonalityAfterPerson, 8);
                for (int k = 0; k < 8; k++)
                {
                    int v;
                    if (!int.TryParse(Convert.ToString(gridAttrs.Rows[FmLayout.AttrCount + k].Cells[1].Value), out v)) continue;
                    v = Math.Max(1, Math.Min(20, v));
                    if (v == loadedPersonality[k]) continue;
                    if (curP == null || curP[k] != loadedPersonality[k]) { skipped++; continue; }
                    if (WriteLogged(loadedPlayer.Person + FmLayout.PersonalityAfterPerson + (ulong)k, new[] { (byte)v },
                        loadedPlayer.Name + ": " + FmLayout.Personality[k].Split((char)124)[0] + " " + loadedPersonality[k] + " -> " + v)) written++;
                }
            }
            for (int k = 0; k < FmLayout.PosCount; k++)
            {
                int v;
                if (!int.TryParse(Convert.ToString(gridPos.Rows[k].Cells[1].Value), out v)) continue;
                v = Math.Max(1, Math.Min(20, v));
                if (v == loadedBlock[k]) continue;
                if (current[k] != loadedBlock[k]) { skipped++; continue; }
                if (WriteLogged(loadedPlayer.Block + (ulong)k, new[] { (byte)v },
                    loadedPlayer.Name + "：位置 " + FmLayout.Positions[k] + " " + loadedBlock[k] + " -> " + v)) written++;
            }
            for (int k = 0; k < loadedStatus.Length; k++)
            {
                int pct;
                if (loadedStatus[k] < 0 || !int.TryParse(Convert.ToString(gridPos.Rows[FmLayout.PosCount + k].Cells[1].Value), out pct)) continue;
                pct = Math.Max(0, Math.Min(100, pct));
                if (pct == ToPercent(loadedStatus[k], k)) continue;
                int raw = (int)Math.Round(pct * FmLayout.StatusMax[k] / 100.0);
                if (WriteStatus(loadedPlayer, k, loadedStatus[k], raw, loadedPlayer.Name + ": " + FmLayout.StatusNames[k] + " " + ToPercent(loadedStatus[k], k) + " -> " + pct)) written++;
                else skipped++;
            }
            if (loadedContract != 0)
            {
                int row = FmLayout.PosCount + loadedStatus.Length;
                double r = Rate(), dollars;
                string wageText = Convert.ToString(gridPos.Rows[row].Cells[1].Value);
                if (wageText != Money(loadedWage * r * 52) && ParseMoney(wageText, out dollars))
                {
                    int perWeek = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(dollars / r / 52)));
                    if (perWeek != loadedWage)
                    {
                        var cur = fm.ReadI32(loadedContract + FmLayout.ContractWage);
                        if (cur == loadedWage && WriteLogged(loadedContract + FmLayout.ContractWage, BitConverter.GetBytes(perWeek),
                            loadedPlayer.Name + "：周薪 £" + loadedWage + "/wk -> £" + perWeek + "/wk")) written++;
                        else skipped++;
                    }
                }
                DateTime until;
                string dateText = Convert.ToString(gridPos.Rows[row + 1].Cells[1].Value);
                if (dateText != FromPacked(loadedExpiry).ToString("yyyy-MM-dd") &&
                    DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out until))
                {
                    uint packed = ((uint)until.Year << 16) | (loadedExpiry & 0xFE00u) | (uint)until.DayOfYear;
                    var cur = fm.Read(loadedContract + FmLayout.ContractExpiry, 4);
                    if (cur != null && BitConverter.ToUInt32(cur, 0) == loadedExpiry && WriteLogged(loadedContract + FmLayout.ContractExpiry, BitConverter.GetBytes(packed),
                        loadedPlayer.Name + "：合同到期日 " + FromPacked(loadedExpiry).ToString("yyyy-MM-dd") + " -> " + dateText)) written++;
                    else skipped++;
                }
            }
            if (loadedCa >= 0)
            {
                var names = new[] { "当前能力", "潜在能力" }; var old = new[] { loadedCa, loadedPa };
                for (int k = 0; k < 2; k++)
                {
                    int v;
                    if (!int.TryParse(Convert.ToString(gridPos.Rows[loadedAbilityRow + k].Cells[1].Value), out v)) continue;
                    if (v == old[k] || v < -200 || v > 200 || v == 0) continue;
                    ulong a = loadedPlayer.Tail - FmLayout.AbilityBeforePerson + (ulong)(2 * k);
                    var cur = fm.Read(a, 2);
                    if (cur != null && BitConverter.ToInt16(cur, 0) == old[k] && WriteLogged(a, BitConverter.GetBytes((short)v), loadedPlayer.Name + ": " + names[k] + " " + old[k] + " -> " + v)) written++;
                    else skipped++;
                }
            }
            if (loadedTransferRow >= 0)
            {
                bool wantT = IsYes(gridPos.Rows[loadedTransferRow].Cells[1].Value), wantL = IsYes(gridPos.Rows[loadedTransferRow + 1].Cells[1].Value);
                byte flags = (byte)((loadedListFlags & ~3) | (wantT ? 1 : 0) | (wantL ? 2 : 0));
                if (flags != loadedListFlags)
                {
                    var cur = fm.Read(loadedContract + FmLayout.ContractListFlags, 1);
                    if (cur == null || cur[0] != loadedListFlags) skipped++;
                    else
                    {
                        if (((flags ^ loadedListFlags) & 1) != 0 && wantT && !Safety.On("date"))
                        {
                            MessageBox.Show("转会挂牌需要读取游戏日期，但当前版本的日期未通过安全检查。仍可修改租借挂牌和要价。", "转会状态");
                            flags = (byte)((flags & ~1) | (loadedListFlags & 1));
                        }
                        if (((flags ^ loadedListFlags) & 1) != 0)
                        {
                            // same bookkeeping FM does: listing date and listing code with the transfer-listed bit
                            uint date = wantT ? (fm.ReadGameDate() ?? FmLayout.EmptyDate) : FmLayout.EmptyDate;
                            WriteLogged(loadedContract + FmLayout.ContractListedDate, BitConverter.GetBytes(date), loadedPlayer.Name + "：挂牌日期");
                            WriteLogged(loadedContract + FmLayout.ContractListCode, new[] { (byte)(wantT ? 4 : 0) }, loadedPlayer.Name + "：挂牌代码");
                        }
                        if (flags != loadedListFlags && WriteLogged(loadedContract + FmLayout.ContractListFlags, new[] { flags }, loadedPlayer.Name + "：转会挂牌 " + (wantT ? "yes" : "no") + "，租借挂牌 " + (wantL ? "yes" : "no"))) written++;
                    }
                }
                string askText = Convert.ToString(gridPos.Rows[loadedTransferRow + 2].Cells[1].Value ?? "").Trim();
                double askMoney;
                int ask = askText.Length == 0 ? -1 : (ParseMoney(askText, out askMoney) ? (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(askMoney / Rate()))) : loadedAsk);
                if (askText != (loadedAsk < 0 ? "" : Money(loadedAsk * Rate())) && ask != loadedAsk)
                {
                    ulong a = loadedPlayer.Tail - FmLayout.AskingBeforePerson;
                    var cur = fm.ReadI32(a);
                    if (cur == loadedAsk && WriteLogged(a, BitConverter.GetBytes(ask), loadedPlayer.Name + "：要价 " + (loadedAsk < 0 ? "未设置" : "£" + loadedAsk) + " -> " + (ask < 0 ? "未设置" : "£" + ask))) written++;
                    else skipped++;
                }
            }
            if (loadedSquadRow >= 0)
            {
                string want = Convert.ToString(gridPos.Rows[loadedSquadRow].Cells[1].Value);
                // the list shows Chinese names, so match the stored English name or its translation
                int code = FmLayout.SquadStatusOffered.Where(v => FmLayout.SquadStatusNames[v] == want || Zh.Z(FmLayout.SquadStatusNames[v]) == want).DefaultIfEmpty(-1).First();
                if (code >= 0 && want != Zh.Z(SquadStatusText(loadedSquad)))
                {
                    var cur = fm.Read(loadedContract + FmLayout.ContractSquadStatus, 1);
                    if (cur != null && cur[0] == loadedSquad && WriteLogged(loadedContract + FmLayout.ContractSquadStatus, new[] { (byte)code },
                        loadedPlayer.Name + "：球队定位 " + SquadStatusText(loadedSquad) + " -> " + want)) written++;
                    else skipped++;
                }
            }
            foreach (var nr in loadedNums)
            {
                int v;
                if (!int.TryParse(Convert.ToString(gridPos.Rows[nr.Row].Cells[1].Value), out v)) continue;
                v = Math.Max(nr.Min, Math.Min(nr.Max, v));
                if (v == nr.Old) continue;
                if(fm.VerifiedCurrentBuild && loadedShirt!=null && nr.Addr==loadedShirt.Address)
                {
                    try{var shirtSnapshot=loadedShirt;var plan=shirtSnapshot.Plan(v);if(CommitCurrent("squadnumber",plan,()=>shirtSnapshot.Reader.Matches(fm.Read,plan)))written++;else skipped++;}
                    catch(Exception ex){LogLine("球衣号码未修改："+ex.Message);skipped++;}
                    continue;
                }
                var cur = fm.Read(nr.Addr, nr.Bytes);
                int c = cur == null ? int.MinValue : nr.Bytes == 1 ? cur[0] : (nr.Max > short.MaxValue ? (int)BitConverter.ToUInt16(cur, 0) : BitConverter.ToInt16(cur, 0));
                if(fm.VerifiedCurrentBuild && nr.Addr==loadedPlayer.Tail-FmLayout.JadedBeforePerson)
                {
                    try
                    {
                        var snapshot=CurrentFieldSnapshot.Capture(fm.Read,fm.Base,loadedPlayer,nr.Addr,2);
                        var plan=snapshot.Plan(BitConverter.GetBytes((short)v),loadedPlayer.DisplayName+"：疲劳度 "+nr.Old+" → "+v);
                        if(c==nr.Old && BitConverter.ToInt16(snapshot.Before,0)==nr.Old && CommitCurrent("jaded",plan,()=>snapshot.Reader.Matches(fm.Read,plan)))written++;else skipped++;
                    }
                    catch{skipped++;}
                }
                else if (c == nr.Old && WriteLogged(nr.Addr, nr.Bytes == 1 ? new[] { (byte)v } : BitConverter.GetBytes((short)v), loadedPlayer.Name + ": " + nr.Name.Split('(')[0].Trim().ToLower() + " " + nr.Old + " -> " + v)) written++;
                else skipped++;
            }
            LoadPlayer(loadedPlayer);
            ShowOperationFeedback(WriteFeedbackKind(written,skipped),
                written + " 项球员数值已修改" + (skipped > 0 ? "，" + skipped + " 项因游戏数据变化或写入失败而跳过" : "") +
                "。" + (written > 0 ? "请在 FM 中推进一天后查看。" : ""));
        }

        static bool IsYes(object v)
        {
            var t = Convert.ToString(v ?? "").Trim().ToLowerInvariant();
            return t == "是" || t == "yes" || t == "y" || t == "1" || t == "true" || t == "da" || t == "ja";
        }

        // ---------- clubs ----------

        double Rate()
        {
            double rate;return Cur()=="£"?1:CurrencySetting.TryRate(txtRate.Text,out rate)?rate:1;
        }

        // ---------- currency ----------
        // FM stores money in pounds and displays it in the currency chosen in its preferences. The symbol is cosmetic,
        // the rate converts. Labels registered with CurLabel use "{C}" for the symbol.

        string Cur() { var s = txtCur.Text.Trim(); return s.Length == 0 ? "£" : s; }

        Label CurLabel(Label l, string template) { template = Zh.Z(template); curLabels.Add(new KeyValuePair<Label, string>(l, template)); l.Text = template.Replace("{C}", Cur()); return l; }

        void RefreshCurrencyLabels()
        {
            foreach (var kv in curLabels) kv.Key.Text = kv.Value.Replace("{C}", Cur());
            lblMoney.Text = Cur() == "£" ? "金额按 £ 显示（存档原值）"
                : "金额显示为 " + Cur() + "   （1 £ = " + Rate().ToString("0.####", CultureInfo.InvariantCulture) + " " + Cur() + "）";
        }

        // Re-shows loaded club and player in the current currency (discards unsaved edits in those views)
        void RefreshCurrency()
        {
            RefreshCurrencyLabels();
            if (fm == null) return;
            if (loadedClub != null) LoadClub(loadedClub);
            if (loadedPlayer != null) LoadPlayer(loadedPlayer);
            if (loadedStaff != null) LoadStaff(loadedStaff);
            if (loadedPrize != null) LoadPrize(loadedPrize);
        }

        string SettingsPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt"); } }

        void LoadSettings()
        {
            try
            {
                var setting=CurrencySetting.Read(File.ReadAllLines(SettingsPath),new CurrencySetting("$",1.3476));
                txtCur.Text=setting.Symbol;txtRate.Text=setting.Rate.ToString("R",CultureInfo.InvariantCulture);
            }
            catch { }
        }

        void SaveSettings()
        {
            try {new CurrencySetting(Cur(),Rate()).Save(SettingsPath);}
            catch(Exception ex) {LogLine("显示货币设置无法保存："+ex.Message);}
        }

        static ulong[] FinFields { get { return new[] { FmLayout.FinBalance, FmLayout.FinTransfer, FmLayout.FinTransferNext, FmLayout.FinWageBudget, FmLayout.FinWageSpend }; } }
        static readonly string[] ClubMoneyNames = { "俱乐部结余", "剩余转会预算", "下赛季转会预算", "周薪预算" };

        TextBox[] ClubMoneyInputs()
        {
            return new[] { txtBalance, txtTransfer, txtTransferNext, txtWage };
        }

        static string ClubMoneyText(int? amount, double rate)
        {
            return amount.HasValue ? Money(amount.Value * rate) : "暂不可读";
        }

        static string FinanceReadOnlyReason()
        {
            string reason;
            if (Safety.Off.TryGetValue("core", out reason)) return "全局安全检查未通过：" + reason;
            if (Safety.Off.TryGetValue("finance", out reason)) return "财务只读，全局财务检查未通过：" + reason;
            return null;
        }

        void ShowClubCatalogEntry(Club club,string reason)
        {
            loadedClub=club;
            loadedFin=null;
            loadedTickets=null;
            loadedSeasonTickets=-1;
            loadedTicketPrice=-1;
            loadedStadium=0;
            loadedReputation=-1;
            loadedClubName=loadedClubShort=null;
            foreach(var box in new[]{txtBalance,txtTransfer,txtTransferNext,txtWage,txtTicketPrice,txtClubName,txtClubShort})
            {
                box.Clear();
                box.ReadOnly=true;
            }
            txtTicketPrice.Enabled=false;
            txtClubName.Enabled=txtClubShort.Enabled=club!=null;
            txtClubName.Text=club==null?"":club.Name;
            txtClubShort.Text=club==null?"":club.ShortName??"";
            lblWageYear.Text=lblWageSpend.Text=lblStoredPounds.Text="";
            foreach(var value in new[]{numReputation,numCapacity,numUsedCap,numSeasonTickets})
            {
                value.Enabled=false;
                value.Value=0;
            }
            BindFacilityEditors(null,false,null);
            btnApplyClub.Enabled=btnFreezeClub.Enabled=btnClubSponsors.Enabled=false;
            btnReloadClub.Enabled=club!=null;
            lblClub.Text=club==null?"选择俱乐部后查看和编辑资料":club.Name+"  [UID "+club.Uid+"]\n"+reason;
        }

        void LoadClub(Club c)
        {
            if (filteringClubs) return;
            if (c == null || fm == null || !btnScan.Enabled)
            {
                ShowClubCatalogEntry(c, "请先完成挂接并扫描。");
                return;
            }
            if(!c.HasFullFinance)
            {
                ShowClubCatalogEntry(c,c.CompactFinance?"已读取俱乐部。当前使用简化财务，俱乐部管理只读。":
                    "已读取俱乐部。完整财务尚未确认，俱乐部管理只读。");
                ShowOperationFeedback(FeedbackKind.ReadOnly,lblClub.Text);
                return;
            }
            if(!ClubCatalogReader.MatchesFullFinance(fm,c))
            {
                ShowClubCatalogEntry(c,"俱乐部或财务资料已变化。请重新挂接读取。");
                return;
            }
            var vals = FinFields.Select(field => fm.ReadI32(c.Finance + field)).ToArray();
            if (vals.All(value => !value.HasValue))
            {
                ShowClubCatalogEntry(c, "财务数值暂不可读，可重新加载此俱乐部。");
                ShowOperationFeedback(FeedbackKind.ReadOnly,lblClub.Text);
                return;
            }
            loadedClub = c; loadedFin = vals;
            loadedReputation = ReadReputation(c);
            numReputation.Enabled = loadedReputation >= 0;
            numReputation.Value = Math.Max(0, Math.Min(10000, loadedReputation));
            LoadFacilities(c);
            loadedStadium = ReadStadium(c);
            numCapacity.Enabled = numUsedCap.Enabled = loadedStadium != 0;
            if (loadedStadium != 0)
            {
                loadedCapacity = fm.ReadI32(loadedStadium + FmLayout.StadiumCapacity) ?? 0; loadedSeats = fm.ReadI32(loadedStadium + FmLayout.StadiumSeats) ?? 0;
                loadedUsed = fm.ReadI32(loadedStadium + FmLayout.StadiumUsed) ?? 0;
                numCapacity.Value = Math.Max(0, Math.Min(250000, loadedCapacity)); numUsedCap.Value = Math.Max(0, Math.Min(250000, loadedUsed));
            }
            loadedClubName = Safety.On("clubname") ? fm.ReadLenString(fm.ReadPtr(c.Addr + FmLayout.ClubName)) : null;
            loadedClubShort = Safety.On("clubname") ? fm.ReadLenString(fm.ReadPtr(c.Addr + FmLayout.ClubShortName)) : null;
            txtClubName.Text = loadedClubName ?? ""; txtClubShort.Text = loadedClubShort ?? "";
            txtClubName.ReadOnly=txtClubShort.ReadOnly=false;
            txtClubName.Enabled = loadedClubName != null; txtClubShort.Enabled = loadedClubShort != null;
            double r = Rate();
            loadedSeasonTickets = -1; loadedTicketPrice = -1;loadedTickets=null;
            if(Safety.On("tickets") && ClubFinanceCalibration.CheckedTickets(fm))
            {
                try{loadedTickets=CurrentTicketSnapshot.Capture(fm,c);loadedSeasonTickets=loadedTickets.SeasonTickets;loadedTicketPrice=loadedTickets.Price;}catch{}
            }
            else if (Safety.On("tickets") && c.Data > 0x10000)
            {
                loadedSeasonTickets = fm.ReadI32(c.Data + FmLayout.DataSeasonTickets) ?? -1;
                var tp = fm.Read(c.Finance + FmLayout.FinTicketPrice, 4); loadedTicketPrice = tp == null ? -1 : BitConverter.ToSingle(tp, 0);
            }
            numSeasonTickets.Enabled = loadedSeasonTickets >= 0; numSeasonTickets.Value = Math.Max(0, Math.Min(300000, loadedSeasonTickets));
            txtTicketPrice.Enabled = loadedTickets!=null || loadedTicketPrice >= 0; txtTicketPrice.Text = loadedTicketPrice >= 0 ? (loadedTicketPrice * r).ToString("0.00", CultureInfo.InvariantCulture) : loadedTickets!=null ? "未设置" : "";
            txtTicketPrice.ReadOnly=!txtTicketPrice.Enabled;
            string financeRestriction = FinanceReadOnlyReason();
            var inputs = ClubMoneyInputs();
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index].ReadOnly = financeRestriction != null || !vals[index].HasValue;
                inputs[index].Text = ClubMoneyText(vals[index], r);
            }
            lblClub.Text = c.Name+"  [UID "+c.Uid+"]" + (financeRestriction == null ? "" : "\n" + financeRestriction);
            if (financeRestriction != null) ShowOperationFeedback(FeedbackKind.ReadOnly,lblClub.Text);
            btnReloadClub.Enabled=true;
            btnApplyClub.Enabled=Safety.On("core");
            btnFreezeClub.Enabled=Safety.On("finance");
            btnClubSponsors.Enabled=Safety.On("sponsor");
            lblWageYear.Text = vals[3].HasValue ? "= " + Money(vals[3].Value * r * 52) + "／年" : "暂不可读";
            lblWageSpend.Text = vals[4].HasValue ? ClubMoneyText(vals[4], r) + "／周（" + Money(vals[4].Value * r * 52) + "／年）" : "暂不可读";
            lblStoredPounds.Text = "原始英镑金额：余额 £" + ClubMoneyText(vals[0], 1) + "，转会预算 £" + ClubMoneyText(vals[1], 1) +
                                   "，下赛季预算 £" + ClubMoneyText(vals[2], 1) + "，周薪预算 £" + ClubMoneyText(vals[3], 1) + "/wk";
        }

        static string Money(double v) { return Math.Round(v).ToString("N0", CultureInfo.InvariantCulture); }

        static bool ParseMoney(string s, out double v) {return CurrencySetting.TryMoney(s,out v);}

        delegate bool ClubMoneyWriter(ulong address, byte[] value, out int error);

        sealed class ClubMoneyEdit
        {
            public ulong Address;
            public string Name, Input, Result;
            public int? Before;
            public int After;
            public Freeze Floor;
            public int? FloorValue;
            public bool Written;
            public bool LowersFloor { get { return FloorValue.HasValue && After < FloorValue.Value; } }
        }

        List<ClubMoneyEdit> ClubMoneyDrafts(Club club, int?[] snapshot, double rate)
        {
            var edits = new List<ClubMoneyEdit>();
            if (club == null || snapshot == null) return edits;
            var inputs = ClubMoneyInputs();
            var fields = FinFields;
            for (int i = 0; i < inputs.Length; i++)
            {
                string input = inputs[i].Text;
                if (input == ClubMoneyText(snapshot[i], rate)) continue;
                edits.Add(new ClubMoneyEdit { Address = club.Finance + fields[i], Name = ClubMoneyNames[i],
                    Input = input, Before = snapshot[i] });
            }
            return edits;
        }

        string ClubDraftProblem(FmProcess attached, Club club, int?[] snapshot, ulong? save)
        {
            if (club == null || loadedClub != club || lstClubs.SelectedItem != club || loadedFin != snapshot)
                return "俱乐部选择或财务草稿已变化，请重新核对。";
            if (attached == null || fm != attached || !btnScan.Enabled ||
                (save.HasValue && SaveLoadCount(attached) != save))
                return "连接或存档已变化，请重新挂接并扫描。";
            if (!club.HasFullFinance)
                return club.CompactFinance ? "财务只读，当前俱乐部使用简化财务。" : "财务只读，完整财务尚未确认。";
            if (!ClubCatalogReader.MatchesFullFinance(attached, club))
                return "俱乐部或财务资料已变化，请重新挂接读取。";
            if (!Safety.On("core")) return FinanceReadOnlyReason();
            return snapshot == null ? "财务只读，原值读取失败，请重新加载。" : null;
        }

        static string CheckClubMoneyOriginal(ClubMoneyEdit edit, Func<ulong, int?> read)
        {
            var current = read(edit.Address);
            if (!current.HasValue) return "旧值读取失败，请重新加载。";
            return current == edit.Before ? null : "旧值已变更，当前为 £" + Money(current.Value) + "，请重新加载。";
        }

        static string PrepareClubMoney(ClubMoneyEdit edit, double rate, Func<ulong, int?> read)
        {
            if (!edit.Before.HasValue) return "财务只读，旧值读取失败。";
            double entered;
            if (!ParseMoney(edit.Input, out entered)) return "金额格式无效。";
            double pounds = entered / rate;
            if (double.IsNaN(pounds) || double.IsInfinity(pounds)) return "折算后的英镑金额不是有限数值。";
            if (pounds < int.MinValue || pounds > int.MaxValue)
                return "折算后的英镑金额超出 -2,147,483,648 至 2,147,483,647 的范围。";
            edit.After = (int)Math.Round(pounds);
            return CheckClubMoneyOriginal(edit, read);
        }

        Freeze ClubMoneyFloor(ulong address)
        {
            return freezes.FirstOrDefault(f => f.Addr == address && f.Signed && f.Value.Length == 4 && GuardsOk(f));
        }

        string CheckClubMoneyBeforeWrite(ClubMoneyEdit edit, Func<ulong, int?> read)
        {
            var floor = ClubMoneyFloor(edit.Address);
            if (floor != edit.Floor || (floor != null && BitConverter.ToInt32(floor.Value, 0) != edit.FloorValue))
                return "冻结下限已变化，请重新核对并提交。";
            return CheckClubMoneyOriginal(edit, read);
        }

        const string ClubMoneySameValue = "折算后同值，已跳过。";

        static bool IsClubMoneyReadOnly(string reason)
        {
            return reason != null && (reason.StartsWith("财务只读",StringComparison.Ordinal) ||
                reason.StartsWith("全局安全检查未通过",StringComparison.Ordinal));
        }

        static FeedbackKind ClubMoneyFeedbackKind(List<ClubMoneyEdit> edits,int written,int skipped,string blockedBy=null)
        {
            var unsuccessful=edits.Where(edit=>!edit.Written && edit.Result!=ClubMoneySameValue).ToArray();
            if (written==0 && (IsClubMoneyReadOnly(blockedBy) ||
                skipped==0 && unsuccessful.Length>0 && unsuccessful.All(edit=>IsClubMoneyReadOnly(edit.Result))))
                return FeedbackKind.ReadOnly;
            return WriteFeedbackKind(written,skipped+unsuccessful.Length+(blockedBy==null?0:1));
        }

        void ReportClubMoney(Club club, List<ClubMoneyEdit> edits, string pendingResult = null, bool canceled = false)
        {
            string prefix = club == null ? "俱乐部财务" : club.Name;
            foreach (var edit in edits)
            {
                if (edit.Result == null) edit.Result = pendingResult;
                if (!edit.Written) LogLine(prefix + " · " + edit.Name + " " + edit.Result);
            }
            if (edits.Count==0 && pendingResult!=null) LogLine(prefix + " · " + pendingResult);
            string details=edits.Count==0?pendingResult:
                string.Join(Environment.NewLine,edits.Select(edit=>edit.Name+"："+edit.Result));
            ShowOperationFeedback(canceled?FeedbackKind.Information:
                ClubMoneyFeedbackKind(edits,edits.Count(edit=>edit.Written),0,pendingResult),
                prefix+Environment.NewLine+details);
        }

        bool ApplyClubMoney(Club club, List<ClubMoneyEdit> edits, double rate, Func<string> checkDraft,
            Func<ulong, int?> read, ClubMoneyWriter write, Func<string, bool> confirm, out int written)
        {
            written = 0;
            if (edits.Count == 0) return true;
            Func<string> check = () => checkDraft() ?? FinanceReadOnlyReason();
            string problem = check();
            if (problem != null) { ReportClubMoney(club, edits, problem); return false; }

            bool invalid = false;
            foreach (var edit in edits)
            {
                edit.Result = PrepareClubMoney(edit, rate, read);
                if (edit.Result != null) invalid = true;
                else if (edit.After == edit.Before.Value) edit.Result = ClubMoneySameValue;
            }
            if (invalid)
            {
                ReportClubMoney(club, edits, "本次有金额未通过校验，已跳过。");
                return false;
            }

            var pending = edits.Where(edit => edit.Result == null).ToArray();
            foreach (var edit in pending)
            {
                edit.Floor = ClubMoneyFloor(edit.Address);
                edit.FloorValue = edit.Floor == null ? (int?)null : BitConverter.ToInt32(edit.Floor.Value, 0);
            }
            var reductions = pending.Where(edit => edit.LowersFloor).ToArray();
            if (reductions.Length > 0)
            {
                string detail = string.Join("\n", reductions.Select(edit => edit.Name + " 当前冻结下限 £" +
                    Money(edit.FloorValue.Value) + " → £" + Money(edit.After)));
                if (!confirm(detail + "\n\n是否写入金额并同步下调这些冻结下限？"))
                {
                    ReportClubMoney(club, edits, "已取消下调冻结下限，本次未写入。", canceled:true);
                    return false;
                }
            }

            // The game and the roster refresh can advance while the confirmation dialog is open.
            problem = check();
            if (problem != null) { ReportClubMoney(club, edits, problem); return false; }
            foreach (var edit in pending) edit.Result = CheckClubMoneyBeforeWrite(edit, read);
            if (pending.Any(edit => edit.Result != null))
            {
                ReportClubMoney(club, edits, "本次财务状态已变化，已跳过。");
                return false;
            }

            bool floorsChanged = false;
            foreach (var edit in pending)
            {
                edit.Result = check() ?? CheckClubMoneyBeforeWrite(edit, read);
                if (edit.Result != null) continue;
                var data = BitConverter.GetBytes(edit.After);
                int error;
                if (!write(edit.Address, data, out error))
                {
                    edit.Result = "写入失败（Windows 错误码 " + error + "）。";
                    continue;
                }
                problem = check();
                if (problem != null)
                {
                    edit.Result = "写入后回读未通过。" + problem;
                    continue;
                }
                var actual = read(edit.Address);
                if (actual != edit.After)
                {
                    edit.Result = actual.HasValue ? "回读不一致，预期 £" + Money(edit.After) +
                        "，实际 £" + Money(actual.Value) + "。" : "写入后回读失败。";
                    continue;
                }
                edit.Written = true;
                edit.Result = "已写入 £" + Money(edit.After) + "，回读通过。";
                if (edit.LowersFloor)
                {
                    edit.Floor.Value = (byte[])data.Clone();
                    edit.Floor.What = club.Name + " · " + edit.Name + " 最低 £" + Money(edit.After);
                    edit.Result += " 冻结下限已同步。";
                    floorsChanged = true;
                }
                RecordLoggedChange(edit.Address, BitConverter.GetBytes(edit.Before.Value), data, club.Name + " · " +
                    edit.Name + " £" + Money(edit.Before.Value) + " → £" + Money(edit.After) + "。回读通过。" +
                    (edit.LowersFloor ? " 冻结下限已同步。" : ""));
                written++;
            }
            if (floorsChanged) RefreshFreezeList();
            ReportClubMoney(club, edits);
            return true;
        }

        void ApplyClub()
        {
            ApplyClub(prompt => MessageBox.Show(this, prompt, "调整资金冻结下限",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes);
        }

        void ApplyClub(Func<string, bool> confirm, Func<ulong, int?> financeRead = null, ClubMoneyWriter financeWrite = null)
        {
            var attached = fm;
            var club = loadedClub;
            var snapshot = loadedFin;
            double r = Rate();
            var moneyEdits = ClubMoneyDrafts(club, snapshot, r);
            var save = scannedSaveLoads;
            Func<string> checkDraft = () => ClubDraftProblem(attached, club, snapshot, save);
            string draftProblem = checkDraft();
            if (draftProblem != null)
            {
                ShowClubCatalogEntry(lstClubs.SelectedItem as Club, draftProblem);
                ReportClubMoney(club, moneyEdits, draftProblem);
                return;
            }
            int written, skipped = 0;
            if (!ApplyClubMoney(club, moneyEdits, r, checkDraft, financeRead ?? new Func<ulong, int?>(attached.ReadI32),
                financeWrite ?? new ClubMoneyWriter(attached.Write), confirm, out written)) return;
            string moneyStatus = moneyEdits.Count == 0 ? "" : lblStatus.Text;
            draftProblem = checkDraft();
            if (draftProblem != null)
            {
                ShowClubCatalogEntry(lstClubs.SelectedItem as Club, draftProblem);
                ShowOperationFeedback(ClubMoneyFeedbackKind(moneyEdits,written,0,draftProblem),
                    (moneyStatus.Length==0?"":moneyStatus+Environment.NewLine)+draftProblem);
                return;
            }
            int rep = (int)numReputation.Value;
            if (numReputation.Enabled && rep != loadedReputation)
            {
                if (ReadReputation(loadedClub) != loadedReputation) skipped++;
                else if (WriteLogged(loadedClub.Team + FmLayout.TeamReputation, BitConverter.GetBytes((ushort)rep), loadedClub.Name + "：声望 " + loadedReputation + " -> " + rep)) written++;
            }
            string facilityFailure=null;
            var facilityRequests=new Dictionary<int,int>();
            for(int k=0;k<numFacility.Length;k++)if(numFacility[k].Enabled && (int)numFacility[k].Value!=loadedFacility[k])facilityRequests[k]=(int)numFacility[k].Value;
            if(facilityRequests.Count>0)
            {
                string failure;if(CommitFacilities(facilityRequests,out failure))written+=facilityRequests.Count;
                else
                {
                    skipped+=facilityRequests.Count;LogLine("设施修改未完成："+failure);MessageBox.Show(failure,"设施修改未完成");
                    facilityFailure=failure;
                    if(failure!=null && failure.Contains("重载"))
                    {
                        LoadClub(loadedClub);
                        ShowOperationFeedback(ClubMoneyFeedbackKind(moneyEdits,written,skipped),
                            (moneyStatus.Length==0?"":moneyStatus+Environment.NewLine)+"设施修改未完整恢复："+failure);
                        return;
                    }
                }
            }
            if (loadedStadium != 0)
            {
                int cap = (int)numCapacity.Value, used = Math.Min((int)numUsedCap.Value, cap);
                if (cap != loadedCapacity || used != loadedUsed)
                {
                    if (fm.ReadI32(loadedStadium + FmLayout.StadiumCapacity) != loadedCapacity || fm.ReadI32(loadedStadium + FmLayout.StadiumUsed) != loadedUsed) skipped++;
                    else
                    {
                        string sw = loadedClub.Name + "：球场容量 " + loadedCapacity + " -> " + cap + "，使用座位 " + loadedUsed + " -> " + used;
                        curGroup = ++lastGroup;
                        try
                        {
                            if (cap != loadedCapacity && WriteLogged(loadedStadium + FmLayout.StadiumCapacity, BitConverter.GetBytes(cap), sw)) written++;
                            // an all-seater stays all-seater
                            if (cap != loadedCapacity && loadedSeats == loadedCapacity) WriteLogged(loadedStadium + FmLayout.StadiumSeats, BitConverter.GetBytes(cap), sw + "（座位）");
                            if (used != loadedUsed && WriteLogged(loadedStadium + FmLayout.StadiumUsed, BitConverter.GetBytes(used), sw + "（已使用）")) written++;
                        }
                        finally { curGroup = 0; }
                    }
                }
            }
            if(loadedTickets!=null)
            {
                try
                {
                    int? tickets=numSeasonTickets.Enabled && (int)numSeasonTickets.Value!=loadedSeasonTickets?(int?)numSeasonTickets.Value:null;
                    float? amount=null;double entered;
                    string original=loadedTicketPrice<0?"未设置":(loadedTicketPrice*r).ToString("0.00",CultureInfo.InvariantCulture);
                    if(txtTicketPrice.Enabled && txtTicketPrice.Text!=original)
                    {
                        if(!ParseMoney(txtTicketPrice.Text,out entered) || entered/r<0 || entered/r>2000)throw new ArgumentException("票价需为有效金额，折合英镑在 0–2000 之间。");
                        amount=(float)(entered/r);
                    }
                    var plan=loadedTickets.Plan(tickets,amount);var ticketSnapshot=loadedTickets;
                    if(plan.Count>0){if(CommitCurrent("tickets",plan,()=>ticketSnapshot.Reader.Matches(fm.Read,plan)))written+=plan.Count;else skipped+=plan.Count;}
                }
                catch(Exception ex){LogLine("票价／季票未修改："+ex.Message);skipped++;}
            }
            else if (numSeasonTickets.Enabled && (int)numSeasonTickets.Value != loadedSeasonTickets)
            {
                int v = (int)numSeasonTickets.Value;
                if (fm.ReadI32(loadedClub.Data + FmLayout.DataSeasonTickets) != loadedSeasonTickets) skipped++;
                else if (WriteLogged(loadedClub.Data + FmLayout.DataSeasonTickets, BitConverter.GetBytes(v), loadedClub.Name + "：季票持有者 " +
                    loadedSeasonTickets.ToString("N0", CultureInfo.InvariantCulture) + " -> " + v.ToString("N0", CultureInfo.InvariantCulture))) written++;
            }
            double price;
            if (loadedTickets==null && !fm.VerifiedCurrentBuild && txtTicketPrice.Enabled && txtTicketPrice.Text != (loadedTicketPrice * r).ToString("0.00", CultureInfo.InvariantCulture) && ParseMoney(txtTicketPrice.Text, out price))
            {
                float pounds = (float)Math.Max(0, Math.Min(2000, price / r));
                var cur = fm.Read(loadedClub.Finance + FmLayout.FinTicketPrice, 4);
                if (cur == null || BitConverter.ToSingle(cur, 0) != loadedTicketPrice) skipped++;
                else if (Math.Abs(pounds - loadedTicketPrice) >= 0.005 && WriteLogged(loadedClub.Finance + FmLayout.FinTicketPrice, BitConverter.GetBytes(pounds), loadedClub.Name + "：平均票价 £" +
                    loadedTicketPrice.ToString("0.00", CultureInfo.InvariantCulture) + " -> £" + pounds.ToString("0.00", CultureInfo.InvariantCulture))) written++;
            }
            int renamed = WriteName(loadedClub.Addr + FmLayout.ClubName, loadedClubName, txtClubName.Text, "name", ref skipped);
            written += renamed;
            written += WriteName(loadedClub.Addr + FmLayout.ClubShortName, loadedClubShort, txtClubShort.Text, "short name", ref skipped);
            if (renamed != 0) FilterClubs();
            LoadClub(lstClubs.SelectedItem as Club);
            string summary=written+" 项俱乐部数值已修改"+
                (skipped>0?"，另有 "+skipped+" 项跳过":"")+"。"+
                (written>0?"请在 FM 中推进一天后查看。":"");
            ShowOperationFeedback(ClubMoneyFeedbackKind(moneyEdits,written,skipped),
                summary+(moneyStatus.Length==0?"":Environment.NewLine+moneyStatus)+
                (facilityFailure==null?"":Environment.NewLine+"设施修改未完成："+facilityFailure));
        }

        // ---------- sponsorship deals ----------
        // The club's deals (pointers to 0x18-byte records), empty when the list does not look right
        internal static List<ulong> SponsorDeals(FmProcess fm, ulong club)
        {
            var res = new List<ulong>();
            ulong v = fm.ReadPtr(club + FmLayout.ClubSponsors); if (v < 0x10000) return res;
            ulong b = fm.ReadPtr(v), e = fm.ReadPtr(v + 8);
            if (b < 0x10000 || e < b || (e - b) % 8 != 0 || e - b > 800) return res;
            var raw = fm.Read(b, (int)(e - b)); if (raw == null) return res;
            for (int k = 0; k < raw.Length; k += 8) { ulong d = BitConverter.ToUInt64(raw, k); if (d > 0x10000) res.Add(d); }
            return res;
        }

        static string DealDate(byte[] d, int off) { int day = BitConverter.ToUInt16(d, off) & 0x1FF, y = BitConverter.ToUInt16(d, off + 2); try { return new DateTime(y, 1, 1).AddDays(day - 1).ToString("yyyy-MM-dd"); } catch { return "?"; } }

        void SponsorsDialog()
        {
            var c = loadedClub; if (c == null || fm == null) return;
            if(!btnScan.Enabled || !ClubCatalogReader.MatchesFullFinance(fm,c))return;
            if (!Safety.On("sponsor")) { MessageBox.Show("当前游戏版本的赞助合同为只读。", "赞助合同"); return; }
            // current deals only: expired ones keep their total but pay nothing per season
            var deals = SponsorDeals(fm, c.Addr).Select(a => new KeyValuePair<ulong, byte[]>(a, fm.Read(a, 0x18))).Where(x => x.Value != null && BitConverter.ToInt32(x.Value, 12) > 0).ToList();
            if (deals.Count == 0) { MessageBox.Show(c.Name + " 当前没有赞助合同。", "赞助合同"); return; }
            double r = Rate();
            var f = new Form { Width = 760, Height = 360, MaximizeBox = false };
            StyleWorkspaceDialog(f,"赞助合同 · "+c.Name);
            var grid = NewGrid();grid.TabIndex=0;
            foreach (var h in new[] { "类型", "到期日", "年数", "每赛季（" + Cur() + ")", "总额（" + Cur() + ")" }) grid.Columns.Add(h, h);
            for (int k = 0; k < 3; k++) grid.Columns[k].ReadOnly = true;
            foreach (var d in deals)
            {
                string type; int code = d.Value[FmLayout.DealType];
                if (!FmLayout.SponsorTypes.TryGetValue(code, out type)) type = "赞助（类型 " + code + ")"; else type = Zh.Z(type);
                int years = Math.Max(1, BitConverter.ToUInt16(d.Value, 6) - BitConverter.ToUInt16(d.Value, 2));
                grid.Rows.Add(type, DealDate(d.Value, 4), years, Money(BitConverter.ToInt32(d.Value, 12) * r), Money(BitConverter.ToInt32(d.Value, 8) * r));
            }
            grid.CellEndEdit += (o, e) =>
            {
                // a new value per season updates the total over the deal's years
                if (e.ColumnIndex != 3) return; double v;
                if (ParseMoney(Convert.ToString(grid.Rows[e.RowIndex].Cells[3].Value), out v)) grid.Rows[e.RowIndex].Cells[4].Value = Money(v * (int)grid.Rows[e.RowIndex].Cells[2].Value);
            };
            var note = new Label { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4), ForeColor = UiMuted,
                Text = "新金额会立即显示在「财务 > 赞助及其他」中。FM 每年支付一次赞助费，修改会在下一次付款时生效。" };
            var ok = new Button { Text = "应用" };
            var actions=DialogActions(f,DialogDismiss("CancelSponsors","取消"),ok);
            actions.TabIndex=1;
            f.Controls.Add(grid); f.Controls.Add(note); f.Controls.Add(actions);
            ok.Click += delegate
            {
                if(!Safety.On("sponsor") || !ClubCatalogReader.MatchesFullFinance(fm,c))
                {
                    lblStatus.Text="俱乐部赞助资料已变化，请重新加载。";
                    f.Close();
                    return;
                }
                grid.EndEdit(); int written = 0, skipped = 0;
                for (int k = 0; k < deals.Count; k++)
                {
                    var d = deals[k]; int oldSea = BitConverter.ToInt32(d.Value, 12), oldTot = BitConverter.ToInt32(d.Value, 8);
                    double sea, tot;
                    if (!ParseMoney(Convert.ToString(grid.Rows[k].Cells[3].Value), out sea) || !ParseMoney(Convert.ToString(grid.Rows[k].Cells[4].Value), out tot)) continue;
                    int newSea = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(sea / r))), newTot = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(tot / r)));
                    if (Convert.ToString(grid.Rows[k].Cells[3].Value) == Money(oldSea * r)) newSea = oldSea;
                    if (Convert.ToString(grid.Rows[k].Cells[4].Value) == Money(oldTot * r)) newTot = oldTot;
                    if (newSea == oldSea && newTot == oldTot) continue;
                    var cur = fm.Read(d.Key, 0x18);
                    if (cur == null || !cur.SequenceEqual(d.Value)) { skipped++; continue; }
                    string what = c.Name + ": " + grid.Rows[k].Cells[0].Value + " 赞助费 £" + oldSea.ToString("N0", CultureInfo.InvariantCulture) + " -> £" + newSea.ToString("N0", CultureInfo.InvariantCulture) +
                                  "／赛季，总额 £" + oldTot.ToString("N0", CultureInfo.InvariantCulture) + " -> £" + newTot.ToString("N0", CultureInfo.InvariantCulture);
                    var nb = new byte[8]; BitConverter.GetBytes(newTot).CopyTo(nb, 0); BitConverter.GetBytes(newSea).CopyTo(nb, 4);
                    if (WriteLogged(d.Key + FmLayout.DealTotal, nb, what)) written++;
                }
                lblStatus.Text = written + " 份赞助合同已修改" + (skipped > 0 ? ", " + skipped + " 项因游戏数据变化而跳过" : "") + "。FM「财务 > 赞助及其他」中立即显示。";
                f.Close();
            };
            Zh.Tree(f);
            f.ShowDialog(this);
        }

        // The stadium behind the club's first team, or 0
        ulong ReadStadium(Club c)
        {
            if (!Safety.On("stadium") || c.Team < 0x10000 || fm.ReadPtr(c.Team + FmLayout.TeamClub2) != c.Addr) return 0;
            ulong sd = fm.ReadPtr(c.Team + FmLayout.TeamStadium);
            return sd > 0x10000 && fm.ReadPtr(sd) == fm.Base + FmProcess.StadiumVtRva ? sd : 0;
        }

        // Rewrites a club name in place (int32 length + UTF-8). Only names that fit in the old one: nothing is allocated.
        int WriteName(ulong field, string old, string want, string what, ref int skipped)
        {
            want = (want ?? "").Trim();
            if (old == null || want.Length == 0 || want == old) return 0;
            var nb = Encoding.UTF8.GetBytes(want); var ob = Encoding.UTF8.GetBytes(old);
            if (nb.Length > ob.Length) { MessageBox.Show("新的 " + what + " 超过原名称长度（" + ob.Length + " 字节），请使用更短的名称。", "俱乐部"); return 0; }
            ulong str = fm.ReadPtr(field);
            if (fm.ReadLenString(str) != old) { skipped++; return 0; }
            var buf = new byte[4 + ob.Length];
            BitConverter.GetBytes(nb.Length).CopyTo(buf, 0); nb.CopyTo(buf, 4);
            if (!WriteLogged(str, buf, loadedClub.Name + ": " + what + " " + old + " -> " + want)) return 0;
            if (what == "name") loadedClub.Name = want;
            return 1;
        }

        // Club reputation 0-10000 from the first team, -1 if the club has no first team we can verify
        int ReadReputation(Club c)
        {
            if (!Safety.On("reputation") || c.Team < 0x10000 || fm.ReadPtr(c.Team + FmLayout.TeamClub2) != c.Addr) return -1;
            var b = fm.Read(c.Team + FmLayout.TeamReputation, 2);
            if (b == null) return -1;
            int v = BitConverter.ToUInt16(b, 0);
            return v <= 10000 ? v : -1;
        }

        // ---------- staff ----------
        // Coaches, scouts, physios and other non-players. Attributes sit before the staff object (FmLayout.StaffAttrs);
        // personality bytes after it, at the same place as for players. Changes show in game after the next day.

        TabPage BuildStaffTab()
        {
            var page = new TabPage("职员");
            var split = splitStaff = new SplitContainer { Dock = DockStyle.Fill };
            var searchRow=SearchWithListSpacing(txtStaffSearch);
            var searchCaption=new Label {Text="搜职员：姓名或俱乐部（至少 2 个字符）\n输入 manager 可编辑你自己",
                Dock=DockStyle.Top,Height=46,Font=new Font(Font.FontFamily,8.5f),Padding=new Padding(0,2,0,0)};
            var leftGridInset=new Panel{Dock=DockStyle.Top,Height=0};
            var rightGridInset=new Panel{Dock=DockStyle.Top,Height=0};
            split.Panel1.Controls.Add(lstStaff);
            split.Panel1.Controls.Add(leftGridInset);
            split.Panel1.Controls.Add(searchRow);
            split.Panel1.Controls.Add(searchCaption);

            gridStaff.Columns.Add("attr", "属性");
            gridStaff.Columns.Add("value", "数值");
            gridStaff.Columns.Add("note", "备注");
            gridStaff.Columns[0].ReadOnly = true; gridStaff.Columns[2].ReadOnly = true;
            gridStaff.Columns[0].FillWeight = 160; gridStaff.Columns[2].FillWeight = 70;

            var buttons = WrappingBar("StaffActions");
            buttons.Dock=DockStyle.Bottom;
            buttons.Padding=new Padding(4);
            var btnApply = new Button { Text = "应用修改", Width = 120 };
            StyleButton(btnApply,WorkspaceButtonRole.Primary);
            var btnReload = new Button { Text = "重新加载", Width = 80 };
            var btnMax = new Button { Text = "所有属性拉到 20", Width = 140 };
            buttons.Controls.Add(btnApply); buttons.Controls.Add(btnReload); buttons.Controls.Add(btnMax);
            btnStaffMove = new Button { Name = "StaffTransfer", Text = "转会／职务…", Width = 120, Enabled = false };
            buttons.Controls.Add(btnStaffMove); buttons.Controls.Add(btnStaffAge);
            btnStaffAge.Click += delegate { if (loadedStaff != null) OpenBirthdayDialog(loadedStaff.Pers, loadedStaff.Name); };
            btnStaffMove.Click += delegate { MoveStaffDialog(); };

            var staffToolbar=BuildStaffAttributeToolbar();
            split.Panel2.Controls.Add(gridStaff);
            split.Panel2.Controls.Add(rightGridInset);
            split.Panel2.Controls.Add(staffToolbar);
            split.Panel2.Controls.Add(buttons);
            WrapDockedLabel(lblStaff);
            split.Panel2.Controls.Add(lblStaff);
            EventHandler alignStaffTables=delegate
            {
                int difference=searchCaption.Height+searchRow.Height-lblStaff.Height-staffToolbar.Height;
                leftGridInset.Height=Math.Max(0,-difference);
                rightGridInset.Height=Math.Max(0,difference);
                split.Panel1.Padding=new Padding(0,0,0,buttons.Height);
            };
            foreach(var row in new Control[]{searchCaption,searchRow,lblStaff,staffToolbar,buttons})row.SizeChanged+=alignStaffTables;
            alignStaffTables(split,EventArgs.Empty);
            page.Controls.Add(split);

            txtStaffSearch.TextChanged += delegate { FilterStaff(); };
            lstStaff.SelectedIndexChanged += delegate { if (!filteringStaff) LoadStaff(lstStaff.SelectedItem as Staff); };
            gridStaff.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex == staffRoleRow && btnStaffMove.Enabled) MoveStaffDialog(true);
            };
            btnReload.Click += delegate { LoadStaff(loadedStaff); };
            btnApply.Click += delegate { ApplyStaff(); };
            btnMax.Click += delegate
            {
                if (loadedStaffBlock == null) return;
                for (int k = 0; k < FmLayout.StaffAttrs.Length && k < gridStaff.Rows.Count; k++) gridStaff.Rows[k].Cells[1].Value = 20;
            };
            return page;
        }

        void FilterStaff()
        {
            var q = txtStaffSearch.Text.Trim();
            var selected = lstStaff.SelectedItem as Staff;
            filteringStaff = true;
            lstStaff.BeginUpdate();
            try
            {
                lstStaff.Items.Clear();
                if (PlayerNames.CanSearch(q))
                    lstStaff.Items.AddRange(staff.Where(x => x.Matches(q)).Take(500).ToArray());
                if (selected != null && lstStaff.Items.Contains(selected)) lstStaff.SelectedItem = selected;
                else if (lstStaff.Items.Count == 1) lstStaff.SelectedIndex = 0;
            }
            finally { lstStaff.EndUpdate(); filteringStaff = false; }
            if (loadedStaff != null || lstStaff.SelectedItem != null) LoadStaff(lstStaff.SelectedItem as Staff);
        }

        static int StaffDisplay(byte raw, string scale) { return scale == "5" ? ToDisplay(raw) : raw; }

        bool StaffIdentityMatches(Staff person)
        {
            ulong type = fm.ReadPtr(person.Pers);
            return fm.ReadI32(person.Pers + FmLayout.Uid) == person.Uid &&
                (person.Manager ? type == fm.Base + FmProcess.ManagerVtRva :
                    type == fm.Base + FmProcess.StaffVtRva || type == fm.Base + FmProcess.PlayerCoachVtRva);
        }

        void LoadStaff(Staff st)
        {
            loadedStaff = null; loadedStaffBlock = null; loadedStaffContract = 0; staffRoleRow = -1;
            btnStaffMove.Enabled = false;
            if (st == null || fm == null)
            {
                gridStaff.Rows.Clear(); lblStaff.Text = ""; return;
            }
            if (!StaffIdentityMatches(st))
            {
                gridStaff.Rows.Clear(); lblStaff.Text = "职员身份已变化，请重新挂接并扫描。"; return;
            }
            var b = fm.Read(st.Addr - FmLayout.StaffBlockBefore, FmLayout.StaffBlockLen);
            var employment = StaffEmployment.Read(fm.Read, fm.Base, fm.ImageSize, st.Pers);
            bool attributesAvailable = FmLayout.ValidStaffBlock(b);
            if (!attributesAvailable && (employment == null || !employment.VerifiesClubAssignment(fm.ReadPtr, fm.Base)))
            { gridStaff.Rows.Clear(); lblStaff.Text = "职员资料已变化，请重新挂接并扫描。"; return; }
            loadedStaff = st; loadedStaffBlock = attributesAvailable ? b : null;
            try { st.Born = Birthdays.Decode(fm.ReadU32(st.Pers + FmLayout.BirthDate).Value); } catch { }
            lblStaff.Text = st.ToString() + "  " + DisplayAge(st.Born);
            gridStaff.Rows.Clear();
            if (attributesAvailable)
            {
                foreach (var a in FmLayout.StaffAttrs)
                {
                    var p = a.Split('|');
                    gridStaff.Rows.Add(Zh.Attr(p[0]), StaffDisplay(b[(int)(FmLayout.StaffBlockBefore - Convert.ToUInt64(p[1], 16))], p[2]), st.Manager ? ManagerNote(p[0]) : "已验证");
                }
            }
            else gridStaff.Rows[gridStaff.Rows.Add("职员属性", "资料不完整", "只读")].ReadOnly = true;
            loadedStaffPers = attributesAvailable && Safety.On("personality") && Safety.On("staff") ?
                fm.Read(st.Pers + FmLayout.PersonalityAfterPerson, 8) : null;
            if (loadedStaffPers != null)
                for (int k = 0; k < 8; k++)
                    gridStaff.Rows.Add(Zh.Attr(FmLayout.Personality[k].Split((char)124)[0]), loadedStaffPers[k], k == 0 ? "性格，已验证" : "性格");
            loadedStaffRep = null;
            if (attributesAvailable && Safety.On("staffrep"))
            {
                loadedStaffRep = new short[3];
                string[] rn = { "当前声望（0-10000）", "国内声望（0-10000）", "世界声望（0-10000）" };
                for (int k = 0; k < 3; k++)
                {
                    var v = fm.Read(st.Addr - FmLayout.StaffRepBefore[k], 2);
                    loadedStaffRep[k] = v == null ? (short)-1 : BitConverter.ToInt16(v, 0);
                    gridStaff.Rows.Add(rn[k], loadedStaffRep[k], k == 2 ? "决定星级，下一游戏日显示" : "下一游戏日显示");
                }
            }
            if (attributesAvailable && employment != null && employment.Full && Safety.On("staffcontract"))
            {
                var expiry = fm.ReadU32(employment.Address + FmLayout.ContractExpiry);
                if (expiry.HasValue && employment.WeeklyWage >= 0)
                {
                    loadedStaffContract = employment.Address; loadedStaffWage = employment.WeeklyWage; loadedStaffExpiry = expiry.Value;
                    loadedStaffContractRow = gridStaff.Rows.Count;
                    gridStaff.Rows.Add("工资（" + Cur() + "／年）  [£" + Money(loadedStaffWage) + "／周]",
                        Money(loadedStaffWage * Rate() * 52), "合同");
                    gridStaff.Rows.Add("合同到期日（年-月-日）", FromPacked(loadedStaffExpiry).ToString("yyyy-MM-dd"), "合同");
                }
            }
            if (employment != null && employment.Job > 1)
            {
                var role = StaffRole.Find(employment.Job);
                btnStaffMove.Enabled = !st.Manager && role != null && fm.ReadPtr(st.Pers) == fm.Base + FmProcess.StaffVtRva &&
                    Safety.On("staffmove") && StaffTransferPlanner.LayoutSupported;
                staffRoleRow = gridStaff.Rows.Add("职务", StaffRole.Title(employment.Job),
                    btnStaffMove.Enabled ? (employment.Full ? "双击调整" : "简式合同，双击调整") : "只读");
                gridStaff.Rows[staffRoleRow].ReadOnly = true;
            }
            ApplyStaffAttributeFilter();
        }

        // FM's manager profile shows 18 of the staff attributes, two under other names
        static string ManagerNote(string attr)
        {
            if (attr == "Judging Player Ability") return "显示为「球员知识」";
            if (attr == "Judging Player Potential") return "显示为「年轻球员知识」";
            if (new[] { "Judging Staff Ability", "Negotiating", "Tactical Knowledge", "Analysing Data", "Physiotherapy", "Sports Science" }.Contains(attr)) return "经理资料页不显示此项";
            return "已验证";
        }

        void ApplyStaff()
        {
            if (fm == null || loadedStaff == null || loadedStaffBlock == null)
            {
                if (loadedStaff!=null) ShowOperationFeedback(FeedbackKind.ReadOnly,"职员属性资料不完整，保持只读。");
                else ShowOperationFeedback(FeedbackKind.Information,"请选择职员并读取资料后再提交。");
                return;
            }
            if (!Safety.On("staff"))
            {
                ShowOperationMessage(FeedbackKind.ReadOnly,
                    "当前游戏版本的职员资料为只读：" + (Safety.Off.ContainsKey("staff") ? Safety.Off["staff"] : Safety.Off["core"]) + "。","职员");
                return;
            }
            if (!StaffIdentityMatches(loadedStaff))
            { ShowOperationMessage(FeedbackKind.Failed,"职员身份已变化，请重新挂接并扫描。","职员"); return; }
            var cur = fm.Read(loadedStaff.Addr - FmLayout.StaffBlockBefore, FmLayout.StaffBlockLen);
            if (!FmLayout.ValidStaffBlock(cur))
            {
                ShowOperationMessage(FeedbackKind.Failed,"这个人的数据已经移位，请重新点「挂接并扫描」。","职员");
                return;
            }
            int written = 0, skipped = 0;
            for (int k = 0; k < FmLayout.StaffAttrs.Length; k++)
            {
                var p = FmLayout.StaffAttrs[k].Split('|');
                int i = (int)(FmLayout.StaffBlockBefore - Convert.ToUInt64(p[1], 16)), v;
                if (!int.TryParse(Convert.ToString(gridStaff.Rows[k].Cells[1].Value), out v)) continue;
                v = Math.Max(1, Math.Min(20, v));
                int old = StaffDisplay(loadedStaffBlock[i], p[2]);
                if (v == old) continue;
                if (cur[i] != loadedStaffBlock[i]) { skipped++; continue; }
                if (WriteLogged(loadedStaff.Addr - FmLayout.StaffBlockBefore + (ulong)i, new[] { (byte)(p[2] == "5" ? v * 5 : v) },
                    loadedStaff.Name + ": " + p[0] + " " + old + " -> " + v)) written++;
            }
            if (loadedStaffPers != null)
            {
                var curP = fm.Read(loadedStaff.Pers + FmLayout.PersonalityAfterPerson, 8);
                for (int k = 0; k < 8; k++)
                {
                    int v;
                    if (!int.TryParse(Convert.ToString(gridStaff.Rows[FmLayout.StaffAttrs.Length + k].Cells[1].Value), out v)) continue;
                    v = Math.Max(1, Math.Min(20, v));
                    if (v == loadedStaffPers[k]) continue;
                    if (curP == null || curP[k] != loadedStaffPers[k]) { skipped++; continue; }
                    if (WriteLogged(loadedStaff.Pers + FmLayout.PersonalityAfterPerson + (ulong)k, new[] { (byte)v },
                        loadedStaff.Name + ": " + FmLayout.Personality[k].Split((char)124)[0] + " " + loadedStaffPers[k] + " -> " + v)) written++;
                }
            }
            if (loadedStaffRep != null)
            {
                int first = FmLayout.StaffAttrs.Length + (loadedStaffPers != null ? 8 : 0);
                string[] rn = { "当前", "国内", "世界" };
                for (int k = 0; k < 3; k++)
                {
                    int v;
                    if (first + k >= gridStaff.Rows.Count || !int.TryParse(Convert.ToString(gridStaff.Rows[first + k].Cells[1].Value), out v)) continue;
                    v = Math.Max(0, Math.Min(10000, v));
                    if (v == loadedStaffRep[k]) continue;
                    ulong a = loadedStaff.Addr - FmLayout.StaffRepBefore[k];
                    var c = fm.Read(a, 2);
                    if (c == null || BitConverter.ToInt16(c, 0) != loadedStaffRep[k]) { skipped++; continue; }
                    if (WriteLogged(a, BitConverter.GetBytes((short)v), loadedStaff.Name + ": " + rn[k] + "声望 " + loadedStaffRep[k] + " -> " + v)) written++;
                }
            }
            if (loadedStaffContract != 0 && loadedStaffContractRow + 1 < gridStaff.Rows.Count && Safety.On("staffcontract"))
            {
                var currentContract = StaffEmployment.Read(fm.Read, fm.Base, fm.ImageSize, loadedStaff.Pers);
                if (currentContract == null || !currentContract.Full || currentContract.Address != loadedStaffContract)
                {
                    LoadStaff(loadedStaff);
                    ShowOperationFeedback(WriteFeedbackKind(written,1),"职员合同已变化，合同修改已跳过。已修改 " + written + " 项其他数值。");
                    return;
                }
                double r = Rate(), dollars;
                string wageText = Convert.ToString(gridStaff.Rows[loadedStaffContractRow].Cells[1].Value);
                if (wageText != Money(loadedStaffWage * r * 52) && ParseMoney(wageText, out dollars))
                {
                    int perWeek = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(dollars / r / 52)));
                    if (perWeek != loadedStaffWage)
                    {
                        var curW = fm.ReadI32(loadedStaffContract + FmLayout.ContractWage);
                        if (curW == loadedStaffWage && WriteLogged(loadedStaffContract + FmLayout.ContractWage, BitConverter.GetBytes(perWeek),
                            loadedStaff.Name + "：周薪 £" + loadedStaffWage + "/wk -> £" + perWeek + "/wk")) written++;
                        else skipped++;
                    }
                }
                DateTime until;
                string dateText = Convert.ToString(gridStaff.Rows[loadedStaffContractRow + 1].Cells[1].Value);
                if (dateText != FromPacked(loadedStaffExpiry).ToString("yyyy-MM-dd") &&
                    DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out until))
                {
                    uint packed = ((uint)until.Year << 16) | (loadedStaffExpiry & 0xFE00u) | (uint)until.DayOfYear;
                    var curE = fm.Read(loadedStaffContract + FmLayout.ContractExpiry, 4);
                    if (curE != null && BitConverter.ToUInt32(curE, 0) == loadedStaffExpiry && WriteLogged(loadedStaffContract + FmLayout.ContractExpiry, BitConverter.GetBytes(packed),
                        loadedStaff.Name + "：合同到期日 " + FromPacked(loadedStaffExpiry).ToString("yyyy-MM-dd") + " -> " + dateText)) written++;
                    else skipped++;
                }
            }
            LoadStaff(loadedStaff);
            ShowOperationFeedback(WriteFeedbackKind(written,skipped),
                written+" 项职员数值已修改"+(skipped>0?"，"+skipped+" 项因游戏数据变化或写入失败而跳过":"")+
                "。"+(written>0?"请在 FM 中推进一天后查看。":""));
        }

        // ---------- freeze ----------
        // Frozen values are checked every 500 ms and put back when FM lowered them (they may still rise). The first write goes through WriteLogged (so Undo can restore it),
        // the repeats do not, to keep the log readable. A freeze stops by itself when its object's guard no longer matches.

        TabPage BuildFreezeTab()
        { return BuildReadableFreezeTab(); }

        void RefreshFreezeList()
        {
            lstFreeze.BeginUpdate();
            var sel = new HashSet<Freeze>(lstFreeze.SelectedItems.Cast<Freeze>());
            lstFreeze.Items.Clear();
            foreach (var f in freezes) { lstFreeze.Items.Add(f); if (sel.Contains(f)) lstFreeze.SelectedItems.Add(f); }
            lstFreeze.EndUpdate();
            lblFreeze.Text = freezes.Count == 0
                ? "当前没有冻结项目，可从球员、球队或俱乐部页面添加。"
                : freezes.Count + " 项数值正在冻结，编辑器关闭后停止。允许数值增加，最低保持冻结时的数值；恢复次数表示游戏降低数值后被恢复的次数。";
            if (freezePage != null) freezePage.Text = freezes.Count == 0 ? "冻结管理" : "冻结管理（" + freezes.Count + "）";
        }

        static KeyValuePair<ulong, byte[]> Guard(ulong addr, byte[] expected) { return new KeyValuePair<ulong, byte[]>(addr, expected); }

        // The secondary person class depends on whether this is a player-coach.
        List<KeyValuePair<ulong, byte[]>> PlayerGuards(Player p)
        {
            return RosterPlayerIdentity.Guards(p,fm.Base,FmProcess.PersonVtRva,FmProcess.PlayerCoachVtRva,FmLayout.PlayerCoachExtra);
        }

        bool GuardsOk(Freeze f)
        {
            foreach (var g in f.Guards) { var cur = fm.Read(g.Key, g.Value.Length); if (cur == null || !cur.SequenceEqual(g.Value)) return false; }
            return true;
        }

        bool AddFreeze(ulong addr, byte[] value, bool signed, string what, List<KeyValuePair<ulong, byte[]>> guards)
        {
            if (fm == null || Safety.Off.ContainsKey("core")) return false;
            var f = new Freeze { Addr = addr, Value = value, Signed = signed, What = what, Guards = guards };
            if (!GuardsOk(f)) return false;
            var cur = fm.Read(addr, value.Length);
            if (cur == null) return false;
            if (AsNumber(cur, signed) < AsNumber(value, signed) && !WriteLogged(addr, value, what)) return false;
            freezes.RemoveAll(x => x.Addr == addr);
            freezes.Add(f);
            LogLine("已开启冻结：" + what);
            RefreshFreezeList();
            return true;
        }

        // Condition, sharpness and morale at their maximum
        int FreezeStatus(Player p)
        {
            int n = 0;
            if (!Safety.On("status")) return 0;
            var guards = PlayerGuards(p);
            for (int k = 0; k < FmLayout.StatusNames.Length; k++)
                if (AddFreeze(p.Tail - FmLayout.StatusBeforePerson[k], StatusValues.Encode(FmLayout.StatusMax[k],k), false,
                        p.Name + ": " + FmLayout.StatusNames[k] + " 保持 100", guards)) n++;
            return n;
        }

        // Balance, transfer budget and wage budget never below their current values (apply new amounts first)
        int FreezeMoney(Club c)
        {
            int n = 0;
            if(!Safety.On("finance") || !btnScan.Enabled || !ClubCatalogReader.MatchesFullFinance(fm,c))return 0;
            var guards=new List<KeyValuePair<ulong,byte[]>>
            {
                Guard(c.Addr,BitConverter.GetBytes(fm.Base+FmProcess.ClubVtRva)),
                Guard(c.Addr+12,BitConverter.GetBytes(c.Uid)),
                Guard(c.Addr+FmLayout.ClubFinance,BitConverter.GetBytes(c.Finance)),
                Guard(c.Finance,BitConverter.GetBytes(fm.Base+FmProcess.FinanceVtRva))
            };
            var names = new[] { "balance", "transfer budget", "wage budget" };
            var fields = new[] { FmLayout.FinBalance, FmLayout.FinTransfer, FmLayout.FinWageBudget };
            for (int k = 0; k < fields.Length; k++)
            {
                var v = fm.ReadI32(c.Finance + fields[k]);
                if (v == null) continue;
                if (AddFreeze(c.Finance + fields[k], BitConverter.GetBytes(v.Value), true,
                        c.Name + ": " + names[k] + " 最低 £" + v.Value.ToString("N0", CultureInfo.InvariantCulture), guards)) n++;
            }
            return n;
        }

        void FreezeTick()
        {
            if (fm == null || freezes.Count == 0) return;
            bool changed = false;
            foreach (var f in freezes.ToList())
            {
                if (!GuardsOk(f)) { StopFreeze(f, "已自动停止（游戏数据已移位或被释放，请重新扫描）"); changed = true; continue; }
                var cur = fm.Read(f.Addr, f.Value.Length);
                if (cur == null) { StopFreeze(f, "已自动停止（无法读取游戏数据）"); changed = true; continue; }
                if (AsNumber(cur, f.Signed) < AsNumber(f.Value, f.Signed) && fm.Write(f.Addr, f.Value)) { f.Rewrites++; changed = true; }
            }
            if (changed) RefreshFreezeList();
        }

        static long AsNumber(byte[] b, bool signed)
        {
            switch (b.Length)
            {
                case 1: return signed ? (long)(sbyte)b[0] : b[0];
                case 2: return signed ? (long)BitConverter.ToInt16(b, 0) : BitConverter.ToUInt16(b, 0);
                case 4: return signed ? (long)BitConverter.ToInt32(b, 0) : BitConverter.ToUInt32(b, 0);
                default: return BitConverter.ToInt64(b, 0);
            }
        }

        void StopFreeze(Freeze f, string why)
        {
            if (freezes.Remove(f)) LogLine("已停止冻结：" + f.What + " - " + why);
        }

        void StopAllFreezes(string why)
        {
            foreach (var f in freezes.ToList()) StopFreeze(f, why);
            RefreshFreezeList();
        }

        void LogLine(string what)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + what;
            lstLog.Items.Add(line);
            try { File.AppendAllText(logPath, line + Environment.NewLine); } catch { }
        }

        // ---------- writing + undo ----------

        int curGroup, lastGroup;

        void RecordGroupedChanges(IList<EligibilityEdit> edits,Func<bool> undoGuard, StaffTransferPlan staffTransfer)
        {
            int group=++lastGroup;
            var items=new Change[edits.Count];
            var lines=new StringBuilder();
            var when=DateTime.Now;
            string timestamp=when.ToString("yyyy-MM-dd HH:mm:ss");
            for(int i=0;i<items.Length;i++)
            {
                var edit=edits[i];
                items[i]=new Change{Addr=edit.Address,Old=(byte[])edit.Before.Clone(),New=(byte[])edit.After.Clone(),
                    What=staffTransfer==null?edit.Label:staffTransfer.Origin.Person.DisplayName+" · "+edit.Label,
                    When=when,Group=group,Guard=undoGuard,Growth=staffTransfer==null,StaffTransfer=staffTransfer};
                lines.Append(timestamp).Append("  ").Append(items[i].What).Append("  @").Append(edit.Address.ToString("X"))
                    .Append(' ').Append(BitConverter.ToString(edit.Before)).Append(" -> ").AppendLine(BitConverter.ToString(edit.After));
            }
            changes.AddRange(items);
            lstLog.BeginUpdate();
            try { lstLog.Items.AddRange(items); }
            finally { lstLog.EndUpdate(); }
            try { File.AppendAllText(logPath,lines.ToString()); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }

        bool WriteLogged(ulong addr, byte[] data, string what)
        {
            if (Safety.Off.ContainsKey("core"))
            {
                if (!readOnlyWarned) MessageBox.Show("当前游戏版本的编辑器为只读：" + Safety.Off["core"] + "。详见「变更记录」页。", "只读模式");
                readOnlyWarned = true; return false;
            }
            var old = fm.Read(addr, data.Length);
            if (old == null || !fm.Write(addr, data)) { MessageBox.Show("写入失败：" + what, "写入数据"); return false; }
            RecordLoggedChange(addr, old, data, what);
            return true;
        }

        void RecordLoggedChange(ulong addr, byte[] old, byte[] data, string what)
        {
            var ch = new Change { Addr = addr, Old = old, New = data, What = what, When = DateTime.Now, Group = curGroup };
            changes.Add(ch);
            lstLog.Items.Add(ch);
            var line = ch.When.ToString("yyyy-MM-dd HH:mm:ss") + "  " + what + "  @" + addr.ToString("X") + " " + BitConverter.ToString(old) + " -> " + BitConverter.ToString(data);
            try { File.AppendAllText(logPath, line + Environment.NewLine); } catch { }
        }

        void UndoAll()
        {
            var open = changes.Where(c => !c.Undone).ToList();
            if (open.Count == 0 || fm == null) return;
            if (MessageBox.Show("撤销本次会话的全部 " + open.Count + " 项修改？" + (freezes.Count > 0 ? "同时将停止所有冻结。" : ""),
                    "撤销修改", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            StopAllFreezes("因撤销修改而停止");
            StopGrow("因撤销修改而停止");
            Undo(open);
        }

        void UndoSelected()
        {
            var sel = lstLog.SelectedItems.OfType<Change>().Where(c => !c.Undone).ToList();
            // a player swap or move is several writes: undo all of them
            var groups = new HashSet<int>(sel.Where(c => c.Group != 0).Select(c => c.Group));
            sel = sel.Union(changes.Where(c => !c.Undone && groups.Contains(c.Group))).ToList();
            if (fm == null) return;
            if (sel.Count == 0) { MessageBox.Show("请先选择一项或多项带时间的修改记录。标记为「已撤销」的项目已经恢复。", "撤销修改"); return; }
            if (sel.Count > 1 && MessageBox.Show("撤销所选的 " + sel.Count + " 项修改？", "撤销修改", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            StopGrow("因撤销修改而停止");
            // a frozen value would be written straight back, so its freeze stops
            foreach (var f in freezes.ToList())
                if (sel.Any(c => f.Addr < c.Addr + (ulong)c.New.Length && c.Addr < f.Addr + (ulong)f.Value.Length)) StopFreeze(f, "因撤销修改而停止");
            RefreshFreezeList();
            Undo(sel);
        }

        // Newest first. A change is only restored while memory still holds what the editor wrote; if the game or a
        // later change wrote there since, it is skipped (undo the later change first).
        void Undo(List<Change> list)
        {
            int restored = 0, skipped = 0;
            var notes = new List<string>();
            var handledGrowth = new HashSet<int>();
            var handledFacilities = new HashSet<int>();
            var handledStaffTransfers = new HashSet<int>();
            bool staffRestored = false;
            var selected=new HashSet<Change>(list);
            var groups=changes.Where(c=>!c.Undone && (c.Growth || c.Facility || c.StaffTransfer!=null)).ToLookup(c=>c.Group);
            for(int position=changes.Count-1;position>=0;position--)
            {
                var ch=changes[position];
                if(!selected.Contains(ch))continue;
                if (ch.StaffTransfer != null)
                {
                    if (!handledStaffTransfers.Add(ch.Group)) continue;
                    var group = groups[ch.Group].ToList();
                    var plan = ch.StaffTransfer;
                    string error = "请同时选择本次职员转会的全部记录。";
                    if (group.All(selected.Contains) && plan.Apply(fm.Read, fm.Write, ch.Guard, true, out error))
                    {
                        restored += group.Count;
                        foreach (var item in group)
                        {
                            item.Undone = true;
                            int index = lstLog.Items.IndexOf(item);
                            if (index >= 0) lstLog.Items[index] = item;
                            LogLine("撤销：" + item.What);
                        }
                        plan.Origin.Person.SetClub(plan.SourceClubName);
                        staffRestored = true;
                    }
                    else { skipped += group.Count; notes.Add("职员转会整组未撤销：" + error); }
                    if (error != null && error.Contains("重载")) break;
                    continue;
                }
                if(ch.Facility)
                {
                    if(!handledFacilities.Add(ch.Group))continue;
                    var group=groups[ch.Group].Where(c=>c.Facility && !c.Undone).ToList();string error="请同时选择本次设施修改的全部记录。";
                    if(group.All(selected.Contains) && FacilityTransactions.Undo(fm.Read,fm.Write,group,out error))
                    {
                        restored+=group.Count;
                        foreach(var item in group){int index=lstLog.Items.IndexOf(item);if(index>=0)lstLog.Items[index]=item;LogLine("撤销："+item.What);}
                    }
                    else{skipped+=group.Count;notes.Add("设施整组未撤销："+error);}
                    if(error!=null && error.Contains("重载"))break;
                    continue;
                }
                if (ch.Growth)
                {
                    if (!handledGrowth.Add(ch.Group)) continue;
                    var group=groups[ch.Group].Where(c=>c.Growth && !c.Undone).ToList();
                    string error = "请同时选择本次 CA 与属性修改。";
                    if (group.All(selected.Contains) && GrowthUndo.Apply(fm.Read, fm.Write, group, out error))
                    {
                        restored += group.Count;
                        foreach (var item in group)
                        {
                            int index = lstLog.Items.IndexOf(item); if (index >= 0) lstLog.Items[index] = item;
                            LogLine("撤销：" + item.What);
                        }
                    }
                    else { skipped += group.Count; notes.Add("属性成长整组未撤销：" + error); }
                    if (error != null && error.Contains("重载")) break;
                    continue;
                }
                var cur = fm.Read(ch.Addr, ch.New.Length);
                if ((ch.Guard != null && !ch.Guard()) || cur == null || !cur.SequenceEqual(ch.New))
                {
                    skipped++;
                    bool later=changes.Skip(position+1).Any(c=>!c.Undone && c.Addr<ch.Addr+(ulong)ch.New.Length && ch.Addr<c.Addr+(ulong)c.New.Length);
                    notes.Add(ch.What + (later ? "：之后另一次修改写入过该位置，请先撤销较新的修改" : "：此数据已被游戏更新"));
                    continue;
                }
                if (!fm.Write(ch.Addr, ch.Old)) { skipped++; notes.Add(ch.What + "：写入失败"); continue; }
                ch.Undone = true; restored++;
                int i = lstLog.Items.IndexOf(ch);
                if (i >= 0) lstLog.Items[i] = ch;   // re-shows the [undone] mark
                LogLine("撤销：" + ch.What);
            }
            if (skipped > 0) LogLine("撤销：" + skipped + " 项已跳过");
            lblStatus.Text = restored + " 项修改已撤销" + (skipped > 0 ? ", " + skipped + " 项已跳过" : "") + ".";
            if (notes.Count > 0) MessageBox.Show(restored + " 项修改已撤销。以下项目已跳过：\n- " + string.Join("\n- ", notes.Take(15).ToArray()) + (notes.Count > 15 ? "\n..." : ""), "撤销修改");
            if (loadedPlayer != null) LoadPlayer(loadedPlayer);
            if (loadedClub != null) LoadClub(loadedClub);
            if (staffRestored) RefreshStaffAfterTransfer(loadedStaff);
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--selftest") return SelfTest(args.Length > 1 ? args[1] : "selftest.txt");
            if (args.Length >= 2 && args[0] == "--check") return MainForm.Check(args[1], args.Contains("--calibrate"));
            if (args.Length >= 1 && args[0] == "--dump-profile")
            {
                Profile.Load("", AppDomain.CurrentDomain.BaseDirectory);
                File.WriteAllText(args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile-fm27.txt"), Profile.Dump());
                return 0;
            }
            Application.EnableVisualStyles();
            var preferences = new GrowthPreferenceStore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GrowthPreferenceStore.FileName));
            Application.Run(new MainForm(args.Contains("--scan"), args.Contains("--calibrate"), preferences));
            return 0;
        }

        // Read-only check used during development: prints counts and a few known players/clubs.
        static int SelfTest(string outPath)
        {
            var sb = new StringBuilder();
            try
            {
                using (var fm = FmProcess.Attach())
                {
                    var persons = new List<ulong>(); var clubAddrs = new List<ulong>(); var staffAddrs = new List<ulong>();
                    var sw = Stopwatch.StartNew();
                    fm.ScanQwords(new[] { fm.Base + FmProcess.PersonVtRva, fm.Base + FmProcess.ClubVtRva, fm.Base + FmProcess.StaffVtRva },
                        (t, a) => { if (t == 0) persons.Add(a); else if (t == 1) clubAddrs.Add(a); else staffAddrs.Add(a); }, f => { });
                    sb.AppendLine("scan " + sw.Elapsed.TotalSeconds.ToString("F1") + "s, person objects " + persons.Count + ", club objects " + clubAddrs.Count + ", staff objects " + staffAddrs.Count);
                    int validStaff = 0;
                    foreach (var s in staffAddrs)
                    {
                        var b = fm.Read(s - FmLayout.StaffBlockBefore, FmLayout.StaffBlockLen);
                        if (!FmLayout.ValidStaffBlock(b)) continue;
                        validStaff++;
                        string name, search;
                        fm.ReadPersonNames(fm.Read(s, 0x78), out name, out search);
                        if (name != "Des Buckingham") continue;
                        sb.AppendLine("staff " + name + ": " + string.Join(", ", FmLayout.StaffAttrs.Select(a =>
                        {
                            var p = a.Split('|'); byte raw = b[(int)(FmLayout.StaffBlockBefore - Convert.ToUInt64(p[1], 16))];
                            return p[0] + " " + (p[2] == "5" ? Math.Round(raw / 5.0, MidpointRounding.AwayFromZero) : raw);
                        }).ToArray()));
                    }
                    sb.AppendLine("valid staff " + validStaff);
                    int valid = 0; int named = 0; int injured = 0;
                    foreach (var p in persons)
                    {
                        var b = fm.Read(p - FmLayout.BlockBeforePerson, 69);
                        if (!FmLayout.ValidBlock(b)) continue;
                        valid++;
                        string name, search;
                        fm.ReadPersonNames(fm.Read(p, 0x70), out name, out search);
                        if (!string.IsNullOrEmpty(name)) named++;
                        ulong med = fm.ReadPtr(p - FmLayout.MedicalBeforePerson);
                        var v = med > 0x10000 ? fm.Read(med, 16) : null;
                        if (v != null && BitConverter.ToUInt64(v, 8) > BitConverter.ToUInt64(v, 0) && BitConverter.ToUInt64(v, 8) - BitConverter.ToUInt64(v, 0) < 0x400)
                        {
                            injured++;
                            if (search.Contains("Wildsmith") || search.Contains("Barkhuizen") || search.Contains("Joe Ward")) sb.AppendLine("injured: " + name);
                        }
                        if (search.Contains("Cristiano Ronaldo") || (search.Contains("Ward") && fm.ReadI32(p + 0x44) == (1995 << 16 | 234)))
                            sb.AppendLine(name + " [" + search + "]" + " person " + p.ToString("X") + " crossing raw " + b[15] + " heading raw " + b[18]);
                    }
                    int withContract = 0; string wardC = "";
                    foreach (var p in persons)
                    {
                        ulong c = fm.ReadPtr(p + FmLayout.ContractAfterPerson);
                        if (c < 0x10000 || fm.ReadPtr(c) != fm.Base + FmProcess.ContractVtRva || fm.ReadPtr(c + 8) != p) continue;
                        withContract++;
                        if (fm.ReadI32(p + 0x44) == (1995 << 16 | 234)) { var e = fm.Read(c + FmLayout.ContractExpiry, 4); wardC += " [wage GBP " + fm.ReadI32(c + FmLayout.ContractWage) + "/wk, expires day " + (BitConverter.ToUInt32(e, 0) & 0x1FF) + "/" + (BitConverter.ToUInt32(e, 0) >> 16) + "]"; }
                    }
                    sb.AppendLine("players with a contract " + withContract + " (born 22 Aug 1995:" + wardC + ")");
                    sb.AppendLine("valid players " + valid + ", with a name " + named + ", injured " + injured);
                    ulong finVt = fm.Base + FmProcess.FinanceVtRva; int clubsOk = 0;
                    foreach (var c in clubAddrs)
                    {
                        ulong fin = fm.ReadPtr(c + FmLayout.ClubFinance);
                        if (fin == 0 || fm.ReadPtr(fin) != finVt) continue;
                        clubsOk++;
                        var name = fm.ReadLenString(fm.ReadPtr(c + FmLayout.ClubName));
                        if (name == "Derby County")
                        {
                            var squad = new List<string>();
                            foreach (var p in persons)
                            {
                                ulong team = fm.ReadPtr(p - FmLayout.TeamBeforePerson);
                                if (team < 0x10000 || fm.ReadPtr(team + FmLayout.TeamClub) != c) continue;
                                if (!FmLayout.ValidBlock(fm.Read(p - FmLayout.BlockBeforePerson, 69))) continue;
                                string pn, ps; fm.ReadPersonNames(fm.Read(p, 0x70), out pn, out ps); squad.Add(pn);
                            }
                            sb.AppendLine("Derby squad " + squad.Count + ": " + string.Join(", ", squad.OrderBy(x => x).ToArray()));
                        }
                        if (name == "Derby County")
                            sb.AppendLine("Derby County fin " + fin.ToString("X") + " balance £" + fm.ReadI32(fin + FmLayout.FinBalance) + " transfer £" + fm.ReadI32(fin + FmLayout.FinTransfer) +
                                          " next £" + fm.ReadI32(fin + FmLayout.FinTransferNext) + " wage/wk £" + fm.ReadI32(fin + FmLayout.FinWageBudget) + " spend/wk £" + fm.ReadI32(fin + FmLayout.FinWageSpend) +
                                          " | facilities " + string.Join(", ", Enumerable.Range(0, FmLayout.FacilityNames.Length).Select(k =>
                                          {
                                              ulong data = fm.ReadPtr(c + FmLayout.ClubData);
                                              var f = FmLayout.FacilityFin[k] != 0 ? fm.Read(fin + FmLayout.FacilityFin[k], 1) : fm.Read(data + FmLayout.FacilityData[k], 1);
                                              return FmLayout.FacilityNames[k] + " " + (f == null ? "?" : f[0].ToString());
                                          }).ToArray()));
                    }
                    sb.AppendLine("valid clubs " + clubsOk);
                }
            }
            catch (Exception ex) { sb.AppendLine("ERROR " + ex); }
            File.WriteAllText(outPath, sb.ToString());
            return 0;
        }
    }
}
