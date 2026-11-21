// Modified build v1.1.18, 2026-11-17. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class DualRoleSelector : UserControl
    {
        sealed class Item
        {public int Index;public override string ToString(){return Index<0?"基本方案（按主要位置）":GrowthProfile.All[Index].Name+(GrowthProfile.All[Index].Phase=="基本"?"（基本）":"");}}
        public readonly ComboBox InPosition,InRole,OutPosition,OutRole;
        public readonly NumericUpDown Weight;
        readonly Label weightLabel;
        GrowthOptions options=new GrowthOptions();byte[] positions;bool binding;
        public event Action<int,int,int> Changed;
        public DualRoleSelector(string prefix)
        {
            Name=prefix+"DualRoleSelector";Height=146;Dock=DockStyle.Fill;
            InPosition=Combo(prefix+"PositionGroup");InRole=Combo(prefix+"FocusProfile");OutPosition=Combo(prefix+"OutPositionGroup");OutRole=Combo(prefix+"OutFocusProfile");
            InPosition.Items.Add("按主要位置");InPosition.Items.AddRange(GrowthProfile.Groups.Cast<object>().ToArray());
            OutPosition.Items.AddRange(new object[]{"不使用无球方案","按主要位置"});OutPosition.Items.AddRange(GrowthProfile.Groups.Cast<object>().ToArray());
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
            var tabs=new TabControl{Name=prefix+"RolePhaseTabs",Dock=DockStyle.Fill,TabIndex=0};tabs.TabPages.Add(Page("有球位置／角色",InPosition,InRole));tabs.TabPages.Add(Page("无球位置／角色",OutPosition,OutRole));root.Controls.Add(tabs,0,0);
            var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,TabIndex=1};bar.Controls.Add(new Label{Text="有球权重 %",AutoSize=true,Padding=new Padding(0,6,0,0)});
            Weight=new NumericUpDown{Name=prefix+"PossessionWeight",Minimum=0,Maximum=100,Value=50,Width=58};bar.Controls.Add(Weight);weightLabel=new Label{AutoSize=true,Padding=new Padding(2,6,0,0)};bar.Controls.Add(weightLabel);root.Controls.Add(bar,0,1);Controls.Add(root);
            InPosition.SelectedIndexChanged+=delegate{if(binding)return;options.Profile=InPosition.SelectedIndex<=0?-1:Array.FindIndex(GrowthProfile.All,p=>p.Group==GrowthProfile.Groups[InPosition.SelectedIndex-1] && p.Phase=="基本");RefreshChoices();Emit();};
            OutPosition.SelectedIndexChanged+=delegate{if(binding)return;options.DefendingProfile=OutPosition.SelectedIndex==0?-2:OutPosition.SelectedIndex==1?-1:Array.FindIndex(GrowthProfile.All,p=>p.Group==GrowthProfile.Groups[OutPosition.SelectedIndex-2] && p.Phase=="基本");RefreshChoices();Emit();};
            InRole.SelectedIndexChanged+=delegate{if(binding || !(InRole.SelectedItem is Item))return;options.Profile=((Item)InRole.SelectedItem).Index;Emit();};
            OutRole.SelectedIndexChanged+=delegate{if(binding || !(OutRole.SelectedItem is Item))return;options.DefendingProfile=((Item)OutRole.SelectedItem).Index;Emit();};
            Weight.ValueChanged+=delegate{if(binding)return;options.PossessionWeight=(int)Weight.Value;RefreshWeight();Emit();};Bind(options,null,false);
        }
        static ComboBox Combo(string name){return new ComboBox{Name=name,Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DropDownWidth=350};}
        static TabPage Page(string name,Control position,Control role)
        {
            var page=new TabPage(name){Padding=new Padding(4)};var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            position.TabIndex=0;role.TabIndex=1;
            for(int i=0;i<2;i++)table.RowStyles.Add(new RowStyle(SizeType.Percent,50));table.Controls.Add(new Label{Text="位置",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);table.Controls.Add(position,1,0);table.Controls.Add(new Label{Text="角色",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,1);table.Controls.Add(role,1,1);page.Controls.Add(table);return page;
        }
        GrowthProfile Context(int index){try{return GrowthProfile.Resolve(positions,index);}catch{return index>=0 && index<GrowthProfile.All.Length?GrowthProfile.All[index]:GrowthProfile.All[8];}}
        void Fill(ComboBox role,int selected,string phase)
        {
            role.Items.Clear();if(selected==-2)return;var context=Context(selected);
            if(selected==-1)role.Items.Add(new Item{Index=-1});
            for(int i=0;i<GrowthProfile.All.Length;i++)if(GrowthProfile.All[i].Group==context.Group && (GrowthProfile.All[i].Phase==phase || GrowthProfile.All[i].Phase=="基本"))role.Items.Add(new Item{Index=i});
            for(int i=0;i<role.Items.Count;i++)if(((Item)role.Items[i]).Index==selected){role.SelectedIndex=i;break;}
            if(role.SelectedIndex<0 && role.Items.Count>0)role.SelectedIndex=0;
        }
        void RefreshChoices()
        {
            binding=true;
            try
            {
                InPosition.SelectedIndex=options.Profile<0?0:Array.IndexOf(GrowthProfile.Groups,Context(options.Profile).Group)+1;
                OutPosition.SelectedIndex=options.DefendingProfile==-2?0:options.DefendingProfile==-1?1:Array.IndexOf(GrowthProfile.Groups,Context(options.DefendingProfile).Group)+2;
                Fill(InRole,options.Profile,"持球");Fill(OutRole,options.DefendingProfile,"无球");Weight.Value=Math.Max(0,Math.Min(100,options.PossessionWeight));RefreshWeight();
                OutRole.Enabled=options.DefendingProfile!=-2;Weight.Enabled=options.DefendingProfile!=-2;
            }
            finally{binding=false;}
        }
        void RefreshWeight(){weightLabel.Text=options.DefendingProfile==-2?"仅有球":"无球 "+(100-options.PossessionWeight)+"%";}
        void Emit(){if(Changed!=null)Changed(options.Profile,options.DefendingProfile,options.PossessionWeight);}
        public void Bind(GrowthOptions value,byte[] currentPositions,bool enabled)
        {options=value.Clone();positions=currentPositions==null?null:(byte[])currentPositions.Clone();RefreshChoices();Enabled=enabled;}
    }
}
