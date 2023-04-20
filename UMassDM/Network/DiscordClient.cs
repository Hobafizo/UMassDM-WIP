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
using CurlThin.Enums;

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
        private CurlClient m_socket;
        private TokenStatus m_status;
        private string m_token, m_email, m_password;
        private ulong m_userid;
        private string m_username, m_discriminator;
        private string m_proxy;

        public string Token { get { return m_token; } }
        public string Email { get { return m_email; } }
        public string Password { get { return m_password; } }
        public TokenStatus Status { get { return m_status; } set { m_status = value; } }
        #endregion

        public DiscordClient(string token, string email, string pw, TokenStatus status = TokenStatus.Uninitialized, string useragent = null, string xsuperproperties = null, string ja3 = null, string proxy = null)
        {
            m_token = token;
            m_email = email;
            m_password = pw;
            m_userid = 0;
            m_username = string.Empty;
            m_discriminator = string.Empty;
            m_status = status;
            m_proxy = proxy != null ? proxy : string.Empty;

            m_socket = new CurlClient(true, useragent, xsuperproperties, ja3);
        }

        public async Task<InviteResponse> JoinGuild(string invite_code, int attempt_idx = 0, DiscordCookie cookie = null, string xcontext = null, InvitePayload payload = null)
        {
            if (m_status == TokenStatus.Valid)
            {
                try
                {
                    if (cookie == null)
                        cookie = await GetCookies();
                    if (xcontext == null)
                        xcontext = await GetInvitationContext(invite_code, cookie.CookieString);

                    var response = await m_socket.PostDiscord(string.Format("/invites/{0}", invite_code),
                        m_token, true, string.Format(@"
                    {{
                        'accept':               '*/*',
			            'accept-language':      'en-US,en;q=0.9',
			            'cookie':               '{0}',
			            'origin':               'https://discord.com',
			            'referer':              'https://discord.com/channels/@me',
                        'sec-ch-ua-mobile':     '?0',
                        'sec-ch-ua-platform':   '""Windows""',
			            'sec-fetch-dest':       'empty',
			            'sec-fetch-mode':       'cors',
			            'sec-fetch-site':       'same-origin',
			            'x-context-properties': '{1}',
			            'x-debug-options':      'bugReporterEnabled',
			            'x-discord-locale':     'en-US',
			            {2}
                    }}
                    ", cookie.CookieString, xcontext,
                    cookie.Fingerprint == null ? "" : string.Format("'x-fingerprint':        '{0}',", cookie.Fingerprint)),
                    (payload != null ? payload.ToJSON() : "{}"));

                    if (response.Data != null)
                    {
                        JObject res = JObject.Parse(response.Data);

                        Console.WriteLine("{0}\n{1}", response.StatusCode, res.ToString());

                        if (response.StatusCode == HttpStatusCode.BadRequest && response.Data.Contains("captcha_sitekey") && attempt_idx <= Config.Instance.Setting.CaptchaInviteRetryTimes)
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
                                    await Task.Delay(Config.Instance.Setting.JoinWaitTime * 1000);

                                    try
                                    {
                                        // try to solve captcha
                                        solution = await CaptchaResolver.SolveCaptcha(m_socket, captcha, cookie.CookieString, "https://discord.com/channels/@me", m_proxy, Config.Instance.Setting.CaptchaWaitTime);

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
                                            return await JoinGuild(invite_code, ++attempt_idx, cookie, xcontext, payload);
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

                        else if (response.StatusCode == (HttpStatusCode)429 && response.Data.Contains("1015"))
                            return InviteResponse.RateLimited;

                        else if (response.StatusCode == HttpStatusCode.Forbidden)
                            MainForm.Instance.PutLog(Color.DarkMagenta, "[{0}] Failed to join {1}: {2}.",
                                               m_username.Length > 0 ? string.Format("{0}#{1}", m_username, m_discriminator) : m_token,
                                               invite_code, res["message"]);

                        else if (response.StatusCode == HttpStatusCode.OK)
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
            if (m_status == TokenStatus.Valid)
            {
                try
                {
                    var request = await m_socket.GetDiscord("/users/@me/channels", m_token);
                    if (request.Result == CURLcode.OK)
                    {
                        var array = JArray.Parse(request.Data);

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

        public async Task<bool> RegenerateToken()
        {
            if (m_status == TokenStatus.Valid)
            {
                try
                {
                    DiscordCookie cookie = await GetCookies();

                    var login = await m_socket.PostDiscord("/auth/login", null, true, string.Format(@"
                    {{
                        'accept':               '*/*',
			            'accept-language':      'en-US,en;q=0.9',
			            'cookie':               '{0}',
			            'origin':               'https://discord.com',
			            'referer':              'https://discord.com/login',
                        'sec-ch-ua-mobile':     '?0',
                        'sec-ch-ua-platform':   '""Windows""',
			            'sec-fetch-dest':       'empty',
			            'sec-fetch-mode':       'cors',
			            'sec-fetch-site':       'same-origin',
			            'x-debug-options':      'bugReporterEnabled',
			            'x-discord-locale':     'en-US',
			            {1}
                    }}
                    ", cookie.CookieString,
                    cookie.Fingerprint == null ? "" : string.Format("'x-fingerprint':        '{0}',", cookie.Fingerprint)),
                    
                    string.Format(@"
                    {{
                        'captcha_key':          null,
                        'gift_code_sku_id':     null,
                        'login_source':         null,
                        'login':                '{0}',
                        'password':             '{1}',
                        'undelete':             false
                    }}", m_email, m_password));

                    if (login.Result == CURLcode.OK)
                    {
                        m_token = JObject.Parse(login.Data)["token"].ToString();

                        var array = JObject.Parse(login.Data);
                        Console.WriteLine(array.ToString());
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
                var request = await m_socket.GetDiscord("/users/@me?with_analytics_token=true", m_token);
                if (request.Result == CURLcode.OK)
                {
                    JObject res = JObject.Parse(request.Data);

                    string username = res["username"].ToString(),
                    discriminator = res["discriminator"].ToString();

                    if (username.Length > 0 && discriminator.Length == 4)
                    {
                        m_status = TokenStatus.Valid;
                        m_userid = Convert.ToUInt64(res["id"]);
                        m_username = username;
                        m_discriminator = discriminator;

                        //await RegenerateToken();
                        return m_status;
                    }
                }
            }
            catch { }

            m_status = TokenStatus.Invalid;
            return m_status;
        }

        public async Task<DiscordCookie> GetCookies()
        {
            try
            {
                DiscordCookie cookie = new DiscordCookie(await m_socket.GetCookies("https://discord.com/"), null);

                var request = await m_socket.GetDiscord("/experiments");
                if (request.Result == CURLcode.OK)
                    cookie.Fingerprint = JObject.Parse(request.Data)["fingerprint"].ToString();

                return cookie;
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, "[Discord Client] {0}", ex.ToString());
            }
            return null;
        }

        public async Task<string> GetInvitationContext(string invite_code, string cookie)
        {
            try
            {
                var request = await m_socket.GetDiscord(string.Format("/invites/{0}?inputValue={0}&with_counts=true&with_expiration=true", invite_code),
                    m_token, true, string.Format(@"
                {{
                    'accept':             '*/*',
			        'accept-language':    'en-US,en;q=0.9',
			        'cookie':             '{0}',
			        'referer':            'https://discord.com/channels/@me',
                    'sec-ch-ua-mobile':   '?0',
			        'sec-fetch-dest':     'empty',
			        'sec-fetch-mode':     'cors',
			        'sec-fetch-site':     'same-origin',
			        'x-debug-options':    'bugReporterEnabled',
			        'x-discord-locale':   'en-US'
                }}", cookie));

                if (request.Result == CURLcode.OK)
                {
                    // Get information of target guild
                    JObject guildinfo = JObject.Parse(request.Data);

                    return new XContext
                    {
                        GuildID = guildinfo["guild"]["id"].ToString(),
                        ChannelID = guildinfo["channel"]["id"].ToString(),
                        ChannelType = Convert.ToSingle(guildinfo["channel"]["type"])
                    }.ToString();
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
                        var request = await m_socket.GetDiscord("/users/@me?with_analytics_token=true", m_token);
                        if (request.Result == CURLcode.OK)
                        {
                            JObject res = JObject.Parse(request.Data);
                            return string.Format("{0}#{1}", res["username"], res["discriminator"]);
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
