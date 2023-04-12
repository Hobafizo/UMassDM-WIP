using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using CurlThin;
using CurlThin.Native;
using CurlThin.Enums;
using CurlThin.Helpers;
using CurlThin.SafeHandles;
using UMassDM.Engines;
using UMassDM.Utils;
using Newtonsoft.Json.Linq;

namespace UMassDM.Network
{
    public struct CurlResponse
    {
        public CURLcode Result;
        public HttpStatusCode StatusCode;
        public string Data;
        public string Headers;
    }

    public class CurlClient
    {
        private const bool IsBot = false;
        private static string AuditLogReason = "UMDM";

        private bool m_initialized;
        private bool m_ssluse;
        private string m_useragent, m_xsuperproperties, m_ja3;

        public CurlClient(bool ssl = false, string useragent = null, string xsuperproperties = null, string ja3 = null)
        {
            CurlResources.Init();

            m_initialized = CurlNative.Init() == CURLcode.OK;
            m_ssluse = ssl;

            m_useragent = useragent != null ? useragent : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36";
            m_xsuperproperties = xsuperproperties != null ? xsuperproperties : "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiQ2hyb21lIiwiZGV2aWNlIjoiIiwic3lzdGVtX2xvY2FsZSI6ImVuLVVTIiwiYnJvd3Nlcl91c2VyX2FnZW50IjoiTW96aWxsYS81LjAgKFdpbmRvd3MgTlQgMTAuMDsgV2luNjQ7IHg2NCkgQXBwbGVXZWJLaXQvNTM3LjM2IChLSFRNTCwgbGlrZSBHZWNrbykgQ2hyb21lLzExMS4wLjAuMCBTYWZhcmkvNTM3LjM2IiwiYnJvd3Nlcl92ZXJzaW9uIjoiMTExLjAuMC4wIiwib3NfdmVyc2lvbiI6IjEwIiwicmVmZXJyZXIiOiIiLCJyZWZlcnJpbmdfZG9tYWluIjoiIiwicmVmZXJyZXJfY3VycmVudCI6IiIsInJlZmVycmluZ19kb21haW5fY3VycmVudCI6IiIsInJlbGVhc2VfY2hhbm5lbCI6InN0YWJsZSIsImNsaWVudF9idWlsZF9udW1iZXIiOjE4NzEyMSwiY2xpZW50X2V2ZW50X3NvdXJjZSI6bnVsbCwiZGVzaWduX2lkIjowfQ==";
            m_ja3 = ja3;
        }

        ~CurlClient()
        {
            if (m_initialized)
                CurlNative.Cleanup();
        }

        public async Task<CurlResponse> GetDiscord(string endpoint, string auth = null, bool xsuper = false, string headers = null)
        {
            return await Get(string.Format("https://discord.com/api/v{0}{1}", Config.Instance.APIVersion, endpoint), auth, xsuper, headers);
        }

        public async Task<CurlResponse> Get(string endpoint, string auth = null, bool xsuper = false, string headers = null)
        {
            if (!m_initialized)
                return new CurlResponse
                {
                    Result = CURLcode.FAILED_INIT,
                    StatusCode = HttpStatusCode.Unused,
                    Data = null,
                    Headers = null
                };

            SafeSlistHandle headerlist;

            using (SafeEasyHandle easy = GetEasy(endpoint, auth, xsuper, headers, out headerlist))
            {
                // set headers
                CurlNative.Easy.SetOpt(easy, CURLoption.HTTPHEADER, headerlist.DangerousGetHandle());

                // result data contexts
                DataCallbackCopier ResData = new DataCallbackCopier(), HeaderData = new DataCallbackCopier();
                CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, ResData.DataHandler);
                CurlNative.Easy.SetOpt(easy, CURLoption.HEADERFUNCTION, HeaderData.DataHandler);

                CURLcode result = await Task.Run(() => CurlNative.Easy.Perform(easy));

                return new CurlResponse
                {
                    Result = result,
                    StatusCode = easy.StatusCode(),
                    Data = Encoding.UTF8.GetString(ResData.Stream.ToArray()),
                    Headers = Encoding.UTF8.GetString(HeaderData.Stream.ToArray())
                };
            }
        }

        public async Task<CurlResponse> PostDiscord(string endpoint, string auth = null, bool xsuper = false, string headers = null, string json = null)
        {
            return await Post(string.Format("https://discord.com/api/v{0}{1}", Config.Instance.APIVersion, endpoint), auth, xsuper, headers, json);
        }

