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
                || objectType == typeof(SocketVoiceChannel)
                || objectType == typeof(SocketGuildUser)
                || objectType == typeof(IUserMessage)
                || objectType == typeof(RestUserMessage)
                || objectType == typeof(SocketUserMessage);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string thing = (string)reader.Value;
            var split = thing.Split('.');
            var gid = ulong.Parse(split[0]);
            var id = ulong.Parse(split[1]);
            var guild = Program.Client.GetGuild(gid);
            if (guild == null)
                return null;
            if (objectType == typeof(SocketTextChannel) || objectType == typeof(ITextChannel))
                return guild.GetTextChannel(id);
            if(objectType == typeof(SocketVoiceChannel) || objectType == typeof(IVoiceChannel))
                return guild.GetVoiceChannel(id);
            if(objectType == typeof(SocketGuildUser) || objectType == typeof(IGuildUser)) 
                return guild.GetUser(id);

            var msgId = ulong.Parse(split[2]);
            return guild.GetTextChannel(id).GetMessageAsync(msgId).Result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is IGuildChannel gc)
            {
                var jval = new JValue($"{gc.GuildId}.{gc.Id}");
                jval.WriteTo(writer);
            } else if(value is IGuildUser gu)
            {
                var jval = new JValue($"{gu.GuildId}.{gu.Id}");
                jval.WriteTo(writer);
            } else if (value is IUserMessage m)
            {
                ulong gId = 0;
                if (m.Channel is IGuildChannel c)
                    gId = c.GuildId;
                var jval = new JValue($"{gId}.{m.Channel.Id}.{m.Id}");
                jval.WriteTo(writer);
            }
            else if (value is IEntity<ulong> eu)
            {
                var jval = new JValue($"0.{eu.Id}");
                jval.WriteTo(writer);
            }
        }
    }
}
