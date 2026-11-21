// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        CurrentShirtSnapshot loadedShirt;
        CurrentTicketSnapshot loadedTickets;

        bool CommitCurrent(string feature,IList<EligibilityEdit> plan,Func<bool> identity)
        {
            if(fm==null || !fm.VerifiedCurrentBuild || !Safety.On(feature) || plan.Count==0)return false;
            var attached=fm;
            Func<bool> guard=()=>fm==attached && CurrentBuildProfile.Matches(attached) && CurrentBuildProfile.Supported(attached.Version,attached.ImageSize,attached.Read,attached.Base) && Safety.On(feature) && identity();
            string error;
            if(!EligibilityTransaction.Apply(attached.Read,attached.Write,plan,guard,out error))
            {LogLine("修改停止："+error);MessageBox.Show(error,"数据已变化或写入失败");return false;}
            int group=plan.Count>1?++lastGroup:0;
            foreach(var item in plan)
            {
                var change=new Change{Addr=item.Address,Old=(byte[])item.Before.Clone(),New=(byte[])item.After.Clone(),What=item.Label,When=DateTime.Now,Guard=guard,Group=group};
                changes.Add(change);lstLog.Items.Add(change);
                try{File.AppendAllText(logPath,change.When.ToString("yyyy-MM-dd HH:mm:ss")+"  "+change.What+"  @"+change.Addr.ToString("X")+" "+BitConverter.ToString(change.Old)+" -> "+BitConverter.ToString(change.New)+Environment.NewLine);}catch{}
            }
            return true;
        }
    }
}
