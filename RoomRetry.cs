using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaRoomRetry
{

    public sealed class RoomRetryPlugin : MonoBehaviour
    {
        internal static RoomRetryPlugin Instance;
        private BepInEx.Logging.ManualLogSource Logger { get { return SephiriaDicePreview.PersonalConveniencePlugin.Instance.Log; } }
        public bool Busy { get; internal set; }

        private void Awake()
        {
            Instance = this;

            Logger.LogInfo("Room retry ready: in-session checkpoint restore.");
        }

        internal static PlayerAvatar Player
        {
            get { return PlayerSpawner.MultiplayerList.Where(p => p && p.isLocalPlayer).Select(p => p.PlayerAvatar).FirstOrDefault(); }
        }

        public bool CanRetry(out string reason)
        {
            reason = "";
            var player = Player;
            if (Busy) reason = "방을 다시 불러오는 중입니다.";
            else if (!NetworkServer.active || PlayerSpawner.MultiplayerList.Count != 1)
                reason = "이번 방 재시도는 혼자 플레이할 때 사용할 수 있습니다.";
            else if (!player || player.isInDungeon <= 0 || !DungeonManager.Instance || !DungeonManager.Instance.isRunStarted || player.IsDead)
                reason = "던전 진행 중에 사용할 수 있습니다.";
            else if (player.loadingScreenType != -1 || player.MovingFloorViaWorldmap || DungeonManager.Instance.IsHostTraveling || SteamInvitation.waitForExternalConnect)
                reason = "방 이동이 끝난 뒤 다시 시도해 주세요.";
            else if (SaveManager.IsSaving != SaveManager.ESaveState.None)
                reason = "저장이 끝난 뒤 다시 시도해 주세요.";
            return reason.Length == 0;
        }

        public void Retry(UI_PausePanel panel)
        {
            string reason;
            if (!CanRetry(out reason)) { Message(reason); return; }
            try
            {
                string slot = OptionsBinding.Instance.Options.GetString("SelectedProfile", SaveManager.defaultSlotName);
                // Read-only validation. Never turn a missing/corrupt checkpoint into a new run.
                if (!CheckpointMatches(slot, Player.currentFloorGuid, DungeonManager.Instance.DestinySeed))
                {
                    Message("현재 방의 이어하기 저장이 없어 재시도할 수 없습니다.");
                    return;
                }
                Busy = true;
                string floor = Player.currentFloorGuid;
                Logger.LogInfo("Retry requested: " + floor);
                panel.Close();
                StartCoroutine(GuardResume(slot, floor, DungeonManager.Instance.DestinySeed));
            }
            catch (Exception error) { Busy = false; Logger.LogError(error); Message("저장을 확인하지 못했습니다. 현재 방을 유지합니다."); }
        }

        internal static bool CheckpointMatches(string slot, string floor, int seed)
        {
            var saved = new SaveData(true, ".sav", 1);
            var profile = new SaveData(true);
            string runPath = Path.Combine(SaveData.CommonPath, slot + "TMP.sav");
            string profilePath = Path.Combine(SaveData.CommonPath, slot + ".sav");
            return File.Exists(runPath) && File.Exists(profilePath) &&
                saved.LoadFromString(File.ReadAllText(runPath)) && profile.LoadFromString(File.ReadAllText(profilePath)) && saved.GetBool("RunStarted", false) &&
                saved.GetInt("FloorCount", 0) > 0 && saved.ContainsKey("CurrentGame") && saved.GetInt("SavedPlayerCount", 0) > 0 && saved.GetString("LastFloorGuid", "") == floor && saved.GetInt("Seed", -1) == seed;
        }

        internal IEnumerator GuardResume(string slot, string floor, int seed)
        {
            var routine = Resume(slot, floor, seed);
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception error)
                {
                    Busy = false; Logger.LogError(error);
                    Message("자동 재시도를 완료하지 못했습니다. 게임 시작으로 이어해 주세요.");
                    yield break;
                }
                if (!next) break;
                yield return routine.Current;
            }
            Busy = false;
        }

        private IEnumerator Resume(string slot, string floor, int seed)
        {
            var manager = NetworkManager.singleton as HorayNetworkManager;
            var dungeon = DungeonManager.Instance;
            var connection = NetworkServer.localConnection;
            if (!manager || !dungeon || connection == null || !connection.identity)
                throw new InvalidOperationException("Local session unavailable.");
            if (!CheckpointMatches(slot, floor, seed) || !SaveManager.Load(slot) || !SaveManager.LoadTMP(slot))
                throw new InvalidOperationException("Checkpoint could not be read.");
            if (!SaveManager.CurrentRun.GetBool("RunStarted", false) || SaveManager.CurrentRun.GetInt("FloorCount", 0) <= 0 ||
                !SaveManager.CurrentRun.ContainsKey("CurrentGame") || SaveManager.CurrentRun.GetInt("SavedPlayerCount", 0) <= 0 ||
                SaveManager.CurrentRun.GetString("LastFloorGuid", "") != floor || SaveManager.CurrentRun.GetInt("Seed", -1) != seed)
                throw new InvalidOperationException("Incomplete resume data; refusing to start a new game.");
            var restoredProfile = SaveManager.Current.Copy();
            var restoredRun = SaveManager.CurrentRun.Copy();
            float began = Time.realtimeSinceStartup;
            GameTimeManager.Instance.ResetTimeScaleTo1();
            // Recreate gameplay objects using the ordinary native spawn/load path.
            // Keep the scene, host, local connection, Steam lobby, and loaded assets alive.
            dungeon.enabled = false;
            try
            {
                dungeon.RemovePlayerFloorOccupancy(connection.identity.netId);
                NetworkServer.DestroyPlayerForConnection(connection);
                manager.ClearNetworkObjects();
                yield return null; // Allow OnDestroy to detach old UI and floor registrations.
                dungeon.globalItemStatTable.Clear();
                AccessTools.Method(typeof(HorayNetworkManager), "ClearRunScopedRejoinState").Invoke(manager, null);
                HorayNetworkManager.serverPlayerIndex = 0;
                // Old-player teardown must not alter the checkpoint used to spawn the replacement.
                AccessTools.Field(typeof(SaveManager), "current").SetValue(null, restoredProfile);
                AccessTools.Field(typeof(SaveManager), "currentRun").SetValue(null, restoredRun);
                manager.NewGame();
                manager.OnServerAddPlayer(connection);
            }
            finally { if (dungeon) dungeon.enabled = true; }
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var player = Player;
                if (player && player.currentFloorGuid == floor && player.loadingScreenType == -1 && player.CanMove && !dungeon.IsHostTraveling)
                {
                    Busy = false; Logger.LogInfo("Retry complete: " + floor + " in " + (Time.realtimeSinceStartup - began).ToString("F2") + "s (same session)"); yield break;
                }
                yield return null;
            }
            Busy = false; Logger.LogWarning("Automatic resume timed out.");
            Message("이어하기 상태를 확인해 주세요.");
        }

        private static void Message(string value)
        {
            if (UIManager.Instance) UIManager.Instance.GetElement<UI_SystemMessage>().Open(value, 4f);
        }
    }

    [HarmonyPatch(typeof(UI_PausePanel), "OnOpened")]
    internal static class PauseMenuPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(UI_PausePanel __instance)
        {
            var menu = __instance.GetComponent<RetryMenu>();
            if (!menu) menu = __instance.gameObject.AddComponent<RetryMenu>();
            menu.Refresh(__instance);
        }
    }

    public sealed class RetryMenu : MonoBehaviour
    {
        private Button button;
        private VerticalLayoutGroup layout;
        private float originalSpacing;
        private void LateUpdate()
        {
            if (!button) return;
            var player = RoomRetryPlugin.Player;
            bool visible = player && player.isInDungeon > 0 && SephiriaDicePreview.PersonalConveniencePlugin.Instance.ShowRoomRetry.Value;
            if (button.gameObject.activeSelf != visible) Refresh(GetComponent<UI_PausePanel>());
        }
        public void Refresh(UI_PausePanel panel)
        {
            if (!button && panel.giveupButton)
            {
                var source = panel.giveupButton.GetComponent<Button>();
                if (!source) return;
                button = Instantiate(source, source.transform.parent);
                button.name = "RetryCurrentRoom";
                button.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(delegate { RoomRetryPlugin.Instance.Retry(panel); });
                foreach (var loc in button.GetComponentsInChildren<UI_LocalizationStringText>(true)) loc.enabled = false;
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.text = "이번 방 재시도";
                var nav = button.navigation; nav.mode = Navigation.Mode.Automatic; button.navigation = nav;
                layout = button.transform.parent.GetComponent<VerticalLayoutGroup>();
                if (layout) originalSpacing = layout.spacing;
            }
            if (button)
            {
                var player = RoomRetryPlugin.Player;
                button.gameObject.SetActive(player && player.isInDungeon > 0 && SephiriaDicePreview.PersonalConveniencePlugin.Instance.ShowRoomRetry.Value);
                // Keep unavailable actions selectable so the reason is visible on click.
                button.interactable = !RoomRetryPlugin.Instance.Busy;
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)button.transform.parent);
                if (layout)
                {
                    int count = button.transform.parent.Cast<Transform>().Count(t => t.gameObject.activeSelf && t.GetComponent<Button>());
                    float height = ((RectTransform)button.transform).rect.height;
                    layout.spacing = count <= 7 ? originalSpacing : Mathf.Max(2f, (7f * height + 6f * originalSpacing - count * height) / (count - 1));
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)button.transform.parent);
                }
                var siblings = button.transform.parent.GetComponentsInChildren<UI_HorayButton>()
                    .Where(b => b.transform.parent == button.transform.parent && b.IsInteractable())
                    .OrderBy(b => b.transform.GetSiblingIndex()).ToArray();
                for (int i = 0; i < siblings.Length; i++)
                {
                    siblings[i].SetForceNavUp(i > 0 ? siblings[i - 1] : null);
                    siblings[i].SetForceNavDown(i + 1 < siblings.Length ? siblings[i + 1] : null);
                }
            }
        }
    }
}



