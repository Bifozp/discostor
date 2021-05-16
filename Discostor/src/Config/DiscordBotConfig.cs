//using System.Collections.Generic;
namespace Impostor.Plugins.Discostor.Config
{
    public class DiscordBotConfig
    {
        public string Token { get; set; } = "";
        public int MessageCacheSize { get; set; } = 100;
        // Future...
        //public List<string> WorkerTokens { get; set; } = new List<string>();
        public string CommandPrefix { get; set; } = "!ds";
        public bool DeleteConfirmedCommand { get; set; } = true;
        public bool MuteSpectator { get; set; } = false;

        public MuteConfig TaskPhaseMuteRules { get; set; } =
            new MuteConfig(/*Mute*/ false, true, /*Deaf*/ false, true );

        public MuteConfig MeetingPhaseMuteRules { get; set; } =
            new MuteConfig(/*Mute*/true, false, /*Deaf*/ false, false );

        public MuteDelayConfig MuteDelays { get; set; } = new MuteDelayConfig();
    }
}
