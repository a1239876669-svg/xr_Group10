# Phase 2 Weekly Activity Record

COMP5424 Group 10 — Scenario C: Virtual Apartment Design Review
Prepared 18 October 2026. Integrated source: [`d0f512eb453a19d47352dccf2dc4979bd094a90d`](https://github.com/a1239876669-svg/xr_Group10/commit/d0f512eb453a19d47352dccf2dc4979bd094a90d), `merge-apartmentguidence`.

## Evidence and confirmation limits

This record reconstructs **repository development events**, supported by commit contents, author dates, PR merge records, existing design notes and the user's hardware reports. It is not a retrospective claim that weekly meetings occurred. The earlier local activity-record draft was an unfilled template; no complete contemporaneous meeting record was found. Hours, attendance, oral decisions and work before Git remain **to be confirmed by the members**. Dates below use Asia/Shanghai (+08:00), converted from recorded Git author or merge dates; they do not measure working duration.

The team details and roles below retain the integrated README. See [Contribution Evidence](Contribution_Evidence.md) for identity qualifications, original authors and imported-source provenance. A merge author is not the author of every incoming feature. Some implementation, checks and document preparation in the Kai-led workflow used Codex assistance under human direction; repository attribution is not a claim of independent manual authorship.

## Team responsibilities recorded in the README

| Member | Student ID | Recorded account | Main responsibility |
|---|---|---|---|
| Guo Jikang | 26125103g | YuzYue | Measurement and feedback |
| Fan Zujian | 26124914g | fanfanzujian-oss | UI and onboarding |
| Yang Jiale | 26042226g | YJL-196 | Movement comfort |
| Zhuang Kaiyu | 26109544g | KaiZ755 | Apartment modelling and shopping bag |
| Xu Boyan | 26052354g | a1239876669-svg | Integration, version control and setup |

The README records account `YuzYue`, while the measurement commit's raw author name `YuzYue` is linked by GitHub to login `YuzYue7`. The public user endpoints resolve to different account IDs (`yuzyue`: 69242538; `YuzYue7`: 202832118), without a redirect. Guo should confirm the correct account; the original README information and student ID are preserved pending confirmation. Yang's commits identify `YANG Jiale`, but GitHub returns no linked author account for those commits; the `YJL-196` mapping comes from the team README and needs his confirmation. Student IDs are preserved, not independently authenticated.

## Week of 5–11 October 2026

| Repository date | Attributed work / responsibility | Completed evidence | Design or integration significance | Remaining qualification |
|---|---|---|---|---|
| 6 Oct, 19:02 | `A`, linked to a1239876669-svg; initial project check-in | [`bf9222e`](https://github.com/a1239876669-svg/xr_Group10/commit/bf9222e) | Establishes an initial shared repository baseline. | Commit date does not establish when all imported assets were created. |
| 11 Oct, 00:51 | KaiZ755; apartment graybox | [`7e8c817`](https://github.com/a1239876669-svg/xr_Group10/commit/7e8c817): layout JSON, graybox builder, scene and geometry assumptions | A metric, repeatable layout makes spatial assumptions inspectable. Confirmed, derived and assumed dimensions are distinguished in the design notes. | Simplified geometry is not a construction model or a compliance assessment. |
| 11 Oct, 02:34 | KaiZ755; furnished layout | [`3e6c531`](https://github.com/a1239876669-svg/xr_Group10/commit/3e6c531): furniture layout JSON, builder, materials and furnished scene | Furniture extents and orientation support circulation review. | Does not establish ownership of third-party furniture meshes. |
| 11 Oct, 15:14 | KaiZ755; initial VR/navigation foundation | [`5b697b9`](https://github.com/a1239876669-svg/xr_Group10/commit/5b697b9): initial VR scene, locomotion and entrance-safety tools | Reuses the XR rig and introduces a navigable baseline and safety handling. | Later UI, comfort and scene integration are separate contributions. |
| 11 Oct, 15:34 | YANG Jiale; VR/configuration follow-up | [`c55a4a5`](https://github.com/a1239876669-svg/xr_Group10/commit/c55a4a5) | Records a subsequent scene/configuration iteration. | This is not a Kai-authored commit. |

**Member confirmation still needed:** actual allocation discussions, any meeting notes, time spent, and the dated Baidu Netdisk hand-off of the initial apartment model. The user reports having made and shared that model; Git does not fully establish the pre-repository creation process.

## Week of 12–18 October 2026

| Repository date | Attributed work / responsibility | Completed evidence | Decision or implemented behavior evidenced by the artefact |
|---|---|---|---|
| 13 Oct, 19:12 | Xu/account a1239876669-svg; early scene integration | [PR #1](https://github.com/a1239876669-svg/xr_Group10/pull/1), merge `fc0444c` | Integrates the earlier apartment scene into main. It is not the later final integrated prototype. |
| 14 Oct, 00:12 | fanfanzujian-oss; detailed apartment and interaction UI | [`a976cbd`](https://github.com/a1239876669-svg/xr_Group10/commit/a976cbd) | Detailed VR apartment, menu/onboarding, furniture/door interaction and scene wiring provide the common user-facing foundation. |
| 15 Oct, 16:09; 16:59 | YuzYue, GitHub-linked YuzYue7; review tools and package-resolution follow-up | [`962e77d`](https://github.com/a1239876669-svg/xr_Group10/commit/962e77d), [`87f1f68`](https://github.com/a1239876669-svg/xr_Group10/commit/87f1f68) | Measurement, location-linked markers, structured review and storage are integrated with the menu on the source branch. Later formal-scene import must retain this provenance. |
| 15 Oct, 21:49; 22:19 | KaiZ755; ShoppingBag A/B | [`470468d`](https://github.com/a1239876669-svg/xr_Group10/commit/470468d), [`5769964`](https://github.com/a1239876669-svg/xr_Group10/commit/5769964) | Three metric bag profiles, near Grip/single ownership, explicit contact targets, red feedback and holding-hand haptics. Contact warnings inform the review instead of universally blocking movement. |
| 15 Oct, 22:57 | YANG Jiale; movement comfort | [`de67e8e`](https://github.com/a1239876669-svg/xr_Group10/commit/de67e8e) | Comfort presets/vignette, motion ramp and dynamic-font iteration. Comfort effectiveness still requires representative-user evaluation. |
| 16 Oct, 03:29 | KaiZ755; ShoppingBag C1 | [`40193be`](https://github.com/a1239876669-svg/xr_Group10/commit/40193be) | Blocks new acquisition during unsafe states, preserves release, and requires a neutral Grip sample per hand before reacquisition. |
| 16 Oct, 20:57–20:58 | KaiZ755; bag/UI integration | [`b8cbf42`](https://github.com/a1239876669-svg/xr_Group10/commit/b8cbf42), [`9e0987e`](https://github.com/a1239876669-svg/xr_Group10/commit/9e0987e) | Adds the bridge, My Items size selection and joint tests; the second commit only normalizes serialization whitespace. |
| 16 Oct, 22:23; 23:00; 23:11 | Xu/account a1239876669-svg; team integration | [PR #5](https://github.com/a1239876669-svg/xr_Group10/pull/5), [#6](https://github.com/a1239876669-svg/xr_Group10/pull/6), [#7](https://github.com/a1239876669-svg/xr_Group10/pull/7) | Ordinary merges retain UI, comfort and bag integration history in `merge-apartmentguidence`. |
| 17 Oct, 02:47 | Xu/account a1239876669-svg; formal scene assembly and review import | [`c3408f4`](https://github.com/a1239876669-svg/xr_Group10/commit/c3408f4) | Brings review sources and formal scene wiring together; importing a file does not transfer its original authorship. |
| 17 Oct, 23:42; 23:55 | Xu/account a1239876669-svg; bag hint and README metadata | [`d3ef3ce`](https://github.com/a1239876669-svg/xr_Group10/commit/d3ef3ce), [`a27dd1c`](https://github.com/a1239876669-svg/xr_Group10/commit/a27dd1c) | Adds a scene hint/font content and repository/member instructions. |
| 18 Oct, 17:10–19:39 | KaiZ755; reviewer-facing documentation | [`37af622`](https://github.com/a1239876669-svg/xr_Group10/commit/37af622), [`eef9203`](https://github.com/a1239876669-svg/xr_Group10/commit/eef9203), [PR #9](https://github.com/a1239876669-svg/xr_Group10/pull/9) | Corrects design-review positioning and preserves member information, setup and honest validation limits. |
| 18 Oct, 19:30; merged 21:36 | KaiZ755; Guided Journey and scoped feedback integration | [`07ba53a`](https://github.com/a1239876669-svg/xr_Group10/commit/07ba53a), [PR #10](https://github.com/a1239876669-svg/xr_Group10/pull/10) | Adds observable G00–G11 progress, real bag carry/drop and review/save progression. Kitchen completion uses the designated floor zone, not implemented cabinet storage. |
| 18 Oct, 19:43; merged 21:40 | KaiZ755; approved OpenXR reference repair | [`d75ff2c`](https://github.com/a1239876669-svg/xr_Group10/commit/d75ff2c), [PR #11](https://github.com/a1239876669-svg/xr_Group10/pull/11) | Single-asset repair preserves the approved feature configuration and avoids whole-asset regeneration. It does not prove the historical native crash's cause is fixed. |
| 18 Oct, 20:55–22:11; merged 22:31 | KaiZ755; bilingual UI, safe recovery and session display | [`53434fb`](https://github.com/a1239876669-svg/xr_Group10/commit/53434fb), [`913cc8b`](https://github.com/a1239876669-svg/xr_Group10/commit/913cc8b), [`8848bf5`](https://github.com/a1239876669-svg/xr_Group10/commit/8848bf5), [`f3b2507`](https://github.com/a1239876669-svg/xr_Group10/commit/f3b2507), [PR #12](https://github.com/a1239876669-svg/xr_Group10/pull/12) | Per-run English/live Chinese, stable guide/tool hints, non-destructive measurement/marker recovery and persistent data separated from current visuals. Integration uses a normal merge; original core/schema are preserved. |

These are recorded development/merge times, not scheduled meetings or estimates of effort. The original bag commits have author-preserving integrated counterparts `20be7f0`, `295e199`, `e5f5ab1`; do not count them as independent extra implementations.

## Verification actually evidenced

| Scope | Evidence and result | Boundary |
|---|---|---|
| Isolated bag A/B/C1 | Existing records: 78/78 EditMode, including earlier 26 A/B cases; 2/2 simulated PlayMode. User-reported Quest 3: five grab/release cycles per hand, four C1 checks, eight Phase B checks. | The 16 Oct archive date is not an invented exact hardware execution time. Independent module passes alone do not prove formal-apartment completion. |
| Guided Journey | User-reported G00–G11 pass on `07ba53a`. | No generated screenshots or detailed execution times. |
| Merged PR #12 feature source `8848bf5` | Actual Unity 6000.6.4f1/DX11 EditMode: 172 passed, 0 failed, 0 skipped; 18 Oct 22:07:59–22:08:00 +08:00. [Recorded results](../UISessionRefinement/Verification/PR12-Integration-Results.json). | Known TMP shutdown exception is retained; this run is not Quest or Windows Player validation. |
| PR #12 head `f3b2507` | User explicitly reported Quest 3 success for fresh English/live language switching, UI/tutorial, G00–G11, bag, measurement, Marker, session display, save/reopen, safe teleport and second Play, with no blocking issue. | User-reported Pass, received 18 Oct; execution times, screenshots and raw hardware logs were not supplied. This is not Windows EXE acceptance. |
| Final integrated build and course-machine execution | Consult [Verification Summary](Verification_Summary.md) and the actual build artefacts. | Do not infer a successful build or PQ604A/B/C run from a merge or Editor tests. |

## Final integration milestones

GitHub was queried directly for these records; the statuses are not inferred from draft document wording.

| Milestone | Actual result |
|---|---|
| README PR #9 | Merged normally as `0f4e8b4d9f833c4b90d3c52dacc2a66d6916a343`. |
| Guided Journey PR #10 | Merged normally as `ab149710c303d0ba8577db864cb9bdf961195707`; second parent is the tested `07ba53a`. |
| OpenXR PR #11 | Merged normally as `e0ddc2c752d99aab36941615635caa5cc90c2695`. |
| UI/session PR #12 | Merged normally as `d0f512eb453a19d47352dccf2dc4979bd094a90d`; parents are `e0ddc2c` and `f3b2507`. |
| Older PR #2 / #3 / #8 | Still open at this evidence snapshot; do not mark them merged. Corresponding bag code reached integration through PR #7; review code was imported at `c3408f4`. |

## Member completion and remaining issues

| Item requiring genuine confirmation | Owner / action |
|---|---|
| Weekly allocation discussions, meetings, attendance, hours and decision dates not established above | Each member adds or confirms only records they can substantiate; do not backdate. |
| Identity/alias and contribution scope | Each member confirms their row in Contribution Evidence, especially `YuzYue`/`YuzYue7` and `YANG Jiale`/`YJL-196`. |
| Pre-Git apartment model and external assets | Kai supplies dated Netdisk/source artefacts if used in personal claims; asset importers confirm sources/licences. |
| Windows Player, save/reopen and classroom compatibility | Final testing/integration owner records actual run outcomes separately from Editor/Quest reports. |
| Historical native XR failures and TMP quit exception | Retain logs; no proven native-crash root-cause fix or representative comfort/user study is claimed. |

This evidence-based record supports, but does not replace, the team's confirmation of actual weekly work and individual reflection.
