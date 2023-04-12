using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UMassDM.Engines;
using UMassDM.Network;

namespace UMassDM
{
    public partial class MainForm : Form
    {
        public static MainForm Instance;

        #region Static Config
        enum PanelType : byte
        {
            Tokens = 1,
            Settings = 2,
            All = Tokens | Settings
        }

        enum ActionType : byte
        {
            StatusCheck,
            JoinGuild
        }

        public static bool Debugging = false;
        
        private const string AppName    = "UMassDM",
                             AppDesc    = "Why don't you make it easier!",
                             AppVersion = "1.0.0";

        private const int StatusCheckTimeout = 10, JoinGuildTimeout = 30;
        #endregion

        public MainForm()
        {
            InitializeComponent();

            if (Instance == null)
                Instance = this;
        }

        private void LoadTitle(bool tokens = false)
        {
            try
            {
                string text;

                if (!tokens)

                    text = string.Format("{0} - {1} - {2}", AppName, AppVersion, AppDesc);
                else
                {
                    int valid = Config.Instance.Tokens.Count(x => x.Status == TokenStatus.Valid),
                        invalid = Config.Instance.Tokens.Count(x => x.Status == TokenStatus.Invalid);
                    text = string.Format("{0} - {1} - {2} | (Tokens: {3} - Valid: {4} - Invalid {5})", AppName, AppVersion, AppDesc, Config.Instance.Tokens.Count, valid, invalid);
                }

                this.Invoke((MethodInvoker)delegate
                {
                    this.Text = text;
                });
            }
            catch (Exception)
            {
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                LoadTitle();

                if (Config.Instance.Load())
                {
                    PutLog(Color.LimeGreen, "{0} configuration loaded succcessfully.", AppName);

                    LoadPanel(PanelType.All);

                    // Load tokens status (valid, invalid?)
                    HandleTokensAction(ActionType.StatusCheck);

                    TestShyt();
                }
                else
                    PutLog(Color.DarkRed, "Failed to load {0} configuration.", AppName);

                this.ConfigStatus.Image = Config.Instance.Loaded ? Properties.Resources.checkbox : Properties.Resources.xx;
                LoadTitle(true);
            }
            catch (Exception ex)
            {
                if (MainForm.Debugging)
                    Logger.Show(LogType.Error, ex.ToString());
            }
        }

        #region Log Box Events
        private List<Color> m_logcolors = new List<Color>();

        public void PutLog(Color color, string msg, params object[] args)
        {
            m_logcolors.Add(color);

            lstLogBox.Invoke((MethodInvoker)delegate
            {
                lstLogBox.Items.Add("[" + DateTime.Now + "] " + string.Format(msg, args));
                lstLogBox.TopIndex = lstLogBox.Items.Count - 1;
            });
        }

        private void lstLogBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            e.Graphics.DrawString(lstLogBox.Items[e.Index].ToString(), e.Font, new SolidBrush(m_logcolors[e.Index]),
                e.Bounds.Left, e.Bounds.Top);
            e.DrawFocusRectangle();
        }

        private void lstLogBox_DoubleClick(object sender, EventArgs e)
        {
            Clipboard.SetText(lstLogBox.SelectedItem.ToString());
        }
        #endregion

        private async void TestShyt()
        {
            //Console.WriteLine(await Config.Instance.Tokens[0].Username());
            await Config.Instance.Tokens[0].JoinGuild("rhd57rDaeS");

            /*UMassDM.Network.Branches.CaptchaPayload captcha = new Network.Branches.CaptchaPayload();
            captcha.captcha_sitekey = "a9b5fb07-92ff-493f-86fe-352a2803b3df";
            captcha.captcha_rqdata  = "SiCjfbe3RbGulVxK5330YnDxHdL+YI80L7s/DyG4ul0RjPSC0Kervst2Zv5/GxvK+RqogSokfj70AJbsVvsXprSQR2pIJU1283pESTDGcBwKXe+13PKQTp3cyoXjhUtxkQCv8zqlkExVjUGoF53NOw==y5JclQYbRrM5ShBd";
            captcha.captcha_rqtoken = "ImdvV1BIUnV2VkJvR25wYklSY0FQSUxhWWxPT0JTUmdaRUgrYzlFWHJHdVFZV3E3azkrcWdIMkVvYlZOd2E4V2RERVhKWGc9PUtsejdUOHUwWHc1RmdrcWwi.ZC8jKg.cxLGN9Cu4iGye_FyWMc24m5mf04";

            string cookie = "__dcfduid=ea124c70d4aa11ed83afe1895f435491; __sdcfduid=ea124c71d4aa11ed83afe1895f43549128066ecce57776757adb5a78614bfad9f4d2c518551eea579f3d6f0ef5283650; __cfruid=b945d3121ba3022afabfa4d21f7b3cb3bebe4778-1680806605; locale=en-US";
            string solution = await CaptchaResolver.SolveCaptcha(captcha, cookie, "https://discord.com/channels/@me", "", Config.Instance.Setting.CaptchaWaitTime);

            Console.WriteLine(@"{{
                                    ""captcha_key"": ""{0}"",
                                    ""captcha_rqtoken"": ""{1}""
                                }}", solution, captcha.captcha_rqtoken);*/
        }

