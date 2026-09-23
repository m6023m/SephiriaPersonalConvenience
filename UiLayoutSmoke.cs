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
        if(Environment.GetEnvironmentVariable("SEPHIRIA_DEDICATED_SCREENSHOTS")=="1")
        {
            var dedicated=CaptureDedicated();while(dedicated.MoveNext())yield return dedicated.Current;
            Log("PASS DEDICATED SCREENSHOTS");Application.Quit(0);yield break;
        }
        if(DamageTooltip.ColorText("1",EDamageElementalType.Physical)!="<color=#FFFFFF>1</color>"||
            DamageTooltip.ColorText("1",EDamageElementalType.Fire)!="<color=#FFC900>1</color>"||
            DamageTooltip.ColorText("1",EDamageElementalType.Ice)!="<color=#5F9FFF>1</color>"||
            DamageTooltip.ColorText("1",EDamageElementalType.Lightning)!="<color=#87FFFF>1</color>"||
            DamageTooltip.ColorText("1",EDamageElementalType.Chaos)!="<color=#C06CFF>1</color>")throw new Exception("Element damage palette mismatch");
        var anchor=new GameObject("TooltipProbeAnchor",typeof(RectTransform)).GetComponent<RectTransform>();
        var weaponUI=UIManager.Instance.GetElement<UI_WeaponTooltip>(); weaponUI.Connect(Player.gameObject);
        anchor.SetParent(weaponUI.transform.parent,false); anchor.anchoredPosition=new Vector2(80,650);
        var weapons=Resources.LoadAll<WeaponEntity>("Weapon").Where(w=>w.enabled && w.mainWeaponPrefab && w.mainWeaponPrefab.GetComponent<WeaponSimple>()).OrderBy(w=>w.id).ToArray();
        Log("weapons="+weapons.Length);
        var sample=weapons[0].mainWeaponPrefab.GetComponent<WeaponSimple>();
        foreach(var icons in Resources.LoadAll<ControllerIconSet>("ControllerIconSet"))Log("ICONS "+icons.name+" "+string.Join(",",icons.buttons.Select(b=>b.name).ToArray()));
        Player.AddCustomStatUnsafe("ELITEDAMAGE",50);
        var controls=ControlsChangeHandler.Current;
        controls.PlayerInput.SwitchCurrentControlScheme("Keyboard&Mouse",Keyboard.current,Mouse.current);controls.HandleOnControlsChanged(controls.PlayerInput);
        weaponUI.Open(null,anchor,Vector2.zero,weapons[0]);yield return null;
        yield return Capture(weaponUI,"ui-layout-tooltip.png");
        weaponUI.GetComponent<DamageTooltipView>().OpenDetails();yield return null;
        var dialog=DamageDetailsDialog.Current;var modal=dialog.GetComponent<UI_MessageBox_Yes>();
        while(dialog.IsCalculating)yield return null;
        if(modal.text.text.Contains("보스·미니보스"))throw new Exception("Boss rows visible");
        var viewedHeader=modal.transform.Find("DamageViewedItemHeader");
        if(!viewedHeader||!viewedHeader.gameObject.activeSelf)throw new Exception("Viewed item header missing");
        var viewedIcon=viewedHeader.Find("ViewedItemIcon").GetComponent<UnityEngine.UI.Image>();
        if(!viewedIcon.sprite)throw new Exception("Viewed item icon missing");
        if(!viewedHeader.GetComponentInChildren<TMPro.TMP_Text>(true).text.Contains(weaponUI.weaponNameText.text))throw new Exception("Viewed item name mismatch");
        foreach(var glyph in modal.GetComponentsInChildren<UI_PadGlyphImage>())if(!glyph.glyphImage.sprite)throw new Exception("Keyboard glyph missing: "+glyph.iconName);
        yield return Capture(modal,"ui-layout-keyboard.png");
        var stableSize=modal.rectTransform.sizeDelta;int stableCalculations=DamageTooltip.Calculations;
        DamageTooltip.Invalidate(Player);
        float stableDeadline=Time.realtimeSinceStartup+1;
        while(Time.realtimeSinceStartup<stableDeadline)
        {
            if(modal.text.text.Contains("#EDB14D"))throw new Exception("Unchanged invalidation flashed calculating state");
            if((modal.rectTransform.sizeDelta-stableSize).sqrMagnitude>.001f)throw new Exception("Unchanged invalidation resized dialog");
            yield return null;
        }
        if(dialog.IsCalculating)throw new Exception("Unchanged invalidation did not settle");
        if(DamageTooltip.Calculations!=stableCalculations)throw new Exception("Unchanged invalidation repeated numeric calculation");
        string detailPage=modal.text.text;
        modal.text.text="<color=#81E6C4>속성 피해 색상</color>\n"+
            DamageTooltip.ColorText("물리 피해 120",EDamageElementalType.Physical)+"\n"+
            DamageTooltip.ColorText("화염 피해 120",EDamageElementalType.Fire)+"\n"+
            DamageTooltip.ColorText("냉기 피해 120",EDamageElementalType.Ice)+"\n"+
            DamageTooltip.ColorText("번개 피해 120",EDamageElementalType.Lightning)+"\n"+
            DamageTooltip.ColorText("혼돈 피해 120",EDamageElementalType.Chaos);
        yield return Capture(modal,"ui-layout-element-palette.png");
        modal.text.text=detailPage;
        dialog.ToggleFullBuff();while(dialog.IsCalculating)yield return null;
        yield return Capture(modal,"ui-layout-fullbuff.png");
        var pad=InputSystem.AddDevice<Gamepad>();
        controls.PlayerInput.SwitchCurrentControlScheme("Gamepad",pad);controls.HandleOnControlsChanged(controls.PlayerInput);
        yield return null;yield return null;
        var tooltipView=weaponUI.GetComponent<DamageTooltipView>();
        var detailShortcut=(UI_PadGlyphImage)HarmonyLib.AccessTools.Field(typeof(DamageTooltipView),"openGlyph").GetValue(tooltipView);
        var dpsShortcut=(UI_PadGlyphImage)HarmonyLib.AccessTools.Field(typeof(DamageTooltipView),"dpsGlyph").GetValue(tooltipView);
        if(detailShortcut.iconName!="leftStickPress"||dpsShortcut.iconName!="rightStickPress")throw new Exception("Tooltip stick shortcuts were not reversed");
        foreach(var glyph in modal.GetComponentsInChildren<UI_PadGlyphImage>())if(!glyph.glyphImage.sprite)throw new Exception("Gamepad glyph missing: "+glyph.iconName);
        yield return Capture(modal,"ui-layout-gamepad.png");
        foreach(var glyph in modal.GetComponentsInChildren<UI_PadGlyphImage>())Log("GLYPH "+glyph.iconName+" sprite="+(glyph.glyphImage.sprite?glyph.glyphImage.sprite.name:"MISSING"));
        foreach(var rt in modal.GetComponentsInChildren<RectTransform>())if(rt.name.Contains("Button"))Log("RECT "+rt.name+" parent="+rt.parent.name+" rect="+rt.rect+" pos="+rt.anchoredPosition);
        modal.Close();yield return null;
        weaponUI.GetComponent<DamageTooltipView>().OpenDps();yield return null;
        dialog=DamageDetailsDialog.Current;modal=dialog.GetComponent<UI_MessageBox_Yes>();
        float deadline=Time.realtimeSinceStartup+15;
        while(dialog.IsCalculating&&Time.realtimeSinceStartup<deadline)yield return null;
        if(dialog.IsCalculating)throw new Exception("DPS worker timeout");
        if(!modal.text.text.Contains("DPS"))throw new Exception("DPS dialog missing output");
        yield return Capture(modal,"ui-layout-dps-gamepad.png");
        controls.PlayerInput.SwitchCurrentControlScheme("Keyboard&Mouse",Keyboard.current,Mouse.current);controls.HandleOnControlsChanged(controls.PlayerInput);
        yield return null;yield return null;
        yield return Capture(modal,"ui-layout-dps-keyboard.png");
        dialog.ToggleFullBuff();deadline=Time.realtimeSinceStartup+15;
        while(dialog.IsCalculating&&Time.realtimeSinceStartup<deadline)yield return null;
        if(dialog.IsCalculating)throw new Exception("Full-buff DPS worker timeout");
        yield return Capture(modal,"ui-layout-dps-fullbuff.png");
        modal.Close();yield return null;
        var artifactEntities=Resources.LoadAll<ItemEntity>("Item").Where(i=>i.type==EItemType.Charm&&i.activeType!=EItemActiveType.Disabled&&i.activeType!=EItemActiveType.TestOnly&&i.resourcePrefab&&i.resourcePrefab.GetComponent<Charm_Basic>()).OrderBy(i=>i.id).Take(2).ToArray();
        if(artifactEntities.Length<2)throw new Exception("Artifact identity fixtures missing");
        var artifactUI=UIManager.Instance.GetElement<UI_CharmTooltip>();artifactUI.Connect(Player.gameObject);
        var dialogIdField=HarmonyLib.AccessTools.Field(typeof(DamageDetailsDialog),"id");
        for(int i=0;i<artifactEntities.Length;i++)
        {
            var selectedArtifact=artifactEntities[i];
            artifactUI.Open(null,anchor,Vector2.zero,selectedArtifact);yield return null;
            // Training and inventory screens reuse this tooltip. Simulate another
            // equipped artifact updating the public field after UpdateData bound it.
            artifactUI.currentEntity=artifactEntities[1-i];
            artifactUI.GetComponent<DamageTooltipView>().OpenDetails();yield return null;
            dialog=DamageDetailsDialog.Current;modal=dialog.GetComponent<UI_MessageBox_Yes>();while(dialog.IsCalculating)yield return null;
            string dialogId=(string)dialogIdField.GetValue(dialog);
            if(!dialogId.StartsWith("A"+selectedArtifact.id+":"))throw new Exception("Artifact detail target drifted: expected "+selectedArtifact.id+" actual "+dialogId);
            yield return Capture(modal,"ui-layout-artifact-identity-"+selectedArtifact.id+".png");
            modal.Close();artifactUI.Close();yield return null;
        }
        Log("PASS artifact target remains bound across reused tooltip fields");
        var comboCategory=ItemDatabase.GetAllItemCategory().First(c=>c.comboEffectPrefab&&c.comboEffectPrefab.GetComponent<ComboEffect_Debuff>());
        var comboEffect=comboCategory.comboEffectPrefab.GetComponent<ComboEffectBase>();
        var comboData=new SynergyTooltipData{itemCategory=comboCategory,comboEffectInstance=comboEffect,comboCount=comboEffect.GetHighestComboCount(),showDropBonus=false};
        var comboUI=UIManager.Instance.GetElement<UI_SynergyTooltip>();comboUI.Connect(Player.gameObject);
        comboUI.Open(null,anchor,Vector2.zero,comboData);yield return null;
        var comboView=comboUI.GetComponent<DamageTooltipView>();
        if(!comboView)throw new Exception("Combo detail buttons missing");
        yield return Capture(comboUI,"ui-layout-combo-tooltip.png");
        comboView.OpenDetails();yield return null;dialog=DamageDetailsDialog.Current;modal=dialog.GetComponent<UI_MessageBox_Yes>();
        while(dialog.IsCalculating)yield return null;
        if(!modal.text.text.Contains("콤보"))throw new Exception("Combo detail output missing");
        yield return Capture(modal,"ui-layout-combo-detail.png");
        modal.Close();yield return null;comboView.OpenDps();yield return null;dialog=DamageDetailsDialog.Current;modal=dialog.GetComponent<UI_MessageBox_Yes>();
        deadline=Time.realtimeSinceStartup+15;while(dialog.IsCalculating&&Time.realtimeSinceStartup<deadline)yield return null;
        if(dialog.IsCalculating||!modal.text.text.Contains("DPS"))throw new Exception("Combo DPS output missing");
        yield return Capture(modal,"ui-layout-combo-dps.png");
        Log("PASS COMPLETE");Application.Quit(0);
    }
    private IEnumerator CaptureDedicated()
    {
        var anchor=new GameObject("DedicatedTooltipAnchor",typeof(RectTransform)).GetComponent<RectTransform>();
        var weaponUI=UIManager.Instance.GetElement<UI_WeaponTooltip>();weaponUI.Connect(Player.gameObject);
        anchor.SetParent(weaponUI.transform.parent,false);anchor.anchoredPosition=new Vector2(80,650);
        int[] ids={6,119,120,121,127,416,417,421,507,518,1001,1023,1101,1121,1200,1216};
        var weapons=Resources.LoadAll<WeaponEntity>("Weapon").Where(w=>ids.Contains(w.id)&&w.enabled&&w.mainWeaponPrefab).OrderBy(w=>w.id).ToArray();
        foreach(var entity in weapons)
        {
            weaponUI.Open(null,anchor,Vector2.zero,entity);yield return null;
            var view=weaponUI.GetComponent<DamageTooltipView>();
            view.OpenDetails();yield return null;
            var dialog=DamageDetailsDialog.Current;var modal=dialog.GetComponent<UI_MessageBox_Yes>();
            float deadline=Time.realtimeSinceStartup+20;while(dialog.IsCalculating&&Time.realtimeSinceStartup<deadline)yield return null;
            if(dialog.IsCalculating)throw new Exception("Detail timeout weapon:"+entity.id);
            if(entity.id==507||entity.id==1121){dialog.ToggleFullBuff();while(dialog.IsCalculating)yield return null;}
            var pages=CapturePages(dialog,modal,"dedicated-weapon-"+entity.id+"-detail");while(pages.MoveNext())yield return pages.Current;
            modal.Close();yield return null;
            view.OpenDps();yield return null;dialog=DamageDetailsDialog.Current;modal=dialog.GetComponent<UI_MessageBox_Yes>();
            deadline=Time.realtimeSinceStartup+20;while(dialog.IsCalculating&&Time.realtimeSinceStartup<deadline)yield return null;
            if(dialog.IsCalculating)throw new Exception("DPS timeout weapon:"+entity.id);
            if(entity.id==507||entity.id==1121){dialog.ToggleFullBuff();while(dialog.IsCalculating)yield return null;}
            pages=CapturePages(dialog,modal,"dedicated-weapon-"+entity.id+"-dps");while(pages.MoveNext())yield return pages.Current;
            modal.Close();yield return null;weaponUI.Close();yield return null;
        }
        var dedicatedCombos=ItemDatabase.GetAllItemCategory().Where(c=>
        {
            var effect=c.comboEffectPrefab?c.comboEffectPrefab.GetComponent<ComboEffectBase>():null;
            return effect is ComboEffect_Debuff||effect is ComboEffect_DarkCloud||effect is ComboEffect_FlameSword;
        }).ToArray();
        var comboUI=UIManager.Instance.GetElement<UI_SynergyTooltip>();comboUI.Connect(Player.gameObject);
        int comboIndex=0;
        foreach(var comboCategory in dedicatedCombos)
        {
            var combo=comboCategory.comboEffectPrefab.GetComponent<ComboEffectBase>();
            var debuff=combo as ComboEffect_Debuff;
            string suffix=debuff&&debuff.debuffPrefab?debuff.debuffPrefab.ID.ToLowerInvariant():combo.GetType().Name.ToLowerInvariant();
            string prefix="dedicated-combo-"+(comboIndex++)+"-"+suffix;
            comboUI.Open(null,anchor,Vector2.zero,new SynergyTooltipData{itemCategory=comboCategory,comboEffectInstance=combo,comboCount=combo.GetHighestComboCount(),showDropBonus=false});yield return null;
            var comboView=comboUI.GetComponent<DamageTooltipView>();comboView.OpenDetails();yield return null;
            var comboDialog=DamageDetailsDialog.Current;var comboModal=comboDialog.GetComponent<UI_MessageBox_Yes>();while(comboDialog.IsCalculating)yield return null;
            var comboPages=CapturePages(comboDialog,comboModal,prefix+"-detail");while(comboPages.MoveNext())yield return comboPages.Current;comboModal.Close();yield return null;
            comboView.OpenDps();yield return null;comboDialog=DamageDetailsDialog.Current;comboModal=comboDialog.GetComponent<UI_MessageBox_Yes>();while(comboDialog.IsCalculating)yield return null;
            comboPages=CapturePages(comboDialog,comboModal,prefix+"-dps");while(comboPages.MoveNext())yield return comboPages.Current;comboModal.Close();yield return null;comboUI.Close();yield return null;
        }
    }
    private IEnumerator CapturePages(DamageDetailsDialog dialog,UI_MessageBox_Yes modal,string prefix)
    {
        var match=System.Text.RegularExpressions.Regex.Match(modal.text.text,@"<align=center>(\d+) / (\d+)</align>");
        int count=match.Success?Int32.Parse(match.Groups[2].Value):1;
        var field=HarmonyLib.AccessTools.Field(typeof(DamageDetailsDialog),"page");
        var draw=HarmonyLib.AccessTools.Method(typeof(DamageDetailsDialog),"Draw");
        for(int i=0;i<count;i++)
        {
            field.SetValue(dialog,i);draw.Invoke(dialog,null);yield return null;
            var capture=Capture(modal,prefix+"-p"+(i+1)+".png");while(capture.MoveNext())yield return capture.Current;
        }
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
