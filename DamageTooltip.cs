using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace SephiriaDicePreview
{
    public static class DamageTooltip
    {
        private static readonly MethodInfo Related = AccessTools.Method(typeof(WeaponSimple), "GetRelatedStatMultiplier");
        private static readonly FieldInfo CharmText = AccessTools.Field(typeof(UI_CharmTooltip), "effectText");
        private static readonly FieldInfo TempestStack = AccessTools.Field(typeof(WeaponAddonCommon_Tempest), "tempestStack");
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        public static int Revision;
        public static int Calculations;
        public static int CacheHits;
        private static GameObject previewRoot;
        private static Charm_Basic previewCharm;
        private static int previewId;
        private const string Heading = "\n\n<color=#81E6C4>공격당 예상 피해 · 일반 / 치명타</color>\n";

        internal static PlayerAvatar Player { get { return CombatManager.Instance ? CombatManager.Instance.CurrentPlayer as PlayerAvatar : null; } }
        public static void Invalidate(UnitAvatar avatar)
        {
            var player=Player;
            if (!player||!avatar||(avatar!=player&&avatar.NetworkLeader!=player)) return;
            unchecked { Revision++; }
        }
        private static string Key(string source, PlayerAvatar p)
        {
            return source + "|" + p.GetInstanceID() + "|" + Revision + "|" + p.hp + "|" + p.MP + "|" + p.Money + "|" + p.IsInBattle;
        }
        private static string Remember(string key, string value)
        {
            if (Cache.Count >= 64) Cache.Clear();
            if(capturing!=null)return value;
            Cache[key] = value; Calculations++; return value;
        }
        public sealed class Hit
        {
            internal FollowerDamageProfiles.Hit Follower;
            public float Raw;
            public float AfterFactors;
            public float ProjectileDamagePercent;
            public float ResourceAmount, ResourcePerUnit;
            public float[] Factors;
            public bool Weapon;
            public bool Magic;
            public float ExtraCriticalChance;
            public bool ElementalEffect;
            public EDamageElementalType Element=EDamageElementalType.Physical;
            public int ExtraCritical;
            public float CriticalRateMultiplier=1;
            public bool CanCritical=true;
        }
        internal static string DisplayDamage(float value) { return Math.Floor((double)value).ToString("0",System.Globalization.CultureInfo.InvariantCulture); }
        internal static string ColorText(string text,EDamageElementalType element)
        {
            string color=ElementColor(element);
            return color==null?text:"<color=#"+color+">"+text+"</color>";
        }
        internal const string FixedDamageColor="B8B8B8";
        private static string ElementColor(EDamageElementalType element)
        {
            return element==EDamageElementalType.Physical?"FFFFFF":
                element==EDamageElementalType.Fire?"FFC900":
                element==EDamageElementalType.Ice?"5F9FFF":
                element==EDamageElementalType.Lightning?"87FFFF":
                element==EDamageElementalType.Chaos?"C06CFF":
                element==EDamageElementalType.IceAndLightning?"73CFFF":
                element==EDamageElementalType.FireAndIce?"AFB480":
                element==EDamageElementalType.FireAndLightning?"C3E480":"FFFFFF";
        }
        private static string HexText(string text,string color){return "<color=#"+color+">"+text+"</color>";}
        private static void AddColorWeight(ref double red,ref double green,ref double blue,ref double total,string hex,double weight)
        {
            weight=Math.Abs(weight);if(weight<=0)return;
            red+=Convert.ToInt32(hex.Substring(0,2),16)*weight;
            green+=Convert.ToInt32(hex.Substring(2,2),16)*weight;
            blue+=Convert.ToInt32(hex.Substring(4,2),16)*weight;total+=weight;
        }
        private static string MixedColor(IEnumerable<KeyValuePair<EDamageElementalType,Pair>> elements,Pair fixedDamage,int mode)
        {
            double red=0,green=0,blue=0,total=0;
            foreach(var entry in elements)
            {
                float value=mode==0?entry.Value.Normal:mode==1?entry.Value.Critical:entry.Value.Execution;
                AddColorWeight(ref red,ref green,ref blue,ref total,ElementColor(entry.Key),value);
            }
            float flat=mode==0?fixedDamage.Normal:mode==1?fixedDamage.Critical:fixedDamage.Execution;
            AddColorWeight(ref red,ref green,ref blue,ref total,FixedDamageColor,flat);
            if(total<=0)return "FFFFFF";
            return ((int)Math.Round(red/total)).ToString("X2")+((int)Math.Round(green/total)).ToString("X2")+((int)Math.Round(blue/total)).ToString("X2");
        }
        internal static string MixedColor(Dictionary<EDamageElementalType,double> elements,double fixedDamage)
        {
            double red=0,green=0,blue=0,total=0;
            foreach(var entry in elements)AddColorWeight(ref red,ref green,ref blue,ref total,ElementColor(entry.Key),entry.Value);
            AddColorWeight(ref red,ref green,ref blue,ref total,FixedDamageColor,fixedDamage);
            if(total<=0)return "FFFFFF";
            return ((int)Math.Round(red/total)).ToString("X2")+((int)Math.Round(green/total)).ToString("X2")+((int)Math.Round(blue/total)).ToString("X2");
        }
        internal static string ColorHex(string text,string color){return HexText(text,color);}
        internal static string ColorDamage(string text,Hit[] hits)
        {
            if(hits==null||hits.Length==0)return text;
            EDamageElementalType element=DisplayElement(hits[0]);
            for(int i=1;i<hits.Length;i++)if(DisplayElement(hits[i])!=element)return text;
            return ColorText(text,element);
        }
        private static EDamageElementalType DisplayElement(Hit hit){return hit.Follower!=null?hit.Follower.DisplayElement:hit.Element;}
        public sealed class Pair
        {
            public float Normal, Critical, Execution;
            public override string ToString() { return DisplayDamage(Normal)+" / "+DisplayDamage(Critical); }
        }
        public sealed class Snapshot
        {
            public struct RawStep
            {
                // 0: probabilistic reduction, 1: close, 2: full HP, 3: debuff count, 4: frostbite.
                public int Kind;
                public float Percent,Chance;
                public bool[] Elements;
            }
            public RawStep[] RawSteps;
            public string Template;
            internal DpsSnapshot Dps;
            internal bool FullConditions;
            internal readonly List<DebuffDamagePreview> Debuffs=new List<DebuffDamagePreview>();
            public readonly List<Hit[]> Rows=new List<Hit[]>();
            public int All, DashBonus, DashCount, Critical, WeaponCritical, WeaponAmp, Flat;
            public int Elite;
            public float CriticalChance,WeaponCriticalChance,MagicCriticalChance;
            public int MagicCriticalDamage;
            public float[] ElementCriticalChance;
            public bool ExecutionEnabled;
            public int GoldBonus,DebuffBonus,PoisonDebuffBonus;
            public float DefenseBonus;
            public float Gold {get{return 1+GoldBonus/100f;}}
            public float Defense {get{return 1+DefenseBonus;}}
            public float Close=1, First=1, OneDebuff=1, Burn;
            public int[] ElementCritical;
            public float[] Frostbite;
            public float AlwaysDamageFactor=1,RandomDamageMinimum=1,RandomDamageMaximum=1;
            public Pair Evaluate(Hit hit,bool elite=false,int? targetDefense=null,int ignoreDefense=0)
            {
                int conditions=FullConditions?(Dps==null?7:5):0;
                int debuffCount=FullConditions?(Burn!=0?2:1):0;
                return EvaluateConditions(hit,1,FullConditions?Burn:0,elite,conditions,debuffCount,Dps==null?0:2,targetDefense,ignoreDefense);
            }
            internal Pair EvaluateWithoutFlat(Hit hit,bool elite=false,int? targetDefense=null,int ignoreDefense=0)
            {
                int conditions=FullConditions?(Dps==null?7:5):0;
                int debuffCount=FullConditions?(Burn!=0?2:1):0;
                return EvaluateConditions(hit,1,FullConditions?Burn:0,elite,conditions,debuffCount,Dps==null?0:2,targetDefense,ignoreDefense,0);
            }
            internal EDamageElementalType GetDisplayElement(Hit hit){return DisplayElement(hit);}
            internal double Expected(Hit hit,Pair damage)
            {
                if(!hit.CanCritical)return damage.Normal;
                double chance=CriticalChanceFor(hit);
                bool execution=hit.Follower!=null?hit.Follower.ExecutionEnabled:ExecutionEnabled;
                return DpsNumbers.CriticalExpected(damage.Normal,damage.Critical,damage.Execution,chance,execution);
            }
            internal double CriticalChanceFor(Hit hit)
            {
                if(!hit.CanCritical)return 0;
                double chance=hit.Follower!=null?hit.Follower.Chance():
                    CriticalChance+(hit.Weapon?WeaponCriticalChance:hit.Magic?MagicCriticalChance:0);
                int element=(int)hit.Element;
                if(hit.Follower==null&&ElementCriticalChance!=null&&element>=0&&element<ElementCriticalChance.Length)chance+=ElementCriticalChance[element];
                chance+=hit.ExtraCriticalChance;
                return chance;
            }
            internal static float ApplyRawSteps(float raw,RawStep[] steps,EDamageElementalType elementalType,int conditions,int debuffCount=0,int randomMode=0)
            {
                if(steps==null)return raw;
                foreach(var step in steps)
                {
                    switch(step.Kind)
                    {
                        case 0:
                            if(step.Chance>=100)raw-=raw*(step.Percent/100f);
                            else if(step.Chance>0)
                            {
                                // Mode 2 is the probability-weighted value used by expected DPS.
                                if(randomMode==2)raw-=raw*(step.Percent/100f)*Mathf.Clamp01(step.Chance/100f);
                                else if((randomMode<0&&step.Percent>0)||(randomMode==1&&step.Percent<0))raw-=raw*(step.Percent/100f);
                            }
                            break;
                        case 1:if((conditions&1)!=0)raw+=raw*step.Percent/100f;break;
                        case 2:if((conditions&2)!=0)raw+=raw*step.Percent/100f;break;
                        case 3:if(debuffCount>0)raw*=(100+debuffCount*step.Percent)/100f;break;
                        case 4:
                            int element=(int)elementalType;
                            if((conditions&4)!=0&&step.Elements!=null&&element>=0&&element<step.Elements.Length&&step.Elements[element])raw+=raw*step.Percent/100f;
                            break;
                    }
                }
                return raw;
            }
            private Pair EvaluateConditions(Hit hit,float rawFactor,float directBonus,bool elite=false,int conditions=0,int debuffCount=0,int randomMode=0,int? targetDefense=null,int ignoreDefense=0,int? flatOverride=null)
            {
                if(hit.Follower!=null)return hit.Follower.Evaluate(elite,rawFactor,hit.ExtraCritical,hit.CriticalRateMultiplier,hit.CanCritical,hit.ProjectileDamagePercent,conditions,targetDefense,ignoreDefense,flatOverride,randomMode);
                float raw=hit.Raw+hit.ResourceAmount*hit.ResourcePerUnit;
                if(hit.Factors!=null) foreach(float f in hit.Factors) raw*=f;
                raw+=hit.AfterFactors;
                raw+=raw*hit.ProjectileDamagePercent/100f;
                raw*=rawFactor;
                raw=ApplyRawSteps(raw,RawSteps,hit.Element,conditions,debuffCount,randomMode);
                float normal=raw;
                if(hit.Weapon)normal+=raw*((DashBonus*DashCount)/100f);
                float allBonus=raw*(All/100f);
                float eliteBonus=elite?raw*Elite/100f:0;
                normal+=allBonus+eliteBonus;
                if(hit.ElementalEffect)
                {
                    normal+=normal*DebuffBonus/100f;
                    normal+=normal*PoisonDebuffBonus/100f;
                }
                normal+=normal*(GoldBonus/100f);
                if(hit.Weapon)normal+=normal*directBonus;
                int critical=50+Critical;
                if(hit.Weapon) { critical+=WeaponCritical; if(WeaponAmp>0) critical+=(int)(critical*WeaponAmp/100f); }
                else if(hit.Magic)critical+=MagicCriticalDamage;
                critical+=hit.ExtraCritical;
                if(ElementCritical!=null&&(int)hit.Element>=0&&(int)hit.Element<ElementCritical.Length)critical+=ElementCritical[(int)hit.Element];
                if(hit.CriticalRateMultiplier!=1)critical=(int)Math.Round(critical*hit.CriticalRateMultiplier,MidpointRounding.ToEven);
                float criticalBonus=hit.CanCritical?critical/100f:0;
                // Execution doubles only the critical bonus: raw 100, +70% => 240, not 340.
                int flat=flatOverride??Flat;
                return new Pair { Normal=FinishOutgoing(normal,DefenseBonus,flat,targetDefense,ignoreDefense), Critical=FinishOutgoing(normal+normal*criticalBonus,DefenseBonus,flat,targetDefense,ignoreDefense), Execution=FinishOutgoing(normal+normal*criticalBonus*2f,DefenseBonus,flat,targetDefense,ignoreDefense) };
            }
            internal string RenderRow(Hit[] hits,bool elite=false,int? targetDefense=null,int ignoreDefense=0)
            {
                var total=new Pair();var withoutFlat=new Pair();bool execution=true,hasCritical=false,guaranteedCritical=true;
                var elements=new Dictionary<EDamageElementalType,Pair>();
                foreach(var hit in hits)
                {
                    var value=Evaluate(hit,elite,targetDefense,ignoreDefense);
                    var baseValue=EvaluateConditions(hit,1,FullConditions?Burn:0,elite,FullConditions?(Dps==null?7:5):0,FullConditions?(Burn!=0?2:1):0,0,targetDefense,ignoreDefense,0);
                    total.Normal+=value.Normal;total.Critical+=value.Critical;total.Execution+=value.Execution;
                    withoutFlat.Normal+=baseValue.Normal;withoutFlat.Critical+=baseValue.Critical;withoutFlat.Execution+=baseValue.Execution;
                    var displayElement=DisplayElement(hit);
                    Pair element;if(!elements.TryGetValue(displayElement,out element)){element=new Pair();elements.Add(displayElement,element);}
                    element.Normal+=baseValue.Normal;element.Critical+=baseValue.Critical;element.Execution+=baseValue.Execution;
                    execution&=hit.Follower!=null?hit.Follower.ExecutionEnabled:ExecutionEnabled;hasCritical|=hit.CanCritical;
                    guaranteedCritical&=hit.CanCritical&&CriticalChanceFor(hit)>=100;
                }
                var fixedDamage=new Pair{Normal=total.Normal-withoutFlat.Normal,Critical=total.Critical-withoutFlat.Critical,Execution=total.Execution-withoutFlat.Execution};
                bool showFixed=Math.Abs(fixedDamage.Normal)>.0001||Math.Abs(fixedDamage.Critical)>.0001||Math.Abs(fixedDamage.Execution)>.0001;
                bool mixed=elements.Count>1||showFixed;
                if(!mixed)
                {
                    string simple=guaranteedCritical?"치명타 "+DisplayDamage(total.Critical):hits.Length==1&&!hits[0].CanCritical?DisplayDamage(total.Normal)+" (치명타 없음)":total.ToString();
                    if(execution&&hasCritical)simple+=" · 처형 "+DisplayDamage(total.Execution);
                    return ColorDamage(simple,hits);
                }
                var parts=new List<string>();
                foreach(var entry in elements)
                {
                    string value=guaranteedCritical?DisplayDamage(entry.Value.Critical):hasCritical?entry.Value.ToString():DisplayDamage(entry.Value.Normal)+" (치명타 없음)";
                    parts.Add(ColorText(value,entry.Key));
                }
                if(showFixed)
                {
                    string flat=guaranteedCritical?DisplayDamage(fixedDamage.Critical):hasCritical?fixedDamage.ToString():DisplayDamage(fixedDamage.Normal)+" (치명타 없음)";
                    parts.Add(HexText(flat,FixedDamageColor));
                }
                string combined;
                if(guaranteedCritical)combined=HexText(DisplayDamage(total.Critical),MixedColor(elements,fixedDamage,1));
                else
                {
                    combined=HexText(DisplayDamage(total.Normal),MixedColor(elements,fixedDamage,0));
                    if(hasCritical)combined+=" / "+HexText(DisplayDamage(total.Critical),MixedColor(elements,fixedDamage,1));
                    else combined+=" (치명타 없음)";
                }
                if(execution&&hasCritical)combined+=" · 처형 "+HexText(DisplayDamage(total.Execution),MixedColor(elements,fixedDamage,2));
                return (guaranteedCritical?"치명타 ":"")+(parts.Count>0?String.Join(" + ",parts.ToArray())+" = 합계 ":"")+combined;
            }
            private string FormatConditions(Hit hit,float factor,float bonus,bool elite=false,int conditions=0,int debuffCount=0)
            {
                var pair=EvaluateConditions(hit,factor,bonus,elite,conditions,debuffCount);
                bool execution=hit.Follower!=null?hit.Follower.ExecutionEnabled:ExecutionEnabled;
                bool guaranteed=hit.CanCritical&&CriticalChanceFor(hit)>=100;
                string value=guaranteed?"치명타 "+DisplayDamage(pair.Critical)+(execution?" · 처형 "+DisplayDamage(pair.Execution):""):hit.CanCritical?pair.ToString()+(execution?" · 처형 "+DisplayDamage(pair.Execution):""):DisplayDamage(pair.Normal)+" (치명타 없음)";
                return ColorText(value,hit.Element);
            }
            public string Calculate()
            {
                if(Dps!=null){Interlocked.Increment(ref Calculations);LastWorkerThread=Thread.CurrentThread.ManagedThreadId;return Dps.Calculate(this);}
                var renderedRows=new string[Rows.Count];
                for(int i=0;i<Rows.Count;i++)
                {
                    bool hasCritical=false,guaranteedCritical=true;
                    foreach(var hit in Rows[i]){hasCritical|=hit.CanCritical;guaranteedCritical&=hit.CanCritical&&CriticalChanceFor(hit)>=100;}
                    string rendered=RenderRow(Rows[i]);
                    if(RandomDamageMinimum!=1||RandomDamageMaximum!=1)
                    {
                        bool ownAttack=false;
                        var minimum=new Pair();var maximum=new Pair();
                        foreach(var h in Rows[i])
                        {
                            ownAttack|=h.Follower==null;
                            var low=EvaluateConditions(h,1,0,false,0,0,-1);
                            var high=EvaluateConditions(h,1,0,false,0,0,1);
                            minimum.Normal+=low.Normal;minimum.Critical+=low.Critical;
                            maximum.Normal+=high.Normal;maximum.Critical+=high.Critical;
                        }
                        string minimumText=guaranteedCritical?"치명타 "+DisplayDamage(minimum.Critical):hasCritical?minimum.ToString():DisplayDamage(minimum.Normal);
                        string maximumText=guaranteedCritical?"치명타 "+DisplayDamage(maximum.Critical):hasCritical?maximum.ToString():DisplayDamage(maximum.Normal);
                        if(ownAttack)rendered+="\n  확률 피해 보정 최소: "+ColorDamage(minimumText,Rows[i])+"\n  확률 피해 보정 최대: "+ColorDamage(maximumText,Rows[i])+"\n  각 타격에서 개별 발동 · 다른 대상 조건 미적용";
                    }
                    bool hasElite=false;
                    foreach(var hit in Rows[i])
                    {
                        hasElite|=(hit.Follower!=null?hit.Follower.Elite:Elite)!=0;
                    }
                    if(hasElite)
                    {
                        rendered+="\n  보스·미니보스: "+RenderRow(Rows[i],true);
                    }
                    renderedRows[i]=rendered;
                }
                Interlocked.Increment(ref Calculations);
                LastWorkerThread=Thread.CurrentThread.ManagedThreadId;
                string renderedDetails=RenderRows(Template,renderedRows);
                foreach(var debuff in Debuffs)if(debuff.Visible(FullConditions))renderedDetails+="\n"+debuff.Render(this,false);
                return renderedDetails;
            }
            private static string RenderRows(string template,string[] rows)
            {
                if(rows.Length==0)return template;
                const string marker="{damage:";
                var result=new StringBuilder(template.Length);
                int position=0;
                while(position<template.Length)
                {
                    int start=template.IndexOf(marker,position,StringComparison.Ordinal);
                    if(start<0){result.Append(template,position,template.Length-position);break;}
                    result.Append(template,position,start-position);
                    int end=template.IndexOf('}',start+marker.Length);
                    if(end<0){result.Append(template,start,template.Length-start);break;}
                    int index;
                    string number=template.Substring(start+marker.Length,end-start-marker.Length);
                    if(int.TryParse(number,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out index)&&index>=0&&index<rows.Length)
                        result.Append(rows[index]);
                    else result.Append(template,start,end-start+1);
                    position=end+1;
                }
                return result.ToString();
            }
        }
        public static int LastWorkerThread;
        public static double MaxCaptureMilliseconds, TotalCaptureMilliseconds;
        public static int CaptureCount;
        private static Snapshot capturing;
        internal static Snapshot CurrentCapture {get{return capturing;}}
        internal static float FinishOutgoing(float amount,float defenseBonus,int flat,int? targetDefense=null,int ignoreDefense=0)
        {
            if(targetDefense.HasValue)return DpsNumbers.FinishDamage(amount,defenseBonus,flat,targetDefense.Value,ignoreDefense);
            amount+=amount*defenseBonus;
            return amount>0?Math.Max(1,amount+flat):0;
        }
        private static Snapshot Stats(PlayerAvatar p)
        {
            var s=new Snapshot { All=p.GetCustomStat(ECustomStat.AllDamageBonus), DashBonus=p.GetCustomStatUnsafe("WEAPONDAMAGEBONUSBYDASHCOUNT"), DashCount=p.GetCustomStatUnsafe("DASHCOUNT"), Critical=p.GetCustomStat(ECustomStat.CriticalDamageBonus), WeaponCritical=p.GetCustomStatUnsafe("WEAPONCRITICALDAMAGE"), WeaponAmp=p.GetCustomStatUnsafe("WEAPONCRITICALDAMAGEAMPLIFY"), Flat=p.GetCustomStatUnsafe("TRUEDAMAGE") };
            s.DebuffBonus=p.GetCustomStatUnsafe("DEBUFFDAMAGE");s.PoisonDebuffBonus=p.GetCustomStatUnsafe("POISONDEBUFFDAMAGEBONUS");
            s.ExecutionEnabled=p.GetCustomStat(ECustomStat.EXECUTION)>0;
            s.CriticalChance=p.GetCustomStat(ECustomStat.Critical)/100f;
            s.WeaponCriticalChance=p.GetCustomStatUnsafe("WEAPONCRITICAL")/100f;
            s.MagicCriticalChance=p.GetCustomStat(ECustomStat.MagicCritical)/100f;
            s.MagicCriticalDamage=p.GetCustomStat(ECustomStat.MagicCriticalDamageBonus);
            s.Elite=p.GetCustomStatUnsafe("ELITEDAMAGE");
            if(p.GetCustomStatUnsafe("GOLDHAND")>0)
            {
                int leaf=KeywordDatabase.GetConstValue("GOLDHANDLEAF");int bonus=leaf>0?p.Money/leaf:0;
                if(p.GetCustomStatUnsafe("GOLDHANDUNLIMIT")<=0)bonus=Math.Min(bonus,KeywordDatabase.GetConstValue("GOLDHANDMAX"));
                s.GoldBonus=bonus;
            }
            if(p.GetCustomStatUnsafe("DEFENSETOATTACK")>0)s.DefenseBonus=UnitAvatar.GetDamageReduction(1,p.GetCustomStat(ECustomStat.DamageReduction));
            ConditionalDamageProfiles.Capture(p,s);
            return s;
        }
        internal static string Hits(PlayerAvatar p, params Hit[] hits)
        {
            if(capturing!=null) { int idx=capturing.Rows.Count;capturing.Rows.Add(hits);return "{damage:"+idx+"}"; }
            return Stats(p).RenderRow(hits);
        }
        public static string DamagePair(PlayerAvatar p,float raw,bool weapon) { return Hits(p,new Hit {Raw=raw,Weapon=weapon}); }
        public static Snapshot Capture(Func<string> collect,PlayerAvatar p)
        {
            if(capturing!=null)throw new InvalidOperationException("Nested damage snapshot");
            var watch=System.Diagnostics.Stopwatch.StartNew();
            capturing=Stats(p);
            try { capturing.Template=collect();return capturing; }
            finally { capturing=null;watch.Stop();CaptureCount++;TotalCaptureMilliseconds+=watch.Elapsed.TotalMilliseconds;MaxCaptureMilliseconds=Math.Max(MaxCaptureMilliseconds,watch.Elapsed.TotalMilliseconds); }
        }
        public static string WeaponText(WeaponSimple weapon, PlayerAvatar p)
        {
            if (!weapon || !p) return "현재 캐릭터가 있어야 피해를 계산할 수 있습니다.";
            string key = Key("W" + weapon.GetInstanceID() + ":" + weapon.attackMoveSet, p);
            string cached; if(capturing==null && Cache.TryGetValue(key, out cached)) { CacheHits++; return cached; }
            var text = new StringBuilder(Heading.TrimStart());
            ResourceAttacks(text,weapon,p);
            WeaponInputAttacks(text,weapon,p,weapon.basicComboAttacks,"평타",0);
            WeaponInputAttacks(text,weapon,p,weapon.dashAttacks,"돌진",1);
            WeaponInputAttacks(text,weapon,p,weapon.specialAttacks,"특수",2);
            WeaponAdditionalDamageProfiles.Append(text,weapon,p);
            WeaponDedicatedProfiles.Append(text,weapon,p);
            DebuffDamagePreview.CaptureWeapon(weapon,p);
            text.Append("<color=#B9C4D4>기본 동작 1타 · 일반 / 모두 치명타\n적 방어 적용 전</color>");
            return Remember(key, text.ToString());
        }
        public static Hit MagicBladeHit(WeaponSimple_Katana weapon,PlayerAvatar p,int missing)
        {
            return new Hit {Raw=weapon.magicBladeDamge,ResourceAmount=missing,ResourcePerUnit=.7f,Factors=new[]{1+p.GetCustomStat(ECustomStat.MagicDamageBonus)/100f},ExtraCritical=p.GetCustomStat(ECustomStat.MagicCriticalDamageBonus)};
        }
        private static void ResourceAttacks(StringBuilder text,WeaponSimple weapon,PlayerAvatar p)
        {
            foreach(var addon in weapon.addons)
            {
                var change=addon as WeaponAddonCommon_ChangeWeaponAction;
                if(change && change.newActionParameter.Contains("MP=ALL"))MpAttackRange(text,weapon,p,change.fireData);
                var more=addon as WeaponAddonCommon_ChangeWeaponAction_More;
                if(more)foreach(var action in more.changeWeaponActionInfos)if(action.newActionParameter.Contains("MP=ALL"))foreach(var fire in action.fireDatas)MpAttackRange(text,weapon,p,fire);
                var tempest=addon as WeaponAddonCommon_Tempest;
                if(tempest && tempest.tempestFireData)
                {
                    text.AppendLine("폭풍 중첩 · 일반 / 치명타 (0중첩: 발동 불가)");
                    int current=(int)TempestStack.GetValue(tempest);
                    if(tempest.maxTempestStack>0)
                    {
                        text.Append("최소 (1중첩): ").AppendLine(TempestDescription(weapon,p,tempest,1));
                        text.Append("최대 (").Append(tempest.maxTempestStack).Append("중첩): ").AppendLine(TempestDescription(weapon,p,tempest,tempest.maxTempestStack));
                    }
                    text.Append("현재 (").Append(current).Append("중첩): ").AppendLine(current>0?TempestDescription(weapon,p,tempest,current):"발동 불가");
                    text.AppendLine("사용 시 현재 중첩 전부 소모 · MP 소모 없음");
                }
            }
            var katana=weapon as WeaponSimple_Katana;
            if(katana&&katana.sheathActionType==WeaponSimple_Katana.ESheathActionType.CloudSlash)
                CloudSlashRange(text,katana,p);
            if(katana && katana.useMagicBlade)
            {
                bool infinite=p.GetCustomStatUnsafe("INFINITYMP")>0;
                int min=infinite?0:Math.Max(0,Math.Min(p.MaxMp,DamageStatPreview.ReadReservedMp(p)));
                int max=infinite?0:Math.Max(0,p.MaxMp);
                int current=infinite?0:Math.Max(0,p.MaxMp-p.MP);
                text.AppendLine("마법 사용 시 마력 도 · 일반 / 치명타");
                text.Append("최소 (빈 MP ").Append(min).Append("): ").AppendLine(Hits(p,MagicBladeHit(katana,p,min)));
                text.Append("최대 (빈 MP ").Append(max).Append("): ").AppendLine(Hits(p,MagicBladeHit(katana,p,max)));
                text.Append("현재 (빈 MP ").Append(current).Append("): ").AppendLine(Hits(p,MagicBladeHit(katana,p,current)));
                text.AppendLine("기본 피해 = "+katana.magicBladeDamge+" + 빈 MP × 0.7");
                if(infinite)text.AppendLine("무한 MP: 빈 MP는 0으로 계산");
                text.AppendLine("마법 피해 증가·치명타·최종 보정 반영");
            }
        }
        private static string TempestDescription(WeaponSimple weapon,PlayerAvatar p,WeaponAddonCommon_Tempest tempest,int stack)
        {
            var hit=TempestHit(weapon,p,tempest,stack);
            return ProjectileDamageProfiles.DescribeMelee(p,hit,tempest.tempestFireData);
        }
        internal static Hit TempestHit(WeaponSimple weapon,PlayerAvatar p,WeaponAddonCommon_Tempest tempest,int stack,NewWeaponFireData selected=null)
        {
            var fire=selected?selected:tempest.tempestFireData;
            var hit=SpecialHit(weapon,p,fire,0);
            var factors=new List<float>(hit.Factors);
            // The creation callback replaces defaultDamageRatio with the consumed
            // stack; it does not multiply the prefab's original ratio by it.
            if(fire is NewWeaponFireData_MeleeAttack)
                ProjectileDamageProfiles.PrepareMelee(hit,fire,stack);
            else factors.Add(stack);
            hit.ProjectileDamagePercent=(int)(p.MaxMp/50f)*tempest.mpBonusDamage;
            if(!(fire is NewWeaponFireData_MeleeAttack))hit.Factors=factors.ToArray();
            return hit;
        }
        private static void CloudSlashRange(StringBuilder text,WeaponSimple_Katana weapon,PlayerAvatar p)
        {
            text.AppendLine("구름베기 · 잔류 번개 중첩별 일반 / 치명타");
            if(weapon.cloudSlashFireDatas==null||weapon.cloudSlashFireDatas.Length<8)
            {text.AppendLine("구름베기 단계별 공격 데이터 없음");return;}
            int cap=Math.Max(0,weapon.residualLightningStackMax);
            int current=Mathf.Clamp(weapon.residualLightningStack,0,cap);
            int cost=weapon.SpecialAttackCost;
            int luck=Mathf.Clamp(p.GetCustomStatUnsafe("DARKCLOUDLUCK"),0,100);
            int[] stacks={0,current,cap};string[] labels={"최소 중첩","현재 중첩","최대 중첩"};
            for(int i=0;i<stacks.Length;i++)
            {
                int stack=stacks[i],stage=Mathf.Clamp(stack/Math.Max(1,weapon.residualLightningStackPerStage),0,3);
                text.Append(labels[i]).Append(" ").Append(stack).Append(" · ").Append(stage+1).AppendLine("단계:");
                for(int variant=0;variant<2;variant++)
                {
                    if(variant==0&&luck==100||variant==1&&luck==0)continue;
                    var fire=weapon.cloudSlashFireDatas[stage+variant*4];
                    if(!fire&&variant==1)fire=weapon.cloudSlashFireDatas[stage];
                    if(!fire){text.AppendLine("단계 공격 데이터 없음");continue;}
                    var hit=SpecialHit(weapon,p,fire,cost,p.GetCustomStatUnsafe("DARKCLOUDDAMAGE"));
                    ProjectileDamageProfiles.PrepareMelee(hit,fire);
                    hit.ProjectileDamagePercent=weapon.cloudSlashDamagePercentPerStack*stack;
                    text.Append(variant==0?"일반 발동":"강화 발동").Append(" (").Append(variant==0?100-luck:luck).AppendLine("%):");
                    string description;
                    text.AppendLine(ProjectileDamageProfiles.DescribeWeaponFire(p,hit,fire,out description)?description:ProjectileDamageProfiles.DescribeMelee(p,hit,fire));
                }
            }
            text.Append("사용 MP ").Append(cost).Append(" · 사용 후 중첩 유지 확률 ").Append(Mathf.Clamp(p.GetCustomStatUnsafe("DARKCLOUDKEEP"),0,100)).AppendLine("%");
            if(p.MP<cost)text.AppendLine("현재 MP 부족 · 위 수치는 사용 가능한 경우의 예상 피해");
            text.AppendLine("공격 단계마다 별도 계산 · 무작위 강화는 두 결과를 구분하며 실제 난수를 사용하지 않음");
        }
        private static Hit SpecialHit(WeaponSimple weapon,PlayerAvatar p,NewWeaponFireData fire,int consumed,int? additionalPercent=null)
        {
            object[] args={p,fire.damageElementalType,fire.relatedStatFormula,EDamageElementalType.Normal};
            float raw=(float)Related.Invoke(weapon,args);
            return new Hit{Raw=raw,Element=fire.useElementalTypeFromRelatedStatFormula?(EDamageElementalType)args[3]:fire.damageElementalType,Weapon=true,Factors=new[]{
                1+p.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f,
                1+p.GetCustomStat(ECustomStat.SpecialAttackDamageBonus)/100f,
                1+(additionalPercent??(weapon.owner?weapon.GetAdditionalSpecialAttackDamagePercent(0):0))/100f,
                consumed>0?1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f:1,
                1+p.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f,fire.damageMultiplier,
                fire.CalculateFinalDamageMultiplier(consumed)}};
        }
        private static void MpAttackRange(StringBuilder text,WeaponSimple weapon,PlayerAvatar p,NewWeaponFireData fire)
        {
            if(!fire)return;
            var special=fire as NewWeaponFireData_SpecialProjectile;
            var fury=special && special.projectilePrefab?special.projectilePrefab.GetComponent<SpecialProjectile_FuryMP>():null;
            if(!fury && fire.addDamagePerUsedMP<=0)return;
            text.AppendLine("전체 MP 소비 공격 · 일반 / 치명타");
            int capacity=Math.Max(0,p.MaxMp-DamageStatPreview.ReadReservedMp(p));
            int[] amounts={0,capacity,Math.Max(0,p.MP)};string[] labels={"최소","최대","현재"};
            for(int i=0;i<amounts.Length;i++)
            {
                int amount=amounts[i];var hit=SpecialHit(weapon,p,fire,amount);
                ProjectileDamageProfiles.PrepareMelee(hit,fire);
                WeaponAdditionalDamageProfiles.ApplyCreatedAttack(weapon,fire,2,p,hit);
                if(fury)
                {
                    EDamageElementalType element=fire.damageElementalType;
                    if(fire.useElementalTypeFromRelatedStatFormula)
                    {
                        object[] related={p,fire.damageElementalType,fire.relatedStatFormula,EDamageElementalType.Normal};
                        Related.Invoke(weapon,related);element=(EDamageElementalType)related[3];
                    }
                    text.Append(labels[i]).Append(" (MP ").Append(amount).AppendLine("):");
                    text.AppendLine(ProjectileDamageProfiles.DescribeFury(p,hit,fury,element,amount));
                }
                else
                {
                    string description;
                    text.Append(labels[i]).Append(" (MP ").Append(amount).Append("): ")
                        .AppendLine(ProjectileDamageProfiles.DescribeWeaponFire(p,hit,fire,out description)?description:ProjectileDamageProfiles.DescribeMelee(p,hit,fire));
                }
            }
        }
        private static void WeaponInputAttacks(StringBuilder text,WeaponSimple weapon,PlayerAvatar player,NewWeaponFireData[] original,string label,int kind)
        {
            var attacks=WeaponBuffPreview.Attacks(weapon,original,kind);
            var dagger=kind==2?weapon as WeaponSimple_Dagger:null;
            if(dagger)
            {
                if(DpsTiming.DaggerPrimaryAvailable(dagger,player))
                    WeaponTimedAttacks(text,weapon,player,original,label+(dagger.throwDagger?" · 투척 단검":" · 패리"),kind,attacks,DpsTiming.DaggerPrimary(dagger,player));
                if(DpsTiming.DaggerFuryAvailable(dagger,player))
                    WeaponTimedAttacks(text,weapon,player,original,label+" · 퓨리",kind,attacks,DpsTiming.DaggerFury(dagger,player));
                return;
            }
            var timing=kind==0?DpsTiming.Basic(weapon,player):kind==1?DpsTiming.Dash(weapon,player):DpsTiming.Special(weapon,player);
            WeaponTimedAttacks(text,weapon,player,original,label,kind,attacks,timing);
        }
        private static void WeaponTimedAttacks(StringBuilder text,WeaponSimple weapon,PlayerAvatar player,NewWeaponFireData[] original,string label,int kind,NewWeaponFireData[] attacks,DpsTiming.Cycle timing)
        {
            if(timing.NoDamage)return;
            if(timing.Unavailable==null&&timing.Attacks.Count>0)
            {
                var indices=new List<int>();bool compatible=true;
                foreach(var attack in timing.Attacks)
                {
                    if(attack.Kind!=kind||attack.Index<0||attacks==null||attack.Index>=attacks.Length){compatible=false;break;}
                    if(!indices.Contains(attack.Index))indices.Add(attack.Index);
                }
                if(compatible&&indices.Count>0)
                {
                    WeaponAttacks(text,weapon,player,original,label,kind,false,-1,null,false,indices.Count==1,indices);
                    return;
                }
            }
            WeaponAttacks(text,weapon,player,original,label,kind);
        }
        private static void WeaponAttacks(StringBuilder text, WeaponSimple weapon, PlayerAvatar p, NewWeaponFireData[] attacks, string label, int kind,bool selected=false,int requestedIndex=-1,WeaponAttackSample sample=null,bool fullyManual=false,bool suppressIndex=false,List<int> included=null)
        {
            if(!selected)attacks=WeaponBuffPreview.Attacks(weapon,attacks,kind);
            if(!selected&&kind==0)
            {
                NewWeaponFireData[] alternate;float chance;string variant;
                if(WeaponBuffPreview.AlternateBasic(weapon,p,attacks,out alternate,out chance,out variant))
                {
                    if(chance<100)WeaponAttacks(text,weapon,p,attacks,label+" · 일반 발동 "+(100-chance).ToString("0.###")+"%",kind,true,-1,null,false,suppressIndex,included);
                    WeaponAttacks(text,weapon,p,alternate,label+" · "+variant+" "+chance.ToString("0.###")+"%",kind,true,-1,null,false,suppressIndex,included);
                    return;
                }
            }
            int last=WeaponBuffPreview.FinalCombo(weapon);
            int usedMp=fullyManual?0:WeaponBuffPreview.UsedMp(weapon,kind,p);
            float finalArtifactPercent=kind==0?WeaponAdditionalDamageProfiles.FinalComboArtifactPercent(p):0;
            float finalCriticalChance=kind==0?WeaponAdditionalDamageProfiles.FinalComboCriticalChance(p):0;
            for(int i=requestedIndex<0?0:requestedIndex; i<attacks.Length && (requestedIndex>=0?i==requestedIndex:(included!=null||kind!=0||i<=last)); i++)
            {
                if(included!=null&&!included.Contains(i))continue;
                var attack=attacks[i]; if(!attack) continue;
                if(sample!=null){sample.Fire=attack;sample.UsedMp=usedMp;}
                int shownIndex=included==null?i+1:included.IndexOf(i)+1;
                text.Append(label).Append(attacks.Length > 1&&!suppressIndex ? " " + shownIndex : "").Append(": ");
                if (!(attack is NewWeaponFireData_MeleeAttack) && !(attack is NewWeaponFireData_Bullet) && !(attack is NewWeaponFireData_BulletSpread) && !(attack is NewWeaponFireData_BulletBurst) && !(attack is NewWeaponFireData_SpecialProjectile) && !(attack is NewWeaponFireData_Summon))
                { text.AppendLine("다중/특수 공격 · 조건별 계산 필요"); continue; }
                object[] args = { p, attack.damageElementalType, attack.relatedStatFormula, EDamageElementalType.Normal };
                float raw=(float)Related.Invoke(weapon,args);
                var factors=new List<float>();
                factors.Add(1+p.GetCustomStat(ECustomStat.WeaponDamageBonus)/100f);
                factors.Add(1+p.GetCustomStat(kind==0?ECustomStat.BasicAttackDamageBonus:kind==1?ECustomStat.DashAttackDamageBonus:ECustomStat.SpecialAttackDamageBonus)/100f);
                factors.Add(1+p.GetCustomStat(ECustomStat.FinalWeaponDamage)/100f);
                factors.Add(attack.damageMultiplier);
                var baseFactors=new List<float>(factors);
                if(usedMp>0)baseFactors.Insert(2,1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f);
                baseFactors.Add(attack.CalculateFinalDamageMultiplier(usedMp));
                if(kind==2 && weapon.owner&&!fullyManual)baseFactors.Add(1+weapon.GetAdditionalSpecialAttackDamagePercent(i)/100f);
                if(kind==0 && weapon.owner)baseFactors.Add(1+weapon.GetAdditionalBasicAttackDamagePercent(i)/100f);
                if(kind==0 && (i==last||attack.forceFinalCombo))baseFactors.Add(1+p.GetCustomStatUnsafe("LASTBASICATTACKDAMAGE")/100f);
                var main=new Hit {Raw=raw,Element=attack.useElementalTypeFromRelatedStatFormula?(EDamageElementalType)args[3]:attack.damageElementalType,Weapon=true,Factors=baseFactors.ToArray()};
                var summon=attack as NewWeaponFireData_Summon;
                if(summon)
                {
                    if(sample!=null)sample.Hits=new List<Hit>{main};
                    text.AppendLine(FollowerDamageProfiles.WeaponSummon(summon,main,p));
                    continue;
                }
                ProjectileDamageProfiles.PrepareMelee(main,attack);
                if(kind==0&&attack is NewWeaponFireData_MeleeAttack&&WeaponBuffPreview.Transformed(weapon))
                {
                    foreach(var addon in weapon.addons)
                    {
                        var transform=addon as WeaponAddonCommon_TransformAttackBonus;
                        if(transform)main.AfterFactors+=KeywordDatabase.GetConstValue(transform.transformAttackBonusDamageConstKey)+p.GetCustomStatUnsafe(transform.relatedStatUnsafe)*KeywordDatabase.GetConstValue(transform.relatedStatBonusConstKey)/100f;
                    }
                }
                WeaponAdditionalDamageProfiles.ApplyCreatedAttack(weapon,attack,kind,p,main);
                // The scroll consumes its charge on the first created dash projectile.
                // Its native enhanced fire data is a single melee projectile.
                if(kind==1&&i==0&&attack is NewWeaponFireData_MeleeAttack)
                    main.ProjectileDamagePercent+=WeaponAdditionalDamageProfiles.EnhancedKatanaDashPercent(weapon);
                if(kind==0&&(i==last||attack.forceFinalCombo))main.ProjectileDamagePercent+=finalArtifactPercent;
                if(kind==0&&(i==last||attack.forceFinalCombo))main.ExtraCriticalChance+=finalCriticalChance;
                main=ProjectileDamageProfiles.SelectMeleeDamage(main,attack);
                var hits=new List<Hit>();hits.Add(main);
                string projectileDescription;
                bool projectile=ProjectileDamageProfiles.DescribeWeaponFire(p,main,attack,out projectileDescription);
                var extraText=new StringBuilder();
                foreach(var addon in weapon.addons)
                {
                    var extra=addon as WeaponAddonCommon_AdditionalElementalDamage;if(!extra)continue;
                    var extraFactors=new List<float>(factors);
                    int percent=extra.additionalDamagePercent+p.GetCustomStatUnsafe("ADDITIONALELEMENTALDAMAGEBONUS");
                    var katana=weapon as WeaponSimple_Katana;
                    bool eclipse=katana && weapon.owner && WeaponBuffPreview.EclipseBuff(weapon);
                    bool hasEclipse=false;foreach(var other in weapon.addons)if(other is WeaponAddonKatana_FlameSword_Eclipse)hasEclipse=true;
                    eclipse &= hasEclipse;
                    if(eclipse)percent+=KeywordDatabase.GetConstValue("katanaEclipseElementalDamageBonusPercent");
                    extraFactors.Add(percent/100f);
                    if(eclipse&&kind==0)extraFactors.Add(1+p.GetCustomStatUnsafe("FLAMESWORDDAMAGE")/100f);
                    var hit=new Hit {Raw=p.GetCustomStat(extra.statId),Element=extra.elementalType,Factors=extraFactors.ToArray(),ExtraCritical=eclipse&&kind==0?p.GetCustomStatUnsafe("FLAMESWORDCRITICALDAMAGERATE"):0};
                    hits.Add(hit);
                    string element=extra.elementalType==EDamageElementalType.Fire?"화염":extra.elementalType==EDamageElementalType.Ice?"냉기":extra.elementalType==EDamageElementalType.Lightning?"번개":"속성";
                    if(projectile)extraText.Append("  + ").Append(element).Append(" 추가타: ").AppendLine(Hits(p,hit));
                }
                int linkedStart=hits.Count;
                AttackLinkedArtifactDamage.AppendToWeaponAttack(p,hits);
                if(projectile)for(int linked=linkedStart;linked<hits.Count;linked++)extraText.Append("  + ").AppendLine(Hits(p,hits[linked]));
                if(sample!=null)sample.Hits=hits;
                if(!projectile)text.AppendLine(hits.Count>1?ProjectileDamageProfiles.DescribeMelee(p,hits.ToArray(),attack):ProjectileDamageProfiles.DescribeMelee(p,main,attack));
                else
                {
                    text.AppendLine(projectileDescription);
                    text.Append(extraText);
                    if(hits.Count>1)text.AppendLine("  속성 추가타는 별도 판정 · 본타의 폭발·관통·연사 횟수와 구분");
                }
            }
        }
        internal sealed class WeaponAttackSample
        {
            internal WeaponSimple Weapon;
            internal NewWeaponFireData Fire;
            internal int UsedMp,Kind,Index;
            internal List<Hit> Hits;
        }
        internal static WeaponAttackSample CaptureWeaponAttack(WeaponSimple weapon,PlayerAvatar player,int kind,int index,NewWeaponFireData forced=null,bool fullyManual=false)
        {
            if(capturing==null)throw new InvalidOperationException("Weapon DPS requires a damage snapshot");
            if(fullyManual)
            {
                var manual=new WeaponAttackSample{Weapon=weapon,Kind=kind,Index=index};
                if(forced)WeaponAttacks(new StringBuilder(),weapon,player,new[]{forced},"",kind,true,0,manual,true);
                return manual;
            }
            var original=kind==0?weapon.basicComboAttacks:kind==1?weapon.dashAttacks:weapon.specialAttacks;
            var sample=new WeaponAttackSample{Weapon=weapon,Kind=kind,Index=index};
            if(original==null||original.Length==0)return sample;
            // Resolve the animation's raw fire index, including sparse indices
            // such as staff 10/50, before clamping any replacement fire array.
            var requested=new NewWeaponFireData[Math.Max(original.Length,index+1)];
            for(int i=0;i<requested.Length;i++)requested[i]=original[Math.Min(i,original.Length-1)];
            var selected=WeaponBuffPreview.Attacks(weapon,requested,kind);
            if(selected==null||selected.Length==0)return sample;
            var padded=new NewWeaponFireData[Math.Max(selected.Length,index+1)];
            for(int i=0;i<padded.Length;i++)padded[i]=selected[Math.Min(i,selected.Length-1)];
            var staff=weapon as WeaponSimple_QuartterStaff;
            if(staff&&kind==2&&index==10&&!weapon.owner&&!staff.overrideSpecialAttackAddon)
            {
                bool enhanced=(bool)AccessTools.Field(typeof(WeaponSimple_QuartterStaff),"isSecondSpecialAttackEnhanced").GetValue(staff);
                padded[index]=enhanced?staff.secondSpecialAttackEnhancedFireData:staff.secondSpecialAttackFireData;
            }
            if(forced)padded[index]=forced;
            WeaponAttacks(new StringBuilder(),weapon,player,padded,"",kind,true,index,sample);
            return sample;
        }
        public static string ArtifactText(ItemEntity entity, Charm_Basic live, int level, PlayerAvatar p)
        {
            if(!entity || !entity.resourcePrefab || !p) return "";
            var source=entity.resourcePrefab.GetComponent<Charm_Basic>(); if(!source) return "";
            string key=Key("A"+entity.id+":"+level+":"+(live ? live.GetInstanceID() : 0)+":"+(live && live.IsEffectEnabled),p);
            string cached; if(capturing==null && Cache.TryGetValue(key,out cached)) { CacheHits++; return cached; }
            string result;
            if(ArtifactDamageProfiles.TryDescribe(source,live,level,p,out result,entity)) { }
            else if(source is Charm_GoldIsDamage)
            {
                var gold=(Charm_GoldIsDamage)source;int step=gold.damagePercentByLevel[gold.LevelToIdx(level)];
                result="\n자원 비례 피해 증가\n최소: +0% (나뭇잎 0개)\n최대: 고정 상한 없음\n현재: +"+(p.Money/gold.perGold*step)+"%\n나뭇잎 "+gold.perGold+"개당 +"+step+"%\n활성 장비의 보너스는 공격 피해에 이미 합산";
            }
            else if(source is Charm_IncreaseAllDamageByHP)
            {
                var hp=(Charm_IncreaseAllDamageByHP)source;int bonus=hp.damagePercentByLevel[hp.LevelToIdx(level)];
                result="\n체력 조건부 피해 증가\n최소: +0%\n최대: +"+bonus+"% (HP "+hp.overHpPercent+"% 이상)\n현재 조건: "+(DamageStatPreview.ReadHp(p)/p.MaxHp>=hp.overHpPercent/100f?"충족":"미충족")+"\n활성 장비의 보너스는 공격 피해에 이미 합산";
            }
            else if(!(source is IAttackableCharm)) result="\n\n<color=#81E6C4>능력치/효과형 아티팩트</color>\n속성 보너스는 현재 공격 피해에 합산됩니다.";
            else if(source is Charm_Magic || source is Charm_AttackSummon || source is Charm_FlameBall || source is Charm_FlameSwordFall || source is Charm_LeadNPC || source is Charm_LakeSpirit || source is Charm_ShadowEye || source is Charm_NearMagicBullet || source is Charm_SummonUnit)
                result="\n\n<color=#81E6C4>조건부/소환 공격</color>\n공격 종류와 발동 조건별 피해가 다릅니다.";
            else
            {
                Charm_Basic calculator=live;
                if(!calculator || calculator.CurrentLevelToIdx()!=source.LevelToIdx(level))
                {
                    if(!previewRoot) { previewRoot=new GameObject("DamageTooltipPreview"); previewRoot.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(previewRoot); }
                    if(!previewCharm || previewId!=entity.id)
                    {
                        if(previewCharm) UnityEngine.Object.Destroy(previewCharm.gameObject);
                        previewCharm=UnityEngine.Object.Instantiate(entity.resourcePrefab,previewRoot.transform).GetComponent<Charm_Basic>(); previewId=entity.id;
                    }
                    calculator=previewCharm;
                    AccessTools.Field(typeof(Charm_Basic),"Avatar").SetValue(calculator,p);
                    AccessTools.Field(typeof(Charm_Basic),"limitedEffectEnabledLevel").SetValue(calculator,source.LevelToIdx(level));
                }
                float raw=((IAttackableCharm)calculator).GetDamage(p);
                int bonus=live && live.netId!=0 ? live.RequestCharmDamageBonusOnRoot() : p.GetCustomStatUnsafe("CHARMDAMAGEBONUS");
                raw *= 1f+bonus/100f;
                if(calculator is Charm_IceSword)raw*=1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")/100f;
                result=Heading+Hits(p,new Hit {Raw=raw,ExtraCritical=calculator is Charm_TuningForks?p.GetCustomStat(ECustomStat.MagicCriticalDamageBonus):0})+"\n<color=#B9C4D4>발동 기본 1타 · 적 방어/추가타 제외</color>";
            }
            if(live && !live.IsEffectEnabled)
                result="<color=#EDB14D>현재 발동 조건 미충족 · 아래 장비 효과는 현재 적용되지 않음</color>\n"+result;
            return Remember(key,result);
        }
        internal static void RenderWeapon(UI_WeaponTooltip ui)
        {
            var view=ui.GetComponent<DamageTooltipView>()??ui.gameObject.AddComponent<DamageTooltipView>();
            view.Bind(ui,ui.debugInfoText,0);
        }
        internal static void RenderArtifact(UI_CharmTooltip ui,ITooltip data,int offset)
        {
            var view=ui.GetComponent<DamageTooltipView>()??ui.gameObject.AddComponent<DamageTooltipView>();
            view.BindArtifact(ui,data,(TMP_Text)CharmText.GetValue(ui),offset);
        }
        internal static void RenderCombo(UI_SynergyTooltip ui,SynergyTooltipData data)
        {
            var view=ui.GetComponent<DamageTooltipView>()??ui.gameObject.AddComponent<DamageTooltipView>();
            view.BindCombo(ui,data);
        }
    }
    [HarmonyPatch(typeof(UI_WeaponTooltip),"Open")]
    internal static class WeaponDamageTooltipPatch
    {
        private static void Postfix(UI_WeaponTooltip __instance) { try { DamageTooltip.RenderWeapon(__instance); } catch(Exception e) { Debug.LogWarning("Damage tooltip: "+e.Message); } }
    }
    [HarmonyPatch(typeof(UI_CharmTooltip),"UpdateData")]
    internal static class ArtifactDamageTooltipPatch
    {
        private static void Postfix(UI_CharmTooltip __instance, ITooltip data, int virtualLevelOffset) { try { DamageTooltip.RenderArtifact(__instance,data,virtualLevelOffset); } catch(Exception e) { Debug.LogWarning("Artifact damage tooltip: "+e.Message); } }
    }
    [HarmonyPatch(typeof(UI_SynergyTooltip),"Open")]
    internal static class ComboDamageTooltipPatch
    {
        private static void Postfix(UI_SynergyTooltip __instance,ITooltip data)
        {
            var combo=data as SynergyTooltipData;
            if(combo!=null)try { DamageTooltip.RenderCombo(__instance,combo); } catch(Exception e) { Debug.LogWarning("Combo damage tooltip: "+e.Message); }
        }
    }
    [HarmonyPatch(typeof(UnitAvatar),"OnCustomStatChanged")]
    internal static class TooltipStatChangedPatch { private static void Postfix(UnitAvatar __instance) { DamageTooltip.Invalidate(__instance); } }
    [HarmonyPatch(typeof(UnitAvatar),"OnCustomStatAmpChanged")]
    internal static class TooltipStatAmpChangedPatch { private static void Postfix(UnitAvatar __instance) { DamageTooltip.Invalidate(__instance); } }
}
