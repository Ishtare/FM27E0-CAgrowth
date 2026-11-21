// Modified build v1.1.24, 2026-11-20. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
// See CHANGELOG.md; GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Fm24Editor
{
    sealed class CurrencySetting
    {
        public string Symbol;
        public double Rate;
        public CurrencySetting(string symbol,double rate)
        {
            symbol=(symbol??"").Trim();if(symbol.Length==0)symbol="£";
            if(symbol.Length>16 || symbol.Any(char.IsControl))throw new ArgumentException("请填写有效的货币符号。");
            if(symbol=="£")rate=1;
            if(double.IsNaN(rate) || double.IsInfinity(rate) || rate<=0)throw new ArgumentException("换算率必须是大于 0 的有限数值。");
            Symbol=symbol;Rate=rate;
        }
        public static bool TryRate(string value,out double rate)
        {
            value=(value??"").Trim().Normalize(NormalizationForm.FormKC);
            if(!value.Contains('.') && value.Count(c=>c==',')==1)value=value.Replace(',','.');
            return double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out rate) && rate>0 && !double.IsNaN(rate) && !double.IsInfinity(rate);
        }
        public static bool TryMoney(string value,out double amount)
        {
            amount=0;string s=(value??"").Trim().Normalize(NormalizationForm.FormKC);
            bool negative=s.StartsWith("(") && s.EndsWith(")");if(negative)s=s.Substring(1,s.Length-2);
            s=Regex.Replace(s,@"人民币|英镑|欧元|美元|CNY|RMB|GBP|EUR|USD|[£$€¥￥元\s]","",RegexOptions.IgnoreCase);
            var match=Regex.Match(s,@"^([+-]?(?:(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d*)?|\.\d+))([kKmMbB千万亿]?)$");
            if(!match.Success || !double.TryParse(match.Groups[1].Value,NumberStyles.Number,CultureInfo.InvariantCulture,out amount))return false;
            string unit=match.Groups[2].Value.ToLowerInvariant();
            double multiplier=unit=="k"||unit=="千"?1e3:unit=="万"?1e4:unit=="m"?1e6:unit=="亿"?1e8:unit=="b"?1e9:1;
            amount*=multiplier;if(negative){if(amount<0)return false;amount=-amount;}
            return !double.IsNaN(amount) && !double.IsInfinity(amount);
        }
        public static double FromBalance(string shown,int pounds)
        {
            double amount;if(!TryMoney(shown,out amount) || amount==0)throw new ArgumentException("请准确填写 FM 显示的总余额，支持千、万、亿及 k/m/b。");
            if(pounds==0)throw new ArgumentException("该俱乐部余额为 0，请改用手动换算率，或选择其他俱乐部。");
            return new CurrencySetting("校准",amount/pounds).Rate;
        }
        public static CurrencySetting Read(IEnumerable<string> lines,CurrencySetting fallback)
        {
            string symbol=fallback.Symbol,rate=fallback.Rate.ToString("R",CultureInfo.InvariantCulture);
            foreach(string line in lines)
            {
                int at=line.IndexOf('=');if(at<0)continue;
                string key=line.Substring(0,at).Trim(),value=line.Substring(at+1).Trim();
                if(key=="symbol")symbol=value;else if(key=="rate")rate=value;
            }
            double parsed;if(!TryRate(rate,out parsed))return new CurrencySetting("£",1);
            try{return new CurrencySetting(symbol,parsed);}catch{return new CurrencySetting("£",1);}
        }
        public void Save(string path)
        {File.WriteAllLines(path,new[]{"symbol="+Symbol,"rate="+Rate.ToString("R",CultureInfo.InvariantCulture)},Encoding.UTF8);}
    }

    partial class MainForm
    {
        internal Form BuildCurrencyDialog()
        {
            var dlg=new Form {Text="修改器显示货币",Font=Font,Width=650,Height=460,
                AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
            var layout=new TableLayoutPanel {Name="CurrencyForm",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                ColumnCount=2,Padding=new Padding(12)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));
            var intro=new Label {Text="选择修改器显示的货币，并填写与游戏一致的换算率。\n这项设置可以在挂接前保存；游戏内显示币种请在 FM 偏好设置中调整。",AutoSize=true,MaximumSize=new Size(590,0)};
            layout.Controls.Add(intro,0,0);layout.SetColumnSpan(intro,2);
            var symbols=new ComboBox {Name="CurrencySymbol",Width=260,DropDownStyle=ComboBoxStyle.DropDown};
            symbols.Items.AddRange(new object[]{"人民币 ¥","英镑 £","欧元 €","美元 $"});
            string[] values={"¥","£","€","$"};int chosen=Array.IndexOf(values,Cur());if(chosen>=0)symbols.SelectedIndex=chosen;else symbols.Text=Cur();
            Func<string> symbol=()=>symbols.SelectedIndex>=0?values[symbols.SelectedIndex]:symbols.Text.Trim();
            var manual=new RadioButton {Name="ManualCurrency",Text="手动填写换算率",Checked=true,AutoSize=true};
            var rate=new TextBox {Name="CurrencyRate",Width=180,Text=Rate().ToString("R",CultureInfo.InvariantCulture)};
            var estimate=new RadioButton {Name="BalanceCalibration",Text="根据游戏余额校准（可选）",AutoSize=true};
            var club=new ComboBox {Name="CalibrationClub",Width=330,DropDownStyle=ComboBoxStyle.DropDownList};
            club.Items.AddRange(clubs.Where(c=>c.HasFullFinance).ToArray());
            club.SelectedItem=loadedClub ?? (lstSquadClubs.SelectedItem as Club);
            var balance=new TextBox {Name="CalibrationBalance",Width=260};
            var quote=new Label {AutoSize=true};
            AddRow(layout,1,"显示货币",symbols);AddRow(layout,2,"换算方式",manual);AddRow(layout,3,"每 1 英镑对应的金额",rate);
            layout.Controls.Add(quote,1,4);layout.Controls.Add(estimate,1,5);AddRow(layout,6,"用于校准的俱乐部",club);AddRow(layout,7,"FM 显示的总余额",balance);
            var note=new Label {AutoSize=true,MaximumSize=new Size(590,0),Text="换算率以你的 FM 设置为准，本程序不提供实时汇率。校准金额支持“1.5亿”“2000万”等写法。\n应用后会刷新已打开的人物、财务和奖金页；请先应用这些页面中的未保存修改。"};
            layout.Controls.Add(note,0,8);layout.SetColumnSpan(note,2);
            foreach(Control field in new Control[]{symbols,rate,club,balance})field.Anchor=AnchorStyles.Left|AnchorStyles.Right;
            foreach(Control field in layout.Controls)if(field is Label)field.Dock=DockStyle.Fill;
            Action update=()=>
            {
                bool pounds=symbol()=="£";if(pounds){manual.Checked=true;rate.Text="1";}
                manual.Enabled=!pounds;rate.Enabled=!pounds && manual.Checked;
                estimate.Enabled=!pounds && fm!=null && club.Items.Count>0 && Safety.On("finance");
                club.Enabled=balance.Enabled=estimate.Checked && estimate.Enabled;
                quote.Text="换算方向：每 1 £ 对应多少 "+symbol();
            };
            string previous=symbol();
            symbols.TextChanged+=delegate {string current=symbol();if(current!=previous){rate.Text=current=="£"?"1":"";previous=current;}update();};
            manual.CheckedChanged+=delegate {update();};estimate.CheckedChanged+=delegate {update();};update();
            var apply=new Button {Name="SaveCurrency",Text="保存显示设置",Dock=DockStyle.Bottom,Height=38};
            apply.Click+=delegate
            {
                try
                {
                    double parsed=1;string unit=symbol();
                    if(unit!="£")
                    {
                        if(estimate.Checked)
                        {
                            var selected=club.SelectedItem as Club;
                            if(!estimate.Enabled || selected==null || fm==null || !Safety.On("finance"))throw new InvalidOperationException("余额校准需要已通过检查的俱乐部；也可直接填写换算率。");
                            if(!ClubCatalogReader.MatchesFullFinance(fm,selected))throw new InvalidOperationException("俱乐部财务资料已变化，请重新扫描。");
                            var pounds=fm.ReadI32(selected.Finance+FmLayout.FinBalance);if(!pounds.HasValue)throw new IOException("无法读取俱乐部余额，请改用手动换算率。");
                            parsed=CurrencySetting.FromBalance(balance.Text,pounds.Value);
                        }
                        else if(!CurrencySetting.TryRate(rate.Text,out parsed))throw new ArgumentException("请填写有效的正数换算率：每 1 英镑对应多少当前货币。");
                    }
                    var setting=new CurrencySetting(unit,parsed);setting.Save(SettingsPath);
                    txtCur.Text=setting.Symbol;txtRate.Text=setting.Rate.ToString("R",CultureInfo.InvariantCulture);
                    RefreshCurrency();lblStatus.Text="显示设置已保存：1 £ = "+setting.Rate.ToString("G",CultureInfo.InvariantCulture)+" "+setting.Symbol+"。";
                    dlg.Close();
                }
                catch(Exception ex){MessageBox.Show(ex.Message,"显示货币设置未保存");}
            };
            dlg.Controls.Add(ScrollableForm(layout,apply));dlg.AcceptButton=apply;
            dlg.Load+=delegate{FitWindowToWorkArea(dlg,this);};
            return dlg;
        }
        void Calibrate() {using(var dialog=BuildCurrencyDialog())dialog.ShowDialog(this);}
    }
}
