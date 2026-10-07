# F05 M1 benchmark calibration — PASS CANDIDATE

This directory contains the M1 slice of F05. It defines benchmark inputs, templates, comparison rules, hidden/core handling, and a stress subset. It is a **definition candidate**, not evidence that the M1 product path currently works or that any benchmark result has passed.

## Frozen scope candidate

- Six nominal scenarios: S19, S20, S07, S23, S22, S18.
- Six templates: TP01, TP02, TP03, TS01, TS02, TS08.
- Each template has normal, boundary, and invalid input profiles. Valid normal and boundary profiles expect automatic deliverables; invalid profiles expect a safe, reasoned refusal before output writes.
- Each scenario covers Z01–Z08 and the M1 path for edit, undo, data update, and interruption recovery. Candidate/preview/repair budgets follow FINAL: at most 12 structural candidates, 3 initial previews, 5 total previews, 2 automatic repair rounds, and 2 final renders per single-page job.
- TS08 uses the G-205 ruling: qualification predicate plus a gold naming convention. Its real gold input is `NOT-AVAILABLE-PENDING-USER`; `multi.tif` is not used as a qualifying input or gold example.

## Acceptance rules

`comparison-rules-v1.json` is the machine-readable source for rule IDs and thresholds. The concrete calibration choices are identified as such where FINAL states that a value must be frozen but does not provide the value. Invalid inputs pass only when rejected before output writes with a usable reason. Attempts, failures, timeouts, cancellations, and refusals remain in the denominator applicable to their class.

The candidate machine thresholds are deliberately fail-closed: categorical facts and stored numeric answers compare exactly; geometry uses the input dataset's declared XY tolerance, while raster grids must match exactly; semantic color tokens compare exactly before ICC conversion; overlay registration is zero-pixel in the shared map-frame transform; essential text has an 8 pt floor. Two independent reviewers use the FINAL five-point suggestion (mean at least 4, no item below 3). This calibration has not been independently accepted.

The overall FINAL targets remain 108/108 standard maps, at least 98 first-render successes, at least 40 stress cases with at least 95% applicable completion, and at least 12 hidden variants with at least 11/12 first-version automatic completion, no more than two repair rounds, and zero serious scientific errors. This M1 subset does not claim those full-scope targets have been run or met.

## Files and cross-references

| Logical deliverable | Machine-readable file | Human-readable file |
|---|---|---|
| F05 M1 overview | `README.md` | `README.md` |
| Six scenario definitions | `m1-scenarios-6.jsonl` | `m1-scenarios-6.md` |
| Six template definitions | `m1-templates-6.jsonl` | `m1-templates-6.md` |
| Comparison rules v1 | `comparison-rules-v1.json` | `comparison-rules-v1.md` |
| Core/hidden split v1 | `core-hidden-split-v1.json` | `core-hidden-split-v1.json` |
| M1 stress subset | `stress-40-m1-subset.jsonl` | `stress-40-m1-subset.jsonl` |

The FINAL benchmark defines at least 40 stress cases and names dimensions, but does not assign stable IDs to forty individual rows. The M1 subset therefore uses local `ST-M1-*` identifiers linked to named FINAL dimensions; it does not claim that the full stress set is materialized here.

The 108 standard-image scope is 36 templates × 3 frozen inputs. This batch defines three profile classes (normal, boundary, invalid) for each of six templates. These 18 profile definitions are not claimed to map one-to-one to FINAL's 18 positive standard-image inputs: invalid profiles expect safe refusal, and the three frozen positive inputs and their gold outputs still require case-level identification. No rendered image or benchmark execution is claimed. The remaining 30 templates, other 54 scenarios, remaining core tasks, and rest of the pressure-set count are deferred to later calibration batches. The full 24-task core set and cross-domain hidden set are not claimed complete here.

Machine definitions explicitly cross-reference scenario, template, comparison rule, core/hidden IDs, and local M1 stress IDs. Hidden content and gold paths remain absent.

## Evidence and freeze

`../.runtime/evolution/v5-f/run-20260929-d085/nominal-mapping.md` records the complete nominal-to-M1 mapping and deferred remainder. `tools/benchmark/freeze-benchmark-s0.py` hashes the six logical deliverables and their member files. The resulting `benchmark-s0.json` and `.txt` are reproducibility records only. After Commander acceptance, these definitions become contract surface; changes require Commander ruling.

## Sources

- `Docs/GIS_AUTOMATION_FINAL_IMPLEMENTATION_ROADMAP_20260927.md` §§6, 8–10.
- `Docs/GIS_AUTOMATION_FINAL_CAPABILITY_CATALOG_20260927.md` §§6–7, 10.
- `Docs/GIS_AUTOMATION_FINAL_BENCHMARK_AND_DECISIONS_20260927.md` §§3–8.
- `Docs/_gatekeeper/GATE-G205-D085-RESUME_RULING.md` R-G205-1 through R-G205-4.
- D-085 ticket and HANDOFF, which define the M1 subset and required fields.
