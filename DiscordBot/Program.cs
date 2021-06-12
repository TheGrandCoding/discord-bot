using Discord;
using Interactivity;
using Discord.Commands;
using Discord.Rest;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using DiscordBot.Classes.Chess;
using DiscordBot.MLAPI;
using DiscordBot.MLAPI.Modules.TimeTracking;
using DiscordBot.Permissions;
using DiscordBot.Services;
using DiscordBot.TypeReaders;
using DiscordBot.Utils;
using Google.Apis.YouTube.v3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure; // this is needed!
using System;
using System.Collections.Concurrent;
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
        public const string VERSION = "0.18.0"; 
        public const string CHANGELOG = VERSION + @"
== Permissions changes
Changed how permissions worked for bot.
";
        public static DiscordSocketClient Client { get; set; }
        public static IConfigurationRoot Configuration { get; set; }
        public static ServiceProvider Services { get; set; }
        public static CommandService Commands { get; set; }
        public static char Prefix { get; set; }
        private static CancellationTokenSource endToken;

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
            _braille = new List<char>();
            foreach (var chr in "⠀⠁⠂⠃⠄⠅⠆⠇⠈⠉⠊⠋⠌⠍⠎⠏⠐⠑⠒⠓⠔⠕⠖⠗⠘⠙⠚⠛⠜⠝⠞⠟⠠⠡⠢⠣⠤⠥⠦⠧⠨⠩⠪⠫⠬⠭⠮⠯⠰⠱⠲⠳⠴⠵⠶⠷⠸⠹⠺⠻⠼⠽⠾⠿⡀⡁⡂⡃⡄⡅⡆⡇⡈⡉⡊⡋⡌⡍⡎⡏⡐⡑⡒⡓⡔⡕⡖" +
    "⡗⡘⡙⡚⡛⡜⡝⡞⡟⡠⡡⡢⡣⡤⡥⡦⡧⡨⡩⡪⡫⡬⡭⡮⡯⡰⡱⡲⡳⡴⡵⡶⡷⡸⡹⡺⡻⡼⡽⡾⡿⢀⢁⢂⢃⢄⢅⢆⢇⢈⢉⢊⢋⢌⢍⢎⢏⢐⢑⢒⢓⢔⢕⢖⢗⢘⢙⢚⢛⢜⢝⢞⢟⢠⢡⢢⢣⢤⢥⢦⢧⢨⢩⢪⢫⢬⢭" +
    "⢮⢯⢰⢱⢲⢳⢴⢵⢶⢷⢸⢹⢺⢻⢼⢽⢾⢿⣀⣁⣂⣃⣄⣅⣆⣇⣈⣉⣊⣋⣌⣍⣎⣏⣐⣑⣒⣓⣔⣕⣖⣗⣘⣙⣚⣛⣜⣝⣞⣟⣠⣡⣢⣣⣤⣥⣦⣧⣨⣩⣪⣫⣬⣭⣮⣯⣰⣱⣲⣳⣴⣵⣶⣷⣸⣹⣺⣻⣼⣽⣾⣿")
                _braille.Add(chr);
            endToken = new CancellationTokenSource();
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
            {
                Console.WriteLine("[Console:CancelKeyPress]");
                Thread.Sleep(1000);
                Program.Close(0);
                Thread.Sleep(2500);
                Environment.Exit(0);
            };
            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => 
            {
                Console.WriteLine("[ProcessExit]");
                Program.Close(1);
            };
            Program.MainAsync().GetAwaiter().GetResult();
        }

        public static CancellationToken GetToken() => endToken.Token;

        static void buildConfig()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(BASE_PATH);
            builder.AddJsonFile("_configuration.json");
            Configuration = builder.Build();
            var other = Path.Combine(BASE_PATH, "google-mlapi.json");
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", other);
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
                Program.LogError($"Failed to download: {response.StatusCode} {text}", fName);
                return;
            }
            var local = Path.Combine(BASE_PATH, "Saves", fName + ".new");
            File.WriteAllText(local, text);
            long length = new System.IO.FileInfo(local).Length;
            Program.LogDebug($"Downloaded {length / 1000}kB", fName);
        }
