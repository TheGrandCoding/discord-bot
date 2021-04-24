using Discord.Commands;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DiscordBot.MLAPI.Attributes
{
    /// <summary>
    /// For MLAPI.
    /// </summary>
    public class RequireServiceAttribute : APIPrecondition
    {
        private Type[] _services;
        public RequireServiceAttribute(params Type[] serviceTypes)
        {
            var srv = typeof(Service);
            foreach(var type in serviceTypes)
            {
                if (!srv.IsAssignableFrom(type))
                    throw new ArgumentException("Type is not a Service", type.Name);
            }
            _services = serviceTypes;
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
            foreach(var type in _services)
            {
                var service = Program.Services.GetRequiredService(type) as Service;
                if(service.IsEnabled == false)
                    return PreconditionResult.FromError("Relies on " + service.Name + ", but that service is not enabled.");
                if (service.HasFailed)
                    return PreconditionResult.FromError("Relies on " + service.Name + ", but that service has encountered an error.");
            }
            return PreconditionResult.FromSuccess();
        }
    }
}
