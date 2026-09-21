using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using SephiriaRoomRetry;

[BepInPlugin("local.sephiria.room-retry-smoke", "Room Retry Isolated Smoke", "1.0.0")]
public sealed class RetrySmoke : BaseUnityPlugin
{
    private string root;
    private PlayerAvatar Player { get { return PlayerSpawner.MultiplayerList.Where(p => p && p.isLocalPlayer).Select(p => p.PlayerAvatar).FirstOrDefault(); } }
    private void Awake()
    {
        root = Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
        if (String.IsNullOrEmpty(root) || Path.GetFullPath(SaveData.CommonPath) != Path.Combine(root, "isolated-saves")) throw new Exception("Isolation absent");
        if (UnityEngine.InputSystem.Keyboard.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        if (UnityEngine.InputSystem.Mouse.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        File.WriteAllText(Path.Combine(root, "retry-smoke.txt"), "");
        StartCoroutine(Guard());
    }
    private void Log(string value) { File.AppendAllText(Path.Combine(root, "retry-smoke.txt"), value + "\n"); }
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
        Log("isolation=" + SaveData.CommonPath);
        float end = Time.realtimeSinceStartup + 90;
        while ((!Player || !Player.CanMove || Player.loadingScreenType != -1) && Time.realtimeSinceStartup < end) yield return null;
        if (!Player || !Player.CanMove) throw new Exception("Startup timeout");
        OptionsBinding.Instance.Options.SetBool("Photosensitive_NeverShowAgain", true);
        OptionsBinding.Instance.Options.SetBool("DataCollectionAgreementInitialized", true);
        OptionsBinding.Instance.Options.SetString("GamePlayLanguage", "ko-KR");
        OptionsBinding.Instance.Options.Save();
        LocalizationManager.Instance.LoadLanguage("ko-KR");
        var plugin = SephiriaDicePreview.PersonalConveniencePlugin.Instance.GetComponent<RoomRetryPlugin>();
        string reason;
        if (plugin.CanRetry(out reason)) throw new Exception("Retry allowed in training");
        Log("training-blocked=" + reason);
        SaveManager.Current.SetString("PlayerName", "RETRY TEST");
        var charm = Resources.LoadAll<ItemEntity>("Item").First(i => i.type == EItemType.Charm && i.activeType == EItemActiveType.Default);
        Player.Inventory.AddItem(new ItemMetadata(190000001, charm.id, 1), 0, false);
        Player.Inventory.AddItem(new ItemMetadata(190000002, 0, 3), 0, false);
        yield return new WaitForSecondsRealtime(.5f);
        string stage = RaceDatabase.FindById(DungeonManager.Instance.raceId).stages[0].name;
        Log("stage=" + stage);
        DungeonManager.Instance.LoadStageAndMove(stage);
        end = Time.realtimeSinceStartup + 60;
        while ((!Player || Player.isInDungeon <= 0 || Player.loadingScreenType != -1 || SaveManager.IsSaving != SaveManager.ESaveState.None) && Time.realtimeSinceStartup < end) yield return null;
        yield return new WaitForSecondsRealtime(2);
        if (!plugin.CanRetry(out reason)) throw new Exception("Retry unavailable: " + reason);
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var panel = UIManager.Instance.GetElement<UI_PausePanel>();
            panel.Open();
            panel.hardModeInfoButton.SetActive(true); // Match the user's full menu layout.
            panel.GetComponent<RetryMenu>().Refresh(panel);
            yield return new WaitForSecondsRealtime(1);
            var button = panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "RetryCurrentRoom");
            if (!button.gameObject.activeInHierarchy) throw new Exception("Button hidden");
            if (attempt == 1) {
                var options = UIManager.Instance.GetElement<UI_OptionsPanel>(); options.Open();
                var setting = options.GetComponent<SephiriaDicePreview.PreviewOptions>(); options.SelectTab(setting.TabIndex);
                setting.RetryBox.ChangeValue(0); yield return new WaitForSecondsRealtime(.3f);
                if(button.gameObject.activeSelf) throw new Exception("Pause retry toggle off not immediate");
                options.Close(); yield return new WaitForSecondsRealtime(.2f); Capture(panel,"retry-disabled.png");
                options.Open(); options.SelectTab(setting.TabIndex); setting.RetryBox.ChangeValue(1);
                yield return new WaitForSecondsRealtime(.3f);
                if(!button.gameObject.activeSelf)throw new Exception("Pause retry toggle on not immediate");
                options.Close(); yield return new WaitForSecondsRealtime(.2f);
                Log("PASS retry off/on updates already-open pause menu immediately");
            }
            string floor = Player.currentFloorGuid;
            float hp = Player.hp;
            int money = Player.Money;
            string inventory = InventorySignature();
            float maxHp = Player.MaxHp;
            int mp = Player.MP;
            if (Player.Inventory.inventoryMatrix.Count == 0) throw new Exception("Empty inventory fixture");
            Log("attempt=" + attempt + ";floor=" + floor + ";hp=" + hp + ";money=" + money);
            var above = button.FindSelectableOnUp();
            var below = button.FindSelectableOnDown();
            if (!above || !below || above.FindSelectableOnDown() != button || below.FindSelectableOnUp() != button) throw new Exception("Controller navigation failed");
            Log("navigation=" + above.name + " -> " + button.name + " -> " + below.name);
            foreach (var b in panel.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.activeInHierarchy)) {
                var rect = (RectTransform)b.transform;
                Log("button=" + b.name + ";pos=" + rect.position + ";size=" + rect.rect.size + ";components=" + String.Join(",", b.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()));
            }
            Capture(panel, "retry-menu-" + attempt + ".png");
            yield return new WaitForSecondsRealtime(.4f);
            if (attempt == 1)
            {
                string path = Path.Combine(SaveData.CommonPath, "SLOT1TMP.sav");
                string backup = path + ".retry-test";
                File.Move(path, backup);
                try {
                    button.onClick.Invoke();
                    if (plugin.Busy || !Player || Player.currentFloorGuid != floor) throw new Exception("Missing checkpoint changed session");
                    Log("missing-checkpoint=blocked; current session preserved");
                } finally { File.Move(backup, path); }
            }
            Player.Networkhp = Mathf.Max(1, hp - 9);
            Player.Networkmp = 0;
            Player.Inventory.ForceRemoveAll();
            Player.AddMoney(777);
            Log("damaged-hp=" + Player.hp);
            var connectionBefore = NetworkServer.localConnection;
            var managerBefore = NetworkManager.singleton;
            int sceneBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            float retryStarted = Time.realtimeSinceStartup;
            button.onClick.Invoke();
            if (!plugin.Busy) throw new Exception("Retry rejected");
            if (plugin.CanRetry(out reason)) throw new Exception("Duplicate retry allowed");
            end = Time.realtimeSinceStartup + 90;
            while (plugin.Busy && Time.realtimeSinceStartup < end) yield return null;
            float elapsed = Time.realtimeSinceStartup - retryStarted;
            Log("elapsed-seconds=" + elapsed.ToString("F3"));
            if (NetworkServer.localConnection != connectionBefore || NetworkManager.singleton != managerBefore ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != sceneBefore) throw new Exception("Retry replaced session/scene");
            if (plugin.Busy || !Player || Player.currentFloorGuid != floor || Player.loadingScreenType != -1) throw new Exception("Resume failed");
            UIManager.Instance.GetElement<UI_PausePanel>().Open();
            Log("restored-hp=" + Player.hp + ";floor=" + Player.currentFloorGuid + ";money=" + Player.Money);
            if (Player.hp != hp) throw new Exception("HP did not restore");
            if (Player.Money != money) throw new Exception("Money did not restore");
            if (InventorySignature() != inventory || Player.MaxHp != maxHp || Player.MP != mp)
                throw new Exception("Inventory/stats/MP did not restore: " + InventorySignature() + " expected " + inventory);
            Log("inventory-and-stats=restored; " + inventory + ";maxHp=" + Player.MaxHp + ";mp=" + Player.MP);
            yield return new WaitForSecondsRealtime(.5f);
        }
        Log("PASS: two native resumes to the same room; HP rollback; one menu entry");
        var finalPanel = UIManager.Instance.GetElement<UI_PausePanel>();
        finalPanel.hardModeInfoButton.SetActive(true);
        finalPanel.GetComponent<RetryMenu>().Refresh(finalPanel);
        Capture(finalPanel, "retry-final.png");
        yield return new WaitForSecondsRealtime(1);
        Application.Quit(0);
    }

    private string InventorySignature()
    {
        return String.Join(";", Player.Inventory.inventoryMatrix.Values.OrderBy(i => i.InstanceID)
            .Select(i => i.InstanceID + ":" + i.EntityID + ":" + i.Quantity + ":" + i.XIdx + "," + i.YIdx).ToArray());
    }

    private void Capture(UI_PausePanel panel, string file)
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
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var capture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); capture.Apply();
        File.WriteAllBytes(Path.Combine(root, file), capture.EncodeToPNG());
        RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
        Destroy(capture); Destroy(cameraObject); target.Release(); Destroy(target);
    }
}