#else
        static void fetchFile(string fName)
        {
            var from = string.Format(Configuration["urls:download"], fName);
            if(!File.Exists(from))
            {
                Program.LogError($"File does not exist", fName);
                return;
            }
            var to = Path.Join(BASE_PATH, "Saves", fName + ".new");
            File.Copy(from, to, true);
            Program.LogDebug("Copied for debug use", fName);
        }
#endif

        static void fetchServiceFiles(List<Service> services)
        {
#if !DEBUG
            // Release shouldn't be downloading the files.. from itself
            return;
#endif
            List<Service> savedServices;
            if (ShouldDownload)
            {
                savedServices = services.Where(x => x is SavedService).ToList();
            } else
            {
                savedServices = services.Where(x => x is SavedService 
                    && x.GetType().GetCustomAttribute<AlwaysSyncAttribute>() != null)
                    .ToList();
            }
            var files = savedServices.Select(x => ((SavedService)x).SaveFile).ToList();
            files.Add(saveName);
            foreach(var x in files)
            {
                fetchFile(x);
            }
        }

        public static async Task MainAsync()
        {
            Log += consoleLog;
            Log += fileLog;
            Log += cacheLogs;

            Program.LogInfo($"Starting bot with v{VER_STR}", "App");

            Directory.SetCurrentDirectory(BASE_PATH);
            try
            {
                buildConfig();
            } catch (Exception ex)
            {
                LogError(ex, "Config");
                LogError("Failed to load configuration; we must exit.", "App");
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
                var slsh = Services.GetRequiredService<SlashCommandService>();
                slsh.Log += LogAsync;
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
                await slsh.AddModulesAsync(Assembly.GetEntryAssembly(), Services);

                try
                {
                    await Task.Delay(-1, endToken.Token);
                } catch(Exception ex)
                {
                    Console.WriteLine("MainAsync is ending.");
                    await client.LogoutAsync();
                    await client.StopAsync();
                    client?.Dispose();
                    Console.WriteLine("MainAsync has ended.");
                }
            }
        }

        public struct LogWithTime
        {
            public LogWithTime(LogSeverity severity, string source, string message, Exception exception = null)
            {
                Source = source;
                Severity = severity;
                Message = message;
                Exception = exception;
                When = DateTime.Now;
            }
            public string Source { get; set; }
            public string Message { get; set; }
            public LogSeverity Severity { get; set; }
            public Exception Exception { get; set; }
            public DateTime When { get; set; }

            public static implicit operator LogWithTime(LogMessage msg)
            {
                return new LogWithTime(msg.Severity, msg.Source, msg.Message, msg.Exception);
            }
        }

        public static ConcurrentQueue<LogWithTime> lastLogs = new ConcurrentQueue<LogWithTime>();
        static void cacheLogs(object sender, LogMessage msg)
        {
            lastLogs.Enqueue(msg);
            if (lastLogs.Count > 100)
                lastLogs.TryDequeue(out _);
        }

        public static ConsoleColor getColor(LogSeverity s)
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

        public static object _lockObj = new object();
        static string logFileLocation {  get
            {
                var directory = Path.Combine(BASE_PATH, "data", "logs");
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                return Path.Combine(directory, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
            } }
        static void fileLog(object sender, LogMessage msg)
        {
            if ((int)msg.Severity > (int)LogLevel)
                return;
            lock (_lockObj)
            {
                string s = FormatLogMessage(msg);
                File.AppendAllText(logFileLocation, s + "\r\n");
            }
        }

        public static string FormatLogMessage(LogWithTime msg)
        {
            var sb = new StringBuilder();
            sb.Append(msg.When.ToString("[HH:mm:ss.fff] "));
            sb.Append($"<{msg.Severity.ToString().PadRight(8)}|{(msg.Source ?? "n/s").PadRight(18)}> ");
            int padLength = sb.Length + 1;
            var s = msg.Exception?.ToString() ?? msg.Message ?? "n/m";
            sb.Append(s.Replace("\n", "\n" + new string(' ', padLength)));
            return sb.ToString();
        }

        static void consoleLog(object sender, LogMessage msg)
        {
            var c = Console.ForegroundColor;
            Console.ForegroundColor = getColor(msg.Severity);
            Console.WriteLine(FormatLogMessage(msg));
            Console.ForegroundColor = c;
        }

        public static event EventHandler<LogMessage> Log;
        public static void LogMsg(LogMessage msg)
        {
            Log?.Invoke(null, msg);
        }

        public static void LogDebug(string message, string source, Exception error = null) => LogMsg(new LogMessage(LogSeverity.Debug, source, message, error));
        public static void LogVerbose(string message, string source, Exception error = null) => LogMsg(new LogMessage(LogSeverity.Verbose, source, message, error));
        public static void LogInfo(string message, string source, Exception error = null) => LogMsg(new LogMessage(LogSeverity.Info, source, message, error));
        public static void LogWarning(string message, string source, Exception error = null) => LogMsg(new LogMessage(LogSeverity.Warning, source, message, error));
        public static void LogError(Exception error, string source) => LogMsg(new LogMessage(LogSeverity.Error, source, null, error));
        public static void LogError(string message, string source, Exception error = null) => LogMsg(new LogMessage(LogSeverity.Error, source, message, error));
        public static void LogCritical(string message, string source, Exception error = null) => LogMsg(new LogMessage(LogSeverity.Critical, source, message, error));



        private static Task LogAsync(LogMessage log)
        {
            LogMsg(log);
            return Task.CompletedTask;
        }

        public static string getDbString(string database)
        {
#if DEBUG
            var config = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog={0};Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
#else
            var config = Configuration["tokens:db"];
#endif
            return string.Format(config, database);
        }

        private static ServiceProvider ConfigureServices()
        {
            var coll = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Debug,
                    MessageCacheSize = 1000,
                    AlwaysAcknowledgeInteractions = false,
                    AlwaysDownloadUsers = true,
                    GatewayIntents = GatewayIntents.All
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    LogLevel = LogSeverity.Debug,
                    DefaultRunMode = RunMode.Async,
                    CaseSensitiveCommands = false
                }))
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<InteractivityService>()
                .AddSingleton(new InteractivityConfig()
                {
                    RunOnGateway = false,
                    DefaultTimeout = TimeSpan.FromSeconds(30)
                });
            coll.AddSingleton(new SlashCommandService());
            coll.AddDbContext<LogContext>(ServiceLifetime.Transient);
