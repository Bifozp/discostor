# Discostor

Discostor is a plugin for [Impostor](https://github.com/Impostor/Impostor) that automatically
mute/unmute players during game.

This plugin was created with the goal of being usable in environments where custom colors are used.

## Supported version

  | Discostor version | Impostor version | Download |
  |:------------------|:-----------------|-----:----|
  | v0.1.0            | 1.4.0-dev        |          |


## Usage

  Get the latest release from the releases page and put the contents of the zip in your Impostor folder.

```
├ Impostor.Server(.exe)
├ config.json
├ libraries
│    └ (bunch of dependency files)
├ plugins
│    └ Discostor.dll
└ config
     └ discostor.json
```

You will also need a Discord bot token.

There are many guides on the web, so I won't explain them here.

Once you have obtained the BOT token, put it in the "Token" section of "discostor.json".

### Permissions required by BOT

  | General Permissions | Text Permissions     | Voice Permissions  |
  |:--------------------|----------------------|--------------------|
  | Manage Nicknames    | Send TTS Messages    | Connect            |
  | Manage Channels     | Embed Links          | Mute Members       |
  | Change Nickname     | Read Message History | Move Members       |
  | View Channels       | Use External Emojis  | Speak              |
  |                     | Send Messages        | Deafen Members     |
  |                     | Manage Messages      | Use Voice Activity |
  |                     | Add Reactions        |                    |

  (based on automuteus. Some permissions may be unnecessary.)

## Commands

 | Command     | Aliases | Arguments    | Description                                                                          | Example       |
 |:------------|------------------------|--------------------------------------------------------------------------------------|---------------|
 | !ds help    | !ds h   | None         | Print help info and command usage                                                    |               |
 | !ds new     | !ds n   | GameCode     | Start a new game in the current text channel.                                        | `!ds n DBXYJ` |
 | !ds end     | !ds e   | None         | End the game entirely, and stop tracking players.                                    |               |
 | !ds link    | !ds l   | player index | link a discord user to their in-game index                                           | `!ds l 1`     |
 | !ds unlink  | !ds u   | player index | unlink a player                                                                      | `!ds u 1`     |
 | !ds refresh | !ds r   | None         | Remake the bot's status message entirely, in case it ends up too far up in the chat. |


## Compiling

```sh
# Setting up the build environment
./setup.sh

# build
cd Discostor/
./build.sh       # Debug build
# or
./build.sh -r    # Release build
# If you want to zip it
./build.sh -r -z # Release build & Generate zip
```

## Debug run

```sh
cd Discostor
./run.sh
```

## Similar Projects

  | Repos                                                    | Description                                  |
  |:---------------------------------------------------------|:---------------------------------------------| 
  | [AutoMuteUs](https://github.com/denverquane/automuteus)  | Mute bot using packet capture.               |
  | [ImpostorCord](https://github.com/tuxinal/impostorCord)  | Similar plugin using DSharpPlus for Discord. |


## Credits

  | Repos                                                     | Description                        |
  |:----------------------------------------------------------|:-----------------------------------|
  | [Impostor](https://github.com/Impostor/Impostor)          | An open source server for Among Us |
  | [Discord.Net](https://github.com/discord-net/Discord.Net) | Discord API wrapper for C#         |

