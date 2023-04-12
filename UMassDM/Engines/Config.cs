using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UMassDM.Tools;
using UMassDM.Network;

namespace UMassDM.Engines
{
    public enum CaptchaService : byte
    {
        Unset,
        TwoCaptcha,
        RUCaptcha,
        CapMonster,
        CapSolver
    }

    public class CaptchaServiceInfo
    {
        public CaptchaService Type;
        public string Name;
        public string Host;

        public CaptchaServiceInfo(CaptchaService type, string name, string host)
        {
            Type = type;
            Name = name;
            Host = host;
        }
    }

    public struct Settings
    {
        // Main settings
        public int JoinWaitTime;
        public int JoinDelayBeforeToken;
        public bool ExcludeInvalid;

        // Captcha settings
        public CaptchaService CaptchaService;
        public string CaptchaAPIKey;
        public int CaptchaWaitTime;
        public int CaptchaDMRetryTimes;
        public int CaptchaInviteRetryTimes;
        public bool CaptchaUseProxy;
    }

    public class Config
    {
        public static Config Instance = new Config();

        // Constant Configuration
        private const string CfgPath = "config/settings.ini";
        private const string TokenPath = "config/tokens.txt", InvalidTokenPath = "config/invalid.txt";

        #region Class Members
        private bool m_loaded;
        private int m_apiver;
        private string m_useragent, m_xsuperproperties, m_ja3;

        public Settings Setting;

        private List<CaptchaServiceInfo> CaptchaServices;
        public List<DiscordClient> Tokens;

        public bool Loaded { get { return m_loaded; } }
        public int APIVersion { get { return m_apiver; } }
        public string UserAgent { get { return m_useragent; } }
        public string XSuperProperties { get { return m_xsuperproperties; } }
        public string JA3 { get { return m_ja3; } }
        #endregion

        public Config()
        {
            Tokens = new List<DiscordClient>();

            CaptchaServices = new List<CaptchaServiceInfo>()
            {
                new CaptchaServiceInfo(CaptchaService.TwoCaptcha, "2captcha",   "2captcha.com"),
                new CaptchaServiceInfo(CaptchaService.RUCaptcha,  "rucaptcha",  "rucaptcha.com"),
                new CaptchaServiceInfo(CaptchaService.CapMonster, "capmonster", "capmonster.cloud"),
                new CaptchaServiceInfo(CaptchaService.CapSolver,  "capsolver",  "api.capsolver.com")
            };

            // Defaults
            m_apiver = 10;
            m_useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/111.0.0.0 Safari/537.36";
            m_xsuperproperties = "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiQ2hyb21lIiwiZGV2aWNlIjoiIiwic3lzdGVtX2xvY2FsZSI6ImVuLVVTIiwiYnJvd3Nlcl91c2VyX2FnZW50IjoiTW96aWxsYS81LjAgKFdpbmRvd3MgTlQgMTAuMDsgV2luNjQ7IHg2NCkgQXBwbGVXZWJLaXQvNTM3LjM2IChLSFRNTCwgbGlrZSBHZWNrbykgQ2hyb21lLzExMS4wLjAuMCBTYWZhcmkvNTM3LjM2IiwiYnJvd3Nlcl92ZXJzaW9uIjoiMTExLjAuMC4wIiwib3NfdmVyc2lvbiI6IjEwIiwicmVmZXJyZXIiOiIiLCJyZWZlcnJpbmdfZG9tYWluIjoiIiwicmVmZXJyZXJfY3VycmVudCI6IiIsInJlZmVycmluZ19kb21haW5fY3VycmVudCI6IiIsInJlbGVhc2VfY2hhbm5lbCI6InN0YWJsZSIsImNsaWVudF9idWlsZF9udW1iZXIiOjE4NTc1OCwiY2xpZW50X2V2ZW50X3NvdXJjZSI6bnVsbCwiZGVzaWduX2lkIjowfQ==";
            m_ja3 = "771,4865-4866-4867-49195-49199-49196-49200-52393-52392-49171-49172-156-157-47-53,0-23-65281-10-11-35-16-5-13-18-51-45-43-27-17513,29-23-24,0";
        }

        public bool Load()
        {
            CaptchaServiceInfo captcha;

            try
            {
                IniFile cfg = new IniFile(CfgPath);

                // Main settings load
                Setting.JoinWaitTime = int.Parse(cfg.Read("JoinWaitTime", "Main"));
                Setting.JoinDelayBeforeToken = int.Parse(cfg.Read("JoinDelayBeforeToken", "Main"));
                Setting.ExcludeInvalid = bool.Parse(cfg.Read("ExcludeInvalidToken", "Main"));

                // Captcha settings load
                captcha = GetCaptchaService(cfg.Read("Captcha_API", "Captcha"));
                Setting.CaptchaService = captcha != null ? captcha.Type : CaptchaService.Unset;
                Setting.CaptchaAPIKey = cfg.Read("Captcha_API_Key", "Captcha");
                Setting.CaptchaWaitTime = int.Parse(cfg.Read("Captcha_WaitTime", "Captcha"));
                Setting.CaptchaDMRetryTimes = int.Parse(cfg.Read("Captcha_RetryTimes_DM", "Captcha"));
                Setting.CaptchaInviteRetryTimes = int.Parse(cfg.Read("Captcha_RetryTimes_Invite", "Captcha"));
                Setting.CaptchaUseProxy = bool.Parse(cfg.Read("Captcha_UseProxy", "Captcha"));

                // Static config load
                m_apiver = int.Parse(cfg.Read("Discord_API_Ver", "Static"));
                m_useragent = cfg.Read("UserAgent", "Static");
                m_xsuperproperties = cfg.Read("XSuperProperties", "Static");
                m_ja3 = cfg.Read("JA3", "Static");

                if (!m_loaded)
                    LoadTokens();

                // Ensure user settings input is valid
                FixUserSettings();

                m_loaded = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Show(LogType.Error, "Error on loading configuration: {0}", ex.ToString());
            }
            return false;
        }