#if INCLUDE_CHESS
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
#endif
            coll.AddDbContext<TimeTrackDb>(options =>
            {
#if WINDOWS
                options.UseSqlServer(getDbString("watch"));
                options.EnableSensitiveDataLogging();
#else
                options.UseMySql(getDbString("watch"), 
                    new MariaDbServerVersion(new Version(10, 3, 25)), mysqlOptions =>
                    {
                        mysqlOptions.CharSet(CharSet.Utf8Mb4);
                    });
#endif
            }, ServiceLifetime.Transient);
            var yClient = new Google.Apis.YouTube.v3.YouTubeService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = Program.Configuration["tokens:youtubeApi"],
                ApplicationName = "mlapiBot",
            });
            coll.AddSingleton<YouTubeService>(yClient);
            var http = new Utils.BotHttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", $"dsMLAPI-v{VER_STR}");
            coll.AddSingleton(typeof(HttpClient), http);
            coll.AddSingleton(typeof(BotHttpClient), http);
            foreach(var service in ReflectiveEnumerator.GetEnumerableOfType<Service>(null))
                coll.AddSingleton(service.GetType());
            return coll.BuildServiceProvider();
        }

        public static void Close(int code)
        {
            if(Service.GlobalState != ServiceState.None)
            {
                Service.SendClose();
                Program.Save(true);
            }
            endToken.Cancel();
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
                Program.LogWarning("Save file was not present, attempting to continue..", "Load");
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
            var timerOverall = new Stopwatch();
            timerOverall.Start();
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
                LogError(ex, "BotLoad");
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
                LogError(ex, "SetOwnerDev");
            }
            Service.SendLoad();
            try
            {
                APIHandler ??= new Handler();
                Handler.Start();
            }
            catch (Exception ex)
            {
                LogError(ex, "StartHandler");
                Environment.Exit(2);
                return;
            }
            Program.Save(); // for some DailyValidationFailed things.
            timerOverall.Stop();
