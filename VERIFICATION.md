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

## v1.0.6 paid death-room retry

Baseline: public canonical main c9fd7d9b2a918d9bc48dfdfb42eaef80a7239876, fetched 2026-09-21 into the initially clean outputs/SephiriaPersonalConvenience checkout. Runtime files (4): RetryPayment.cs validates the exact room-entry balance, atomically records native SapphireUseInRun plus a capped retry cost step, and supports rollback; StageRetry.cs displays cost/balance and gates the existing death-room retry; RoomRetry.cs exposes whether native restoration succeeded; PersonalConveniencePlugin.cs sets version 1.0.6. Test support (1): StageSmoke.cs. Documentation (3): README.md, RELEASE_NOTES.md, VERIFICATION.md. No changes to updater or deployment scripts. Full-file overwrites are valid only on the recorded baseline; reconcile if main advances.

The balance is saved profile Sapphire plus the saved local player's SapphireInRun minus saved SapphireUseInRun. Current-room gains are excluded. Payment uses the existing native run-spending field, so result settlement subtracts it once. Retry step and spending are written together to the same run checkpoint. Normal pause retry remains free and loads that updated checkpoint; a new native run clears the step.

Build and runtime commands: build.ps1; run-stage-smoke.ps1 against the muted save-isolated fixture. The final release binary is EAF7A612951CD4DC6F97F6FD51E313508B2C7A4B67DB01960B561CD114D82BF9. Stable unchanged updater remains 7D3AD4C9BDC92C0A4AAB5427E663BF760E9ACE26C29A5B46ED9372237C73A07A. Separate compilation/execution of UpdateCore.cs plus tests/UpdateTests.cs passed 25 updater safety checks with the release binary.

Final runtime suite passed: seven actual lethal-damage retries across Moleland/Grassland charged 2,4,8,16,32,32,32 (126 total), reducing checkpoint balance 220 to 94. First-death automatic movement, duplicate prompts and duplicate retry requests were suppressed; HP/MP/inventory/money and exact current room restored. A later free pause retry preserved the paid amount and cost step. With only 1 checkpoint sapphire but 999 current-room sapphires, retry was refused and save bytes unchanged. A locked save also refused without charging. Direct commit/refund restored original checkpoint bytes. Original pause retry tests passed. Native result settlement applied all prior spending exactly once; native RestartGame created a new adventure with zero retry step/spending (next cost 2). The final Korean cost/balance prompt render was inspected. The suite uses isolated test saves, never user save data. Refund storage was tested directly; arbitrary failure during every native spawn phase, every chapter, multiplayer and cross-device Steam Cloud synchronization were not exercised. No process restart was required to test reload persistence: normal native room loading read the paid checkpoint from disk.


## v1.0.7 damage details and UI

Fresh baseline: canonical https://github.com/m6023m/SephiriaPersonalConvenience, main 4e9100ab706c1db8c5350fa70ea620e6e6a06ee8. Reconciled in a clean release clone. Existing retry/updater behavior has no functional changes.

Runtime source: damage snapshot/profile/buff calculation files, DamageTooltipUI, invalidation hooks, options toggle and plugin version. Deployment: cached build script, isolated-test removal script. Test-only: damage smoke fixtures and incremental validation utilities. Game assemblies, saves, credentials, runtime fixtures and unfinished DPS prototype are excluded.

The release DLL was built in the reconciled clone and rendered in an isolated muted Sephiria runtime (1280x800). Keyboard and gamepad native glyphs were non-null; current and full-buff dialogs and the tooltip were visually inspected. Buttons stay inside the modal; page icons flank the page count; boss/miniboss rows are absent with ELITEDAMAGE +50 present. Full-buff and current output use the ordinary-target rows. The executable test ended PASS COMPLETE. Screenshots and full logs are stored in the local UI release evidence folder, not packaged into the mod.

Calculation logic is the previously audited local implementation: changed-case checks cover inactive conditional artifacts, zero-MP basic/dash versus MP-paid attacks, integer floor display, and six attacking artifacts in four loadouts. These were calculation-output tests, not a claim of every weapon's actual hit damage verification. Earlier catalog checks distinguished wiki-listed items from dummy/deleted entries. No combat simulation is distributed; the removal script is for older separately installed test modules.

DPS is explicitly deferred. Multiplayer and every live conditional combat outcome are not verified. Numeric worker execution/caching and previous-result display were exercised in earlier isolated tests; this release's focused runtime rerun verifies the changed UI.

## v1.0.8 DPS and additional damage

Fresh baseline: canonical https://github.com/m6023m/SephiriaPersonalConvenience, main efe0cdf6ce6f1d83a1e4bcb1a5451bfa47da2f9a, fetched 2026-09-23 into a clean worktree. Existing retry behavior and update protocol have no functional changes.

The final candidate was built from this worktree and run in the muted, save-isolated CombatTestArena runtime. The full damage tooltip smoke completed with PASS COMPLETE. It verified worker-thread calculation and caching, no repeated idle recalculation, retained prior results during recalculation, native damage projection, weapon-linked Typhoon damage, fixed and elemental per-hit additions, and probability-weighted expected DPS for Pointed Bat while retaining the detail range. Snapshot collection covered 164 cases with a 23.5958 ms maximum and 0.3451 ms mean in this run.

Updater unit tests passed all 25 integrity, staging, lock, backup, equal-version, downgrade and corrupt-manifest checks. The test fixture now derives the downgrade manifest version from the supplied old DLL so it validates a real previous release.

Release plugin SHA256: 70F2C1D8F7F263E03BD8441A92A1AD766392B6CDEEE272B4FD63301F5E5A7C60. Updater SHA256: 474E8E05396C9B1374456D9F5AB57BA8A0321814CF4C4D13F77E5118410A1421. The artifact-specific sustained-DPS gaps found during the requested investigation remain documented in ARTIFACT_DPS_AUDIT.md and are not claimed as implemented.
