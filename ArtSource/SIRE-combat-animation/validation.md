# Combat animation validation plan

## R7 real Unity tests — listed before test

| ID | User path | Expected | Actual/evidence | Result |
|---|---|---|---|---|
| C01 normal on-foot attack | Enter Battle with an on-foot player; choose Attack and a target; resolve turn. | Hunter/Mechanic wind up, lunge, return to idle; target recoils on hit; miss triggers a readable dodge. | Not run: Claude's Unity `-batchmode -runTests PlayMode` process active in a separate checkout. | BLOCKED-无法真实验证 |
| C02 vehicle gun attack | Enter Battle while in tank; fire available main/sub weapon; observe muzzle and chassis. | Muzzle flash and chassis recoil play; successful target hit receives visible reaction. | Not run; Unity held by the other worktree. | BLOCKED-无法真实验证 |
| C03 defeat/tank loss boundary | Let a target reach 0 HP; separately break an active tank. | On-foot target falls and stays down; tank hit recoils, tank loss forces visible dismount; battle return removes tableau cleanly, including when `Ended` fires before queued dismount. | Not run in Unity; static pending-dismount queue repair reviewed PASS independently. | BLOCKED-无法真实验证 |
| C04 repeat/cleanup boundary | Trigger multiple attacks in one synchronous turn, board/leave tank, end battle, and return to Field. | Cues play sequentially; BoardChanged swaps actor/tank model; no stuck trigger/duplicate stage/material leak or leftover units in Field. | Static cue queue/BoardChanged refresh + scene cleanup; no runtime confirmation. | BLOCKED-无法真实验证 |

## Offline validation

- Blender 4.5 generated `CHR_Hunter.blend` and `CHR_Mechanic.blend`: each has 20 bones and 25/27 meshes; actions are `Idle`, `Walk`, `Run`, `Attack`, `Hit`, `Defeat`.
- Both character FBXs round-trip-import in Blender: six action clips remain; height 1.78m; 20 bones; 25/27 meshes. Locomotion loops have matching seam values; combat clips contain keyed chest/hip curves.
- Python compile for Blender generation/export/validation scripts and `git diff --check` passed. Unity import/controller build/scene presentation remain unverified. The Battle scene currently has no combatant layout, so `CombatPresentationBootstrap` builds a transient presentation stage from the approved events/IDs without touching Battle logic.
- Independent R6 review passed for the edge case where synchronous `Ended` clears a queued `TankDisabled` cue, subsequent Hit/Defeat routing after dismount, disabling/re-enabling during an active battle, missed Ended while disabled, and stale cached BattleSystem clearing outside Battle. No remaining static findings.
- R7 remains blocked. The other checkout currently runs `Unity.exe -batchmode -projectPath D:/code/GAME-claude/UNITY -executeMethod Game.EditorTools.BatchTasks.RebuildAll` with an AssetImportWorker child. No Unity/editor/batchmode process was launched by Codex.

## Archive limitation

The shared SIRE path resolver returned no machine archive target (`SIRE_ARCHIVE_ROOT` / `SIRE_ARCHIVE_DB` unset). Do not guess a path or write outside the user-authorized paths. Preserve task evidence here and report the archive blocker.
