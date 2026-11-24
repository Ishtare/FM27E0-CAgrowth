# Changelog

## v1.1.33 (modified release, 2026-11-24)

- Add a dedicated Steam profile for FM27 27.1.1+2511875. Match the engine image and five native signatures covering non-EU status, the game date, retirement, fitness and corporate facilities. Restore club funds, facilities, reputation, stadium capacity, tickets, sponsorship and names, plus condition, match sharpness, morale, injuries and fatigue.
- Recover finance types in the generic scanner for PC Game Pass, Epic and later 27.1.x builds that share a verified layout. Distinguish full and compact finances by native type and check the supported field layout before applying it. Keep compact finances read-only and retain the existing vote and safety thresholds.
- Keep individual domestic and UEFA registration editing read-only on Steam 27.1.1. Retirement, supported competition rules and verified Italian historical non-EU status continue to use their own checks.
- Pass 35,105 offline assertions. The Steam 27.1.1 acceptance build passed in-game write and undo spot checks.

## v1.1.32 (modified release, 2026-11-24)

- Use a 15/14/11/10 baseline for role-based redistribution. With extra budget, raise blue to 17, green to 15, blue to 20, then green to 17. Below the baseline, lower other, white, green, then blue. Keep hard floors of 6 for other attributes and 8 for white. If all floors still exceed the target CA, preview the RCA shortfall and block the change.
- Use two passes for directed growth and Preserve: blue 15, green 14, white 11 first, then blue 17 and green 15. Retain existing attributes and charge growth to the cumulative RCA budget. The default acceleration mode continues to write CA only.
- Order attributes within tiers using position-group priorities informed by FM27.0.x match engine results, with a separate goalkeeper cross-reference. Keep the existing role colors and RCA formula.
- Publish a smaller player ZIP with an ASCII filename, the matching buildable source archive, licenses, attribution notices and checksums. Generate forum text separately.

## v1.1.31 (modified release, 2026-11-24)

- Optimize attributes in four role tiers with targets of 17+ for blue core skills, 15+ for green supporting skills, around 12 for white foundations, and around 10 for other adjustable attributes. Combine core and supporting skills from both tactical phases. Reduce targets gradually with floors at low CA and spend surplus on blue skills first.
- Show targets reached, projected ranges, lowered attributes, and unmet blue and green goals in the optimization preview. Fit and scroll the settings at larger font sizes while keeping the action buttons visible.
- Allocate directed growth against a cumulative estimated RCA budget, filling blue, green, then white. Repeated steps share the same allowance and preserve existing non-priority attributes above 10. The default acceleration mode continues to write CA only.

## v1.1.30 (modified release, 2026-11-22)

- Match club names entered with full-width Latin characters or without accents. Show club type hits, record checks, filtering, and the final catalog count in the connection report.
- Default new growth plans to CA-only acceleration and let FM distribute attributes. Check directed growth against estimated recommended CA for the player's actual positions and limit the proposed increases to the available budget.
- Set growth mode, speed, and unchecked-attribute ceilings for the full watched queue, including players from other clubs or hidden by filters. Preserve each player's existing custom attribute selections.

## v1.1.29 (modified release, 2026-11-21)

- Validate all edited club finance fields before writing. Reject non-finite currency conversions and rounded values outside the signed 32-bit range. Check the selected club and the original field values at submission.
- Ask before lowering a funding freeze floor. Read back each finance write and report the result per field, including skipped values, failed writes and verification errors.
- Refine the responsive workspace with aligned and framed grids, a compact connection toolbar and readable operation feedback. Improve text contrast and give primary, secondary and undo buttons explicit styles.
- Open player and staff profiles from search by button or keyboard. Clarify growth actions and registration scope while preserving the selected view. Standardize auxiliary dialogs, focus order and confirmation defaults.

## v1.1.28 (modified full test build, 2026-11-21)

- Separate physiotherapy and doctor roles. Use one catalog of 21 roles for staff search and transfers, including head performance analysts, head coaches and directors of football.
- Identify basic staff contracts through the native class descriptor. Staff with corroborated employment and incomplete attribute data remain searchable and transferable. The job search starts without a required attribute.
- Keep the existing job or choose a new one during a transfer. Use the same checked path for a job change at the current club. Select clubs by verified teams, including patched clubs with compact finances.
- Confirm club/team ownership, department lists and dedicated posts. Apply the employment change as a group, read back the result, and support grouped undo and employer display refresh.

