// Modified build v1.1.27. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    // An in-memory editor. Its events change preferences only; it never reads or writes FM.
    sealed class GrowthEditorPanel : UserControl
    {
        sealed class AttributeItem
        {
            public int Index;
            public override string ToString() { return Zh.Attr(FmLayout.Attrs[Index]); }
        }
        readonly Label playerName = new Label { Dock=DockStyle.Fill, AutoEllipsis=true, TextAlign=ContentAlignment.MiddleLeft };
        readonly Label selection = new Label { Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleLeft };
        readonly Label help = new Label { Dock=DockStyle.Fill, ForeColor=MainForm.UiMuted, UseMnemonic=false };
        internal readonly ComboBox Mode = new ComboBox { Name="GrowthFocusMode", Dock=DockStyle.Fill, DropDownStyle=ComboBoxStyle.DropDownList };
        readonly DualRoleSelector roles = new DualRoleSelector("Growth");
        internal ComboBox Direction {get{return roles.InRole;}}
        internal ComboBox PositionGroup {get{return roles.InPosition;}}
        internal readonly TabControl AttributeTabs = new TabControl { Name="GrowthAttributeTabs", Dock=DockStyle.Fill };
        internal readonly Button Speed = new Button { Name="GrowthSpeedPreset", Text="只练爆发力和速度", Dock=DockStyle.Fill };
        internal readonly Button ClearAttributes = new Button { Name="GrowthClearAttributes", Text="清空", Dock=DockStyle.Fill };
        internal readonly Button Watch = new Button { Name="GrowthWatchPlayer", Text="勾选该球员", Dock=DockStyle.Fill };
        internal readonly Button ApplyChecked = new Button { Name="GrowthApplyChecked", Text="将此设置应用到已勾选球员", Dock=DockStyle.Fill };
        internal readonly Button Preview = new Button { Name="GrowthPreviewButton", Text="成长预览…", Dock=DockStyle.Fill };
        internal readonly CheckBox LowSupport = new CheckBox {Name="GrowthLowSupport",Text="低值属性补强",AutoSize=true};
        internal readonly CheckBox LimitUnselected = new CheckBox
            {Name="GrowthLimitUnselected",Text="限制未勾选属性上涨",AutoSize=true,Margin=new Padding(0,4,0,0)};
        internal readonly Button ConfigureSupport = new Button {Name="GrowthSupportSettings",Text="补强设置…",Width=100};
        readonly CheckedListBox[] lists = new CheckedListBox[4];
        readonly TableLayoutPanel body;
        GrowthOptions options = new GrowthOptions();
        byte[] positions;
        byte[] currentAttributes;
        int currentCa;
        int? currentAge;
        int uid;
        bool binding, running, watched;
        public event Action<int,GrowthOptions> OptionsChanged;
        public event Action<int> WatchRequested;
        public event Action<int,GrowthOptions> ApplyCheckedRequested;
        public event Action<int,GrowthOptions> PreviewRequested;
        public GrowthOptions Value { get { return options.Clone(); } }

        public GrowthEditorPanel()
        {
            Name="GrowthEditorPanel";BackColor=MainForm.UiInset;AutoScroll=true;Padding=new Padding(10);
            body=new TableLayoutPanel { Dock=DockStyle.Top, Height=540, ColumnCount=1, RowCount=8, Padding=new Padding(2), BackColor=BackColor };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float height in new[]{24F,38F,96F,34F,26F})body.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,50));body.RowStyles.Add(new RowStyle(SizeType.Absolute,76));
            body.Controls.Add(new Label {Text="球员培养设置",Font=new Font(Font,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
            playerName.Font=new Font(Font,FontStyle.Bold);body.Controls.Add(playerName,0,1);
            var selectors=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            selectors.RowStyles.Add(new RowStyle(SizeType.Absolute,32));selectors.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            selectors.Controls.Add(new Label {Text="模式",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);selectors.Controls.Add(Mode,1,0);
            selectors.Controls.Add(roles,0,1);selectors.SetColumnSpan(roles,2);body.Controls.Add(selectors,0,2);body.RowStyles[2].Height=180;
            Mode.Items.AddRange(new object[]{"保留特点（按角色成长）","自选属性成长","仅提升 CA（游戏分配）"});
            selection.AutoEllipsis=true;
            var quick=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
            quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));quick.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,58));quick.Controls.Add(Speed,0,0);quick.Controls.Add(ClearAttributes,1,0);body.Controls.Add(quick,0,3);
            body.Controls.Add(selection,0,4);
            foreach(string category in new[]{"技术","精神","身体","门将"})
            {
                int i=AttributeTabs.TabPages.Count;var page=new TabPage(category){Padding=new Padding(2)};
                var list=lists[i]=new CheckedListBox {Name="GrowthAttributes"+i,Dock=DockStyle.Fill,CheckOnClick=true,BorderStyle=BorderStyle.None,IntegralHeight=false,BackColor=Color.White};
                list.ItemCheck+=(sender,args)=>OnAttributeCheck((CheckedListBox)sender,args);page.Controls.Add(list);AttributeTabs.TabPages.Add(page);
            }
            body.Controls.Add(AttributeTabs,0,5);
            body.RowStyles[6].Height=126;
            var supportArea=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};
            supportArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            supportArea.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
            supportArea.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            supportArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            supportArea.Controls.Add(LimitUnselected,0,0);
            var supportBar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            LowSupport.Margin=new Padding(0,7,4,0);supportBar.Controls.Add(LowSupport);supportBar.Controls.Add(ConfigureSupport);
            supportArea.Controls.Add(supportBar,0,1);supportArea.Controls.Add(help,0,2);body.Controls.Add(supportArea,0,6);
            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            actions.Controls.Add(Watch,0,0);actions.Controls.Add(Preview,1,0);actions.Controls.Add(ApplyChecked,0,1);actions.SetColumnSpan(ApplyChecked,2);body.Controls.Add(actions,0,7);
            MainForm.StyleButton(Watch,WorkspaceButtonRole.Primary);
            foreach(var button in new[]{Speed,ClearAttributes,ApplyChecked,Preview,ConfigureSupport})MainForm.StyleButton(button);
            Controls.Add(body);
            Resize+=delegate{FitEditorHeight();};
            FontChanged+=delegate
            {
                playerName.Font=new Font(Font,FontStyle.Bold);
                body.GetControlFromPosition(0,0).Font=new Font(Font,FontStyle.Bold);
                FitEditorHeight();
            };
            FitEditorHeight();
            Mode.SelectedIndexChanged+=delegate
            {
                if(binding || uid==0 || running)return;
                options.Mode=(GrowthMode)Mode.SelectedIndex;
                Rebuild();Emit();
            };
            roles.Changed+=(inside,outside,weight)=>{if(binding || uid==0 || running)return;options.Profile=inside;options.DefendingProfile=outside;options.PossessionWeight=weight;Rebuild();Emit();};
            Speed.Click+=delegate {if(uid==0 || running)return;options.Mode=GrowthMode.SelectedAttributes;options.Attributes=new[]{34,38};Rebuild();AttributeTabs.SelectedIndex=2;Emit();};
            ClearAttributes.Click+=delegate {if(uid==0 || running)return;options.Mode=GrowthMode.SelectedAttributes;options.Attributes=new int[0];Rebuild();Emit();};
            Watch.Click+=delegate {if(uid>0 && !running && WatchRequested!=null)WatchRequested(uid);};
            ApplyChecked.Click+=delegate {if(uid>0 && !running && ApplyCheckedRequested!=null)ApplyCheckedRequested(uid,options.Clone());};
            Preview.Click+=delegate {if(uid>0 && PreviewRequested!=null)PreviewRequested(uid,options.Clone());};
            LimitUnselected.CheckedChanged+=delegate
            {
                if(binding || uid==0 || running || options.Mode!=GrowthMode.SelectedAttributes)return;
                options.LimitUnselected=LimitUnselected.Checked;
                RefreshSummary();Emit();
            };
            LowSupport.CheckedChanged+=delegate
            {
                if(binding || uid==0 || running)return;options.Support.Enabled=LowSupport.Checked;
                if(options.Support.Enabled && currentAttributes!=null)
                    foreach(var item in GrowthSupportPlanner.Suggest(Context(),currentAttributes,currentCa,currentAge,options.Support,options.SupportScope))
                        if(item.Included)options.Support.Targets[item.Index]=item.Target;
                RefreshSummary();Emit();
            };
            ConfigureSupport.Click+=delegate
            {
                if(uid==0 || running || currentAttributes==null)return;
                using(var dialog=new GrowthSupportDialog(options.Support,Context(),currentAttributes,currentCa,currentAge,options.SupportScope))
                    if(dialog.ShowDialog(FindForm())==DialogResult.OK){options.Support=dialog.ResultOptions;Rebuild();Emit();}
            };
            Bind(0,null,new GrowthOptions(),null,false,false);
        }
        GrowthProfile Context()
        {
            try{return options.ResolveProfile(positions);}catch{return options.Profile>=0 && options.Profile<GrowthProfile.All.Length?GrowthProfile.All[options.Profile]:GrowthProfile.All[8];}
        }
        static int Category(int index)
        {
            if(new[]{11,12,13,14,15,16,19,21,32,33}.Contains(index))return 3;
            if(new[]{34,36,37,38,39,42,46,50}.Contains(index))return 2;
            if(new[]{17,18,20,26,28,29,40,43,45,51,52,53}.Contains(index))return 1;
            return 0;
        }
        void Emit(){if(OptionsChanged!=null)OptionsChanged(uid,options.Clone());}
        void FitEditorHeight()
        {
            int fixedRows=(int)Math.Ceiling(body.RowStyles.Cast<RowStyle>().Where(row=>row.SizeType==SizeType.Absolute).Sum(row=>row.Height));
            int minimum=fixedRows+Font.Height*12+body.Padding.Vertical;
            int height=Math.Max(minimum,ClientSize.Height-Padding.Vertical);
            if (body.Height!=height) body.Height=height;
        }

        void OnAttributeCheck(CheckedListBox source,ItemCheckEventArgs args)
        {
            if(binding || uid==0 || running || !options.UsesAttributes)return;
            var chosen=new List<int>();
            foreach(var list in lists)for(int i=0;i<list.Items.Count;i++)
                if(list==source && i==args.Index?args.NewValue==CheckState.Checked:list.GetItemChecked(i))chosen.Add(((AttributeItem)list.Items[i]).Index);
            options.Attributes=chosen.Distinct().OrderBy(i=>i).ToArray();options.Mode=GrowthMode.SelectedAttributes;
            binding=true;
            try { Mode.SelectedIndex=(int)GrowthMode.SelectedAttributes;UpdateLimitControl(); }
            finally { binding=false; }
            RefreshSummary();Emit();
        }
        public void Bind(int playerUid,string name,GrowthOptions value,byte[] currentPositions,bool isWatched,bool isRunning,byte[] attributes=null,int ca=0,int? age=null)
        {
            uid=playerUid;options=(value??new GrowthOptions()).Clone();positions=currentPositions==null?null:(byte[])currentPositions.Clone();running=isRunning;watched=isWatched;
            currentAttributes=attributes==null?null:(byte[])attributes.Clone();currentCa=ca;currentAge=age;
            playerName.Text=uid==0?"从名单中选择一名球员":name;Rebuild();
        }
        void Rebuild()
        {
            binding=true;
            try
            {
                Mode.SelectedIndex=(int)options.Mode;
                var profile=Context();bool active=uid>0;roles.Bind(options,positions,active && !running && options.UsesAttributes);
                var checkedValues=options.Mode==GrowthMode.SelectedAttributes?options.Attributes:profile.AutomaticTargets.ToArray();
                foreach(var list in lists)list.Items.Clear();
                for(int i=0;i<54;i++)if(GrowthProfile.Developable(i) && profile.Cost(i)>0)lists[Category(i)].Items.Add(new AttributeItem{Index=i},checkedValues.Contains(i));
                Mode.Enabled=active && !running;
                AttributeTabs.Enabled=active && options.UsesAttributes;
                foreach(var list in lists)list.Enabled=active && !running && options.UsesAttributes;
                AttributeTabs.TabPages[3].Enabled=lists[3].Items.Count>0;
                Speed.Enabled=ClearAttributes.Enabled=Watch.Enabled=ApplyChecked.Enabled=active && !running;Preview.Enabled=active;
                LowSupport.Checked=options.Support.Enabled;LowSupport.Enabled=ConfigureSupport.Enabled=active && !running && options.UsesAttributes && currentAttributes!=null && currentCa>=1;
                UpdateLimitControl();
                Watch.Text=watched?"取消该球员培养":"勾选该球员";
                RefreshSummary();
            }
            finally{binding=false;}
        }
        void UpdateLimitControl()
        {
            LimitUnselected.Enabled=uid>0 && !running && options.Mode==GrowthMode.SelectedAttributes;
            LimitUnselected.Checked=options.LimitsUnselected;
        }
        void RefreshSummary()
        {
            var profile=Context();
            selection.Text=options.Mode==GrowthMode.CaOnly?"属性由游戏训练处理":options.Mode==GrowthMode.SelectedAttributes?"已选 "+options.Attributes.Length+" 项属性":options.DefendingProfile==-2?profile.DisplayName:"有球 "+options.PossessionWeight+"% / 无球 "+(100-options.PossessionWeight)+"% 综合成长";
            bool invalid=options.Mode==GrowthMode.SelectedAttributes && (options.Attributes.Length==0 || options.Attributes.Any(i=>!GrowthProfile.Developable(i) || profile.Cost(i)<=0));
            selection.ForeColor=invalid?MainForm.UiErrorInk:MainForm.UiInk;
            if(uid==0)help.Text="连接后选择球员，即可单独设置培养方式。";
            else if(running)help.Text="成长运行中，停止后可调整设置。预览可随时查看。";
            else if(invalid)help.Text="请勾选至少一项可用属性，再开始成长。";
            else if(options.LimitsUnselected)help.Text="上限取本轮开始时的数值，允许自然下滑。低值补强限于已勾选项。";
            else if(options.UsesAttributes && options.Support.Enabled)help.Text="额外补强已启用，最多使用新增预算的 "+options.Support.BudgetPercent+"%。可在设置与预览中核对目标。";
            else if(options.Mode==GrowthMode.SelectedAttributes)help.Text="仅同步勾选属性。游戏自身训练仍可能改变其他属性。";
            else help.Text="默认保留定位球属性。如需培养定位球，可手动勾选并使用自选成长。";
        }
        internal void SetCheckedAttributes(params int[] indices)
        {options.Mode=GrowthMode.SelectedAttributes;options.Attributes=(int[])indices.Clone();Rebuild();Emit();}
    }
}
