Character Pool Editor - Safety Rules

This document describes safety rules and constraints for the Character Pool Editor tooling and diagnostics.

Mandatory safety constraints
- Do not write, edit, overwrite, delete, rename, copy, or move any `.chr` files.
- Do not call `SaveCharacter`.
- Do not call `DeleteCharacter`.
- Do not call `CreateLocalData`.
- Do not call `CreateCharacterFromTemplate`.
- Do not call `CreateDefaultCharactersAsNeeded`.
- Do not mutate the returned `hero` or `snapshot` objects.
- Do not register the loaded hero into gameplay state manually.
- Do not change gameplay rules or persistent game data.
- Reflection allowed only for safe property/field getters; do not invoke arbitrary methods.
- Do not add dependencies or editing UI through this diagnostic feature.
- All diagnostics must be capped to avoid exposing large binary blobs or UI issues.

Diagnostics and output limits
- Summary output is capped to protect UI and logs:
  - Maximum lines: 350
  - Maximum characters: 40,000
  - When capped, append "[truncated for safety]".
- Do not include full binary/image data. For images/byte arrays, report presence and length only.
- For collections, prefer showing counts and small samples only (with explicit caps).

Purpose
- The tooling is intended for read-only discovery, diagnostic, and mapping of runtime character structures to help implement safe editors later.

If in doubt
- Prefer not to read or display a suspect member rather than risk mutating game state or exposing large data.
- Ask for guidance before making any change that might alter game data or write files.
