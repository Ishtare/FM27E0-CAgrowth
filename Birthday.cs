// Modified build v1.1.22, 2026-11-19. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Fm24Editor
{
    static class Birthdays
    {
        public static DateTime Decode(uint packed)
        {
            int year=(int)(packed>>16),day=(int)(packed&0x1FF);
            if(year<1901 || year>2200 || day<1 || day>(DateTime.IsLeapYear(year)?366:365))throw new InvalidDataException("出生日期无效，当前保持只读。");
            return new DateTime(year,1,1).AddDays(day-1);
        }
        public static int AgeOn(DateTime born,DateTime today)
        {
            int age=today.Year-born.Year;
            return born.AddYears(age).Date>today.Date?age-1:age;
        }
        public static DateTime WithAge(DateTime born,DateTime today,int age)
        {
            if(age<1 || age>120)throw new ArgumentOutOfRangeException("age","年龄范围为 1-120 岁。");
            int year=today.Year-age;
            var candidate=new DateTime(year,born.Month,Math.Min(born.Day,DateTime.DaysInMonth(year,born.Month)));
            if(AgeOn(candidate,today)==age)return candidate;
            var previous=new DateTime(year-1,born.Month,Math.Min(born.Day,DateTime.DaysInMonth(year-1,born.Month)));
            if(AgeOn(previous,today)==age)return previous;
            // Feb 29 can straddle two different leap-year birthday conventions.
            // Keep the requested age exact; the UI previews the resulting date.
            while(AgeOn(candidate,today)<age)candidate=candidate.AddDays(-1);
            while(AgeOn(candidate,today)>age)candidate=candidate.AddDays(1);
            return candidate;
        }
        public static uint Pack(uint old,DateTime born,DateTime today)
        {
            int age=AgeOn(born,today);
            if(born.Year<1901 || born.Date>today.Date || age<1 || age>120)throw new ArgumentException("出生日期应对应 1-120 岁，且不能晚于当前游戏日期。");
            return ((uint)born.Year<<16)|(old&0xFE00u)|(uint)born.DayOfYear;
        }
    }
    partial class MainForm
    {
        Button btnPlayerAge=new Button {Text="年龄／出生日期…",AutoSize=true};
        Button btnStaffAge=new Button {Text="年龄／出生日期…",AutoSize=true};
        DateTime CurrentGameDay()
        {
            if(fm==null || !Safety.On("date"))throw new InvalidOperationException("当前游戏日期未通过检查，年龄修改保持只读。请先挂接并扫描。");
            var raw=fm.ReadGameDate();if(!raw.HasValue)throw new InvalidOperationException("无法读取当前游戏日期。");
            return RetirementSnapshot.Decode(raw.Value);
        }
        string DisplayAge(DateTime? birthday)
        {
            if(!birthday.HasValue)return "年龄未知";
            try{return Birthdays.AgeOn(birthday.Value,CurrentGameDay())+" 岁";}catch{return "年龄未知";}
        }
        void OpenBirthdayDialog(ulong person,string displayName)
        {
            if(fm==null || person==0) {MessageBox.Show("请先选择球员或职员。","年龄／出生日期");return;}
            try
            {
                if(!Safety.On("core"))throw new InvalidOperationException("球员资料未通过检查，出生日期保持只读。");
                var attached=fm;DateTime today=CurrentGameDay();ulong address=person+FmLayout.BirthDate;
                var old=attached.Read(address,4);var identity=attached.Read(person,0x18);
                if(old==null || identity==null)throw new InvalidOperationException("无法读取这名球员或职员，请重新扫描。");
                uint oldPacked=BitConverter.ToUInt32(old,0);DateTime original=Birthdays.Decode(oldPacked);int originalAge=Birthdays.AgeOn(original,today);
                if(originalAge<1 || originalAge>120)throw new InvalidOperationException("这名球员或职员的年龄不在已支持范围内，当前保持只读。");
                var dialog=new Form {Text="修改年龄／出生日期",Width=575,Height=310,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
                var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(14),RowCount=6};
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                var name=new Label {Text=displayName,AutoSize=true,MaximumSize=new System.Drawing.Size(505,0),UseMnemonic=false};layout.Controls.Add(name,0,0);layout.SetColumnSpan(name,2);
                layout.Controls.Add(new Label {Text="当前游戏日期",AutoSize=true},0,1);layout.Controls.Add(new Label {Text=today.ToString("yyyy-MM-dd"),AutoSize=true},1,1);
                var age=new NumericUpDown {Minimum=1,Maximum=120,Value=originalAge,Width=95};
                var date=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",Width=155,MinDate=new DateTime(Math.Max(1901,today.Year-121),1,1),MaxDate=today.AddYears(-1),Value=original};
                layout.Controls.Add(new Label {Text="年龄（岁）",AutoSize=true},0,2);layout.Controls.Add(age,1,2);
                layout.Controls.Add(new Label {Text="出生日期",AutoSize=true},0,3);layout.Controls.Add(date,1,3);
                var preview=new Label {AutoSize=true,MaximumSize=new System.Drawing.Size(505,0),UseMnemonic=false};layout.Controls.Add(preview,0,4);layout.SetColumnSpan(preview,2);
                var note=new Label {Text="调整年龄时尽量保留原生日月、日；目标年份没有 2 月 29 日时使用 2 月 28 日。修改可从「变更记录」撤销。",AutoSize=true,MaximumSize=new System.Drawing.Size(505,0),UseMnemonic=false};layout.Controls.Add(note,0,5);layout.SetColumnSpan(note,2);
                var apply=new Button {Text="应用年龄修改",Dock=DockStyle.Bottom,Height=34};bool syncing=false;
                Action update=()=>{int value=Birthdays.AgeOn(date.Value,today);preview.Text="出生日期："+original.ToString("yyyy-MM-dd")+" → "+date.Value.ToString("yyyy-MM-dd")+"；年龄："+originalAge+" → "+value+" 岁";apply.Enabled=value>=1 && value<=120 && date.Value.Date!=original.Date;};
                age.ValueChanged+=delegate {if(syncing)return;syncing=true;date.Value=Birthdays.WithAge(original,today,(int)age.Value);syncing=false;update();};
                date.ValueChanged+=delegate {if(syncing)return;syncing=true;int value=Birthdays.AgeOn(date.Value,today);if(value>=1 && value<=120)age.Value=value;syncing=false;update();};
                apply.Click+=delegate
                {
                    try
                    {
                        if(CurrentGameDay()!=today)throw new InvalidOperationException("游戏日期已经变化，请重新打开窗口，按当前日期换算年龄。");
                        uint packed=Birthdays.Pack(oldPacked,date.Value,today);var data=BitConverter.GetBytes(packed);
                        string what="出生日期 "+original.ToString("yyyy-MM-dd")+" → "+date.Value.ToString("yyyy-MM-dd")+"（"+originalAge+" → "+Birthdays.AgeOn(date.Value,today)+" 岁）";
                        if(MessageBox.Show(displayName+"：\n"+what+"？","修改年龄",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
                        Func<bool> guard=()=>{try{return fm==attached && Safety.On("core") && attached.Read(person,0x18).SequenceEqual(identity);}catch{return false;}};
                        var write=new RetirementWrite {Address=address,Old=old,New=data,What=what};string error;
                        Func<bool> writeGuard=()=>{try{return guard() && CurrentGameDay()==today;}catch{return false;}};
                        if(!RetirementBatch.Apply(attached.Read,attached.Write,new System.Collections.Generic.List<RetirementWrite>{write},writeGuard,out error))throw new InvalidOperationException(error);
                        var change=new Change {Addr=address,Old=(byte[])old.Clone(),New=(byte[])data.Clone(),What=displayName+"："+what,When=DateTime.Now,Guard=guard};
                        changes.Add(change);lstLog.Items.Add(change);
                        try { File.AppendAllText(logPath,change.When.ToString("yyyy-MM-dd HH:mm:ss")+"  "+change.What+"  @"+address.ToString("X")+" "+BitConverter.ToString(old)+" -> "+BitConverter.ToString(data)+Environment.NewLine); } catch { }
                        personDetailsRevision++;
                        foreach(var player in players.Where(p=>p.Person==person)) {player.Born=date.Value.Date;PlayerNames.RememberBirthday(player,AppDomain.CurrentDomain.BaseDirectory);}
                        foreach(var staffMember in staff.Where(p=>p.Pers==person))staffMember.Born=date.Value.Date;
                        lblStatus.Text=displayName+"：年龄已修改，按游戏日期计算为 "+Birthdays.AgeOn(date.Value,today)+" 岁。请重新打开游戏内资料查看。";
                        dialog.Close();lstPlayers.Refresh();lstStaff.Refresh();
                        if(loadedPlayer!=null && loadedPlayer.Person==person)LoadPlayer(loadedPlayer);
                        if(loadedStaff!=null && loadedStaff.Pers==person)LoadStaff(loadedStaff);
                    }
                    catch(Exception ex){MessageBox.Show(ex.Message,"年龄修改");}
                };
                dialog.Controls.Add(layout);dialog.Controls.Add(apply);update();dialog.ShowDialog(this);
            }
            catch(Exception ex){MessageBox.Show(ex.Message,"年龄／出生日期（只读）");}
        }
    }
}
