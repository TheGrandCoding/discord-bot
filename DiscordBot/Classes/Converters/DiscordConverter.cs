using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class DiscordConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ITextChannel)
                || objectType == typeof(SocketTextChannel)
                || objectType == typeof(RestTextChannel)
                || objectType == typeof(IVoiceChannel)
                || objectType == typeof(SocketVoiceChannel)
                || objectType == typeof(RestVoiceChannel)
                || objectType == typeof(SocketGuildUser)
                || objectType == typeof(IUserMessage)
                || objectType == typeof(RestUserMessage)
                || objectType == typeof(SocketUserMessage)
                || objectType == typeof(ICategoryChannel)
                || objectType == typeof(SocketCategoryChannel)
                || objectType == typeof(RestCategoryChannel)
                || objectType == typeof(IRole)
                || objectType == typeof(SocketRole)
                || objectType == typeof(RestRole)
                || objectType == typeof(IGuild)
                || objectType == typeof(SocketGuild)
                || objectType == typeof(IGuildUser)
                || objectType == typeof(SocketGuildUser)
                || objectType == typeof(SocketUser)
                || objectType == typeof(IUser);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
                return null;
            string thing = (string)reader.Value;
            var split = thing.Split('.');
            var gid = ulong.Parse(split[0]);
            var id = ulong.Parse(split[1]);

            var guild = Program.Client.GetGuild(gid);
            if (objectType == typeof(SocketGuild) || objectType == typeof(IGuild))
                return guild;
            if (objectType == typeof(SocketGuildUser) || objectType == typeof(IGuildUser))
                return guild.GetUser(id);
            if (objectType == typeof(SocketUser) || objectType == typeof(IUser))
                return guild != null ? guild.GetUser(id) : Program.Client.GetUser(id);
            if (objectType == typeof(SocketTextChannel) || objectType == typeof(ITextChannel))
                return (ITextChannel)guild.GetTextChannel(id) ?? new NullTextChannel(id, gid);
            if (objectType == typeof(SocketCategoryChannel) || objectType == typeof(ICategoryChannel))
                return guild.GetCategoryChannel(id);
            if(objectType == typeof(SocketVoiceChannel) || objectType == typeof(IVoiceChannel))
                return guild.GetVoiceChannel(id);
            if(objectType == typeof(SocketGuildUser) || objectType == typeof(IGuildUser)) 
                return guild.GetUser(id);
            if (objectType == typeof(SocketRole) || objectType == typeof(IRole))
                return guild.GetRole(id);
            if(objectType == typeof(SocketUserMessage) || objectType == typeof(IUserMessage))
            {
                var msgId = ulong.Parse(split[2]);
                if(guild == null)
                {
                    var chnl = Program.Client.GetUser(id).CreateDMChannelAsync().Result;
                    return chnl.GetMessageAsync(msgId).Result;
                } else
                {
                    return guild.GetTextChannel(id)?.GetMessageAsync(msgId)?.Result;
                }
            }
            return null;
        }

        public string GetValue(object value)
        {
            if (value is IGuildChannel gc)
            {
                return $"{gc.GuildId}.{gc.Id}";
            }
            else if (value is IRole rl)
            {
                return $"{rl.Guild.Id}.{rl.Id}";
            }
            else if (value is IGuildUser gu)
            {
                return $"{gu.GuildId}.{gu.Id}";
            }
            else if (value is IUserMessage m)
            {
                ulong gId = 0;
                ulong cId = m.Channel.Id;
                ulong mId = m.Id;
                if (m.Channel is IGuildChannel c)
                    gId = c.GuildId;
                if (m.Channel is IDMChannel d)
                    cId = d.Recipient.Id;
                return $"{gId}.{cId}.{mId}";
            } else if(value is IGuild g)
            {
                return $"{g.Id}.0";
            }
            else if (value is IEntity<ulong> eu)
            {
                return $"0.{eu.Id}";
            }
            return (value ?? "<null>").ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var jval = new JValue(GetValue(value));
            jval.WriteTo(writer);
        }
    }
}
