# Spatial design acceptance - pre-evaluation stabilization

Baseline: main 2dd047056e661512c831601481d3b82519056a1c, Unity 6000.6.4f1, 2026-10-26. This is developer-inspection/owner-symptom-driven stabilization, not an improvement inferred from non-developer participant evaluation.

## Area declaration
The model has an approximate outer footprint of 72 m², including wall footprints and the balcony. This is a modelling assumption, not a certified saleable or net internal area.

Use actual floor boundary vertices / polygon area, not AABB multiplication alone. Keep the outer X=0..9.6, Z=0..7.5 perimeter. Balcony 5.436 m² is already inside; the exterior safety platform 8.06 m² is outside. The wall solid union at Y=.10m is subtracted once using planar union, not summed at wall intersections. Recomputed before values: wall union 10.418225646 m²; floor-minus-wall 61.581777204 m²; additionally minus balcony 56.145777288 m². Final values: wall union 10.328625678 m²; floor-minus-wall 61.671377172 m²; additionally minus balcony 56.235377256 m². The 0.089599968 m² increase follows only local doorway changes. Closed floor area is 72.000002850 m²; tiny departures from 72.00 and earlier rounded estimates are floating-point/source-vertex rounding. These are section-dependent geometric references, not certified usable area; furniture/frames/tiles are not all deducted. No whole-house scaling.

## Classification and acceptance matrix
A = verified applicable formal requirement; B = residential universal-design recommendation; C = prototype design choice; D = unresolved assumption. No site-specific Hong Kong statutory applicability has been established for this educational private-flat model.

| Use | Method / before | Target / tolerance | Category / basis | Verification / limits |
|---|---|---|---|---|
| Outer envelope | Closed floor polygon and outside wall faces, 72 m² | Same perimeter, no expansion; numeric tolerance .001m | C / Scenario approximately72m², undefined area basis | Imported vertices + offline cross-check; not saleable/net certification |
| Furniture supports | Actual transformed mesh bottoms, about .118-.120m above main floor and .105-.108 above balcony | Correct support +1mm, numeric tolerance .2mm; preserve scales and XZ child overrides | C, visual/numerical allowance, not construction standard | Unity imported mesh vertices; final sofa and coffee-table XZ projections fully inside the rug; not a certified foot-load contact analysis |
| Rug support | Visual thickness .00586m, support top .12586m | Continuous main Floor retained; redundant raised support collider disabled | C | Floor collider/rays/route checks plus Quest pending |
| Entrance / bath | Full-open leaf to opposite finished jamb, about .6387/.6825m | >=.85m effective clear opening, no negative target tolerance | C, informed by B HKHS3.10.2 | Door leaf/frame/finish/solid/sweep together; not legal blanket minimum |
| Core travel | Actual capsule along continuous Entrance-Living-Kitchen and Living-Balcony | .90m natural route design aim where feasible; door openings separately measured | C, not BFA1050mm compliance | Full collider path + turns + two-way Quest; non-route gaps need not meet .90m |
| Door sweep | All angles, fixed furniture/walls, protected head and fresh-press recovery | No unintended penetration; Blocked/Pause behavior unchanged | C; B HKHS door-manoeuvring guidance | Unity physics samples and Quest; not endpoint-only AABB |
| Furniture operation | Chair use/pull-out, cabinet sweep and standing positions | Preserve basic usable relationships; no scaling to manufacture clearance | C, B contextual reference | Empty bedrooms/incomplete bath cannot be evaluated as furnished rooms |
| Bags and tasks | Actual S .30x.30x.15, M .45x.40x.22, L .60x.48x.30m | Keep size, grip, rearm, sensing, red overlay and holding-hand haptics | C / frozen task | No rigid carried-bag blocking; explicit targets only; no red does not prove no contact |
| Data | World-coordinate review records and scene path key | Copy original data before edits; never delete/migrate/rewrite JSON | C / research integrity | Old markers may no longer align; developer and participant datasets separated |
| Height | Disabled ceiling bottom2.80m, door lintels2.10m | No ceiling/lighting change | C model fact / D operational | Not a claim of building/fire/light/ventilation/accessibility compliance |