        public async Task<CurlResponse> Post(string endpoint, string auth = null, bool xsuper = false, string headers = null, string json = null)
        {
            if (!m_initialized)
                return new CurlResponse
                {
                    Result = CURLcode.FAILED_INIT,
                    StatusCode = HttpStatusCode.Unused,
                    Data = null,
                    Headers = null
                };

            SafeSlistHandle headerlist;

            using (SafeEasyHandle easy = GetEasy(endpoint, auth, xsuper, headers, out headerlist))
            {
                if (json != null)
                {
                    headerlist = headerlist.AppendHeader("Content-Type", "application/json");
                    json = JObject.Parse(json).ToString();
                    CurlNative.Easy.SetOpt(easy, CURLoption.POSTFIELDSIZE, Encoding.ASCII.GetByteCount(json));
                    CurlNative.Easy.SetOpt(easy, CURLoption.COPYPOSTFIELDS, json);
                }

                CurlNative.Easy.SetOpt(easy, CURLoption.POST, 1);

                // set headers
                CurlNative.Easy.SetOpt(easy, CURLoption.HTTPHEADER, headerlist.DangerousGetHandle());

                // result data contexts
                DataCallbackCopier ResData = new DataCallbackCopier(), HeaderData = new DataCallbackCopier();
                CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, ResData.DataHandler);
                CurlNative.Easy.SetOpt(easy, CURLoption.HEADERFUNCTION, HeaderData.DataHandler);

                CURLcode result = await Task.Run(() => CurlNative.Easy.Perform(easy));

                return new CurlResponse
                {
                    Result = result,
                    StatusCode = easy.StatusCode(),
                    Data = Encoding.UTF8.GetString(ResData.Stream.ToArray()),
                    Headers = Encoding.UTF8.GetString(HeaderData.Stream.ToArray())
                };
            }
        }

        public async Task<string> GetCookies(string endpoint, string auth = null, bool xsuper = false, string headers = null)
        {
            if (m_initialized)
            {
                var response = await Get(endpoint, auth, xsuper, headers);
                if (response.Result == CURLcode.OK)
                {
                    string cookies = string.Empty;
                    
                    foreach (string header in response.Headers.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                    {
                        if (header.StartsWith("set-cookie: "))
                            cookies += header.Substring(12) + "; ";
                    }

                    return cookies.Length > 1 ? cookies.Remove(cookies.Length - 2) : cookies;
                }
            }
            return null;
        }

        private SafeEasyHandle GetEasy(string url, string auth, bool xsuper, string headers, out SafeSlistHandle headerlist)
        {
            SafeEasyHandle easy = CurlNative.Easy.Init();

            CurlNative.Easy.SetOpt(easy, CURLoption.URL, url);

            // Initialize SSL/TLS
            CurlNative.Easy.SetOpt(easy, CURLoption.CAINFO, CurlResources.CaBundlePath);

            if (m_ssluse && m_ja3 != null)
            {
                CurlNative.Easy.SetOpt(easy, CURLoption.SSLVERSION, (int)SSLVersion.CURL_SSLVERSION_TLSv1_2);
                CurlNative.Easy.SetOpt(easy, CURLoption.SSL_ENABLE_ALPN, (int)SSLVersion.CURL_SSLVERSION_TLSv1_2);
                CurlNative.Easy.SetOpt(easy, CURLoption.SSL_CIPHER_LIST, "TLS_AES_128_GCM_SHA256:TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-RSA-AES128-SHA:ECDHE-RSA-AES256-SHA:AES128-GCM-SHA256:AES256-GCM-SHA384:AES128-SHA:AES256-SHA");
            }

            headerlist = CurlNative.Slist.Append(SafeSlistHandle.Null, string.Empty);

            // Load client headers
            if (auth != null)
                headerlist = headerlist.AppendHeader("Authorization", string.Format("{0}{1}", IsBot ? "Bot " : "", auth));

            headerlist = headerlist.AppendHeader("User-Agent", m_useragent);
            if (xsuper)
                headerlist = headerlist.AppendHeader("X-Super-Properties", m_xsuperproperties);

            if (headers != null)
            {
                foreach (var header in JObject.Parse(headers))
                    headerlist = headerlist.AppendHeader(header.Key, header.Value.ToString());
            }

            return easy;
        }
    }
}
