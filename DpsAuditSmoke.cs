using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Threading;
using BepInEx;
using UnityEngine;
using SephiriaDicePreview;
[BepInPlugin("local.sephiria.damage-tooltip-smoke", "Damage Tooltip Smoke", "1.0.0")]
public sealed class DamageTooltipSmoke : BaseUnityPlugin
{
    private string root;
    private PlayerAvatar Player { get { return PlayerSpawner.MultiplayerList.Where(p=>p && p.isLocalPlayer).Select(p=>p.PlayerAvatar).FirstOrDefault(); } }
    private void Log(string s) { File.AppendAllText(Path.Combine(root,"damage-tooltip-smoke.txt"),s+"\n"); }
    private void Awake()
    {
        root=Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
        if(String.IsNullOrEmpty(root)||Path.GetFullPath(SaveData.CommonPath)!=Path.Combine(root,"isolated-saves")) throw new Exception("Isolation absent");
        if(UnityEngine.InputSystem.Keyboard.current==null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        if(UnityEngine.InputSystem.Mouse.current==null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        File.WriteAllText(Path.Combine(root,"damage-tooltip-smoke.txt"),""); StartCoroutine(Guard());
    }
    private IEnumerator Guard() { var routine=Run(); while(true) { bool next; try {next=routine.MoveNext();} catch(Exception e) {Log("FAIL "+e);Application.Quit(21);yield break;} if(!next)break; yield return routine.Current; } }
    private IEnumerator Run()
    {
        float end=Time.realtimeSinceStartup+100;
        while((!Player || !Player.CanMove || Player.loadingScreenType!=-1)&&Time.realtimeSinceStartup<end) yield return null;
        if(!Player) throw new Exception("Startup failed");
        LocalizationManager.Instance.LoadLanguage("ko-KR");
        if(Environment.GetEnvironmentVariable("SEPHIRIA_DPS_DUMP_BONDS")=="1")
        {
            foreach(var entity in Resources.LoadAll<ItemEntity>("Item").Where(item=>item.isDual&&item.resourcePrefab).OrderBy(item=>item.id))
            {
                var source=entity.resourcePrefab.GetComponent<Charm_Basic>();
                string stats="";var status=source as Charm_StatusInstance;
                if(status)stats=String.Join(";",status.stats.Select(group=>group.statusID+"="+String.Join(",",group.valuesByLevel.Select(value=>value.ToString()).ToArray())).ToArray());
                string effects=source==null?"":String.Join(" | ",Enumerable.Range(0,source.GetEffectStringCount()).Select(index=>source.GetEffectString(index,0,0,true)).Where(value=>!String.IsNullOrEmpty(value)).ToArray());
                Log("BOND artifact:"+entity.id+" "+entity.Name+" type="+(source?source.GetType().Name:"none")+" categories=["+String.Join(",",entity.categories.ToArray())+"] stats=["+stats+"] effects=["+effects+"]");
            }
        }
        if(Environment.GetEnvironmentVariable("SEPHIRIA_DPS_COMBO_BONDS")=="1")ValidateComboBondConversions();
        var allowed=new System.Collections.Generic.HashSet<string>(new[]{"artifact:1000","artifact:1001","artifact:1002","artifact:1004","artifact:1005","artifact:1008","artifact:1009","artifact:1010","artifact:1011","artifact:1012","artifact:1013","artifact:1014","artifact:1015","artifact:1016","artifact:1018","artifact:1023","artifact:1025","artifact:1026","artifact:1027","artifact:1029","artifact:1031","artifact:1032","artifact:1034","artifact:1035","artifact:1036","artifact:1040","artifact:1048","artifact:1050","artifact:1051","artifact:1056","artifact:1058","artifact:1060","artifact:1063","artifact:1065","artifact:1066","artifact:1067","artifact:1068","artifact:1070","artifact:1071","artifact:1072","artifact:1074","artifact:1076","artifact:1077","artifact:1078","artifact:1079","artifact:1080","artifact:1081","artifact:1082","artifact:1083","artifact:1086","artifact:1087","artifact:1088","artifact:1090","artifact:1094","artifact:1100","artifact:1101","artifact:1102","artifact:1104","artifact:1105","artifact:1106","artifact:1107","artifact:1108","artifact:1109","artifact:1113","artifact:1114","artifact:1118","artifact:1119","artifact:1120","artifact:1121","artifact:1122","artifact:1125","artifact:1126","artifact:1128","artifact:1129","artifact:1130","artifact:1132","artifact:1137","artifact:1141","artifact:1149","artifact:1153","artifact:1162","artifact:1163","artifact:1164","artifact:1175","artifact:1176","artifact:1177","artifact:1178","artifact:1180","artifact:1183","artifact:1185","artifact:1186","artifact:1189","artifact:1190","artifact:1191","artifact:1192","artifact:1194","artifact:1195","artifact:1208","artifact:1209","artifact:1210","artifact:1213","artifact:1214","artifact:1215","artifact:1218","artifact:1223","artifact:1224","artifact:1225","artifact:1235","artifact:1239","artifact:1241","artifact:1242","artifact:1247","artifact:1252","artifact:1270","artifact:1271","artifact:1272","artifact:1273","artifact:1277","artifact:1283","artifact:1288","artifact:1325","artifact:3031","artifact:5000","artifact:1038","artifact:3005","artifact:1154","artifact:1282","artifact:1297","artifact:1298","artifact:1299","artifact:1305","artifact:1306","artifact:1307","artifact:1308","artifact:1309","artifact:1310","artifact:1064","artifact:1069","artifact:1073","artifact:1075","artifact:1161","artifact:1165","artifact:1166","artifact:1167","artifact:1168","artifact:3002","artifact:3011","artifact:3012","artifact:3018","artifact:3019","artifact:3020","artifact:3022","artifact:3023","artifact:3027","artifact:3033","artifact:3034","artifact:1017","artifact:1021","artifact:1028","artifact:1033","artifact:1092","artifact:1093","artifact:1091","artifact:1095","artifact:1124","artifact:1138","artifact:1139","artifact:1140","artifact:1196","artifact:1123","artifact:1143","artifact:1145","artifact:1146","artifact:1150","artifact:1151","artifact:1234","artifact:1170","artifact:1171","artifact:1200","artifact:1211","artifact:1212","artifact:1238","artifact:1264","artifact:1089","artifact:1115","artifact:1169","artifact:1199","artifact:1216","artifact:3013","artifact:3028","artifact:3030","artifact:3032","artifact:3036","artifact:1274","artifact:1275","artifact:1276","artifact:1278","artifact:1279","artifact:1285","artifact:1158","artifact:1300","artifact:1301","artifact:1302","artifact:1053","artifact:1117","artifact:1144","artifact:1187","artifact:1243","artifact:1244","artifact:1245","artifact:1246","artifact:1248","artifact:1249","artifact:1250","artifact:1251","artifact:1259","artifact:1263","artifact:1268","artifact:1291","artifact:1292","artifact:1293","artifact:1294","artifact:1295","artifact:1296","artifact:1303","artifact:1311","artifact:1193","artifact:1229","artifact:1230","artifact:1231","artifact:1232","artifact:1233","artifact:1236","artifact:1237","artifact:1240","artifact:1059","artifact:1055","artifact:1172","artifact:1179","artifact:1188","artifact:1217","artifact:1220","artifact:1221","weapon:8","weapon:119","weapon:120","weapon:121","weapon:122","weapon:123","weapon:124","weapon:125","weapon:126","weapon:127","weapon:128","weapon:404","weapon:405","weapon:422","weapon:423","weapon:424","weapon:425","weapon:501","weapon:502","weapon:503","weapon:505","weapon:506","weapon:507","weapon:509","weapon:510","weapon:513","weapon:514","weapon:515","weapon:517","weapon:518","weapon:519","weapon:521","weapon:522","weapon:523","weapon:525","weapon:526","weapon:528","weapon:529","weapon:530","weapon:531","weapon:532","weapon:1020","weapon:1023","weapon:1024","weapon:1025","weapon:1026","weapon:1028","weapon:1029","weapon:1030","weapon:1111","weapon:1121","weapon:1210","weapon:1211","weapon:1212","weapon:1213","weapon:1214","weapon:1215","weapon:1216","weapon:1217","artifact:1111","artifact:1112","artifact:1197","artifact:1313","artifact:1314","artifact:1315","artifact:1316","artifact:1317","artifact:1318","artifact:1319","artifact:1323","artifact:1324","artifact:3035","artifact:1062","artifact:1155","artifact:1156","artifact:1173","artifact:1181","artifact:1321","artifact:1322","artifact:1030","artifact:1085","artifact:1202","artifact:1147","artifact:1198","artifact:1228","artifact:1262","artifact:1157","artifact:1159","artifact:1152","artifact:1160","artifact:1182","artifact:1049","artifact:1258","artifact:1289","artifact:1267","weapon:0","weapon:1","weapon:2","weapon:3","weapon:4","weapon:5","weapon:6","weapon:7","weapon:9","weapon:10","weapon:11","weapon:12","weapon:14","weapon:15","weapon:16","weapon:17","weapon:20","weapon:22","weapon:23","weapon:26","weapon:27","weapon:28","weapon:29","weapon:100","weapon:101","weapon:102","weapon:103","weapon:104","weapon:105","weapon:106","weapon:108","weapon:109","weapon:110","weapon:111","weapon:112","weapon:113","weapon:114","weapon:115","weapon:116","weapon:117","weapon:118","weapon:400","weapon:401","weapon:402","weapon:403","weapon:406","weapon:407","weapon:408","weapon:409","weapon:410","weapon:411","weapon:412","weapon:413","weapon:414","weapon:416","weapon:417","weapon:419","weapon:420","weapon:421","weapon:500","weapon:1000","weapon:1001","weapon:1004","weapon:1006","weapon:1007","weapon:1013","weapon:1014","weapon:1015","weapon:1016","weapon:1017","weapon:1018","weapon:1019","weapon:1100","weapon:1101","weapon:1102","weapon:1103","weapon:1104","weapon:1105","weapon:1106","weapon:1107","weapon:1108","weapon:1109","weapon:1112","weapon:1113","weapon:1114","weapon:1115","weapon:1117","weapon:1118","weapon:1119","weapon:1120","weapon:1122","weapon:1123","weapon:1124","weapon:1200","weapon:1201","weapon:1203","weapon:1204","weapon:1205","weapon:1206","weapon:1207","weapon:1208","weapon:1209"});
        string selectedFilter=Environment.GetEnvironmentVariable("SEPHIRIA_DPS_IDS");
        if(!String.IsNullOrEmpty(selectedFilter))allowed.IntersectWith(selectedFilter.Split(','));
        bool fullConditions=Environment.GetEnvironmentVariable("SEPHIRIA_DPS_FULL")=="1";
        bool detailWeapons=Environment.GetEnvironmentVariable("SEPHIRIA_DETAIL_WEAPONS")=="1";
        if(Environment.GetEnvironmentVariable("SEPHIRIA_DPS_RANDOM_BOLT_FIXTURE")=="1")
        {
            int fixtureId=199950000;
            foreach(int id in new[]{3000,3002})Player.Inventory.AddItem(new ItemMetadata(fixtureId++,id,1),0,false);
            yield return new WaitForSecondsRealtime(.25f);
            foreach(var magic in Player.Inventory.charms.Values.OfType<Charm_Magic>())magic.currentAmmo=magic.ContainedMagic.ammo;
            Log("FIXTURE random-bolt magic charms="+String.Join(",",Player.Inventory.charms.Values.OfType<Charm_Magic>().Select(x=>x.Item.EntityID+":"+x.currentAmmo).ToArray()));
        }
        int captured=0,errors=0;
        foreach(var entity in Resources.LoadAll<WeaponEntity>("Weapon").Where(w=>allowed.Contains("weapon:"+w.id)&&w.enabled&&w.mainWeaponPrefab&&w.mainWeaponPrefab.GetComponent<WeaponSimple>()).OrderBy(w=>w.id))
        {
            var weapon=entity.mainWeaponPrefab.GetComponent<WeaponSimple>();
            Log("SOURCE weapon:"+entity.id+" "+weapon.GetType().Name+
                " addons=["+String.Join(",",weapon.addons.Where(x=>x).Select(x=>x.GetType().Name).ToArray())+"] "+
                DescribeFire(WeaponBuffPreview.Attacks(weapon,weapon.basicComboAttacks,0),"basic")+" "+
                DescribeFire(WeaponBuffPreview.Attacks(weapon,weapon.dashAttacks,1),"dash")+" "+
                DescribeFire(WeaponBuffPreview.Attacks(weapon,weapon.specialAttacks,2),"special"));
            DamageTooltip.Snapshot snapshot=null;
            try{snapshot=DamageTooltip.Capture(()=>{DamageTooltip.CurrentCapture.FullConditions=fullConditions;return DpsCapture.Weapon(weapon,Player);},Player);}
            catch(Exception e){errors++;Log("ERROR weapon:"+entity.id+" "+entity.Name+" "+e);}
            if(snapshot!=null){var routine=Calculate("weapon:"+entity.id,entity.Name,snapshot);while(routine.MoveNext())yield return routine.Current;captured++;}
            if(detailWeapons)
            {
                var detail=DamageTooltip.Capture(()=>DamageTooltip.WeaponText(weapon,Player),Player);
                var detailJob=System.Threading.Tasks.Task.Factory.StartNew(()=>detail.Calculate());
                while(!detailJob.IsCompleted)yield return null;
                if(detailJob.IsFaulted){errors++;Log("ERROR detail-weapon:"+entity.id+" "+detailJob.Exception);}
                else
                {
                    string rendered=detailJob.Result;
                    var attacks=WeaponBuffPreview.Attacks(weapon,weapon.specialAttacks,2);
                    var timing=DpsTiming.Special(weapon,Player);
                    var indices=new System.Collections.Generic.HashSet<int>();bool compatible=timing.Unavailable==null&&timing.Attacks.Count>0;
                    foreach(var attack in timing.Attacks)
                    {
                        if(attack.Kind!=2||attack.Index<0||attacks==null||attack.Index>=attacks.Length){compatible=false;break;}
                        indices.Add(attack.Index);
                    }
                    var dagger=weapon as WeaponSimple_Dagger;
                    int specialRows=rendered.Split('\n').Count(line=>System.Text.RegularExpressions.Regex.IsMatch(line,"^특수(?: [0-9]+)?:"));
                    if(dagger)
                    {
                        bool primary=DpsTiming.DaggerPrimaryAvailable(dagger,Player),fury=DpsTiming.DaggerFuryAvailable(dagger,Player);
                        int primaryRows=rendered.Split('\n').Count(line=>line.StartsWith(dagger.throwDagger?"특수 · 투척 단검:":"특수 · 패리:"));
                        int furyRows=rendered.Split('\n').Count(line=>line.StartsWith("특수 · 퓨리:"));
                        if(primaryRows!=(primary?1:0)||furyRows!=(fury?1:0)||specialRows!=0)
                            throw new Exception("Dagger special variants mismatch weapon:"+entity.id+" primary="+primaryRows+"/"+primary+" fury="+furyRows+"/"+fury);
                        bool dpsPrimary=snapshot.Dps.Cycles.Any(c=>c.Name=="특공 DPS · 패리"||c.Name=="특공 DPS · 투척 단검");
                        bool dpsFury=snapshot.Dps.Cycles.Any(c=>c.Name=="특공 DPS · 퓨리");
                        if(dpsPrimary!=primary||dpsFury!=fury)throw new Exception("Dagger DPS variants mismatch weapon:"+entity.id);
                    }
                    else if(timing.NoDamage&&specialRows!=0)throw new Exception("Non-damaging special displayed damage weapon:"+entity.id+" rows="+specialRows);
                    else if(compatible)
                    {
                        if(specialRows!=indices.Count)throw new Exception("Special detail rows mismatch weapon:"+entity.id+" expected="+indices.Count+" actual="+specialRows);
                        if(indices.Count==1&&!rendered.Split('\n').Any(line=>line.StartsWith("특수:")))throw new Exception("Single special attack still numbered weapon:"+entity.id);
                    }
                    ValidateInputRows(entity.id,rendered,"평타",weapon,WeaponBuffPreview.Attacks(weapon,weapon.basicComboAttacks,0),DpsTiming.Basic(weapon,Player),0);
                    ValidateInputRows(entity.id,rendered,"돌진",weapon,WeaponBuffPreview.Attacks(weapon,weapon.dashAttacks,1),DpsTiming.Dash(weapon,Player),1);
                    if(entity.id==1121)
                    {
                        int currentBasics=rendered.Split('\n').Count(line=>System.Text.RegularExpressions.Regex.IsMatch(line,"^평타(?: [0-9]+)?:"));
                        if(currentBasics!=3)throw new Exception("Bloodletting device sword current basic combo should have 3 hits, got "+currentBasics);
                        var buffed=DamageTooltip.Capture(()=>
                        {
                            DamageTooltip.CurrentCapture.FullConditions=true;
                            using(var preview=new WeaponBuffPreview(weapon,false,true))return DamageTooltip.WeaponText(weapon,Player);
                        },Player);
                        string buffedText=System.Threading.Tasks.Task.Factory.StartNew(()=>buffed.Calculate()).GetAwaiter().GetResult();
                        int buffedBasics=buffedText.Split('\n').Count(line=>System.Text.RegularExpressions.Regex.IsMatch(line,"^평타(?: [0-9]+)?:"));
                        int buffedSpecials=buffedText.Split('\n').Count(line=>System.Text.RegularExpressions.Regex.IsMatch(line,"^특수(?: [0-9]+)?:"));
                        if(buffedBasics!=2||buffedSpecials!=0)throw new Exception("Bloodletting device sword buffed rows mismatch basic="+buffedBasics+" special="+buffedSpecials);
                        Log("DETAIL-FULL weapon:1121 "+entity.Name+" "+buffedText.Replace("\n"," | "));
                    }
                    Log("DETAIL weapon:"+entity.id+" "+entity.Name+" "+rendered.Replace("\n"," | "));
                }
            }
            yield return null;
        }
        if(Environment.GetEnvironmentVariable("SEPHIRIA_DPS_PLANET_BONUS")=="1")Player.AddCustomStat(ECustomStat.SUPERPLANET,1);
        if(Environment.GetEnvironmentVariable("SEPHIRIA_DPS_WEAPONS_ONLY")!="1")
        foreach(var entity in Resources.LoadAll<ItemEntity>("Item").Where(i=>allowed.Contains("artifact:"+i.id)&&i.resourcePrefab).OrderBy(i=>i.id))
        {
            var source=entity.resourcePrefab.GetComponent<Charm_Basic>();if(!source)continue;
            var magic=source as Charm_Magic;
            var profile=magic&&magic.ContainedMagic&&magic.ContainedMagic.magicPrefab
                ? " magic="+String.Join(",",magic.ContainedMagic.magicPrefab.GetComponents<ActiveSkill>().Select(x=>x.GetType().Name).ToArray())+
                  " cast="+magic.ContainedMagic.castingType+" charge="+magic.ContainedMagic.chargingTime+" tiers="+magic.ContainedMagic.maxChargingPower
                : "";
            Log("SOURCE artifact:"+entity.id+" "+source.GetType().Name+profile);
            for(int level=0;level<=source.maxLevel;level++)
            {
                DamageTooltip.Snapshot snapshot=null;
                try{int selected=level;snapshot=DamageTooltip.Capture(()=>{DamageTooltip.CurrentCapture.FullConditions=fullConditions;return DpsCapture.Artifact(entity,null,selected,Player);},Player);}
                catch(Exception e){errors++;Log("ERROR artifact:"+entity.id+" level="+level+" "+entity.Name+" "+e);}
                if(snapshot!=null){var routine=Calculate("artifact:"+entity.id+":"+level,entity.Name,snapshot);while(routine.MoveNext())yield return routine.Current;captured++;}
                yield return null;
            }
        }
        Log("AUDIT COMPLETE captures="+captured+" captureErrors="+errors+"; output presence only, not accuracy verdict");Application.Quit(0);
    }
    private void ValidateComboBondConversions()
    {
        var effects=ItemDatabase.GetAllItemCategory().Where(category=>category.comboEffectPrefab)
            .Select(category=>category.comboEffectPrefab.GetComponent<ComboEffectBase>()).Where(effect=>effect).ToArray();
        var cloud=effects.OfType<ComboEffect_DarkCloud>().First();
        var sword=effects.OfType<ComboEffect_FlameSword>().First();
        var debuff=effects.OfType<ComboEffect_Debuff>().First(effect=>effect.debuffPrefab is CharacterDebuff_Burn);
        var glacier=effects.OfType<ComboEffect_Debuff>().First(effect=>effect.debuffPrefab is CharacterDebuff_Frostbite);

        Player.AddCustomStatUnsafe("DARKCLOUDICE",1);
        var cloudSnapshot=DamageTooltip.Capture(()=>ComboDamageProfiles.Detail(cloud,cloud.GetHighestComboCount(),Player),Player);
        if(cloudSnapshot.Rows.Count==0||cloudSnapshot.Rows[0][0].Element!=EDamageElementalType.IceAndLightning||!cloudSnapshot.Template.Contains("눈구름"))
            throw new Exception("Bond conversion failed: dark cloud to snow cloud");
        Player.AddCustomStatUnsafe("DARKCLOUDICE",-1);

        Player.AddCustomStatUnsafe("FLAMESWORDFROST",1);
        var swordSnapshot=DamageTooltip.Capture(()=>ComboDamageProfiles.Detail(sword,sword.GetHighestComboCount(),Player),Player);
        if(swordSnapshot.Rows.Count==0||swordSnapshot.Rows[0][0].Element!=EDamageElementalType.Ice||!swordSnapshot.Template.Contains("얼음 태양검"))
            throw new Exception("Bond conversion failed: flame sword to frost sword");
        Player.AddCustomStatUnsafe("FLAMESWORDFROST",-1);

        Player.AddCustomStatUnsafe("BLUEBURNCHANGE",1);
        var blueBurn=DamageTooltip.Capture(()=>ComboDamageProfiles.Detail(debuff,debuff.GetHighestComboCount(),Player),Player);
        EDamageElementalType blueBurnElement;CharacterDebuff_Burn.CalculateTickDamage(Player,out blueBurnElement);
        if(!blueBurn.Debuffs.Any(row=>row.Name.Contains("푸른 화상"))||blueBurn.Rows.Count==0||blueBurn.Rows.Any(row=>row.Length>0&&row[0].Element!=blueBurnElement))
            throw new Exception("Bond conversion failed: burn to blue burn stat="+Player.GetCustomStatUnsafe("BLUEBURNCHANGE")+" rows="+blueBurn.Rows.Count+" debuffs=["+String.Join(",",blueBurn.Debuffs.Select(row=>row.Name).ToArray())+"] template="+blueBurn.Template);
        Player.AddCustomStatUnsafe("BLUEBURNCHANGE",-1);

        Player.AddCustomStatUnsafe("PLASMAACTIVE",1);
        var plasma=DamageTooltip.Capture(()=>ComboDamageProfiles.Detail(debuff,debuff.GetHighestComboCount(),Player),Player);
        EDamageElementalType plasmaElement;CharacterDebuff_Plasma.CalculateTickDamage(Player,out plasmaElement);
        if(!plasma.Debuffs.Any(row=>row.Name.Contains("플라즈마"))||plasma.Rows.Count==0||plasma.Rows[0][0].Element!=plasmaElement)
            throw new Exception("Bond conversion failed: burn to plasma");
        Player.AddCustomStatUnsafe("PLASMAACTIVE",-1);
        var frostbite=(CharacterDebuff_Frostbite)glacier.debuffPrefab;
        var freeze=frostbite.freezeDebuffPrefab;
        var glacierDetail=DamageTooltip.Capture(()=>ComboDamageProfiles.Detail(glacier,glacier.GetHighestComboCount(),Player),Player);
        string glacierText=glacierDetail.Calculate();
        if(!freeze||Math.Abs(freeze.damageMultiplier-5f)>.001f||!glacierDetail.Debuffs.Any(row=>row.OneShotAtMaximum)||!glacierText.Contains("빙결 피해")||!glacierText.Contains("기절"))
            throw new Exception("Glacier freeze detail failed multiplier="+(freeze?freeze.damageMultiplier:-1)+" text="+glacierText);
        var glacierDps=DamageTooltip.Capture(()=>DpsCapture.Combo(glacier,glacier.GetHighestComboCount(),Player),Player);
        string glacierDpsText=glacierDps.Calculate();
        if(!glacierDpsText.Contains("빙결 피해")||!glacierDpsText.Contains("기대 DPS")||glacierDpsText.Contains("빙결 발동 주기를 계산할 수 없음"))
            throw new Exception("Glacier freeze DPS failed: "+glacierDpsText);
        Log("PASS glacier freeze multiplier="+freeze.damageMultiplier+" stun="+freeze.defaultDuration+" detail="+glacierText.Replace("\n"," | "));
        Log("PASS combo bond conversions: snow cloud, frost sword, blue burn, plasma");
    }
    private static void ValidateInputRows(int id,string rendered,string label,WeaponSimple weapon,NewWeaponFireData[] fires,DpsTiming.Cycle timing,int kind)
    {
        if(timing.Unavailable!=null||timing.Attacks.Count==0)return;
        var indices=new System.Collections.Generic.HashSet<int>();
        foreach(var attack in timing.Attacks)
        {
            if(attack.Kind!=kind||attack.Index<0||fires==null||attack.Index>=fires.Length)return;
            indices.Add(attack.Index);
        }
        int rows=rendered.Split('\n').Count(line=>System.Text.RegularExpressions.Regex.IsMatch(line,"^"+label+"(?: [0-9]+)?:"));
        if(rows!=indices.Count)throw new Exception(label+" detail rows mismatch weapon:"+id+" expected="+indices.Count+" actual="+rows);
        if(indices.Count==1&&!rendered.Split('\n').Any(line=>line.StartsWith(label+":")))throw new Exception("Single "+label+" attack still numbered weapon:"+id);
    }
    private static string DescribeFire(NewWeaponFireData[] fires,string label)
    {
        if(fires==null)return label+"=[]";
        return label+"=["+String.Join(",",fires.Select((fire,index)=>
        {
            if(!fire)return index+":null";
            var prefabs=fire.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)
                .Where(field=>typeof(GameObject).IsAssignableFrom(field.FieldType)).Select(field=>field.GetValue(fire) as GameObject).Where(value=>value)
                .Select(value=>value.name+"{"+String.Join("+",value.GetComponents<Component>().Select(component=>component.GetType().Name).ToArray())+"}").ToArray();
            return index+":"+fire.GetType().Name+(prefabs.Length==0?"":"="+String.Join("/",prefabs));
        }).ToArray())+"]";
    }
    private IEnumerator Calculate(string id,string name,DamageTooltip.Snapshot snapshot)
    {
        var job=System.Threading.Tasks.Task.Factory.StartNew(()=>snapshot.Calculate());
        float deadline=Time.realtimeSinceStartup+30;
        while(!job.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!job.IsCompleted){Log("ERROR "+id+" worker timeout");throw new Exception("Worker timeout");}
        if(job.IsFaulted){Log("ERROR "+id+" "+name+" "+job.Exception);yield break;}
        Log("RESULT "+id+" "+name+" "+job.Result.Replace("\n"," | "));
    }
}