## v1.1.27 (modified full test build, 2026-11-20)

- Add per-player, opt-in ceilings for unchecked custom-growth attributes. Capture the ceiling at the start of a growth run and limit low-value support to checked attributes whenever the option is enabled.
- Enforce ceilings in scheduled growth and in bounded batches between game dates. Keep opening values across a verified roster refresh. Release the ceiling when growth stops or the player reaches PA.
- Start a newly selected custom plan empty. Refresh the relevant growth row when changing a player's settings and reuse the one-click plan when filtering the comparison.
- Reduce repeated reads and array copies in growth, optimization and restoration. Verify the final payload in grouped edits and continue rollback after an individual recovery read fails. Index group membership once in Undo.

## v1.1.26 (modified full test build, 2026-11-20)

- Added an opt-in PA budget for one-click attribute optimization. Preserve mode distributes additional room by role and existing strengths. The reviewed CA and attribute changes apply and undo together.
- Added restoration of defined player defaults from the installed FM27 database. The source preview identifies the database version and fields set by the game at career creation. The selected early-save source still supplies exact career values.
- Tested the installed Steam 2700 database, exact early-save reads, the PA ceiling and rollback. The public package includes the reader and bundled decoder.

## v1.1.25 (modified full test build, 2026-11-20)

- Budget one-click optimization against current CA with an absolute, uncapped estimate. Account for positional weights, proficiency, both feet, multiple positions and attribute distribution. Show before/after estimates and explain infeasible preserve plans.
- Restore a player's attributes from a user-selected earlier FM27 save. Preview the exact differences. Original CA and hidden attributes/feet are optional. Use the same checked transaction and grouped undo.
- Bundle the x64 Zstandard decoder and third-party notices. Test positional reference results, a saturated CA example, save parsing, player identity, protected values and restore rollback.

## v1.1.24 (modified full test build, 2026-11-20)

- Discover readable clubs with verified compact finances or a verified owned squad, including patched or revived clubs with no players at the start of a save.
- Search the club management, squad and growth lists by full name, short name or UID. Add Oriental Dragon aliases and report full-finance and compact-finance counts in connection diagnostics.
- Require current club and full-finance identity before exposing club management edits. Keep the existing full-finance safety samples, and verify that a fully empty patched roster can be refreshed offline.

## v1.1.23 (modified full test build, 2026-11-19)

- Add a searchable multi-club picker and a dedicated, counted growth queue. Search, squad and club filters act on the displayed candidates while the start action uses the full set of explicitly selected players.
- Show the current playing club, per-player plan and progress in the queue. Support individual removal, retain single-club filtering, and deduplicate loans and overlapping squads by confirmed identity.
- Verify the normal selection, refresh, removal, start and game-date paths on offline club rosters. Read back CA and attributes to compare participants with unchanged players, and check both views at three window sizes.

## v1.1.22 (modified full test build, 2026-11-19)

- Read squad rosters off the UI thread, serialize manual and monthly reads, and retain a consistent roster if a read is canceled or superseded. Preserve current selections and drafts when identity and contract still agree.
- Index players by UID, club, squad and contract owner. Reuse name search keys and repeated name-entry reads, and load the full embedded name bank on demand.
- Process a large CA growth day in short batches of complete player transactions. Keep stop, replay protection, per-player checks and grouped undo in effect. Use elapsed calendar days across year boundaries.
- Avoid rebuilding hidden squad grids and batch the per-player log output. Cover interrupted reads, rebinding, hidden-page refresh and interrupted growth in the offline regression suite. Compare roster refresh and growth responsiveness with 95,000 simulated players in VALIDATION.md.

## v1.1.21 (modified full test build, 2026-11-19)

