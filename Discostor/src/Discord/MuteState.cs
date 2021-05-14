using System;
namespace Impostor.Plugins.Discostor.Discord
{
    internal class MuteState
    {
        internal bool Mute { get; set; } = false;
        internal bool Deaf { get; set; } = false;

        internal MuteState(){}
        internal MuteState(MuteState m)
        {
            this.Mute = m.Mute;
            this.Deaf = m.Deaf;
        }

        public override bool Equals(object obj)
        {
            if(obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }
            var m = obj as MuteState;
            if( this.Mute != m.Mute
                || this.Deaf != m.Deaf )
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Mute, Deaf);
        }
    }
}
