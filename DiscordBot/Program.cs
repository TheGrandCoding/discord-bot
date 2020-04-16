using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using DiscordBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

[assembly: AssemblyVersion(DiscordBot.Program.VERSION)]
namespace DiscordBot
{
    public partial class Program
    {
        public const string VERSION = "0.0.0"; 
        public const string CHANGELOG = VERSION + @"
== Rewrite Entire Bot
**This entire bot has been re-written**
It is now in .NET Core v3.1;
Any useless functions, modules or services have not been carried over.
The entire casino section has been dropped
";
        public static DiscordSocketClient Client { get; set; }
        public static IConfigurationRoot Configuration { get; set; }
        public static ServiceProvider Services { get; set; }
        public static CommandService Commands { get; set; }
        public static char Prefix { get; set; }

        public static Handler APIHandler { get; set; }

        public static Random RND { get; set; }

        #region Configuration Specific Settings

#if WINDOWS
        public const string BASE_PATH = @"D:\Bot\";
#else
        public const string BASE_PATH = @"/mnt/drive/bot/Data/";
#endif

        #endregion

        static void Main(string[] args)
        {
            Program.MainAsync().GetAwaiter().GetResult();
        }

        static void buildConfig()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(BASE_PATH);
            builder.AddJsonFile("_configuration.json");
            Configuration = builder.Build();

            Prefix = Configuration["prefix"][0];
        }

        public static async Task MainAsync()
        {
            Directory.SetCurrentDirectory(BASE_PATH);
            Program.LogMsg($"Starting bot with v{VERSION}");
            try
            {
                buildConfig();
            } catch (Exception ex)
            {
                LogMsg(ex, "Config");
                LogMsg("Failed to load configuration; we must exit.");
                Console.ReadLine();
                Environment.Exit(1);
                return;
            }
            using (Services = ConfigureServices())
            {
                var client = Services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;
                client.Ready += ClientReady;
                Commands = Services.GetRequiredService<CommandService>();
                Commands.Log += LogAsync;

                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hardcoding.
                await client.LoginAsync(TokenType.Bot, Configuration["tokens:discord"]);
                await client.StartAsync();

                // Here we initialize the logic required to register our commands.
                await Services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                await Task.Delay(-1);
            }
        }

        static ConsoleColor getColor(LogSeverity s)
        {
            return s switch
            {
                LogSeverity.Critical => ConsoleColor.Magenta,
                LogSeverity.Debug    => ConsoleColor.DarkGray,
                LogSeverity.Error    => ConsoleColor.Red,
                LogSeverity.Info     => ConsoleColor.White,
                LogSeverity.Verbose  => ConsoleColor.Gray,
                LogSeverity.Warning  => ConsoleColor.Yellow,
                _                    => ConsoleColor.Cyan,
            };
        }

        public static void LogMsg(LogMessage msg)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = getColor(msg.Severity);
            Console.WriteLine(msg.ToString());
            Console.ForegroundColor = c;
        }

        public static void LogMsg(Exception ex, string source) => LogMsg(new LogMessage(LogSeverity.Error, source, null, ex));

        public static void LogMsg(string message, string source = "App", LogSeverity sev = LogSeverity.Info)
            => LogMsg(new LogMessage(sev, source, message));

        private static Task LogAsync(LogMessage log)
        {
            LogMsg(log);
            return Task.CompletedTask;
        }

        private static ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {                                       
                    LogLevel = LogSeverity.Info,
                    MessageCacheSize = 1000            
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {                                       
                    LogLevel = LogSeverity.Debug,
                    DefaultRunMode = RunMode.Async,    
                    CaseSensitiveCommands = false       
                }))
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<InteractiveService>()
                .BuildServiceProvider();
        }

#region Save Info
            
        public static List<BotUser> Users { get; set; }
        public const string saveName = "new_bot_save.json";

        class BotSave
        {
            public List<BotUser> users;
        }

        public static void Load()
        {
            string content;
            try
            {
                content = File.ReadAllText(saveName);
            }
            catch (FileNotFoundException ex)
            {
                Program.LogMsg("Save file was not present, attempting to continue..", "Load", LogSeverity.Warning);
                content = "{}";
            }
            var save = JsonConvert.DeserializeObject<BotSave>(content);
            Users = save.users ?? new List<BotUser>();
        }


        private static async Task ClientReady()
        {
            Service.SendReady(); // TODO: remove ready?
            try
            {
                Load();
            }
            catch (Exception ex)
            {
                LogMsg(ex, "BotLoad");
                Environment.Exit(1);
                return;
            }
            try
            {
                APIHandler = new Handler();
                Handler.Start();
            } catch (Exception ex)
            {
                LogMsg(ex, "StartHandler");
                Environment.Exit(2);
                return;
            }
            Service.SendLoad();
        }
        #endregion
    }
}
