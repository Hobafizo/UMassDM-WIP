using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UMassDM.Network.Branches
{
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
        private string m_data;

        public XContext(string guildid, string channelid, float channeltype)
        {
            m_data = string.Format(@"
            {{
                'Location':            'Join Guild',
		        'LocationGuildID':     {0},
		        'LocationChannelID':   {1},
		        'LocationChannelType': {2}
            }}"
            , guildid, channelid, channeltype);
        }

        public override string ToString()
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(JObject.Parse(m_data).ToString()));
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
}
