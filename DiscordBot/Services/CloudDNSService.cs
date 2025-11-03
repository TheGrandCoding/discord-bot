using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class CloudDNSService : SavedClassService<CloudSave>
    {
        public override bool IsEnabled => false;

        bool croned = false;
        public override void OnReady(IServiceProvider services)
        {
            base.OnReady(services);

            if (croned || Data.CurrentIpMethod != "router") return;
            croned = true;

            schedule(new("*", "*/5"), OnDailyTick);
        }

        async Task<string> getExternalIP(Classes.BotHttpClient client)
        {
            if (Data.CurrentIpMethod == "router")
            {
                var response = await client.GetAsync("http://192.168.1.1/check.lp?ppp=1");
                if (!response.IsSuccessStatusCode)
                    return null;
                var content = await response.Content.ReadAsStringAsync();
                var jobj = JObject.Parse(content);
                return jobj["PPP_IP"].ToObject<string>();
            }
            else
            {
                var response = await client.GetAsync("https://checkip.amazonaws.com/");
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return null;
                return content.Trim();
            }
        }

        static bool IsPrivateAddress(IPAddress addr)
        {
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                return addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal;
            }
            var bytes = addr.GetAddressBytes();
            return
                ((bytes[0] == 10) ||
                ((bytes[0] == 192) && (bytes[1] == 168)) ||
                ((bytes[0] == 172) && ((bytes[1] & 0xf0) == 16)));
        }

        async Task<(string, string)> getCurrentIp(Classes.BotHttpClient client)
        {
            var request = initCF(HttpMethod.Get, $"zones/{Data.ZoneId}/dns_records");
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return (null, null);
            var jobj = JObject.Parse(content);
            var records = jobj["result"] as JArray;
            foreach (var rec in records)
            {
                if (rec is JObject j && j["name"].ToObject<string>() == Data.DomainName)
                    return (j["content"].ToObject<string>(), j["id"].ToObject<string>());
            }
            return (null, null);
        }



        HttpRequestMessage initCF(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, "https://api.cloudflare.com/client/v4/" + path);
            request.Headers.Add("X-Auth-Email", Data.Email);
            request.Headers.Add("X-Auth-Key", Data.AuthKey);
            return request;
        }

        async Task<string> getZoneId(Classes.BotHttpClient client)
        {
            string zone = Uri.EscapeDataString(Data.Zone);
            var request = initCF(HttpMethod.Get, $"zones?name={zone}&status=active");
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            return "";
        }

        async Task updateRecordedIp(Classes.BotHttpClient client, string zoneId, string dnsRecordId, string newValue)
        {
            var request = initCF(HttpMethod.Put, $"zones/{zoneId}/dns_records/{dnsRecordId}");
            var jobj = new Newtonsoft.Json.Linq.JObject();
            jobj["type"] = "A";
            jobj["name"] = Data.DomainName;
            jobj["content"] = newValue;
            jobj["ttl"] = 1;
            jobj["proxied"] = true;
            request.Content = new StringContent(jobj.ToString(), Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
        }

        public async Task<Commands.BotResult> Perform()
        {
            var client = Program.GlobalServices.GetRequiredService<Classes.BotHttpClient>()
                .Child("DynDNS");
            var externalIp = await getExternalIP(client);
            if(string.IsNullOrWhiteSpace(externalIp)) return new("No ext IP");

            if (externalIp == Data.LastSeenIP)
                return new();
            Info($"New external IP: {externalIp} (old: {Data.LastSeenIP}");
            Data.LastSeenIP = externalIp;

            if (string.IsNullOrWhiteSpace(Data.ZoneId))
            {
                Data.ZoneId = await getZoneId(client);
            }
            Info($"Zone {Data.Zone} has ID {Data.ZoneId}");

            (var currentIp, var dnsRecordId) = await getCurrentIp(client);
            Info($"DNS Lookup: {currentIp}");
            if (currentIp == null) return new("Could not fetch existing IP record");
            if(externalIp == currentIp)
            {
                this.OnSave();
                return new("DNS record already points to that IP");
            }


            Info($"DNS {Data.DomainName} has ID {dnsRecordId}");

            await updateRecordedIp(client, Data.ZoneId, dnsRecordId, externalIp);
            Info("Updated.");
            this.OnSave();
            return new();
        }
        public override void OnDailyTick()
        {
            if(Data != null && !string.IsNullOrWhiteSpace(Data.AuthKey))
            {
                Task.Run(async () => {
                    try
                    {
                        await Perform();
                    } catch(Exception ex)
                    {
                        ErrorToOwner(ex, "DT");
                    }
                });
            }
        }
    }
    public class CloudSave
    {
        [JsonProperty("zone")]
        public string Zone { get; set; }
        [JsonProperty("zone_id")]
        public string ZoneId { get; set; }
        [JsonProperty("last_external_ip")]
        public string LastSeenIP { get; set; }

        [JsonProperty("domain")]
        public string DomainName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("auth_key")]
        public string AuthKey { get; set; }

        [JsonProperty("cur_ip_method")]
        public string CurrentIpMethod { get; set; } = "router";
    }
}
