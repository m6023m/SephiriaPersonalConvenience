using System;
using System.Text;
using UnityEngine;
using HarmonyLib;

namespace SephiriaDicePreview
{
    internal static class ChargingDamageProfiles
    {
        private struct Payment
        {
            internal float Multiplier,Flat;
            internal int Spent,Remaining;
        }
        private static Payment Pay(PlayerAvatar p,int mp,bool repeat)
        {
            var value=new Payment{Multiplier=1,Remaining=mp};
            bool infinite=p.GetCustomStatUnsafe("INFINITYMP")>0;
            int bonus=p.GetCustomStatUnsafe("FROSTRELICMPDAMAGE");
            float skill=1+p.GetCustomStatUnsafe("MPSKILLDAMAGE")*.01f;
            if(bonus>0&&value.Remaining>=4)
            {
                if(!infinite){value.Remaining-=4;value.Spent+=4;}
                value.Multiplier=(1+bonus*.01f)*skill;
            }
            if(p.GetCustomStatUnsafe("FROSTRELICMPMAXMPDAMAGE")>0&&value.Remaining>=4)
            {
                if(!infinite){value.Remaining-=4;value.Spent+=4;}
                // The native no-count retrigger uses literal 50; the initial cast uses the database.
                int basis=repeat?50:KeywordDatabase.GetConstValue("PLAYERDEFAULTMP");
                value.Flat=Math.Max(0,p.MaxMp-basis)*skill;
            }
            return value;
        }
        internal static string AirSlash(Charm_AirSlash c,Charm_Basic live,int level,PlayerAvatar p)
        {
            bool fire=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            bool small=p.GetCustomStatUnsafe("AIRSLASHMINI")>0;
            var prefab=fire?(small?c.bulletPrefab_Flame_Small:c.bulletPrefab_Flame):(small?c.bulletPrefab_Small:c.bulletPrefab);
            float root=1+(live&&live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f;
            float raw=c.defaultDamage+p.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f;
            var text=new StringBuilder((fire?"화염":"냉기")+" 참격 · 투사체 1발 · 일반 / 치명타\n");
            Action<string,int,bool> append=(label,mp,repeat)=>
            {
                var pay=Pay(p,mp,repeat);
                var hit=new DamageTooltip.Hit{Raw=raw,Factors=new[]{1+p.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,1+p.GetCustomStatUnsafe("AIRSLASHDAMAGE")/100f,root,pay.Multiplier},AfterFactors=pay.Flat};
                hit.Element=fire?EDamageElementalType.Fire:EDamageElementalType.Ice;
                text.AppendLine(ProjectileDamageProfiles.Describe(p,hit,prefab,label));
                text.Append("  MP ").Append(mp).Append(" → ").Append(pay.Remaining).Append(" · 소모 ").Append(pay.Spent).AppendLine();
            };
            append("MP 0 기준",0,false);
            append("현재 MP 기준",p.MP,false);
            append("MP 가득 찬 상태",Math.Max(0,p.MaxMp),false);
            int count=Math.Max(0,1+p.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"));
            text.Append("한 번 발동 시 ").Append(count).AppendLine("발 · 발사 간격 0.25초 · 실제 적중한 발만 합산");
            int step=KeywordDatabase.GetConstValue("chargingCharmRetriggerByAttackSpeed");
            int chance=step>0&&p.GetCustomStatUnsafe("ATTACKSPEED")>0?p.GetCustomStatUnsafe("ATTACKSPEED")/step*p.GetCustomStatUnsafe("CHARGINGCHARMRETRIGGERBYATTACKSPEED"):0;
            if(chance>0)
            {
                var first=Pay(p,p.MP,false);
                append("추가 발동 1발 (다른 MP 변화 없음)",first.Remaining,true);
                text.Append("추가 발동 확률 ").Append(Math.Min(100,chance)).AppendLine("% · 추가 발동은 다시 추가 발동하지 않음");
            }
            text.Append("평타 동작으로 충전 효과 발동 · MP 보정은 순서대로 각각 MP 4 확인\n최대 MP 비례 항은 비율 보정 뒤 더함 · 이 효과 자체의 고정 피해 상한 없음");
            return text.ToString();
        }
        private static string Ranges(PlayerAvatar p,Charm_Basic live,float raw,float extraFactor,GameObject prefab,string label,float triggerFactor=1,bool noCount=false,bool afterExtraHammer=false)
        {
            float root=1+(live&&live.netId!=0?live.RequestCharmDamageBonusOnRoot():p.GetCustomStatUnsafe("CHARMDAMAGEBONUS"))/100f;
            var text=new StringBuilder(label+" · 투사체 1발 · 일반 / 치명타\n");
            string[] names={"MP 0","현재 MP","MP 가득"};int[] resources={0,p.MP,Math.Max(0,p.MaxMp)};
            for(int i=0;i<resources.Length;i++)
            {
                int mp=resources[i];
                if(afterExtraHammer)mp=Pay(p,mp,true).Remaining;
                var pay=Pay(p,mp,noCount);
                var hit=new DamageTooltip.Hit{Raw=raw,Factors=new[]{1+p.GetCustomStatUnsafe("FROSTRELICDAMAGE")/100f,extraFactor,root,triggerFactor,pay.Multiplier},AfterFactors=pay.Flat};
                hit.Element=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0?EDamageElementalType.Fire:EDamageElementalType.Ice;
                text.AppendLine(ProjectileDamageProfiles.Describe(p,hit,prefab,names[i]));
                text.Append("  MP ").Append(mp).Append(" → ").Append(pay.Remaining).AppendLine();
            }
            text.Append("충전 증폭 횟수 ").Append(Math.Max(0,1+p.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"))).AppendLine("회 · 실제 적중 횟수만 합산");
            text.AppendLine("최대 MP 비례 항은 나머지 비율 보정 뒤 더함");
            return text.ToString();
        }
        internal static string Spear(Charm_IceSpear c,Charm_Basic live,int level,PlayerAvatar p)
        {
            bool fire=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            float raw=c.defaultDamage+p.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f;
            int upgrade=p.GetCustomStatUnsafe("VOLUSPAUPGRADE");float factor=upgrade>0?(100+upgrade)/100f:1;
            var prefab=fire?c.bulletPrefab_Flame:c.bulletPrefab;
            string text=Ranges(p,live,raw,factor,prefab,fire?"화염 창":"냉기 창");
            text+="증폭 1회당 "+c.fireCountByLevel.SafeRandomAccess(c.LevelToIdx(level))+"발 · 창 사이 0.25초, 다음 묶음 전 0.25초\n전투 중 충전 완료 시 자동 발동\n";
            if(p.GetCustomStatUnsafe("ICESPEARWITHWEAPONATTACK")>0)
                text+=Ranges(p,live,raw,factor,prefab,"무기 적중 누적 발동",1,true)+"무기 직접 적중 "+KeywordDatabase.GetConstValue("staffAttackToActiveIceSpearCount")+"회마다 충전 대기와 별도로 발동\n";
            return text;
        }
        internal static string Bow(Charm_IceBow c,Charm_IceBow live,int level,PlayerAvatar p)
        {
            bool fire=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0;
            float raw=c.defaultDamage+p.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f;
            int shots=Math.Max(0,1+p.GetCustomStatUnsafe("CHARGINGCHARMAMPLIFY"));
            return Ranges(p,live,raw,1,fire?c.bulletPrefab_Flame:c.bulletPrefab,fire?"화염 활":"냉기 활")+
                "장전량 최소 0 / 현재 "+(live?live.readyArrowCount:0)+" / 최대 "+c.arrowReloadLimit+"\n장전 1개당 "+shots+"발 · 최대 장전 시 "+(c.arrowReloadLimit*shots)+"발\n장전 소모 간격 "+c.fireInterval.ToString("0.###")+"초 · 발동 전체에 MP 보정을 한 번 계산\n분산 화살은 실제 맞힌 발만 합산";
        }
        internal static string Hammer(Charm_IceHammer c,Charm_Basic live,int level,PlayerAvatar p)
        {
            bool fire=p.GetCustomStatUnsafe("FROSTRELICFLAME")>0,scythe=p.GetCustomStatUnsafe("ICEHAMMERSCYTHE")>0;
            float raw=c.defaultDamage+p.GetCustomStat(fire?ECustomStat.FireDamage:ECustomStat.IceDamage)*c.damagePercentByLevel.SafeRandomAccess(c.LevelToIdx(level))/100f;
            var prefab=scythe?(fire?c.bulletPrefab_Scythe_Flame:c.bulletPrefab_Scythe):(fire?c.bulletPrefab_Flame:c.bulletPrefab);
            bool extra=p.GetCustomStatUnsafe("DASHATTACKICEHAMMER")>0;
            string text="추가 공격 없이 충전 완료 공격\n"+Ranges(p,live,raw,scythe?0.5f:1,prefab,scythe?"낫":"망치");
            if(extra)
            {
                var smaller=scythe?prefab:(fire?c.bullet75Prefab_Flame:c.bullet75Prefab);
                text+="돌진 비용을 낸 경우 먼저 생성되는 추가 공격\n"+Ranges(p,live,raw,scythe?0.5f:1,smaller,"추가 공격",.6f,true);
                text+=Ranges(p,live,raw,scythe?0.5f:1,prefab,"추가 공격 뒤 충전 완료 공격",1,false,true);
                text+="기본 충전 공격의 현재 MP는 추가 공격 비용을 먼저 차감한 기준\n";
            }
            return text+"낫 전환 시 원시 피해 50% · 최대 MP로 더하는 피해에는 적용하지 않음\n증폭된 공격의 발사 간격 0.25초 · 각 투사체의 실제 적중 횟수만 합산";
        }
    }
    [HarmonyPatch(typeof(Charm_IceBow),"HookReadyArrowCount")]
    internal static class TooltipIceBowAmmoChangedPatch
    {
        private static void Postfix(Charm_IceBow __instance,int oldCount,int newCount)
        {
            if(oldCount!=newCount&&__instance.NetworkAvatar==DamageTooltip.Player)DamageTooltip.Invalidate(DamageTooltip.Player);
        }
    }
}
