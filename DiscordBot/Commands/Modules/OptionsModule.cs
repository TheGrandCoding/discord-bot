using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("options")]
    [Name("Bot User Options")]
    public class OptionsModule : BotBase
    {
        [Command("view")]
        [Alias("list", "see")]
        [Description("Lists all options, current and default values.")]
        public async Task View()
        {
            var properties = typeof(BotDbUserOptions).GetProperties().Where(x => x.CanWrite);
            EmbedBuilder builder = new EmbedBuilder();
            var def = BotDbUserOptions.Default;
            foreach (var property in properties.OrderBy(x => x.Name))
            {
                var key = $"{Program.GetTypeName(property.PropertyType)} {property.Name}";
                var defaultValue = property.GetValue(def);
                var value = $"Default: `{defaultValue}`";
                value += $"\r\nCurrent: `{property.GetValue(Context.BotDbUser.Options)}`";
                if (property.PropertyType.IsEnum)
                {
                    value += "\r\n" + enumGetPermitted(property);
                }
                builder.AddField(key, value, true);
            }
            await ReplyAsync(embed: builder.Build());
        }

        [Command("set")]
        [Description("Sets an option to a specific value")]
        public async Task<RuntimeResult> Set(string key, [Remainder]string value)
        {
            var property = typeof(BotDbUserOptions).GetProperty(key);
            if(property == null)
            {
                await ReplyAsync("Unknown property. Please see possible options below:");
                await View();
                return new BotResult();
            }
            var result = setValue(property, value);
            if (!result.IsSuccess)
                return result;
            Program.Save();
            await ReplyAsync("Set.");
            return new BotResult();
        }

        string enumGetPermitted(PropertyInfo property)
        {
            return "Permitted: `" + string.Join("`, `", property.PropertyType.GetEnumNames()) + "`";
        }

        BotResult setValue(PropertyInfo property, string value)
        {
            if(property.PropertyType == typeof(string))
            {
                property.SetValue(Context.BotDbUser.Options, value);
            } else if (property.PropertyType == typeof(int))
            {
                if (!int.TryParse(value, out var v))
                    return new BotResult("Value must be an integer.");
                property.SetValue(Context.BotDbUser.Options, v);
            } else if (property.PropertyType == typeof(bool))
            {
                if (!bool.TryParse(value, out var b))
                    return new BotResult("Value must be a boolean");
                property.SetValue(Context.BotDbUser.Options, b);
            } else if (property.PropertyType.IsEnum)
            {
                if (!Enum.TryParse(property.PropertyType, value, out var en))
                    return new BotResult("Enum value not allowed; " + enumGetPermitted(property));
                property.SetValue(Context.BotDbUser.Options, en);
            } else
            {
                return new BotResult($"Unable to set values of type '{property.PropertyType.FullName}'.\r\n" +
                    $"Please contact bot developer to add.");
            }
            return new BotResult();
        }
    }
}
