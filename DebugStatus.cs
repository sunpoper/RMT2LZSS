using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace RMT
{

    public class DebugStatus
    {
        static System.Windows.Forms.TextBox s_statusBox;
        static public bool s_verbose = true;
        static public bool s_enabled;

        public static void InitDebugStatus(System.Windows.Forms.TextBox statusBox)
        {
            s_statusBox = statusBox;
            s_enabled = true;
        }

        //-------------------------------------------------------------------------------------------------------------------

        public enum VerboseOptions
        {
            AlwaysOutput,
            MaxVerboseOnly,
        }

        static public void Clear()
        {
            if (s_statusBox != null)
            {
                s_statusBox.Lines = new string[1];
                s_statusBox.Refresh();
            }
        }


        static public void Print(string statusLine, VerboseOptions vo = VerboseOptions.AlwaysOutput)
        {
            if ((s_verbose || (vo != VerboseOptions.MaxVerboseOnly)) && s_enabled)
            {
                if (s_statusBox == null)
                {
                    Debug.Print(statusLine);
                }
                else
                {
                    string[] newLines = new string[s_statusBox.Lines.Count() + 1];
                    s_statusBox.Lines.CopyTo(newLines, 0);
                    newLines[s_statusBox.Lines.Count()] = statusLine;
                    s_statusBox.Lines = newLines;
                }

            }

            s_statusBox.Update();
            s_statusBox.Focus();

        }
    }
}