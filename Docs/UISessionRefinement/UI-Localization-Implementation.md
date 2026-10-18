# UI language implementation — review candidate

Base: `feature/phase2-guided-journey@07ba53a976ff60237ed3a28726f065f829781e54`.
This change is a presentation refinement, not a new tutorial or final hardware acceptance.

## Cause and scope

The existing menu and review pages authored Chinese strings, while the Guided Journey authored English and mixed-language instructions. Secondary pages, door prompts and measurement labels used separate rendering paths. There was no language preference service. `Hardcoded-UI-Inventory.csv` records 524 source/serialized candidate literals from the frozen base; this inventory also contains internal IDs and stored preset values, which must not be translated as data.

`Assets/Scripts/Localization/ApartmentLanguage.cs` and its Catalog/Journey partial files provide stable text keys and English/Chinese copy. The existing `ApartmentVRPanel` translates authored display labels at its rendering boundary; dynamic labels keep their authored source so language changes refresh existing text. Tutorial labels, task cards, door prompts, feedback pages and bag sizes use the same service. There is no second menu, scene language manager, package or input mapping.

B > Settings > Language offers **English** and **简体中文**. Every new Play/Player run starts in English through `SubsystemRegistration`; language is kept only within the current runtime. The legacy PlayerPrefs key is neither read nor changed. Switching refreshes open panels immediately; closing the menu or temporarily losing focus preserves the current language. The reset retains live listeners when Domain/Scene Reload is disabled and does not reset Journey or feedback data. Actual repeated Play and Player startup remain manual checks.

## Data boundary

JSON field names, IDs, enums, object references, stage transitions and existing Chinese preset values remain unchanged. Canonical preset data is translated only for display. Unrecognised participant text is shown with `LabelRaw` and retained; the user must explicitly choose **Use preset feedback** before replacing it. This avoids the former editor fallback silently replacing legacy free text with the first preset. Raw user text is never sent through the authored-copy translator.

The implementation uses existing TMP/Noto Sans SC assets and licence notices. Read-only OpenType cmap inspection found all **547 distinct printable characters** in the new localization source, plus printable ASCII, in the existing font source. No font file/atlas, SDK, Scene or Prefab is committed by this change. Source glyph availability does not prove comfortable size, layout or Player rendering.

## Actual verification and limits

Historical Unity 6000.6.4f1 DX11 EditMode: **160/160 Passed**, including the earlier 9 localization tests, 6 panel tests and 10 session/recovery tests. These are the original `53434fb` results. The subsequent pre-integration UI run passed **172/172**, including 13 localization and 13 session/readout tests; the current merged-source rerun is recorded separately in `Verification/PR12-Integration-Results.json` when executed. The rendered Settings page test verifies default English and live Chinese labels; other pages require the bilingual hardware checklist. New-session data tests use private system-temp files only.

The Editor still logs a pre-existing TMP `m_AtlasTextures` exception on shutdown. The seven known font/performance/settings side effects were backed up and restored byte-for-byte. No C# compilation errors occurred. Current-branch Quest 3, PlayMode and Windows Player validation are **Not Run**. Both-language overflow, runtime atlas expansion and actual readability remain manual acceptance items.
