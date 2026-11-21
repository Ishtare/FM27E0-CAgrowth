// GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System.Drawing;
using System.Windows.Forms;

namespace Fm24Editor
{
    partial class MainForm
    {
        internal static void StyleWorkspaceDialog(Form dialog,string title)
        {
            dialog.Text=title;
            dialog.Font=UiFont;
            dialog.ForeColor=UiInk;
            dialog.BackColor=Color.White;
            dialog.Padding=new Padding(12);
            dialog.AutoScaleDimensions=new SizeF(96F,96F);
            dialog.AutoScaleMode=AutoScaleMode.Dpi;
            dialog.StartPosition=FormStartPosition.CenterParent;
            dialog.ShowInTaskbar=false;
            dialog.MinimizeBox=false;
        }

        internal static Button DialogDismiss(string name,string text)
        {
            return new Button{Name=name,Text=text,DialogResult=DialogResult.Cancel,CausesValidation=false};
        }

        internal static FlowLayoutPanel DialogActions(Form dialog,Button dismiss,Button primary=null)
        {
            var actions=new FlowLayoutPanel
            {
                Name="DialogActions",Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft,
                WrapContents=false,Padding=new Padding(0,4,0,0),Margin=Padding.Empty
            };
            StyleButton(dismiss,WorkspaceButtonRole.Secondary);
            dismiss.MinimumSize=new Size(96,0);
            dismiss.AutoSize=true;
            dismiss.TabIndex=primary==null?0:1;
            actions.Controls.Add(dismiss);
            if(primary!=null)
            {
                StyleButton(primary,WorkspaceButtonRole.Primary);
                primary.MinimumSize=new Size(96,0);
                primary.AutoSize=true;
                primary.TabIndex=0;
                actions.Controls.Add(primary);
            }
            dialog.CancelButton=dismiss;
            return actions;
        }
    }
}
