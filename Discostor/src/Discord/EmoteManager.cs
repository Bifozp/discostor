using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
using Impostor.Plugins.Discostor.Config;

namespace Impostor.Plugins.Discostor.Discord
{
    public class EmoteManager
    {
        private readonly EmoteConfig _emoteConfig;
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordSocketClient _client;
        private const int DiscordFieldsCapacity = 25;
        private const int EmojiBegin = 0x0001f1e6; // regional indicator "A"

        private Dictionary<string, int> _emotes = new Dictionary<string, int>(DiscordFieldsCapacity);
        private bool[] _isCustom = new bool[DiscordFieldsCapacity];
        private string[] _defaultEmotes = new string[DiscordFieldsCapacity];

        internal string[] Emotes{ get{ return _emotes.Keys.ToArray(); }}

        public EmoteManager(
                ILogger<Discostor> logger,
                EmoteConfig emoteConfig,
                DiscordSocketClient client)
        {
            _emoteConfig = emoteConfig;
            _logger = logger;
            _client = client;
            try
            {
                // init default emojis
                for(int i=0, code=EmojiBegin ; i<DiscordFieldsCapacity; i++)
                {
                    _defaultEmotes.SetValue(char.ConvertFromUtf32(code), i);
                    _isCustom[i] = false;
                    code++;
                }

                // register emotes
                int inx = 0;
                foreach(var em in emoteConfig.Emotes)
                {
                    if(!string.IsNullOrEmpty(em))
                    {
                        _emotes.Add(em, inx);
                        _isCustom[inx] = true;
                    }
                    else
                    {
                        _emotes.Add(_defaultEmotes[inx], inx);
                    }
                    inx++;
                    if(inx >= DiscordFieldsCapacity) break;
                }
                while(inx < DiscordFieldsCapacity)
                {
                    _emotes.Add(_defaultEmotes[inx], inx);
                    inx++;
                }
            }
            catch(Exception e)
            {
                logger.LogError($"{e.GetType()} -- {e.Message}");
                using(logger.BeginScope("emote")){ logger.LogError(e.StackTrace); }

                _emotes.Clear();
                _isCustom = null;
                _defaultEmotes = null;
                _emotes = null;
                throw;
            }
        }

        internal int GetIndex(string emote)
        {
            if(_emotes.TryGetValue(emote, out var index))
            {
                return index;
            }
            return -1;
        }

        internal IEmote GetEmote(int inx)
        {
            if(_isCustom[inx])
            {
                if(TryGetCustomEmote(inx, out var em))
                {
                    return em;
                }
            }

            // not custom, or default
            return new Emoji(_defaultEmotes[inx]);
        }

        private bool TryGetCustomEmote(int inx, out IEmote outEmote)
        {
            IGuild guild = null;
            if(_emoteConfig.GuildId != null)
            {
                guild = _client.GetGuild(_emoteConfig.GuildId.Value);
            }
            if(guild != null)
            {
                // search emote
                foreach(var em in guild.Emotes)
                {
                    if(em.Name == _emoteConfig.Emotes[inx])
                    {
                        outEmote = em;
                        return true;
                    }
                }
            }

            // not found, switch to default
            _isCustom.SetValue(false, inx);
            _emotes.Remove(_emoteConfig.Emotes[inx]);
            _emotes.Add(_defaultEmotes[inx], inx);

            outEmote = null;
            return false;
        }
    }
}
