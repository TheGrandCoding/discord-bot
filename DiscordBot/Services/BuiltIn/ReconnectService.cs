using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services.BuiltIn
{
    public class ReconnectService : Service
    {
        private DiscordSocketClient _discord;
        private CancellationTokenSource _cancelSource;
        private TaskCompletionSource<bool> _connectResult;
        private Thread _thread;

        public override void OnReady(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _discord.Disconnected += (Exception ex) =>
            {
                Info($"Disconnected with: {ex}");
                _discord.StopAsync();
                if(_thread == null)
                {
                    _cancelSource = new CancellationTokenSource();
                    _thread = new Thread(reconnectThread);
                    _thread.Start();
                } else
                {
                    _connectResult?.SetResult(false);
                }
                return Task.CompletedTask;
            };
            _discord.Connected += () =>
            {
                Info($"Connected");
                _connectResult.SetResult(true);
                _cancelSource?.Cancel();
                _thread = null;
                return Task.CompletedTask;
            };
        }

        private void reconnectThread()
        {

            int attempts = 0;
            while(_discord.ConnectionState != Discord.ConnectionState.Connected && !_cancelSource.Token.IsCancellationRequested)
            {
                try
                {
                    int delay = (1000 * (int)Math.Pow(2, attempts)) + Program.RND.Next(-250, 250);
                    Info($"Will attempt reconnect at {DateTime.Now.AddMilliseconds(delay)}");
                    Task.Delay(delay, _cancelSource.Token).Wait();
                    Info($"Attempting reconnect {attempts}");
                    _connectResult = new TaskCompletionSource<bool>();
                    _discord.StopAsync();
                    _discord.StartAsync();
                    if(!_connectResult.Task.Wait(10000, _cancelSource.Token))
                    {
                        Warning($"Reconnect took too long or was cancelled.");
                        continue;
                    }

                    var result = _connectResult.Task.Result;
                    Info($"Attempt {attempts} result={result}; state={_discord.ConnectionState}; login={_discord.LoginState}");
                    if (result) break;
                } catch (Exception ex)
                {
                    Error(ex, $"Recon#{attempts}");
                } finally
                {
                    attempts++;
                }
            }
            Info($"Exiting with state={_discord.ConnectionState}; login={_discord.LoginState}; cancel={(_cancelSource?.Token.IsCancellationRequested ?? false)}");
            _cancelSource = null;
            _connectResult = null;
        }
    }
}
