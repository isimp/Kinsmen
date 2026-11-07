# Technical notes

## How it works

Every skill effect in the game reads its level through `Skills.GetSkillLevel`, which copies the saved level into a local, hands the copy to `SEMan.ModifySkillLevel` for status effects to add to (gear sets, Wolf Sight, meads), floors it and returns it. `SEMan.ModifySkillLevel` has no other caller in the game. Kinsmen puts a prefix on it that lowers the copy before effects add, for the local player's own character only (`src/CapHook.cs`). Damage rolls, stamina costs, bow draw, run speed, block power, summon counts and tooltips all follow, since they all go through `GetSkillLevel` or `GetSkillFactor`. The prefix only ever lowers, so a caller that asks what effects add alone, starting from 0, gets the same answer as without Kinsmen.

The rule (`src/Ladder.cs`): per skill, everyone's whole levels are walked from the lowest up, and the first step up wider than `OutlierGap` splits off the characters far ahead. Everyone below it counts in full. Everyone at or above it counts in full up to the best level below the gap plus `Margin`, and each level beyond by `RateAbove`. Without such a step nobody is held. A skill a character never trained counts as 0 for them. Whole levels are compared because a death takes a share of a level. A cap set by hand in `FixedCaps` replaces the group's cap for its skill, listed or not.

Skills live only in each player's character file, so the server keeps a record (`src/Record.cs`, `src/RecordServer.cs`): per character id (`Player.GetPlayerID`), the name, when last seen and the real level in each listed skill. Each game sends its character's levels on joining and whenever one changes, read from `Skills.GetSkillList` every two seconds (`src/RecordClient.cs`). The server notes them, drops characters unseen for `ForgetAfterDays`, writes the record to `BepInEx/config/Kinsmen/<world>.record.txt` through a file beside it, and sends the whole record to every game over the game's routed RPCs (`src/RecordMessages.cs`). Each game builds from it a table of the other characters' levels per skill (`src/CountingTable.cs`) and asks it with its own character's live level, so asking makes nothing new. Only a record sent by whatever runs the world is taken. A record file that cannot be read at all is moved aside as `.unreadable`, never written over.

## Real levels stay untouched

Kinsmen never writes `Skill.m_level`, `Skill.m_accumulator` or `Skills.m_skillData`, never calls a method that raises, lowers, resets or loads skills, never asks for a skill through `GetSkillLevel` or `GetSkillFactor` (which add an empty skill to a character that never trained it, and the save keeps it), and patches nothing but its four hooks: `SEMan.ModifySkillLevel`, `SkillsDialog.Setup`, `SEMan.GetHUDStatusEffects` and `ZNetScene.Awake`. `tests/Kinsmen.Standards/SkillSafetyStandardTests.cs` holds all of that over the source with the compiler's own types, the reflection route through a field's name included, and fails on any patch outside the four.

Gaining is the game's own: `Skill.Raise` adds a fixed amount per action (a hit, a parry, a dodge, a craft) times `Game.m_skillGainRate` and the status effects' share through `SEMan.ModifyRaiseSkill`, and compares it with a requirement worked out from the saved level. None of that reads a skill level through `GetSkillLevel`. Death loss (`Skills.LowerAllSkills`) works on the saved level too. So per action a held character gains what they gained before; over time they can gain less where they act at their counted level, a slower bow draw meaning fewer shots a minute.

## What shows

The game's skills window prints the counted level minus the saved one in its bonus slot, so a held Swords shows "100 -42" with no change from Kinsmen; `src/SkillsWindow.cs` colours that slot where it is negative and adds a line to the tooltip naming whose level sets the cap. The rows follow `Skills.GetSkillList` in order, as the window does.

The effect icon (`src/HeldEffect.cs`) is added to the list the HUD asks `SEMan.GetHUDStatusEffects` for. It never enters the character's effects, so nothing of it is sent to other players or saved. It shows the game's Blocking skill icon, the name Kinsmen and how many skills are held.

