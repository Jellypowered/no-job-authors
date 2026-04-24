# Community Feedback Action Plan

Source: Steam Workshop comments, summarized April 2026.
Edit out items you don't want to pursue before implementation begins.

## Ground Rules

- Stock behavior is the baseline. With all settings unchecked, the mod must behave exactly like current No Job Authors behavior.
- Every new feature is opt-in and beta. No feature may silently change baseline behavior.
- Settings must be independent. Any option must work correctly whether other options are on or off.
- Compatibility work should prefer narrow patches and explicit detection over broad behavior changes.
- Every beta feature and compat path needs verbose debug logging in `DEBUG` builds so failures can be traced from job selection through job completion.
- Use the local `ModCompat/` copies as the first investigation source for workshop compatibility work:
	- `ModCompat/Achtung`
	- `ModCompat/FinishIt`
	- `ModCompat/LifeLessons`
	- `ModCompat/VPE`

## Implementation Strategy

- Phase 1: investigate and instrument. Add targeted debug logs around the exact bill/job/unfinished-item paths touched by each issue before changing logic.
- Phase 2: add compat or feature logic behind dedicated settings toggles.
- Phase 3: validate each toggle in isolation, then validate pairwise combinations so options do not depend on each other.
- Phase 4: leave each beta feature default-OFF until verified.

---

## Bugs / Compatibility Issues

### B1 — "Finish It!" mod incompatibility
- **Problem**: The "Finish It!" widget does nothing when clicked. The mod sets author to "Everyone" and the game can't resolve who to assign the finishing job to.
- **Action**: Investigate `ModCompat/FinishIt` to find how the widget resolves and assigns finishing jobs. If possible, add a dedicated beta compat toggle that preserves Finish It! behavior without changing stock behavior when the toggle is OFF. If not viable, document a hard incompatibility.
- **Debug**: Log widget-triggered flow, selected pawn, creator resolution, and final job assignment in `DEBUG` builds.
- **Priority**: High — breaks another mod's core function.

### B2 — "Life Lessons" mod error
- **Problem**: User reported an error when both mods are loaded. No further details or error log captured.
- **Action**: Inspect `ModCompat/LifeLessons`, reproduce in debug, and trace any overlap with `UnfinishedThing`, `WorkGiver_DoBill`, or bill subclasses we patch. Any compat workaround should be isolated behind its own beta toggle if it changes behavior.
- **Debug**: Log patch entry/exit, exception context, and detected Life Lessons integration points.
- **Priority**: Medium.

### B3 — Writing/Books (Odyssey DLC) — perpetual new-book starts
- **Problem**: Bills set to "forever" cause pawns to perpetually start new books instead of finishing existing unfinished ones. Unfinished books become detached from their bill.
- **Action**: Investigate Odyssey writing path and confirm whether books use `Bill_ProductionWithUft`, a subclass, or a detached bookkeeping path. If a fix requires new logic, put it behind a beta toggle that does not alter stock No Job Authors behavior when disabled.
- **Debug**: Log bill type, recipe, unfinished def, bound worker state, and bill linkage for writing/book jobs.
- **Priority**: High — Odyssey is a major DLC with a wide user base.

### B4 — Art job issues (recurring)
- **Problem**: Multiple users report problems specifically with art jobs. No consistent error logs. Suspected: art bills may use a different code path or the author matters more for art quality attribution.
- **Action**: Trace art job execution in debug and determine whether art needs a compat path or is fully covered by the upcoming quality-item exclusion toggle. Keep any art-specific change behind its own toggle unless it is a pure bug fix that preserves current intended behavior.
- **Debug**: Log art bill selection, unfinished reuse, author value, and final quality-related path.
- **Priority**: Medium — recurring theme warrants a dedicated debug session.

### B5 — "Everyone" tag not localizing
- **Problem**: The author tag always displays as the English word "Everyone" regardless of the player's game language.
- **Action**: Replace the hardcoded string `"Everyone"` in `UnfinishedThing_SetCreator_Patch` with a translation key. Add a `Languages/English/Keyed/NoJobAuthors.xml` file with a `NJA_Everyone` key and instruct translators from there.
- **Debug**: Log the resolved localized string once at startup in debug builds.
- **Priority**: Low-Medium — cosmetic but noticed by non-English players.

### B6 — Achtung! mod: pawns stealing unfinished components
- **Problem**: With Achtung! active, pawns appear to steal unfinished items from each other and stop working.
- **Action**: Investigate `ModCompat/Achtung` to see how it issues forced jobs and reservations. If handling differs from vanilla, add a dedicated compat toggle rather than weakening baseline logic globally.
- **Debug**: Log reservation checks, forced-job origin, selected unfinished thing, and any reassignment/interrupt path.
- **Priority**: Medium.

### B7 — Mod appearing non-functional for some users
- **Problem**: At least one user reported the mod didn't appear to work at all, suspected a mod conflict with no resolution.
- **Action**: Expand the existing debug logger into a structured startup diagnostic that reports patch application, active beta toggles, detected compat mods, and whether core patch entry points are firing.
- **Priority**: Low — our existing debug logging partially covers this; may just need better user-facing instructions.

---

## Feature Requests

### F1 — Opt-out for quality-dependent items (most-requested)
- **Problem**: Many users want the mod to apply only to items where author doesn't affect quality (components, bionics, etc.) and leave quality items (apparel, weapons, art) with standard authorship.
- **Action**: Add an independent beta toggle: "Only apply to non-quality items". When enabled, gate all relevant behavior for quality-producing recipes without requiring any other toggle to be enabled. The default unchecked state must preserve stock No Job Authors behavior.
- **Debug**: Log per-recipe quality classification decisions and whether a recipe is bypassed by this toggle.
- **Priority**: High — most-requested feature by a wide margin.

### F3 — Prevent unfinished items from entering stockpiles / bulk-remove
- **Problem**: User requested a checkbox to block unfinished items from being stored in stockpiles, plus a button to remove existing unfinished items from stockpiles.
- **Action**: Treat this as a fully separate beta feature. Investigate whether stockpile rejection and bulk-remove can be implemented without changing unfinished-item crafting logic at all. If implemented, it must not depend on the unfinished-first toggle or quality toggle.
- **Debug**: Log stockpile rejection decisions and bulk-remove results in debug builds.
- **Priority**: Low — quality-of-life, not related to core functionality.

---

## Unanswered / Compatibility Questions

### U2 — "Craft Timeskip" psycast (Vanilla Psycasts Expanded) compatibility
- **Problem**: The psycast fast-forwards crafting progress. Users are unsure if it interacts correctly with our author-strip patches.
- **Action**: Inspect `ModCompat/VPE` and verify how Craft Timeskip advances crafting. Prefer documenting compatibility if it already works. If a fix is needed, isolate it behind a dedicated compat toggle so it does not alter stock behavior by default.
- **Debug**: Log accelerated crafting completion path, unfinished-item state before/after, and bill linkage after timeskip.
- **Priority**: Low-Medium — VPE is extremely widely used.

---

## Validation Matrix

- Baseline: all toggles OFF must match current stock No Job Authors behavior.
- Single-toggle validation: test each beta setting by itself.
- Pairwise validation: test every pair of enabled beta settings together.
- Compat validation: test each relevant toggle with its target mod loaded from the matching `ModCompat/` folder.
- Debug validation: every new code path should produce enough logging in `DEBUG` builds to explain why a pawn started, resumed, skipped, or failed a job.
