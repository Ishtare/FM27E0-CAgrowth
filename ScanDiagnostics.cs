// Modified build notice: FM27E0-CAgrowth v1.1.13; changes dated 2026-11-15.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    static class ScanDiagnostics
    {
        public static string ProcessIssue(int count)
        {
            if (count == 0) return "Football Manager（fm.exe）未启动，请先启动游戏并载入存档。";
            if (count != 1) return "检测到 " + count + " 个 fm.exe，无法确定应连接哪个游戏。请关闭不使用的游戏实例或残留进程后重试；本次未自动选择第一个进程。";
            return null;
        }

        public static string Connection(int pid, string module, bool calibrate, string profile, string source)
        {
            return "连接环境：修改器 v" + MainForm.Version + "；PID " + pid + "；模块 " + (string.IsNullOrEmpty(module) ? "未记录" : module) +
                "；自动识别 " + (calibrate ? "启用" : "关闭") + "；配置 " + profile + (source == null ? "（内置）" : "（外部 " + Path.GetFileName(source) + "）") + "。";
        }

        public static string Candidate(ulong rva, int blocks, int sampled, int identities)
        {
            string result = blocks < 1000 ? "候选不足，未进行身份抽样" : identities < sampled * 0.9 ? "姓名／生日检查未通过" : "通过";
            return "校准球员：类型 0x" + rva.ToString("X") + "，属性候选 " + blocks + "，身份抽样 " + (identities < 0 ? "未执行" : identities + "/" + sampled) + "；" + result + "。";
        }

        public static void AppendFailure(StringBuilder text, ScanResult result, IEnumerable<string> recovery)
        {
            var lines = result.Diagnostics.Where(s => s != null && new[] { "引擎版本：", "连接环境：", "内存扫描：", "球员扫描：", "名单筛选：" }.Any(p => s.StartsWith(p, StringComparison.Ordinal))).Take(5).ToList();
            if (recovery != null)
            {
                lines.AddRange(recovery.Where(s => s != null && s.StartsWith("校准候选：", StringComparison.Ordinal)).Take(1));
                lines.AddRange(recovery.Where(s => s != null && s.StartsWith("校准球员：", StringComparison.Ordinal)).Take(2));
            }
            if (lines.Count == 0) return;
            text.AppendLine("扫描诊断摘要：");
            foreach (string line in lines) text.AppendLine(line.Length > 280 ? line.Substring(0, 280) + "…" : line);
            text.AppendLine();
        }
    }
}
