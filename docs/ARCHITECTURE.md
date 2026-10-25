# How enRAGE is put together

A map of the reconstructed source, for finding where a behaviour lives and how to add one.
Everything here was read from the decompiled code. Where it says "the author", that is an
inference about intent, not something the code states.

## The runtime

enRAGE is a single [ScriptHookRDR2DotNet V2](https://github.com/Halen84/ScriptHookRDR2DotNet-V2)
script, `NemesisSystem.NemesisMod : RDR2.Script`, built for .NET Framework 4.8 x64 against
`ScriptHookRDRNetAPI` 2.2.0. Script Hook .NET loads every DLL in `<game>\scripts\` and creates
one instance of each `Script` subclass it finds.

| Hook | Where | What it does |
|---|---|---|
| constructor | `NemesisMod/NemesisMod.cs` `NemesisMod()` | Finds the data folder, loads `NemesisMod.ini` + `state.ini` into `Config`, builds `RosterMenu`, `IniPanel` (Alt+F11), `WelcomeGuide`, `BulletImpactModule`, loads the roster, people, world and content, then wires the events below. `Interval = 0` (every frame). |
| `Tick` | `OnTick` in `NemesisMod.cs` | The main loop, wrapped in `Guard("tick", …)` and skipped unless `ActiveCopy()` (a second copy of the DLL stays dormant). It calls ~60 `Update*` subsystems; see the next section. |
| `KeyDown` / `KeyUp` | queued into `_keyQueue`, drained by `DrainKeys` (`NemesisMod.Input.cs`) on the tick | Menu keys, interrogation, telegraph, debug keys. Key bindings come from `Config` (`KeyCombo`). Gamepad input is polled through XInput (`XPad`, `NemesisMod.Gamepad.cs`). |
| `Aborted` | `OnAborted` (`NemesisMod.Menu.cs`) | Script reload (Insert) or game exit: flushes saves and cleans up spawned peds, blips and prompts. |

`Guard(name, action)` (`NemesisMod.Records.cs`) runs a step and logs exceptions instead of
letting one subsystem kill the script. `Crumbs` / `StartupTrace` write `lastframe.txt`,
`startup.log` and `lastcrash.txt`, so a crash says what the mod was doing.

### What one tick runs

`OnTick` calls these in this order (abridged). Each throttles itself with a `_nextXCheck`
game-time stamp, so most do nothing on most frames:

```
UpdateCalm  UpdateSavePoints  UpdateMenuPause  UpdateSaloonFight  PollInteractionEvents
UpdateGrudges  UpdateWorldNemeses  UpdateGeneral  UpdateRegion  UpdateTheft  UpdateNaming
UpdateTownSightings  UpdateCamps  UpdateTreasuries  UpdateFairGame  UpdateSupplyCamps
UpdateSupplyWagons  UpdateSelfCheck  UpdateTrainRobbery  UpdateRaids  UpdateAftermath
UpdatePeople  UpdateUnmasking  UpdateGroupWatch  UpdateGreetScenes  UpdateVoice*  UpdateFightOver
UpdateEnemyDots  RunSpawnMenuRequests  UpdateInterrogation  UpdateSocial  UpdateGestureSystems
RunRandomEncounter  UpdateTension  UpdateEncounter  UpdateConvo  UpdateTelegraph  UpdateGameSystems
UpdateLeaders  UpdateCompanion  UpdateStoryProgress  UpdateOutlawGroups  UpdateSkirmish  UpdateBands
UpdateCheckpoint  UpdateRoadWagon  UpdateDistantBattle  UpdateLeaving  UpdateHunters  UpdateRival
UpdateWorldSim  UpdatePlans  UpdateMissionDeeds  DrawNameCards  DrawWarHud  DrawDebug
```

To add a per-frame system: write `UpdateYourThing(Ped player)` in a new partial file, throttle
it the same way, and call it from `OnTick`.

## The source tree

```
src/Properties/AssemblyInfo.cs      original assembly attributes (version 0.9.5.3)
src/NemesisSystem/                  ~150 top-level types: data model, content, UI, AI
src/NemesisSystem/BulletImpact/     the X-ray / wound system (its own module)
src/NemesisSystem/NemesisMod/       the NemesisMod class, as ~150 partial files by feature
Resources/doors.json                embedded built-in door table
```

### The `NemesisMod` class (one class, ~90k lines, split by feature)

The shipped DLL has a single `NemesisMod` class. The author evidently wrote it as alphabetically
named partial files: the DLL preserves their order, and that is the order they appear in below.
`docs/RECONSTRUCTION.md` explains how the split was recovered. File names are
`NemesisMod.<Topic>.cs`, and `NemesisMod.cs` holds the core (constructor, `OnTick`, data
folder, reset).

| Area | Files (`NemesisMod.*.cs`) |
|---|---|
| Core, input, menus, logging | `NemesisMod.cs` (core), Input, Gamepad, Menu, Notices, Records, RosterIO, Debug, SelfCheck, Copies, Entities, Search, SpawnMenu, TestCamps, LawTest |
| Nemeses: grudges, hate, rank, memory | Grudges, GrudgeEvents, GrudgeSocial, Hate, Rank, Casualties, Minds, History, Timeline, Routines, DeathNews, Feed, Fingerprints |
| Identity and recognition | Names, Naming, NameCards, Disguise, Unmasking, Sightings, Witnesses, Crime, Reputation, TownReputation |
| People and social life | People, Friends, PenPals, Rivals, Children, Companions, CompanionIdle, Social, Gestures, GreetScenes, Voices, Seating, NaturalSpots, Unease, Spared, Runners |
| Encounters and fights | Ambush, Encounters, EncounterSpawn, EncounterStandoff, EncounterCamp, EnemyDots, Bands, Nerve, FightReactions, Fistfights, FairGame, Skirmishes, Aftermath, Survivors, HisPeople, HoldUp, Calm, Tension, Escalation, Theft, GoodDeeds, Roadside, RoadAmbush, Hidden, Klan, Saloons |
| Interrogation and talk | InterrogationTargets, Interrogation, InterrogationTalk, Questions, Convo, Telegraph |
| Factions, leaders, war | Factions, Affiliation, Agenda, Army, ArmyGeneral, Patrols, PatrolScenes, OutlawGroups, OurPeds, Groups, Manpower, Leaders, LeaderCopies, LeaderFates, LoreMen, Succession, Relations, Grievances, WarDeclarations, Wars, WarCards, WarHeat, WarBattles, WarNights, WarReports, NightShelling, Territory, Sandbox |
| Camps, holdings, economy | Camps, CampSites, CampTakeovers, CampDoors, CampObjectives, CampRegard, Holdings, Supplies, SupplyWagons, TownCrates, Treasury, StashSpots, Checkpoint, Wagons, Trains |
| Towns and ceremonies | TownPresence, TownVisits, Funerals, Procession, RideIn, Rest |
| Story, era, content | Story, Era, Epilogue, VanDerLinde, Cards, Content, ContentFactions |
| World simulation and saves | World, WorldSim, Plans, SavePoints, SaveSlices |
| Presentation | Newspaper, MapIcons, GameSystems (weather, music) |

Nested types of `NemesisMod` (`Band`, `LiveCamp`, `Interrogation`, `TownMan`, …) sit at the
top of the file that uses them.

### Top-level types worth knowing

| Type | Role |
|---|---|
| `NemesisRecord` | One remembered person or nemesis: identity, gang, rank, hate, traits, scars, past. The roster (`nemeses.json`) is a list of these. |
| `NemesisSettings` (`Config`) | Every setting. It reads and writes `<game>\scripts\NemesisMod.ini` (user settings) and `state.ini` (bookkeeping). `Layout()` declares the settings and their sections, so a new setting goes there. |
| `Balance` | Tuning multipliers and odds (hate gain, cooling, fear, interrogation bases…), loaded from the `[Balance]` INI section. |
| `Gang` / `Gangs` | Faction definitions (ids, names, districts, tiers, seed troops, relations). Built-in data, overridable from `content/factions.json`. |
| `RosterMenu` | The F11 menu (Hierarchy, World, X-ray tabs and the detail card). 4.8k lines, the next candidate for splitting. |
| `IniPanel` | Alt+F11 live settings editor. |
| `WelcomeGuide`, `WelcomePics`, `Theme`, `UiDraw`, `UiMouse`, `UiFeed` | Drawing, fonts, colours, mouse, the welcome guide. |
| `Mind`, `Traits`, `Personalities`, `Quirks`, `Tempers`, `Backstory` | Per-person psychology and life story. |
| `Holdings`, `CampKit`, `Camps`, `CampSite` | Who holds what: camps, supply caches, treasuries. |
| `BulletImpact/*` | The X-ray: hit detection (`ImpactDetector`), anatomy, wound analysis, crippling (`CrippleTracker`), rendering and the viewer. Has its own `BulletImpact.ini`. |
| `SafeFile`, `SlicedJson`, `IniFile` | Crash-safe writes, large JSON saved in slices, INI I/O. |
| `ApiCheck` | At startup, checks that every Script Hook API member the mod uses exists in the installed `ScriptHookRDRNetAPI`. |

## Files on disk

| Where | What |
|---|---|
| `<game>\scripts\NemesisMod.ini` | Settings, written on first run. Sections: Ambush, CheatDeath, Grudges, Followers, Sightings, Gangs, Calm, People, Telegraph, SavePoints, World, Balance, Fear, Intent, Factions, Naming, Disguise, GrudgeScore, Menu, Tension, Encounters, Camps, Keys, Interrogation, Debug, GameSystems, Meta. |
| `<game>\scripts\BulletImpact.ini` | X-ray settings. |
| `Documents\Rockstar Games\Red Dead Redemption 2\enRAGE\` | The data folder. Under Proton: `steamapps/compatdata/1174180/pfx/drive_c/users/steamuser/Documents/…`. Holds `nemeses.json`, `people.json`, `world.json`, `fingerprints2.json`, `state.ini`, `savepoints/` (one world per game save), the logs (`grudges.log`, `startup.log`, `lastframe.txt`) and `content/`. |
| `…\enRAGE\content\*.json` | Editable content: `factions.json`, `lives.json`, `places.json`, `questions.json`, `tales.json`, `camps.json`, `doors.json`, `encounters/`. The mod writes the built-in version on first run (`*.builtin` records which build wrote it) and reads your edits back. |

## Extension points, cheapest first

1. **Settings**: `NemesisMod.ini` or Alt+F11. No code.
2. **Content JSON** in `content/`: factions, names, life events, places, interrogation
   questions, tales, camps, encounters. No code. The loaders are in `NemesisMod.Content.cs`,
   `NemesisMod.ContentFactions.cs` and the `*Content` / `*Row` types.
3. **A new setting**: add it to `NemesisSettings.Layout()` and read it from `Config`.
4. **A new subsystem**: add a `NemesisMod.YourThing.cs` partial, call it from `OnTick`, and keep
   its state in fields in that file. If it should survive a reload, persist it the way
   `World` / `People` do (`SafeFile`, under `_saveDir`).

## Things that will bite

- **Constants are inlined.** The compiler substitutes `const` values at every use, so the
  decompiled code shows `Game.GameTime + 180000` where the author wrote something like
  `BandWatchMs`. The `const` declarations are still there (often near the top of a file) but
  look unused. Changing a const changes nothing until you also replace the literal at its uses.
- **Static initializer order.** In a partial class, static field initializers run file by file.
  Thirteen fields read another field while initializing (e.g. `TownSpots = BuiltInTownSpots`,
  `Districts` built by `D(...)`, which reads `BuiltInRegions`). Each such pair is kept in one file,
  source first. `make check` fails if a change breaks this.
- **Names are the author's; locals are not.** Types, members and fields keep their real names.
  Local variables are ILSpy's (`num4`, `text2`), and so are comments: the original had none
  that survive compilation.
- **One instance only.** `ActiveCopy()` / `CheckOtherCopies` make a second copy of the DLL in
  `scripts\` (say `NemesisMod.old.dll`) stay inactive and warn. Don't leave backups in that folder.
- **Script Hook API 2.2.0.** `ApiCheck` reports any API member missing from the installed
  `ScriptHookRDRNetAPI.dll`. The build references the pinned 2.2.0 release (`make fetch-api`).
  Before using an API member that is newer than that, build against your installed version
  (empty `lib/`, then `-p:GameDir=…`).