        private void FixUserSettings()
        {
            const int CaptchaWaitTimeMin = 10;
            const int CaptchaDMRetryTimesMin = 0;
            const int CaptchaInviteRetryTimesMin = 0;

            if (Setting.CaptchaWaitTime < CaptchaWaitTimeMin)
                Setting.CaptchaWaitTime = CaptchaWaitTimeMin;

            if (Setting.CaptchaDMRetryTimes < CaptchaDMRetryTimesMin)
                Setting.CaptchaDMRetryTimes = CaptchaDMRetryTimesMin;

            if (Setting.CaptchaInviteRetryTimes < CaptchaInviteRetryTimesMin)
                Setting.CaptchaInviteRetryTimes = CaptchaInviteRetryTimesMin;
        }

        public void LoadTokens()
        {
            Tokens.Clear();

            if (File.Exists(TokenPath))
            {
                string[] splitter;

                foreach (string line in File.ReadAllLines(TokenPath))
                {
                    if (!line.StartsWith("//"))
                    {
                        splitter = line.Split(':');
                        if (splitter.Length == 3)
                            Tokens.Add(new DiscordClient(splitter[2], splitter[0], splitter[1], TokenStatus.Uninitialized, m_useragent, m_xsuperproperties, m_ja3));
                    }
                }
            }
            else
                File.CreateText(TokenPath);
        }

        public void Save()
        {
            try
            {
                IniFile cfg = new IniFile(CfgPath);
                CaptchaServiceInfo captcha = GetCaptchaService();

                cfg.Write("JoinWaitTime", Setting.JoinWaitTime.ToString(), "Main");
                cfg.Write("JoinDelayBeforeToken", Setting.JoinDelayBeforeToken.ToString(), "Main");
                cfg.Write("ExcludeInvalidToken", Setting.ExcludeInvalid.ToString(), "Main");

                cfg.Write("Captcha_API", captcha != null ? captcha.Name : "", "Captcha");
                cfg.Write("Captcha_API_Key", Setting.CaptchaAPIKey, "Captcha");
                cfg.Write("Captcha_WaitTime", Setting.CaptchaWaitTime.ToString(), "Captcha");
                cfg.Write("Captcha_RetryTimes_DM", Setting.CaptchaDMRetryTimes.ToString(), "Captcha");
                cfg.Write("Captcha_RetryTimes_Invite", Setting.CaptchaInviteRetryTimes.ToString(), "Captcha");
                cfg.Write("Captcha_UseProxy", Setting.CaptchaUseProxy.ToString(), "Captcha");
            }
            catch (Exception ex)
            {
                Logger.Show(LogType.Error, "Error on saving configuration: {0}", ex.ToString());
            }
        }

        public void SaveTokens(string tokens)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(TokenPath, false))
                {
                    writer.Write(tokens);
                    writer.Close();
                }

                LoadTokens();
            }
            catch (Exception ex)
            {
                Logger.Show(LogType.Error, "Error on saving tokens: {0}", ex.ToString());
            }
        }

        public void ExcludeInvalidTokens()
        {
            if (Setting.ExcludeInvalid)
            {
                using (StreamWriter writer = new StreamWriter(InvalidTokenPath, true))
                {
                    foreach (DiscordClient client in Tokens.Where(x => x.Status == TokenStatus.Invalid))
                        writer.WriteLine(client.ToString());
                    writer.Close();
                }

                using (StreamWriter writer = new StreamWriter(TokenPath, false))
                {
                    foreach (DiscordClient client in Tokens.Where(x => x.Status != TokenStatus.Invalid))
                        writer.WriteLine(client.ToString());
                    writer.Close();
                }

                Tokens.RemoveAll(x => x.Status == TokenStatus.Invalid);
            }
        }

        public CaptchaServiceInfo GetCaptchaService(string name)
        {
            return CaptchaServices.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public CaptchaServiceInfo GetCaptchaService()
        {
            return GetCaptchaService(Setting.CaptchaService);
        }

        public CaptchaServiceInfo GetCaptchaService(CaptchaService type)
        {
            return CaptchaServices.FirstOrDefault(x => x.Type == type);
        }
    }
}
