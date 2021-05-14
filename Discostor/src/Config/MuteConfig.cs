using System.Text.Json.Serialization;
namespace Impostor.Plugins.Discostor.Config
{
    public class MuteConfigChild
    {
        public MuteConfigChild(bool dead, bool alive)
        {
            Dead = dead; Alive = alive;
        }
        public bool Dead  { get; set; }
        public bool Alive { get; set; }
    }

    public class MuteConfig
    {
        public MuteConfigChild Mute { get; set; }
        public MuteConfigChild Deaf { get; set; }

        [JsonConstructor]
        public MuteConfig(MuteConfigChild mute, MuteConfigChild deaf)
        { Mute = mute; Deaf = deaf; }

        public MuteConfig(bool muteDead, bool muteAlive, bool deafDead, bool deafAlive)
        {
            Mute = new MuteConfigChild(muteDead, muteAlive);
            Deaf = new MuteConfigChild(deafDead, deafAlive);
        }
    }
}
