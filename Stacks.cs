using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plinxl
{

    public class plixException : Exception
    {
        public plixException() : base() { }
        public plixException(string message) : base(message) { }
        public plixException(string message, Exception inner) : base(message, inner) { }
    }



    internal class HEAPCell
    {
        public String Tag;
        public int StoreAdress;
        public override String ToString()
        { return ("< " + Tag + " | " + StoreAdress + " >"); }
    }

    internal class STACKGoalitem
    {
        internal Term STACKgoal;
        internal int STACKclause_bib;
        internal int EntryHEAPcell = -1;
    }

    internal static class CODE
    {
        internal static int bibNbr;
        internal static List<Clause> myClauses = new List<Clause>();
        //internal static List<Clause> builtinClausesXXX = new List<Clause>();

        internal static Dictionary<String, String> _fixOps_Dict;
        internal static String keywords;

        internal static Dictionary<String, String> _fixOpsUser_Dict;
        internal static String keywordsUser;


        internal static void reset()
        {
            myClauses = new List<Clause>();
            bibNbr = 10;
            _fixOpsUser_Dict = new Dictionary<String, String>();
            keywordsUser = "";

            //Called only once with ThisAddIn.startup()
            if (_fixOps_Dict == null)
            {
                _fixOps_Dict = new Dictionary<String, String>();
                //Default value : all the built-in fix operators
                _fixOps_Dict.Add(@"**", "5200xfx");
                _fixOps_Dict.Add(@"mod", "5400yfx");
                _fixOps_Dict.Add(@"//", "5400yfx");
                _fixOps_Dict.Add(@"*", "5400yfx");
                _fixOps_Dict.Add(@"/", "5400yfx");
                _fixOps_Dict.Add(@"+", "5500yfx");
                _fixOps_Dict.Add(@"-", "5500yfx");
                _fixOps_Dict.Add(@"\==", "5700xfx");
                _fixOps_Dict.Add(@"==", "5700xfx");
                _fixOps_Dict.Add(@"=:=", "5700xfx");
                _fixOps_Dict.Add(@"=\=", "5700xfx");
                _fixOps_Dict.Add(@"@>=", "5700xfx");
                _fixOps_Dict.Add(@"@=<", "5700xfx");
                _fixOps_Dict.Add(@"@>", "5700xfx");
                _fixOps_Dict.Add(@"@<", "5700xfx");
                _fixOps_Dict.Add(@">=", "5700xfx");
                _fixOps_Dict.Add(@"=<", "5700xfx");
                _fixOps_Dict.Add(@">", "5700xfx");
                _fixOps_Dict.Add(@"<", "5700xfx");
                _fixOps_Dict.Add(@"is", "5700xfx");
                _fixOps_Dict.Add(@"=", "5700xfx");                
                _fixOps_Dict.Add(@"=..", "5700xfx");
                //Missing :   \=       
                _fixOps_Dict.Add(@"not", "5900_fy");
                _fixOps_Dict.Add(@"\+", "5900_fy");
                _fixOps_Dict.Add(@",", "6000xfy");
                _fixOps_Dict.Add(@";", "6100xfy");
                _fixOps_Dict.Add(@"->", "6150xfy");
                _fixOps_Dict.Add(@":-", "6199xfx");
                _fixOps_Dict.Add(@"?-", "6200_fx");
                _fixOps_Dict.Add(@"::-", "6200_fx");    //???????

                //keywords = @"(\->|\:\-|\?\-|\*\*|mod|[/][/]|\*|[/]|\+|\-|\\==|==|=:=|=\\=|@>=|@=<|@>|@<|>=|=<|>|<|is|=|not|\\\+|[;]|[,]|::\-)";
                keywords = @"(\->|\:\-|\?\-|\*\*|mod|[/][/]|\*|[/]|\+|\-|\\==|==|=:=|=\\=|@>=|@=<|@>|@<|>=|=<|>|<|is|=|=..|not|\\\+|[;]|[,]|::\-)";
            }
        }

        internal static void asserta(Clause c)
        { myClauses.Insert(0, c); }
        internal static void assertz(Clause c)
        { myClauses.Add(c); }

        internal static List<Clause> GetClauses(String requestedLabel)
        {
            List<Clause> lc = new List<Clause>();
            foreach (Clause c in myClauses) 
                if (c.Head.PredicateName == requestedLabel) 
                    lc.Add(c);
            return lc;
        }
        internal static int GetClausesCount()
        { return myClauses.Count; }

        internal static int next_bibNbr
        { get { bibNbr += 1; return bibNbr; } }
    }



}
