using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Office.Tools.Ribbon;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;




namespace plinxl
{
    public partial class Ribbon1
    {
        private void Ribbon1_Load(object sender, RibbonUIEventArgs e)
        { }

        private void reset_Click(object sender, RibbonControlEventArgs e)
        {
            if (ThisAddIn.taskPaneValue == null || ThisAddIn.taskPaneValue.Visible == false)
                _ = ThisAddIn.createConsole();
            _ = ThisAddIn.cleanConsole();
            CODE.reset();
        }

        private void consultWS_Click(object sender, RibbonControlEventArgs e)
        {
            _ = ThisAddIn.userINPUT("?- consult.");
        }

        private void about_Click(object sender, RibbonControlEventArgs e)
        {
            string publishVersion = "?";
            if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed)
            {
                System.Deployment.Application.ApplicationDeployment cd = System.Deployment.Application.ApplicationDeployment.CurrentDeployment;
                publishVersion = cd.CurrentVersion.ToString();
            }
            System.Windows.Forms.MessageBox.Show("Plinxl version: " + publishVersion);
        }

        private void plixWeb_Click(object sender, RibbonControlEventArgs e)
        {
            System.Diagnostics.Process.Start(@"https://plinxl.com/");
        }

        private void traceOnOff_Click(object sender, RibbonControlEventArgs e)
        {}

        private void consoleOnOff_Click(object sender, RibbonControlEventArgs e)
        {
            _ = ThisAddIn.createConsole();
            Globals.ThisAddIn.TaskPane.Visible = ((RibbonToggleButton)sender).Checked;
        }

        private void backtrackNext_Click(object sender, RibbonControlEventArgs e)
        {
            //Solver.UserWhishAnotherSolution = true;
            //ThisAddIn.QuerySolver.NEXT_QuerySolution();
            ThisAddIn.NEXT_QuerySolution();
        }

        private void backtrackEnd_Click(object sender, RibbonControlEventArgs e)
        {
            Debug.WriteLine("backtrackEnd_Click !!!!!!!!!!!!!!!!!!");
            ThisAddIn.END_Query();
        }
    }
}
