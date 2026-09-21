using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using SephiriaRoomRetry;

[BepInPlugin("local.sephiria.stage-retry-smoke", "Room Retry Isolated Smoke", "1.0.0")]
public sealed class StageSmoke : BaseUnityPlugin
{
    private string root;
    private PlayerAvatar Player { get { return PlayerSpawner.MultiplayerList.Where(p => p && p.isLocalPlayer).Select(p => p.PlayerAvatar).FirstOrDefault(); } }
    private void Awake()
    {
        root = Environment.GetEnvironmentVariable("SEPHIRIA_COMBAT_TEST_ROOT");
        if (String.IsNullOrEmpty(root) || Path.GetFullPath(SaveData.CommonPath) != Path.Combine(root, "isolated-saves")) throw new Exception("Isolation absent");
        if (UnityEngine.InputSystem.Keyboard.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        if (UnityEngine.InputSystem.Mouse.current == null) UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        File.WriteAllText(Path.Combine(root, "stage-smoke.txt"), "");
        StartCoroutine(Guard());
    }
    private void Log(string value) { File.AppendAllText(Path.Combine(root, "stage-smoke.txt"), value + "\n"); }
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
        SwitchManager.SetDestinySwitch("EnableTowntreePortal", true);
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
        for (int attempt = 1; attempt <= (Environment.GetEnvironmentVariable("STAGE_UI_ONLY") == "1" ? 0 : 2); attempt++)
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
                options.Close(); yield return new WaitForSecondsRealtime(.2f); yield return Capture(panel,"retry-disabled.png");
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
            yield return Capture(panel, "retry-menu-" + attempt + ".png");
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
        if (Environment.GetEnvironmentVariable("STAGE_UI_ONLY") != "1") Log("PASS: two native resumes to the same room; HP rollback; one menu entry");
        var finalPanel = UIManager.Instance.GetElement<UI_PausePanel>();
        finalPanel.Open();
        finalPanel.hardModeInfoButton.SetActive(true);
        finalPanel.GetComponent<RetryMenu>().Refresh(finalPanel);
        yield return Capture(finalPanel, "retry-final.png");
        yield return new WaitForSecondsRealtime(1);
        finalPanel.Close();
        for (int stageIndex = 0; stageIndex < (Environment.GetEnvironmentVariable("STAGE_UI_ONLY") == "1" ? 1 : 2); stageIndex++)
        {
            if (stageIndex > 0) {
                string nextStage = RaceDatabase.FindById(DungeonManager.Instance.raceId).stages[stageIndex].name;
                DungeonManager.Instance.LoadStageAndMove(nextStage);
                yield return new WaitForSecondsRealtime(3);
                end = Time.realtimeSinceStartup + 60;
                while ((Player.loadingScreenType != -1 || SaveManager.IsSaving != SaveManager.ESaveState.None) && Time.realtimeSinceStartup < end) yield return null;
            }
            string head = Player.currentFloorGuid;
            string stageInv = InventorySignature(); int stageMoney = Player.Money; float stageHp = Player.hp; int stageMp = Player.MP;
            int sapphire = SaveManager.Current.GetInt("Sapphire", 0);
            string currentStage = DungeonManager.Instance.generatedFloors[head].stageName;
            string nextFloor = DungeonManager.Instance.GetAllFloorInStage(currentStage).First(f => f.guid != head).guid;
            for (int attempt = 0; attempt < (Environment.GetEnvironmentVariable("STAGE_UI_ONLY") == "1" ? 1 : 2); attempt++) {
                DungeonManager.Instance.MoveTogether(nextFloor, "FLOORSTARTING", 0, false, true);
                yield return new WaitForSecondsRealtime(3);
                end = Time.realtimeSinceStartup + 60;
                while ((Player.loadingScreenType != -1 || SaveManager.IsSaving != SaveManager.ESaveState.None) && Time.realtimeSinceStartup < end) yield return null;
                if (Player.currentFloorGuid == head) throw new Exception("Did not leave stage head");
                Player.AddMoney(777); Player.Networkmp = 0;
                Player.GetComponent<PlayerSpawner>().NetworksapphireInRun = 17;
                Player.Inventory.ForceRemoveAll();
                Player.ForceDie();
                var death = UIManager.Instance.GetElement<UI_GameOverLabel>();
                end = Time.realtimeSinceStartup + 40;
                while ((!death.IsOpened || !death.button.gameObject.activeInHierarchy || !death.button.interactable || SaveManager.IsSaving != SaveManager.ESaveState.None) && Time.realtimeSinceStartup < end) yield return null;
                yield return new WaitForSecondsRealtime(1);
                if (SaveManager.Current.GetInt("Sapphire", 0) <= sapphire) throw new Exception("Positive death settlement was not exercised");
                Log("death settlement sapphire=" + SaveManager.Current.GetInt("Sapphire", 0) + ";stage entry=" + sapphire);
                var stageMenu = death.GetComponent<StageRetryMenu>();
                if (!stageMenu || !stageMenu.Button.gameObject.activeInHierarchy) throw new Exception("Death retry button absent");
                yield return Capture(death, "stage-death-" + stageIndex + "-" + attempt + ".png");
                if (stageIndex == 0 && attempt == 0) {
                    string checkpointPath = Path.Combine(SaveData.CommonPath, SaveManager.Binded + ".stage-retry");
                    byte[] checkpoint = File.ReadAllBytes(checkpointPath);
                    try {
                        File.WriteAllText(checkpointPath, "invalid");
                        stageMenu.Button.onClick.Invoke();
                        if (plugin.Busy || !Player.IsDead || !death.IsOpened) throw new Exception("Corrupt snapshot changed session");
                        Log("PASS corrupt snapshot refused without leaving death screen");
                    } finally { File.WriteAllBytes(checkpointPath, checkpoint); }
                }
                if (stageMenu.Button.FindSelectableOnRight() != death.treeShopButton.GetComponent<Button>() || death.treeShopButton.GetComponent<Button>().FindSelectableOnLeft() != stageMenu.Button) throw new Exception("Death navigation mismatch");
                var connection = NetworkServer.localConnection;
                stageMenu.Button.onClick.Invoke();
                stageMenu.Button.onClick.Invoke(); // Duplicate clicks must be ignored.
                end = Time.realtimeSinceStartup + 60;
                while (plugin.Busy && Time.realtimeSinceStartup < end) yield return null;
                if (plugin.Busy || !Player || Player.IsDead || !Player.CanMove || Player.currentFloorGuid != head || NetworkServer.localConnection != connection) throw new Exception("Stage restore failed");
                if (Player.Money != stageMoney || Player.hp != stageHp || Player.MP != stageMp || InventorySignature() != stageInv || SaveManager.Current.GetInt("Sapphire", 0) != sapphire)
                    throw new Exception("Stage state mismatch hp=" + Player.hp + "/" + stageHp + " mp=" + Player.MP + "/" + stageMp + " money=" + Player.Money + "/" + stageMoney + " sapphire=" + SaveManager.Current.GetInt("Sapphire", 0) + "/" + sapphire);
                Log("PASS stage=" + currentStage + " attempt=" + attempt + " firstRoom=" + head + " HP/MP/inventory/money/sapphire restored; same session; alive and movable");
            }
        }
        Log("PASS all stage retry tests");
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