## Verified original references and scope
- Scenario C p2: reference two-bedroom home approximately72m²; p3 no CAD/BIM, fixed envelope/structural core, simplified model and deviations disclosed.
- Official Phase3 rubric p2 group3/2/3 (design+conduct / analysis+evidence / prioritization+integrated outcome); p3 individual2/1/1 (technical refinement / evaluation contribution / reflection). Description pp8-9 normally>=5 non-developer participants, consent/anonymity, systematic analysis, evidence-linked substantive refinement and regression; group body<=6pages, individual<=750words. Local Canvas screenshots say22Nov2026 23:59; live Canvas/any later teacher clarification not reverified. Official documents override automated class summary.
- [BD BFA2008, 2025 edition](https://www.bd.gov.hk/doc/en/resources/codes-and-references/code-and-design-manuals/BFA2008_e.pdf): printedp4 Table1 limits domestic applicability largely to common areas/entrances/access routes; printedp57 §37-39 accessible-route doors, 800mm clear width; printedp49 §31 applicable corridors1050mm; printedp59 §42 thresholds<=20mm bevelled. Not automatically mandatory for every private-flat internal gap.
- [HKHS2005 chapter3](https://www.hkhs.com/home/upload/pdf/07_ch3.pdf) §3.10.2 pp65-66 recommends850mm doors; §3.13.2 pp73-74 contextual furniture/turning space. Recommendations, not general statutes.
- [SRPA FAQ3.2](https://www.srpa.gov.hk/sc/faq-purchasers-part3.html): saleable area is distinct from clear floor and can include walls/balcony; this model is not certified under that definition.
- [BD APP-5 p1](https://www.bd.gov.hk/doc/en/resources/codes-and-references/practice-notes-and-circular-letters/pnap/APP/APP005.pdf): structural room-height measures and exceptions; door lintel height is not whole-room height.

Formal course PDFs: D:/Documents/COMP5424 XR/. Their inspected SHA256 values are recorded in the change log. This batch does not replace participant evaluation, coding, negative evidence, prioritized backlog or subsequent evidence-led technical refinement.
## Actual outcome, 26 October 2026

Scene change commit: be9f20cd5fef102534f967ee24b552e0e4acfeac. Final scene SHA-256: 23a9813c3479d699846a03a277d9368b3482bd5eb5f70383c6f81dc0bc710da5.

| Criterion | Current evidence | Outcome |
|---|---|---|
| Approximate 72 m² envelope, metre scale | Closed polygon 72.000002850 m²; original boundary and floor collider retained | Met within numeric tolerance |
| Furniture supports / carpet | 14 individually measured instances; carpet support disabled; true floor retained | Unity mesh/support checks passed; visual comfort pending |
| Entrance / bath effective opening >=.85m | Real fully-open solid/finished clearances .974500 / .875000m | Selected prototype target met |
| Bedroom 2 swing / existing doors | Bedroom 2 -90 degrees inward; ten real door component open/close/blocked/resume fixtures | Automated geometry passed; headset/player position pending |
| Core route target .90m where feasible | Sofa west axis gap .931429m, north local gap .769407m; r=.30m physics paths at three exact destinations | Partial: continuous static capsule routes exist; .90m full-route aim not established |
| Furniture relationships | Sofa and coffee table fully over rug; known balcony table/right-chair mesh intersection removed | Improved; chair pull-out, sitting and furnished bedrooms/bath not comprehensively verified |
| Task, data and bag invariants | Unchanged runtime scripts/Prefabs, task anchors and review key; 7 spatial + 7 storage tests | Automated checks passed; Play lifecycle / Quest / Player pending |

The coffee-table/TV strip is not a designated passage. The intended main route runs west and north of the sofa. The new balcony table-to-right-chair gap (~.06911m) is a furniture assembly gap, not a human route. No local gap is represented as proof of a statutory minimum or complete room usability. See the change log and before/after CSV for the detailed evidence and rejected-layout rationale.
