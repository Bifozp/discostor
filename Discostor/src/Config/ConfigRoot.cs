namespace Impostor.Plugins.Discostor.Config
{
    public class ConfigRoot
    {
        public DiscordBotConfig Discord { get; set; } = new DiscordBotConfig();
        public AmongusConfig Amongus { get; set; } = new AmongusConfig();
    }
}
