using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaRoomRetry
{
    public sealed class StageRetry : MonoBehaviour
    {
        public static StageRetry Instance;
        private string capturedKey;
        private static string Slot { get { return SaveManager.Binded; } }
        private static string SnapshotPath { get { return Path.Combine(SaveData.CommonPath, Slot + ".stage-retry"); } }
        private void Awake() { Instance = this; SaveManager.OnSaveEnd += Capture; }
        private void OnDestroy() { SaveManager.OnSaveEnd -= Capture; }
        private static bool Context(out FloorData floor)
        {
            floor = null;
            var p = RoomRetryPlugin.Player;
            return NetworkServer.active && PlayerSpawner.MultiplayerList.Count == 1 && p && p.isInDungeon > 0 &&
                DungeonManager.Instance && !String.IsNullOrEmpty(Slot) &&
                DungeonManager.Instance.generatedFloors.TryGetValue(p.currentFloorGuid, out floor);
        }
        private static string Key(FloorData floor)
        {
            return Slot + "|" + DungeonManager.Instance.DestinySeed + "|" + floor.stageName + "|" +
                SaveManager.CurrentRun.GetString("StageHead_" + floor.stageName, "");
        }
        private void Capture()
        {
            try
            {
                FloorData floor;
                if (!Context(out floor) || RoomRetryPlugin.Player.IsDead || !SaveManager.CurrentRun.enableSave ||
                    !SaveManager.CurrentRun.GetBool("RunStarted", false)) return;
                string head = SaveManager.CurrentRun.GetString("StageHead_" + floor.stageName, "");
                if (head != RoomRetryPlugin.Player.currentFloorGuid || capturedKey == Key(floor)) return;
                try { if (File.Exists(SnapshotPath))
                    using (var reader = new BinaryReader(File.OpenRead(SnapshotPath)))
                        if (reader.ReadString() == "StageRetry1" && reader.ReadString() == Key(floor)) { capturedKey = Key(floor); return; }
                } catch (IOException) { /* Replace an unreadable old snapshot at the next stage entry. */ }
                string run = File.ReadAllText(Path.Combine(SaveData.CommonPath, Slot + "TMP.sav"));
                string profile = File.ReadAllText(Path.Combine(SaveData.CommonPath, Slot + ".sav"));
                var check = new SaveData(true, ".sav", 1);
                if (!check.LoadFromString(run) || check.GetString("LastFloorGuid", "") != head ||
                    check.GetInt("Seed", -1) != DungeonManager.Instance.DestinySeed) return;
                string temporary = SnapshotPath + ".tmp";
                using (var writer = new BinaryWriter(File.Create(temporary)))
                { writer.Write("StageRetry1"); writer.Write(Key(floor)); writer.Write(profile); writer.Write(run); }
                if (File.Exists(SnapshotPath)) File.Replace(temporary, SnapshotPath, null);
                else File.Move(temporary, SnapshotPath);
                capturedKey = Key(floor);
                Debug.Log("Stage retry checkpoint: " + capturedKey);
            }
            catch (Exception error) { Debug.LogWarning("Stage retry checkpoint unavailable: " + error.Message); }
        }
        public void Retry(UI_GameOverLabel panel)
        {
            if (RoomRetryPlugin.Instance.Busy) return;
            FloorData floor;
            if (!Context(out floor) || !RoomRetryPlugin.Player.IsDead || panel.openType != 0)
            { Message("스테이지 재시도는 혼자 플레이하는 던전의 사망 화면에서 사용할 수 있습니다."); return; }
            if (SaveManager.IsSaving != SaveManager.ESaveState.None || !panel.button.interactable)
            { Message("사망 정산 저장이 끝난 뒤 다시 시도해 주세요."); return; }
            try
            {
                string profileText, runText;
                using (var reader = new BinaryReader(File.OpenRead(SnapshotPath)))
                {
                    if (reader.ReadString() != "StageRetry1" || reader.ReadString() != Key(floor)) throw new InvalidDataException("Checkpoint mismatch");
                    profileText = reader.ReadString(); runText = reader.ReadString();
                }
                var profile = new SaveData(true);
                var run = new SaveData(true, ".sav", 1);
                if (!profile.LoadFromString(profileText) || !run.LoadFromString(runText) || !run.GetBool("RunStarted", false) ||
                    run.GetString("LastFloorGuid", "") != SaveManager.CurrentRun.GetString("StageHead_" + floor.stageName, ""))
                    throw new InvalidDataException("Invalid checkpoint");
                // Bind without CreateNew(), which would write an empty save before validation.
                Bind(profile, Slot); Bind(run, Slot + "TMP");
                RoomRetryPlugin.Instance.Busy = true;
                StartCoroutine(GuardRestore(panel, profile, run, Slot));
            }
            catch (Exception error)
            { Debug.LogWarning("Stage retry refused: " + error.Message); Message("이 스테이지의 시작 기록이 없습니다. 모드 적용 후 스테이지에 새로 진입해야 합니다."); }
        }
        private static void Bind(SaveData data, string name)
        {
            AccessTools.Property(typeof(SaveData), "BindedFileName").SetValue(data, name, null);
            AccessTools.Field(typeof(SaveData), "bindedPath").SetValue(data, Path.Combine(SaveData.CommonPath, name + ".sav"));
            data.version = Application.version;
        }
        private IEnumerator Restore(UI_GameOverLabel panel, SaveData profile, SaveData run, string slot)
        {
            // Restore the profile too: death settlement must not be awarded on every retry.
            AccessTools.Field(typeof(SaveManager), "current").SetValue(null, profile);
            AccessTools.Field(typeof(SaveManager), "currentRun").SetValue(null, run);
            SaveManager.Save(true, true);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (SaveManager.IsSaving != SaveManager.ESaveState.None && Time.realtimeSinceStartup < deadline) yield return null;
            if (SaveManager.IsSaving != SaveManager.ESaveState.None) throw new IOException("Checkpoint save timed out");
            var saved = new SaveData(true, ".sav", 1);
            if (!saved.LoadFromString(File.ReadAllText(Path.Combine(SaveData.CommonPath, slot + "TMP.sav"))) ||
                saved.GetString("LastFloorGuid", "") != run.GetString("LastFloorGuid", "")) throw new IOException("Checkpoint save failed");
            panel.Close();
            yield return RoomRetryPlugin.Instance.GuardResume(slot, run.GetString("LastFloorGuid", ""), run.GetInt("Seed", -1));
        }
        private IEnumerator GuardRestore(UI_GameOverLabel panel, SaveData profile, SaveData run, string slot)
        {
            var previousProfile = SaveManager.Current;
            var previousRun = SaveManager.CurrentRun;
            var routine = Restore(panel, profile, run, slot);
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception error)
                {
                    AccessTools.Field(typeof(SaveManager), "current").SetValue(null, previousProfile);
                    AccessTools.Field(typeof(SaveManager), "currentRun").SetValue(null, previousRun);
                    RoomRetryPlugin.Instance.Busy = false;
                    Debug.LogError("Stage retry failed: " + error);
                    Message("스테이지 복원을 완료하지 못했습니다. 저장 상태를 확인해 주세요.");
                    yield break;
                }
                if (!next) break;
                yield return routine.Current;
            }
            RoomRetryPlugin.Instance.Busy = false;
        }
        private static void Message(string text) { UIManager.Instance.GetElement<UI_SystemMessage>().Open(text, 4f); }
    }

    [HarmonyPatch(typeof(UI_GameOverLabel), "OnOpened")]
    internal static class StageRetryDeathPatch
    {
        private static void Postfix(UI_GameOverLabel __instance)
        {
            var menu = __instance.GetComponent<StageRetryMenu>() ?? __instance.gameObject.AddComponent<StageRetryMenu>();
            menu.Ensure(__instance);
        }
    }
    public sealed class StageRetryMenu : MonoBehaviour
    {
        private UI_GameOverLabel panel;
        public UI_HorayButton Button;
        public void Ensure(UI_GameOverLabel value)
        {
            panel = value;
            if (Button) return;
            Button = Instantiate(panel.button, panel.button.transform.parent);
            Button.name = "RetryCurrentStage";
            Button.onClick = new Button.ButtonClickedEvent();
            Button.onClick.AddListener(delegate { StageRetry.Instance.Retry(panel); });
            foreach (var loc in Button.GetComponentsInChildren<UI_LocalizationStringText>(true)) loc.enabled = false;
            foreach (var text in Button.GetComponentsInChildren<TMP_Text>(true)) text.text = "현재 스테이지 재시도";
            Button.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (!Button || !panel) return;
            bool visible = panel.IsOpened && panel.openType == 0 && panel.button.gameObject.activeSelf &&
                NetworkServer.active && PlayerSpawner.MultiplayerList.Count == 1;
            Button.gameObject.SetActive(visible);
            if (!visible) return;
            var rect = (RectTransform)Button.transform;
            var original = (RectTransform)panel.button.transform;
            rect.anchoredPosition = original.anchoredPosition - new Vector2(2 * (original.rect.width + 8), 0);
            Button.interactable = panel.button.interactable && !RoomRetryPlugin.Instance.Busy;
            var shop = panel.treeShopButton ? panel.treeShopButton.GetComponent<UI_HorayButton>() : null;
            var next = shop && shop.gameObject.activeInHierarchy ? shop : panel.button;
            Button.SetForceNavUp(null); Button.SetForceNavDown(null); Button.SetForceNavLeft(null);
            Button.SetForceNavRight(next); next.SetForceNavLeft(Button);
        }
    }
}
