# D-101 synthetic evaluation corpus — PASS CANDIDATE

Authorised by G-222 R-1 and R-2 (user: generate the TS08 fixture; synthetic known-answer sets are
acceptable for M4's eight domains). Everything in `inputs/` is constructed, not observed. No frozen
byte was edited: M1 keeps nine members under `92ECD265…` and M4 keeps fourteen under `F71BF6CD…`.

- artefacts **78** in **9** groups, 449,069 bytes
- deterministic seed **20260930**, LCG documented in the manifest, integer arithmetic where possible
- the TS08 predicate itself is unchanged; the fixture satisfies it clause by clause in `inputs/ts08/ts08_qualification.json`

## Groups

| group | artefacts | kinds | G-DOMAIN four artefacts |
|---|---:|---|---|
| `ts08` | 6 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x1` | 7 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x2` | 9 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x3` | 7 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x4` | 10 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x5` | 10 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x6` | 11 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x7` | 13 | answer, automatic-expectation, input, negative, updated-instance | yes |
| `x8` | 5 | answer, automatic-expectation, input, negative, updated-instance | yes |

## What this corpus is for, and what it is not

It gives a later execution batch a known answer to be judged against, plus a negative example,
a complete-automatic-result expectation and an updated instance per domain (the G-DOMAIN four).
It does not close the real-data gap G-205 registered, does not make any tool exist, and does not
prove that any host format, licence or algorithm works.

## Unfilled axes, declared rather than faked

- COPC
- LAZ
- NetCDF
- Network Analyst solver admission (no entry in the frozen 53-name whitelist)
- Zarr
- a GPU-free inference path actually executed
- a multidimensional raster carrier
- a published geoid or vertical-grid model with a named version
- a real multi-band product with sensor metadata
- a real network dataset with impedance, direction and time attributes
- a real point cloud with a published classification coding
- a real sensor product with acquisition metadata and published QA semantics
- a real time-enabled series with a declared calendar and provenance
- a verified DOCX channel for the report path
- a vertical datum attached to a real elevation product
- an accepted real gold example for TS08
- an approved Open XML generator with a recorded licence for the deck path
- an approved model register entry with a recomputable weight hash
- an expert statement that an error surface is supportable for a chosen method
- editability proved by reopening a real generated file
- independent analyst acceptance of the synthetic gold
- licence and algorithm admission for any real 3D analysis path
- real field observations with a published sampling protocol
- real multi-band product with sensor metadata
- the wider modern-format list beyond RFC 7946 (GeoPackage, COG, GeoParquet, FlatGeobuf, STAC), which has no adapter in the accepted source

## Per-artefact register (hash and lineage are authoritative in the JSON)

| id | kind | group | bytes | sha256 (first 16) | path |
|---|---|---|---:|---|---|
| `D101-TS08-01` | input | ts08 | 24,896 | `02D200960FB8B9C6…` | `Benchmarks/inputs/ts08/ts08_gold_input_b4.tif` |
| `D101-TS08-02` | answer | ts08 | 12,505 | `B60716115341BC58…` | `Benchmarks/inputs/ts08/ts08_reference_classmap.asc` |
| `D101-TS08-03` | negative | ts08 | 18,748 | `21E943789A305C98…` | `Benchmarks/inputs/ts08/ts08_negative_repeated_lineage_b3.tif` |
| `D101-TS08-04` | updated-instance | ts08 | 24,896 | `9FFDCA0483FEB26F…` | `Benchmarks/inputs/ts08/ts08_gold_input_updated_b4.tif` |
| `D101-TS08-05` | answer | ts08 | 4,729 | `04C3E026B2C46B88…` | `Benchmarks/inputs/ts08/ts08_qualification.json` |
| `D101-TS08-06` | automatic-expectation | ts08 | 930 | `ED7477F236A32118…` | `Benchmarks/inputs/ts08/ts08_automatic_expectation.json` |
| `D101-X1-01` | input | x1 | 46,893 | `0917D74CC6E33997…` | `Benchmarks/inputs/x1/x1_events_space_time.csv` |
| `D101-X1-02` | answer | x1 | 3,598 | `EC3AC1A0998F9826…` | `Benchmarks/inputs/x1/x1_answer_bins.json` |
| `D101-X1-03` | negative | x1 | 278 | `8712D2446F2C8809…` | `Benchmarks/inputs/x1/x1_negative_no_time_field.csv` |
| `D101-X1-04` | updated-instance | x1 | 49,643 | `E01FD79880861775…` | `Benchmarks/inputs/x1/x1_events_space_time_updated.csv` |
| `D101-X1-05` | answer | x1 | 2,799 | `EDC6B3127763476F…` | `Benchmarks/inputs/x1/x1_answer_change_point.json` |
| `D101-X1-06` | answer | x1 | 3,915 | `F156452F1A48E5D0…` | `Benchmarks/inputs/x1/x1_answer_forecast.json` |
| `D101-X1-07` | automatic-expectation | x1 | 1,114 | `8B244DD4C058A017…` | `Benchmarks/inputs/x1/x1_animation_expectation.json` |
| `D101-X2-01` | input | x2 | 6,991 | `BD3C3A6D42830137…` | `Benchmarks/inputs/x2/x2_surface.asc` |
| `D101-X2-02` | input | x2 | 50 | `A828B692D0EBE7B2…` | `Benchmarks/inputs/x2/x2_observers.csv` |
| `D101-X2-03` | answer | x2 | 1,387 | `6D9EBE28FD88A581…` | `Benchmarks/inputs/x2/x2_answer_viewshed.json` |
| `D101-X2-04` | answer | x2 | 2,289 | `4235F47363ED888D…` | `Benchmarks/inputs/x2/x2_answer_profile.json` |
| `D101-X2-05` | input | x2 | 7,062 | `A9862B77E4BDF65D…` | `Benchmarks/inputs/x2/x2_surface_after.asc` |
| `D101-X2-06` | answer | x2 | 562 | `A4453D6DAEA795EE…` | `Benchmarks/inputs/x2/x2_answer_cutfill.json` |
| `D101-X2-07` | negative | x2 | 1,616 | `EAFB52258D3DD4A7…` | `Benchmarks/inputs/x2/x2_negative_grid_mismatch.asc` |
| `D101-X2-08` | updated-instance | x2 | 6,991 | `9D2D4CED7B9C701B…` | `Benchmarks/inputs/x2/x2_surface_updated.asc` |
| `D101-X2-09` | automatic-expectation | x2 | 570 | `454E6866A7E0471F…` | `Benchmarks/inputs/x2/x2_automatic_expectation.json` |
| `D101-X3-01` | input | x3 | 48,227 | `C3E55EC0C7CB149B…` | `Benchmarks/inputs/x3/x3_cloud_epoch1.las` |
| `D101-X3-02` | answer | x3 | 1,012 | `DC12F8CD2494E7B5…` | `Benchmarks/inputs/x3/x3_answer_counts.json` |
| `D101-X3-03` | input | x3 | 45,427 | `2A5A1C0167515866…` | `Benchmarks/inputs/x3/x3_cloud_epoch2.las` |
| `D101-X3-04` | answer | x3 | 1,406 | `85D413C07E49378A…` | `Benchmarks/inputs/x3/x3_answer_epochs.json` |
| `D101-X3-05` | negative | x3 | 540 | `0C6A67B953D7C403…` | `Benchmarks/inputs/x3/x3_negative_unsupported_formats.json` |
| `D101-X3-06` | updated-instance | x3 | 52,227 | `EA6631188BB9BF4E…` | `Benchmarks/inputs/x3/x3_cloud_epoch1_updated.las` |
| `D101-X3-07` | automatic-expectation | x3 | 520 | `98D164641965B775…` | `Benchmarks/inputs/x3/x3_automatic_expectation.json` |
| `D101-X4-01` | input | x4 | 8,964 | `F2940D7F1372BEA8…` | `Benchmarks/inputs/x4/x4_image_qa_b5.tif` |
| `D101-X4-02` | answer | x4 | 689 | `8BA316B57694C024…` | `Benchmarks/inputs/x4/x4_answer_mask.json` |
| `D101-X4-03` | input | x4 | 53 | `09BF996F2249DBCB…` | `Benchmarks/inputs/x4/x4_train_points.csv` |
| `D101-X4-04` | input | x4 | 110 | `8BD1F23C5877F7FF…` | `Benchmarks/inputs/x4/x4_val_points.csv` |
| `D101-X4-05` | answer | x4 | 2,388 | `174B557EEFC1D25B…` | `Benchmarks/inputs/x4/x4_class_legend.json` |
| `D101-X4-06` | input | x4 | 5,536 | `2EFA9D827E36076F…` | `Benchmarks/inputs/x4/x4_trend_stack_b12.tif` |
| `D101-X4-07` | answer | x4 | 4,589 | `C1382151730962B4…` | `Benchmarks/inputs/x4/x4_answer_trend.json` |
| `D101-X4-08` | negative | x4 | 1,168 | `2F3C55773A548655…` | `Benchmarks/inputs/x4/x4_negative_stack_grid_mismatch.tif` |
| `D101-X4-09` | updated-instance | x4 | 8,964 | `FA022D4749D5A786…` | `Benchmarks/inputs/x4/x4_image_qa_updated_b5.tif` |
| `D101-X4-10` | automatic-expectation | x4 | 661 | `8A5708920FB01096…` | `Benchmarks/inputs/x4/x4_automatic_expectation.json` |
| `D101-X5-01` | input | x5 | 994 | `EB055EA3DD121EBF…` | `Benchmarks/inputs/x5/x5_design_brief.json` |
| `D101-X5-02` | input | x5 | 2,423 | `DD519FE3713702ED…` | `Benchmarks/inputs/x5/x5_strata.geojson` |
| `D101-X5-03` | answer | x5 | 3,116 | `826ADD073C34BE2A…` | `Benchmarks/inputs/x5/x5_answer_sample.json` |
| `D101-X5-04` | negative | x5 | 389 | `DB1FBB056A85CADA…` | `Benchmarks/inputs/x5/x5_negative_no_seed.json` |
| `D101-X5-05` | input | x5 | 863 | `001AA6424B50DDFD…` | `Benchmarks/inputs/x5/x5_observations.csv` |
| `D101-X5-06` | answer | x5 | 1,188 | `5A5B4B739ADAF627…` | `Benchmarks/inputs/x5/x5_answer_interpolation.json` |
| `D101-X5-07` | negative | x5 | 652 | `B86207F42C9B2D65…` | `Benchmarks/inputs/x5/x5_negative_leaky_folds.json` |
| `D101-X5-08` | answer | x5 | 2,495 | `F02D4190457BA652…` | `Benchmarks/inputs/x5/x5_answer_decision.json` |
| `D101-X5-09` | automatic-expectation | x5 | 482 | `F4534379A371377C…` | `Benchmarks/inputs/x5/x5_automatic_expectation.json` |
| `D101-X6-01` | input | x6 | 1,754 | `8FD132863465DFFA…` | `Benchmarks/inputs/x6/x6_network.json` |
| `D101-X6-02` | answer | x6 | 1,337 | `B0B7404117F2451A…` | `Benchmarks/inputs/x6/x6_answer_od.json` |
| `D101-X6-03` | answer | x6 | 1,368 | `C45E21727A410475…` | `Benchmarks/inputs/x6/x6_answer_facilities.json` |
| `D101-X6-04` | answer | x6 | 1,019 | `D77EE6541AD72ED8…` | `Benchmarks/inputs/x6/x6_answer_allocation.json` |
| `D101-X6-05` | answer | x6 | 1,669 | `6F7C7229A709DCB5…` | `Benchmarks/inputs/x6/x6_answer_vrp.json` |
| `D101-X6-06` | answer | x6 | 478 | `CE4DE6D5C3255423…` | `Benchmarks/inputs/x6/x6_answer_disruption.json` |
| `D101-X6-07` | input | x6 | 582 | `E329F557E5DEE0D1…` | `Benchmarks/inputs/x6/x6_cost_surface.asc` |
| `D101-X6-08` | answer | x6 | 2,449 | `6788A7189AAFE5E6…` | `Benchmarks/inputs/x6/x6_answer_costdistance.json` |
| `D101-X6-09` | negative | x6 | 335 | `4F64BD31D3B93099…` | `Benchmarks/inputs/x6/x6_negative_disconnected.json` |
| `D101-X6-10` | updated-instance | x6 | 1,798 | `29CAB86012FC28FD…` | `Benchmarks/inputs/x6/x6_updated_network.json` |
| `D101-X6-11` | automatic-expectation | x6 | 608 | `4ED9FE08656FDD02…` | `Benchmarks/inputs/x6/x6_automatic_expectation.json` |
| `D101-X7-01` | input | x7 | 922 | `FEE2D6296F0807D3…` | `Benchmarks/inputs/x7/x7_conformant.geojson` |
| `D101-X7-02` | input | x7 | 886 | `028951AFC7F96C14…` | `Benchmarks/inputs/x7/x7_nonconformant.geojson` |
| `D101-X7-03` | answer | x7 | 851 | `2582451954D49A6B…` | `Benchmarks/inputs/x7/x7_answer_conformance.json` |
| `D101-X7-04` | input | x7 | 496 | `8874C4B1CFDAF6F6…` | `Benchmarks/inputs/x7/x7_vertical_grid.asc` |
| `D101-X7-05` | input | x7 | 73 | `921B1D609A34BFF9…` | `Benchmarks/inputs/x7/x7_z_points.csv` |
| `D101-X7-06` | answer | x7 | 863 | `8CFCAB8F3E721B73…` | `Benchmarks/inputs/x7/x7_answer_vertical.json` |
| `D101-X7-07` | input | x7 | 1,789 | `91CCA014CA1C41AD…` | `Benchmarks/inputs/x7/x7_offline_package.zip` |
| `D101-X7-08` | answer | x7 | 1,227 | `80A6FC02C34C1D22…` | `Benchmarks/inputs/x7/x7_answer_package.json` |
| `D101-X7-09` | negative | x7 | 511 | `4FCC5B4DB7157463…` | `Benchmarks/inputs/x7/x7_package_external_ref.json` |
| `D101-X7-10` | input | x7 | 1,302 | `42F0A104BBCBB6FB…` | `Benchmarks/inputs/x7/x7_cube.csv` |
| `D101-X7-11` | answer | x7 | 746 | `EC61EB49D7797586…` | `Benchmarks/inputs/x7/x7_answer_cube.json` |
| `D101-X8-01` | input | x8 | 571 | `B80179094EEDF881…` | `Benchmarks/inputs/x8/x8_fact_table.csv` |
| `D101-X8-02` | answer | x8 | 3,234 | `2010AC94204E780F…` | `Benchmarks/inputs/x8/x8_answer_report.json` |
| `D101-X8-03` | negative | x8 | 541 | `687612E97CD2A3CA…` | `Benchmarks/inputs/x8/x8_negative_number_mismatch.json` |
| `D101-X5-10` | updated-instance | x5 | 1,529 | `F23FC2D5B5F71F1A…` | `Benchmarks/inputs/x5/x5_updated_design.json` |
| `D101-X7-12` | updated-instance | x7 | 1,943 | `2FFC3B93410BB8C2…` | `Benchmarks/inputs/x7/x7_offline_package_updated.zip` |
| `D101-X7-13` | automatic-expectation | x7 | 629 | `72A62B39CC14A144…` | `Benchmarks/inputs/x7/x7_automatic_expectation.json` |
| `D101-X8-04` | updated-instance | x8 | 667 | `C7E4A78D9D72F0DB…` | `Benchmarks/inputs/x8/x8_fact_table_updated.csv` |
| `D101-X8-05` | automatic-expectation | x8 | 692 | `2C07D1173DE523A8…` | `Benchmarks/inputs/x8/x8_automatic_expectation.json` |

## Sources

- G-222 R-1, R-2 and R-4; GATE-G205 (TS08 predicate), GATE-G216, GATE-G220.
- `Benchmarks/comparison-rules-v1.json` (TS08-INPUT-QUAL-001), `comparison-rules-v4.json`
  (KDA-001, the eight domain rules and the six `d097CalibrationChoice` envelopes, inherited unchanged).
- `Benchmarks/g-domain-precalibration-v1.json` (the four artefacts and the per-domain blocking
  preconditions this corpus answers), `m4-scenarios-27.jsonl` (the 27 inputQualification rows).
- `.runtime/evolution/v5-f/run-20260930-d101/` (`formats_d101.py`, `synthesize_inputs.py`,
  `field-schema-draft-d101.md`, `intake/`, `verify-delivery-d101.py`, `evidence-index.md`).
