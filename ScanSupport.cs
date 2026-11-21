// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fm24Editor
{
    static class ScanRecovery
    {
        // Only player identity/layout determines whether the core scanner needs recovery.
        // Club, staff, city and competition checks remain isolated in Safety.Run.
        public static string PlayerIssue(ScanResult result)
        {
            if(result.Players.Count<1000)return "识别到 "+result.Players.Count+" 名球员，未达到完整名单检查门槛";
            var sample=result.Players.Where((p,i)=>i%Math.Max(1,result.Players.Count/400)==0).ToList();
            if(sample.Count(p=>p.Born.HasValue && !string.IsNullOrEmpty(p.Name))<sample.Count*0.95)
                return "球员姓名或出生日期未通过检查";
            return null;
        }

        public static ScanResult Read(Func<ScanResult> scan,Func<List<string>> calibrate,bool force,List<string> notes,Action<string> status)
        {
            var initial=scan();string issue=PlayerIssue(initial);
            if(issue==null && !force)return initial;
            var layout=Profile.Fields().ToDictionary(f=>f,f=>f.GetValue(null) is Array?((Array)f.GetValue(null)).Clone():f.GetValue(null));
            Action restore=()=>{foreach(var field in layout)field.Key.SetValue(null,field.Value);};
            notes.Add(force?"已请求重新识别数据位置（--calibrate）":"球员识别需要重新检查："+issue);
            notes.AddRange(initial.Diagnostics.Select(s=>"首次扫描："+s));
            status("正在重新识别球员数据位置……");
            try
            {
                notes.AddRange(calibrate());
                var recovered=scan();string after=PlayerIssue(recovered);
                if(after==null)notes.Add("重新识别结果：球员名单检查通过。");
                else if(issue==null)
                {
                    restore();notes.Add("重新识别未通过，已恢复此前有效的球员数据位置。");return initial;
                }
                else notes.Add("重新识别后仍未通过："+after+"。保持只读，请提供本次变更记录。");
                return recovered;
            }
            catch
            {
                restore();
                if(issue!=null)throw;
                notes.Add("重新识别发生错误，已保留此前有效的球员名单。");return initial;
            }
        }
    }

    static class AttachReport
    {
        public static bool IsUnmapped(string reason)
        {
            return reason != null && reason.StartsWith("not mapped", StringComparison.Ordinal);
        }

        public static bool ShouldPopup(FmProcess fm, IDictionary<string,string> disabled)
        {
            if(disabled.ContainsKey("core"))return true;
            bool current=CurrentBuildProfile.Matches(fm);
            return disabled.Any(item=>!IsUnmapped(item.Value) && !(current &&
                ((item.Key=="squadnumber" && item.Value=="租借合同包含有效球衣号码") ||
                 (item.Key=="tickets" && item.Value=="季票数量和票价有效"))));
        }

        public static string Build(string version,bool listed,ScanResult result,IDictionary<string,string> disabled,bool onlyUnmapped,IEnumerable<string> recovery=null)
        {
            var text=new StringBuilder("修改器 v"+MainForm.Version+"；已连接 FM，引擎版本："+version+"。\n\n");
            text.AppendLine("识别到 "+result.Players.Count+" 名球员、"+result.Staff.Count+" 名职员、"+result.Clubs.Count+" 家俱乐部。\n");
            if(disabled.ContainsKey("core"))
            {
                text.AppendLine("球员核心数据未通过检查，当前全部编辑功能保持只读。\n原因："+disabled["core"]+"。\n");
                if(result.Players.Count==0)
                    text.AppendLine("扫描未识别到球员；这不表示未收录中国联赛或其他联赛。请确认存档已载入完成后重新扫描；仍为 0 时可从「连接详情…」复制本次诊断。\n");
                if(result.RawPlayers>0)text.AppendLine("筛选前识别到 "+result.RawPlayers+" 人，其中隐藏女足球员 "+result.HiddenWomen+" 人。\n");
                ScanDiagnostics.AppendFailure(text,result,recovery);
            }
            else if(disabled.Count==0)text.AppendLine("本次检查通过，可以编辑。\n");
            else
            {
                text.AppendLine("球员核心数据检查通过，可以修改已通过检查的项目。\n");
                var unmapped=disabled.Where(item=>IsUnmapped(item.Value)).ToList();
                var failed=disabled.Where(item=>!IsUnmapped(item.Value)).ToList();
                if(unmapped.Count>0)
                {
                    text.AppendLine("尚未适配当前构建的功能（只读）：");
                    foreach(var item in unmapped)text.AppendLine("- "+Zh.G(item.Key));
                    text.AppendLine();
                }
                if(failed.Count>0)
                {
                    text.AppendLine("以下项目检查未通过（只读）：");
                    foreach(var item in failed)text.AppendLine("- "+Zh.G(item.Key)+"（"+(item.Value??"读取校验未通过")+"）");
                    text.AppendLine();
                }
            }
            if(!disabled.ContainsKey("core") && !string.IsNullOrEmpty(result.FacilityNote))text.AppendLine(result.FacilityNote+"\n");
            if(!listed)text.AppendLine("此构建未列入整套功能验证清单；该提示本身不会阻止扫描或编辑，可用功能以本次逐项检查为准。\n");
            text.Append("详细结果见「连接详情…」或「变更记录」。");return text.ToString();
        }
    }
}
