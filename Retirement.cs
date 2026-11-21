// Modified build notice: FM27E0-CAgrowth v1.1.20; changes dated 2026-11-18.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Fm24Editor
{
    class RetirementWrite
    {
        public ulong Address;
        public byte[] Old, New;
        public string What;
    }
    class RetirementRecord
    {
        public ulong Address;
        public int PersonIndex;
        public byte[] Raw;
        public DateTime Date { get { return RetirementSnapshot.Decode(BitConverter.ToUInt32(Raw,12)); } }
    }
    // Verified on 27.0.1+2494687: manager RTTI, both indexes, and person+0x18 bit 0.
    // The native retirement manager sets/clears that bit; all 33 pending active players
    // have it. Ronaldo retained it after the old editor removed his index entries.
    // Cancellation clears only this bit, preserving other person flags and contract expiry.
    // Other executable builds stay read-only until their layout has been verified.
    class RetirementSnapshot
    {
        public ulong Manager, Vtable, SecondaryVtable, Person;
        public byte[] Identity, AnnouncementFlags, DatesHeader, PeopleHeader, Dates, People;
        public RetirementRecord Record;
        public List<RetirementRecord> Records = new List<RetirementRecord>();
        public bool HasAnnouncement { get { return (AnnouncementFlags[0]&1)!=0; } }
        public static ulong VtableFor(string version) { return version == "27.0.1+2494687" ? 0x4FCFA88UL : 0; }
        public static ulong VtableFor(FmProcess fm)
        {
            return CurrentBuildProfile.Matches(fm)?CurrentBuildProfile.Active.RetirementType:VtableFor(fm.Version);
        }
        public static DateTime Decode(uint packed)
        {
            int year=(int)(packed>>16), day=(int)(packed&0x1FF);
            if(year<1900 || year>2200 || day<1 || day>(DateTime.IsLeapYear(year)?366:365)) throw new InvalidDataException("退役记录包含无效日期。");
            return new DateTime(year,1,1).AddDays(day-1);
        }
        static byte[] Read(Func<ulong,int,byte[]> reader,ulong address,int length)
        { if(length==0)return new byte[0];var b=reader(address,length);if(b==null || b.Length!=length)throw new InvalidDataException("无法完整读取退役数据，请重新扫描。");return b; }
        static byte[] Header(Func<ulong,int,byte[]> reader,ulong address,int stride)
        {
            var h=Read(reader,address,24);ulong b=BitConverter.ToUInt64(h,0),e=BitConverter.ToUInt64(h,8),c=BitConverter.ToUInt64(h,16);
            if((b<0x10000 && (b!=0 || e!=0 || c!=0)) || e<b || c<e || c-b>(ulong)stride*100000 || (e-b)%(ulong)stride!=0 || (c-b)%(ulong)stride!=0)throw new InvalidDataException("退役名单结构与已验证版本不符。");
            return h;
        }
        public static RetirementSnapshot Capture(Func<ulong,int,byte[]> reader,ulong manager,ulong vtable,Player player,int secondaryGap=16)
        {
            if(secondaryGap!=16 && secondaryGap!=24)throw new InvalidDataException("退役管理器的副类型配置无效。");
            var s=new RetirementSnapshot {Manager=manager,Vtable=vtable,SecondaryVtable=vtable+(ulong)secondaryGap,Person=player.Person};
            if(manager<0x10000 || BitConverter.ToUInt64(Read(reader,manager,16),0)!=vtable || BitConverter.ToUInt64(Read(reader,manager+8,8),0)!=s.SecondaryVtable)
                throw new InvalidDataException("退役管理数据已失效或当前版本尚未支持，请重新扫描。");
            s.Identity=Read(reader,player.Person+8,8);
            int index=BitConverter.ToInt32(s.Identity,0);
            if(index<0 || BitConverter.ToInt32(s.Identity,4)!=player.Uid)throw new InvalidDataException("球员身份已经变化，请重新扫描。");
            s.AnnouncementFlags=Read(reader,player.Person+0x18,1);
            s.DatesHeader=Header(reader,manager+0x30,8);s.PeopleHeader=Header(reader,manager+0x90,16);
            int count=(int)((BitConverter.ToUInt64(s.DatesHeader,8)-BitConverter.ToUInt64(s.DatesHeader,0))/8);
            if((BitConverter.ToUInt64(s.PeopleHeader,8)-BitConverter.ToUInt64(s.PeopleHeader,0))/16!=(ulong)count)
                throw new InvalidDataException("两份退役索引的数量不一致，请暂停游戏并重新扫描。");
            s.Dates=Read(reader,BitConverter.ToUInt64(s.DatesHeader,0),count*8);
            s.People=Read(reader,BitConverter.ToUInt64(s.PeopleHeader,0),count*16);
            var byAddress=new Dictionary<ulong,RetirementRecord>();DateTime previous=DateTime.MinValue;
            for(int i=0;i<count;i++)
            {
                ulong address=BitConverter.ToUInt64(s.Dates,i*8);var raw=Read(reader,address,24);
                var record=new RetirementRecord {Address=address,PersonIndex=BitConverter.ToInt32(raw,0),Raw=raw};
                if(byAddress.ContainsKey(address) || record.PersonIndex<0 || raw[22]!=5 || record.Date<previous)
                    throw new InvalidDataException("退役记录或日期排序未通过检查，当前保持只读。");
                Decode(BitConverter.ToUInt32(raw,8));Decode(BitConverter.ToUInt32(raw,16));
                previous=record.Date;byAddress.Add(address,record);s.Records.Add(record);
            }
            var seen=new HashSet<ulong>();int last=-1;
            for(int i=0;i<count;i++)
            {
                int key=BitConverter.ToInt32(s.People,i*16);ulong address=BitConverter.ToUInt64(s.People,i*16+8);RetirementRecord record;
                if(key<=last || !byAddress.TryGetValue(address,out record) || record.PersonIndex!=key || !seen.Add(address))
                    throw new InvalidDataException("球员退役索引未通过检查，当前保持只读。");
                last=key;if(key==index)s.Record=record;
            }
            if(!Read(reader,manager+0x30,24).SequenceEqual(s.DatesHeader) || !Read(reader,manager+0x90,24).SequenceEqual(s.PeopleHeader))
                throw new InvalidDataException("游戏正在更新退役名单，请暂停游戏后重试。");
            return s;
        }
        public bool StillCurrent(Func<ulong,int,byte[]> reader)
        {
            try
            {
                return reader(Manager,8).SequenceEqual(BitConverter.GetBytes(Vtable)) && reader(Manager+8,8).SequenceEqual(BitConverter.GetBytes(SecondaryVtable)) &&
                    reader(Person+8,8).SequenceEqual(Identity) && reader(Person+0x18,1).SequenceEqual(AnnouncementFlags) &&
                    reader(Manager+0x30,24).SequenceEqual(DatesHeader) && reader(Manager+0x90,24).SequenceEqual(PeopleHeader) &&
                    reader(BitConverter.ToUInt64(DatesHeader,0),Dates.Length).SequenceEqual(Dates) &&
                    reader(BitConverter.ToUInt64(PeopleHeader,0),People.Length).SequenceEqual(People) && Records.All(r=>reader(r.Address,24).SequenceEqual(r.Raw));
            }
            catch { return false; }
        }
        public List<RetirementWrite> Plan(DateTime today,DateTime? postponeTo)
        {
            if(Record==null && (postponeTo.HasValue || !HasAnnouncement))throw new InvalidOperationException("这名球员没有可修改的退役计划或残留退役标记。");
            if(Record!=null && Record.Date.Date<=today.Date)throw new InvalidOperationException("只能修改已宣布、尚未生效的退役计划。");
            var writes=new List<RetirementWrite>();
            Action<ulong,byte[],byte[],string> add=(a,o,n,w)=>{if(!o.SequenceEqual(n))writes.Add(new RetirementWrite {Address=a,Old=o,New=n,What=w});};
            ulong dateBegin=BitConverter.ToUInt64(DatesHeader,0), peopleBegin=BitConverter.ToUInt64(PeopleHeader,0);
            if(postponeTo.HasValue)
            {
                DateTime date=postponeTo.Value.Date;
                if(date<=Record.Date || date<=today.Date || date.Year>2200)throw new ArgumentException("新的退役日期必须晚于原计划退役日期，且不超过 2200 年。");
                uint old=BitConverter.ToUInt32(Record.Raw,12), packed=((uint)date.Year<<16)|(old&0xFE00u)|(uint)date.DayOfYear;
                add(Record.Address+12,BitConverter.GetBytes(old),BitConverter.GetBytes(packed),"计划退役日期 "+Record.Date.ToString("yyyy-MM-dd")+" → "+date.ToString("yyyy-MM-dd"));
                var sorted=Records.OrderBy(r=>r.Address==Record.Address?date:r.Date).ToList();var bytes=new byte[Dates.Length];
                for(int i=0;i<sorted.Count;i++)BitConverter.GetBytes(sorted[i].Address).CopyTo(bytes,i*8);
                add(dateBegin,Dates,bytes,"退役日期索引重新排序");
            }
            else if(Record!=null)
            {
                var dates=(byte[])Dates.Clone();int at=Records.FindIndex(r=>r.Address==Record.Address);
                Buffer.BlockCopy(Dates,(at+1)*8,dates,at*8,Dates.Length-(at+1)*8);
                var people=(byte[])People.Clone();int row=-1;for(int i=0;i<People.Length;i+=16)if(BitConverter.ToUInt64(People,i+8)==Record.Address)row=i;
                if(row<0)throw new InvalidDataException("找不到对应的球员退役记录。");
                Buffer.BlockCopy(People,row+16,people,row,People.Length-row-16);
                add(dateBegin,Dates,dates,"移除退役日期索引中的球员");
                add(peopleBegin,People,people,"移除球员退役索引中的球员");
                add(Manager+0x38,DatesHeader.Skip(8).Take(8).ToArray(),BitConverter.GetBytes(BitConverter.ToUInt64(DatesHeader,8)-8),"更新计划退役名单数量");
                add(Manager+0x98,PeopleHeader.Skip(8).Take(8).ToArray(),BitConverter.GetBytes(BitConverter.ToUInt64(PeopleHeader,8)-16),"更新球员退役索引数量");
            }
            if(!postponeTo.HasValue && HasAnnouncement)
                add(Person+0x18,AnnouncementFlags,new[]{(byte)(AnnouncementFlags[0]&0xFE)},"清除已宣布退役标记");
            return writes;
        }
    }
    static class RetirementBatch
    {
        // Validate the complete plan, recheck before each write, read back, and restore on failure.
        public static bool Apply(Func<ulong,int,byte[]> read,Func<ulong,byte[],bool> write,List<RetirementWrite> plan,Func<bool> identity,out string error)
        {
            error=null;var applied=new List<RetirementWrite>();
            try
            {
                if(!identity() || plan.Any(w=>!Equal(read(w.Address,w.Old.Length),w.Old)))throw new InvalidOperationException("游戏数据已经变化，请重新打开退役窗口。");
                foreach(var w in plan)
                {
                    if(!identity() || !Equal(read(w.Address,w.Old.Length),w.Old))throw new InvalidOperationException("写入前数据已被游戏更新。");
                    applied.Add(w);
                    if(!write(w.Address,w.New) || !Equal(read(w.Address,w.New.Length),w.New))throw new IOException("退役修改未能完整写入。");
                }
                return true;
            }
            catch(Exception ex)
            {
                bool restored=true;
                foreach(var w in applied.AsEnumerable().Reverse())
                {
                    var current=read(w.Address,w.New.Length);if(Equal(current,w.Old))continue;
                    if(!identity() || !Equal(current,w.New) || !write(w.Address,w.Old) || !Equal(read(w.Address,w.Old.Length),w.Old))restored=false;
                }
                error=ex.Message+(restored?" 本次修改已取消。":" 部分数据无法恢复，请重新读取测试前的存档。");return false;
            }
        }
        static bool Equal(byte[] a,byte[] b) {return a!=null && b!=null && a.SequenceEqual(b);}
    }
    partial class MainForm
    {
        ulong retirementManager;
        Button btnRetirement = new Button {Text="退役计划…",AutoSize=true};
        RetirementSnapshot ReadRetirementSnapshot(Player player,ulong vtable)
        {
            int gap=16;
            if(CurrentBuildProfile.Matches(fm) && CurrentBuildProfile.RetirementManager(fm.Read,fm.Base,out gap)!=retirementManager)
                throw new InvalidOperationException("退役数据所属存档已变化，请重新挂接并扫描。");
            return RetirementSnapshot.Capture(fm.Read,retirementManager,fm.Base+vtable,player,gap);
        }
        string RetirementStatus(Player player)
        {
            try
            {
                ulong rva=RetirementSnapshot.VtableFor(fm);
                if(rva==0 || retirementManager==0)return "退役计划：当前版本尚未验证，保持只读";
                var s=ReadRetirementSnapshot(player,rva);
                return s.Record==null?(s.HasAnnouncement?"退役计划：记录已移除，仍有退役标记，可点击修复":"退役计划：未发现已宣布的计划"):"已宣布退役："+s.Record.Date.ToString("yyyy-MM-dd");
            }
            catch {return "退役计划：数据未通过检查，保持只读";}
        }
        void OpenRetirementDialog()
        {
            var player=loadedPlayer;if(player==null || fm==null)return;
            try
            {
                ulong rva=RetirementSnapshot.VtableFor(fm);
                if(rva==0)throw new InvalidOperationException("当前构建的退役结构未通过检查。请在连接详情核对版本和适配结果。");
                if(retirementManager==0)throw new InvalidOperationException("尚未读取到有效的退役管理器，请重新挂接并扫描；本次保持只读。");
                var snapshot=ReadRetirementSnapshot(player,rva);
                bool repair=snapshot.Record==null && snapshot.HasAnnouncement;
                if(snapshot.Record==null && !repair) {MessageBox.Show("这名球员当前没有已宣布的退役计划或残留退役标记。","退役计划");return;}
                uint? rawToday=fm.ReadGameDate();
                if(!Safety.On("date") || !rawToday.HasValue)throw new InvalidOperationException("无法安全读取游戏日期，退役计划保持只读。");
                DateTime today=RetirementSnapshot.Decode(rawToday.Value);
                if(snapshot.Record!=null && snapshot.Record.Date<=today)throw new InvalidOperationException("退役日期已到，不能使用此功能让已经退役的球员复出。");
                var dialog=new Form {Name="RetirementPlanDialog",Width=620,Height=330,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false};
                StyleWorkspaceDialog(dialog,repair?"修复退役状态":"调整退役计划");
                var info=new Label {Text=player.DisplayName+"  [UID "+player.Uid+"]\n当前游戏日期："+today.ToString("yyyy-MM-dd")+"\n"+(repair?"计划记录已移除，但球员身上仍保留已宣布退役标记。":"计划退役日期："+snapshot.Record.Date.ToString("yyyy-MM-dd")),Dock=DockStyle.Top,Height=78,Padding=new Padding(4),UseMnemonic=false};
                var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=44,TabIndex=0};
                var date=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",MaxDate=new DateTime(2200,12,31),Width=130};
                var postpone=new Button {Text="推迟至所选日期",AutoSize=true};var cancel=new Button {Text=repair?"修复残留退役状态":"取消退役计划",AutoSize=true};
                if(!repair)
                {
                    if(snapshot.Record.Date<date.MaxDate)
                    {
                        date.MinDate=snapshot.Record.Date.AddDays(1);
                        date.Value=snapshot.Record.Date.Year<2200?snapshot.Record.Date.AddYears(1):date.MaxDate;
                        bar.Controls.Add(date);bar.Controls.Add(postpone);
                    }
                }
                StyleButton(postpone,WorkspaceButtonRole.Primary);
                StyleButton(cancel,bar.Controls.Contains(postpone)?WorkspaceButtonRole.Secondary:WorkspaceButtonRole.Primary);
                bar.Controls.Add(cancel);
                var note=new Label {Text="取消退役会同时移除计划记录并清除已宣布退役标记；推迟退役保留该标记。\n请暂停游戏后修改，完成后重新打开 FM 球员资料核对。\n修改会记录到「变更记录」，可在数据仍有效时撤销。",Dock=DockStyle.Fill,Padding=new Padding(4),UseMnemonic=false,ForeColor=UiMuted};
                Action<bool> apply=delay=>
                {
                    if(CurrentGameDay()!=today)throw new InvalidOperationException("游戏日期已变化，请重新打开退役计划。");
                    string action=delay?"将退役日期推迟至 "+date.Value.ToString("yyyy-MM-dd"):(repair?"修复残留退役状态":"取消已宣布的退役计划");
                    if(MessageBox.Show(dialog,player.DisplayName+"："+action+"？","退役计划",MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
                    if(!snapshot.StillCurrent(fm.Read)) {MessageBox.Show("退役数据已被游戏更新，请关闭窗口重新打开。","退役计划");return;}
                    var plan=snapshot.Plan(today,delay?(DateTime?)date.Value:null);var attached=fm;
                    Func<bool> identity=()=>
                    {
                        try { return fm==attached && !Safety.Off.ContainsKey("core") && CurrentGameDay()==today && (!attached.VerifiedCurrentBuild || CurrentBuildProfile.RetirementManager(attached.Read,attached.Base)==snapshot.Manager) && attached.ReadPtr(snapshot.Manager)==snapshot.Vtable && attached.Read(snapshot.Person+8,8).SequenceEqual(snapshot.Identity); }
                        catch {return false;}
                    };
                    string error;
                    if(!RetirementBatch.Apply(fm.Read,fm.Write,plan,identity,out error)) {LogLine("退役修改失败："+error);MessageBox.Show(error,"退役计划");return;}
                    int group=++lastGroup;
                    foreach(var w in plan)
                    {
                        var change=new Change {Addr=w.Address,Old=(byte[])w.Old.Clone(),New=(byte[])w.New.Clone(),What=player.DisplayName+"："+w.What,When=DateTime.Now,Group=group,Guard=identity};
                        changes.Add(change);lstLog.Items.Add(change);
                        try {File.AppendAllText(logPath,change.When.ToString("yyyy-MM-dd HH:mm:ss")+"  "+change.What+"  @"+w.Address.ToString("X")+" "+BitConverter.ToString(w.Old)+" -> "+BitConverter.ToString(w.New)+Environment.NewLine);}catch{}
                    }
                    lblStatus.Text=player.DisplayName+"：已"+action+"。请在 FM 中核对，保存游戏后会写入存档。";
                    LogLine(lblStatus.Text);dialog.Close();LoadPlayer(player);
                };
                postpone.Click+=delegate {try {apply(true);}catch(Exception ex){MessageBox.Show(ex.Message,"退役计划");}};
                cancel.Click+=delegate {try {apply(false);}catch(Exception ex){MessageBox.Show(ex.Message,"退役计划");}};
                var actions=DialogActions(dialog,DialogDismiss("CloseRetirementPlan","关闭"));
                actions.TabIndex=1;
                dialog.Controls.Add(note);dialog.Controls.Add(bar);dialog.Controls.Add(info);dialog.Controls.Add(actions);
                dialog.ShowDialog(this);
            }
            catch(Exception ex) {MessageBox.Show(ex.Message,"退役计划（只读）");}
        }
    }
}
