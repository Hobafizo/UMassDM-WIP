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

        public static bool Debugging = true;
        
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
            LoadTitle();

            Request.FixSSLTlsChannels();

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
            await Config.Instance.Tokens[0].JoinGuild("vGFKXNpEQr");
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
