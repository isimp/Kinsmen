# Kinsmen

Hold characters who stand far ahead of the rest of a server in a fight skill close to everyone else, without changing anyone's saved skill levels.

## AI notice

Most of Kinsmen was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## How it works

For each skill, Kinsmen sorts everyone's levels and looks for the first gap of more than 20 levels. Everyone below that gap counts in full. Everyone above it counts in full up to the best level below the gap plus 5, and each further level counts as a quarter. A skill at 100 against a best of 40 below the gap counts as 58. When nobody stands that far ahead, nothing changes.

The lowered level applies only on that server and only to what the skill does in the moment. Gear, meads and other effects add their levels on top of it. The saved level, how skills rise and what a death takes stay as the game has them, and leaving the server restores every skill in full.

By default Kinsmen holds every weapon skill, both magic skills, Blocking, Run, Jump, Dodge, Sneak, Swim, Ride, Crafting and Cooking. Wood Cutting, Fishing and Farming stay free.

## Using it

A player whose skills are held gets a message on joining, a list in chat, and an effect icon with the number of held skills. The skills window shows the lowered level beside the real one, and the tooltip says what the skill counts as and whose level sets the limit.

Type /kinsmen in chat to see your held skills and why, /kinsmen caps to see every skill's current limit, and /kinsmen record to see which characters the server remembers.

## Multiplayer

Kinsmen has to be installed on the server and by every player, and the server refuses players without it. The server keeps a record of every character's real levels in the held skills, which each game sends it, and shares that record with every game. A character not seen for 30 days drops out of the record, and an admin can remove one at once with /kinsmen forget followed by the character's name. The server's settings apply to everyone, while the icon and the joining message can be switched off by each player.

## Settings

All settings are in BepInEx/config/isimp.Kinsmen.cfg, each with a description: the gap, the margin, the share each further level counts, the held skills, limits set by hand for single skills, and how long the record keeps a character.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Kinsmen
