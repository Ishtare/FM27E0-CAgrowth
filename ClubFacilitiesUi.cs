// Modified build v1.1.19, 2026-11-17. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
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
        ClubFacilitySnapshot loadedFacilities;
        readonly Label[] facilityNotes=Enumerable.Range(0,5).Select(i=>new Label{Name="FacilityStatus"+i,AutoSize=true,MaximumSize=new Size(300,0),Padding=new Padding(5,5,0,0),ForeColor=UiMuted}).ToArray();
        Control BuildFacilityEditor(int index)
        {
            var row=new FlowLayoutPanel{Name="FacilityEditor"+index,AutoSize=true,WrapContents=false,Margin=Padding.Empty,Dock=DockStyle.Top};
            row.Controls.Add(numFacility[index]);row.Controls.Add(facilityNotes[index]);return row;
        }
        internal void BindFacilityEditors(ClubFacilitySnapshot snapshot,bool globallyWritable,string disabledReason)
        {
            loadedFacilities=snapshot;loadedFacility=new int[5];
            for(int i=0;i<5;i++)
            {
                var field=snapshot==null?null:snapshot.Fields[i];bool valid=field!=null && field.Editable;
                loadedFacility[i]=valid?field.Value:-1;numFacility[i].Visible=valid;numFacility[i].Enabled=valid && globallyWritable;
                numFacility[i].Value=valid?field.Value:1;
                facilityNotes[i].Text=!valid?(field==null?"设施资料尚未读取":field.Reason):globallyWritable?"已核对":disabledReason;
                facilityNotes[i].ForeColor=valid && globallyWritable?UiMuted:Color.FromArgb(150,90,30);
            }
        }
        void LoadFacilities(Club club)
        {
            ClubFacilitySnapshot snapshot=null;
            try{snapshot=new ClubFacilitySnapshot(fm.Read,FacilityLayout.From(fm),club);}catch{}
            string reason=Safety.Off.ContainsKey("core")?"核心数据检查未通过，仅显示":Safety.Off.ContainsKey("facilities")?"设施适配检查未通过，仅显示":"当前项目只读";
            BindFacilityEditors(snapshot,Safety.On("facilities"),reason);
        }
        bool CommitFacilities(IDictionary<int,int> requested,out string error)
        {
            error=null;var attached=fm;var snapshot=loadedFacilities;
            try
            {
                if(attached==null || snapshot==null || loadedClub!=snapshot.Club || !Safety.On("facilities"))throw new InvalidOperationException("设施连接或检查状态已变化，请重新读取俱乐部。");
                var plan=snapshot.Plan(requested);
                Func<bool> guard=()=>fm==attached && Safety.On("facilities") && snapshot.Layout.Same(FacilityLayout.From(attached));
                if(!plan.Apply(attached.Read,attached.Write,guard,out error))return false;
                int group=++lastGroup;
                foreach(var edit in plan.Edits)
                {
                    var change=new Change{Addr=edit.Address,Old=(byte[])edit.Before.Clone(),New=(byte[])edit.After.Clone(),What=edit.Label,When=DateTime.Now,Group=group,Facility=true,
                        Guard=()=>guard() && plan.IdentityMatches(attached.Read)};
                    changes.Add(change);lstLog.Items.Add(change);
                    try{File.AppendAllText(logPath,change.When.ToString("yyyy-MM-dd HH:mm:ss")+"  "+change.What+"  @"+change.Addr.ToString("X")+" "+BitConverter.ToString(change.Old)+" -> "+BitConverter.ToString(change.New)+Environment.NewLine);}catch{}
                }
                return true;
            }
            catch(Exception ex){error=ex.Message;return false;}
        }
    }
}
