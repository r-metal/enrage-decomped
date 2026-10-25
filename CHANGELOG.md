# Changelog

Changes made in this repository on top of the reconstructed enRAGE. Versions keep enRAGE's
number and add `-decomped.N` for each release made here. These builds are unofficial: report
their problems here, not to the enRAGE author.

## 0.9.5.3-decomped.1 - unreleased

Built on the 0.9.5.3 reconstruction (the `upstream` branch). **Tested in game on 2026-10-25**,
on a copied save with the enRAGE folder restored afterwards. Recruiting by sparing (questioning
path), Recruit Now, Send For Him, Send Home, Let Him Go, riding, fighting, laid up, and keeping
recruits across a mod reload and a game save and load all worked. Camp companions at a gang
camp are not yet tested in game, because the gang was away on a story mission. A code review after
the test tightened the companion-fight and offer rules: mounted, surrendered and tied men, a
10-minute cap on a tied man's offer, and offers ending when a mission starts. Those changes
were not re-tested in game. The design
is in [docs/decisions/D-001-companions-and-recruits.md](docs/decisions/D-001-companions-and-recruits.md).

### Added

- **Ride With Me: camp companions are back.** The author's companion code shipped in 0.9.5.3
  but was switched off: a `const bool` guarded it, so the compiler dropped its per-tick
  dispatcher. That dispatcher is rewritten (`UpdateCompanion`), and the rest is the author's
  code as it was.
  - At a gang camp, stand within 3 m of one of the gang and hold **Ride With Me**. Charles,
    Sadie, John, Lenny, Javier, Hosea, Bill, Micah, Sean and Kieran (once he walks free) say
    yes, Uncle half the time. The others say no once and are not asked again.
  - Up to 3 ride with you, recruits included. They mount and dismount with you, catch up when
    left behind, take on the man you shoot at and join fights near you.
  - On foot, hold **Send Home** by one to send him back. Riding back into camp sends the gang
    home, and so does coming across one of them out in the world (he has work for you).
  - Below 30% health a companion pulls out and is laid up for one game day.
- **Recruits.** A man you spare may offer to ride with you for good.
  - The three ways to spare a man all count: he surrenders in a standoff and you let him run,
    you let him go after questioning, or his nerve breaks and you let him go.
  - The odds (`RecruitRules.OfferChance`) start from his temper: Coward 45%, Drifter 40,
    Greedy 30, none 20, Cold 15, Hothead 8, Loyal 5, Proud 3. Fear adds a fifth of itself (up
    to 20), hate takes off a quarter of itself, and officers and law or army men get half. Then
    `[Recruits] ChancePercent` scales it, and it never goes above 75%. Leaders, story and lore
    men, and men out to avenge something on you never offer. Nor does anyone while recruits
    are off or you already keep `MaxKept` men.
  - When the roll succeeds he stops, faces you and offers for 60 seconds. Get within 3 m and
    hold **Ride With Me**. If you don't, the offer lapses and he goes on his way as before.
  - Once you take him on, his record leaves the roster (`nemeses.json`) and goes to
    `world.json` `Recruits`, so no outfit can send him after you. His old gang hears he turned:
    a `LIFE` event in the World tab, and a `Bitter` thought for everyone tied to him.
  - If 3 already ride with you (camp companions count), or `[Companions] Enabled` is off, he
    is still kept. He goes his way until you send for him.
  - Hurt below 30% health, he leaves and is laid up for one game day, like a camp companion.
- **"Your Men" page in the F11 menu.** In Hierarchy, press Q / E (pad: LB / RB) to the last
  chip on the faction bar, after "Unaffiliated". It is not a header tab. It lists each man you
  keep with his old gang and his status (Riding, Laid up (Nh), Waiting). Its actions:
  - **Send For Him**: he joins you from out of sight, behind you;
  - **Send Home**: he leaves, and stays one of your men;
  - **Let Him Go**: he is gone for good, with a `LIFE` event. It needs a second Enter.
- **What's New page** for this version. It shows once, the first time the new version loads.
- **Debug: Recruit Now.** With `[Debug] Enabled` and the overlay on (Ctrl+F11), a man's F11
  card gets **Recruit Now**: he joins Your Men at once. Send for him to have him ride with you.
  It's for testing, and only for men who are off stage and below rank 5.
- **Cowards swear revenge less.** A coward needs 25 more anger than other men before he swears
  to settle it with you (`Mind.AvengeLine`). This applies where revenge is sworn on a grudge,
  when you kill his kin or friend, and when goals are first set. It also changes the original
  enRAGE: fewer cowards end up hunting you.
- `grudges.log` lines for all of it: `COMPANION ...` for riding together, `RECRUIT ...` for
  offers, odds and rolls, take-ons, send-for, send-home and let-go.

### Fixed during the in-game test

- A spared man's offer died within a second, because the "he quits his gang" logic took him off
  the roster at once. Now that logic waits while an offer is open, and covers his kin and friend
  too.
- A hogtied man you let go could never see the prompt, so his 60 seconds ran out while he was
  tied. Now the offer waits until you cut him free, for up to 10 minutes.
- A companion fought a horse that kicked you and never stopped: he treated any combat you were
  in as a threat. Now only men count who are fighting you or a companion. Men who are tied,
  spared, surrendered, being questioned or offering don't. Mounted men count. A companion drops
  a man as soon as he gives up. He stands down 6 seconds after the last one stops.
- A companion ignored enRAGE's own encounter men, so he dropped out of their fights.
- Your own rider got the Interrogate prompt.
- No recruit prompt shows during a story mission, and open offers end when one starts.

### Settings

New `NemesisMod.ini` sections. They are written on first run and can be changed in Alt+F11.

| Key | Default | What it does |
|---|---|---|
| `[Companions] Enabled` | `true` | Ride With Me. `false` sends everyone riding with you home once and hides the prompt, the way 0.9.5.3 behaves. |
| `[Recruits] Enabled` | `true` | Spared men may offer to ride with you. `false`: none do, and open offers end. |
| `[Recruits] MaxKept` | `6` | The most men you keep at once (0-12). At the cap, no one offers. |
| `[Recruits] ChancePercent` | `100` | % of the usual offer odds (0-300). The odds never go above 75%. |

### Saves

- `world.json` gains a `Recruits` list: the men you keep, as full records. It is optional.
  Saves from 0.9.5.3 load fine and start with no recruits. No other save property was added
  or changed.
- If you go back to the official 0.9.5.3, it ignores `Recruits` and drops it the next time it
  writes `world.json`. The men you kept are gone from that world, and they do not return to
  the roster.

### Changed

- The version shown in game is `0.9.5.3-decomped.1`. The assembly version stays `0.9.5.3`, so
  a newer official enRAGE in `scripts\` still wins over this build.

### For contributors

- `tests/NemesisMod.Tests`: xunit tests against the built DLL. `make test` (`FILTER=Class`,
  `VERBOSE=1`) prints failures and a tally.
- `make ci` runs `make check`, `make test` and `make invariants` (no binaries, nothing
  identifying, UTC commit dates). It ends with `ci: OK`.
- `make verify` passes only on the untouched reconstruction (`upstream`). On `master` it now
  fails on these changes, as expected; `make diff-upstream` lists them.

## 0.9.5.3

The official enRAGE release that this repository reconstructs. Its release notes are on the
[enRAGE Nexus Mods page](https://www.nexusmods.com/reddeadredemption2/mods/10967).
