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
using UMassDM.Utils;
using Zennolab.CapMonsterCloud;
using Zennolab.CapMonsterCloud.Requests;

namespace UMassDM.Engines
{
    public class CaptchaResolver
    {
        // Time to wait before re-checking captcha request status.
        private const int CaptchaStatusCheckDelay = 5000;
        private const int CapSolverStatusCheckDelay = 5000;

        public static async Task<string> SolveCaptcha(CurlClient client, CaptchaPayload payload, string cookie, string url, string proxy, int timeout = 0)
        {
            if (timeout > 0)
                return await GetCaptchaSolution(client, payload, cookie, url, proxy).TimeoutAfter(TimeSpan.FromSeconds(Config.Instance.Setting.CaptchaWaitTime));
            return await GetCaptchaSolution(client, payload, cookie, url, proxy);
        }

        private static async Task<string> GetCaptchaSolution(CurlClient client, CaptchaPayload payload, string cookie, string url, string proxy)
        {
            CaptchaServiceInfo service = Config.Instance.GetCaptchaService();
            if (service != null)
            {
                switch (service.Type)
                {
                    case CaptchaService.RUCaptcha:
                    case CaptchaService.TwoCaptcha:
                        return await Solve2Captcha(client, service, payload, url, proxy);

                    case CaptchaService.CapMonster:
                        return await SolveCapMonster(client, service, payload, url, proxy);

                    case CaptchaService.CapSolver:
                        return await SolveCapSolver(client, service, payload, url, proxy);
                }
            }
            return null;
        }

        private static async Task<string> Solve2Captcha(CurlClient client, CaptchaServiceInfo service, CaptchaPayload payload, string url, string proxy)
        {
            const string request_url = "https://2captcha.com/in.php",
                         response_url = "https://2captcha.com/res.php";

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

                uri.Host = service.Host;
                uri.Query = query.ToString();

                // send captcha solution request
                var response = await client.Get(uri.ToString());
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    TwoCaptchaSubmitResponse reqinfo = new TwoCaptchaSubmitResponse(response.Data);

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

                        uri.Host = service.Host;
                        uri.Query = query.ToString();

                        // loop captcha request status
                        while (true)
                        {
                            await Task.Delay(CaptchaStatusDelay(service.Type));

                            response = await client.Get(uri.ToString());
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                reqinfo.Load(response.Data);

                                /*if (reqinfo.request != "CAPCHA_NOT_READY") //captcha is not ready yet
                                {

                                }
                                 
                                else*/
                                if (reqinfo.request.Contains("ERROR")) //service faced an error while solving
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
                Logger.Show(LogType.Error, "[Captcha Resolver] Exception caught on solving 2Captcha: {0}", ex.ToString());
            }
            return null;
        }

        private static async Task<string> SolveCapMonster(CurlClient client, CaptchaServiceInfo service, CaptchaPayload payload, string url, string proxy)
        {
            // NOT TESTED, 99% BUGGED

            try
            {
                var clientOptions = new ClientOptions
                {
                    ClientKey = Config.Instance.Setting.CaptchaAPIKey
                };

                var cmCloudClient = CapMonsterCloudClientFactory.Create(clientOptions);

                // solve HCaptcha (without proxy)
                var hcaptchaRequest = new HCaptchaProxylessRequest
                {
                    WebsiteUrl = url,
                    WebsiteKey = payload.captcha_sitekey
                };

                var res = await cmCloudClient.SolveAsync(hcaptchaRequest);
                if (!res.Error.HasValue)
                    return res.Solution.Value;
            }
            catch (Exception ex)
            {
                Logger.Show(LogType.Error, "[Captcha Resolver] Exception caught on solving Cap Monster: {0}", ex.ToString());
            }
            return null;
        }

        private static async Task<string> SolveCapSolver(CurlClient client, CaptchaServiceInfo service, CaptchaPayload payload, string url, string proxy)
        {
            const string request_url  = "https://api.capsolver.com/createTask",
                         response_url = "https://api.capsolver.com/getTaskResult";

            try
            {
                // send captcha solution request
                var response = await client.Post(request_url, null, false, string.Format("{{ 'Host': '{0}' }}", service.Host),
                    string.Format(@"{{
                                        'clientKey':                '{0}',

                                        'task':
                                        {{
                                            'type':                 '{3}',
                                            'websiteURL':           '{2}',
                                            'websiteKey':           '{1}',
                                            'isInvisible':          true,
                                            {4}
                                            {5}
                                            'userAgent':            '{6}'
                                        }}
                                    }}"
                , Config.Instance.Setting.CaptchaAPIKey, payload.captcha_sitekey, url, !Config.Instance.Setting.CaptchaUseProxy ? "HCaptchaEnterpriseTaskProxyLess" : "HCaptchaEnterpriseTask",

                string.IsNullOrEmpty(payload.captcha_rqdata) ? "" : string.Format(@"
                        'enterprisePayload':
                        {{
                            'rqdata':   '{0}'
                        }},
                       ", payload.captcha_rqdata),

                !Config.Instance.Setting.CaptchaUseProxy ? "" : string.Format(@"
                        'proxy':        '{0}',  
                        'enableIPV6':   false,
                       ", "de.proxiware.com:22001"),

                     Config.Instance.UserAgent));

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    CapSolverSubmitResponse reqinfo = new CapSolverSubmitResponse(response.Data);

                    // on captcha request accepted & processing
                    if (reqinfo.errorId == 0)
                    {
                        CapSolverTaskResult result = new CapSolverTaskResult();

                        // loop captcha request status
                        while (true)
                        {
                            await Task.Delay(CaptchaStatusDelay(service.Type));

                            response = await client.Post(response_url, null, false, string.Format("{{ 'Host': '{0}' }}", service.Host),
                                string.Format(@"{{
                                                    'clientKey':    '{0}',
                                                    'taskId':       '{1}'
                                                }}", Config.Instance.Setting.CaptchaAPIKey, reqinfo.taskId));

                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                result.Load(response.Data);

                                /*if (result.status != "ready") //captcha is not ready yet
                                {

                                }
                                 
                                else*/
                                if (result.errorId != 0) //service faced an error while solving
                                    return null;

                                else if (result.status == "ready") // captcha is solved!
                                    return result.solution.gRecaptchaResponse;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Show(LogType.Error, "[Captcha Resolver] Exception caught on solving 2Captcha: {0}", ex.ToString());
            }
            return null;
        }

        private static int CaptchaStatusDelay(CaptchaService type)
        {
            switch (type)
            {
                case CaptchaService.CapSolver:
                    return CapSolverStatusCheckDelay;
            }
            return CaptchaStatusCheckDelay;
        }
    }
}
