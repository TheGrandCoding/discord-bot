using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Calculator;
using DiscordBot.Classes.Chess;
using DiscordBot.MLAPI;
using DiscordBot.Permissions;
using DiscordBot.Services;
using DiscordBot.TypeReaders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: AssemblyVersion(DiscordBot.Program.VERSION)]
namespace DiscordBot
{
    public partial class Program
    {
        public const string VERSION = "0.12.2"; 
        public const string CHANGELOG = VERSION + @"
== Permissions changes
Changed how permissions worked for bot.
";
        public static DiscordSocketClient Client { get; set; }
        public static IConfigurationRoot Configuration { get; set; }
        public static ServiceProvider Services { get; set; }
        public static CommandService Commands { get; set; }
        public static char Prefix { get; set; }

        public static RestApplication AppInfo { get; set; }
        public static LogSeverity LogLevel { get; set; } = LogSeverity.Verbose; // set through config: "settings:log"

        public static Handler APIHandler { get; set; }

        public static Random RND { get; set; } = new Random();

        public static bool ShouldDownload { get; set; } = false;

        public int something = 0xff;

        #region Configuration Specific Settings

#if WINDOWS
        public const string BASE_PATH = @"D:\Bot\";
#else
#if DEBUG
        public const string BASE_PATH = @"/mnt/drive/bot/DebugData/";
#else
        public const string BASE_PATH = @"/mnt/drive/bot/Data/";
#endif
#endif

#if DEBUG
        public static bool BOT_DEBUG = true;
        public static string VER_STR = VERSION + "-dev";
#else
        public static bool BOT_DEBUG = false;
        public static string VER_STR = VERSION;
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
            LogLevel = Enum.Parse<LogSeverity>(Configuration["settings:log"], true);
            Prefix = Configuration["prefix"][0];
        }

#if WINDOWS
        static void fetchFile(string fName)
        {
            var client = Services.GetRequiredService<HttpClient>();
            var remote = Configuration["urls:download"];
            var authPwd = Configuration["tokens:download"];
            var fullRemote = string.Format(remote, fName);
            var request = new HttpRequestMessage(HttpMethod.Get, fullRemote);
            var authValue = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"bot:{authPwd}")));
            request.Headers.Authorization = authValue;
            var response = client.SendAsync(request).Result;
            var text = response.Content.ReadAsStringAsync().Result;
            if(!response.IsSuccessStatusCode)
            {
                Program.LogMsg($"Failed to download: {response.StatusCode} {text}", LogSeverity.Error, fName);
                return;
            }
            var local = Path.Combine(BASE_PATH, "Saves", fName + ".new");
            File.WriteAllText(local, text);
            long length = new System.IO.FileInfo(local).Length;
            Program.LogMsg($"Downloaded {length / 1000}kB", LogSeverity.Debug, fName);
        }
#else
        static void fetchFile(string fName)
        {
            var from = string.Format(Configuration["urls:download"], fName);
            if(!File.Exists(from))
            {
                Program.LogMsg($"File does not exist", LogSeverity.Error, fName);
                return;
            }
            var to = Path.Join(BASE_PATH, "Saves", fName + ".new");
            File.Copy(from, to, true);
            Program.LogMsg("Copied for debug use", LogSeverity.Debug, fName);
        }
#endif

        static void fetchServiceFiles(List<Service> services)
        {
#if !DEBUG
            // Release shouldn't be downloading the files.. from itself
            return;
#endif
            if (!ShouldDownload)
                return;
            var savedServices = services.Where(x => x is SavedService);
            var files = savedServices.Select(x => ((SavedService)x).SaveFile).ToList();
            files.Add(saveName);
            foreach(var x in files)
            {
                fetchFile(x);
            }
        }

        public static async Task MainAsync()
        {
            Program.LogMsg($"Starting bot with v{VER_STR}");
            Directory.SetCurrentDirectory(BASE_PATH);
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
                Program.Client = client;
                client.Log += LogAsync;
                client.Ready += ClientReady;
                Commands = Services.GetRequiredService<CommandService>();
                Commands.Log += LogAsync;
                var genericType = typeof(BotTypeReader<>);
                foreach (Type type in
                    Assembly.GetAssembly(genericType).GetTypes()
                    .Where(myType => myType.IsClass && !myType.IsAbstract 
                        && myType.BaseType.IsGenericType && myType.BaseType.GetGenericTypeDefinition() == genericType))
                {
                    dynamic instance = Activator.CreateInstance(type);
                    instance.Register(Commands);
                }

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

        private static object _lockObj = new object();
        static string logFileLocation {  get
            {
                var directory = Path.Combine(BASE_PATH, "data", "logs");
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                return Path.Combine(directory, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
            } }
        static void fileLog(LogMessage msg)
        {
            lock(_lockObj)
            {
                string s = formatMsg(msg);
                File.AppendAllText(logFileLocation, s + "\r\n");
            }
        }

        static string formatMsg(LogMessage msg)
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("[HH:mm:ss.fff] "));
            sb.Append($"<{msg.Severity.ToString().PadRight(8)}|{(msg.Source ?? "n/s").PadRight(18)}> ");
            int padLength = sb.Length + 1;
            var s = msg.Exception?.ToString() ?? msg.Message ?? "n/m";
            sb.Append(s.Replace("\n", "\n" + new string(' ', padLength)));
            return sb.ToString();
        }

        public static void LogMsg(LogMessage msg)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = getColor(msg.Severity);
            Console.WriteLine(formatMsg(msg));
            Console.ForegroundColor = c;
            if (msg.Severity < LogLevel)
                return;
            fileLog(msg);
        }

        public static void LogMsg(Exception ex, string source) => LogMsg(new LogMessage(LogSeverity.Error, source, null, ex));
        public static void LogMsg(string source, Exception ex) => LogMsg(ex, source);
        public static void LogMsg(string message, LogSeverity sev = LogSeverity.Info, string source = "App")
            => LogMsg(new LogMessage(sev, source, message));

        private static Task LogAsync(LogMessage log)
        {
            LogMsg(log);
            return Task.CompletedTask;
        }

        public static string getDbString(string database)
        {
            var config = Configuration["tokens:db"];
            return string.Format(config, database);
        }

        private static ServiceProvider ConfigureServices()
        {
            var coll = new ServiceCollection()
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
                .AddSingleton<InteractiveService>();
            coll.AddDbContext<LogContext>(ServiceLifetime.Transient);
            coll.AddDbContext<ChessDbContext>(options =>
            {
#if WINDOWS
                options.UseSqlServer(getDbString("chsData"));
                options.EnableSensitiveDataLogging();
#else
                options.UseMySql(getDbString("chsData"), 
                    new MariaDbServerVersion(new Version(10, 3, 25)), mysqlOptions =>
                {
                    mysqlOptions.CharSet(CharSet.Utf8Mb4);
                });
#endif
            }, ServiceLifetime.Transient);
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", $"dsMLAPI-v{VER_STR}");
            coll.AddSingleton(typeof(HttpClient), http);
            foreach(var service in ReflectiveEnumerator.GetEnumerableOfType<Service>(null))
                coll.AddSingleton(service.GetType());
            return coll.BuildServiceProvider();
        }

        public static void Close(int code)
        {
            Service.SendClose();
            Program.Save(true);
            Environment.Exit(code);
        }

