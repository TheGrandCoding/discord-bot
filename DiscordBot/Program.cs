using Discord;
using Interactivity;
using Discord.Commands;
using Discord.Rest;
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
using DiscordBot.Classes.Calender;
using System.ComponentModel;

[assembly: AssemblyVersion(DiscordBot.Program.VERSION)]
namespace DiscordBot
{
    public partial class Program
    {
        public const string VERSION = "0.20.5"; 
        public const string CHANGELOG = VERSION + @"
== Permissions changes
Changed how permissions worked for bot.
";
        public static DiscordSocketClient Client { get; set; }
        public static IConfigurationRoot Configuration { get; set; }
        public static ServiceProvider GlobalServices { get; set; }
        public static CommandService Commands { get; set; }
        public static char Prefix { get; set; }
        private static CancellationTokenSource endToken;

        public static RestApplication AppInfo { get; set; }
        public static LogSeverity LogLevel { get; set; } = LogSeverity.Verbose; // set through config: "settings:log"

        public static Handler APIHandler { get; set; }

        public static Random RND { get; set; } = new Random();

        public static bool ShouldServiceDownloads { get; set; } = false;
        public static bool ShouldSaveDownload { get; set; } = false;

        public int something = 0xff;

        #region Configuration Specific Settings

#if WINDOWS
        public const string BASE_PATH = @"D:\Bot\";
#else
#if DEBUG
        public const string BASE_PATH = @"/bot/DebugData/";
#else
        public const string BASE_PATH = @"/bot/Data/";
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



        static int Main(string[] args)
        {

            _braille = new List<char>();
            foreach (var chr in "⠀⠁⠂⠃⠄⠅⠆⠇⠈⠉⠊⠋⠌⠍⠎⠏⠐⠑⠒⠓⠔⠕⠖⠗⠘⠙⠚⠛⠜⠝⠞⠟⠠⠡⠢⠣⠤⠥⠦⠧⠨⠩⠪⠫⠬⠭⠮⠯⠰⠱⠲⠳⠴⠵⠶⠷⠸⠹⠺⠻⠼⠽⠾⠿⡀⡁⡂⡃⡄⡅⡆⡇⡈⡉⡊⡋⡌⡍⡎⡏⡐⡑⡒⡓⡔⡕⡖" +
    "⡗⡘⡙⡚⡛⡜⡝⡞⡟⡠⡡⡢⡣⡤⡥⡦⡧⡨⡩⡪⡫⡬⡭⡮⡯⡰⡱⡲⡳⡴⡵⡶⡷⡸⡹⡺⡻⡼⡽⡾⡿⢀⢁⢂⢃⢄⢅⢆⢇⢈⢉⢊⢋⢌⢍⢎⢏⢐⢑⢒⢓⢔⢕⢖⢗⢘⢙⢚⢛⢜⢝⢞⢟⢠⢡⢢⢣⢤⢥⢦⢧⢨⢩⢪⢫⢬⢭" +
    "⢮⢯⢰⢱⢲⢳⢴⢵⢶⢷⢸⢹⢺⢻⢼⢽⢾⢿⣀⣁⣂⣃⣄⣅⣆⣇⣈⣉⣊⣋⣌⣍⣎⣏⣐⣑⣒⣓⣔⣕⣖⣗⣘⣙⣚⣛⣜⣝⣞⣟⣠⣡⣢⣣⣤⣥⣦⣧⣨⣩⣪⣫⣬⣭⣮⣯⣰⣱⣲⣳⣴⣵⣶⣷⣸⣹⣺⣻⣼⣽⣾⣿")
                _braille.Add(chr);
            endToken = new CancellationTokenSource();
            bool hasAttemptedCancel = false;
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
            {
                Console.WriteLine("[Console:CancelKeyPress]");
                if(hasAttemptedCancel)
                {
                    Console.WriteLine("Force closing because you tried it twice.");
                    Environment.Exit(1);
                    return;
                }
                hasAttemptedCancel = true;
                e.Cancel = true; // we'll handle this in our own time
                Program.Close(0);
            };
            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => 
            {
                Console.WriteLine("[ProcessExit]");
                Program.Close(0);
            };
            var code = Program.MainAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Exiting with code {code}");
            Environment.Exit(code);
            return code;
        }

        public static CancellationToken GetToken() => endToken.Token;

        public static void buildConfig()
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

        static HttpResponseMessage fetchAuthedRequest(string url, string passwd)
        {
            var client = GlobalServices.GetRequiredService<BotHttpClient>();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var authValue = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"bot:{passwd}")));
            request.Headers.Authorization = authValue;
            return client.SendAsync(request).Result;
        }