- Add a Windows Xbox app / PC Game Pass profile for 27.1.0+2505117, identified by engine version, image size, PE timestamp and build marker. Use the matching club, team, finance, date, retirement and feature data with existing object checks. Cite the pinned public Game Pass profile. Verify the Steam 27.1 commercial facility field against the installed engine file.
- Add a current-club roster refresh to the squad and CA growth pages. Reconcile live squads, transfers and loans by UID and carry forward eligible growth selections, settings and progress.
- Check the separate player-person types for players and player-coaches. Reload stale player previews after changes in identity or contract and require confirmation of fresh data before editing.
- Default to preserving current attributes in CA growth and one-click optimization. Protect penalties, corners, free kicks and long throws in automatic growth, low-value support and both optimization modes. Allow explicit set-piece selections in custom growth.
- Pass 29,864 offline assertions including Game Pass profile selection, roster transitions, defaults, set pieces and layout. Live Game Pass attachment and real-game transfer and edit behavior await testing.

## v1.1.20 (modified full build, 2026-11-18)
- Add a separately measured Steam 27.1.0+2505117 native layout (image 0x18878000), preserving the previous Steam layout. Require exact build/size and four independent code anchors; reset selection on reconnect and retain external profiles.
- Use the selected object's types across scan, full finance, medical/status, shirts/tickets, gender, name-region lookup, squads, retirement and eligibility/competition rules. Never substitute compact finance for the full object.
- Bind the current date to a natively referenced packed-date entry and retirement to its typed factory root. Model the new retirement secondary-vtable spacing of 24 bytes separately from the older 16-byte spacing; preserve both indexes, flag handling, identity guards and transactions.
- Preserve per-field facility refusal for zero, out-of-range or unowned values, and per-rule refusal for conditional, ambiguous or inconsistent registration data.
- 29,716 complete offline assertions pass. Static production-source checks match 23 installed-engine RTTI/subobject entries and relevant code instructions. A read-only production scan of a loaded save passes the regular feature-group checks; no game writes or game-window actions. Application/date-advance/save-reload verification remains separate.

## v1.1.19 (modified full build, 2026-11-17)
- For the exact native-verified Steam 27.0.1+2503222 facility adapter, replace the 95% all-five-fields club gate with independently owned and validated facility fields. Retain the previous global gate for other layouts and all core/unmapped refusals.
- Keep zero, out-of-range, missing, unreadable, stale-owner and conflicting-copy fields read-only with explicit reasons; never render an unknown zero as an editable level 1. Require the full finance type before reading corporate facilities.
- Add per-field diagnostic counts, bounded UID examples and a partial-availability connection note. A reported 1,620/1,708 complete-club ratio explains the old threshold failure but does not identify the actual 88 clubs' causes.
- Apply facility edits with stale-data/identity guards, complete final readback, safe failure rollback and atomic grouped undo, preserving unrelated fields and concurrent changes.
- 29,569 offline assertions pass, including a synthetic 1,620-complete/88-partial scenario. Review the facility page at three sizes. No real-game access or writes in this work; actual application and date/save-reload validation remain pending. Preserve all prior features and the unchanged embedded name bank.

## v1.1.18 (modified full build, 2026-11-17)
- Add read-only actual squad/age/loan metadata and per-club filters, including explicit outgoing-loan and separately labelled affiliate views. Preserve the selected queue; validate owner contracts during growth writes.
- Add opt-in, per-player low-value support across foundational physical and role-key technical/mental/keeper fields. Offer editable floors, exclusions, automatic recognition, limited shared budget, distinct preview, per-player batch re-evaluation and grouped undo.
- Split possession and out-of-possession role/position selection and combine their priorities/cost proxies by an adjustable weight. Preserve endpoint and same-profile equivalence; reject goalkeeper/outfield mixtures.
- Add an independent one-click recommended attribute optimizer with preserve and redistribute modes, concrete comparison, explicit target CA bounded by PA, protected fields, stale-preview checks, atomic rollback and grouped undo. The proxy is not the game's unpublished RCA formula or a claim of a unique optimum.
- 29,515 offline assertions pass, plus synthetic offscreen UI review, independent corresponding-source rebuild and portable embedded-resource checks. No real-game access or writes in this work; in-game/date/save-reload validation remains pending.


