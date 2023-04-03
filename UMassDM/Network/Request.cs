using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UMassDM.Network.Branches;
using UMassDM.Engines;

namespace UMassDM.Network
{
    public class Request
    {
        private const bool IsBot = false;
        private static string AuditLogReason = "UDM";

        public static void FixSSLTlsChannels()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public static Task<HttpStatusCode> SendDiscord(string endpoint, string method, DiscordClient auth, string json = null, string headers = null, bool XAuditLogReason = false)
        {
            return SendDiscord(endpoint, method, auth.Token, json, headers, XAuditLogReason);
        }

        public static Task<HttpStatusCode> SendDiscord(string endpoint, string method, string auth, string json = null, string headers = null, bool XAuditLogReason = false)
        {
            return Send(string.Format("https://discord.com/api/v{0}{1}", Config.Instance.APIVersion, endpoint), method, auth, json, headers, XAuditLogReason);
        }

        public static async Task<HttpStatusCode> Send(string endpoint, string method, string auth = null, string json = null, string headers = null, bool XAuditLogReason = false)
        {
            try
            {
                HttpClient client = new HttpClient();
                string token = string.Format("{0}{1}", IsBot ? "Bot " : "", auth);

                if (auth != null)
                    client.DefaultRequestHeaders.Add("Authorization", token);

                if (headers != null)
                {
                    foreach (var header in JObject.Parse(headers))
                        client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString());
                }

                if (XAuditLogReason == true)
                    client.DefaultRequestHeaders.Add("X-Audit-Log-Reason", AuditLogReason);

                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), endpoint);
                if (json != null)
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (!string.IsNullOrEmpty(json))
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                    request.Content = null;

                var response = await client.SendAsync(request);
                return response.StatusCode;
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, ex.ToString());
            }
            return HttpStatusCode.Unused;
        }

        public static async Task<KeyValuePair<HttpStatusCode, string>> SendGetDiscord(string endpoint, DiscordClient auth, string method = "GET", string json = null, string headers = null)
        {
            return await SendGetDiscord(endpoint, auth.Token, method, json, headers);
        }

        public static Task<KeyValuePair<HttpStatusCode, string>> SendGetDiscord(string endpoint, string auth, string method = "GET", string json = null, string headers = null)
        {
            return SendGet(string.Format("https://discord.com/api/v{0}{1}", Config.Instance.APIVersion, endpoint), auth, method, json, headers);
        }

        public static async Task<KeyValuePair<HttpStatusCode, string>> SendGet(string endpoint, string auth = null, string method = "GET", string json = null, string headers = null)
        {
            try
            {
                HttpClient client = new HttpClient();
                string token = string.Format("{0}{1}", IsBot ? "Bot " : "", auth);

                if (auth != null)
                    client.DefaultRequestHeaders.Add("Authorization", token);

                if (headers != null)
                {
                    foreach (var header in JObject.Parse(headers))
                        client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString());
                }

                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), endpoint);
                if (json != null)
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (!string.IsNullOrEmpty(json))
                        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                    request.Content = null;

                var response = client.GetAsync(endpoint).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                //await Task.Delay(1000);
                HttpResponseMessage res = await client.SendAsync(request);
                return new KeyValuePair<HttpStatusCode, string>(res.StatusCode, new StreamReader(await res.Content.ReadAsStreamAsync()).ReadToEnd());
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, ex.ToString());
            }
            return new KeyValuePair<HttpStatusCode, string>(HttpStatusCode.Unused, null);
        }

        public static async Task<IEnumerable<string>> GetCookies()
        {
            try
            {
                const string url = "https://discord.com";
                HttpClient client = new HttpClient();

                // Load constant headers for getting cookies
                foreach (var header in JObject.Parse(string.Format(@"
                {{
                    'accept':                    'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9',
		    	    'accept-language':           'en-US,en;q=0.9',
		    	    'sec-ch-ua-mobile':          '?0',
		    	    'sec-fetch-dest':            'document',
		    	    'sec-fetch-mode':            'navigate',
		    	    'sec-fetch-site':            'none',
		    	    'sec-fetch-user':            '?1',
		    	    'upgrade-insecure-requests': '1',
		    	    'user-agent':                '{0}'
                }}"
                , Config.Instance.UserAgent)))
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString());
                }

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Content = null;

                var response = client.GetAsync(url).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                //await Task.Delay(1000);
                HttpResponseMessage res = await client.SendAsync(request);
                return response.Headers.FirstOrDefault(header => header.Key == "Set-Cookie").Value;
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, ex.ToString());
            }
            return null;
        }
    }
}
