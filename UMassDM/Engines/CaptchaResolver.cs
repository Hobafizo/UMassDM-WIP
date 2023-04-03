using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Web;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UMassDM.Network;
using UMassDM.Network.Branches;

namespace UMassDM.Engines
{
    public class CaptchaResolver
    {
        // Time to wait before re-checking captcha request status.
        private const int RequestStatusCheckDelay = 5000;

        public static async Task<string> SolveCaptcha(CaptchaPayload payload, string cookie, string url, string proxy)
        {
            switch (Config.Instance.Setting.CaptchaService)
            {
                case CaptchaService.RUCaptcha:
                case CaptchaService.TwoCaptcha:
                    return await Solve2Captcha(payload, url, proxy);
            }

            return null;
        }

        private static async Task<string> Solve2Captcha(CaptchaPayload payload, string url, string proxy)
        {
            const string request_url =  "https://2captcha.com/in.php",
                         response_url = "https://2captcha.com/res.php";
            string host = Config.Instance.Setting.CaptchaService == CaptchaService.RUCaptcha ? "rucaptcha.com" : "2captcha.com";

            try
            {
                // build captcha request url
                UriBuilder uri = new UriBuilder(request_url);
                var query = HttpUtility.ParseQueryString(uri.Query);

                foreach (var entry in JObject.Parse(string.Format(@"
                    {{
                        'key':          '{0}',
                        'method':       'hcaptcha',
                        'sitekey':      '{1}',
                        'pageurl':      'https://discord.com',
                        'userAgent':    '{2}',
                        'json':         '1',
                        'soft_id':      '3359',
                        {3}
                        {4}
                    }}", Config.Instance.Setting.CaptchaAPIKey, payload.captcha_sitekey, Config.Instance.UserAgent,

                     string.IsNullOrEmpty(payload.captcha_rqdata) ? "" : string.Format(@"
                        'data':         '{0}',
                        'invisible':    '0',
                       ", payload.captcha_rqdata),

                     !Config.Instance.Setting.CaptchaUseProxy ? "" : string.Format(@"
                        'proxy':        '{0}',
                        'proxytype':    'http',
                       ", proxy)
                     )))
                {
                    query[entry.Key] = entry.Value.ToString();
                }

                uri.Host = host;
                uri.Query = query.ToString();

                // send captcha solution request
                var response = await Request.SendGet(uri.ToString());
                if (response.Key == HttpStatusCode.OK && response.Value != null)
                {
                    TwoCaptchaSubmitResponse reqinfo = new TwoCaptchaSubmitResponse(response.Value);

                    // on captcha request accepted & processing
                    if (reqinfo.status == 1)
                    {
                        // build captcha status check url
                        uri = new UriBuilder(response_url);
                        query = HttpUtility.ParseQueryString(uri.Query);

                        foreach (var entry in JObject.Parse(string.Format(@"
                        {{
                            'key':          '{0}',
                            'action':       'get',
                            'id':           '{1}',
                            'json':         '1'
                        }}
                        ", Config.Instance.Setting.CaptchaAPIKey, reqinfo.request)))
                        {
                            query[entry.Key] = entry.Value.ToString();
                        }

                        uri.Host = host;
                        uri.Query = query.ToString();

                        // loop captcha request status
                        while (true)
                        {
                            await Task.Delay(RequestStatusCheckDelay);

                            response = await Request.SendGet(uri.ToString());
                            if (response.Key == HttpStatusCode.OK && response.Value != null)
                            {
                                reqinfo.Load(response.Value);

                                /*if (reqinfo.request == "CAPCHA_NOT_READY") //captcha is not ready yet
                                {

                                }
                                 
                                else*/ if (reqinfo.request.Contains("ERROR")) //service faced an error while solving
                                    return null;

                                else if (reqinfo.status == 1) // captcha is solved!
                                    return reqinfo.request;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Show(LogType.Error, "[Captcha Resolver] {0}", ex.ToString());
            }
            return null;
        }
    }
}
