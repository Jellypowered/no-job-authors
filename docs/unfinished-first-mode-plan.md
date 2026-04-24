# Unfinished-First Crafting Mode Plan

## Request Summary
Add an optional mode that forces pawns to finish existing matching unfinished things before starting new crafts, while preserving current behavior by default.

## Constraints
- Existing behavior must remain unchanged unless the new setting is enabled.
- Change must be toggle-driven (default OFF).
- This document is plan-only (no runtime code changes in this step).

## Investigation Findings
Current behavior is implemented entirely in `Source/Main.cs` with Harmony patches:
- Patch on `WorkGiver_DoBill.ClosestUnfinishedThingForBill` returns closest reachable matching unfinished thing, ignoring creator ownership.
- Patches on `UnfinishedThing.get_Creator` and `UnfinishedThing.set_Creator` remove/neutralize author linkage.
- Transpilers on `WorkGiver_DoBill.StartOrResumeBillJob` and `WorkGiver_DoBill.FinishUftJob` remove author/worker checks so any pawn can continue unfinished work.
- Patch on `Bill_ProductionWithUft.get_BoundWorker` also neutralizes worker binding interactions.

Interpretation:
- The mod already broadens who can continue unfinished work.
- A strict "unfinished-first" mode likely needs an additional gate that prevents beginning a fresh bill when a valid unfinished thing for that bill exists.

## Inspiration From Other Local Mods (F:/Source)
Verified settings patterns from nearby RimWorld projects:
- `PawnTargetFix/Source/PawnTargetFix.cs`: static settings instance in Mod class, `GetSettings<T>()`, `SettingsCategory()`, `DoSettingsWindowContents(Rect)` with `Listing_Standard`.
- `CloseSettlements/Source/Settings.cs`: compact toggle-only settings via `ModSettings` + `Scribe_Values.Look`.
- `Craftsmanship/Source/Mod.cs`: clean delegation from Mod class to settings window method.

Takeaways for this project:
- Keep a single boolean setting with explicit default false.
- Keep UI minimal: one checkbox + tooltip/description.
- Keep save key stable and simple for backward compatibility.

## Minimal Implementation Skeleton
Expected structure (for implementation phase):

1) `Source/NoJobAuthorsSettings.cs`
- Class `NoJobAuthorsSettings : ModSettings`
- Field: `public bool forceFinishUnfinishedFirst = false;`
- `ExposeData()` using `Scribe_Values.Look(ref forceFinishUnfinishedFirst, "forceFinishUnfinishedFirst", false);`

2) Update `Source/Main.cs` Mod class
- Add static settings field: `public static NoJobAuthorsSettings Settings;`
- In constructor: `Settings = GetSettings<NoJobAuthorsSettings>();`
- Add `SettingsCategory()` and `DoSettingsWindowContents(Rect inRect)`.
- UI: `Listing_Standard.CheckboxLabeled(...)`.

3) Add conditional gating patch
- Prefix on `WorkGiver_DoBill.StartOrResumeBillJob`.
- If setting OFF: return true (run original unchanged).
- If setting ON:
  - Attempt to find closest valid unfinished thing for bill.
  - If found, set `__result` to finishing job and return false.
  - Else return true.

Note:
- Keep existing transpilers and creator patches untouched for initial implementation.
- If prefix conflicts with transpiler behavior, prefer a narrow refactor in a second pass.

## Proposed Technical Design

### 1) Add Mod Settings (default OFF)
Create a settings model and UI:
- `NoJobAuthorsSettings : ModSettings`
  - `bool forceFinishUnfinishedFirst = false;`
  - Implement `ExposeData()` for save/load.
- Extend `Mod_NoJobAuthors`:
  - Keep static settings reference (`GetSettings<NoJobAuthorsSettings>()`).
  - Override `SettingsCategory()` and `DoSettingsWindowContents(Rect inRect)`.
  - Add checkbox label: "Force finish existing unfinished items before starting new".

Behavior guarantee:
- If OFF, current patch behavior is unchanged.