On the first record after joining, a held player gets a message in the middle of the screen and the list in chat. `/kinsmen`, `/kinsmen caps`, `/kinsmen record` and `/kinsmen forget Name` are chat commands; forget is done by the server for admins and the host only.

Other mods that read the saved level themselves can ask `Kinsmen.KinsmenApi.TryCounted(skill, savedLevel, out counted, out why)` by reflection.

## Server rules

Kinsmen is required through ServerSync and clients without it are refused. `Enabled`, `Skills`, `OutlierGap`, `Margin`, `RateAbove`, `FixedCaps` and `ForgetAfterDays` are pushed from the server while `LockConfiguration` is on, and admins can still change them. `ShowEffectIcon` and `ShowJoinMessage` are never synchronised.

## Limits

The lowering runs in each player's own game, since skills exist only in the character file; the version check keeps a game without Kinsmen out, but past that it rests on trust, and a game could send made-up levels. The record is kept per character, not per player, so another character of the same player counts as someone else and can raise the group's best. A character between the group and the one far ahead raises the cap as long as no step to them is wider than the gap. Until the server's record arrives after joining, nothing is lowered.

## Building

Requires the .NET SDK 8 or newer. To build against the reference stubs in `lib/`, with no game installation needed:

```
dotnet build -c Release -p:LibsDir=lib
```

To build against a local installation and copy the result into a BepInEx profile:

```
dotnet build -c Release -p:ValheimDir="<Valheim folder>" -p:ProfileDir="<profile folder>"
```

The `VALHEIM_DIR` environment variable can be used instead of `ValheimDir`. Without these, a default Steam installation and a default Gale profile are assumed.

Everything Kinsmen decides is in game-free classes the unit tests drive, against the metadata-only game assemblies in `lib/`, of which they use only the game's skill enum: the rule (`src/Ladder.cs`), the record and its text (`src/Record.cs`), the skill list and caps set by hand (`src/SkillList.cs`), what a game works out from the record (`src/CountingTable.cs`) and the words the player reads (`src/Words.cs`). The game-side files read the game for them and carry out what they decide; `GameOnlyFiles.cs` names each with why it needs the game.

```
dotnet test tests/Kinsmen.Tests
```

The build itself runs every .NET code analysis rule and fails on any finding (`BuildChecks.props`), but for the few that misread a Unity plugin, which `Analysis.globalconfig` turns off with the reason for each. `tests/Kinsmen.Standards` holds what the compiler cannot, as in the other isimp mods, and the skill safety rules above. Neither set needs a `BepInEx.dll`:

```
dotnet test tests/Kinsmen.Standards
```

`.githooks/pre-push` runs both sets before every push, and `.githooks/commit-msg` refuses a message that carries a clock time. Git uses them once told to, per clone: `git config core.hooksPath .githooks`.

`selftest/` is an in-game self-test, a plugin of its own that reaches into Kinsmen through `InternalsVisibleTo`; build it with `dotnet build selftest` and type `/kinsmen selftest` in chat. It reads the player's character and changes nothing it keeps: for a moment it counts from a made-up record in which a character of its own stands at 0 in the character's best listed skill, then puts the server's record back. It checks the level the game then gives (effects added on top of the counted level), that the saved level and the bytes the save writes stay the same, that the icon shows without entering the character's effects, that `/kinsmen caps` lists the cap, and that the game's own `Skill.Raise`, run on skill objects of its own, ends the same with the hold in force and without. It is never packaged; a release takes `Kinsmen.dll` alone.

The files in `lib/` contain metadata only: every method body is replaced and resources are removed, so they can be compiled against but not run. Regenerate them after a game update with `tools/strip-references.ps1`. Releasing is described in [releasing.md](releasing.md).

ServerSync by blaxxun is included in `src/ServerSync` under MIT-0.
