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
        while((!Player || !Player.CanMove || Player.loadingScreenType!=-1)&&Time.realtimeSinceStartup<end)yield return null;
        if(!Player)throw new Exception("Startup failed");
        LocalizationManager.Instance.LoadLanguage("ko-KR");
        var anchor=new GameObject("AuditAnchor",typeof(RectTransform)).GetComponent<RectTransform>();
        var weaponUI=UIManager.Instance.GetElement<UI_WeaponTooltip>();weaponUI.Connect(Player.gameObject);
        anchor.SetParent(weaponUI.transform.parent,false);anchor.anchoredPosition=new Vector2(80,650);
        var artifactUI=UIManager.Instance.GetElement<UI_CharmTooltip>();artifactUI.Connect(Player.gameObject);
        foreach(var entity in Resources.LoadAll<WeaponEntity>("Weapon").Where(w=>w.enabled&&w.mainWeaponPrefab&&w.mainWeaponPrefab.GetComponent<WeaponSimple>()).OrderBy(w=>w.id))
        {
            var routine=AuditOne("weapon-"+entity.id,entity.Name,weaponUI,()=>weaponUI.Open(null,anchor,Vector2.zero,entity));
            while(true){bool next;try{next=routine.MoveNext();}catch(Exception e){Log("ERROR weapon-"+entity.id+" "+e);break;}if(!next)break;yield return routine.Current;}
            if(DamageDetailsDialog.Current)DamageDetailsDialog.Current.GetComponent<UI_MessageBox_Yes>().Close();weaponUI.Close();yield return null;
        }
        foreach(var entity in Resources.LoadAll<ItemEntity>("Item").Where(i=>i.type==EItemType.Charm&&i.activeType!=EItemActiveType.Disabled&&i.activeType!=EItemActiveType.TestOnly&&i.resourcePrefab&&i.resourcePrefab.GetComponent<Charm_Basic>()).OrderBy(i=>i.id))
        {
            for(int level=0;level<=entity.resourcePrefab.GetComponent<Charm_Basic>().maxLevel;level++)
        {
            int selectedLevel=level;
            var routine=AuditOne("artifact-"+entity.id+"-level-"+level,entity.Name,artifactUI,()=>{artifactUI.Open(null,anchor,Vector2.zero,entity);HarmonyLib.AccessTools.Method(typeof(UI_CharmTooltip),"UpdateData").Invoke(artifactUI,new object[]{entity,selectedLevel});artifactUI.GetComponent<DamageTooltipView>().Offset=selectedLevel;});
            while(true){bool next;try{next=routine.MoveNext();}catch(Exception e){Log("ERROR artifact-"+entity.id+" "+e);break;}if(!next)break;yield return routine.Current;}
            if(DamageDetailsDialog.Current)DamageDetailsDialog.Current.GetComponent<UI_MessageBox_Yes>().Close();artifactUI.Close();yield return null;
        }
        }
        Log("AUDIT CAPTURE COMPLETE; screenshots are not accuracy verdicts");Application.Quit(0);
    }
    private IEnumerator AuditOne(string id,string name,UIBase tooltip,Action open)
    {
        Log("BEGIN "+id+" "+name);open();yield return new WaitForSecondsRealtime(.15f);
        tooltip.GetComponent<DamageTooltipView>().OpenDetails();yield return null;
        var dialog=DamageDetailsDialog.Current;if(!dialog)throw new Exception("Details dialog missing");
        for(int mode=0;mode<2;mode++)
        {
            if(mode==1){dialog.ToggleFullBuff();yield return null;}
            float deadline=Time.realtimeSinceStartup+10;
            while(dialog.IsCalculating&&Time.realtimeSinceStartup<deadline)yield return null;
            var modal=dialog.GetComponent<UI_MessageBox_Yes>();
            string modeName=mode==0?"current":"full";
            string value=(string)HarmonyLib.AccessTools.Field(typeof(DamageDetailsDialog),"result").GetValue(dialog);
            int pages=Math.Max(1,(value.Trim().Split('\n').Length+9)/10);
            bool enabled=dialog.enabled;dialog.enabled=false;
            for(int page=0;page<pages;page++)
            {
                HarmonyLib.AccessTools.Field(typeof(DamageDetailsDialog),"page").SetValue(dialog,page);
                HarmonyLib.AccessTools.Method(typeof(DamageDetailsDialog),"Draw").Invoke(dialog,null);
                string key=id+"-"+modeName+"-page-"+(page+1);
                Log((dialog.IsCalculating?"TIMEOUT ":"RESULT ")+key+" "+name+" "+modal.text.text.Replace("\n"," | "));
                var capture=Capture(modal,"extended-"+key+".png");while(capture.MoveNext())yield return capture.Current;
                Log("CAPTURED "+key);
            }
            dialog.enabled=enabled;
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
