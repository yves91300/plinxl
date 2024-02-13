using System;
using System.Collections.Generic;
using Excel = Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Font = System.Drawing.Font;
using System.Threading;

namespace plinxl
{
    public partial class ThisAddIn
    {
        internal static Microsoft.Office.Tools.CustomTaskPane taskPaneValue;
        public Microsoft.Office.Tools.CustomTaskPane TaskPane
        { get { return taskPaneValue; } }
        private static System.Windows.Forms.UserControl consoleArea;
        internal static System.Windows.Forms.RichTextBox outputConsole;
        private static System.Windows.Forms.TextBox inputConsole;
        private static string inputConsolePrevious;
        private static Thread ASK_Query_Thread;
        internal static Solver QuerySolver;


        public void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            Debug.WriteLine("-------ThisAddIn_Startup---------------------");
            CODE.reset();
        }
        internal static void consult()
        {
            Debug.WriteLine("---consult----------------------");
            Excel.Worksheet wk = Globals.ThisAddIn.Application.ActiveSheet;
            Excel.Range startCell = wk.Cells[1, 1];
            Excel.Range endCell = wk.Cells.SpecialCells(Excel.XlCellType.xlCellTypeLastCell, Type.Missing);

            //Erase all previous clauses 
            CODE.reset();

            while (startCell.Row <= endCell.Row || startCell.Column <= endCell.Column)
            {
                //Debug.WriteLine("consult startCell " + startCell.Row);
                Parser parser = new Parser();
                string r = "";
                try
                {    
                    r = parser.parseCell(startCell); 
                }
                catch (Exception e)
                {    _ = ThisAddIn.OUTPUT(e.Message, Color.Red); }

                inputConsolePrevious = parser.associatedClause.GenuineUserTxtClause;

                if (r == "F" || r == "R")
                {
                    CODE.assertz(parser.associatedClause);
                    _ = Tracer.outputTrace("New clause", parser.associatedClause.GenuineUserTxtClause, 0);
                }
                else if (r == "E")
                    break;

                startCell = wk.Cells[parser.ClauseLastCell.Row, parser.ClauseLastCell.Column + 1];
            }
            _ = ThisAddIn.OUTPUT(CODE.GetClausesCount() + " clauses.", Color.Black);
        }

        private void Application_SheetBeforeDoubleClick(object Sh, Excel.Range Target, ref bool Cancel)
        {
            Debug.WriteLine("");
            Debug.WriteLine("---ThisWorkbook_SheetBeforeDoubleClick----------------------");

            //Query has to start with a  ?-  
            if (Regex.IsMatch(Target.Text, @"^\s*\?-"))
            {
                if (ThisAddIn.taskPaneValue == null || ThisAddIn.taskPaneValue.Visible == false)
                    _ = ThisAddIn.createConsole();

                //Parse compile execute the Query
                Parser parser = new Parser();                               
                //String r = parser.parseCell(Target);
                string r = "";
                try
                {
                    r = parser.parseCell(Target);
                }
                catch (Exception e)
                { _ = ThisAddIn.OUTPUT(e.Message, Color.Red); }

                String queryTxt = parser.associatedClause.GenuineUserTxtClause;
                _ = OUTPUT("", Color.Black);
                _ = OUTPUT(queryTxt, Color.Black);
                inputConsolePrevious = queryTxt;

                if (r == "Q")
                    ThisAddIn.ASK_Query_to_Solver(parser.associatedClause);
                else
                    _ = OUTPUT("Not a valid query", Color.Red);

                Cancel = true;      //Excel internal return.
            }
        }

        internal static bool OUTPUT(string message, Color color, bool newLine = true)
        {
            if (taskPaneValue == null || taskPaneValue.Visible == false)
                _ = createConsole();

            if (outputConsole.InvokeRequired)
            {
                outputConsole.Invoke((MethodInvoker)delegate { OUTPUT(message, color, newLine); });
                return true;
            }

            outputConsole.SelectionStart = outputConsole.TextLength;
            outputConsole.SelectionLength = 0;
            outputConsole.SelectionColor = color;
            outputConsole.AppendText(message);
            outputConsole.SelectionColor = outputConsole.ForeColor;

            if (newLine)
                outputConsole.AppendText(Environment.NewLine);
            outputConsole.Refresh();

            return true;
        }

