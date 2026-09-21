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
