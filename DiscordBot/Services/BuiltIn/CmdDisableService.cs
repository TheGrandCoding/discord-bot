using Discord.Commands;
using DiscordBot.Classes.Attributes;
using DiscordBot.Commands.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Services.BuiltIn
{
    public class CmdDisableService : SavedService
    {
        Dictionary<string, string> disabled = new Dictionary<string, string>();
        string getKey(ModuleInfo thing) => thing.Name;
        string getKey(CommandInfo cmd) => $"{cmd.Name}&{cmd.Parameters.Count}";
        string getDisabled(ModuleInfo module)
        {
            if (disabled.TryGetValue(getKey(module), out var str))
                return str;
            var a = module.Attributes.FirstOrDefault(x => x is RequireServiceAttribute);
            if(a is RequireServiceAttribute relies)
            {
                foreach(var x in relies.Types)
                {
                    var service = (Service)Program.GlobalServices.GetRequiredService(x);
                    if(service.HasFailed)
                    {
                        SetDisabled(module, $"Relies on {service.Name} which has encountered an issue");
                        return $"Required service {service.Name} is unavailable";
                    }
                    if (service.State == ServiceState.Disabled)
                    {
                        SetDisabled(module, $"Relies on {service.Name} which has been disabled");
                        return $"Required service {service.Name} is no longer supported";
                    }
                }
            }
            if (module.Parent != null)
                return getDisabled(module.Parent);
            return null;
        }
        string getDisabled(CommandInfo cmd)
        {
            if (disabled.TryGetValue(getKey(cmd), out var str))
                return str;
            return getDisabled(cmd.Module);
        }
        public bool IsDisabled(CommandInfo info, out string reason)
        {
            reason = getDisabled(info);
            return !string.IsNullOrWhiteSpace(reason);
        }
        public bool IsDisabled(ModuleInfo info, out string reason)
        {
            reason = getDisabled(info);
            return !string.IsNullOrWhiteSpace(reason);
        }
        public void SetDisabled(CommandInfo info, string reason)
        {
            disabled[getKey(info)] = reason;
        }
        public void SetDisabled(ModuleInfo info, string reason)
        {
            disabled[getKey(info)] = reason;
        }
        public void SetEnabled(CommandInfo info)
        {
            disabled.Remove(getKey(info));
        }
        public override string GenerateSave()
        {
            return JsonConvert.SerializeObject(disabled);
        }
        public override bool IsCritical => true;
        public override void OnLoaded(IServiceProvider services)
        {
            disabled = JsonConvert.DeserializeObject<Dictionary<string, string>>(ReadSave());
        }
    }
}
