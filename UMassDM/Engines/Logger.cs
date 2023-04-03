using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace UMassDM.Engines
{
    public enum LogType : byte
    {
        Success,
        Error,
        Report,
        Warning
    }

    public class Logger
    {
        public static void Show(LogType type, string msg, params object[] args)
        {
            string caption = type.ToString();
            MessageBoxIcon icon = MessageBoxIcon.Information;

            switch (type)
            {
                case LogType.Error:
                    icon = MessageBoxIcon.Error;
                    break;

                case LogType.Report:
                    icon = MessageBoxIcon.Exclamation;
                    break;

                case LogType.Warning:
                    icon = MessageBoxIcon.Warning;
                    break;
            }

            MessageBox.Show(string.Format(msg, args), caption, MessageBoxButtons.OK, icon);
        }
    }
}
