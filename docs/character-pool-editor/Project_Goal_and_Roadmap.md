Project Goal and Roadmap

Final goal
- Build a standalone Solasta 1 Character Vault Editor (UMM mod) that allows existing saved/vault characters to be reopened, edited through the game's character builder/creator UI, and saved back safely — similar to Solasta 2.

Desired UX
- Users can open characters from saved files or vault, load them into the game's native builder/creator flow, make edits using the game's UI, validate via game systems, and save back using game-native save APIs.

Current scaffold
- The present implementation is a read-only diagnostic and mapping scaffold integrated into Unfinished Business (UB) under an options tab.
- It is strictly read-only research: the UI is for inspection, discovery, and mapping only; it is not the final editor UI.

Preferred future design
- Use the game's native loading, validation, builder, and save systems where possible.
- Avoid raw `.chr` file editing where possible; favor game-native save/load operations and validation.
- Aim for minimal invasive hooks: load into builder state, not into live gameplay state, and let the game handle final persistence.

Current phase
- Read-only extraction and runtime mapping of `RulesetCharacterHero` and `Snapshot` members.
- Implemented and runtime-verified targeted extraction (Aldrich.chr).

Next safe phases (ordered)
1. Multi-character read-only comparison tooling (detect diffs, structural differences).
2. Read-only discovery of character builder / creator entry points at runtime.
3. Identify whether existing heroes can be loaded into builder state without registration into active gameplay.
4. After approval, prototype a controlled save path that uses game-native APIs and validation (explicit approval required before any save prototype).
5. Split into an independent UMM (UnityModManager) mod to avoid UB integration coupling.
6. Integrate with character vault UI and provide an in-game, game-native builder editing workflow.

Constraints and safety
- Continue to obey the strict safety rules in `Safety_Rules.md`.
- Do not implement saving or editing until the design and approval for a safe save flow are established.

Handoff notes
- The `docs/character-pool-editor/` folder contains current findings and handoff artifacts for downstream implementation teams.
