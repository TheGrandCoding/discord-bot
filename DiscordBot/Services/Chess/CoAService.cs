﻿using DiscordBot.Classes;
using DiscordBot.Classes.Chess.COA;
using DiscordBot.Classes.Converters;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    [RequireService(typeof(ChessService))]
    public class CoAService : SavedService
    {
        public static List<CoAHearing> Hearings = new List<CoAHearing>();

        public override string GenerateSave()
        {
            return Program.Serialise(Hearings, new ChessPlayerConverter());
        }
        public override void OnLoaded()
        {
            Hearings = Program.Deserialise<List<CoAHearing>>(ReadSave("[]"), new ChessPlayerConverter());
            foreach (var x in Hearings)
                x.SetIds();
            this.OnSave();
        }
    }
}
