// Modified build v1.1.26. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class EarlySaveRestoreDialog : Form
    {
        readonly Player player;
        readonly GrowthStep frame;
        readonly Button readDatabase=new Button{Name="ReadDefaultDatabase",Text="读取游戏数据库默认值…",Width=190};
        readonly Button choose=new Button{Name="ChooseEarlySave",Text="选择开档或早期 .fm…",Width=175};
        readonly Button apply=new Button{Name="ApplyEarlySaveRestore",Text="应用恢复",Width=125,Enabled=false};
        readonly CheckBox restoreCa=new CheckBox{Name="RestoreOriginalCA",Text="同时恢复来源 CA",AutoSize=true,Enabled=false};
        readonly CheckBox extra=new CheckBox{Name="RestoreHiddenAndFeet",Text="同时恢复惯用脚与隐藏属性",AutoSize=true,Enabled=false};
        readonly Label status=new Label{Name="EarlySaveStatus",Dock=DockStyle.Top,Height=67,Padding=new Padding(10,5,10,0)};
        readonly Label budget=new Label{Name="EarlySaveComparison",Dock=DockStyle.Bottom,Height=65,Padding=new Padding(10,4,10,0)};
        readonly DataGridView grid=new WorkspaceGrid{Name="EarlySaveAttributes",Dock=DockStyle.Fill,ReadOnly=true,
            RowHeadersVisible=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
        CancellationTokenSource reading;
        AttributeRestoreSource source;
        bool failed;
        internal event Func<AttributeRestoreSource,bool,bool,bool> ApplyRequested;
        public EarlySaveRestoreDialog(Player player,GrowthStep frame,string engineFilePath=null)
        {
            this.player=player; this.frame=frame; Text="恢复球员默认／开档属性";
            Font=new Font("Microsoft YaHei UI",9.25F); Size=new Size(800,610); MinimumSize=new Size(710,490);
            StartPosition=FormStartPosition.CenterParent;
            var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=44,Padding=new Padding(6)};
            MainForm.StyleWorkspaceButton(readDatabase);bar.Controls.Add(readDatabase);
            MainForm.StyleWorkspaceButton(choose);bar.Controls.Add(choose);
            var options=new FlowLayoutPanel{Dock=DockStyle.Top,Height=30,Padding=new Padding(6,1,6,0)};
            options.Controls.Add(restoreCa);options.Controls.Add(extra);
            var close=new Button{Text="关闭",Width=90,DialogResult=DialogResult.Cancel};
            var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=46,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(6)};
            MainForm.StyleWorkspaceButton(close);MainForm.StyleWorkspaceButton(apply,true);
            footer.Controls.Add(close);footer.Controls.Add(apply);CancelButton=close;
            foreach(string title in new[]{"属性","当前","来源值","恢复后"}) grid.Columns.Add(title,title);
            MainForm.StyleWorkspaceGrid(grid,Font);
            Controls.Add(grid);Controls.Add(budget);Controls.Add(status);Controls.Add(options);Controls.Add(bar);Controls.Add(footer);
            status.Text=player.DisplayName+"  [UID "+player.Uid+"]\n"+
                "游戏数据库提供原版默认值。要恢复本生涯开档时的实际值，请选当时另存的 .fm。";
            readDatabase.Click+=async delegate
            {
                string[] installed=DefaultPlayerDatabase.InstalledSources(engineFilePath);
                string path=installed.Length==1?installed[0]:null;
                if(path==null)
                {
                    using(var file=new OpenFileDialog
                    {
                        Title=installed.Length>1?"选择开档采用的游戏数据库版本":"从 FM27 安装目录选择 people_db.dat",
                        Filter="FM27 球员数据库 (people_db.dat)|people_db.dat",CheckFileExists=true,RestoreDirectory=true,
                        InitialDirectory=installed.Length>0?Path.GetDirectoryName(installed[0]):null,
                        FileName="people_db.dat"
                    })
                    {
                        if(file.ShowDialog(this)!=DialogResult.OK) return;
                        path=file.FileName;
                    }
                }
                await LoadSource(token=>DefaultPlayerDatabase.Read(path,player,token),"正在读取本机游戏数据库的球员默认值…");
            };
            choose.Click+=async delegate
            {
                using(var file=new OpenFileDialog{Title="选择开档或早期存档",Filter="FM 游戏存档 (*.fm)|*.fm",CheckFileExists=true,RestoreDirectory=true})
                {
                    if(file.ShowDialog(this)!=DialogResult.OK) return;
                    string path=file.FileName;
                    await LoadSource(token=>EarlySavePlayer.Read(path,player,frame.Day,token),
                        "正在读取 "+Path.GetFileName(path)+" 中的球员资料…");
                }
            };
            restoreCa.CheckedChanged+=delegate{RefreshPreview();}; extra.CheckedChanged+=delegate{RefreshPreview();};
            apply.Click+=delegate
            {
                if(!apply.Enabled || source==null || ApplyRequested==null) return;
                try { if(ApplyRequested(source,restoreCa.Checked,extra.Checked)) { DialogResult=DialogResult.OK; Close(); } }
                catch(Exception ex){failed=true; status.Text=ex.Message; apply.Enabled=false;}
            };
            FormClosed+=delegate{if(reading!=null) reading.Cancel();};
        }
        async Task LoadSource(Func<CancellationToken,AttributeRestoreSource> read,string message)
        {
            if(reading!=null) return;
            choose.Enabled=readDatabase.Enabled=false; source=null; RefreshPreview();
            restoreCa.Enabled=extra.Enabled=false; restoreCa.Checked=false; extra.Checked=false;
            status.Text=message;
            var request=new CancellationTokenSource(); reading=request;
            try
            {
                var record=await Task.Run(()=>read(request.Token));
                if(!IsDisposed && !request.IsCancellationRequested) Bind(record);
            }
            catch(OperationCanceledException) { }
            catch(Exception ex) { if(!IsDisposed) status.Text=ex is DllNotFoundException?
                "未找到存档解压组件，请完整解压下载包并保留 third-party 文件夹。":ex.Message; }
            finally { reading=null;request.Dispose();if(!IsDisposed) choose.Enabled=readDatabase.Enabled=true; }
        }
        internal void Bind(AttributeRestoreSource record)
        {
            source=record;
            restoreCa.Checked=false;extra.Checked=false;
            restoreCa.Enabled=record.CanRestoreCa(frame.Cap);
            extra.Enabled=true;
            grid.Columns[2].HeaderText=record.FromDatabase?"数据库默认值":"所选存档值";
            status.Text=record.Description+"\n"+record.Name+"   ·   UID "+record.Uid+
                "   ·   来源生日 "+record.Born.ToString("yyyy-MM-dd")+"   ·   来源 CA "+(record.Ca.HasValue?record.Ca.Value.ToString():"开档生成");
            if(string.Equals(record.SourceName,"last save overwrite backup.fm",StringComparison.OrdinalIgnoreCase))
                status.Text+="\n这是 FM 的覆盖备份，预览对应上面所列的保存日期。";
            RefreshPreview();
        }
        void RefreshPreview()
        {
            grid.Rows.Clear();apply.Enabled=false;budget.Text="";
            if(source==null) return;
            byte[] next=frame.RestoredAttributes(source,extra.Checked);
            int changes=0;
            for(int i=0;i<next.Length;i++)
            {
                if(frame.Attributes[i]==source.Attributes[i]) continue;
                if(next[i]!=frame.Attributes[i]) changes++;
                grid.Rows.Add(Zh.Attr(FmLayout.Attrs[i]),(frame.Attributes[i]/5.0).ToString("0.0"),
                    source.Attributes[i]==0?"开档生成":(source.Attributes[i]/5.0).ToString("0.0"),(next[i]/5.0).ToString("0.0"));
            }
            int ca=restoreCa.Checked?source.Ca.GetValueOrDefault(frame.Ca):frame.Ca;
            budget.Text="属性变化 "+changes+" 项    CA "+frame.Ca+" → "+ca+"。";
            if(source.FromDatabase && source.Attributes.Contains((byte)0))
                budget.Text+="“开档生成”的项目保留当前值。";
            if(frame.Positions.Any(v=>v>=2))
            {
                var model=new PositionAbility(frame.Positions);
                budget.Text+="\n按当前实际位置估算，恢复前 "+model.Estimate(frame.Attributes)+"，恢复后 "+model.Estimate(next)+"。";
            }
            if(source.Ca>frame.Cap) budget.Text+="  来源 CA 超过当前 PA。";
            apply.Enabled=!failed && (changes!=0 || ca!=frame.Ca);
        }
    }
    partial class MainForm
    {
        bool OpenEarlySaveRestore(Form owner,Player player,GrowthStep frame)
        {
            using(var dialog=new EarlySaveRestoreDialog(player,frame,fm.EngineFilePath))
            {
                dialog.ApplyRequested+=(source,ca,extra)=>ApplyReviewedAttributes(player,frame,"恢复球员属性",
                    "来源 "+source.Description+"\n"+
                    "CA "+frame.Ca+" → "+(ca?source.Ca.Value:frame.Ca)+"，按预览恢复所列属性。",
                    ()=>frame.PlanRestore(source,ca,extra,player.Uid));
                return dialog.ShowDialog(owner)==DialogResult.OK;
            }
        }
    }
}
