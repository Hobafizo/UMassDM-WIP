using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UMassDM.Network.Branches
{
    public class DiscordCookie
    {
        public string CookieString;
        public string Fingerprint;

        public DiscordCookie(string cookie, string fingerprint)
        {
            CookieString = cookie;
            Fingerprint = fingerprint;
        }
    }

    public class InvitePayload
    {
        public string captcha_key;
        public string captcha_rqtoken;

        public InvitePayload(string captchakey, string reqtoken)
        {
            captcha_key = captchakey;
            captcha_rqtoken = reqtoken;
        }

        public string ToJSON()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public struct CaptchaPayload
    {
        public string captcha_sitekey;
        public string captcha_rqdata;
        public string captcha_rqtoken;

        public CaptchaPayload(string sitekey, string reqdata, string reqtoken)
        {
            captcha_sitekey = sitekey;
            captcha_rqdata = reqdata;
            captcha_rqtoken = reqtoken;
        }

        public string ToJSON()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    struct XContext
    {
        public string GuildID;
        public string ChannelID;
        public float ChannelType;

        public override string ToString()
        {
            JObject data = new JObject();

            data["Location"] = "Join Guild";
            data["LocationGuildID"] = GuildID;
            data["LocationChannelID"] = ChannelID;
            data["LocationChannelType"] = ChannelType;

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(data.ToString()));
        }
    }

    public struct TwoCaptchaSubmitResponse
    {
        public int status;
        public string request;

        public TwoCaptchaSubmitResponse(string json)
        {
            this = JsonConvert.DeserializeObject<TwoCaptchaSubmitResponse>(json);
        }

        public void Load(string json)
        {
            this = JsonConvert.DeserializeObject<TwoCaptchaSubmitResponse>(json);
        }
    }

    public struct CapSolverSubmitResponse
    {
        public int errorId;
        public string errorCode;
        public string errorDescription;
        public string taskId;

        public CapSolverSubmitResponse(string json)
        {
            this = JsonConvert.DeserializeObject<CapSolverSubmitResponse>(json);
        }
    }

    public struct CapSolverTaskResult
    {
        public struct CapSolverSolution
        {
            public string userAgent;
            public ulong expireTime;
            public ulong timestamp;
            public string captchaKey;
            public string gRecaptchaResponse;
        };

        public int errorId;
        public string errorCode;
        public string errorDescription;
        public string taskId;
        public CapSolverSolution solution;
        public string status;

        public CapSolverTaskResult(string json)
        {
            this = JsonConvert.DeserializeObject<CapSolverTaskResult>(json);
        }

        public void Load(string json)
        {
            this = JsonConvert.DeserializeObject<CapSolverTaskResult>(json);
        }
    }
}