#if WINDOWS
        static void fetchFile(string fName)
        {
            var remote = Configuration["urls:download"];
            var authPwd = Configuration["tokens:download"];
            var fullRemote = string.Format(remote, fName);
            var response = fetchAuthedRequest(fullRemote, authPwd);
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

            // If any new files are present, load them
            var folder = Path.Combine(BASE_PATH, "Saves");
            var newFiles = Directory.GetFiles(folder, "*.new");
            foreach (var file in newFiles)
            {
                Program.LogInfo($"Loading new save file {Path.GetFileName(file)}", "FetchService");
                File.Move(file, file.Replace(".new", ""), true);
            }
#if !DEBUG
            
            return;
#endif
            var client = GlobalServices.GetRequiredService<BotHttpClient>();
            bool success = false;
            try
            {
                var remote = Configuration["urls:download"];
                var authPwd = Configuration["tokens:download"];
                var response = fetchAuthedRequest(string.Format(remote, ""), authPwd);
                success = response.IsSuccessStatusCode;
                Program.LogWarning($"{response.StatusCode} {response.ReasonPhrase}", "fetchService");
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "fetchService");
                success = false;
            }
            if (!success)
            {
                Program.LogWarning("Skipping fetching service files as it is not set up.", "fetchService");
                return;
            }
            List<Service> savedServices;
            if (ShouldServiceDownloads)
            {
                savedServices = services.Where(x => x is SavedService).ToList();
            } else
            {
                savedServices = services.Where(x => x is SavedService 
                    && x.GetType().GetCustomAttribute<AlwaysSyncAttribute>() != null)
                    .ToList();
            }
            var files = savedServices.Select(x => ((SavedService)x).SaveFile).ToList();
            if(ShouldSaveDownload)
                files.Add(saveName);
            foreach(var x in files)
            {
                fetchFile(x);
            }
        }

        static bool inheritsGeneric(Type generic, Type obj)
        {
            Type type2 = obj; // type is your type, like typeof(ClassA)

            while (type2 != null)
            {
                if (type2.IsGenericType &&
                    type2.GetGenericTypeDefinition() == generic)
                {
                    return true;
                }
                type2 = type2.BaseType;
            }
            return false;
        }

        public static async Task<int> MainAsync()
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
                return 1;
            }
            using (GlobalServices = ConfigureServices())
            {
                var client = GlobalServices.GetRequiredService<DiscordSocketClient>();
                Program.Client = client;
                client.Log += LogAsync;
                client.Ready += ClientReady;
                Commands = GlobalServices.GetRequiredService<CommandService>();
                Commands.Log += LogAsync;

                var slsh = new Discord.Interactions.InteractionService(client, new Discord.Interactions.InteractionServiceConfig()
                {
                    
                });

                slsh.Log += LogAsync;
                var genericType = typeof(BotTypeReader<>);
                foreach (Type type in
                    Assembly.GetAssembly(genericType).GetTypes()
                    .Where(myType => myType.IsClass && !myType.IsAbstract 
                        && inheritsGeneric(genericType, myType.BaseType)))
                {
                    dynamic instance = Activator.CreateInstance(type);
                    instance.Register(Commands);
                }

                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hardcoding.
                await client.LoginAsync(TokenType.Bot, Configuration["tokens:discord"]);
                await client.StartAsync();

                // Here we initialize the logic required to register our commands.
                await GlobalServices.GetRequiredService<CommandHandlingService>().InitializeAsync(slsh);
                await slsh.AddModulesAsync(Assembly.GetEntryAssembly(), GlobalServices);

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
            return exitCode ?? 0;
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

        private static ServiceProvider ConfigureServices()
        {
            var coll = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
#if DEBUG
                    LogLevel = LogSeverity.Debug,
#else
                    LogLevel = LogSeverity.Info,
#endif
                    MessageCacheSize = 1000,
                    AlwaysDownloadUsers = true,
                    GatewayIntents = GatewayIntents.All
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    LogLevel = LogSeverity.Debug,
                    DefaultRunMode = Discord.Commands.RunMode.Async,
                    CaseSensitiveCommands = false
                }))
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<InteractivityService>()
                .AddSingleton(new InteractivityConfig()
                {
                    RunOnGateway = false,
                    DefaultTimeout = TimeSpan.FromSeconds(30)
                });
            coll.AddDbContext<LogContext>(ServiceLifetime.Scoped);
            coll.AddDbContext<BotDbContext>(ServiceLifetime.Scoped);
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
            coll.AddDbContext<TimeTrackDb>(ServiceLifetime.Transient);
            coll.AddDbContext<CalenderDb>(ServiceLifetime.Transient);
            coll.AddDbContext<FoodDbContext>(ServiceLifetime.Transient);
            coll.AddDbContext<HoursDbContext>(ServiceLifetime.Transient);

            var yClient = new Google.Apis.YouTube.v3.YouTubeService(new Google.Apis.Services.BaseClientService.Initializer()
            {
                ApiKey = Program.Configuration["tokens:youtubeApi"],
                ApplicationName = "mlapiBot",
            });
            coll.AddSingleton<YouTubeService>(yClient);

            var httpHandler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            var botHttp = new BotHttpClient(httpHandler);
            coll.AddSingleton(typeof(BotHttpClient), botHttp);
            coll.AddSingleton<HttpClient>((p) =>
            {
                var h = p.GetRequiredService<BotHttpClient>();
                return new HttpClient(h.Child("scoped"));
            });
            foreach(var service in ReflectiveEnumerator.GetEnumerableOfType<Service>(null))
                coll.AddSingleton(service.GetType());
            return coll.BuildServiceProvider();
        }

        private static int? exitCode = null;
        public static void Close(int code)
        {
            exitCode = code;
            if(Service.GlobalState != ServiceState.None && Service.GlobalState != ServiceState.Failed)
            {
                Service.SendClose();
                Program.Save(true);
            }
            endToken.Cancel();
        }

