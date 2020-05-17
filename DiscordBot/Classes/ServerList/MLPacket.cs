using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.ServerList
{
    public class MLPacket : Packet<PacketId>
    {
        public MLPacket(PacketId id, JToken token) : base(id, token)
        {
        }
        public MLPacket(JObject token) : base(token)
        {
        }
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
        Connected,
        #endregion

        #region Multipurpose
        Error,
        Response,
        #endregion
    }
}
