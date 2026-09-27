# UI and Wasteland Source Package

Original art source for the I-22 UI and I-23 map/terrain handoff to Claude. These files were generated outside Unity; no Unity scene was opened or changed while Claude held the Unity lock.

## UI materials

- `Export/Icons/UI_Icon_*.png`: 15 transparent 128×128 icons (Party, Items, Tank, Quests, System, Map, HP, SP, Foot, Interact, Cannon, MachineGun, Ammo, Armor, Repair). Intended for `UNITY/Assets/Resources/Icons/` and `Resources.Load<Sprite>("Icons/<filename without extension>")`.
- `Export/MAP_Wasteland_Base.png`: 1024×1024 top-down, no location dots or labels. The UI draws discovered locations and player position on top using `WorldMapService.Normalize`; mapping is west-to-east / south-to-north across world bounds `[-29,29]`.
- `Export/UI_Icons_Preview.png`: contact sheet for the 15 icons.
- `Export/NotoSansSC-VF.ttf`: Noto Sans SC variable TrueType font for Simplified Chinese and Latin. Downloaded from the [Google Fonts Noto Sans SC source](https://github.com/google/fonts/blob/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf) on 2026-09-27. Google Fonts upstream metadata identifies Noto CJK source version 2.004 at commit `523d033d6cb47f4a80c58a35753646f5c3608a78`; the Google Fonts binary may include their documented 832-byte post-processing. Include `Export/OFL-NotoSansSC.txt` with redistributed font copies. License: SIL Open Font License 1.1.

## Continuous wasteland terrain

- `Export/Field_Wasteland_513.raw`: 513×513, unsigned 16-bit little-endian, row-major south-to-north / west-to-east. Normalize each value by 65535 and assign it to a Unity Terrain with `TerrainData.size = (88, 12, 88)` and origin `(-44, 0, -44)`.
- `Export/Field_Wasteland_513.pgm`: the same 16-bit height values in PGM P5 big-endian form for image tools.
- `Export/Field_Wasteland_HeightPreview.png`: 8-bit grayscale preview only; do not use this as the source heightmap.
- `Export/TEX_Wasteland_SaltGround.png`: separate 1024×1024 seamless salt-crust diffuse texture for the Unity TerrainLayer. It is a repeat-wrapped surface material; the map image above is UI artwork and must not be assigned as terrain diffuse.
- Height generation is deterministic and editable in `generate_assets.py`. It makes connected rolling salt flats / shallow basins and a continuous raised rim beyond the 58×58 m map bounds. There are no scattered tree/rock props. Interaction pads use the I-23 coordinates for the town gate (-15,-14), pump entrance (22,24), Qupo (-9,-14), player spawn (0,-10), and (17,19), kept at base elevation within radius 6 m. The encounter basin is centered at (0,12), spans 30×20 m, and blends into adjacent landforms.
- The Unity integration creates a Terrain GameObject and TerrainCollider from the height values, and uses the dedicated salt-ground texture for the TerrainLayer. Verify the five pads and encounter area with Claude's RebuildAll.

## Rebuild and validation

```powershell
python ArtSource/UI_Terrain_Package/generate_assets.py
python ArtSource/UI_Terrain_Package/validate_assets.py
```

The source height/icon generators require Pillow. Unity integration now exists under the Codex-owned `UNITY/Assets/Art/World`, `Scenes/Art`, and `Scripts/Presentation` paths; Claude's Build Settings synchronization and in-game visual QA remain pending his RebuildAll and native Play test.
