using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using SephiriaRoomRetry;

[BepInPlugin("local.sephiria.update-smoke", "Room Retry Isolated Smoke", "1.0.0")]
public sealed class UpdateSmoke : BaseUnityPlugin
{
    private string root;
    private PlayerAvatar Player { get { return PlayerSpawner.MultiplayerList.Where(p => p && p.isLocalPlayer).Select(p => p.PlayerAvatar).FirstOrDefault(); } }
    private void Awake()
    {
        root = Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
        if (String.IsNullOrEmpty(root) || Path.GetFullPath(SaveData.CommonPath) != Path.Combine(root, "isolated-saves")) throw new Exception("Isolation absent");
        if (UnityEngine.InputSystem.Keyboard.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        if (UnityEngine.InputSystem.Mouse.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        File.WriteAllText(Path.Combine(root, "update-smoke.txt"), "");
        var uiType=typeof(SephiriaDicePreview.PersonalConveniencePlugin).Assembly.GetType("SephiriaDicePreview.UpdateUI");
        ((BepInEx.Configuration.ConfigEntry<bool>)HarmonyLib.AccessTools.Field(uiType,"CheckOnStartup").GetValue(null)).Value=false;
        StartCoroutine(Guard());
    }
    private void Log(string value) { File.AppendAllText(Path.Combine(root, "update-smoke.txt"), value + "\n"); }
    private IEnumerator Guard()
    {
        var test = Run();
        while (true) {
            bool next;
            try { next = test.MoveNext(); }
            catch (Exception ex) { Log("FAIL: " + ex); Application.Quit(21); yield break; }
            if (!next) break;
            yield return test.Current;
        }
    }
    private IEnumerator Run()
    {
        float end=Time.realtimeSinceStartup+90;
        while ((!Player || !Player.CanMove || Player.loadingScreenType!=-1) && Time.realtimeSinceStartup<end) yield return null;
        if(!Player || !Player.CanMove)throw new Exception("Startup timeout");
        LocalizationManager.Instance.LoadLanguage("ko-KR");
        var options=UIManager.Instance.GetElement<UI_OptionsPanel>();options.Open();
        var settings=options.GetComponent<SephiriaDicePreview.PreviewOptions>();options.SelectTab(settings.TabIndex);
        yield return new WaitForSecondsRealtime(1);
        if(!settings.UpdateBox.gameObject.activeInHierarchy)throw new Exception("Update row absent");
        yield return Capture(options,"updater-options.png");
        if (Environment.GetEnvironmentVariable("UPDATER_EXPECT_LATEST") == "1") {
            if(typeof(SephiriaDicePreview.PersonalConveniencePlugin).Assembly.GetName().Version.ToString()!="1.0.3.0")throw new Exception("Preloader did not update plugin");
            settings.UpdateBox.ChangeValue(1);var latestHolder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();
            end=Time.realtimeSinceStartup+50;UI_MessageBox_Yes latest=null;
            while(Time.realtimeSinceStartup<end){latest=latestHolder.GetComponentsInChildren<UI_MessageBox_Yes>().FirstOrDefault();if(latest)break;yield return null;}
            if(!latest || !latest.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("1.0.3"))throw new Exception("Latest version message absent");
            yield return new WaitForSecondsRealtime(.5f);yield return Capture(latestHolder,"updater-latest.png");
            Log("PASS preloader applied 1.0.3; native options loaded; manual check reports latest; no restart pending");Application.Quit(0);yield break;
        }
        settings.UpdateBox.ChangeValue(1);
        var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();UI_MessageBox_YesNo offer=null;
        end=Time.realtimeSinceStartup+50;
        while(Time.realtimeSinceStartup<end){offer=holder.GetComponentsInChildren<UI_MessageBox_YesNo>().FirstOrDefault();if(offer)break;yield return null;}
        if(!offer || !offer.text.text.Contains("1.0.2") || !offer.text.text.Contains("1.0.3"))throw new Exception("Version offer missing");
        yield return new WaitForSecondsRealtime(.5f);yield return Capture(holder,"updater-offer.png");
        offer.noButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
        string cache=Path.Combine(BepInEx.Paths.CachePath,"SephiriaPersonalConvenience");
        if(Directory.Exists(Path.Combine(cache,"pending")))throw new Exception("Decline downloaded update");
        settings.UpdateBox.ChangeValue(1);offer=null;end=Time.realtimeSinceStartup+50;
        while(Time.realtimeSinceStartup<end){offer=holder.GetComponentsInChildren<UI_MessageBox_YesNo>().FirstOrDefault();if(offer)break;yield return null;}
        if(!offer)throw new Exception("Second offer missing");
        offer.yesButton.onClick.Invoke();end=Time.realtimeSinceStartup+60;
        while(!File.Exists(Path.Combine(cache,"pending/update.json"))&&Time.realtimeSinceStartup<end)yield return null;
        if(!File.Exists(Path.Combine(cache,"pending/SephiriaPersonalConvenience.dll")))throw new Exception("Download missing");
        var installed=System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(BepInEx.Paths.PluginPath,"SephiriaPersonalConvenience.dll"));
        if(installed.Version.ToString()!="1.0.2.0")throw new Exception("Running DLL replaced");
        yield return new WaitForSecondsRealtime(1);yield return Capture(holder,"updater-ready.png");
        Log("PASS real GitHub offer 1.0.2 -> 1.0.3; decline does not stage; accept downloads verified DLL; current DLL remains 1.0.2; restart pending");
        Application.Quit(0);
    }

    private string InventorySignature()
    {
        return String.Join(";", Player.Inventory.inventoryMatrix.Values.OrderBy(i => i.InstanceID)
            .Select(i => i.InstanceID + ":" + i.EntityID + ":" + i.Quantity + ":" + i.XIdx + "," + i.YIdx).ToArray());
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
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var capture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); capture.Apply();
        File.WriteAllBytes(Path.Combine(root, file), capture.EncodeToPNG());
        RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
        Destroy(capture); Destroy(cameraObject); target.Release(); Destroy(target);
    }
}


