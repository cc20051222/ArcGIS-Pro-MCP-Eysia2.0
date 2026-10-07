# F05 G-DOMAIN pre-calibration (PASS CANDIDATE, definition only)

`g-domain-precalibration-v1.json` is the machine source. This is the roadmap §9 M4 exit gate:
every domain needs a known answer, a negative example, a complete automatic result and an updated
instance. It freezes what will be checked; it is not evidence that anything was.

| criterion | predicate | severity | state today |
|---|---|---|---|
| `GDOM-01` known answer from an independent path | For each domain row the manifest names an answer computed by a path other than the candidate under test, with the method… | critical | NOT-AVAILABLE-PENDING-USER for all eight M4 domains (no qualifying domain input exists in this workspace) |
| `GDOM-02` negative example | Each domain ships at least one input that must be refused before output, with the reason expected in advance.… | critical | defined per scenario as the invalid profile; not run |
| `GDOM-03` complete automatic result | The result is produced without a manual step; a development-time manual fix is never booked as an automatic success (inh… | major | PENDING-EXECUTION |
| `GDOM-04` updated instance | A changed input re-run keeps the design intent and publishes only the declared deltas.… | major | PENDING-EXECUTION |
| `GDOM-05` domain ledger separated from the tool ledger | A domain pass never increments a tool-availability account by itself, and a GP or bridge path used under a whitelist ent… | critical | definition only; inherited from GBC-001 and RAS-003 |

## Domain readiness (recomputed at build time)

| domain | scenarios | qualifying input present | blocking precondition |
|---|---|---|---|
| space-time-trends | S31, S32, S33, S34, S35 | no | a time-enabled series with a declared calendar and a known change or forecast answer |
| three-dimensional | S36, S37, S38, S39 | no | an elevation surface with published control heights and a declared vertical datum |
| point-cloud-terrain | S40, S41, S42, S43 | no | a real point cloud per format with a reference ground set (no .las/.laz/.copc here) |
| remote-sensing-models | S44, S45, S47, S48, S49 | no | multi-band imagery with published QA plus a reference mask, class legend or model register |
| science-decision-uncertainty | S50, S51, S53, S54 | no | observations plus a published design, method and fold geometry |
| network-facility | S55, S56, S57, S58 | no | a network dataset with impedance and direction (no solver entry in the frozen 53) |
| local-interchange | S59 | no | an interchange standard version and a modern container adapter (none in the accepted source) |
| same-source-report |  | no | an approved Open XML generator and a verified document channel |

No row above claims a tool, a licence, a fixture or a run. `NOT-AVAILABLE-PENDING-USER` is the
honest state for all eight breadth domains in this workspace.

