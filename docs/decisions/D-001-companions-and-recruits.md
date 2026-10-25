# D-001: Companions and recruits (0.9.5.3-decomped.1)

Status: locked 2026-10-25. Changing anything here is a new decision, not an implementation detail.

## Version

- `AssemblyInformationalVersion` becomes `0.9.5.3-decomped.1`. That is what `Theme.ModVersion` shows
  in game, and the change makes the What's New page appear once.
- `AssemblyVersion` and `AssemblyFileVersion` stay `0.9.5.3`. `CheckOtherCopies` compares
  `AssemblyVersion`, so a newer official enRAGE still wins over this build.
- Git tag `v0.9.5.3-decomped.1`, made only after the in-game acceptance test passes.

## Camp companions ("Ride With Me")

- The author's code in `NemesisMod.Companions.cs` and `NemesisMod.CompanionIdle.cs` is reused as is.
  Its per-tick dispatcher was compiled away: `CompanionsAvailable` was a `const false`, so the C#
  compiler dropped every branch it guarded. The dispatcher is rewritten in `UpdateCompanion`.
- It is switched by a new INI key, `[Companions] Enabled`, default `true`. When it is off, the old
  behaviour stays: send everyone home once, hide the prompt.
- The per-tick order inside `UpdateCompanion`, when enabled:
  1. track `_youOffSince` (when the player left his mount, or -1 while mounted);
  2. `UpdateAsk`;
  3. `MissionGiverNear`;
  4. each companion by `State`: `0` → `WaitToJoin`, `1` → `Ride`, `2` → `GoingHome`, iterating over a
     copy because these remove from `_comps`;
  5. `Banter`, then `MaskFollow`.
- The cap stays `MaxCompanions = 3`. It counts recruits too.

## Recruits (persistent)

- **Storage.** A recruited man's `NemesisRecord` leaves `Roster` through `RemoveRecord(rec, ...)` and
  moves into a new `WorldState.Recruits` list, which is saved in `world.json`. He is never in
  `Roster` while recruited, so no nemesis system (ambushes, wars, succession, town presence) can spawn
  him as an enemy. `rec.Gang` keeps his old gang's id as history ("formerly of"); nothing reads a
  recruit's gang as membership because he is not in `Roster`. Old saves without the field load with no recruits. The property defaults to
  `null` and is created on first use, the same pattern as `CompanionHurt` and `CompanionNever`.
- **Offer.** The offer happens in `SparedHim`, which all three spare paths already call: a standoff
  surrender, "let go after questioning" (`HoldUp`), and a nerve break. The roll is
  `RecruitRules.OfferChance(...)`, a pure static function with no RDR2 dependency, unit-tested:
  - base by personality: Coward 45, Drifter 40, Greedy 30, none/other 20, Cold 15, Hothead 8, Loyal 5,
    Proud 3;
  - plus `min(20, fear / 5)`;
  - minus `hate / 4`, with hate clamped to 0..100;
  - halved if he is an officer (`Rank >= 4`) or his gang is `OrdersNotFeelings` (law/army);
  - multiplied by `[Recruits] ChancePercent / 100`, then clamped to 0..75;
  - **0** if: `Rank >= 5`, a story/lore record (`Id` starts with `lore_`, `fallen_`, `vdl_`, or the gang
    is `StoryOnly`), he has an `Avenge` goal targeting `"you"`, recruits are disabled, or
    `WorldState.Recruits.Count >= [Recruits] MaxKept`.
- **Prompt.** A successful roll opens a 60-second window. In it, a held "Ride With Me" prompt shows on
  his ped within 3 m, using the same prompt machinery as `UpdateAsk`. If you take it, he is
  recruited. If the window lapses, nothing changes and `SparedHim`'s existing leave logic runs as it
  does today. When he is recruited, any pending `Leaving` entry for him is removed.
- **Recruiting him.** His record moves to `WorldState.Recruits`, and his live ped becomes a
  `Companion`:
  - `Key = "rec:" + rec.Id`, `Rec = rec`, `IsReal = false` (his ped is already mod-owned);
  - no camp, so the "back at camp" send-home branch of `Ride` is skipped for recruits;
  - the 3-rider cap applies. If 3 already ride, he is still recruited but goes home now ("I'll be
    around when you need me").
- **His old gang.** A `WorldEvent` of kind `LIFE`, "<name> turned - rides with Arthur Morgan now",
  flagged `Heard`. Every roster member with a tie to him gets a thought, the same way
  `WalkAway` adds `Homesick`. `GrudgeNotice` and `Feed` lines are added. No new thought types unless
  `Mind` already accepts arbitrary ones.
- **Calling him back and sending him home.** These go through `NemesisMod` methods
  `SendForRecruit(rec)`, `SendRecruitHome(rec)` and `LetRecruitGo(rec)`, the list
  `RecruitsKept()`, and `RecruitStatus(rec)`, which returns "Riding", "Laid up (Nh)" or "Waiting".
  - Sending for him spawns his ped out of sight (`OutOfSightSpot`, `SpawnNemesisPed` with his record's
    model and outfit) and runs `Setup`.
  - Sending him home is the companions' `SendHome`. The record stays in `Recruits`.
  - Letting him go removes him from `Recruits` for good and adds a `LIFE` event. He does not go back
    to `Roster`.
- **Hurt.** The companion rule: under 30% health he leaves and is laid up for one game day
  (`CompHurt["rec:" + id]`). Sending for him while laid up refuses with a line.
- **Menu.** The F11 menu gets a "Your Men" page listing `RecruitsKept()`: name, old gang, laid up or
  riding. Its actions are Send For Him / Send Home / Let Him Go (confirm twice, like Forget).
- **Settings** (`[Recruits]`): `Enabled` (default `true`), `MaxKept` (default `6`), `ChancePercent`
  (default `100`).

## Out of scope for this version

- Your own gang: hiring, holding camps, wars on your side.
- Recruits fighting in the mod's wars, or being hunted by their old gang.
- A "Talk" prompt for companions. `HideTalkPrompt` stays; no new talk feature.

## Amendments after the in-game test (2026-10-25)

All of these were asked for, or approved, by the maintainer during the test.

- **The offer window waits while he is tied.** It starts when he is free, capped at 10 minutes.
  While an offer is open, his `Leaving` entry is held (his kin's and friend's too), and
  `OffStage` treats him as on stage.
- **No recruit prompt during story missions.** Open offers end when a mission starts.
- **Companion threats are people only.** A threat is a human within 100 m of you or of a
  riding companion who is not a companion, not carried (riders and wagon passengers still count), is in combat with the player or with a riding companion, and is not tied, writhing,
  spared, surrendered, being questioned, or offering. Companions stand down 6 s after the last
  threat. "Take your target" accepts the mod's own men only while they are fighting you or a
  companion, and a companion drops his target as soon as the man stops being fightable.
- **Debug "Recruit Now"** on the F11 card, with `[Debug] Enabled` and the overlay on, for men
  who are off stage and below rank 5.
- **Cowards are slower to swear revenge:** +25 anger (`Mind.AvengeLine`). This is a deliberate
  change to the original enRAGE, asked for by the maintainer.
