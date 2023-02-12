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
    }
}
