// Modified build notice: FM27E0-CAgrowth v1.1.18; changes dated 2026-11-17.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class ContentTabs : TabControl
    {
        public override Rectangle DisplayRectangle {get{return ClientRectangle;}}
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x1328 && !DesignMode){message.Result=(IntPtr)1;return;}
            base.WndProc(ref message);
        }
    }
    partial class MainForm
    {
        static readonly Color UiInk=Color.FromArgb(31,42,61),UiMuted=Color.FromArgb(101,115,135),UiLine=Color.FromArgb(224,230,238),UiAccent=Color.FromArgb(37,99,210);
        string lastScanReport="尚未连接游戏。先在 FM 中载入存档，再点击「挂接并扫描」。";
        enum FeedbackKind { Information, Success, Partial, ReadOnly, Failed }
        readonly Label feedbackTitle = new Label
        {
            Name = "OperationFeedbackTitle", Text = "操作反馈", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false
        };

        static void StyleButton(Button button,bool primary=false)
        {
            button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=primary?UiAccent:UiLine;
            button.BackColor=primary?UiAccent:Color.White;button.ForeColor=primary?Color.White:UiInk;
            button.Height=32;button.Margin=new Padding(4);button.Padding=new Padding(4,0,4,0);button.UseVisualStyleBackColor=false;
        }
        internal static void StyleWorkspaceButton(Button button,bool primary=false){StyleButton(button,primary);}

        static FlowLayoutPanel WrappingBar(string name)
        {
            return new FlowLayoutPanel
            {
                Name=name, Dock=DockStyle.Top, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink,
                WrapContents=true, Margin=Padding.Empty
            };
        }

        static FlowLayoutPanel FieldGroup(params Control[] fields)
        {
            var group=new FlowLayoutPanel
            {
                AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink,
                WrapContents=false, Margin=new Padding(0,0,8,0)
            };
            EventHandler fitHeight=delegate
            {
                // Native inputs can grow after the flow layout has measured its preferred row height.
                int height=0;
                foreach(var field in fields)
                    height=Math.Max(height,Math.Max(field.Height,field.PreferredSize.Height)+field.Margin.Vertical);
                height+=group.Padding.Vertical;
                if(group.MinimumSize.Height!=height)group.MinimumSize=new Size(group.MinimumSize.Width,height);
            };
            foreach(var field in fields)
            {
                field.Anchor=AnchorStyles.Left;
                group.Controls.Add(field);
                field.SizeChanged+=fitHeight;
                field.FontChanged+=fitHeight;
                field.MarginChanged+=fitHeight;
            }
            group.PaddingChanged+=fitHeight;
            fitHeight(group,EventArgs.Empty);
            return group;
        }

        static Label FieldLabel(string text)
        {
            return new Label { Text=text, AutoSize=true, ForeColor=UiMuted };
        }

        internal static void WrapDockedLabel(Label label)
        {
            label.AutoSize=false;
            label.AutoEllipsis=false;
            EventHandler fit=delegate
            {
                if(label.Width<=label.Padding.Horizontal)return;
                int height=label.GetPreferredSize(new Size(label.Width,0)).Height;
                if(label.Height!=height)label.Height=height;
            };
            label.SizeChanged+=fit;
            label.TextChanged+=fit;
            label.FontChanged+=fit;
        }

        static void FitWindowToWorkArea(Form window,Control owner)
        {
            var area=Screen.FromControl(owner).WorkingArea;
            var available=new Size(Math.Max(1,area.Width-16),Math.Max(1,area.Height-16));
            window.MinimumSize=new Size(Math.Min(window.MinimumSize.Width,available.Width),
                Math.Min(window.MinimumSize.Height,available.Height));
            window.Size=new Size(Math.Min(window.Width,available.Width),Math.Min(window.Height,available.Height));
        }

        int LayoutPixels(int logical)
        {
            return (int)Math.Round(logical*CurrentAutoScaleDimensions.Width/96f);
        }

        static Panel ScrollableForm(Control fields,Control actions)
        {
            var form=new Panel{Dock=DockStyle.Fill};
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
            fields.Dock=DockStyle.Top;
            scroll.Controls.Add(fields);
            actions.Dock=DockStyle.Bottom;
            form.Controls.Add(scroll);
            form.Controls.Add(actions);
            return form;
        }

        Control BuildHeader()
        {
            var header=new Panel {Name="AppHeader",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                BackColor=Color.White,Padding=new Padding(16,8,16,6)};
            var identity=new TableLayoutPanel {Name="AppIdentity",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                ColumnCount=4,RowCount=1,Margin=Padding.Empty};
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,46));identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var logo=new PictureBox {Dock=DockStyle.Fill,MaximumSize=new Size(38,38),SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(0,0,8,4)};
            using(var stream=typeof(MainForm).Assembly.GetManifestResourceStream("FM27E0.logo.png"))
                if(stream!=null)using(var original=Image.FromStream(stream))logo.Image=new Bitmap(original);
            identity.Controls.Add(logo,0,0);
            identity.Controls.Add(new Label {AutoSize=true,Text="FM27E0 修改器  v"+Version,Font=new Font(Font,FontStyle.Bold),ForeColor=UiInk,Anchor=AnchorStyles.Left,Margin=new Padding(0)},1,0);
            btnCalibrate.Text="显示货币…";StyleButton(btnCalibrate);btnCalibrate.AutoSize=true;btnCalibrate.Width=108;
            identity.Controls.Add(btnCalibrate,2,0);
            var names=new Button {Name="NameCatalogButton",Text="姓名库…",Width=108,AutoSize=true};StyleButton(names);
            names.Click+=delegate {ShowNameCatalog();};identity.Controls.Add(names,3,0);
            var connection=new TableLayoutPanel {Name="ConnectionBar",Dock=DockStyle.Top,AutoSize=true,
                AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=3,RowCount=1,Margin=Padding.Empty,Padding=new Padding(0,4,0,0)};
            connection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,162));
            connection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            StyleButton(btnScan,true);btnScan.Name="AttachAndScan";btnScan.AutoSize=true;btnScan.Width=132;btnScan.Margin=new Padding(0,0,12,0);
            progress.Name="ScanProgress";progress.Height=16;progress.Anchor=AnchorStyles.Left|AnchorStyles.Right;
            progress.Margin=new Padding(0,0,12,0);
            lblMoney.Name="CurrencyDisplayHint";lblMoney.AutoSize=true;lblMoney.AutoEllipsis=false;
            lblMoney.Anchor=AnchorStyles.Left|AnchorStyles.Right;lblMoney.TextAlign=ContentAlignment.MiddleLeft;
            lblMoney.ForeColor=UiMuted;lblMoney.Padding=Padding.Empty;lblMoney.Margin=Padding.Empty;
            connection.Controls.Add(btnScan,0,0);connection.Controls.Add(progress,1,0);connection.Controls.Add(lblMoney,2,0);
            header.Controls.Add(connection);header.Controls.Add(identity);return header;
        }

        Control BuildWorkspace(TabControl tabs)
        {
            tabs.Name="WorkspaceTabs";tabs.Appearance=TabAppearance.FlatButtons;tabs.SizeMode=TabSizeMode.Fixed;
            tabs.ItemSize=new Size(1,1);tabs.Multiline=true;
            var area=new Panel {Name="Workspace",Dock=DockStyle.Fill,Padding=new Padding(0,10,12,8),BackColor=Color.FromArgb(244,247,251)};
            var navigation=new FlowLayoutPanel {Name="WorkspaceNavigation",Dock=DockStyle.Left,Width=176,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(10,4,10,8)};
            var buttons=new Dictionary<int,Button>();
            Action<string,int[],string[]> group=(heading,indices,labels)=>
            {
                navigation.Controls.Add(new Label {Text=heading,ForeColor=UiMuted,AutoSize=false,Width=148,Height=28,Padding=new Padding(8,6,0,0),Margin=new Padding(0,6,0,2)});
                for(int i=0;i<indices.Length;i++)
                {
                    int index=indices[i];var button=new Button {Name="Navigate"+index,Text=labels[i],Tag=index,Width=148,Height=36,TextAlign=ContentAlignment.MiddleLeft};
                    StyleButton(button);button.FlatAppearance.BorderSize=0;button.Padding=new Padding(12,0,4,0);button.Margin=new Padding(0,2,0,2);
                    button.Click+=delegate {tabs.SelectedIndex=index;};buttons.Add(index,button);navigation.Controls.Add(button);
                }
            };
            group("人员管理",new[]{0,1,5,6},new[]{"球员资料","职员资料","球员筛选","职员筛选"});
            group("俱乐部与成长",new[]{2,3,4},new[]{"俱乐部管理","球队名单","CA 成长"});
            group("工具与记录",new[]{7,8,9},new[]{"赛事与报名","冻结管理","变更记录"});
            Action highlight=()=>{foreach(var item in buttons){bool selected=item.Key==tabs.SelectedIndex;item.Value.BackColor=selected?UiAccent:Color.Transparent;item.Value.ForeColor=selected?Color.White:UiInk;}};
            tabs.SelectedIndexChanged+=delegate {highlight();};highlight();
            navigation.ClientSizeChanged+=delegate
            {
                int width=Math.Max(1,navigation.ClientSize.Width-navigation.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth);
                foreach(Control item in navigation.Controls)item.Width=width;
            };
            foreach(TabPage page in tabs.TabPages){page.BackColor=Color.White;page.Padding=new Padding(10);}
            area.Controls.Add(tabs);area.Controls.Add(navigation);return area;
        }

        Control BuildStatusBar()
        {
            var bar=new Panel {Name="ScanStatusBar",Dock=DockStyle.Bottom,Height=96,BackColor=Color.FromArgb(234,240,248),Padding=new Padding(14,4,12,6)};
            var heading=new Panel {Dock=DockStyle.Top,Height=30};
            var details=new Button {Name="ScanDetailsButton",Text="连接详情…",Dock=DockStyle.Right,Width=112};StyleButton(details);details.Height=28;
            details.Click+=delegate {ShowScanDetails();};
            feedbackTitle.Font=new Font(Font,FontStyle.Bold);
            heading.Controls.Add(feedbackTitle);heading.Controls.Add(details);
            lblStatus.Dock=DockStyle.Fill;lblStatus.BackColor=Color.White;lblStatus.ForeColor=UiInk;
            lblStatus.TextChanged+=delegate
            {
                SetFeedbackTitle(FeedbackKind.Information);
                lblStatus.Select(0,0);
                if(lblStatus.IsHandleCreated)lblStatus.ScrollToCaret();
            };
            bar.Controls.Add(lblStatus);bar.Controls.Add(heading);return bar;
        }

        void SetFeedbackTitle(FeedbackKind kind)
        {
            string title;
            Color color;
            switch(kind)
            {
                case FeedbackKind.Success: title="成功"; color=Color.FromArgb(25,100,65); break;
                case FeedbackKind.Partial: title="部分成功"; color=Color.FromArgb(122,79,12); break;
                case FeedbackKind.ReadOnly: title="只读拦截"; color=Color.FromArgb(122,79,12); break;
                case FeedbackKind.Failed: title="未完成"; color=Color.FromArgb(156,35,34); break;
                default: title=""; color=UiInk; break;
            }
            feedbackTitle.Text=title.Length==0?"操作反馈":"操作反馈 · "+title;
            feedbackTitle.ForeColor=color;
        }

        static FeedbackKind WriteFeedbackKind(int written,int unsuccessful)
        {
            if(unsuccessful>0)return written>0?FeedbackKind.Partial:FeedbackKind.Failed;
            return written>0?FeedbackKind.Success:FeedbackKind.Information;
        }

        void ShowOperationFeedback(FeedbackKind kind,string text)
        {
            lblStatus.Text=text;
            SetFeedbackTitle(kind);
        }

        void ShowOperationMessage(FeedbackKind kind,string text,string title)
        {
            ShowOperationFeedback(kind,text);
            MessageBox.Show(text,title);
        }

        void ShowScanDetails()
        {
            using(var dialog=new Form {Text="本次连接与诊断信息",Width=780,Height=560,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false})
            {
                var text=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Text=lastScanReport,BackColor=Color.White};
                var copy=new Button {Text="复制本次诊断",Dock=DockStyle.Bottom,Height=36};
                copy.Click+=delegate {try{Clipboard.SetText(lastScanReport);}catch(Exception ex){MessageBox.Show(ex.Message,"无法复制");}};
                dialog.Controls.Add(text);dialog.Controls.Add(copy);dialog.ShowDialog(this);
            }
        }

        internal static ToolStripMenuItem MenuGroup(string title,params Button[] commands)
        {
            var group=new ToolStripMenuItem(title);
            foreach(var command in commands)
            {
                var item=new ToolStripMenuItem(Zh.Z(command.Text)) {Tag=command,Enabled=command.Enabled};
                item.Click+=delegate {if(command.Enabled)command.PerformClick();};group.DropDownItems.Add(item);
            }
            return group;
        }
        internal static void RefreshCommandMenu(ToolStripItemCollection items)
        {foreach(ToolStripItem item in items){var source=item.Tag as Button;if(source!=null)item.Enabled=source.Enabled;var parent=item as ToolStripMenuItem;if(parent!=null)RefreshCommandMenu(parent.DropDownItems);}}

        void BuildPlayerActions(FlowLayoutPanel bar,Button apply,Button reload,Button maximize)
        {
            bar.Name="PlayerActions";bar.Controls.Clear();bar.Padding=new Padding(0,10,0,0);bar.Margin=Padding.Empty;
            btnPlayerAge.Text="年龄 / 生日…";
            foreach(var button in new[]{apply,reload,btnPlayerAge,btnRetirement,btnOptimizeAttributes}){StyleButton(button,button==apply);button.AutoSize=true;bar.Controls.Add(button);}
            btnOptimizeAttributes.Click+=delegate{OpenAttributeOptimizer(loadedPlayer);};
            var more=new Button {Name="MorePlayerActions",Text="更多操作 ▾",AutoSize=true};StyleButton(more);bar.Controls.Add(more);
            var menu=new ContextMenuStrip {Name="PlayerCommandMenu"};
            menu.Items.Add(MenuGroup("资料与合同",btnDetails,btnClauses,btnEligibility));
            btnEligibility.Click+=delegate {OpenEligibilityDialog();};
            menu.Items.Add(MenuGroup("属性与状态",maximize,btnHeal,btnFreeze));
            menu.Items.Add(MenuGroup("球队与人员",btnSwap,btnMove,btnRelease,btnCreate));
            menu.Items.Add(new ToolStripSeparator());
            var raw=new ToolStripMenuItem("显示原始属性数值") {CheckOnClick=true};raw.CheckedChanged+=delegate {gridAttrs.Columns[2].Visible=raw.Checked;};menu.Items.Add(raw);
            menu.Opening+=delegate {RefreshCommandMenu(menu.Items);};more.Click+=delegate {menu.Show(more,new Point(0,more.Height));};
            more.ContextMenuStrip=menu;gridAttrs.Columns[2].Visible=false;gridAttrs.Columns[3].HeaderText="检查状态";
            lblPlayer.Padding=new Padding(6,10,6,8);lblPlayer.Font=new Font(Font,FontStyle.Bold);lblPlayer.ForeColor=UiInk;
            if(string.IsNullOrEmpty(lblPlayer.Text))lblPlayer.Text="在左侧搜索并选择球员，查看资料后再应用修改。";
        }
    }
}
