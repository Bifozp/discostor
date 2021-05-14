using System.Text.Json.Serialization;
namespace Impostor.Plugins.Discostor.Config
{
    public class MuteDelayConfigChild
    {
        public int ToLobby   { get; set; }
        public int ToTask    { get; set; }
        public int ToMeeting { get; set; }

        public MuteDelayConfigChild(){}

        public MuteDelayConfigChild(int lobby, int task, int meeting)
        { ToLobby = lobby; ToTask = task; ToMeeting = meeting; }
    }

    public class MuteDelayConfig
    {
        public MuteDelayConfigChild FromLobby   { get; set; } = new MuteDelayConfigChild(   0, 6000,    0);
        public MuteDelayConfigChild FromTask    { get; set; } = new MuteDelayConfigChild(1000,    0,    0);
        public MuteDelayConfigChild FromMeeting { get; set; } = new MuteDelayConfigChild(1000, 6000,    0);
    }
}
