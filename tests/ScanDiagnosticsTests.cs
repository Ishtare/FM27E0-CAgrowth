using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Fm24Editor
{
    static class ScanDiagnosticsTests
    {
        public static void Run(Action<bool,string> check)
        {
            check(ScanDiagnostics.ProcessIssue(0).Contains("未启动"), "missing FM process is distinct from an empty player scan");
            check(ScanDiagnostics.ProcessIssue(1) == null, "one FM process remains eligible for attachment");
            check(ScanDiagnostics.ProcessIssue(2).Contains("未自动选择第一个"), "ambiguous FM targets cannot silently attach to the first process");
            foreach (string method in new[] { "ReadProcessMemory", "VirtualQueryEx" })
            {
                var import = (DllImportAttribute)Attribute.GetCustomAttribute(typeof(Native).GetMethod(method), typeof(DllImportAttribute));
                check(import != null && import.SetLastError, "native failure codes are preserved by the marshaler: " + method);
            }

            var result = new ScanResult();
            result.Diagnostics.Add(ScanDiagnostics.Connection(123, "game_plugin.dll", true, "profile", null));
            using (var process = new FmProcess { ScanRegionsSeen = 12, ScanChunksRead = 10, ScanChunksFailed = 2, ScanBytesRead = 40960 })
            {
                process.ScanReadErrors[299] = 2;
                string memory = process.ScanSummary("内存扫描");
                check(memory.Contains("失败块 2") && memory.Contains("299×2") && memory.Contains("区域查询完成"), "read failures are reported separately from successful region enumeration");
                process.RegionQueryStoppedAt = 0x12000; process.RegionQueryError = 5;
                check(process.ScanSummary("校准内存").Contains("0x12000，错误 5"), "region enumeration failures retain their stop address and error");
                result.Diagnostics.Add(memory);
            }
            result.Diagnostics.Add("球员扫描：类型命中 0，属性检查排除 0，资料无法读取 0，姓名无法读取 0。");
            result.Diagnostics.Add("名单筛选：原始球员 0，隐藏女足 0，保留球员 0。");
            var recovery = new List<string> { "校准候选：2 组属性候选", ScanDiagnostics.Candidate(0x100, 1500, 200, 0), ScanDiagnostics.Candidate(0x200, 800, 200, -1) };
            var blocked = new Dictionary<string,string> { { "core", "0 名球员" } };
            string report = AttachReport.Build("27.0.1+2503222", false, result, blocked, false, recovery);
            check(report.Contains("修改器 v" + MainForm.Version) && report.Contains("PID 123") && report.Contains("game_plugin.dll"), "a screenshot identifies both editor and selected engine");
            check(report.Contains("299×2") && report.Contains("类型命中 0") && report.Contains("姓名／生日检查未通过"), "zero-player report includes actual read and candidate failures");
            check(report.Contains("候选不足，未进行身份抽样") && !report.Contains("身份抽样 -1"), "a skipped candidate identity check is not presented as a failed sample");
            check(report.Contains("全部编辑功能保持只读") && report.Contains("该提示本身不会阻止扫描或编辑"), "core failure and the informational version notice remain distinct");
            string source = ScanDiagnostics.Connection(123, "game_plugin.dll", false, "custom", @"C:\Private\profiles\custom.txt");
            check(source.Contains("自动识别 关闭") && source.Contains("外部 custom.txt") && !source.Contains("Private"), "diagnostics disclose a disabled external profile without exposing its directory");

            var good = new ScanResult();
            for (int i = 0; i < 1000; i++) good.Players.Add(new Player { Name = "Test Person " + i, Born = new DateTime(2000, 1, 1) });
            check(ScanRecovery.PlayerIssue(good) == null, "minimum valid player sample remains sufficient without clubs or staff");
            report = AttachReport.Build("27.0.1+2503222", false, good, new Dictionary<string,string>(), false, recovery);
            check(report.Contains("本次检查通过，可以编辑") && !report.Contains("全部编辑功能保持只读") && !report.Contains("扫描诊断摘要"), "unlisted version does not block a valid player scan or show failure-only diagnostics");
            result.RawPlayers = 1000; result.HiddenWomen = 1000;
            report = AttachReport.Build("27.0.1+2503222", true, result, blocked, false, recovery);
            check(report.Contains("筛选前识别到 1000 人，其中隐藏女足球员 1000 人"), "filtering remains distinguishable from an entirely unread player list");
            result = new ScanResult();
            result.Diagnostics.AddRange(Enumerable.Repeat("内存扫描：" + new string('X', 10000), 20));
            recovery = Enumerable.Repeat("校准球员：" + new string('Y', 10000), 20).ToList();
            report = AttachReport.Build("27.0.1+2503222", false, result, blocked, false, recovery);
            check(report.Length < 4000, "failure popup remains bounded when detailed diagnostics are large");
        }
    }
}
