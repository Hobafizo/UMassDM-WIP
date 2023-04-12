using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace UMassDM.Network
{
    public class Request
    {
        private const bool IsBot = false;
        private static string AuditLogReason = "UMDM";

        public static Random Randomizer = new Random();

        public static void FixSSLTlsChannels()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
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
                HttpClientHandler handler = new HttpClientHandler();
                //handler.ClientCertificates.Add(new X509Certificate(@"C:\Users\pc\Downloads\mfc_cert.cer", "696696"));

                HttpClient client = new HttpClient(handler);
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
                        request.Content = new StringContent(JObject.Parse(json).ToString(), Encoding.UTF8, "application/json");
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
                        request.Content = new StringContent(JObject.Parse(json).ToString(), Encoding.UTF8, "application/json");
                }
                else
                    request.Content = null;

                if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    var response = await client.GetAsync(endpoint);
                    response.EnsureSuccessStatusCode();
                }

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

        private static void GetCFParams(string response, out string r, out string m)
        {
            Match rmatch = Regex.Match(response, "r:'[^']*'"),
                  mmatch = Regex.Match(response, "m:'[^']*'");

            if (rmatch.Success && mmatch.Success)
            {
                r = rmatch.Value.Replace("r:", "").Replace("'", "");
                m = mmatch.Value.Replace("m:", "").Replace("'", "");
            }
            else
            {
                r = null;
                m = null;
            }
        }

        private static async Task<string> GetCfBm(string r, string m, string cookiestr)
        {
            string url = string.Format("https://discord.com/cdn-cgi/bm/cv/result?req_id={0}", r);

            string payload = string.Format(@"
                {{
                			'm':            {0}
                            'results':      ['859fe3e432b90450c6ddf8fae54c9a58', '460d5f1e93f296a48e3f6745675f27e2'],
                			'timing':       {1},
                			'fp':
                				{{
                					'id':   3,
                					'e':    {{
                                                'r':    [1920,1080],
                					            'ar':   [1032,1920],
                					            'pr':   1,
                					            'cd':   24,
                					            'wb':   true,
                					            'wp':   false,
                					            'wn':   false,
                					            'ch':   false,
                					            'ws':   false,
                					            'wd':   false
                				            }}
                                }}
                }}
                ", m, Randomizer.Next(60, 120));

            CookieContainer cookies = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler();
            handler.CookieContainer = cookies;

            HttpClient client = new HttpClient(handler);

            // Load constant headers for getting cookies
            foreach (var header in JObject.Parse(string.Format(@"
                {{
                    'accept':                    '*/*',
		    	    'accept-language':           'en-US,en;q=0.9',
                    'cookie':                    '{0}',
                    'origin':                    'https://discord.com',
                    'referer':                   'https://discord.com',
                    'sec-fetch-mode':            'cors',
                    'sec-fetch-site':            'same-origin',
		    	    'user-agent':                '{1}'
                }}"
            , cookiestr, Config.Instance.UserAgent)))
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value.ToString());
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            HttpResponseMessage res = await client.SendAsync(request);
            if (res.StatusCode == HttpStatusCode.OK)
            {
                var responseCookies = cookies.GetCookies(new Uri(url)).Cast<Cookie>();
                if (responseCookies.Count() > 0)
                {
                    string str = string.Empty;

                    foreach (Cookie cookie in responseCookies)
                        str += string.Format("; {0}={1}", cookie.Name, cookie.Value);
                    return cookiestr.Remove(0, 2);
                }
            }
            return null;
        }

        public static async Task<string> GetCookies()
        {
            try
            {
                const string url = "https://discord.com";

                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler();
                handler.CookieContainer = cookies;

                HttpClient client = new HttpClient(handler);

                // Load constant headers for getting cookies
                foreach (var header in JObject.Parse(string.Format(@"
                {{
                    'accept':                    'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
		    	    'accept-language':           'en-US,en;q=0.9',
                    'cache-control':             'max-age=0',
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

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var responseCookies = cookies.GetCookies(new Uri(url)).Cast<Cookie>();
                int cookiescnt = responseCookies.Count();

                if (cookiescnt > 0)
                {
                    string str = string.Empty;

                    foreach (Cookie cookie in responseCookies)
                        str += string.Format("; {0}={1}", cookie.Name, cookie.Value);

                    return str.Remove(0, 2);
                }
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, ex.ToString());
            }
            return string.Empty;
        }

        public static async Task<string> GetDiscordCookies()
        {
            try
            {
                const string url = "https://discord.com";

                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler();
                handler.CookieContainer = cookies;

                HttpClient client = new HttpClient(handler);

                // Load constant headers for getting cookies
                foreach (var header in JObject.Parse(string.Format(@"
                {{
                    'accept':                    'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7',
		    	    'accept-language':           'en-US,en;q=0.9,la;q=0.8',
                    'cache-control':             'max-age=0',
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

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                HttpResponseMessage res = await client.SendAsync(request);
                if (res.StatusCode == HttpStatusCode.OK)
                {
                    var responseCookies = cookies.GetCookies(new Uri(url)).Cast<Cookie>();
                    if (responseCookies.Count() > 0)
                    {
                        string html = new StreamReader(await res.Content.ReadAsStreamAsync()).ReadToEnd(),
                            cookiestr = string.Empty, r, m;

                        foreach (Cookie cookie in responseCookies)
                            cookiestr += string.Format("; {0}={1}", cookie.Name, cookie.Value);
                        cookiestr = cookiestr.Remove(0, 2);

                        GetCFParams(html, out r, out m);

                        string cfbm = await GetCfBm(r, m, cookiestr);
                        return string.Format("{0}{1}", cookiestr, cfbm != null ? "; {0}" + cfbm : "");
                    }
                }
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, ex.ToString());
            }
            return string.Empty;
        }
    }
}
