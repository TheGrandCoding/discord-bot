using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.ServerList
{
    public class MLPacket : Packet<PacketId>
    {
        public MLPacket(PacketId id, JToken content) : base(id, content) { }
    }

    public enum PacketId 
    {
        Reserved,

        #region Client -> Server
        SetPlayers,
        AddPlayer,
        PatchPlayer,
        #endregion

        #region Server -> Client 
        #endregion

        #region Multipurpose
        Error,
        #endregion
    }
}
