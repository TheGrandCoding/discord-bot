using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Integrations
{
    public class InteractionCommandContext : ICommandContext
    {
        public InteractionCommandContext(Interaction interaction)
        {
            Client = Program.Client;
            Interaction = interaction;
        }
        public IDiscordClient Client { get; set; }
        public IGuild Guild { get; set; }
        public IMessageChannel Channel { get; set; }
        public IUser User { get; set; }
        public BotUser BotUser { get; set; }
        public Interaction Interaction { get; set; }

        public IUserMessage Message => throw new NotImplementedException();
    }
    /// <summary>
    /// Command base for webhook interactions
    /// </summary>
    public class InteractionBase
    {
        public InteractionBase(InteractionCommandContext context)
        {
            Context = context;
        }
        public InteractionCommandContext Context { get; }
        protected async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, AllowedMentions allowedMentions = null, InteractionResponseFlags flags = InteractionResponseFlags.None)
        {
            try
            {
                var resp = new InteractionResponse(InteractionResponseType.ChannelMessage, message, isTTS, embed, allowedMentions);
                resp.Flags = flags;
                var client = Program.Services.GetRequiredService<HttpClient>();
                var str = Program.Serialise(resp);
                var content = new StringContent(str, Encoding.UTF8, "application/json");
                var url = Discord.DiscordConfig.APIUrl + $"interactions/{Context.Interaction.Id}/{Context.Interaction.Token}/callback";
                Program.LogMsg($"Attempting to send to {url}: {str}");
                var msg = new HttpRequestMessage(HttpMethod.Post, url);
                msg.Content = content;
                var thing = await client.SendAsync(msg);
                var result = await thing.Content.ReadAsStringAsync();
                Program.LogMsg($"{thing.StatusCode}, {result}");
            }
            catch (Exception ex)
            {
                Program.LogMsg("Send", ex);
            }
            return null;
        }
    }

    public class IdAttribute : Attribute
    {
        public ulong Id { get; set; }
        public IdAttribute(ulong id)
        {
            Id = id;
        }
    }
}
