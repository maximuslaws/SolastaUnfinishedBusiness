Prompt: Multi-character Read-Only Comparison

Goal
- Build a read-only tool to compare multiple character snapshots (.chr) side-by-side to identify structural and data differences useful for mapping builder flows.

Requirements
- Accept multiple selected .chr files from the local characters folder.
- Load each via resolved pool key (read-only) using the same safe `LoadCharacterSnapshotReadOnly` helper.
- Produce a compact tabular comparison showing:
  - Identity fields: Name, SurName, Race, SubRace, Level, BuiltIn
  - Key attributes: Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma (Base/Current)
  - Classes and levels
  - Proficiencies counts
  - Visual candidate presence
- Keep output capped and safe (no binary dumps).

Safety
- Read-only only. Do not call any save/mutate APIs.
- Do not modify .chr files.
- Use only property/field getters.

Next steps
- Implement UI to pick multiple files (outside scope for prompt).
- Provide a copy-to-clipboard button for the comparison table.