## v1.1.17 (modified full build, 2026-11-16)
- Fix monthly refresh auto-selecting the entire squad. Rebind only explicit watched UIDs, preserving per-player options and progress; new signings and unchecked players stay unchecked.
- Add independent per-player role, selected-attribute and CA-only modes. Offer all supported public attribute checkboxes, a speed shortcut, explicit empty-mask refusal, preview and preflighted apply-to-watched. Bound narrow masks to their own proportional budget as well as whole-player headroom.
- Add 10 position groups with 10 basic and 88 detailed possession-phase role presets. Label the FM26 reference season and distinguish presets from official FM27 ability weights or automatic tactic detection.
- Reorganize all ten main pages, add player/staff attribute filters, separate club finance/facilities/ticket forms, simplify squad commands, growth inspector and readable history/freeze controls.
- Unify personal eligibility, domestic/UEFA registration and competition quotas/home-grown requirements. Test common registration across countries; retain Italy-only verification for the independent historical non-EU flag and explain other-country read-only scope.
- 26,766 offline assertions pass, plus synthetic offscreen layout review and independent source/portable-resource checks. No real game access or writes in this work; application/date-advance/save-reload verification remains pending.

## v1.1.16 (modified full build, 2026-11-16)
- Add default key-attribute growth with nine position directions, an automatic familiarity-based selection and a read-only preview; retain the original CA-only mode and growth gears/curve.
- Use a baseline-calibrated development proxy, account for current natural gains and preserve fractional attribute values. Enforce CA/PA and attribute bounds without changing feet, positions or hidden attributes. The proxy is an estimate, not a guarantee of FM's later recalculation.
- Guard each paired CA/attribute transaction with identity, team, positions, PA, complete readback and date checks. Check the final transaction state; support a separate ownership guard for safe rollback of known writes when unrelated data changes.
- Group growth undo atomically and stop ongoing growth before undo. Refuse the whole undo on stale data; roll back a failed undo. Pause on a backward game date or unrecoverable state.
- 14,058 offline assertions pass, including 1,311 new growth checks. No game process access, game writes or window automation were used for this feature; in-game application and persistence remain unverified.

## v1.1.15 (modified full build, 2026-11-15)
- Complete native-verified current-build status/morale, injury removal, fatigue, own/loan shirt numbers, and ticket/season-ticket editing. Use the correct one-byte morale field and shared read/write/freeze codec; preserve neighbouring state.
- Validate medical ownership and complete injury records before reversible list removal. Resolve shirt contracts by club ownership, handle reserves and empty loan holders, and preserve the parent contract number.
- Treat ticket price -1 as unset, display it clearly, and support guarded setting with transactional rollback and undo. Retain existing ranges and object-level refusal.
- Separate unmapped limitations from failed checks in Chinese; routine current limitations no longer repeatedly look like an attachment failure. New and core failures still alert.
- 12,747 local assertions pass. Same-source read-only production checks and copied-data previews pass for all five classes, with no disabled groups in the measured scan and no game writes/window actions. Actual user writes and save/reload remain pending.

## v1.1.14 (modified full build, 2026-11-15)
- Add a native-signature and image-size gated Steam 27.0.1+2503222 adapter for actual contract, full-finance, club, person/staff and competition types. Keep custom profiles and existing feature checks.
- Read the date and retirement managers from verified roots, independent of listed-player anchors and heap calibration votes. Use the validated date in age, retirement, transfer and growth paths; check the date again before retirement writes.
- Enable the current retirement layout only after native verification, preserving both-index, announcement, identity, transaction and undo checks. Restore current-build gender classification with club cross-checks; correct youth coaching/recruitment field sources.
- 12,700 local assertions pass. Read-only production checks cover contracts, finance/facilities and dates. Retirement cancel/postpone previews pass on copied data without changing the game; actual application and persistence require separate verification.

## v1.1.13 (modified full build, 2026-11-15)
- Reject ambiguous multiple fm.exe instances and an absent game_plugin.dll instead of silently taking the first process or scanning the launcher.
- Include editor version, target/profile context, native read and region-query failures, candidate counts and actual identity samples in failed-scan diagnostics. Keep summaries bounded; retain full details in the existing copyable report.
- Preserve native error codes with SetLastError, explain skipped sampling and the informational build-list notice. Reset failed attachment reports and stop previous freezes/CA growth when reconnecting.
- Keep existing scan layouts, thresholds, candidate ordering, feature checks and readonly protection. 12,678 local assertions pass; the forum reporter supplied only a screenshot and their failure has not been reproduced or claimed fixed. No game process access, game writes or window automation were used for this update.

