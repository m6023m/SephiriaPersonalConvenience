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
        foreach(var glyph in modal.GetComponentsInChildren<UI_PadGlyphImage>())if(!glyph.glyphImage.sprite)throw new Exception("Keyboard glyph missing: "+glyph.iconName);
        yield return Capture(modal,"ui-layout-keyboard.png");
        dialog.ToggleFullBuff();while(dialog.IsCalculating)yield return null;
        yield return Capture(modal,"ui-layout-fullbuff.png");
        var pad=InputSystem.AddDevice<Gamepad>();
        controls.PlayerInput.SwitchCurrentControlScheme("Gamepad",pad);controls.HandleOnControlsChanged(controls.PlayerInput);
        yield return null;yield return null;
        foreach(var glyph in modal.GetComponentsInChildren<UI_PadGlyphImage>())if(!glyph.glyphImage.sprite)throw new Exception("Gamepad glyph missing: "+glyph.iconName);
        yield return Capture(modal,"ui-layout-gamepad.png");
        foreach(var glyph in modal.GetComponentsInChildren<UI_PadGlyphImage>())Log("GLYPH "+glyph.iconName+" sprite="+(glyph.glyphImage.sprite?glyph.glyphImage.sprite.name:"MISSING"));
        foreach(var rt in modal.GetComponentsInChildren<RectTransform>())if(rt.name.Contains("Button"))Log("RECT "+rt.name+" parent="+rt.parent.name+" rect="+rt.rect+" pos="+rt.anchoredPosition);
        Log("PASS COMPLETE");Application.Quit(0);
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
