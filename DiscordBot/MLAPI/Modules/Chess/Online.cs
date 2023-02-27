#if INCLUDE_CHESS
using DiscordBot.Classes.Chess.Online;
using DiscordBot.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DiscordBot.Services.ChessService;

namespace DiscordBot.MLAPI.Modules
{
    partial class Chess
    {
        const string tokenName = "onlinechesstoken";
        string getToken()
        {
            if (Context.User == null)
                return null;
            if (SelfPlayer == null)
                return null;
            var token = Context.User.Tokens.FirstOrDefault(x => x.Name == tokenName);
            if(token == null)
            {
                token = new Classes.BotDbAuthToken(tokenName, 24);
                Context.User.Tokens.Add(token);
            }
            SelfPlayer.VerifyOnlineReference = token.Value;
            return SelfPlayer.VerifyOnlineReference;
        }

        [Method("GET"), Path("/chess/ai")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        public void JoinAI()
        {
            throw new NotImplementedException("Not yet implemented");
        }

        [Method("GET"), Path("/chess/online")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        public void OnlineBase()
        {
            if(SelfPlayer == null)
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "No Chess", "No linked chess account");
                return;
            }
            if(SelfPlayer.IsBuiltInAccount)
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "Bad Account", "Account not able to do that");
                return;
            }
            if(SelfPlayer.IsBanned)
            {
                HTTPError(System.Net.HttpStatusCode.UnavailableForLegalReasons, "Banned", "You are banned and may not play games.");
            }
            Program.LogMsg($"Getting Lock..", Discord.LogSeverity.Critical, "OnlineBase");
            if(!OnlineLock.WaitOne(5 * 1000))
            {
                Program.LogMsg($"Failed..", Discord.LogSeverity.Critical, "OnlineBase");
                HTTPError(System.Net.HttpStatusCode.InternalServerError, "Thread-Safe Halt", "Unable to get lock");
                return;
            }
            Program.LogMsg($"Got Lock", Discord.LogSeverity.Critical, "OnlineBase");
            string token = getToken();
            string TABLE = "";
            string ROW = "";
            if(CurrentGame == null)
            {
                ROW = "<tr><td colspan='4'>" + aLink($"chess://create/{token}", "Create New Game") + "</td></tr>";
            } else
            {
                ROW += $"<td>{(CurrentGame.White?.Player?.Name ?? "No white")}</td>";
                ROW += $"<td>{(CurrentGame.Black?.Player?.Name ?? "No black")}</td>";
                ROW += $"<td>{ aLink($"chess://join/{token}", "Join Game")}</td>";
                ROW += $"<td>{aLink($"chess://spectate/{token}", "Spectate Game")}</td></tr>";
            }
            TABLE += ROW;
            OnlineLock.Release();
            Program.LogMsg($"Relased Lock", Discord.LogSeverity.Critical, "OnlineBase");
            await ReplyFile("online.html", 200, new Replacements().Add("table", TABLE));
        }

        [Method("GET"), Path("/chess/online_game")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        public void OnlineGame()
        {
            if(!OnlineLock.WaitOne())
            {
                await RespondRaw("Failed thread safe lock", System.Net.HttpStatusCode.InternalServerError);
                return;
            }
            try
            {
                if (ChessService.CurrentGame == null)
                {
                    ChessService.CurrentGame = new OnlineGame();
                    CurrentGame.SendLogStart(SelfPlayer.Name);
                WebSockets.ChessConnection.log($"Game created OG and thus White is: {SelfPlayer.Name}");
                }
                var g = ChessService.CurrentGame;
                var side = g?.GetPlayer(SelfPlayer.Id)?.Side.ToString().ToLower()[0].ToString() ?? "";
                var white = g?.White?.Player?.Name ?? "";
                var black = g?.Black?.Player?.Name ?? "";
                var token = getToken();
                var fen = g?.InnerGame?.generate_fen() ?? "";
                await ReplyFile("game.html", 200, new Replacements()
                    .Add("side", side)
                    .Add("white", white)
                    .Add("black", black)
                    .Add("token", token)
                    .Add("game_fen", fen));
                }
            catch (Exception ex)
            {
                Program.LogError(ex, "Online_Game");
            }
            finally
            {
                OnlineLock.Release();
            }
        }

        [Method("GET"), Path("/chess/api/otherid")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        [RequireValidHTTPAgent(false)]
        public void GetPlayerIdentity(int id)
        {
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("Unknown player id", 404);
                return;
            }
            var jobj = new JObject();
            jobj["name"] = player.Name;
            jobj["id"] = player.Id;
            await RespondRaw(jobj.ToString());
        }

        [Method("GET"), Path("/chess/api/identity")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        [RequireValidHTTPAgent(false)]
        public void GetOwnIdentity()
        {
            GetPlayerIdentity(SelfPlayer.Id);
        }

        [Method("PUT"), Path("/chess/api/online/create")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        [RequireValidHTTPAgent(false)]
        public void CreateNewGame()
        {
            Program.LogMsg($"Getting Lock..", Discord.LogSeverity.Critical, "CreateNewGame");
            if(!OnlineLock.WaitOne(60 * 1000))
            {
                Program.LogMsg($"Failed Lock..", Discord.LogSeverity.Critical, "CreateNewGame");
                await RespondRaw("Unable to get lock", 500);
                return;
            }
            try
            {
                Program.LogMsg($"Got Lock..", Discord.LogSeverity.Critical, "CreateNewGame");
                if(CurrentGame != null)
                {
                    await RespondRaw("Game already in progress", 400);
                    return;
                }
                CurrentGame = new OnlineGame();
                Program.LogMsg($"Released Lock..", Discord.LogSeverity.Critical, "CreateNewGame");
                CurrentGame.SendLogStart(SelfPlayer.Name);
                WebSockets.ChessConnection.log($"Game created and thus White is: {SelfPlayer.Name}");
                await RespondRaw("", 201);
            }
            catch
            {
                throw;
            }
            finally
            {
                OnlineLock.Release();
            }
        }

        [Method("POST"), Path("/chess/api/online/chrome")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        [RequireValidHTTPAgent(false)]
        public void UploadChromeTabs()
        {
            if (ChessService.CurrentGame == null || ChessService.CurrentGame.HasEnded)
            {
                await RespondRaw("Failed", 500);
                return;
            }
            if (SelfPlayer == null)
            {
                await RespondRaw("Failed", 400);
                return;
            }
            var p = ChessService.CurrentGame.GetPlayer(SelfPlayer.Id);
            if (p == null)
            {
                await RespondRaw("Not connected to chess", 403);
                return;
            }
            // we dont expect anything, client pro-actively notifies

            var path = Path.Combine(Program.BASE_PATH, "ChessO", "Demands", DateTime.Now.DayOfYear.ToString("000"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string fName = $"{SelfPlayer.Id.ToString("00")}_{p.DemandsSent.ToString("000")}_chrome.txt";
            p.DemandsSent++;
            File.WriteAllText(Path.Combine(path, fName), Context.Body);
            var builder = new Discord.EmbedBuilder();
            builder.Title = "Chrome Processes";
            builder.Description = $"Client has identified possible concern in open Chrome tabs.\n" +
                $"For privacy purposes, the tab names will be reviewed by the Chief Justice and released to their discretion";
            builder.AddField("File Name", fName);
            ChessS.LogAdmin(builder);
            await RespondRaw("Saved");
        }

        [Method("POST"), Path("/chess/api/online/processes")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        [RequireValidHTTPAgent(false)]
        public void UploadProcesses()
        {
            if(ChessService.CurrentGame == null || ChessService.CurrentGame.HasEnded)
            {
                await RespondRaw("Failed", 500);
                return;
            }
            if(SelfPlayer == null)
            {
                await RespondRaw("Failed", 400);
                return;
            }
            var p = ChessService.CurrentGame.GetPlayer(SelfPlayer.Id);
            if (p == null)
            {
                await RespondRaw("Not connected to chess", 403);
                return;
            }
            if (!p.ExpectDemand)
            {
                await RespondRaw("Unexpected image", 400);
                return;
            }

            var path = Path.Combine(Program.BASE_PATH, "ChessO", "Demands", DateTime.Now.DayOfYear.ToString("000"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string fName = $"{SelfPlayer.Id.ToString("00")}_{p.DemandsSent.ToString("000")}_processes.txt";
            p.DemandsSent++;
            File.WriteAllText(Path.Combine(path, fName), Context.Body);
            var builder = new Discord.EmbedBuilder();
            builder.Title = "Processes Gathered";
            builder.Description = $"In response to admin demand, {SelfPlayer.Name} has sent a list of active processes.\n" +
                $"For privacy purposes, this screenshot will be reviewed by the Chief Justice and released to their discretion";
            builder.AddField("File Name", fName);
            ChessS.LogAdmin(builder);
            await RespondRaw("Saved");
        }

#region Image Uploading
        [Method("POST"), Path("/chess/api/online/screen")]
        [RequireChess(Classes.Chess.ChessPerm.Player)]
        [RequireValidHTTPAgent(false)]
        public void UploadScreenshot(string name)
        {
            if (ChessService.CurrentGame == null || ChessService.CurrentGame.HasEnded)
            {
                await RespondRaw("Failed", 500);
                return;
            }
            if (SelfPlayer == null)
            {
                await RespondRaw("Failed", 400);
                return;
            }
            var p = ChessService.CurrentGame.GetPlayer(SelfPlayer.Id);
            if(p == null)
            {
                await RespondRaw("Not connected to chess", 403);
                return;
            }
            if(!p.ExpectDemand)
            {
                await RespondRaw("Unexpected image", 400);
                return;
            }
            var path = Path.Combine(Program.BASE_PATH, "ChessO", "Demands", DateTime.Now.DayOfYear.ToString("000"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string fName = $"{SelfPlayer.Id.ToString("00")}_{p.DemandsSent.ToString("000")}_{name}.png";
            p.DemandsSent++;
            path = Path.Combine(path, fName);
            SaveFile(Context.Request.ContentEncoding, GetBoundary(Context.Request.ContentType), 
                new MemoryStream(Encoding.UTF8.GetBytes(Context.Body)), path);
            var builder = new Discord.EmbedBuilder();
            builder.Title = "Desktop Uploaded";
            builder.Description = $"In response to admin demand, {SelfPlayer.Name} has uploaded a screenshot of their screens.\n" +
                $"For privacy purposes, this screenshot will be reviewed by the Chief Justice and released to their discretion";
            builder.AddField("File Name", fName);
            ChessS.LogAdmin(builder);
            await RespondRaw("Saved");
        }

        private static String GetBoundary(String ctype)
        {
            return "--" + ctype.Split(';')[1].Split('=')[1].Replace("\"", "");
        }

        private static void SaveFile(Encoding enc, String boundary, Stream input, string path)
        {
            Byte[] boundaryBytes = enc.GetBytes(boundary);
            Int32 boundaryLen = boundaryBytes.Length;
            using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                Byte[] buffer = new Byte[1024];
                Int32 len = input.Read(buffer, 0, 1024);
                Int32 startPos = -1;
                // Find start boundary
                while (true)
                {
                    if (len == 0)
                    {
                        throw new Exception("Start Boundaray Not Found");
                    }

                    startPos = IndexOf(buffer, len, boundaryBytes);
                    if (startPos >= 0)
                    {
                        break;
                    }
                    else
                    {
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen);
                    }
                }
                // Skip four lines (Boundary, Content-Disposition, Content-Type, and a blank)
                for (Int32 i = 0; i < 4; i++)
                {
                    while (true)
                    {
                        if (len == 0)
                        {
                            throw new Exception("Preamble not Found.");
                        }

                        startPos = Array.IndexOf(buffer, enc.GetBytes("\n")[0], startPos);
                        if (startPos >= 0)
                        {
                            startPos++;
                            break;
                        }
                        else
                        {
                            len = input.Read(buffer, 0, 1024);
                        }
                    }
                }
                Array.Copy(buffer, startPos, buffer, 0, len - startPos);
                len = len - startPos;
                while (true)
                {
                    Int32 endPos = IndexOf(buffer, len, boundaryBytes);
                    if (endPos >= 0)
                    {
                        if (endPos > 0) output.Write(buffer, 0, endPos - 2);
                        break;
                    }
                    else if (len <= boundaryLen)
                    {
                        throw new Exception("End Boundaray Not Found");
                    }
                    else
                    {
                        output.Write(buffer, 0, len - boundaryLen);
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen) + boundaryLen;
                    }
                }
            }
        }

        private static Int32 IndexOf(Byte[] buffer, Int32 len, Byte[] boundaryBytes)
        {
            for (Int32 i = 0; i <= len - boundaryBytes.Length; i++)
            {
                Boolean match = true;
                for (Int32 j = 0; j < boundaryBytes.Length && match; j++)
                {
                    match = buffer[i + j] == boundaryBytes[j];
                }

                if (match)
                {
                    return i;
                }
            }
            return -1;
        }
#endregion
    }
}
#endif