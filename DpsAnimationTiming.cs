using System;

namespace SephiriaDicePreview
{
    // Shared by weapon sequences and magic. The generated state data supplies
    // clip length, state speed, parameter use and event timestamps.
    internal static class DpsAnimationTiming
    {
        internal static double BasicSpeed(WeaponSimple weapon,PlayerAvatar player)
        {
            int fixedSpeed=player.GetCustomStatUnsafe("FIXEDATTACKSPEED");
            return fixedSpeed>0?fixedSpeed/100d:
                (100+player.GetCustomStat(ECustomStat.AttackSpeed)*(1+weapon.attackSpeedAmplify))/100d;
        }

        internal static bool Scale(DpsMotions.Motion motion,double animatorSpeed,double attackSpeed,out double scale)
        {
            scale=animatorSpeed*motion.StateSpeed*(motion.AttackSpeed?attackSpeed:1);
            return DpsNumbers.Finite(scale)&&scale>0;
        }

        internal static string MagicSeconds(int layer,double animatorSpeed,double attackSpeed,out double seconds)
        {
            seconds=0;
            foreach(string suffix in MagicSequence)
            {
                DpsMotions.Motion selected=null;
                foreach(var entry in DpsMotions.States)
                    if(entry.Key.StartsWith(layer+":",StringComparison.Ordinal)&&entry.Key.EndsWith(suffix,StringComparison.Ordinal))
                    {if(selected!=null)return "마법 시전 상태 선택 연결 필요";selected=entry.Value;}
                if(selected==null)return "마법 시전 동작 시간 없음: "+suffix;
                double scale;
                if(!Scale(selected,animatorSpeed,attackSpeed,out scale))return "마법 시전 동작이 진행되지 않음";
                double duration=selected.Seconds;
                if(suffix=="_Magic_LoopCharging")
                {
                    bool found=false;
                    foreach(var ev in selected.MagicEvents)if(ev.Name=="MagicEventAnimation_CheckChannelingMagic")
                    {duration=ev.Time;found=true;break;}
                    if(!found)return "마법 발사 전환 이벤트 없음";
                }
                seconds+=duration/scale;
            }
            return null;
        }

        private static readonly string[] MagicSequence={"_Magic_StartCharge","_Magic_LoopCharging","_Magic_StartFire","_Magic_LoopFire","_Magic_StopFire"};
    }
}