## v1.1.12 (modified full build, 2026-11-15)
- Reuse the user-authorized same-project managed person-name rules and complete game-name corpus. Embed 506,169 component rows, 91,482 contextual rows and 251 regional language rows, retaining the 5,270 sourced person records as first priority.
- Fill missing player/staff names through contextual matches, role-aware components and explicitly labelled phonetic approximations; preserve original English and UID searches. Prefer existing full Chinese aliases over Latin-initial displays and retain the old abbreviations as searchable aliases.
- Carry raw name parts and validated origin IDs through scans. Cache normalized lookup strings and invalidate them on original/Chinese/alias changes. Do not store approximate outputs as verified age-edit identities.
- 12,662 local assertions pass. Name completion and bilingual search checks cover sourced matches, components and labelled approximations. These do not establish official-name accuracy for every person or future save. No game writes or window automation were performed.

## v1.1.11 (modified build, 2026-11-15)
- Add a standalone competition registration dialog, independent of player selection and prize-table availability; retain all ten existing pages and the personal Italian-status repair.
- Expand to 18 recognized maximum/minimum rule types: foreign/non-EU starting, bench, match and registration limits; total squad size; non-homegrown capacity; same-nation and club/nation-trained minima. Require the correct one-byte direction and all three equal runtime/serialized values.
- Show actual loaded rules and conditional branches, searchable league aliases and exact UIDs. Derive non-homegrown capacity from compatible loaded total/homegrown values without hardcoding 17. Quick relaxation only fills a preview; zero minima and zero maximum allowances have different meanings, and total capacity is strictly positive.
- 12,561 local assertions pass. Read-only production previews cover 18 rules across the five major leagues and selected second tiers, with French conditional branches refused. Unshown controls and offline layouts were checked; no game writes or game-window automation were performed. Actual application, match eligibility and persistence remain user-test pending for the new league rules.

## v1.1.10 (modified build, 2026-11-15)
- Add a separate registration/eligibility dialog under More actions → Personal details and contract. Clear only the verified Italian non-EU status bit while preserving nationalities and existing registration records.
- Repair current-team Domestic or UEFA registration records in verified existing capacity. Preserve unknown/other-team/other-type rows; refuse unverified allocations, player-coaches and mismatched contract teams.
- Edit five recognized unconditional foreign/non-EU maximum rule types, including squad, match-squad and annual foreign-signing limits. Discover rules from loaded teams, require matching runtime and two serialized copies, and disclose identified shared competitions. Zero means no places, not unlimited.
- Gate the new writer on Steam 27.0.1+2503222, module size and a native check signature. Revalidate identity, class, ownership, date and preview; read back writes, attempt guarded rollback and support grouped undo while guards remain valid.
- 12,470 local assertions pass. Registration structures and previews were checked read-only. Personal non-EU repair has positive registration feedback; match eligibility and save/reload persistence remain unverified. No private save cases are included in the public documentation.

## v1.1.9 (modified build, 2026-11-12)
- Add sourced Benzema short/full Chinese and English name records. A verified FM UID plus original name survives age edits; the UID-free full-name fallback requires the original birthday. A surname alone never establishes an unverified identity.
- Expand the embedded catalog from 3,491 to 5,270 source records: retain the 96 five-league directories and add 16 Chinese Super League teams, 48 national teams and 16 other European clubs. Keep supplementary birthdays mandatory and CCTV IDs separate from FM UIDs.
- Retain both full published token orders for supplementary English names, including inconsistent Chinese pinyin order. Preserve uniqueness and birthday checks.
- Update the offline coverage view, reproducible public snapshots and provenance auditor. Explicitly list 176 pending directory entries instead of generating unsourced Chinese names.
- 11,143 local assertions pass, including every directory identity, all supplementary English orders, Benzema age-edit regressions, duplicate source merging and ambiguous/missing-birthday guards. No game process, game data or game windows were used.