#if !DEBUG
            try
            {
                AppInfo.Owner.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("Started v" + VER_STR)
                    .WithDescription($"Bot launched in {timerOverall.ElapsedMilliseconds}ms")
                    .Build());
            }
            catch { }
#endif
        }

        private static async Task ClientReady()
        {
            AppInfo = await Client.GetApplicationInfoAsync();
            var slsh = Services.GetRequiredService<SlashCommandService>();
            var components = Services.GetRequiredService<MessageComponentService>();
#if !DEBUG
            var guildIds = new List<ulong>() { 420240046428258304 };
#else
            var guildIds = Client.Guilds.Select(x => x.Id).ToList();
#endif
            await slsh.RegisterCommandsAsync(Client, guildIds, new CommandRegistrationOptions(OldCommandOptions.DELETE_UNUSED, ExistingCommandOptions.OVERWRITE));
            Client.InteractionCreated += (SocketInteraction x) =>
            {
                var task = Task.Run(() => executeInteraction(x));
                task.ContinueWith(err =>
                {
                    LogError($"Failed to execute interaction: {err}", "Interaction");
                }, TaskContinuationOptions.OnlyOnFaulted);
                return Task.CompletedTask;
            };
            var th = new Thread(runStartups);
            th.Name = "clientReady";
            th.Start();
        }

        public static bool ignoringCommands = false;

        static async Task executeInteraction(SocketInteraction x)
        {
            if (ignoringCommands)
                return;
            var slsh = Program.Services.GetRequiredService<SlashCommandService>();
            var components = Program.Services.GetRequiredService<MessageComponentService>();
            try
            {
                IResult result;
                if (x.Type == InteractionType.ApplicationCommand)
                {
                    Program.LogDebug($"Executing slash command {x.Id}", "Interactions");
                    result = await slsh.ExecuteAsync(x as SocketSlashCommand, Program.Services).ConfigureAwait(false);
                }
                else if (x.Type == InteractionType.MessageComponent)
                {
                    Program.LogDebug($"Executing message componenet {x.Id}", "Interactions");
                    result = await components.ExecuteAsync(x as SocketMessageComponent).ConfigureAwait(false);
                }
                else
                {
                    Program.LogInfo($"Unknown interaction type: {x.Type} {(int)x.Type}", "Interactions");
                    result = MiscResult.FromError("Unknown interaction type");
                }
                Program.LogInfo($"Executed interaction {x.Id}: {result.IsSuccess} {result.Error} {result.ErrorReason}", "Interactions");
                if (!result.IsSuccess)
                {
                    if (result is ExecuteResult exec && exec.Exception != null)
                    {
                        Program.LogError(exec.Exception, "InteractionInvoke");
                        try
                        {
                            await x.RespondAsync(":x: Internal exception occured whilst handling this interaction: " + exec.Exception.Message);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError($"{x.Id} {x.User?.Id ?? 0} {ex}", "InteractionCreated");
                try
                {
                    await x.RespondAsync($":x: Encountered an internal error attempting that command: {ex.Message}");
                }
                catch
                {
                    try
                    {
                        await x.FollowupAsync($":x: Encountered an internal error attempting that command: {ex.Message}");
                    }
                    catch { }
                }
            }
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
            bool val = false;
            if(states.TryGetValue(name, out int v))
            {
                val = v == DateTime.Now.DayOfYear;
            }
            states[name] = DateTime.Now.DayOfYear;
            return val;
        }

#endregion
    }
}
