# v1.0.3 verification

New public repository; initial main baseline 874c845. No earlier remote main existed. The six original runtime source files were copied from the tested local feature without reconciliation changes. UpdateCore, UpdateUI and the preloader were added for v1.0.3. Source and build references exclude proprietary game assemblies from Git.

Runtime: six convenience/gameplay files plus UpdateCore.cs, UpdateUI.cs and UpdaterPatcher.cs. Deployment: build.ps1 and update.ps1. Smoke programs and tests are development support, not game plugins to install. Local screenshots, save fixtures and raw logs are excluded from publication.

Validated on Sephiria 1.0.33 / BepInEx 5.4.23.5:
- Two stages, two deaths per stage: return from another room to the stage entrance; HP, MP, equipment and money restored in the same local session.
- Death settlement of 17 sapphires resets to the entrance value; missing equipment is restored. Corrupt snapshots and repeated clicks are handled.
- Existing room retry twice; original menu settings retained.
- 25 updater checks: invalid versions, path traversal, protocol, file size, hash, assembly identity, locked file, pending-update preservation, atomic replacement/backup, equal version, downgrade and corrupt manifest.
- Live public GitHub download via installer; plugin and updater hashes verified; fixture installation retained the previous DLL.
- Isolated in-game update: fixture version 1.0.2 checks live release 1.0.3; decline does not download; accept stages verified files and leaves the loaded DLL unchanged.
- Second isolated launch: BepInEx preloader applies 1.0.3 before plugin load and backs up 1.0.2; native options open and manual check reports latest.
- Native options, update offer and latest-version message were rendered and inspected at 1280x800.

Installed/released plugin SHA256: CD30CE00278F330B3DCA87AD713F9FE47348966AE0910D517E7472A65AAA050A
Updater SHA256: 7D3AD4C9BDC92C0A4AAB5427E663BF760E9ACE26C29A5B46ED9372237C73A07A

Game smoke tests require a separately prepared, muted, save-isolated CombatTestArena runtime; it is not distributed. Build first, then run run-update-smoke.ps1 -Prepared <fixture>, wait for its PASS/exit, and run again with -VerifyApplied. The first phase creates an old-version fixture from this source. Disable other test plugins when switching suites.

Scope limits: not all chapters or multiplayer sessions were exercised. Stage retry is single-player only; pre-installation stage entrance state cannot be recovered. Test shutdown may log native authority/offline-session cleanup errors. Repeated offscreen captures may omit individual canvas layers; the complete native renders were used for layout verification. The preloader protocol itself is not auto-updated; future protocol changes need both installer files again.


## v1.0.4 pre-result retry

Baseline: dce11ce80411e1ab3414634aa3dfcdf986f3eba2, fetched before this change. The original game log reported a restore to the recorded stage-head GUID; the exact user-observed fresh-game symptom was not conclusively reproduced. The result-first design was changed as requested instead of treating that log as proof of a correct user experience.

Now intercepts single-player native ClientGameOver before OnGameOverServerside and result UI. The next frame shows a native retry/result choice and pauses. Retry validates saved stage/run identity and player resume data, bypasses settlement and restores the checkpoint. Result executes native game-over once. Old-player teardown cannot mutate the restored save objects used for spawning. No fallback intentionally starts a fresh game on invalid resume data.

Final binary D4990D348C11C4C00666927D856AB9883022DE423979FACA4C7E3E6DC40305B1 tested with actual lethal ApplyDamage (not just ForceDie): two stages x two retries, plus one result choice. Result UI stayed closed, settlement callback count stayed zero, death count/sapphires unchanged, run save present; first-death story was held for more than its normal delay. Same connection, expected stage-head GUID, HP/MP/inventory/money/sapphires and movement restored. Corrupt snapshot refused without settlement or fresh-game transition. Result choice invoked settlement exactly once. Two existing pause-room retries also passed. Native prompt render checked; retry label shortened to avoid wrapping. Other chapters/multiplayer remain untested; multiplayer interception is excluded.

Updater runtime code/protocol is unchanged. Stable 1.0.3 updater binary is reused. Test harness version checks now use the plugin version rather than hard-coding the previous release.

## v1.0.5 current-room correction

Canonical public remote: https://github.com/m6023m/SephiriaPersonalConvenience, main 0236d9c32fa0081614a64b121ca724beae060990, fetched 2026-09-21. Reconciled in the initially clean outputs/SephiriaPersonalConvenience checkout. Runtime source changes: StageRetry.cs uses the native current-room checkpoint and removes stage snapshot capture/restore; RoomRetry.cs shares and strengthens checkpoint validation; PersonalConveniencePlugin.cs sets 1.0.5. Test support: StageSmoke.cs distinguishes current-room progression from stage-head state. Documentation: README, RELEASE_NOTES and this file. No updater implementation or deployment script changes. Full-file overwrite is valid only for this recorded baseline; reconcile if main advances.

Commands from the verified checkout: build.ps1; run-stage-smoke.ps1 (muted isolated fixture); csc UpdateCore.cs tests/UpdateTests.cs and UpdateTests.exe with the release binary. Compilation succeeded; all 25 updater checks passed. Runtime completed with PASS all pre-result current-room retry tests. Two original pause retries passed. Four actual lethal-damage retries across two different rooms in each of Moleland and Grassland restored the current-room GUID, explicitly different from the stage-head GUID, HP, MP, inventory, money and sapphires in the same session. Each next-room entry included new money to distinguish retained earlier-room progression from stage-entry rollback. Corrupt current-room save was rejected before settlement. Duplicate death/retry calls were suppressed. First-death automatic restart remained blocked while choosing. Result selection executed native settlement once. Native Korean prompt was rendered and inspected at 1280x800. All chapters and multiplayer remain untested; interception is single-player only.

Verified plugin SHA256: D01CF77A62B228E3034927A38722BB3FCB6C18F9D3ED334224F942FD37FEEC99. Stable unchanged updater SHA256: 7D3AD4C9BDC92C0A4AAB5427E663BF760E9ACE26C29A5B46ED9372237C73A07A. Raw game logs and rendered evidence retained locally, excluded from public repository along with saves and game binaries.