## v1.1.8 (modified build, 2026-11-12)
- Reorganize the native UI with grouped left navigation, a compact connection header and status bar, four common player actions plus categorized advanced commands, and optional raw attribute values. Keep all ten pages and original command handlers. Add a copyable current-scan report and suppress routine partial-support popups.
- Add exact player/staff UID search, including #UID, UID: prefixes and full-width keyboard input.
- Decouple player mapping recovery from club, staff, city, manager and stadium counts. Keep per-feature safety checks; preserve a verified player mapping if a forced recovery fails. Do not claim that a failed calibration succeeded.
- Record engine/profile, memory scan totals, type hits, rejected player blocks/names, and gender-filter counts for zero-player reports. No country or league whitelist is introduced.
- Allow display-currency configuration before game attachment: common symbols including RMB, manual positive finite conversion rates, persistent settings and optional validated club-balance calibration. Refresh loaded staff and prize amounts as well as players and club finances.
- Parse Chinese money units, preserve conversion precision, reject malformed/nonfinite values, and surface settings save errors.
- 4,151 local assertions pass. No live game writes or window automation were used to validate these code changes; the forum reporter's league patch has not been reproduced.

## v1.1.7 (modified build, 2026-11-12)
- Fix retirement cancellation leaving the person's announced-retirement flag set after removing the pending record. Clear only person+0x18 bit 0; keep every other person flag, birthday and contract expiry unchanged.
- Detect and repair the residual announcement on active players whose retirement record was already removed with an older editor. Reuse identity checks, readback, rollback and grouped undo. Postponement retains the announcement flag.
- Recheck the announcement byte before applying a retirement change. Handle retirement plans at the maximum supported date without opening an invalid date picker.
- 4,078 local regression assertions pass, including legacy-state repair, exact undo and rollback when the flag write fails. See VALIDATION.md for live-game verification and limits.

## v1.1.6 (modified build)
- Expand the offline Chinese/English name catalog to 96 teams in the public CCTV directories for England, Spain, Italy, Germany and France. Of 3,415 distinct directory identities, 3,385 have sourced Chinese names and 30 remain explicitly pending. Keep the original 106 records; the embedded catalog now has 3,491 source records.
- Retain Chinese short/full names and all English forms; cross-check 105 published common spellings for 104 people against CCTV/Xinhua reporting and official Chinese club material. Add per-league/per-team coverage and pending-name views to the app and package.
- Normalize non-decomposing Latin letters such as Ø, Ł and ß; safely recognize extra middle names only with the same birthday, complete first/last tokens, ordered full tokens and a unique person. Support published one-character Chinese aliases by exact match.
- Preserve aliases when multiple records share the same preferred Chinese name, and retain a valid single source URL when alternate translations merge.
- Include the reduced public data snapshot, preferred-spelling evidence and offline generator/auditor in the corresponding source archive. CCTV directory IDs are never used as FM person UIDs.
- 4,058 regression assertions and the independent provenance/coverage audit pass. Name-catalog and coverage windows were checked without attaching to FM. This round does not measure the name match rate of the user's active save.

## v1.1.5 (modified build)
- Hide verified women's player records consistently from all player lists, including free agents and player-coaches; hide women's clubs in club selectors. Read-only identity/club cross-checks are required, and the mapping is enabled only for verified 27.0.1+2494687.
- Add shared age/date-of-birth controls to Players and Staff, using the current game date, preview, preserved FM date flags, guarded writes and undo.
- Keep verified bilingual identity bindings when birthdays are edited; rank exact names/aliases before substring matches.
- Fix clipped player identity/status text and translated staff-job reverse lookup.
- 651 local regression assertions pass. See VALIDATION.md for live evidence and limits.

## v1.1.4 (modified build)
- Extend Chinese labels, traits, appearance options, clauses, dynamic messages and diagnostics.
- Add normalized bilingual player search, a name filter to Find players, sourced offline names and importable TSV name extensions. The bundled catalog has 106 records with source URLs.
- Add guarded cancellation/postponement of announced retirement plans on 27.0.1+2494687; update both retirement indexes and group undo records. The user verified postponement and cancellation in FM.
- Preserve the existing CA growth and roster-refresh behavior; fix the Find players free-agent checkbox so unchecked means all players.

## v1.1.3 (modified build, not the original FM27€0)
Two changes on top of v1.1.2: CA growth time now depends on each player's own gap, and the roster is read again once per in-game month.

