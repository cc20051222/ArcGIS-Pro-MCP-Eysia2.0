# F05 M4 scenario-template-criterion map (PASS CANDIDATE)

Bidirectional closure: every M4 scenario names one primary template plus zero or more secondary
templates, and every new template lists exactly the scenarios that name it. Carried templates gain
M4 scenarios by reference only; their frozen bytes are not restated.

| scenario | template | face | secondary | criteria | planned tools |
|---|---|---|---|---|---|
| `S31` | `TS09` | M2 | TS12 | ALN-001, GEO-001, KDA-001, LAY-001, LIX-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, STM-001, TXT-001, WF-001 | build_space_time_cube, compare_temporal_trajectories, export_spatiotemporal_cube |
| `S32` | `TS12` | M4 | - | KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, STM-002, TXT-001, WF-001 | detect_temporal_change_points |
| `S33` | `TS11` | M4 | - | KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, STM-003, TXT-001, WF-001 | forecast_spatiotemporal_series |
| `S34` | `TS06` | M2 | - | GEO-001, KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, STM-004, TXT-001, WF-001 | analyze_emerging_hotspots |
| `S35` | `TS09` | M2 | - | BAT-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, RFL-001, SCI-001, STM-005, STY-001, TXT-001, WF-001 | export_time_animation |
| `S36` | `TP14` | M4 | TS13 | GEO-001, KDA-001, LAY-001, LIX-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, SCN-001, SCN-003, TXT-001, WF-001 | configure_layer_elevation, configure_scene_environment, create_elevation_profile, extrude_scene_features, transform_vertical_coordinates |
| `S37` | `TP15` | M4 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, SCN-002, TXT-001, WF-001 | analyze_viewshed |
| `S38` | `TP14` | M4 | - | GEO-001, KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, SCN-002, TXT-001, WF-001 | analyze_line_of_sight |
| `S39` | `TP16` | M4 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, SCN-003, TXT-001, WF-001 | calculate_cut_fill |
| `S40` | `TS13` | M4 | - | KDA-001, LAY-001, NUM-001, OUT-001, PCL-001, PRM-001, PRT-001, QLT-001, SCI-001, TXT-001, WF-001 | filter_point_cloud, inspect_point_cloud |
| `S41` | `TS07` | M2 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PCL-002, PCL-003, PRM-001, PRT-001, SCI-001, TXT-001, WF-001 | classify_ground_points, derive_terrain_surface |
| `S42` | `TS14` | M4 | - | GEO-001, KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PCL-003, PRM-001, PRT-001, SCI-001, TXT-001, WF-001 | derive_canopy_height |
| `S43` | `TS03` | M2 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PCL-003, PRM-001, PRT-001, SCI-001, SRC-001, TXT-001, WF-001 | compare_point_cloud_epochs |
| `S44` | `TS08` | M1 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, RMO-001, RMO-002, SCI-001, TS08-INPUT-QUAL-001, TXT-001, WF-001 | assess_remote_sensing_quality, mask_cloud_and_shadow |
| `S45` | `TS15` | M2 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, RMO-003, SCI-001, SRC-001, TXT-001, WF-001 | classify_land_cover |
| `S47` | `TS08` | M1 | - | ALN-001, GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, RMO-002, SCI-001, TXT-001, WF-001 | segment_raster_objects |
| `S48` | `TS08` | M1 | - | GEO-001, KDA-001, LAY-001, MRC-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, SRC-001, TS08-INPUT-QUAL-001, TXT-001, WF-001 | infer_approved_raster_model |
| `S49` | `TS09` | M2 | TS12 | ALN-001, GEO-001, KDA-001, LAY-001, LIX-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, RMO-004, SCI-001, TXT-001, WF-001 | build_raster_time_series, detect_raster_trends, export_spatiotemporal_cube |
| `S50` | `TS18` | M4 | - | GEO-001, KDA-001, LAY-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, SDU-001, TXT-001, WF-001 | design_spatial_sample |
| `S51` | `TS16` | M4 | - | GEO-001, KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, SDU-002, SDU-003, TXT-001, WF-001 | cross_validate_spatial_model, interpolate_surface, validate_statistical_assumptions |
| `S53` | `TP18` | M4 | - | KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, SDU-003, TXT-001, WF-001 | analyze_scenario_sensitivity, compare_multi_criteria_scenarios, validate_statistical_assumptions |
| `S54` | `TS17` | M4 | - | KDA-001, LAY-001, NUM-001, OSV-001, OUT-001, PRM-001, PRT-001, SCI-001, SDU-003, TXT-001, WF-001 | evaluate_scenario_ensemble |
| `S55` | `TP04` | M2 | - | GEO-001, KDA-001, LAY-001, NFD-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, TXT-001, WF-001 | build_od_cost_matrix, find_closest_facilities |
| `S56` | `TP12` | M4 | TP17 | GEO-001, KDA-001, LAY-001, NFD-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, SRC-001, TXT-001, WF-001 | solve_location_allocation |
| `S57` | `TP13` | M4 | - | GEO-001, KDA-001, LAY-001, NFD-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, TXT-001, WF-001 | optimize_vehicle_routes |
| `S58` | `TP11` | M4 | - | DEP-001, GEO-001, KDA-001, LAY-001, NFD-001, NUM-001, OUT-001, PRM-001, PRT-001, SCI-001, TXT-001, WF-001 | analyze_network_disruption |
| `S59` | `TP14` | M4 | - | DEP-001, KDA-001, LAY-001, LIX-001, NUM-001, OUT-001, PKG-001, PNA-001, PRM-001, PRT-001, QLT-001, SCI-001, SCN-004, SRC-001, TXT-001, WF-001 | build_offline_map_package, export_scene_package, validate_interchange_conformance |
