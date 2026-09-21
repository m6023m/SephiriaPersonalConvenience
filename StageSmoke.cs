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
        SwitchManager.SetDestinySwitch("EnableTowntreePortal", false);
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
        SaveManager.Current.SetInt("Sapphire", 20);
        Player.GetComponent<PlayerLocalDataStorage>().Networksapphire = 20;
        SaveManager.Save(true, false);
        while (SaveManager.IsSaving != SaveManager.ESaveState.None) yield return null;
        Player.GetComponent<PlayerSpawner>().NetworksapphireInRun = 200;
        int totalPaid = 0; int retryNumber = 0;
        int[] costs = { 2, 4, 8, 16, 32, 32, 32 };
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
            int sapphire = SaveManager.Current.GetInt("Sapphire", 0);
            string currentStage = DungeonManager.Instance.generatedFloors[head].stageName;
            var roomGuids = DungeonManager.Instance.GetAllFloorInStage(currentStage).Where(f => f.guid != head).Select(f => f.guid).Take(2).ToArray();
            int headMoney = Player.Money;
            for (int attempt = 0; attempt < (Environment.GetEnvironmentVariable("STAGE_UI_ONLY") == "1" ? 1 : (stageIndex == 0 ? 3 : 4)); attempt++) {
                string nextFloor = roomGuids[attempt % roomGuids.Length];
                Player.AddMoney(123 + attempt); // Persist progress that differs from the stage entrance.
                DungeonManager.Instance.MoveTogether(nextFloor, "FLOORSTARTING", 0, false, true);
                yield return new WaitForSecondsRealtime(3);
                end = Time.realtimeSinceStartup + 60;
                while ((Player.loadingScreenType != -1 || SaveManager.IsSaving != SaveManager.ESaveState.None) && Time.realtimeSinceStartup < end) yield return null;
                if (Player.currentFloorGuid == head) throw new Exception("Did not leave stage head");
                string roomInv = InventorySignature(); int roomMoney = Player.Money; float roomHp = Player.hp; int roomMp = Player.MP;
                if (roomMoney == headMoney) throw new Exception("Room fixture does not distinguish stage progress");
                Player.AddMoney(777); Player.Networkmp = 0;
                Player.GetComponent<PlayerSpawner>().NetworksapphireInRun = 999; // Must not fund retry with rewards earned after room entry.
                Player.Inventory.ForceRemoveAll();
                int deathCount = SaveManager.Current.GetInt("DeathCount", 0);
                int settlements = 0; Player.GetComponent<PlayerSpawner>().OnGameOverServerside += delegate { settlements++; };
                FatalHit();
                var death = UIManager.Instance.GetElement<UI_GameOverLabel>();
                var holder = UIManager.Instance.GetElement<UI_MessageBoxHolder>();
                UI_MessageBox_YesNo choice = null;
                end = Time.realtimeSinceStartup + 30;
                while (Time.realtimeSinceStartup < end) {
                    choice = holder.GetComponentsInChildren<UI_MessageBox_YesNo>().FirstOrDefault(bx=>bx.name=="StageRetryBeforeResult");
                    if(choice)break; yield return null;
                }
                if(!choice)throw new Exception("Pre-result retry choice absent");
                yield return new WaitForSecondsRealtime(stageIndex==0 && attempt==0 ? 7 : .5f);
                if(death.IsOpened || settlements!=0 || SaveManager.Current.GetInt("DeathCount",0)!=deathCount ||
                    SaveManager.Current.GetInt("Sapphire",0)!=sapphire || !SaveManager.CurrentRun.enableSave ||
                    !File.Exists(Path.Combine(SaveData.CommonPath,SaveManager.Binded+"TMP.sav")))throw new Exception("Death settled before choice");
                Player.GetComponent<PlayerSpawner>().ClientGameOver();yield return null;
                if(holder.GetComponentsInChildren<UI_MessageBox_YesNo>().Count(bx=>bx.name=="StageRetryBeforeResult")!=1)throw new Exception("Duplicate death prompt");
                Log("PASS pre-result choice; death count and sapphire unchanged; run save preserved; settlement callbacks zero; first-death automatic restart suppressed");
                yield return Capture(holder, "stage-before-result-" + stageIndex + "-" + attempt + ".png");
                if (stageIndex == 0 && attempt == 0) {
                    string checkpointPath = Path.Combine(SaveData.CommonPath, SaveManager.Binded + "TMP.sav");
                    byte[] checkpoint = File.ReadAllBytes(checkpointPath);
                    try {
                        File.WriteAllText(checkpointPath, "invalid");
                        StageRetry.Instance.Retry();
                        if (plugin.Busy || !Player.IsDead || death.IsOpened) throw new Exception("Corrupt room checkpoint changed session");
                        Log("PASS corrupt room checkpoint refused before settlement; no new game");
                    } finally { File.WriteAllBytes(checkpointPath, checkpoint); }
                    try {
                        var insufficient = new SaveData(true, ".sav", 1);
                        insufficient.LoadFromString(File.ReadAllText(checkpointPath));
                        insufficient.SetInt("Player0SapphireInRun", 0);
                        insufficient.SetInt("Player0SapphireUseInRun", 19);
                        insufficient.version = Application.version; insufficient.enableCloudSave = false;
                        insufficient.Save(checkpointPath);
                        byte[] deniedBefore = File.ReadAllBytes(checkpointPath);
                        StageRetry.Instance.Retry();
                        if(plugin.Busy || !Player.IsDead || !File.ReadAllBytes(checkpointPath).SequenceEqual(deniedBefore))
                            throw new Exception("Insufficient checkpoint sapphires were charged or allowed");
                        Log("PASS insufficient room-entry balance rejected despite 999 current-room sapphires; save unchanged");
                    } finally { File.WriteAllBytes(checkpointPath, checkpoint); }
                    var paymentType = typeof(StageRetry).Assembly.GetType("SephiriaRoomRetry.RetryPayment");
                    var payment = paymentType.GetMethod("Read", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(null, new object[] { SaveManager.Binded, 0 });
                    paymentType.GetMethod("Commit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(payment, null);
                    paymentType.GetMethod("Refund", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(payment, null);
                    if(!File.ReadAllBytes(checkpointPath).SequenceEqual(checkpoint)) throw new Exception("Payment refund did not restore exact checkpoint");
                    Log("PASS payment rollback restores exact checkpoint bytes");
                    using (var locked = new FileStream(checkpointPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        StageRetry.Instance.Retry();
                        if(plugin.Busy || !Player.IsDead) throw new Exception("Locked payment save started retry");
                    }
                    if(!File.ReadAllBytes(checkpointPath).SequenceEqual(checkpoint)) throw new Exception("Failed payment modified checkpoint");
                    Log("PASS locked payment save refuses retry without charging");
                }
                var connection = NetworkServer.localConnection;
                choice.yesButton.onClick.Invoke();
                if(!plugin.Busy)throw new Exception("Retry not started");
                StageRetry.Instance.Retry(); // Duplicate request must be ignored.
                end = Time.realtimeSinceStartup + 60;
                while (plugin.Busy && Time.realtimeSinceStartup < end) yield return null;
                if (plugin.Busy || !Player || Player.IsDead || !Player.CanMove || Player.currentFloorGuid != nextFloor || Player.currentFloorGuid == head || NetworkServer.localConnection != connection) throw new Exception("Current room restore failed");
                if (Player.Money != roomMoney || Player.hp != roomHp || Player.MP != roomMp || InventorySignature() != roomInv || SaveManager.Current.GetInt("Sapphire", 0) != sapphire)
                    throw new Exception("Room state mismatch hp=" + Player.hp + "/" + roomHp + " mp=" + Player.MP + "/" + roomMp + " money=" + Player.Money + "/" + roomMoney + " sapphire=" + SaveManager.Current.GetInt("Sapphire", 0) + "/" + sapphire);
                totalPaid += costs[retryNumber++];
                var localData = Player.GetComponent<PlayerLocalDataStorage>();
                if(localData.sapphireUseInRun != totalPaid || localData.GetSapphire() != 220 - totalPaid ||
                    SaveManager.CurrentRun.GetInt("PersonalConvenience_DeathRoomRetries", 0) != Math.Min(5, retryNumber))
                    throw new Exception("Paid retry mismatch: spent=" + localData.sapphireUseInRun + " expected=" + totalPaid + " balance=" + localData.GetSapphire());
                Log("PASS paid retry cost=" + costs[retryNumber-1] + " accumulated=" + totalPaid + " balance=" + localData.GetSapphire());
                if (retryNumber == 1) {
                    var paidPanel = UIManager.Instance.GetElement<UI_PausePanel>(); paidPanel.Open();
                    plugin.Retry(paidPanel);
                    while(plugin.Busy) yield return null;
                    if(Player.GetComponent<PlayerLocalDataStorage>().sapphireUseInRun != totalPaid ||
                        SaveManager.CurrentRun.GetInt("PersonalConvenience_DeathRoomRetries", 0) != 1)
                        throw new Exception("Pause retry reset death payment");
                    Log("PASS pause retry stays free and preserves paid cost/count");
                }
                Log("PASS stage=" + currentStage + " attempt=" + attempt + " currentRoom=" + nextFloor + " stageHead=" + head + " HP/MP/inventory/money/sapphire restored; same session; alive and movable");
            }
        }
        SwitchManager.SetDestinySwitch("EnableTowntreePortal", true);
        int expectedSettlement = SaveManager.Current.GetInt("Sapphire", 0) + UI_GameOverLabel.CalculateRunEarnedSapphire(Player.GetComponent<LevelController>(), Player.GetComponent<PlayerSpawner>(), Player) - totalPaid;
        int resultEvents=0;Player.GetComponent<PlayerSpawner>().OnGameOverServerside+=delegate{resultEvents++;};
        FatalHit();var resultHolder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();UI_MessageBox_YesNo resultChoice=null;
        end=Time.realtimeSinceStartup+30;
        while(Time.realtimeSinceStartup<end){resultChoice=resultHolder.GetComponentsInChildren<UI_MessageBox_YesNo>().FirstOrDefault(bx=>bx.name=="StageRetryBeforeResult");if(resultChoice)break;yield return null;}
        if(!resultChoice)throw new Exception("Result choice missing");
        resultChoice.noButton.onClick.Invoke();yield return new WaitForSecondsRealtime(3);
        if(!UIManager.Instance.GetElement<UI_GameOverLabel>().IsOpened || resultEvents!=1 || StageRetry.Instance.AwaitingDecision || SaveManager.CurrentRun.enableSave)
            throw new Exception("Native result path failed or ran twice");
        if(SaveManager.Current.GetInt("Sapphire", 0) != expectedSettlement) throw new Exception("Native settlement double-charged or refunded payment");
        Log("PASS result choice invokes native settlement exactly once; prior retry costs settled once");
        UIManager.Instance.GetElement<UI_GameOverLabel>().Close();
        GameTimeManager.Instance.ResetTimeScaleTo1();
        ((HorayNetworkManager)NetworkManager.singleton).RestartGame();
        yield return new WaitForSecondsRealtime(5);
        end = Time.realtimeSinceStartup + 60;
        while ((!Player || !Player.CanMove || Player.loadingScreenType != -1 || SaveManager.IsSaving != SaveManager.ESaveState.None) && Time.realtimeSinceStartup < end) yield return null;
        DungeonManager.Instance.LoadStageAndMove(RaceDatabase.FindById(DungeonManager.Instance.raceId).stages[0].name);
        yield return new WaitForSecondsRealtime(4);
        while (SaveManager.IsSaving != SaveManager.ESaveState.None) yield return null;
        if(SaveManager.CurrentRun.GetInt("PersonalConvenience_DeathRoomRetries",0)!=0 || Player.GetComponent<PlayerLocalDataStorage>().sapphireUseInRun!=0)
            throw new Exception("New adventure did not reset retry cost");
        Log("PASS native new adventure resets retry count and cost to 2");
        Log("PASS all pre-result current-room retry tests");
        Application.Quit(0);
    }

    private void FatalHit()
    {
        var damage=DamageInstance.GetDamage(null,"StageRetryFatalHit",Player.transform.position,4294967295L,100000f,EDamageType.Slice,EDamageFromType.DirectAttack,Vector2.right,0,0f);
        damage.ignoreDefense=100;
        var result=Player.ApplyDamage(damage);
        if(!Player.IsDead)throw new Exception("Fatal attack did not kill: "+result+" hp="+Player.hp);
        Log("PASS actual lethal ApplyDamage -> native Die callback");
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