#region Save Info
        public static uint CommandVersions { get; set; } = 0;
        public const string saveName = "new_bot_save.json";

        class BotSave
        {
            public Dictionary<string, int> states;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(0)]
            public uint? commandVersion;
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
            states = save.states ?? new Dictionary<string, int>();
            CommandVersions = save.commandVersion ?? 0;
        }

        public static void Save(bool saveServices = false)
        {
            var bSave = new BotSave()
            {
                states = states,
                commandVersion = CommandVersions
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
            //Client.SetGameAsync($"code v{Program.VER_STR}");
            var servicesTypes = ReflectiveEnumerator.GetEnumerableOfType<Service>(null).Select(x => x.GetType());
            var services = new List<Service>();
            foreach (var type in servicesTypes)
            {
                var req = (Service)Program.GlobalServices.GetRequiredService(type);
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
            using (var _scope = GlobalServices.CreateScope())
            {
                var db = _scope.ServiceProvider.GetBotDb("ProgramReady"); // disposed by scope
                try
                {
                    var owner = Client.GetApplicationInfoAsync().Result.Owner;
                    var bUser = db.GetUserFromDiscord(owner, true).Result.Value;
                    if (bUser != null)
                    {
                        bUser.Approved = true;
                        bUser.Verified = true;
                        var perm = Perm.Parse(Perms.Bot.All);
                        if (!PermChecker.UserHasPerm(bUser, perm))
                        {
                            bUser.WithPerm(perm);
                        }
                        db.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex, "SetOwnerDev");
                }
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
            Program.Save(true); // for some DailyValidationFailed things.
            Program.Save(); // for some DailyValidationFailed things.
            timerOverall.Stop();
            try
            {
                SendLogMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("Started v" + VER_STR)
                    .WithDescription($"Bot launched in {timerOverall.ElapsedMilliseconds}ms")
                    .Build()).Wait();
            }
            catch { }
        }

        private static async Task ClientReady()
        {
            AppInfo = await Client.GetApplicationInfoAsync();
            
            var th = new Thread(runStartups);
            th.Name = "clientReady";
            th.Start();
        }

        public static bool ignoringCommands = false;

        static async Task<IMessageChannel> getLogChannel()
        {
            IMessageChannel channel;
            var settings = Program.Configuration["settings:logchannel"];
            if(!string.IsNullOrWhiteSpace(settings) && ulong.TryParse(settings, out var settingId))
            {
                channel = Program.Client.GetChannel(settingId) as IMessageChannel;
            } else
            {
                channel = await Program.AppInfo.Owner.CreateDMChannelAsync();
            }
            return channel;
        }

        public static async Task<IUserMessage> SendLogMessageAsync(string content = null, Embed embed = null)
        {
            if (content == null && embed == null)
                throw new ArgumentException("One of content or embed must be provided");
            var channel = await getLogChannel();
            return await channel.SendMessageAsync(content, embed: embed);
        }

        public static async Task<IUserMessage> SendLogFileAsync(string path, string content = null, Embed embed = null, IMessageChannel channel = null)
        {
            if (content == null && embed == null)
                throw new ArgumentException("One of content or embed must be provided");
            channel ??= await getLogChannel();
            return await channel.SendFileAsync(path, text: content, embed: embed);
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
