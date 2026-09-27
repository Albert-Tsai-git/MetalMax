# Validation plan and results

## R7 — real Unity use (listed before test)

| ID | User path | Expected | Actual/evidence | Result |
|---|---|---|---|---|
| T01 normal UI path | In Unity, load the 15 PNGs as Sprites, add `MAP_Wasteland_Base` behind the I-23 map panel, create a TMP font asset from Noto Sans SC, then run the normal menu/map UI and navigate the five tabs / map toggle. | All glyphs render Simplified Chinese; icons remain clear; known places/player markers overlay the unmarked basemap at normalized coordinates. | Not attempted: Claude's Unity batchmode PlayMode tests were still running in `D:\code\GAME-claude\UNITY`; no Unity scene/UI wiring exists in this source-only handoff. | BLOCKED-无法真实验证 |
| T02 boundary terrain path | In Unity Terrain import settings, import the RAW as 513×513 / 16-bit / little-endian, set Terrain transform and size per README, enter/approach all I-23 landmarks and the encounter-zone edge, then test beyond the playable map boundary. | Five radius-6m pads stay within 0±1m and traversable; encounter basin stays traversable; raised continuous rim prevents walking out. | Static generator sampling passed; no TerrainCollider or live player traversal tested. | BLOCKED-无法真实验证 |
| T03 invalid asset boundary | With a temporary copy only, try a wrong RAW byte length or 8-bit import setting; restore the valid source after the check. | Importer/tooling reports a dimensions/bit-depth mismatch instead of silently treating it as the approved terrain. | Not run in Unity. Offline validator asserts exact source length (526338 bytes), PGM header and 16-bit payload. | BLOCKED-无法真实验证 |

## Offline acceptance (passed)

- `python ArtSource/UI_Terrain_Package/validate_assets.py`:
  - RAW exact size 513×513×2 bytes; PGM P5 payload length and declared max value match.
  - Each I-23 point (-15,-14), (22,24), (-9,-14), (0,-10), (17,19) samples within 6m radius at height <=1m (flat base 0.12m).
  - Encounter rectangle x=[-15,15], z=[2,22] peaks at 2.17m; maximum sampled slope is 20.3 degrees.
  - Outer terrain edges at +/-44m exceed 5m.
  - 15 icons are RGBA 128×128 with transparent corners; map base 1024×1024; TTF header and OFL 1.1 text are present.
- Preview assets were opened and visually inspected: `Export/UI_Icons_Preview.png`, `Export/MAP_Wasteland_Base.png`, and `Export/Field_Wasteland_HeightPreview.png`.
- R6 independent review passed; no Unity import/scene claim was made.

## Archive limitation

`sire_paths.archive_database_path()` resolved to `None` because neither `SIRE_ARCHIVE_ROOT` nor `SIRE_ARCHIVE_DB` is configured. Do not guess a machine archive location. This run and reusable asset instructions remain under the user-authorized `ArtSource/` path; shared SQLite/knowledge writes are blocked by the unset archive target and the user's write allowlist.
