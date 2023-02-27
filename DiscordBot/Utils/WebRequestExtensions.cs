using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utils
{
    public static class WebRequestExtensions
    {
        public static WebResponse GetResponseWithoutException(this WebRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            try
            {
                return request.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw;
                }

                return e.Response;
            }
        }

        public async static Task ThrowWithContentIfError(this HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;
            var content = await response.Content.ReadAsStringAsync();
            throw new WebException($"{response.StatusCode} {response.ReasonPhrase}: {content}");
        }
    
    
        public static void WithSQLConnection(this DbContextOptionsBuilder options, string dbName, bool remoteConnection = false)
        {
#if DEBUG
            string configPath = remoteConnection ? "tokens:dbprod" : "tokens:dbdev";
#else
            string configPath = "tokens:db";
            remoteConnection = true;
#endif
            if (Program.Configuration == null)
                Program.buildConfig();
            string connStr = string.Format(Program.Configuration[configPath], dbName);
            if (remoteConnection)
            {
                options.UseMySql(connStr,
                    new MariaDbServerVersion(new Version(10, 6, 12)));
            } else
            {
                options.UseSqlServer(connStr);
            }
        }
    
    }
}