#region Save Info
        public static List<BotUser> Users { get; set; }
        public const string saveName = "new_bot_save.json";

        class BotSave
        {
            public List<BotUser> users;
            public Dictionary<string, int> states;
        }

        public static void Load()
        {
            string content;
            try
            {
                content = File.ReadAllText(Path.Combine(BASE_PATH, "Saves", saveName));
            }
            catch (FileNotFoundException ex)
            {
                Program.LogMsg("Save file was not present, attempting to continue..", LogSeverity.Warning, "Load");
                content = "{}";
            }
            var save = JsonConvert.DeserializeObject<BotSave>(content);
            Users = save.users ?? new List<BotUser>();
            states = save.states ?? new Dictionary<string, int>();
        }

        public static void Save(bool saveServices = false)
        {
            var bSave = new BotSave()
            {
                users = Users,
                states = states,
            };
            var str = JsonConvert.SerializeObject(bSave);
            File.WriteAllText(Path.Combine(BASE_PATH, "Saves", saveName), str);
            if(saveServices)
            {
                Service.SendSave();
            }
        }

        static void runStartups()
        {
            Client.SetActivityAsync(new Game($"code v{Program.VER_STR}", ActivityType.Playing));
            var servicesTypes = ReflectiveEnumerator.GetEnumerableOfType<Service>(null).Select(x => x.GetType());
            var services = new List<Service>();
            foreach (var type in servicesTypes)
            {
                var req = (Service)Program.Services.GetRequiredService(type);
                services.Add(req);
            }
            fetchServiceFiles(services);
            Service.SendReady(services);
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
                var owner = Client.GetApplicationInfoAsync().Result.Owner.Id;
                var bUser = GetUserOrDefault(owner);
                if(bUser != null)
                {
                    var perm = Perm.Parse(Perms.Bot.All);
                    if(!PermChecker.UserHasPerm(bUser, perm))
                    {
                        bUser.Permissions.Add(perm);
                        Program.Save();
                    }
                }
            } catch (Exception ex)
            {
                LogMsg(ex, "SetOwnerDev");
            }
            Service.SendLoad();
            try
            {
                APIHandler ??= new Handler();
                Handler.Start();
            }
            catch (Exception ex)
            {
                LogMsg(ex, "StartHandler");
                Environment.Exit(2);
                return;
            }
            Program.Save(); // for some DailyValidationFailed things.
        }

        private static async Task ClientReady()
        {
            AppInfo = await Client.GetApplicationInfoAsync();
            var th = new Thread(runStartups);
            th.Name = "clientReady";
            th.Start();
        }
#endregion

#region AntiRepeat Functions

        static Dictionary<string, int> states = new Dictionary<string, int>();

        /// <summary>
        /// Ensures that functions are only called once per day (even persistant through bot restarts)
        /// </summary>
        /// <returns>True if function FAILS, and should NOT be called; False if function call is valid</returns>
        public static bool DailyValidateFailed()
        {
            var stack = new StackTrace(1, false); // skips this function call
            var frames = stack.GetFrames();
            var frame = frames.First();
            var method = frame.GetMethod();
            var parent = method.DeclaringType.ReflectedType ?? method.DeclaringType;
            string name = $"{parent.Name}:{method.Name}";
            bool val = true;
            if(states.TryGetValue(name, out int v))
            {
                val = v != DateTime.Now.DayOfYear;
            }
            states[name] = DateTime.Now.DayOfYear;
            return val;
        }

#endregion
    }
}