        private void LoadPanel(PanelType panel)
        {
            if ((panel & PanelType.Tokens) == PanelType.Tokens)
            {
                tokens_box.Text = string.Empty;

                foreach (DiscordClient client in Config.Instance.Tokens)
                    tokens_box.Text += string.Format("{0}{1}", client.ToString(), Environment.NewLine);
            }

            if ((panel & PanelType.Settings) == PanelType.Settings)
            {
                excinvalid_checkbox.Checked = Config.Instance.Setting.ExcludeInvalid;
            }
        }

        private void ConfigStatus_Click(object sender, EventArgs e)
        {
            ConfigStatus.Image = Properties.Resources.reload;
            ConfigStatus.Image = Config.Instance.Load() ? Properties.Resources.checkbox : Properties.Resources.xx;

            if (Config.Instance.Load())
            {
                PutLog(Color.LimeGreen, "{0} configuration reloaded succcessfully.", AppName);
                ConfigStatus.Image = Properties.Resources.checkbox;
            }
            else
            {
                PutLog(Color.DarkRed, "Failed to reload {0} configuration.", AppName);
                ConfigStatus.Image = Properties.Resources.xx;
            }
        }

        private void token_save_btn_Click(object sender, EventArgs e)
        {
            Config.Instance.SaveTokens(tokens_box.Text);
            LoadTitle(true);

            HandleTokensAction(ActionType.StatusCheck);
        }

        private void excinvalid_checkbox_CheckedChanged(object sender, EventArgs e)
        {
            Config.Instance.Setting.ExcludeInvalid = excinvalid_checkbox.Checked;
            Config.Instance.Save();
        }

        private async void OnTokenAction(ActionType type, DiscordClient client)
        {
            switch (type)
            {
                case ActionType.StatusCheck:
                    {
                        await client.CheckTokenStatus();
                        LoadTitle(true);
                        break;
                    }

                case ActionType.JoinGuild:
                    {
                        break;
                    }
            }
        }

        private void HandleTokensAction(ActionType type, int threadcnt = -1)
        {
            //one thread
            if (threadcnt == 0)
            {
                Thread th = new Thread(x =>
                {
                    foreach (DiscordClient client in Config.Instance.Tokens)
                        OnTokenAction(type, client);
                });
                th.Start();

                HandleThreadTimeout(type, th);
            }

            //thread limited
            /*else if (threadcnt > 0 && threadcnt < Config.Instance.Tokens.Count)
            {

            }*/

            //all threads (threads count = tokens count)
            else
            {
                List<Thread> threads = new List<Thread>();

                foreach (DiscordClient client in Config.Instance.Tokens)
                {
                    Thread th = new Thread(x => OnTokenAction(type, client));
                    th.Start();
                    threads.Add(th);
                }

                HandleThreadTimeout(type, threads);
            }
        }

        private async void HandleThreadTimeout(ActionType action, Thread thread)
        {
            int delay = 1000;

            switch (action)
            {
                case ActionType.StatusCheck:
                    delay *= StatusCheckTimeout;
                    break;

                case ActionType.JoinGuild:
                    delay *= JoinGuildTimeout;
                    break;
            }

            await Task.Delay(delay);
            if (thread.IsAlive)
                thread.Abort();

            ExcludeInvalidTokens();
        }

        private async void HandleThreadTimeout(ActionType action, List<Thread> threads)
        {
            string msg = null;
            int delay = 1000;

            switch (action)
            {
                case ActionType.StatusCheck:
                    msg = "Tokens status check had finished.";
                    delay *= StatusCheckTimeout;
                    break;

                case ActionType.JoinGuild:
                    delay *= JoinGuildTimeout;
                    break;
            }

            await Task.Delay(delay);
            foreach (Thread thread in threads)
            {
                if (thread.IsAlive)
                    thread.Abort();
            }

            if (!string.IsNullOrEmpty(msg))
                PutLog(Color.Green, msg);

            ExcludeInvalidTokens();
        }

        private void ExcludeInvalidTokens()
        {
            Config.Instance.ExcludeInvalidTokens();
            LoadPanel(PanelType.Tokens);
        }
    }
}
