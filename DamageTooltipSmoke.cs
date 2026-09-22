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
        var anchor=new GameObject("TooltipProbeAnchor",typeof(RectTransform)).GetComponent<RectTransform>();
        var weaponUI=UIManager.Instance.GetElement<UI_WeaponTooltip>(); weaponUI.Connect(Player.gameObject);
        anchor.SetParent(weaponUI.transform.parent,false); anchor.anchoredPosition=new Vector2(80,650);
        var weapons=Resources.LoadAll<WeaponEntity>("Weapon").Where(w=>w.enabled && w.mainWeaponPrefab && w.mainWeaponPrefab.GetComponent<WeaponSimple>()).OrderBy(w=>w.id).ToArray();
        Log("weapons="+weapons.Length);
        var sample=weapons[0].mainWeaponPrefab.GetComponent<WeaponSimple>();
        int beforeOpen=DamageTooltip.Calculations;
        weaponUI.Open(null,anchor,Vector2.zero,weapons[0]);yield return null;
        if(DamageTooltip.Calculations!=beforeOpen)throw new Exception("Opening ordinary tooltip calculated damage");
        foreach(var rt in weaponUI.GetComponentsInChildren<RectTransform>(true))if(rt.GetComponent<UnityEngine.UI.LayoutGroup>()||rt.name.Contains("Debug")||rt.name.Contains("DamageDetails"))Log("LAYOUT "+rt.name+" parent="+rt.parent.name+" components="+string.Join(",",rt.GetComponents<Component>().Select(c=>c.GetType().Name).ToArray())+" rect="+rt.rect);
        yield return Capture(weaponUI,"details-closed.png");
        var pad=InputSystem.AddDevice<Gamepad>();
        InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightStick));
        yield return null;yield return null;
        InputSystem.QueueStateEvent(pad,new GamepadState());
        if(!DamageDetailsDialog.Current)throw new Exception("Gamepad details shortcut failed");
        var dialog=DamageDetailsDialog.Current;var modal=dialog.GetComponent<UI_MessageBox_Yes>();
        while(dialog.IsCalculating)yield return null;
        if(DamageTooltip.LastWorkerThread==Thread.CurrentThread.ManagedThreadId)throw new Exception("Worker ran on main thread");
        yield return Capture(modal,"details-weapon.png");
        Log("PASS gamepad opens details; damage calculated on worker thread "+DamageTooltip.LastWorkerThread);
        string previous=modal.text.text;
        Player.AddCustomStat(ECustomStat.PhysicalDamage,1);
        HarmonyLib.AccessTools.Method(typeof(DamageDetailsDialog),"Request").Invoke(dialog,null);
        if(!modal.text.text.Contains("계산 중입니다")||!modal.text.text.Contains("평타 1: 20 / 30"))throw new Exception("Previous result not retained while calculating");
        dialog.enabled=false;yield return Capture(modal,"details-recalculating.png");dialog.enabled=true;
        while(dialog.IsCalculating)yield return null;
        if(modal.text.text==previous)throw new Exception("Changed stat result not updated");
        Log("PASS previous result retained during recalculation");
        Player.AddCustomStat(ECustomStat.PhysicalDamage,-1);
        modal.Close();weaponUI.Close();yield return null;
        PersonalConveniencePlugin.Instance.ShowDamageDetails.Value=false;
        weaponUI.Open(null,anchor,Vector2.zero,weapons[0]);yield return null;
        weaponUI.GetComponent<DamageTooltipView>().OpenDetails();
        if(UIManager.Instance.GetElement<UI_MessageBoxHolder>().HasOpenedBox)throw new Exception("Disabled feature opened dialog");
        Log("PASS disabled feature cannot calculate/open");
        PersonalConveniencePlugin.Instance.ShowDamageDetails.Value=true;weaponUI.Close();
        int before=DamageTooltip.Calculations;
        string first=DamageTooltip.WeaponText(sample,Player);
        for(int i=0;i<100;i++) if(DamageTooltip.WeaponText(sample,Player)!=first)throw new Exception("Cache inconsistent");
        if(DamageTooltip.Calculations!=before+1)throw new Exception("Repeated hover recomputed");
        Log("PASS 100 repeated requests use cached result");
        Player.AddCustomStat(ECustomStat.PhysicalDamage,40);
        string changed=DamageTooltip.WeaponText(sample,Player);
        if(changed==first)throw new Exception("Elemental stat change did not invalidate");
        Log("PASS physical damage change refreshes tooltip");
        Player.AddCustomStat(ECustomStat.FireDamage,30);Player.AddCustomStat(ECustomStat.IceDamage,20);Player.AddCustomStat(ECustomStat.LightningDamage,10);
        var dummy=FindObjectsOfType<DamageDummy>().FirstOrDefault();
        if(!dummy)throw new Exception("Native damage dummy missing");
        dummy.AddCustomStat(ECustomStat.DamageReduction,-dummy.GetCustomStat(ECustomStat.DamageReduction));
        string[] bonusIds={"ALLDAMAGEBONUS","WEAPONCRITICALDAMAGE","WEAPONCRITICALDAMAGEAMPLIFY","WEAPONDAMAGEBONUSBYDASHCOUNT","DASHCOUNT","TRUEDAMAGE"};
        int[] bonusValues={25,20,50,10,2,3};
        for(int i=0;i<bonusIds.Length;i++)Player.AddCustomStatUnsafe(bonusIds[i],bonusValues[i]);
        foreach(bool direct in new[]{true,false})foreach(bool critical in new[]{false,true})
        {
            var snap=DamageTooltip.Capture(delegate{return DamageTooltip.DamagePair(Player,40,direct);},Player);
            var value=snap.Evaluate(new DamageTooltip.Hit{Raw=40,Weapon=direct});
            var hit=DamageInstance.GetDamage(Player,"TooltipValidation",dummy.transform.position,4294967295L,40,EDamageType.Slice,direct?EDamageFromType.DirectAttack:EDamageFromType.None,Vector2.zero,0,0);
            hit.criticalChancePercent=critical?100:0;
            var applied=dummy.ApplyDamage(hit);
            int expected=(int)(critical?value.Critical:value.Normal);
            if(applied!=EApplyDamageResult.Success||hit.damageResult!=expected)throw new Exception("Native hit mismatch: "+applied+" expected="+expected+" actual="+hit.damageResult);
            Log("PASS native dummy direct="+direct+" crit="+critical+" damage="+hit.damageResult);
        }
        for(int i=0;i<bonusIds.Length;i++)Player.AddCustomStatUnsafe(bonusIds[i],-bonusValues[i]);
        var savvy=ItemDatabase.GetAllItemCategory().First(c=>c.Name=="교섭");
        foreach(var set in savvy.setStatus)Log("SAVVY set "+set.itemCount+"="+set.status);
        var effect=savvy.comboEffectPrefab?savvy.comboEffectPrefab.GetComponent<ComboEffectBase>():null;
        if(effect)foreach(var stat in effect.addStatByCombo)Log("SAVVY combo "+stat.comboCount+"="+string.Join(",",stat.status));
        int originalMoney=Player.Money;
        Player.AddCustomStatUnsafe("GOLDHAND",1);
        int leaf=KeywordDatabase.GetConstValue("GOLDHANDLEAF"),cap=KeywordDatabase.GetConstValue("GOLDHANDMAX");
        foreach(int amount in new[]{0,leaf-1,leaf,leaf*(cap+20)})
        {
            Player.AddMoney(amount-Player.Money);
            var snapshot=DamageTooltip.Capture(delegate{return DamageTooltip.DamagePair(Player,100,false);},Player);
            var expected=snapshot.Evaluate(new DamageTooltip.Hit{Raw=100});
            var hit=DamageInstance.GetDamage(Player,"GoldHandValidation",dummy.transform.position,4294967295L,100,EDamageType.Slice,EDamageFromType.None,Vector2.zero,0,0);hit.criticalChancePercent=0;
            dummy.ApplyDamage(hit);
            if(hit.damageResult!=(int)expected.Normal)throw new Exception("GoldHand mismatch");
            Log("PASS GoldHand money="+amount+" damage="+hit.damageResult+" multiplier="+snapshot.Gold+" cap="+cap);
        }
        Player.AddCustomStatUnsafe("GOLDHANDUNLIMIT",1);
        var uncapped=DamageTooltip.Capture(delegate{return DamageTooltip.DamagePair(Player,100,false);},Player);
        var uncappedHit=DamageInstance.GetDamage(Player,"GoldHandUnlimitValidation",dummy.transform.position,4294967295L,100,EDamageType.Slice,EDamageFromType.None,Vector2.zero,0,0);uncappedHit.criticalChancePercent=0;dummy.ApplyDamage(uncappedHit);
        if(uncappedHit.damageResult!=(int)uncapped.Evaluate(new DamageTooltip.Hit{Raw=100}).Normal)throw new Exception("Uncapped GoldHand mismatch");
        Log("PASS GoldHand uncapped damage="+uncappedHit.damageResult);
        Player.AddCustomStatUnsafe("GOLDHANDUNLIMIT",-1);Player.AddCustomStatUnsafe("GOLDHAND",-1);Player.AddMoney(originalMoney-Player.Money);
        var vow=weapons.First(w=>w.id==414).mainWeaponPrefab.GetComponent<WeaponSimple_Katana>();
        if(!vow.useMagicBlade)throw new Exception("Vow magic blade data missing");
        foreach(int missing in new[]{0,Player.MaxMp/2,Player.MaxMp})
        {
            var snapshot=DamageTooltip.Capture(delegate{return DamageTooltip.WeaponText(vow,Player);},Player);
            var expected=snapshot.Evaluate(DamageTooltip.MagicBladeHit(vow,Player,missing));
            float raw=(vow.magicBladeDamge+missing*.7f)*(1+Player.GetCustomStat(ECustomStat.MagicDamageBonus)/100f);
            var hit=DamageInstance.GetDamage(Player,"MagicBladeValidation",dummy.transform.position,4294967295L,raw,EDamageType.Slice,EDamageFromType.Magic,Vector2.zero,0,0);hit.criticalChancePercent=100;dummy.ApplyDamage(hit);
            if(hit.damageResult!=(int)expected.Critical)throw new Exception("Magic blade resource mismatch");
            Log("PASS vow emptyMP="+missing+" normal="+expected.Normal+" crit="+hit.damageResult);
        }
        foreach(var entity in weapons)
        {
            var weapon=entity.mainWeaponPrefab.GetComponent<WeaponSimple>();
            string text=DamageTooltip.WeaponText(weapon,Player);
            Log("WEAPON "+entity.id+" "+entity.Name+" "+weapon.GetType().Name+" "+text.Replace("\n"," | "));
            weaponUI.Open(null,anchor,Vector2.zero,entity);
            yield return new WaitForSecondsRealtime(.15f);

            if(entity.id==0 || entity.id==414 || weapon.addons.Any(a=>a is WeaponAddonCommon_AdditionalElementalDamage))
            {
                weaponUI.GetComponent<DamageTooltipView>().OpenDetails();yield return null;
                var d=DamageDetailsDialog.Current;while(d.IsCalculating)yield return null;
                yield return Capture(d.GetComponent<UI_MessageBox_Yes>(),"details-weapon-"+entity.id+".png");
                d.GetComponent<UI_MessageBox_Yes>().Close();
            }
            weaponUI.Close();
        }
        var artifactUI=UIManager.Instance.GetElement<UI_CharmTooltip>(); artifactUI.Connect(Player.gameObject);
        var items=Resources.LoadAll<ItemEntity>("Item").Where(i=>i.type==EItemType.Charm && i.activeType!=EItemActiveType.Disabled && i.activeType!=EItemActiveType.TestOnly && i.resourcePrefab && i.resourcePrefab.GetComponent<Charm_Basic>()).OrderBy(i=>i.id).ToArray();
        Log("artifacts="+items.Length);
        var catalog=new System.Text.StringBuilder("id\tname\tavailability\tclass\tmax_level\tdamage_method\tbase_class\n");
        foreach(var entity in Resources.LoadAll<ItemEntity>("Item").Where(i=>i.type==EItemType.Charm && i.resourcePrefab && i.resourcePrefab.GetComponent<Charm_Basic>()).OrderBy(i=>i.id))
        {
            var c=entity.resourcePrefab.GetComponent<Charm_Basic>();var method=c.GetType().GetMethod("GetDamage");
            catalog.Append(entity.id).Append('\t').Append(entity.Name).Append('\t').Append(entity.activeType).Append('\t').Append(c.GetType().Name).Append('\t').Append(c.maxLevel).Append('\t').Append(method!=null?method.DeclaringType.Name:"").Append('\t').Append(c.GetType().BaseType.Name).AppendLine();
        }
        File.WriteAllText(Path.Combine(root,"artifact-catalog.tsv"),catalog.ToString());
        foreach(var entity in items)
        {
            var charm=entity.resourcePrefab.GetComponent<Charm_Basic>();
            string text=DamageTooltip.ArtifactText(entity,null,0,Player);
            for(int level=1;level<=charm.maxLevel;level++)
            {
                string levelText=DamageTooltip.ArtifactText(entity,null,level,Player);
                if(levelText.Contains("NaN")||levelText.Contains("Infinity"))throw new Exception("Invalid numeric artifact result "+entity.id+" level="+level);
            }
            Log("ARTIFACT "+entity.id+" "+entity.Name+" "+charm.GetType().Name+" "+text.Replace("\n"," | "));
            if(charm is Charm_AirSlash || charm is Charm_ElectricEarring || charm is Charm_FireBulletInRange || charm.GetType()==typeof(Charm_StatusInstance))
            {
                artifactUI.Open(null,anchor,Vector2.zero,entity);yield return new WaitForSecondsRealtime(.15f);

                artifactUI.GetComponent<DamageTooltipView>().OpenDetails();yield return null;
                var d=DamageDetailsDialog.Current;while(d.IsCalculating)yield return null;
                yield return Capture(d.GetComponent<UI_MessageBox_Yes>(),"details-artifact-"+entity.id+".png");d.GetComponent<UI_MessageBox_Yes>().Close();artifactUI.Close();
            }
        }
        int idle=DamageTooltip.Calculations;yield return new WaitForSecondsRealtime(2);
        if(DamageTooltip.Calculations!=idle)throw new Exception("Hidden tooltip computed damage");
        Log("PASS hidden tooltip has no repeated calculation; cacheHits="+DamageTooltip.CacheHits);
        Log("snapshot count="+DamageTooltip.CaptureCount+" max_ms="+DamageTooltip.MaxCaptureMilliseconds+" mean_ms="+(DamageTooltip.TotalCaptureMilliseconds/Math.Max(1,DamageTooltip.CaptureCount)));
        UIManager.Instance.CloseAllControl();yield return null;var options=UIManager.Instance.GetElement<UI_OptionsPanel>();options.Open();yield return null;
        var settings=options.GetComponent<PreviewOptions>();options.SelectTab(settings.TabIndex);yield return null;
        yield return Capture(options,"details-options.png");options.Close();
        Log("PASS COMPLETE"); Application.Quit(0);
    }
    private IEnumerator Capture(UIBase panel, string file)
    {
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        var cameraObject = new GameObject("RetryProbeCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 400;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = new RenderTexture(1280, 800, 24);
        camera.targetTexture = target;
        panel.rectTransform.anchorMin=panel.rectTransform.anchorMax=new Vector2(.5f,.5f);
        panel.rectTransform.pivot=new Vector2(.5f,.5f);
        panel.rectTransform.anchoredPosition=Vector2.zero;
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        foreach(var g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.SetAllDirty();
        Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var capture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); capture.Apply();
        File.WriteAllBytes(Path.Combine(root, file), capture.EncodeToPNG());
        Vector3[] corners=new Vector3[4]; panel.rectTransform.GetWorldCorners(corners);
        Vector3 lo=camera.WorldToScreenPoint(corners[0]), hi=camera.WorldToScreenPoint(corners[2]);
        int left=Mathf.Clamp(Mathf.FloorToInt(lo.x)-8,0,1279), bottom=Mathf.Clamp(Mathf.FloorToInt(lo.y)-8,0,799);
        int width=Mathf.Clamp(Mathf.CeilToInt(hi.x)-left+8,1,1280-left), height=Mathf.Clamp(Mathf.CeilToInt(hi.y)-bottom+8,1,800-bottom);
        var detail=new Texture2D(width,height,TextureFormat.RGB24,false);
        detail.ReadPixels(new Rect(left,bottom,width,height),0,0);detail.Apply();
        File.WriteAllBytes(Path.Combine(root,"detail-"+file),detail.EncodeToPNG());Destroy(detail);
        RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
        Destroy(capture); Destroy(cameraObject); target.Release(); Destroy(target);
    }
}