### Per-player growth time and nine speed gears (v1.1.3)
- The old model gave every watched player the same fixed span (the chosen season count), so a player only 5 CA short of his PA still dribbled that point over two seasons. A player's span now depends only on his gap: `span = max(21, round(gap x K))`, with a 21-day floor and no upper cap
- The speed is picked from nine gears, **2x to 10x**, in a dropdown that replaces the old season-count box. The 1x natural baseline is K = 40 days per CA point, so a gear m gives K = 40 / m: 2x -> 20, 3x -> 13.3, 4x -> 10, 5x -> 8, 6x -> 6.7, 7x -> 5.7, 8x -> 5, 9x -> 4.4, 10x -> 4 days per point. The default is **4x** (K = 10)
- The natural curve f(t) = t*(2-t) is unchanged, now run over each player's own span: quick at the start, slower and slower as he reaches his PA. A small gap is closed in weeks; a large gap can take longer than two seasons on the low gears
- There is no age factor at all: a veteran and a youngster follow the same rule, so a veteran can be brought back to his own PA. The span is fixed when growth starts and is not recomputed while the run is alive
- The previous slim build (only the CA growth tab) was removed; the build always carries all ten tabs

### Roster auto-refresh (v1.1.3)
- A club's roster came from the scan, so a player signed after it never showed up in the table until a manual scan. The editor now reads the memory again on its own whenever the game date moves into a new month; this is always on, there is no switch in the UI and nothing is saved for it
- After a refresh the whole squad is ticked automatically: every player on the new roster, including a player signed that month, is added to the watch, so a new signing joins the growth without a manual tick
- The button **Watch squad** is now **Tick squad** (勾选全队) and the start/stop button reads **Start growth** / **Stop growth** (开始成长 / 停止成长)
- A successful refresh is silent: no status bar announcement before or after it. A failed refresh still shows a warning, since the roster did not update
- The **Status** column no longer says "growing" for a player who is only ticked: it reads **Queued** (待开始) until growth starts, then **Growing** (成长中), and **Done** (已完成) at PA
- While growth runs the control row is locked: the tick-squad/cancel buttons, the gear dropdown and the fill button are greyed and the watch check column is read-only, so the roster and the pace cannot be changed mid-run. The start button stays enabled and reads **Stop growth**, and stopping restores every control
- The **Grow one day** button was removed: it duplicated a single day of the daily run, so the row now keeps Tick squad, Cancel, the gear, Fill to PA and Start/Stop
- The second pass runs on the attached process without re-attaching, so a running daily growth and the freezes keep going; it only replaces the player, club and staff lists
- The watched players are rebound by their UID onto their new Player objects, so CA progress, the elapsed days and the added amount survive the refresh
- Players who left the club, retired or cannot be found drop out of the watch (the Change log line says how many); the club list selection is put back on the club that was open
- A month that jumps forward (a holiday across a month border) still triggers exactly one refresh; loading an older save re-anchors instead of scanning
- If the second pass yields no usable result, nothing is replaced and the status line says so

## v1.1.2 (modified build, not the original FM27€0)
Modified build of v1.1.0 by Anze Alexander. CA growth and the Chinese interface are the only additions; everything else is unchanged. The interface text is Chinese, this file and the README stay in English.

### CA growth: natural growth over one or more seasons (v1.1.2)
- Watched players no longer rise by a fixed amount per day. Growth follows a natural curve: it starts the day you press **开始每日成长**, runs over a chosen number of seasons (1 to 5, default 2, one season = 365 in-game days) and he is exactly at his PA when that time is up
- The curve is f(t) = t*(2-t): about three quarters of the gap in the first half of the run, then slower and slower towards the ceiling, which is what FM's own development of a youngster looks like
- The **进度** column shows "elapsed/span 天" or "已到 PA"; changing the season count re-stretches every watched player at once
- **直接拉到 PA** closes the whole gap at once after a confirmation
- **开始每日成长** pays once per in-game day off the game date the editor already reads; a long skip (for example a holiday) pays for up to a whole season in one write. Growth stops on its own if the date stops being safe to read, after a new scan (the old player objects are gone), or when every watched player is at his PA
- Nothing ever writes past PA. If FM itself moved a player further than the curve already asked for, that day is skipped instead of pulling him back
- Negative PA is a random range: growth stops at the top of the range the PA column shows, with a tooltip on the PA cell
- Growth reuses the existing CA/PA write, so every step goes through the same guards, safety check and Change log undo as a hand edit