### 2) Enforce Strict Priority Only When Toggle Is ON
Primary approach (least invasive to existing patches):
- Add a Prefix patch on `WorkGiver_DoBill.StartOrResumeBillJob`.
- In Prefix, when setting is ON and bill has unfinished thing support:
  1. Call `WorkGiver_DoBill.ClosestUnfinishedThingForBill(pawn, bill)` (or equivalent helper path).
  2. If a valid unfinished thing is found, return a `FinishUftJob(...)` result and skip original method.
  3. If none found, allow original method.

Why this point:
- It is the decision boundary between resuming vs starting new production.
- It avoids rewriting existing transpilers.

### 3) Optional Strictness Variant (decide during implementation)
Clarify desired strictness:
- Variant A (recommended): only block new craft if pawn can actually reserve/reach a matching unfinished thing now.
- Variant B (hard strict): block new craft if matching unfinished exists anywhere, even if currently unreachable/reserved.

Recommendation:
- Start with Variant A to avoid pawn idling/deadlocks.

### 4) Defensive Compatibility
- Keep current transpilers/patches unchanged unless the new prefix creates conflicts.
- Add null guards around bill recipe and `unfinishedThingDef` checks.
- Avoid assumptions about other mods replacing `WorkGiver_DoBill` logic.

## Implementation Steps (when approved)
1. Add settings class and serialization.
2. Add settings UI in mod class.
3. Add conditional Prefix for strict unfinished-first path.
4. Keep all existing behavior paths untouched when toggle is OFF.
5. Build using existing task and resolve compile issues.
6. Run in-game verification scenarios.

## Validation Plan

### Functional Cases
1. Toggle OFF:
- Behavior matches current release (regression check).

2. Toggle ON, matching unfinished exists and reachable:
- Pawn chooses unfinished item and finishes it before starting fresh craft.

3. Toggle ON, no matching unfinished exists:
- Pawn starts fresh craft normally.

4. Toggle ON, matching unfinished exists but unreachable/reserved:
- Variant A: pawn may start new craft.
- Variant B: pawn does not start new craft (expected potential idle/wait behavior).

5. Cross-pawn continuity:
- Pawn A starts unfinished item, Pawn B can continue and complete.

### Compatibility Smoke Checks
- Prison Labor (already referenced by Harmony ordering).
- Mods with custom bills/ingredients that still rely on `Bill_ProductionWithUft` semantics.

## Risks
- Method signature drift across RimWorld versions may affect Prefix patch reliability.
- Overly strict blocking could reduce throughput if unfinished items are not practically finishable.
- Mod interaction risks if other mods also patch `StartOrResumeBillJob`.

## Deliverables for Implementation Phase
- New settings toggle in mod settings UI.
- Conditional strict unfinished-first behavior behind toggle.
- No default behavior changes.

## Debug Logging Addition (SurvivalTools-Inspired)

### Objective
Add robust instrumentation that is active only in debug builds so release behavior and log noise remain unchanged.

### Implemented Pattern
- Added `Source/NJA_Logging.cs` as a centralized logging helper.
- Uses `[Conditional("DEBUG")]` on debug methods so calls are compiled out in non-debug builds.
- Includes:
  - `Debug`, `Warn`, `Error`
  - `DebugOnce(key, message)` for one-time startup/transpiler messages
  - `DebugThrottled(key, message, cooldownTicks)` to prevent spam in hot paths
- All logging calls are exception-safe (never break gameplay flow).

### Instrumented Areas
- Mod startup patch application (`PatchAll`).
- `ClosestUnfinishedThingForBill`: search start + found/none result traces.
- `UnfinishedThing.get_Creator` and `.set_Creator`: ownership normalization traces.
- Transpilers:
  - `StartOrResumeBillJob` rewrite count
  - `FinishUftJob` rewrite count
- `Bill_ProductionWithUft.get_BoundWorker` prefix activity.

### Build Verification
- Direct debug build succeeded after instrumentation:
  - `dotnet build .vscode -c Debug -o 1.6/Assemblies /nodeReuse:false /p:UseSharedCompilation=false`