        internal static bool userINPUT(string inputText)
        {
            if (taskPaneValue == null || taskPaneValue.Visible == false)
                _ = createConsole();

            _ = OUTPUT("", Color.Black);
            _ = OUTPUT(inputText, Color.Black);

            Parser parser = new Parser();
            String r = parser.parseString(inputText);
            inputConsolePrevious = inputText;
            inputConsole.Text = "?- ";

            if (r == "Q")
                ThisAddIn.ASK_Query_to_Solver(parser.associatedClause);
            else
                _ = OUTPUT("Not a valid query", Color.Red);

            inputConsole.Select(inputConsole.Text.Length, 0);
            inputConsole.Refresh();
            return true;
        }

        private static void ASK_Query_to_Solver(Clause QueryClause)
        {            
            if (QueryClause == null) throw new plixException("Invalid query.  #621");

            //Create a QuerySolver from the QueryClause
            List<HEAPCell> originalHEAP = new List<HEAPCell>();
            CODE.bibNbr = 1;       //Reset the bib counter
            //Create a genuine STACKBindings.
            Dictionary<String, int> STACKClausesBindingS = new Dictionary<String, int>();
            foreach (KeyValuePair<String, Term> r in QueryClause.Registers)
            {
                String keyTag = Regex.Replace(r.Key, "}", "_" + CODE.bibNbr + "}");
                STACKClausesBindingS.Add(keyTag, -1);   //By convention, the value -1 means "no binding".
            }
            //Create a genuine STACKgoal, with only the first query (wich is a goal)
            LinkedList<STACKGoalitem> originalQuerySTACKgoal = new LinkedList<STACKGoalitem>();
            originalQuerySTACKgoal.AddFirst(new STACKGoalitem
            {
                STACKgoal = QueryClause.Body,
                STACKclause_bib = CODE.bibNbr,
            });

            try
            {
                QuerySolver = new Solver(
                    STACKgoals: originalQuerySTACKgoal,
                    STACKBindings: STACKClausesBindingS,
                    HEAP: originalHEAP,
                    query_Solver: null);        //Q_querySolver = null  is important. Detected as a Flag by the first Solver Object constructor.
            }
            catch (plixException ex)
            {
                _ = ThisAddIn.OUTPUT(ex.Message, Color.Red);
                END_Query();
            }

            //Manage Threading
            if (ASK_Query_Thread != null) ASK_Query_Thread.Abort();
            ASK_Query_Thread = new Thread(() => NEXT_QuerySolution(flagFirstQueryCall: true));
            ASK_Query_Thread.Start();
        }

        internal static void NEXT_QuerySolution(bool flagFirstQueryCall = false)
        {
            try
            {
                Solver Solver_result = QuerySolver.NEXT_QuerySolution();

                if (Solver_result != null)
                {
                    //Success !!! Display result to the user.
                    string message = "true";
                    if (QuerySolver.Q_TailCHOICEpoint != null)
                        message += ";";
                    else
                        message += ".";
                    foreach (KeyValuePair<String, Term> sr in QuerySolver.tentative_Goal.MotherClause.Registers)
                        if (sr.Value.isVar)
                        {
                            string keyTag = Regex.Replace(sr.Key, "}", "_" + QuerySolver.tentative_bib + "}");
                            if (sr.Value.UserRepresentation == "_") continue;
                            if (Solver_result.STACKBindings[keyTag] == -1) continue;
                            string instanciation = Solver_result.DisplayVarInstanciation(Solver_result.STACKBindings[keyTag], Solver_result.HEAP, VarAsUserInput: false, ListUserFriendly: true);
                            if (instanciation == "_") continue;
                            message += "\n" + sr.Value.UserRepresentation + "=" + instanciation;
                        }
                    ThisAddIn.OUTPUT(message, Color.DarkGreen);
                    Globals.ThisAddIn.Application.Cursor = Microsoft.Office.Interop.Excel.XlMousePointer.xlDefault;
                }
                else if (!QuerySolver.Q_GotAtLeastOneResult)
                {
                    QuerySolver.Q_GotAtLeastOneResult = true;   //Flag (not obvious) for single use.
                    END_Query("false.");
                }
                else
                    END_Query("end.");
            }
            catch (plixException ex)
            {
                _ = ThisAddIn.OUTPUT(ex.Message, Color.Red);
                END_Query();
            }
        }

