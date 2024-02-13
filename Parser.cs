using System;
using Excel = Microsoft.Office.Interop.Excel;
using System.Text.RegularExpressions;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using System.Drawing;
using Microsoft.Office.Interop.Excel;
using Microsoft.Office.Tools.Excel;

namespace plinxl
{

    internal class Parser
    {
        internal Excel.Worksheet wk;
        internal Excel.Range ClauseStartCell;
        internal Excel.Range ClauseLastCell;
        internal int wkRowsCount;
        internal int wkColumnsCount;
        internal Clause associatedClause;

        internal Parser(String txtClause = null)
        {
            this.associatedClause = new Clause();

            if (txtClause != null)
                this.associatedClause = new Clause(txtClause);
            else
                this.associatedClause = new Clause();
        }


        internal String parseCell(Excel.Range Pcell, Boolean insideBlockComment = false)
        {
            //Debug.WriteLine("parseCell: " + Pcell.Row + " " + Pcell.Column);

            //Detect very first call of thisParser.parseCell()
            if (wk == null)
            {
                wk = Pcell.Worksheet;
                ClauseStartCell = Pcell;
                ClauseLastCell = ClauseStartCell;
                Excel.Range endCell = wk.Cells.SpecialCells(Excel.XlCellType.xlCellTypeLastCell, Type.Missing);
                Excel.Range currentRange = wk.get_Range(ClauseStartCell, endCell).Cells;
                this.wkRowsCount = endCell.Row;
                this.wkColumnsCount = endCell.Column;
            }

            int lastColumnOfCurrentRow = wk.Cells[Pcell.Row, wkColumnsCount + 1].End[Excel.XlDirection.xlToLeft].Column;
            String PcellVal = Pcell.Text;
            ClauseLastCell = Pcell;

            Match matchRowComment = Regex.Match(PcellVal, @"^s*\%");
            Match matchOpenBlockComment = Regex.Match(PcellVal, @"^s*\/\*");
            Match matchCloseBlockComment = Regex.Match(PcellVal, @"^s*\*\/");

            if (Pcell.Column > lastColumnOfCurrentRow)      //Case after last column, go to row below
                return parseCell(wk.Cells[Pcell.Row + 1, 1], insideBlockComment);
            else if (Pcell.Row > wkRowsCount)    //End of the worksheet > terminate
                return "E";
                //return false;
            else if (matchOpenBlockComment.Success)
                return parseCell(wk.Cells[Pcell.Row, Pcell.Column + 1], insideBlockComment = true);
            else if (matchCloseBlockComment.Success)
                return parseCell(wk.Cells[Pcell.Row, Pcell.Column + 1], insideBlockComment = false);
            else if (insideBlockComment)
                return parseCell(wk.Cells[Pcell.Row, Pcell.Column + 1], insideBlockComment = true);
            else if (matchRowComment.Success)
                return parseCell(wk.Cells[Pcell.Row + 1, 1]);
            else if (String.IsNullOrWhiteSpace(PcellVal))
                return parseCell(wk.Cells[Pcell.Row, Pcell.Column + 1]);
            else
            {
                //Pcell seems to be a valid text.
                //If it's the first valid text, set here the ClauseStartCell
                if (String.IsNullOrWhiteSpace(this.associatedClause.GenuineUserTxtClause))
                    ClauseStartCell = Pcell;

                String r = parseString(" " + PcellVal + " ");
                if (r == "Q" || r == "F" || r == "R" || r == "D")
                    return r;
                else if (r == "?")
                    return parseCell(wk.Cells[Pcell.Row, Pcell.Column + 1]);
                else if (r == "E")
                    return r;
                else
                {
                    Debug.WriteLine("parseCell ERROR SHOULD NOT BE THERE");
                    return r;
                }
            }
        }


        internal String parseString(String StringClause)
        {
            String r = this.associatedClause.AddAndtokenizeObviousTerms(StringClause);

            if (r == "Q" || r == "F" || r == "R" || r == "D" || r == "?")
                return r;
            else
            {
                if (ClauseStartCell != null || ClauseLastCell != null)
                {
                    Excel.Range cell = wk.get_Range(ClauseStartCell, ClauseLastCell).EntireRow;
                    cell.Select();
                }
                _ = ThisAddIn.OUTPUT(r, Color.Red);
                return "E";
            }
        }
    }
}
