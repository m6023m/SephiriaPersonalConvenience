using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;
using SephiriaDicePreview;

[BepInPlugin("local.sephiria.damage-tooltip-smoke","Selected Damage Checks","1.0.0")]
public sealed class SelectedDamageSmoke : BaseUnityPlugin
{
    [Serializable] public sealed class Case { public string id,kind,operation;public int[] artifacts,damage_artifacts;public int item_id,extra_critical,element_critical;public bool direct,critical,elemental_effect,execution,elite,heavy,inherit_element,force_chaos;public float additional_damage,projectile_percent,critical_multiplier,buff_amplified; }
    [Serializable] public sealed class Entry { public Case @case; }
    [Serializable] public sealed class Plan { public Entry[] selected; }
    [Serializable] public sealed class Outcome { public string id,status,evidence; }
    [Serializable] public sealed class Results { public string plan_sha256;public bool complete;public List<Outcome> results=new List<Outcome>(); }
    private string root,runDirectory;
    private Plan plan;
    private Results results;
    private PlayerAvatar Player {get{return PlayerSpawner.MultiplayerList.Where(x=>x&&x.isLocalPlayer).Select(x=>x.PlayerAvatar).FirstOrDefault();}}
    private void Awake()
    {
        root=Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
        string planPath=Environment.GetEnvironmentVariable("SEPHIRIA_DAMAGE_VALIDATION_PLAN");
        if(string.IsNullOrEmpty(root)||string.IsNullOrEmpty(planPath)||Path.GetFullPath(SaveData.CommonPath)!=Path.Combine(Path.GetFullPath(root),"isolated-saves"))throw new Exception("Isolated validation environment required");
        runDirectory=Path.GetDirectoryName(Path.GetFullPath(planPath));
        if(!runDirectory.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Validation output outside fixture");
        byte[] bytes=File.ReadAllBytes(planPath);
        plan=Newtonsoft.Json.JsonConvert.DeserializeObject<Plan>(Encoding.UTF8.GetString(bytes));
        if(plan==null||plan.selected==null||plan.selected.Any(x=>x==null||x.@case==null))throw new Exception("Invalid validation plan entries");
        using(var hash=SHA256.Create())results=new Results{plan_sha256=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()};
        StartCoroutine(Run());
    }
    private IEnumerator Run()
    {
        float deadline=Time.realtimeSinceStartup+100;
        while((!Player||!Player.CanMove||Player.loadingScreenType!=-1)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!Player||!Player.CanMove||Player.loadingScreenType!=-1)
        {File.WriteAllText(Path.Combine(runDirectory,"startup-error.txt"),"Isolated player did not become ready");Application.Quit(21);yield break;}
        LocalizationManager.Instance.LoadLanguage("ko-KR");
        int index=0;
        foreach(var entry in plan.selected)
        {
            var c=entry.@case;
            var outcome=new Outcome{id=c.id,status="failed",evidence=Path.Combine(runDirectory,"case-"+(index++)+".txt")};
            Exception preparation=null;
            if(c.operation=="loadout")
            {
                try { PrepareLoadout(c); } catch(Exception e) { preparation=e; }
                yield return new WaitForSeconds(1.5f);
            }
            try
            {
                if(preparation!=null)throw preparation;
                string evidence;
                if(c.operation=="loadout")evidence=Loadout(c);
                else if(c.kind=="catalog"&&(c.operation=="weapon-catalog"||c.operation=="artifact-catalog"))evidence=Catalog(c);
                else if(c.kind=="accuracy"&&c.operation=="native-pipeline")evidence=NativePipeline(c);
                else if(c.kind=="accuracy"&&c.operation=="native-follower-pipeline")evidence=NativeFollowerPipeline(c);
                else if(c.kind=="accuracy"&&c.operation=="native-explosion")evidence=NativeExplosion(c);
                else if(c.kind=="accuracy"&&c.operation=="native-lightning-armor")evidence=NativeLightningArmor(c);
                else throw new NotSupportedException("No executable check registered: "+c.id);
                File.WriteAllText(outcome.evidence,"case="+c.id+"\nkind="+c.kind+"\n"+evidence);
                outcome.status="passed";
            }
            catch(Exception error){File.WriteAllText(outcome.evidence,error.ToString());}
            results.results.Add(outcome);
            SaveResults();
            yield return null;
        }
        results.complete=true;SaveResults();
        Application.Quit(results.results.All(x=>x.status=="passed")?0:21);
    }
    private void SaveResults()
    {
        string path=Path.Combine(runDirectory,"results.json"),temporary=path+".tmp";
        File.WriteAllText(temporary,Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
        if(File.Exists(path))File.Delete(path);
        File.Move(temporary,path);
    }
    private string Catalog(Case c)
    {
        var evidence=new StringBuilder("Catalog rendering only; this is not formula accuracy evidence.\n");
        if(c.operation=="weapon-catalog")
        {
            var entity=Resources.LoadAll<WeaponEntity>("Weapon").Single(x=>x.id==c.item_id&&x.enabled);
            if(!entity.mainWeaponPrefab)throw new Exception("Native weapon prefab missing");
            var weapon=entity.mainWeaponPrefab.GetComponent<WeaponSimple>();
            AppendSnapshot(evidence,DamageTooltip.Capture(delegate{return DamageTooltip.WeaponText(weapon,Player);},Player));
        }
        else
        {
            var entity=Resources.LoadAll<ItemEntity>("Item").Single(x=>x.id==c.item_id&&x.type==EItemType.Charm&&x.activeType!=EItemActiveType.Disabled&&x.activeType!=EItemActiveType.TestOnly);
            var charm=entity.resourcePrefab?entity.resourcePrefab.GetComponent<Charm_Basic>():null;
            if(!charm)throw new Exception("Native artifact component missing");
            for(int level=0;level<=charm.maxLevel;level++)
            {
                int selectedLevel=level;
                evidence.Append("level=").Append(level).AppendLine();
                AppendSnapshot(evidence,DamageTooltip.Capture(delegate{return DamageTooltip.ArtifactText(entity,null,selectedLevel,Player);},Player));
            }
        }
        return evidence.ToString();
    }
    private static void AppendSnapshot(StringBuilder evidence,DamageTooltip.Snapshot snapshot)
    {
        // Run the same pure-number worker used by the dialog, not the synchronous tooltip fallback.
        string text=Task.Run(()=>snapshot.Calculate()).GetAwaiter().GetResult();
        if(string.IsNullOrEmpty(text)||text.Contains("NaN")||text.Contains("Infinity")||text.Contains("{damage:"))throw new Exception("Invalid rendered snapshot");
        evidence.AppendLine(text);
    }
    private readonly Dictionary<string,float> mpBaseline=new Dictionary<string,float>();
    private int mpArtifact;
    private void PrepareLoadout(Case c)
    {
        Player.Inventory.ForceRemoveAll();
        using(new GridInventory.Permission(Player.Inventory))
        {
            var ids=new List<int>(c.artifacts??new int[0]);
            if(c.item_id==1)
            {
                var statIds=new HashSet<string>(Resources.LoadAll<StatusEntity>("Status").Where(x=>x.className.EndsWith("/MPSKILLDAMAGE",StringComparison.OrdinalIgnoreCase)).Select(x=>x.id));
                var entity=Resources.LoadAll<ItemEntity>("Item").Where(x=>x.type==EItemType.Charm&&x.activeType!=EItemActiveType.Disabled&&x.activeType!=EItemActiveType.TestOnly&&x.resourcePrefab)
                    .First(x=>{var charm=x.resourcePrefab.GetComponent<Charm_StatusInstance>();return charm&&charm.stats.Any(s=>statIds.Contains(s.statusID));});
                mpArtifact=entity.id;ids.Add(entity.id);
            }
            var random=new System.Random(711);
            foreach(int id in ids)
                if(!Player.Inventory.LocalAddItem(ItemDatabase.GenerateInstanceID(random),id,1,0,false,false))throw new Exception("Cannot equip artifact "+id);
        }
    }
    private string Loadout(Case c)
    {
        var p=Player;var controller=p.GetComponent<WeaponControllerSimple>();var weapon=controller.currentWeapon;
        if(!weapon)throw new Exception("No equipped weapon");
        var evidence=new StringBuilder("Equipped inventory; calculation/output only, no attack simulation.\n");
        foreach(var entry in p.Inventory.inventoryMatrix.Values)
        {
            if(!entry.Charm)continue;
            evidence.AppendLine(entry.EntityID+" "+entry.Name+" enabled="+entry.Charm.IsEffectEnabled+" levelIndex="+entry.Charm.CurrentLevelToIdx());
            if(!entry.Charm.IsEffectEnabled)throw new Exception("Artifact effect not enabled: "+entry.Name);
            AppendSnapshot(evidence,DamageTooltip.Capture(()=>DamageTooltip.ArtifactText(entry.Entity,entry.Charm,entry.Charm.CurrentLevelToIdx(),p),p));
            if(c.item_id==-3&&(c.damage_artifacts==null||c.damage_artifacts.Contains(entry.EntityID)))
            {
                var current=DamageTooltip.Capture(()=>DamageTooltip.ArtifactText(entry.Entity,entry.Charm,entry.Charm.CurrentLevelToIdx(),p),p);
                var buffed=FullBuffPlan.Capture(p,()=>DamageTooltip.ArtifactText(entry.Entity,entry.Charm,entry.Charm.CurrentLevelToIdx(),p));
                if(current.Rows.Count==0||buffed.Rows.Count==0)throw new Exception("Attack artifact produced no damage rows: "+entry.Name);
                evidence.AppendLine("Full buff artifact: "+entry.Name);
                AppendSnapshot(evidence,buffed);
                evidence.AppendLine("Damage rows current="+current.Rows.Count+" fullBuff="+buffed.Rows.Count);
            }
        }
        evidence.AppendLine("Equipped weapon="+weapon.name+" MP bonus="+p.GetCustomStatUnsafe("MPSKILLDAMAGE"));
        AppendSnapshot(evidence,DamageTooltip.Capture(()=>DamageTooltip.WeaponText(weapon,p),p));
        AppendSnapshot(evidence,FullBuffPlan.Capture(p,()=>DamageTooltip.WeaponText(weapon,p)));
        if(DamageTooltip.DisplayDamage(.7f)!="0"||DamageTooltip.DisplayDamage(.3f)!="0"||DamageTooltip.DisplayDamage(12.99f)!="12")throw new Exception("Damage floor formatting failed");
        var fraction=new DamageTooltip.Snapshot{Template="{damage:0}"};
        fraction.Rows.Add(new[]{new DamageTooltip.Hit{Raw=1.7f},new DamageTooltip.Hit{Raw=1.7f}});
        if(Math.Abs(fraction.Evaluate(fraction.Rows[0][0]).Normal-1.7f)>.0001f)throw new Exception("Internal fraction lost");
        evidence.AppendLine("Floor checks 0.7 -> 0, 0.3 -> 0, 12.99 -> 12; internal fraction retained.");
        if(c.item_id==-2)
        {
            foreach(var entry in p.Inventory.inventoryMatrix.Values)
            {
                if(!entry.Charm)continue;
                var charm=entry.Charm;charm.DisableEffect(charm.CurrentLevelToIdx(),false);
                var snapshot=DamageTooltip.Capture(()=>DamageTooltip.ArtifactText(entry.Entity,charm,charm.CurrentLevelToIdx(),p),p);
                var output=Task.Run(()=>snapshot.Calculate()).GetAwaiter().GetResult();
                if(!output.Contains("현재 발동 조건 미충족"))throw new Exception("Missing disabled effect notice: "+entry.Name);
                evidence.AppendLine("Disabled notice confirmed: "+entry.Name);evidence.AppendLine(output);
            }
        }
        if(c.item_id>=0)
        {
            if(c.item_id==0)
            {
                var great=Resources.LoadAll<WeaponEntity>("Weapon").Where(x=>x.enabled&&x.mainWeaponPrefab).Select(x=>x.mainWeaponPrefab.GetComponent<WeaponSimple_GreatSword>()).First(x=>x);
                var costField=HarmonyLib.AccessTools.Field(typeof(WeaponSimple_GreatSword),"mpBasicAttackCost");
                bool oldUse=great.useMPBasicAttack,oldTransform=great.isTransformed;int oldCost=(int)costField.GetValue(great);
                try
                {
                    if(p.MP<1)throw new Exception("Insufficient fixture MP for paid basic attack control");
                    great.isTransformed=false;great.useMPBasicAttack=true;costField.SetValue(great,1);
                    if(WeaponBuffPreview.UsedMp(great,0,p)!=1)throw new Exception("MP basic attack exception lost");
                    if(WeaponBuffPreview.UsedMp(great,1,p)!=0)throw new Exception("MP basic bonus leaked into dash");
                    great.useMPBasicAttack=false;
                    if(WeaponBuffPreview.UsedMp(great,0,p)!=0)throw new Exception("Free greatsword basic attack paid");
                    evidence.AppendLine("Greatsword paid basic=1, free basic=0, dash=0 confirmed.");
                }
                finally {great.useMPBasicAttack=oldUse;great.isTransformed=oldTransform;costField.SetValue(great,oldCost);}
            }
            if(c.item_id==1&&p.GetCustomStatUnsafe("MPSKILLDAMAGE")<=0)throw new Exception("MP artifact did not activate: "+mpArtifact);
            int saved=controller.mpConsumedAttackTriggerFromAnimationValue;bool leak=false;
            try
            {
                foreach(int consumed in new[]{0,5})
                foreach(int kind in new[]{0,1,2})
                {
                    controller.mpConsumedAttackTriggerFromAnimationValue=consumed;
                    var buffer=new StringBuilder();
                    var snap=DamageTooltip.Capture(()=>{
                        HarmonyLib.AccessTools.Method(typeof(DamageTooltip),"WeaponAttacks").Invoke(null,new object[]{buffer,weapon,p,kind==0?weapon.basicComboAttacks:kind==1?weapon.dashAttacks:weapon.specialAttacks,kind==0?"평타":kind==1?"돌진":"특수",kind,false});return buffer.ToString();},p);
                    if(snap.Rows.Count==0)throw new Exception("No attack result rows");
                    string key=kind+":"+consumed;
                    float value=snap.Evaluate(snap.Rows[0][0]).Normal;
                    if(c.item_id==0)mpBaseline[key]=value;
                    else
                    {
                        float before=mpBaseline[key];
                        evidence.AppendLine("MP bonus artifact="+mpArtifact+" kind="+kind+" previous MP trigger="+consumed+" before="+before+" after="+value);
                        if(kind<2&&Math.Abs(value-before)>.0001f)leak=true;
                        if(kind==2&&consumed==0&&Math.Abs(value-before)>.0001f)leak=true;
                        if(kind==2&&consumed==5&&value<=before)throw new Exception("MP-consuming control did not increase");
                    }
                }
            }
            finally { controller.mpConsumedAttackTriggerFromAnimationValue=saved; }
            if(leak){File.WriteAllText(Path.Combine(runDirectory,"mp-bonus-leak.txt"),evidence.ToString());throw new Exception("MP bonus affected a non-MP attack; see mp-bonus-leak.txt");}
        }
        return evidence.ToString();
    }
    private string NativeLightningArmor(Case c)
    {
        var p=Player;var dummy=FindObjectsOfType<DamageDummy>().FirstOrDefault();
        if(!dummy)throw new Exception("Native damage dummy missing");
        var prefab=Resources.LoadAll<GameObject>("").OrderBy(x=>x.name)
            .Select(x=>x.GetComponent<CharacterBuff_LightningArmor>()).FirstOrDefault(x=>x);
        if(!prefab)throw new Exception("Native lightning armor prefab missing");
        CharacterBuff_LightningArmor buff=null;
        var randomState=UnityEngine.Random.state;
        string[] ids={"ALLDAMAGEBONUS","DEBUFFDAMAGE","TRUEDAMAGE","MAGICCRITICALDAMAGEBONUS","AP","LIGHTNINGDAMAGE","CHARMDAMAGEBONUS"};
        int[] deltas={25,30,3,37,125,175,80};int applied=0;
        int defense=dummy.GetCustomStat(ECustomStat.DamageReduction);bool removedDefense=false;
        var damages=new List<int>();var elements=new List<EDamageElementalType>();
        var fromTypes=new List<EDamageFromType>();var damageTypes=new List<EDamageType>();
        Action<UnitAvatar,DamageInstance> chooseCritical=delegate(UnitAvatar target,DamageInstance damage)
        {
            if(target==dummy&&damage.id=="Buff_LightningArmor")
            {damage.criticalChancePercent=c.critical?100:0;damage.isExecutionAttack=false;}
        };
        Action<UnitAvatar,DamageInstance> observe=delegate(UnitAvatar target,DamageInstance damage)
        {
            if(target!=dummy||damage.id!="Buff_LightningArmor")return;
            damages.Add(damage.damageResult);elements.Add(damage.elementalType);
            fromTypes.Add(damage.fromType);damageTypes.Add(damage.damageType);
        };
        p.OnAttackUnitBeforeOperation+=chooseCritical;p.OnAttackUnit+=observe;
        try
        {
            dummy.AddCustomStat(ECustomStat.DamageReduction,-defense);removedDefense=true;
            for(;applied<ids.Length;applied++)p.AddCustomStatUnsafe(ids[applied],deltas[applied]);
            buff=Instantiate(prefab,p.transform.position,Quaternion.identity);
            buff.range=Vector2.Distance(p.transform.position,dummy.transform.position)+2;
            buff.Initialize(p,p,c.buff_amplified);
            if(!buff.isServer)throw new Exception("Lightning armor instance did not spawn on the server");
            var snapshot=DamageTooltip.Capture(delegate{return MagicDamageProfiles.DescribeBuffAttack(buff,buff.Amplified,p);},p);
            if(snapshot.Rows.Count!=1||snapshot.Rows[0].Length!=1)throw new Exception("Lightning armor preview did not emit exactly one per-hit row");
            int expected=Task.Run(delegate
            {
                var pair=snapshot.Evaluate(snapshot.Rows[0][0]);
                return (int)(c.critical?pair.Critical:pair.Normal);
            }).GetAwaiter().GetResult();
            var timer=(Timer)HarmonyLib.AccessTools.Field(typeof(CharacterBuff_LightningArmor),"damageTickTimer").GetValue(buff);
            timer.SetTimer(timer.time);
            Physics2D.SyncTransforms();
            // Run the original periodic attack. Only the private fixture timer is advanced.
            HarmonyLib.AccessTools.Method(typeof(CharacterBuff_LightningArmor),"OnUpdate_Server").Invoke(buff,null);
            if(damages.Count==0)throw new Exception("Native lightning pulse did not reach the dummy; no accuracy verdict");
            for(int i=0;i<damages.Count;i++)
                if(damages[i]!=expected||elements[i]!=EDamageElementalType.Lightning||fromTypes[i]!=EDamageFromType.Magic||damageTypes[i]!=EDamageType.ElementalEffectDamage)
                    throw new Exception("Native lightning pulse mismatch: hit="+i+" expected="+expected+" actual="+damages[i]+" element="+elements[i]+" from="+fromTypes[i]+" type="+damageTypes[i]);
            return "Native CharacterBuff_LightningArmor.OnUpdate_Server per-hit comparison; geometry and lifetime hit totals are not covered.\n"+
                "amplified="+c.buff_amplified+" critical="+c.critical+" expected="+expected+" observed_hits="+damages.Count+
                "\nactual="+string.Join(",",damages)+"\nAP=+125 LightningDamage=+175 CharmDamage=+80; raw periodic hit must not scale with these stats.";
        }
        finally
        {
            p.OnAttackUnitBeforeOperation-=chooseCritical;p.OnAttackUnit-=observe;
            try{if(buff)Mirror.NetworkServer.Destroy(buff.gameObject);}
            finally
            {
                for(int i=applied-1;i>=0;i--)p.AddCustomStatUnsafe(ids[i],-deltas[i]);
                if(removedDefense)dummy.AddCustomStat(ECustomStat.DamageReduction,defense);
                UnityEngine.Random.state=randomState;
            }
        }
    }
    private string NativeExplosion(Case c)
    {
        var p=Player;var dummy=FindObjectsOfType<DamageDummy>().FirstOrDefault();
        if(!dummy)throw new Exception("Native damage dummy missing");
        Type moduleType=c.heavy?typeof(BulletDestroyModule_HeavyExplode):typeof(BulletDestroyModule_Explode);
        var prefab=Resources.LoadAll<GameObject>("Bullet").OrderBy(x=>x.name)
            .FirstOrDefault(x=>x.GetComponent<Bullet>()&&x.GetComponent<BulletDestroyModule>()&&x.GetComponent<BulletDestroyModule>().GetType()==moduleType);
        if(!prefab)throw new Exception("Native explosion fixture prefab missing: "+moduleType.Name);
        Bullet bullet=null;int hits=0,actual=0;EDamageElementalType actualElement=EDamageElementalType.Normal;
        int defense=dummy.GetCustomStat(ECustomStat.DamageReduction);bool removedDefense=false;
        Action<UnitAvatar,DamageInstance> forceCritical=delegate(UnitAvatar target,DamageInstance damage)
        {
            if(target==dummy&&damage.id=="SelectedExplosion")damage.criticalChancePercent=100;
        };
        p.OnAttackUnitBeforeOperation+=forceCritical;
        try
        {
            dummy.AddCustomStat(ECustomStat.DamageReduction,-defense);removedDefense=true;
            Vector3 position=dummy.transform.position;
            bullet=Bullet.Pool.Spawn(prefab,position,false,c.direct?EDamageFromType.DirectAttack:EDamageFromType.None,"SelectedExplosion",40,0,0,p,4294967295L,0,
                position,position+Vector3.right,null,delegate(CombatBehaviour target,DamageInstance damage,ProjectileBase projectile)
                {if(target==dummy){hits++;actual=damage.damageResult;actualElement=damage.elementalType;}},0,EDamageElementalType.Ice,false);
            bullet.defaultDamageRatio=.75f;
            bullet.additionalCriticalDamageRate=11;
            bullet.additionalCriticalDamageMultiplier=1.5f;
            bullet.additionalDamagePercent=35;
            var module=bullet.GetComponent<BulletDestroyModule>();
            // Configure only the temporary instance: isolate one area hit from child
            // fragments, camera effects and unrelated targets. The attack method is native.
            if(c.heavy)
            {
                var blast=(BulletDestroyModule_HeavyExplode)module;
                blast.explosionShape=BulletDestroyModule_HeavyExplode.EExplosionShape.Circle;
                blast.explodeOffset=Vector2.zero;blast.explodeRadius=1;
                blast.elementalType=EDamageElementalType.Fire;blast.explosionDamageType=EDamageType.Projectile;
                blast.bulletPrefab=null;blast.shakeCameraOnExplode=false;
            }
            else
            {
                var blast=(BulletDestroyModule_Explode)module;
                blast.explosionShape=BulletDestroyModule_Explode.EExplosionShape.Circle;
                blast.explodeOffset=Vector2.zero;blast.explodeRadius=1;blast.explodeRadiusBonusPercent=0;
                blast.elementalType=EDamageElementalType.Fire;blast.inheritElementalType=c.inherit_element;
                blast.explosionDamageType=EDamageType.Projectile;blast.bulletPrefab=null;
            }
            var parent=new DamageTooltip.Hit{Raw=40,Weapon=c.direct,Element=EDamageElementalType.Ice,ExtraCritical=11,CriticalRateMultiplier=1.5f,ProjectileDamagePercent=35};
            var element=!c.heavy&&c.inherit_element?EDamageElementalType.Ice:EDamageElementalType.Fire;
            var scale=HarmonyLib.AccessTools.Method(typeof(ProjectileDamageProfiles),"Scale");
            var child=(DamageTooltip.Hit)scale.Invoke(null,new object[]{parent,.75f,!c.heavy,(EDamageElementalType?)element,(EDamageType?)EDamageType.Projectile});
            var snapshot=DamageTooltip.Capture(delegate{return "Explosion fixture";},p);
            int predicted=Task.Run(()=>(int)snapshot.Evaluate(child).Critical).GetAwaiter().GetResult();
            Physics2D.SyncTransforms();
            module.OnDestroyRequestReceived(false);
            if(hits!=1||actual!=predicted||actualElement!=element)
                throw new Exception("Native explosion mismatch: hits="+hits+" expected="+predicted+" actual="+actual+" expectedElement="+element+" actualElement="+actualElement);
            return "Native destruction-module area hit; child fragment spawning is not covered.\nprefab="+prefab.name+" module="+moduleType.Name+
                "\ndirect="+c.direct+" inherit_element="+c.inherit_element+"\nparent_critical_bonus=11 parent_critical_multiplier=1.5 parent_damage_percent=35\nexpected="+predicted+" actual="+actual+" hits="+hits;
        }
        finally
        {
            p.OnAttackUnitBeforeOperation-=forceCritical;
            try{if(bullet)bullet.Despawn();}
            finally{if(removedDefense)dummy.AddCustomStat(ECustomStat.DamageReduction,defense);}
        }
    }
    private string NativeFollowerPipeline(Case c)
    {
        var p=Player;var dummy=FindObjectsOfType<DamageDummy>().FirstOrDefault();
        if(!dummy)throw new Exception("Native damage dummy missing");
        var summon=Resources.LoadAll<ItemEntity>("Item").OrderBy(x=>x.id)
            .Select(x=>x.resourcePrefab?x.resourcePrefab.GetComponent<Charm_SummonUnit>():null)
            .FirstOrDefault(x=>x&&x.unitPrefab&&x.unitPrefab.GetComponent<Unit_Amethyst>());
        if(!summon)throw new Exception("Native companion fixture prefab missing");
        GameObject temporary=null;UnitAvatar follower=null;
        string[] leaderIds={"ALLDAMAGEBONUS","FOLLOWERDAMAGE","FOLLOWERCRITICALCONTRIBUTE","TRUEDAMAGE","ADVANCED_NEGOTIATION","NEGOTIATION"};
        int[] leaderDeltas={9,20,75,3,1,13};int applied=0;
        Action<UnitAvatar,DamageInstance> elementalBonus=delegate(UnitAvatar target,DamageInstance damage)
        {
            if(target==dummy&&damage.id=="SelectedFollowerPipeline"&&DamageInstance.IsSameElementalType(damage.elementalType,EDamageElementalType.Ice))
                damage.criticalDamageRate+=c.element_critical;
        };
        bool criticalApplied=false,defenseRemoved=false;
        int defense=dummy.GetCustomStat(ECustomStat.DamageReduction);
        var originalMonsterType=dummy.monsterType;
        try
        {
            temporary=UnityEngine.Object.Instantiate(summon.unitPrefab,p.transform.position+Vector3.right*2,Quaternion.identity,p.transform.parent);
            follower=temporary.GetComponent<UnitAvatar>();
            if(!follower)throw new Exception("Companion prefab has no UnitAvatar");
            Mirror.NetworkServer.Spawn(temporary);
            follower.ChangeFaction(p.faction);follower.SetLeader(p);
            if(follower.NetworkLeader!=p)throw new Exception("Native companion leader was not assigned");
            follower.isForcedChaosDamage=c.force_chaos;
            if(c.element_critical!=0)follower.OnAttackUnitBeforeOperation+=elementalBonus;
            for(;applied<leaderIds.Length;applied++)p.AddCustomStatUnsafe(leaderIds[applied],leaderDeltas[applied]);
            p.AddCustomStat(ECustomStat.CriticalDamageBonus,17);criticalApplied=true;
            follower.AddCustomStat(ECustomStat.CriticalDamageBonus,7);
            follower.AddCustomStatUnsafe("WEAPONCRITICALDAMAGE",20);
            follower.AddCustomStatUnsafe("WEAPONCRITICALDAMAGEAMPLIFY",50);
            follower.AddCustomStatUnsafe("ALLDAMAGEBONUS",25);
            follower.AddCustomStatUnsafe("WEAPONDAMAGEBONUSBYDASHCOUNT",10);
            follower.AddCustomStatUnsafe("DASHCOUNT",2);
            follower.AddCustomStatUnsafe("TRUEDAMAGE",2);
            follower.AddCustomStatUnsafe("ELITEDAMAGE",35);
            dummy.AddCustomStat(ECustomStat.DamageReduction,-defense);defenseRemoved=true;
            if(c.elite)dummy.monsterType=EMonsterType.Boss;
            var capture=HarmonyLib.AccessTools.Method(typeof(FollowerDamageProfiles),"Capture");
            var modeled=(FollowerDamageProfiles.Hit)capture.Invoke(null,new object[]{follower,p,1f});
            modeled.BaseAttack=40;modeled.QuantizeSpawn=false;modeled.Direct=c.direct;
            modeled.AfterFactors=c.additional_damage;
            if(c.element_critical!=0)
            {
                if(modeled.ElementCritical==null)modeled.ElementCritical=new int[Enum.GetValues(typeof(EDamageElementalType)).Length];
                // This test-only subscriber is not an equipped Charm_Burn. Supply its
                // known contribution explicitly; the case verifies pipeline ordering.
                for(int element=0;element<modeled.ElementCritical.Length;element++)
                    if(DamageInstance.IsSameElementalType((EDamageElementalType)element,EDamageElementalType.Ice))modeled.ElementCritical[element]+=c.element_critical;
            }
            float multiplier=c.critical_multiplier==0?1:c.critical_multiplier;
            var hit=new DamageTooltip.Hit{Follower=modeled,ExtraCritical=c.extra_critical,CriticalRateMultiplier=multiplier,ProjectileDamagePercent=c.projectile_percent};
            var snapshot=new DamageTooltip.Snapshot();
            var expected=Task.Run(()=>snapshot.Evaluate(hit,c.elite)).GetAwaiter().GetResult();
            var native=DamageInstance.GetDamage(follower,"SelectedFollowerPipeline",dummy.transform.position,4294967295L,40+c.additional_damage,EDamageType.Slice,c.direct?EDamageFromType.DirectAttack:EDamageFromType.None,Vector2.zero,0,0);
            native.damage+=native.damage*c.projectile_percent/100f;
            native.criticalDamageRate+=c.extra_critical;native.criticalDamageMultiplier=multiplier;
            native.criticalChancePercent=c.critical?100:0;native.isExecutionAttack=c.execution;
            var result=dummy.ApplyDamage(native);
            int predicted=(int)(c.execution?expected.Execution:c.critical?expected.Critical:expected.Normal);
            var expectedElement=c.force_chaos?EDamageElementalType.Chaos:EDamageElementalType.Physical;
            if(result!=EApplyDamageResult.Success||native.damageResult!=predicted||native.elementalType!=expectedElement)throw new Exception("Native follower pipeline mismatch: "+result+" expected="+predicted+" actual="+native.damageResult+" expectedElement="+expectedElement+" actualElement="+native.elementalType);
            return "Native companion UnitAvatar.ApplyDamage comparison; companion attack/spawn formulas are not covered.\nprefab="+summon.unitPrefab.name+
                "\nleader_critical_contribution=75 leader_critical_added=17\nextra_critical="+c.extra_critical+" critical_multiplier="+multiplier+" projectile_percent="+c.projectile_percent+
                "\nforce_chaos="+c.force_chaos+" element_critical="+c.element_critical+" actual_element="+native.elementalType+"\nexpected="+predicted+" actual="+native.damageResult;
        }
        finally
        {
            try
            {
                try{if(follower){if(c.element_critical!=0)follower.OnAttackUnitBeforeOperation-=elementalBonus;follower.SetLeader(null);}}
                finally{if(temporary)Mirror.NetworkServer.Destroy(temporary);}
            }
            finally
            {
                if(criticalApplied)p.AddCustomStat(ECustomStat.CriticalDamageBonus,-17);
                for(int i=applied-1;i>=0;i--)p.AddCustomStatUnsafe(leaderIds[i],-leaderDeltas[i]);
                if(defenseRemoved)dummy.AddCustomStat(ECustomStat.DamageReduction,defense);
                dummy.monsterType=originalMonsterType;
            }
        }
    }
    private string NativePipeline(Case c)
    {
        var p=Player;var dummy=FindObjectsOfType<DamageDummy>().FirstOrDefault();
        if(!dummy)throw new Exception("Native damage dummy missing");
        string[] ids={"ALLDAMAGEBONUS","WEAPONCRITICALDAMAGE","WEAPONCRITICALDAMAGEAMPLIFY","WEAPONDAMAGEBONUSBYDASHCOUNT","DASHCOUNT","TRUEDAMAGE","DEBUFFDAMAGE","POISONDEBUFFDAMAGEBONUS","ELITEDAMAGE"};
        int[] deltas={25,20,50,10,2,3,30,15,35};int applied=0;
        var originalMonsterType=dummy.monsterType;
        if(c.elite)dummy.monsterType=EMonsterType.Boss;
        int defense=dummy.GetCustomStat(ECustomStat.DamageReduction);
        dummy.AddCustomStat(ECustomStat.DamageReduction,-defense);
        Action<UnitAvatar,DamageInstance> elementalBonus=delegate(UnitAvatar target,DamageInstance damage)
        {
            if(DamageInstance.IsSameElementalType(damage.elementalType,EDamageElementalType.Ice))damage.criticalDamageRate+=c.element_critical;
        };
        if(c.element_critical!=0)p.OnAttackUnitBeforeOperation+=elementalBonus;
        try
        {
            for(;applied<ids.Length;applied++)p.AddCustomStatUnsafe(ids[applied],deltas[applied]);
            var snapshot=DamageTooltip.Capture(delegate{return DamageTooltip.DamagePair(p,40,c.direct);},p);
            float multiplier=c.critical_multiplier==0?1:c.critical_multiplier;
            var element=c.element_critical!=0?EDamageElementalType.Ice:EDamageElementalType.Physical;
            if(c.element_critical!=0)
            {
                if(snapshot.ElementCritical==null)snapshot.ElementCritical=new int[Enum.GetValues(typeof(EDamageElementalType)).Length];
                snapshot.ElementCritical[(int)element]+=c.element_critical;
            }
            var hit=new DamageTooltip.Hit{Raw=40,AfterFactors=c.additional_damage,ProjectileDamagePercent=c.projectile_percent,Weapon=c.direct,ElementalEffect=c.elemental_effect,
                Element=element,ExtraCritical=c.extra_critical,CriticalRateMultiplier=multiplier};
            var expected=Task.Run(()=>snapshot.Evaluate(hit,c.elite)).GetAwaiter().GetResult();
            var native=DamageInstance.GetDamage(p,"SelectedDamagePipeline",dummy.transform.position,4294967295L,40+c.additional_damage,c.elemental_effect?EDamageType.ElementalEffectDamage:EDamageType.Slice,c.direct?EDamageFromType.DirectAttack:EDamageFromType.None,Vector2.zero,0,0);
            native.damage+=native.damage*c.projectile_percent/100f;
            native.elementalType=element;
            native.criticalDamageRate+=c.extra_critical;
            native.criticalDamageMultiplier=multiplier;
            native.criticalChancePercent=c.critical?100:0;
            native.isExecutionAttack=c.execution;
            var result=dummy.ApplyDamage(native);
            int predicted=(int)(c.execution?expected.Execution:c.critical?expected.Critical:expected.Normal);
            if(result!=EApplyDamageResult.Success||native.damageResult!=predicted)throw new Exception("Native pipeline mismatch: "+result+" expected="+predicted+" actual="+native.damageResult);
            return "Native UnitAvatar.ApplyDamage comparison only; equipment-specific formulas are not covered.\nadditional_damage="+c.additional_damage+" projectile_percent="+c.projectile_percent+
                "\nextra_critical="+c.extra_critical+" element_critical="+c.element_critical+" critical_multiplier="+multiplier+"\nexpected="+predicted+" actual="+native.damageResult;
        }
        finally
        {
            if(c.element_critical!=0)p.OnAttackUnitBeforeOperation-=elementalBonus;
            for(int i=applied-1;i>=0;i--)p.AddCustomStatUnsafe(ids[i],-deltas[i]);
            dummy.AddCustomStat(ECustomStat.DamageReduction,defense);
            dummy.monsterType=originalMonsterType;
        }
    }
}
