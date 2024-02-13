using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace plinxl
{
    internal class Tracer
    {

        internal static bool outputTrace(string port, string msg, int tag)
        {
            if (!Globals.Ribbons.Ribbon1.tracer.Checked)
                return true;

            string Msg = port + ": [" + tag + "] " + msg;
            Debug.WriteLine("trace " + Msg);
            _ = ThisAddIn.OUTPUT(Msg, Color.Gray);

            //DialogResult result = MessageBox.Show(Msg, "trace", MessageBoxButtons.OKCancel);
            //if (result == DialogResult.Cancel)
            //    throw new plixException("End from tracer          #987");

            return true;
        }

    }
}