        internal static void END_Query(String msg = "end.")
        {
            Globals.ThisAddIn.Application.Cursor = Microsoft.Office.Interop.Excel.XlMousePointer.xlDefault;
            _ = ThisAddIn.OUTPUT(msg, Color.Black);
            if (ASK_Query_Thread != null) ASK_Query_Thread.Abort();
        }

        internal static Boolean createConsole()
        {
            //Delete possible previous console.
            for (int i = Globals.ThisAddIn.CustomTaskPanes.Count; i > 0; i--)
                if (Globals.ThisAddIn.CustomTaskPanes[i - 1].Title == "plinxl_console")
                    Globals.ThisAddIn.CustomTaskPanes.RemoveAt(i - 1);

            //Create a new console.
            consoleArea = new System.Windows.Forms.UserControl();
            taskPaneValue = Globals.ThisAddIn.CustomTaskPanes.Add(consoleArea, "plinxl_console");
            taskPaneValue.DockPosition = Office.MsoCTPDockPosition.msoCTPDockPositionRight;
            taskPaneValue.VisibleChanged += new EventHandler(taskPaneValue_VisibleChanged);

            outputConsole = new System.Windows.Forms.RichTextBox()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                HideSelection = false,
                Font = new Font(FontFamily.GenericMonospace, System.Windows.Forms.RichTextBox.DefaultFont.Size),
            };
            consoleArea.Controls.Add(outputConsole);
            _ = cleanConsole();

            inputConsole = new System.Windows.Forms.TextBox()
            {
                Dock = DockStyle.Bottom,
                Font = new Font(FontFamily.GenericMonospace, System.Windows.Forms.RichTextBox.DefaultFont.Size),
                Text = "?- ",
            };
            consoleArea.Controls.Add(inputConsole);
            inputConsole.KeyDown += inputConsole_KeyDown;
            inputConsole.KeyPress += inputConsole_KeyPress;

            LinkedList<String> inputTexts = new LinkedList<String>();

            Globals.ThisAddIn.TaskPane.Visible = true;
            return true;
        }
        internal static Boolean cleanConsole()
        {
            //outputConsole.Text = "";
            outputConsole.Text = ("Plinxl comes with absolutely NO WARANTY." + "\r\n");
            outputConsole.Text += ("Go to  https://plinxl.com  for legal details, help, and more." + "\r\n");
            outputConsole.Text += ("-----" + Environment.NewLine);
            return true;
        }        
        private static void inputConsole_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle the txtBox key event.
            //Debug.WriteLine("inputConsole_KeyDown: " + e.KeyCode.ToString());

            if (e.KeyCode.ToString() == "Return" && Regex.IsMatch(inputConsole.Text, @"^[?][-]\s*$"))
                NEXT_QuerySolution();
            else if (e.KeyCode.ToString() == "Return")
                _ = userINPUT(inputConsole.Text);
            else if (e.KeyCode.ToString() == "Escape")
                END_Query();
            else if (e.KeyCode.ToString() == "Down")
            {
                inputConsole.Text = "?- ";
                inputConsole.Select(inputConsole.Text.Length, 0);
            }
            else if (e.KeyCode.ToString() == "Up")
            {
                inputConsole.Text = inputConsolePrevious;
                inputConsole.Select(inputConsole.Text.Length, 0);
            }
        }
        private static void inputConsole_KeyPress(Object sender, KeyPressEventArgs e)
        {
            bool isVirginInput = Regex.Match(inputConsole.Text, @"^[?][-]\s*$").Success;

            if (isVirginInput && e.KeyChar.ToString() == ";")
            {
                NEXT_QuerySolution();
                e.Handled = true;   // Stop the character from being entered into the control. Avoid the "ding"
            }
            else if (isVirginInput && e.KeyChar.ToString() == ".")
            {
                END_Query();
                e.Handled = true;   // Stop the character from being entered into the control. Avoid the "ding"
            }
        }

        private static void taskPaneValue_VisibleChanged(object sender, System.EventArgs e)
        { Globals.Ribbons.Ribbon1.consoleOnOff.Checked = taskPaneValue.Visible; }



        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Application.SheetBeforeDoubleClick += Application_SheetBeforeDoubleClick;
        }

        #endregion
    }
}