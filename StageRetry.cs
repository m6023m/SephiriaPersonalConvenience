using System;
using System.Collections;
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
        private PlayerSpawner pendingDeath;
        private bool continueOriginal;
        public bool AwaitingDecision { get { return pendingDeath; } }

        internal bool InterceptDeath(PlayerSpawner spawner)
        {
            FloorData floor;
            if (continueOriginal || !Context(out floor) || !spawner.isLocalPlayer ||
                spawner.PlayerAvatar != RoomRetryPlugin.Player || !spawner.PlayerAvatar.IsDead ||
                DungeonManager.Instance.victoryType != 0) return true;
            if (pendingDeath || RoomRetryPlugin.Instance.Busy) return false;
            pendingDeath = spawner;
            StartCoroutine(OfferRetry());
            return false;
        }
        private IEnumerator OfferRetry()
        {
            yield return null; // Let the fatal damage and native death animation finish this frame.
            if (!pendingDeath) yield break;
            while (SaveManager.IsSaving != SaveManager.ESaveState.None) yield return null;
            pendingDeath.CloseSomeUI();
            GameTimeManager.Instance.Pause();
            bool decided = false;
            var box = UIManager.Instance.GetElement<UI_MessageBoxHolder>().OpenYesNo(
                "사망했습니다.\n\n결과 정산 전에 이번 방을 다시 시작할 수 있습니다.\n지금 사망한 방에 진입했던 상태로 돌아갑니다.",
                delegate { decided = true; Retry(); if (!RoomRetryPlugin.Instance.Busy) StartCoroutine(OfferRetry()); },
                delegate { decided = true; StartCoroutine(ShowResult()); }) as UI_MessageBox_YesNo;
            if (box)
            {
                Label(box.yesButton, "재시도"); Label(box.noButton, "결과 보기");
                box.name = "StageRetryBeforeResult";
                box.onCloseMessageBox += delegate { if (!decided && pendingDeath && !RoomRetryPlugin.Instance.Busy) StartCoroutine(OfferRetry()); };
            }
            Debug.Log("Current room retry decision before death settlement");
        }
        private IEnumerator ShowResult()
        {
            yield return null;
            var spawner = pendingDeath; pendingDeath = null;
            GameTimeManager.Instance.ResetTimeScaleTo1();
            if (!spawner) yield break;
            continueOriginal = true;
            try { spawner.ClientGameOver(); }
            finally { continueOriginal = false; }
        }
        private static void Label(Button button, string text)
        {
            foreach (var loc in button.GetComponentsInChildren<UI_LocalizationStringText>(true)) loc.enabled = false;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.text = text;
        }
        private static string Slot { get { return SaveManager.Binded; } }
        private void Awake() { Instance = this; }
        private static bool Context(out FloorData floor)
        {
            floor = null;
            var p = RoomRetryPlugin.Player;
            return NetworkServer.active && PlayerSpawner.MultiplayerList.Count == 1 && p && p.isInDungeon > 0 &&
                DungeonManager.Instance && !String.IsNullOrEmpty(Slot) &&
                DungeonManager.Instance.generatedFloors.TryGetValue(p.currentFloorGuid, out floor);
        }
        public void Retry()
        {
            if (RoomRetryPlugin.Instance.Busy) return;
            FloorData floor;
            if (!Context(out floor) || !RoomRetryPlugin.Player.IsDead || !pendingDeath)
            { Message("이번 방 재시도는 혼자 플레이하는 던전의 사망 선택창에서 사용할 수 있습니다."); return; }
            if (SaveManager.IsSaving != SaveManager.ESaveState.None)
            { Message("저장이 끝난 뒤 다시 시도해 주세요."); return; }
            try
            {
                string slot = Slot;
                string room = RoomRetryPlugin.Player.currentFloorGuid;
                int seed = DungeonManager.Instance.DestinySeed;
                // Use the same native room-entry save as the pause-menu retry.
                // Do not save the dead player or overwrite it with a stage-entry snapshot.
                if (!RoomRetryPlugin.CheckpointMatches(slot, room, seed))
                { Message("현재 방의 이어하기 저장이 없어 재시도할 수 없습니다."); return; }
                RoomRetryPlugin.Instance.Busy = true;
                StartCoroutine(RestoreRoom(slot, room, seed));
            }
            catch (Exception error)
            {
                RoomRetryPlugin.Instance.Busy = false;
                Debug.LogWarning("Death room retry refused: " + error.Message);
                Message("현재 방의 저장을 확인하지 못했습니다. 결과 선택을 유지합니다.");
            }
        }
        private IEnumerator RestoreRoom(string slot, string room, int seed)
        {
            yield return RoomRetryPlugin.Instance.GuardResume(slot, room, seed);
            if (RoomRetryPlugin.Player && !RoomRetryPlugin.Player.IsDead) pendingDeath = null;
            else if (pendingDeath) StartCoroutine(OfferRetry());
        }
        private static void Message(string text) { UIManager.Instance.GetElement<UI_SystemMessage>().Open(text, 4f); }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "ClientGameOver")]
    internal static class StageRetryDeathPatch
    {
        private static bool Prefix(PlayerSpawner __instance)
        {
            return !StageRetry.Instance || StageRetry.Instance.InterceptDeath(__instance);
        }
    }
}
