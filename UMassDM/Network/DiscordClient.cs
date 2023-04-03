using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Drawing;
using Newtonsoft.Json.Linq;
using UMassDM.Engines;
using UMassDM.Network.Branches;
using UMassDM.Utils;

namespace UMassDM.Network
{
    public enum TokenStatus : byte
    {
        Uninitialized,
        Valid,
        Invalid
    }

    public enum InviteResponse : byte
    {
        Joined,
        Unknown,
        NoResponse,
        CaptchaServiceUnset,
        CaptchaServiceFail,
        RateLimited
    }

    public class DiscordClient
    {
        #region Class Members
        private TokenStatus m_status;
        private string m_token, m_email, m_password;
        private string m_username, m_discriminator;
        private string m_proxy;

        public string Token { get { return m_token; } }
        public string Email { get { return m_email; } }
        public string Password { get { return m_password; } }
        public TokenStatus Status { get { return m_status; } set { m_status = value; } }
        #endregion

        public DiscordClient(string token, string email, string pw, TokenStatus status = TokenStatus.Uninitialized, string proxy = null)
        {
            m_token = token;
            m_email = email;
            m_password = pw;
            m_username = string.Empty;
            m_discriminator = string.Empty;
            m_status = status;
            m_proxy = proxy != null ? proxy : string.Empty;
        }

