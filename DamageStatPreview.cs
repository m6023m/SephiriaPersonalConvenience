using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SephiriaDicePreview
{
    // Only active during synchronous snapshot capture. Never writes avatar stats or applies a buff.
    internal sealed class DamageStatPreview : IDisposable
    {
        internal const string MaxMpDeltaKey="__PREVIEW_MAX_MP";
        internal const string ReservedMpDeltaKey="__PREVIEW_RESERVED_MP";
        internal const string MoveSpeedPercentDeltaKey="__PREVIEW_MOVE_SPEED_PERCENT";
        internal const string MaxHpDeltaKey="__PREVIEW_MAX_HP";
        internal const string FinalHpDeltaKey="__PREVIEW_FINAL_HP";
        [ThreadStatic] private static DamageStatPreview current;
        private readonly UnitAvatar avatar;
        private readonly Dictionary<string,int> added;
        private bool disposed;
        private int? remainingMp;
        private float? hp,maxHp;
        private Charm_Magic lastMagic;
        private bool hasLastMagic;
        private IDictionary<int,int> tuningForkStacks;
        internal void SetTuningForkStacks(IDictionary<int,int> values){tuningForkStacks=values;}
        internal static int ReadTuningForkStack(UnitAvatar target,Charm_TuningForks fork,int nativeValue)
        {
            var preview=current;int value;
            return preview!=null&&preview.avatar==target&&fork&&preview.tuningForkStacks!=null&&preview.tuningForkStacks.TryGetValue(fork.GetInstanceID(),out value)?value:nativeValue;
        }
        internal void SetLastMagic(Charm_Magic value){lastMagic=value;hasLastMagic=true;}
        internal static Charm_Magic ReadLastMagic(UnitAvatar target,Charm_Magic nativeValue)
        {
            var preview=current;
            return preview!=null&&preview.avatar==target&&preview.hasLastMagic?preview.lastMagic:nativeValue;
        }
        internal void SetRemainingMp(int value){remainingMp=value;}
        internal static int ReadReservedMp(UnitAvatar target)
        {
            var preview=current;int delta;
            return preview!=null&&preview.avatar==target&&preview.added.TryGetValue(ReservedMpDeltaKey,out delta)?target.reservedMp+delta:target.reservedMp;
        }
        internal void SetHealth(float value,float maximum){hp=value;maxHp=maximum;}
        internal static float ReadHp(UnitAvatar target)
        {
            var preview=current;
            if(preview==null||preview.avatar!=target)return target.hp;
            if(preview.hp.HasValue)return preview.hp.Value;
            if(target.isHPCursed>0)return target.hp;
            int baseDelta,percentDelta;
            preview.added.TryGetValue(MaxHpDeltaKey,out baseDelta);preview.added.TryGetValue(FinalHpDeltaKey,out percentDelta);
            if(baseDelta==0&&percentDelta==0)return target.hp;
            float original=target.maxHp+target.finalMaxHp*target.maxHp/100f;
            return original!=0?target.MaxHp*Math.Min(1f,target.hp/original):target.hp;
        }
        internal static float ReadMaxHp(UnitAvatar target,float nativeValue)
        {
            var preview=current;
            if(preview==null||preview.avatar!=target)return nativeValue;
            if(preview.maxHp.HasValue)return preview.maxHp.Value;
            if(target.isHPCursed>0)return nativeValue;
            int baseDelta,percentDelta;
            preview.added.TryGetValue(MaxHpDeltaKey,out baseDelta);preview.added.TryGetValue(FinalHpDeltaKey,out percentDelta);
            if(baseDelta==0&&percentDelta==0)return nativeValue;
            float value=target.maxHp+baseDelta;
            return value+(target.finalMaxHp+percentDelta)*value/100f;
        }
        internal static int ReadMp(UnitAvatar target,int nativeValue)
        {
            var preview=current;
            return preview!=null&&preview.avatar==target&&preview.remainingMp.HasValue?preview.remainingMp.Value:nativeValue;
        }
        internal static int ReadMaxMp(UnitAvatar target,int nativeValue)
        {
            var preview=current;int delta;
            if(preview==null||preview.avatar!=target||!preview.added.TryGetValue(MaxMpDeltaKey,out delta)||delta==0||target.GetCustomStatUnsafe("INFINITYMP")>0)return nativeValue;
            int value=target.maxMp+delta;
            return value+(int)((float)(target.GetCustomStat(ECustomStat.FinalMP)*value)/100f);
        }
        internal DamageStatPreview(UnitAvatar target,IDictionary<string,int> changes)
        {
            if(current!=null)throw new InvalidOperationException("Nested damage stat preview");
            if(!target)throw new ArgumentNullException("target");
            avatar=target;added=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var entry in changes)
            {
                string key=entry.Key.ToUpperInvariant();int old;
                added.TryGetValue(key,out old);added[key]=old+entry.Value;
            }
            current=this;
        }
        internal static int Read(UnitAvatar target,string id,int nativeValue)
        {
            var preview=current;int delta;
            if(preview==null||preview.avatar!=target||!preview.added.TryGetValue(id,out delta)||delta==0)return nativeValue;
            int value=target.GetCustomBaseStatUnsafe(id)+delta;
            if(value!=0)value=(int)(value*(100+target.GetCustomStatAmp(id))/100f);
            return value;
        }
        public void Dispose()
        {
            if(disposed)return;
            if(current!=this)throw new InvalidOperationException("Damage preview disposed outside its capture thread");
            current=null;disposed=true;
        }
    }
    [HarmonyPatch(typeof(UnitAvatar),"GetRawStatUnsafe")]
    internal static class DamageStatPreviewReadPatch
    {
        private static void Postfix(UnitAvatar __instance,string id,ref int __result)
        {
            __result=DamageStatPreview.Read(__instance,id,__result);
        }
    }
    [HarmonyPatch(typeof(UnitAvatar),"get_MP")]
    internal static class DamageStatPreviewMpPatch
    {
        private static void Postfix(UnitAvatar __instance,ref int __result){__result=DamageStatPreview.ReadMp(__instance,__result);}
    }
    [HarmonyPatch(typeof(UnitAvatar),"get_MaxMp")]
    internal static class DamageStatPreviewMaxMpPatch
    {
        private static void Postfix(UnitAvatar __instance,ref int __result){__result=DamageStatPreview.ReadMaxMp(__instance,__result);}
    }
    [HarmonyPatch(typeof(UnitAvatar),"get_MaxHp")]
    internal static class DamageStatPreviewMaxHpPatch
    {
        private static void Postfix(UnitAvatar __instance,ref float __result){__result=DamageStatPreview.ReadMaxHp(__instance,__result);}
    }
    [HarmonyPatch(typeof(UnitAvatar),"get_HpRatio")]
    internal static class DamageStatPreviewHpRatioPatch
    {
        private static void Postfix(UnitAvatar __instance,ref float __result)
        {
            // Native code reads the hp field directly, so recompute the ratio in preview scope.
            if(DamageStatPreview.ReadHp(__instance)!=__instance.hp)__result=DamageStatPreview.ReadHp(__instance)/__instance.MaxHp;
        }
    }
}
