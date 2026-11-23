// Modified build v1.1.26. GPL-3.0-or-later with ADDITIONAL-TERMS.txt.
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Fm24Editor
{
    enum AttributeOptimizationMode { Preserve, Redistribute }
    sealed class AttributeOptimizationResult
    {
        public readonly byte[] Before, After, Positions;
        public readonly GrowthProfile Profile;
        public readonly RoleAttributeTiers Tiers;
        public readonly int SourceCa, RawPa, Cap, TargetCa, BeforeRca, AfterRca, BeforeSlot, AfterSlot;
        public readonly AttributeOptimizationMode Mode;
        public readonly string Conflict;
        public int Increased { get { return Enumerable.Range(0,54).Count(i => After[i]>Before[i]); } }
        public int Decreased { get { return Enumerable.Range(0,54).Count(i => After[i]<Before[i]); } }
        public int Remaining { get { return TargetCa - AfterRca; } }
        public bool CanApply { get { return Conflict==null && Remaining>=0 && (TargetCa>SourceCa || Increased+Decreased>0); } }
        internal AttributeOptimizationResult(GrowthStep frame, int targetCa, byte[] after, GrowthProfile profile,RoleAttributeTiers tiers,
            AttributeOptimizationMode mode, PositionAbility model, string conflict)
        {
            Before=(byte[])frame.Attributes.Clone(); After=after; Positions=(byte[])frame.Positions.Clone();
            SourceCa=frame.Ca; RawPa=frame.RawPa; Cap=frame.Cap; TargetCa=targetCa;
            Profile=profile; Tiers=tiers; Mode=mode; Conflict=conflict;
            BeforeRca=model.Estimate(Before, out BeforeSlot); AfterRca=model.Estimate(After, out AfterSlot);
        }
    }
    static class AttributeOptimizer
    {
        public static bool Editable(GrowthProfile profile,int index)
        { return RoleAttributeTiers.Editable(profile,index); }
        public static bool CanRaiseToPotential(GrowthStep frame)
        {
            return frame.RawPa>frame.Ca && frame.RawPa==frame.Cap && frame.RawPa<=200;
        }

        public static AttributeOptimizationResult Create(GrowthStep frame,GrowthOptions roles,
            AttributeOptimizationMode mode,bool raiseToPotential=false)
        {
            if(mode!=AttributeOptimizationMode.Preserve && mode!=AttributeOptimizationMode.Redistribute)
                throw new ArgumentOutOfRangeException("mode");
            GrowthFocusState.ValidateAttributes(frame.Attributes);
            if(frame.Ca<1 || frame.Ca>200 || frame.Cap<1 || frame.Cap>200)
                throw new InvalidDataException("当前 CA 或 PA 超出有效范围。");
            if(raiseToPotential && !CanRaiseToPotential(frame))
                throw new InvalidDataException("同时升到 PA 需要一个高于当前 CA 的确定 PA。");
            int targetCa=raiseToPotential?frame.RawPa:frame.Ca;
            var model=new PositionAbility(frame.Positions);
            var profile=roles.ResolveProfile(frame.Positions);
            var tiers=new RoleAttributeTiers(profile,model);
            var original=frame.Attributes;
            var floor=(byte[])original.Clone();
            if(mode==AttributeOptimizationMode.Redistribute)
                for(int i=0;i<floor.Length;i++)
                    if(tiers.TierOf(i)!=AttributeTier.Protected)floor[i]=Math.Min(floor[i],(byte)5);
            int minimum=model.Estimate(floor);
            if(minimum>targetCa)
            {
                string reason;
                if(mode==AttributeOptimizationMode.Preserve)
                {
                    reason="原有属性需要 "+minimum+" 点，超出预算 "+(minimum-targetCa)+" 点。";
                    reason+=!raiseToPotential && CanRaiseToPotential(frame) && minimum<=frame.RawPa?
                        "勾选“同时升到 PA”即可继续保留特点。":"可尝试恢复原属性，或选“重新分配”。";
                }
                else reason="可调整项降至下限后仍超出 "+(minimum-targetCa)+" 点，受保护属性已占满预算。";
                return new AttributeOptimizationResult(frame,targetCa,(byte[])original.Clone(),profile,tiers,mode,model,reason);
            }

            var after=(byte[])original.Clone();
            var budget=new AttributeBudget(model,targetCa);
            if(mode==AttributeOptimizationMode.Redistribute)
                Redistribute(after,floor,tiers,budget);
            else if(minimum<targetCa)
            {
                for(var tier=AttributeTier.Core;tier<=AttributeTier.Foundation;tier++)
                    if(!budget.Raise(after,tiers.Members(tier),RoleAttributeTiers.Target(tier)))break;
            }
            return new AttributeOptimizationResult(frame,targetCa,after,profile,tiers,mode,model,null);
        }

        static void Redistribute(byte[] values,byte[] minimum,RoleAttributeTiers tiers,AttributeBudget budget)
        {
            for(var tier=AttributeTier.Core;tier<=AttributeTier.Other;tier++)
                foreach(int index in tiers.Members(tier))values[index]=(byte)RoleAttributeTiers.Target(tier);

            var blue=tiers.Members(AttributeTier.Core);
            var green=tiers.Members(AttributeTier.Supporting);
            var white=tiers.Members(AttributeTier.Foundation);
            var other=tiers.Members(AttributeTier.Other);
            if(budget.Fits(values))
            {
                if(budget.Raise(values,blue,100))budget.Raise(values,green,85);
                return;
            }

            if(budget.Lower(values,other,i=>30) || budget.Lower(values,white,i=>40) ||
                budget.Lower(values,green,i=>60) || budget.Lower(values,blue,i=>60))
                return;

            // At low CA, relax both background floors together before lowering the highlights further.
            var background=white.Concat(other).ToArray();
            var before=(byte[])values.Clone();
            for(int retained=99;retained>=0;retained--)
            {
                foreach(int index in background)
                    values[index]=(byte)(minimum[index]+(before[index]-minimum[index])*retained/100);
                if(budget.Fits(values))return;
            }
            if(!budget.Lower(values,green,i=>minimum[i]))
                budget.Lower(values,blue,i=>minimum[i]);
        }
        public static void Validate(AttributeOptimizationResult result)
        {
            GrowthFocusState.ValidateAttributes(result.Before); GrowthFocusState.ValidateAttributes(result.After);
            var model=new PositionAbility(result.Positions);
            if(result.Conflict!=null || result.SourceCa<1 || result.SourceCa>200 || result.Cap<1 || result.Cap>200 ||
               (result.Mode!=AttributeOptimizationMode.Preserve && result.Mode!=AttributeOptimizationMode.Redistribute) ||
               model.Estimate(result.Before)!=result.BeforeRca || model.Estimate(result.After)!=result.AfterRca ||
               (result.TargetCa!=result.SourceCa &&
                    (result.TargetCa<=result.SourceCa || result.TargetCa!=result.RawPa || result.TargetCa!=result.Cap)) ||
               result.AfterRca>result.TargetCa)
                throw new InvalidDataException("属性方案的估算值超出所选预算或预览已变化。");
            for(int i=0;i<54;i++)
                if((!Editable(result.Profile,i) || !model.Counts(i)) && result.Before[i]!=result.After[i] ||
                   result.Mode==AttributeOptimizationMode.Preserve && result.After[i]<result.Before[i])
                    throw new InvalidDataException("属性方案与保护项或所选模式冲突。");
        }
    }
    partial class MainForm
    {
        bool SaveAttributeOptimization(Player player,GrowthStep frame,AttributeOptimizationResult result)
        {
            AttributeOptimizer.Validate(result);
            if(!result.CanApply) return false;
            return ApplyReviewedAttributes(player,frame,"一键属性优化",
                "CA "+frame.Ca+" → "+result.TargetCa+"，整套属性估算 CA "+result.BeforeRca+" → "+result.AfterRca+
                "。\n提高 "+result.Increased+" 项，降低 "+result.Decreased+" 项。",()=>frame.PlanOptimization(result));
        }

        bool ApplyReviewedAttributes(Player player,GrowthStep frame,string title,string details,Action prepare)
        {
            var attached=fm;
            Func<bool> guard=()=>attached!=null && fm==attached && Safety.On("core") && Safety.On("ability") &&
                Safety.On("date") && frame.Matches(attached.Read,attached.ReadGameDate);
            if(!guard()) throw new InvalidOperationException("预览已失效，请重新读取球员资料。");
            if(MessageBox.Show(this,player.DisplayName+"\n"+details+"\n\n确认应用预览中的修改？",title,
                MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes) return false;
            if(!guard()) throw new InvalidOperationException("确认期间球员资料已变化，请重新读取。");
            prepare();
            if(frame.Edits.Count==0) return false;
            string error;
            if(!EligibilityTransaction.Apply(attached.Read,attached.Write,frame.Edits,guard,out error,
                ()=>fm==attached && Safety.On("core") && frame.IdentityMatches(attached.Read)))
                throw new InvalidOperationException((error??"写入失败").Replace("注册资格",title).Replace("注册",title));
            RecordGrowthChanges(frame.Edits,()=>fm==attached && Safety.On("core") && Safety.On("ability") && frame.IdentityMatches(attached.Read));
            lblStatus.Text=title+"已应用并读回，可在变更记录中整组撤销。";
            return true;
        }
    }
}
