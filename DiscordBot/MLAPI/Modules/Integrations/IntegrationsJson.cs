﻿using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Integrations
{
    public class ApplicationCommand
    {
        public ulong Id { get; set; }
        [JsonProperty("application_id")]
        public ulong AplicationId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public ApplicationCommandOption[] Options { get; set; }
    }
    public class ApplicationCommandOption
    {
        public ApplicationCommandOptionType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool? Default { get; set; }
        public bool Required { get; set; }
        public ApplicationCommandOptionChoice[] Choices { get; set; }
        public ApplicationCommandOption[] Options { get; set; }
    }
    public class ApplicationCommandOptionChoice
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }
    public class Interaction
    {
        public ulong Id { get; set; }
        public InteractionType Type { get; set; }
        [JsonProperty("guild_id")]
        public ulong GuildId { get; set; }
        [JsonProperty("channel_id")]
        public ulong ChannelId { get; set; }
        public GuildMember Member { get; set; }
        public ApplicationCommandInteractionData Data { get; set; }
        public string Token { get; set; }
        public int Version { get; set; }
    }
    public class GuildMember
    {
        public User User { get; set; }
        public ulong[] Roles { get; set; }
        [JsonProperty("premium_since")]
        public DateTime? PremiumSince { get; set; }
        [JsonProperty("permissions")]
        public GuildPermission Permissions { get; set; }

        public bool Pending { get; set; }
        public string Nick { get; set; }
        public bool Mute { get; set; }
        [JsonProperty("joined_at")]
        public DateTime JoinedAt { get; set; }
        [JsonProperty("is_pending")]
        public bool IsPending { get; set; }
        public bool Deaf { get; set; }
    }
    public class User
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string Discriminator { get; set; }
        [JsonProperty("public_flags")]
        public int PublicFlags { get; set; }
    }
    public enum InteractionType
    {
        Ping = 1,
        ApplicationCommand
    }
    public class ApplicationCommandInteractionData
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public ApplicationCommandInteractionDataOption[] Options { get; set; }
    }
    public class ApplicationCommandInteractionDataOption
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public ApplicationCommandInteractionDataOption[] Options { get; set; }
    }
    public class InteractionResponse
    {
        public InteractionResponse(InteractionResponseType type,
            string content = null, bool? tts = null, Embed embed = null, AllowedMentions mentions = null)
        {
            Type = type;
            Data = new InteractionApplicationCommandCallbackData()
            {
                TTS = tts,
                Content = content,
                Embeds = embed == null ? null : new Embed[1] { embed },
                AllowedMentions = mentions
            };
        }
        [JsonProperty("type")]
        public InteractionResponseType Type { get; }
        [JsonProperty("data")]
        InteractionApplicationCommandCallbackData Data { get; set; }
    }

    public enum InteractionResponseType
    {
        Pong = 1,
        Acknowledge,
        ChannelMessage,
        ChannelMessageWithSource,
        ACKWithSource
    }
    public class InteractionApplicationCommandCallbackData
    {
        [JsonProperty("tts")]
        public bool? TTS { get; set; }
        [JsonProperty("content")]
        public string Content { get; set; }
        [JsonProperty("embeds")]
        public Embed[] Embeds { get; set; }
        [JsonProperty("allowed_mentions")]
        public AllowedMentions AllowedMentions { get; set; }
    }
    public enum ApplicationCommandOptionType
    {
        SubCommand = 1,
        SubCommandGroup,
        String,
        Integer,
        Boolean,
        User,
        Channel,
        Role
    }
}
