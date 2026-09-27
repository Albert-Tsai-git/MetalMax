# SIRE role ledger — battle character animations

Objective: make player combat beats visible through the already approved `BattleEvents` presentation contract; create Attack / Hit / Defeat clips for CHR_Hunter and CHR_Mechanic; add firing/recoil and impact feedback for vehicle combat. Scope stays in `ArtSource/` and Codex-owned `UNITY/Assets/Models/`, `UNITY/Assets/Animations/`, and `UNITY/Assets/Scripts/Presentation/`.

| Role | State | Evidence / artifacts | Blockers |
|---|---|---|---|
| R0 | 执行完毕 | Scope includes previous combat animation gap and Claude's I-22/I-23 request for UI/map/terrain source assets. UI/map/terrain package separately delivered and committed; combat assets/presentation continue. | Unity verification waits for Claude to release his active batchmode process. |
| R1 | 执行完毕 | Shared KB/vector search receipts in `kb-search.log`, `vector-search.log`; reuse F-044 Y-up/+Z/low-poly project visual contract. | No combat-animation record found. |
| R2 | 执行完毕 | Observable output: both player FBXs contain Attack/Hit/Defeat; Animator routes three triggers; I-09 events trigger animations/effects; attack cues must be sequenced because BattleSystem emits a full turn synchronously. | Enemy species have no `Resources/Visuals/ENM_*` assets yet; presentation uses simple fallback silhouettes. |
| R3 | 执行完毕 | U1 Blender actions + FBX; U2 Animator controller builder; U3 BattleEvents tableau/presentation; no runtime logic changes. | R7 requires Unity Editor after release. |
| R4 | 执行完毕 | Units: U1 combat clips/FBX; U2 Animator routes; U3 I-09 presentation and event ordering. UI/terrain assets were a separate package, independently reviewed and delivered earlier. No Claude logic writes. | Unity verification waits for release. |
| R5 | 执行完毕 | Six clips exported; I-09 tableau/presentation with serialized cues; tank fire/recoil, hit, miss, defeat, forced dismount; BoardChanged replaces tank/actor representation; failed escape dodges; SkillUsed plays character attack. | Runtime C# has not been compiled by Unity or tested in scene. |
| R6 | 执行完毕 | `character_review` independently reviewed the full presentation and subsequent repairs: terminal tank-disabled event replaces the view so later Hit/Defeat uses the new Animator; disabled/re-enabled active battles reconstruct; a missed Ended is caught by BattleState; enabling outside Battle clears stale references. Final verdict PASS, no remaining findings. | Real lifecycle behavior awaits Unity. |
| R7 | BLOCKED-无法真实验证 | Prelisted functional checks in `validation.md`; Blender FBX roundtrip passed twice on both models. | Claude is running `RebuildAll` in `D:\code\GAME-claude\UNITY`; editor and AssetImportWorker processes are active. No Unity operation was run. |
| R8 | 执行中 | I-09/I-10 compliance checked; no changes to BattleSystem/BattleEvents or shared interface. UI/map/terrain source package committed separately in `d887cbe` and review closeout `e102e73`. | Need Unity prefab rebuild and Play validation, then review I-22/I-23 Assets import integration. |
| R9 | 执行中 | Only Codex-owned paths written. | Final review / integration pending; machine SIRE archive target unset. |
| R10 | 跳过 | No native editor operation. | Unity held by Claude's running test process. |

## Read/write contract

- Read: `docs/INTERFACE.md` I-09/I-10, Battle events/types, player visual IDs/prefabs, character source rig/export pipeline.
- Write: `ArtSource/`, `UNITY/Assets/Models/`, `UNITY/Assets/Animations/`, `UNITY/Assets/Scripts/Presentation/` only.
- Do not modify Battle logic, UI routing, Battle scene YAML or Claude-owned source. Do not run Unity/batchmode until its owner releases it.