### Chinese interface (v1.1.2)
- Every page, tab, column header, button, label and search hint of the main window is Chinese (FM's own Chinese terms are used for the 54 player attributes, 23 staff attributes, 15 positions, squad statuses and staff jobs)
- The 8 sub-dialogs (details, clauses, sponsors, swap, move, release, create player, currency) are translated as they open
- Attribute rows, position rows, status rows, money rows and the small numeric rows of the players and staff pages are Chinese, including the "verified / personality" notes
- The most common error and guard messages are Chinese; a few rarer message boxes and the Change log text are still English

### labelling
- Window title, assembly properties, the exe name (FM27E0-CAgrowth v1.1.2.exe) and this file all say this is a modified build; the "Original by Anze Alexander" credit and the link to the original download page stay in the window. The "Support on Ko-fi" donation button the original window carried was removed: the additional terms ask for the credit and the download link, not for the donation link. The Ko-fi mention in the README belongs to the author and is untouched

## v1.1.1 (modified build, not the original FM27€0)
- First version of the CA growth tab: a fixed amount of CA per in-game day (superseded by the natural curve in v1.1.2)

## v1.1.0
- Xbox / PC Game Pass version of FM27: its own built-in profile, picked automatically (same version number as Steam but a different build). Tested in game: players, player-coaches, staff, clubs and prize money found
- Auto-mapper after game updates now also finds player-coaches without a contract, competitions, competition rules, this season's prize money tables and the game date (from the game's code, where FM27's date stands out). Tested by running the Steam profile against the Xbox build: everything found
- Moving staff to another club works in FM27: besides the contract, FM27 keeps each club's staff in department lists (medical, coaching, recruitment), and the move now takes him out of the old club's list and into the new one. FM shows him at the new club after the next day, in the same job, with a new line in his staff history (tested in game: a scout Barcelona -> R. Madrid and a coach Barcelona -> Girona). The new club needs a free place in that department's list; the editor says so when there is none. Moving and swapping players also tested in FM27
- "Access denied" when attaching now says what to do: run FM27€0 as administrator (needed when FM or Steam runs as administrator)
- The credit link in the editor, the README and ADDITIONAL-TERMS now point at the FM27€0 download page on sortitoutsi
## v1.0.0
- FM27€0: a separate editor for Football Manager 27, based on FM24€0 v1.28.1. It attaches to FM27's game engine (game_plugin.dll) and has an FM27 profile of its own
- Mapped for FM27 and tested in game: player attributes, positions, CA/PA, personality, traits, names, nationality and other nationalities, place of birth, height (FM27 has no weight), caps (now two-byte caps and goals), reputation, asking price, transfer listing, contracts (wage, expiry, squad status, squad number, loan squad number, bonuses and clauses), staff attributes, wages, jobs and reputation, the manager's attributes and contract, club balance and budgets, facilities, club reputation, stadium capacity, season tickets and ticket price, sponsorship deals, club names, swapping players (and undo)
- Player-coaches are listed with the players
- Staff attributes follow FM27's names: Possession (was Mental coaching) and Authority (was Level of Discipline); FM27 shows one Goalkeeping rating, the average of the three goalkeeping values
- Sponsorship types Shirt Sleeve and Kit Manufacturer
- Money is shown at FM27's rate, 1 £ = 1.3476 $
- League prize money (tested in game: League One 1st place shown and changed). FM27 pays this season from its own copy of the table, which the editor changes too; after a save is loaded most leagues only have that copy, and it is listed as "this season"
- Not mapped, so read-only: injuries, condition, sharpness and morale, jadedness, cup prize money. Their buttons are switched off
- Healing injuries is skipped when injuries are read-only (Create player could call it anyway)
- After a game update the editor finds the players, staff, clubs, contracts, finances, manager, loan deals and stadiums again by itself (tested with all eleven wrong); the game date and prize money tables go read-only instead
- The attach message lists exactly what is not mapped