        public async Task<InviteResponse> JoinGuild(string invite_code, int attempt_idx = 0, string cookie = null, string xcontext = null, InvitePayload payload = null)
        {
            if (m_status != TokenStatus.Invalid)
            {
                try
                {
                    if (cookie == null)
                        cookie = await GetCookies();
                    if (xcontext == null)
                        xcontext = await GetInvitationContext(invite_code, cookie);

                    Console.WriteLine("Cookies: {0}", cookie);

                    var response = await Request.SendGetDiscord(string.Format("/invites/{0}", invite_code),
                        m_token, "POST",
                        (payload != null ? payload.ToJSON() : ""),
                        string.Format(@"
                    {{
                        'accept':               '*/*',
                        'accept-encoding':      'gzip, deflate, br',
			            'accept-language':      'en-US,en;q=0.9',
			            'cookie':               '{0}',
			            'origin':               'https://discord.com',
			            'referer':              'https://discord.com/channels/@me',
                        'sec-ch-ua':            '""Microsoft Edge"";v=""111"", ""Not(A:Brand"";v=""8"", ""Chromium"";v=""111""',
                        'sec-ch-ua-mobile':     '?0',
                        'sec-ch-ua-platform':   '""Windows""',
			            'sec-fetch-dest':       'empty',
			            'sec-fetch-mode':       'cors',
			            'sec-fetch-site':       'same-origin',
			            'user-agent':           '{1}',
			            'x-context-properties': '{2}',
			            'x-debug-options':      'bugReporterEnabled',
			            'x-discord-locale':     'es-ES',
			            'x-super-properties':   '{3}'
                    }}
                    ", cookie, Config.Instance.UserAgent, xcontext, Config.Instance.XSuperProperties));

                    if (response.Value != null)
                    {
                        JObject res = JObject.Parse(response.Value);

                        Console.WriteLine("{0}\n{1}", response.Key, response.Value);

                        if (response.Value.Contains("captcha_sitekey") && attempt_idx <= Config.Instance.Setting.CaptchaInviteRetryTimes)
                        {
                            if (Config.Instance.Setting.CaptchaService != CaptchaService.Unset)
                            {
                                MainForm.Instance.PutLog(Color.SkyBlue, "[{0}] Captcha detected on joining {1}, attempting to solve it..",
                                    m_username.Length > 0 ? string.Format("{0}#{1}", m_username, m_discriminator) : m_token,
                                    invite_code);

                                CaptchaPayload captcha = new CaptchaPayload(res["captcha_sitekey"].ToString(), res["captcha_rqdata"].ToString(), res["captcha_rqtoken"].ToString());
                                string solution;

                                for (; attempt_idx <= Config.Instance.Setting.CaptchaInviteRetryTimes; ++attempt_idx)
                                {
                                    /*if (attempt_idx != 0)
                                        await Task.Delay(Config.Instance.Setting.JoinWaitTime * 1000);*/
                                    //await Task.Delay(Config.Instance.Setting.JoinWaitTime * 1000);

                                    try
                                    {
                                        // try to solve captcha
                                        solution = await CaptchaResolver.SolveCaptcha(captcha, cookie, "https://discord.com/channels/@me", m_proxy).TimeoutAfter(TimeSpan.FromSeconds(Config.Instance.Setting.CaptchaWaitTime));   

                                        // on captcha solved
                                        if (!string.IsNullOrEmpty(solution))
                                        {
                                            MainForm.Instance.PutLog(Color.Green, "[{0}] Successfully solved captcha on joining {1}.",
                                               m_username.Length > 0 ? string.Format("{0}#{1}", m_username, m_discriminator) : m_token,
                                               invite_code);

                                            // setup captcha solution
                                            if (payload == null)
                                                payload = new InvitePayload(solution, captcha.captcha_rqtoken);
                                            else
                                            {
                                                payload.captcha_key = solution;
                                                payload.captcha_rqtoken = captcha.captcha_rqtoken;
                                            }

                                            // re-attempt to join with solution
                                            return await JoinGuild(invite_code, ++attempt_idx, null, null, payload);
                                        }

                                        // on solution failure
                                        else
                                            MainForm.Instance.PutLog(Color.DarkMagenta, "[{0}] Failed to solve captcha on joining {1}.",
                                               m_username.Length > 0 ? string.Format("{0}#{1}", m_username, m_discriminator) : m_token,
                                               invite_code);
                                    }
                                    catch (TimeoutException)
                                    {
                                        MainForm.Instance.PutLog(Color.DarkMagenta, "[{0}] Captcha timed out on joining {1}.",
                                               m_username.Length > 0 ? string.Format("{0}#{1}", m_username, m_discriminator) : m_token,
                                               invite_code);
                                    }
                                }
                                return InviteResponse.CaptchaServiceFail;
                            }
                            else
                                return InviteResponse.CaptchaServiceUnset;
                        }
                        else if (response.Key == (HttpStatusCode)429 && response.Value.Contains("1015"))
                            return InviteResponse.RateLimited;
                        else if (response.Key == HttpStatusCode.OK)
                            return InviteResponse.Joined;
                    }
                    else
                        return InviteResponse.NoResponse;
                }
                catch (Exception ex)
                {
                    if (MainForm.Debugging)
                        Logger.Show(LogType.Error, "[Discord Client] {0}", ex.ToString());
                }
            }
            return InviteResponse.Unknown;
        }

        public async Task<bool> MassDMTest()
        {
            if (m_status != TokenStatus.Invalid)
            {
                try
                {
                    var request = await Request.SendGetDiscord("/users/@me/channels", m_token);
                    if (request.Value != null)
                    {
                        var array = JArray.Parse(request.Value);

                        foreach (var entry in array)
                            Console.WriteLine(entry);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    if (MainForm.Debugging)
                        Logger.Show(LogType.Error, "[Discord Client] {0}", ex.ToString());
                }
            }
            return false;
        }

        public async Task<TokenStatus> CheckTokenStatus()
        {
            try
            {
                var request = await Request.SendGetDiscord("/users/@me", m_token);
                if (request.Value != null)
                {
                    string username = JObject.Parse(request.Value)["username"].ToString(),
                    discriminator = JObject.Parse(request.Value)["discriminator"].ToString();

                    if (username.Length > 0 && discriminator.Length == 4)
                    {
                        m_status = TokenStatus.Valid;
                        m_username = username;
                        m_discriminator = discriminator;
                        return m_status;
                    }
                }
            }
            catch { }

            m_status = TokenStatus.Invalid;
            return m_status;
        }

        public async Task<string> GetCookies()
        {
            string str = string.Empty;

            IEnumerable<string> cookies = await Request.GetCookies();
            if (cookies != null)
            {
                int count = cookies.Count();
                if (count > 0)
                {
                    for (int i = 0; i < count - 1; ++i)
                        str += string.Format("{0}; ", cookies.ElementAt(i));
                    str += cookies.ElementAt(count - 1);

                    if (str.IndexOf("locale=en-US;", StringComparison.OrdinalIgnoreCase) == -1)
                        str += "; locale=en-US ";
                }
            }

            return str;
        }

        public async Task<string> GetInvitationContext(string invite_code, string cookie)
        {
            try
            {
                var request = await Request.SendGetDiscord(string.Format("/invites/{0}?inputValue={0}&with_counts=true&with_expiration=true", invite_code),
                    m_token, "GET", null, string.Format(@"
                {{
                    'accept':             '*/*',
			        'accept-language':    'en-US,en;q=0.9',
			        'cookie':             '{0}',
			        'referer':            'https://discord.com/channels/@me',
                    'sec-ch-ua-mobile':   '?0',
                    'sec-ch-ua-platform': 'Windows',
			        'sec-fetch-dest':     'empty',
			        'sec-fetch-mode':     'cors',
			        'sec-fetch-site':     'same-origin',
			        'user-agent':         '{1}',
			        'x-debug-options':    'bugReporterEnabled',
			        'x-discord-locale':   'en-US',
			        'x-super-properties': '{2}'
                }}"
                , cookie, Config.Instance.UserAgent, Config.Instance.XSuperProperties));

                if (request.Value != null)
                {
                    // Get information of target guild
                    JObject guildinfo = JObject.Parse(request.Value);

                    return new XContext(
                                        guildinfo["guild"]["id"].ToString(),
                                        guildinfo["channel"]["id"].ToString(),
                                        Convert.ToSingle(guildinfo["channel"]["type"]
                        )).ToString();
                }
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, "[Discord Client] {0}", ex.ToString());
            }
            return null;
        }

        public async Task<string> Username()
        {
            if (m_status != TokenStatus.Invalid)
            {
                if (!string.IsNullOrEmpty(m_username))
                    return m_username;
                else
                {
                    try
                    {
                        var request = await Request.SendGetDiscord("/users/@me", m_token);
                        if (request.Value != null)
                        {
                            var username = JObject.Parse(request.Value)["username"];
                            var discriminator = JObject.Parse(request.Value)["discriminator"];
                            return string.Format("{0}#{1}", username, discriminator);
                        }
                    }
                    catch { }
                }
            }
            return "N/A";
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}:{2}", m_email, m_password, m_token);
        }
    }
}
