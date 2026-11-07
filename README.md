# Kinsmen

Keep a character who is far ahead of the rest of the group in the skills that matter in a fight close to everyone else on your server, without touching anyone's saved levels.

A character brought in from another world, or one that simply played a lot more, can have most skills near 100 while the rest of the group sits between 20 and 40. Kinsmen lets that character count on your server as a few levels above the best of the rest, and each level beyond that as only a share. The group's own spread is never touched. Leaving the server gives the character everything back.

## AI notice

Most of Kinsmen was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## How it works

For each skill, Kinsmen walks up everyone's levels. The first gap wider than 20 levels splits off the characters far ahead. Everyone below that gap counts in full. Everyone above it counts in full up to the best level below the gap plus 5, and each level beyond that as a quarter. With the rest of the group at 25 and 40 in Swords, a character at 100 counts as 58. Two characters far ahead stand above the gap together, so neither lifts the other.

The lowering happens before gear, potions and other effects add their levels, so everyone gets the full bonus of a set or a mead on top. It counts only for what the skill does in the moment. The saved level, how fast a skill rises and what a death takes stay exactly as the game has them.

Held skills by default are every weapon, both magics, Blocking, Run, Jump, Dodge, Sneak, Swim, Ride, Crafting and Cooking. Wood Cutting, Fishing and Farming stay free.

## What you see

A held player sees a message on joining and a list in chat, an effect icon beside the others while any skill counts lower, and in the game's skills window the lowered level in the bonus slot with a line in the tooltip saying what the skill counts as and whose level sets it.

Type /kinsmen in chat for your held skills and why, /kinsmen caps for every skill's cap as it stands, and /kinsmen record for who the server remembers.

## Multiplayer

Kinsmen has to be installed on the server and by every player, and the server refuses players without it. The server keeps a record of every character's real levels in the listed skills, which each game sends it, and hands that record to every game. A character not seen for 30 days drops out of the record, and an admin can take one out at once with /kinsmen forget followed by the character's name. The server's settings apply to everyone, while the icon and the joining message can be switched off by each player.

## Settings

All settings are in BepInEx/config/isimp.Kinsmen.cfg, each with a description: the gap, the margin, the share each level beyond counts, the held skills, caps set by hand for single skills, and how long the record keeps a character.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Kinsmen
