using System.Collections.Generic;
namespace Impostor.Plugins.Discostor.Config
{
    public class EmoteConfig
    {
        public ulong? GuildId { get; set; } = null;
        public List<string> Emotes { get; set; } = new List<string>();
    }
}
