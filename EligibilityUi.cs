// Modified build notice: FM27E0-CAgrowth v1.1.17; changes dated 2026-11-16.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        Button btnEligibility=new Button {Text="报名与资格…",AutoSize=true,Name="PlayerEligibilityButton"};
        List<EligibilityCompetition> EligibilityCompetitions(FmProcess process)
        {
            var result=new Dictionary<ulong,EligibilityCompetition>();var seenTeams=new HashSet<ulong>();
            foreach(var player in players)
            {
                try
                {
                    ulong team=process.ReadPtr(player.Tail-FmLayout.TeamBeforePerson);
                    if(team<0x10000 || !seenTeams.Add(team) || process.ReadPtr(team)!=process.Base+EligibilityLayout.TeamVt)continue;
                    ulong address=process.ReadPtr(team+0x50);
                    if(address<0x10000 || result.ContainsKey(address) || process.ReadPtr(address)!=process.Base+EligibilityLayout.CompetitionVt)continue;
                    string name=process.ReadLenString(process.ReadPtr(address+0x40));int? uid=process.ReadI32(address+12);
                    if(!string.IsNullOrEmpty(name) && uid.HasValue && uid>0)result[address]=new EligibilityCompetition {Address=address,Uid=uid.Value,Name=name};
                }
                catch{ }
            }
            // Existing scanned prize tables may expose additional competitions without guessing a global root.
            foreach(ulong address in prizes.Select(p=>p.Comp).Distinct())
            {
                try
                {
                    if(address<0x10000 || result.ContainsKey(address) || process.ReadPtr(address)!=process.Base+EligibilityLayout.CompetitionVt)continue;
                    string name=process.ReadLenString(process.ReadPtr(address+0x40));int? uid=process.ReadI32(address+12);
                    if(!string.IsNullOrEmpty(name) && uid.HasValue && uid>0)result[address]=new EligibilityCompetition {Address=address,Uid=uid.Value,Name=name};
                }
                catch{ }
            }
            return result.Values.OrderBy(x=>x.Name).ToList();
        }
        bool SaveEligibilityChanges(FmProcess attached,EligibilityReader snapshot,IList<EligibilityEdit> plan,string title)
        {
            if(plan.Count==0){MessageBox.Show("所选状态已经满足，无需重复写入。",title);return false;}
            if(fm!=attached || Safety.Off.ContainsKey("core") || !snapshot.Matches(attached.Read))
                throw new InvalidOperationException("连接或预览数据已变化，请重新打开注册资格窗口。");
            string preview=string.Join("\r\n",plan.Select(w=>w.Label).Distinct().ToArray());
            if(MessageBox.Show(preview+"\r\n\r\n确认应用以上修改？请先保留存档副本，修改后在 FM 中核对。",title,MessageBoxButtons.YesNo)!=DialogResult.Yes)return false;
            Func<bool> guard=()=>fm==attached && !Safety.Off.ContainsKey("core") && snapshot.Matches(attached.Read,plan);
            string error;
            if(!EligibilityTransaction.Apply(attached.Read,attached.Write,plan,guard,out error))throw new InvalidOperationException(error);
            int group=++lastGroup;
            foreach(var w in plan)
            {
                var change=new Change {Addr=w.Address,Old=(byte[])w.Before.Clone(),New=(byte[])w.After.Clone(),What=w.Label,When=DateTime.Now,Group=group,Guard=guard};
                changes.Add(change);lstLog.Items.Add(change);
                try {File.AppendAllText(logPath,change.When.ToString("yyyy-MM-dd HH:mm:ss")+"  "+change.What+"  @"+w.Address.ToString("X")+" "+BitConverter.ToString(w.Before)+" -> "+BitConverter.ToString(w.After)+Environment.NewLine);}catch{ }
            }
            lblStatus.Text="注册资格数据已写入并读回。请在 FM 中重新打开报名界面核对；当前游戏日内、数据未变化时可撤销。";
            LogLine(title+"："+string.Join("；",plan.Select(w=>w.Label).Distinct().ToArray()));
            return true;
        }
        void OpenEligibilityDialog()
        { OpenRegistrationCenter(true); }
    }
}
