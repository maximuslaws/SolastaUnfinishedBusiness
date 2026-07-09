Solasta Character Pool Editor - Handoff

Baseline: character-pool-editor-prototype branch

Milestone: Targeted read-only extraction (frozen)

Status
- Implemented: targeted read-only extraction of character snapshots from .chr files.
- Runtime-verified using `Aldrich.chr` (local characters folder + resolved pool key via `ICharacterPoolService`).
- The feature is strictly read-only: no saving, no registration, no mutation.

What the summary now includes (copied from "Load Snapshot Read-Only")
- Targeted snapshot compact values
- Targeted class / progression details
- Targeted ability attribute details
- Targeted proficiencies / trained lists
- Targeted active features / powers / spells
- Visual preservation candidates

Runtime-confirmed key values (Aldrich.chr)
- Snapshot.Classes: Ranger
- Snapshot.Levels: 1
- Snapshot.Subclasses: empty/none
- ClassesAndLevels: Ranger -> level 1
- ClassesAndSubclasses: none / not selected / unavailable
- `Hero.Attributes` is the confirmed source for ability scores
- Ability keys include: Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma
- Aldrich current abilities: STR 14, DEX 16, CON 16, INT 12, WIS 14, CHA 12

Proficiencies (runtime)
- SkillProficiencies: Athletics, Insight, Intimidation, Perception, Survival
- ToolTypeProficiencies: ArtisanToolSmithToolsType
- WeaponTypeProficiencies: BattleaxeType, HandaxeType, WarhammerType, HeavyCrossbowType
- WeaponCategoryProficiencies: SimpleWeaponCategory, MartialWeaponCategory
- ArmorCategoryProficiencies: LightArmorCategory, MediumArmorCategory, ShieldCategory
- LanguageProficiencies: Language_Orc, Language_Common, Language_Dwarvish
- FeatProficiencies: (empty)
- InvocationProficiencies: (empty)

Active features / powers / spells (runtime)
- ActiveFeatures keys: 02Race, 03ClassRanger1, 04Background
  - 02Race count approx=12
  - 03ClassRanger1 count approx=7
  - 04Background count approx=3
- UsablePowers: currently resolves compactly as `FeatureDefinitionPower` (count approx=1)
- SpellRepertoires: empty

Visual preservation candidates (runtime)
- Snapshot.PortraitTextureMode: PNG
- Snapshot.RulesetCharacterPhotoData length: 174220 (presence only)
- Hero.BodyHeight: 55
- Hero.BodyAssetPrefix: Dwarf_Male
- Hero.BodyDecorationAssetSuffix: (empty)
- Hero.FaceShapeAssetPrefix: Dwarf_Male
- Hero.HairShapeAssetPrefix: Dwarf_Male
- Hero.BeardShapeAssetPrefix: Dwarf_Male
- Hero.VoiceID: MAL2
- Backing lowercase fields (bodyAssetPrefix, faceShapeAssetPrefix, hairShapeAssetPrefix, beardShapeAssetPrefix, voiceID) readable and match above

Implementation notes / constraints
- Resolution of the correct pool key uses `ICharacterPoolService.Pool` and `BuildCharacterFilename(baseName, false)` when necessary.
- `LoadCharacter` is invoked only with the resolved pool key; the loaded `hero` and `snapshot` are not modified or registered.
- Reflection is limited to property/field getters; methods are not invoked except allowed internal helpers.
- Output is capped to avoid huge UI dumps (currently 350 lines / 40,000 chars) and will append "[truncated for safety]" when necessary.

Safety / policy
- The milestone is read-only. No editing, saving, or registration of characters is implemented or permitted.
- The implementation does not call any of: `SaveCharacter`, `DeleteCharacter`, `CreateLocalData`, `CreateCharacterFromTemplate`, `CreateDefaultCharactersAsNeeded`.
- No `.chr` files are modified by this tool.

Next recommended steps (outside scope for this handoff)
- Add per-section expand/collapse UI for readability.
- Provide optional JSON export of the capped summary for programmatic analysis (read-only export only).
- Add a small confirmation prompt before invoking `LoadCharacter` in UI (for safety).

Handoff complete. The current baseline (branch: `character-pool-editor-prototype`) contains the verified read-only extraction implementation.
