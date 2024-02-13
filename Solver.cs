using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;



namespace plinxl
{

    internal class Solver
    {

        //Dedicated to the very first Q_querySolver object, wich is the original Query. These vars are usefull for all the child Solver objects. Equivalent to static vars. 
        internal Solver Q_querySolver;
        internal bool   Q_GotAtLeastOneResult = false;
        internal Solver Q_TailCHOICEpoint = null;        
        internal int    Q_Cutted_bib = -1; //by convention: -1 means there is no pending CUT! 

        //Stack copies at the creation of any Solver object, scope limited to this object.
        internal List<HEAPCell>             HEAP;
        private LinkedList<STACKGoalitem>   STACKgoals;
        internal Dictionary<string, int>    STACKBindings;

        //Variables for tentative solving
        internal Term   tentative_Goal;
        internal int    tentative_bib;
        private string  tentativePredicateName;
        private int     tentative_EntryHEAPcell;
        private int     next_bib = -1;
        private Solver  previous_TailCHOICEpoint = null;
        private System.Collections.IEnumerator tentative_enumeratorCHOICES = null;



        internal Solver(
            LinkedList<STACKGoalitem>   STACKgoals,
            Dictionary<string, int>     STACKBindings,
            List<HEAPCell>              HEAP,
            Solver                      query_Solver)
        {

            //Case very new Query entry point.
            if (query_Solver == null)
                query_Solver = this;

            //Create this.STACKgoals as a copy of STACKgoals. // TBC ???????? should avoid systematic creation of a copy. ??????
            this.STACKgoals = new LinkedList<STACKGoalitem>();
            foreach (STACKGoalitem i in STACKgoals)
            {
                this.STACKgoals.AddLast(new STACKGoalitem
                {
                    STACKgoal = i.STACKgoal,
                    STACKclause_bib = i.STACKclause_bib,
                    EntryHEAPcell = i.EntryHEAPcell,
                });
            }
                        
            this.STACKBindings = STACKBindings;
            this.HEAP = HEAP;
            this.Q_querySolver = query_Solver;
        }


        internal Solver NEXT_QuerySolution()
        {
            //This is the heart of backtracking.
            //We always keep track of the very last existing CHOICEPoint (called Q_TailCHOICEpoint).

            Solver Solver_result = null;
            Globals.ThisAddIn.Application.Cursor = Microsoft.Office.Interop.Excel.XlMousePointer.xlWait;

            if (this == Q_querySolver && !Q_GotAtLeastOneResult)
                //Very first call after this Query construction.
                Solver_result = this.solveGoal();

            while (true)
            {
                if (Solver_result != null && Solver_result == this.Q_querySolver.Q_TailCHOICEpoint)
                    //During the solving iterative process, we reached a CHOICEpoint. The iterative process backtrack until here.
                    //We have to launch a new iterative process starting from this new CHOICEpoint.
                    Solver_result = this.Q_querySolver.Q_TailCHOICEpoint.CHOICEpoint();
                else if (Solver_result == null && this.Q_querySolver.Q_TailCHOICEpoint != null)
                    //The solving iterative process reached a deadlock.
                    //We have to launch a new iterative process starting from the last existing CHOICEpoint
                    Solver_result = this.Q_querySolver.Q_TailCHOICEpoint.CHOICEpoint();
                else if (Solver_result != null && Solver_result.STACKgoals.Count == 0)
                {
                    //Success !
                    this.Q_GotAtLeastOneResult = true;
                    return Solver_result;
                }
                else
                    //We reached a deadlock, and there is no more CHOICEpoint to explore. End the process.
                    return null;
            }
        }


        private Solver solveGoal()
        {
            //We try to solve the last Goal on the STACKgoals.
            //Debug.WriteLine("solveGoal >");

            if (this.STACKgoals.Count == 0)     //Success, we solved all the goals. Return this successfull Solver.
                return this;

            //Pick up one goal and Clause on the STACK, and prepare.
            STACKGoalitem tentative_STACKgoalItem = this.STACKgoals.Last();
            this.STACKgoals.RemoveLast();       //Remaining STACKgoals, without the tentative_STACKgoalItem
            tentative_Goal = tentative_STACKgoalItem.STACKgoal;
            tentative_bib = tentative_STACKgoalItem.STACKclause_bib;
            tentative_EntryHEAPcell = tentative_STACKgoalItem.EntryHEAPcell;

            if (tentative_EntryHEAPcell == -1)
            {
                //WAM major step : put new goal on the HEAP.
                _ = WAM_putGoalUponTheHeap(tentative_Goal, ref this.HEAP);
                string k = tentative_Goal.FlattenedTerms.Last();
                k = Regex.Replace(k, "}", "_" + tentative_bib + "}");
                tentative_EntryHEAPcell = this.STACKBindings[k];
            }

            //The HEAP and tentative_EntryHEAPcell are ready. Create tentativePredicateName, from the existing HEAP entry.
            int entryHEAPcellTarget = WAM_deref_target(tentative_EntryHEAPcell, HEAP);
            String functor = HEAP[entryHEAPcellTarget].Tag;
            int arity = HEAP[entryHEAPcellTarget].StoreAdress;
            tentativePredicateName = functor + "/" + arity;


            //Debug.WriteLine("solveGoal PredName: " + tentativePredicateName + "  " + "_" + tentative_bib);
            //PrintHEAP(HEAP);
            //Debug.WriteLine("tentative_EntryHEAPcell >" + tentative_EntryHEAPcell);
            //Debug.WriteLine("entryHEAPcellTarget          >" + entryHEAPcellTarget);
            //Debug.WriteLine("HEAP lenght: " + HEAP.Count);
            //Debug.WriteLine("STACKgoals.Count:   " + this.STACKgoals.Count);
            //foreach (KeyValuePair<String, int> b in this.STACKBindings)
            //    Debug.WriteLine(b.KeyIndex + " " + b.Value);

            List<Clause> TempProgClauses = new List<Clause>();
            IEnumerable<Solver> iteratorCHOICES = null;
            //XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
            //XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
            #region   //Identify and process built-in predicates

            //PROLOG programmed clauses
            Clause builtinClause;
            if (tentativePredicateName == "not/1")
            {
                builtinClause = new Clause("not(X) :- call(X),!,fail."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause("not(X)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"\+/1")
            {
                builtinClause = new Clause(@"\+(X) :- call(X),!,fail."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"\+(X)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"once/1")
            {
                builtinClause = new Clause(@"once(G) :- G,!."); TempProgClauses.Add(builtinClause);
                //builtinClause = new Clause(@"once(G) :- call(G), ! ."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"member/2")
            {
                builtinClause = new Clause(@"member(X,[X|_])."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"member(X,[_|Rest]) :- member(X,Rest)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"append/3")
            {
                builtinClause = new Clause(@"append([],T,T)."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"append([X|L1],L2,[X|L3]) :- append(L1,L2,L3)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"sort/2")
            {
                builtinClause = new Clause(@"sort([],[])."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"sort( [X1|Xs], Y) :- sort(Xs,Ys), insert(X1,Ys,Y)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"insert/3")
            {
                builtinClause = new Clause(@"insert(X,[],[X])."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"insert(X,[Y1|Ys],[X,Y1|Ys]) :- X @=< Y1."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"insert(X,[Y1|Ys],[Y1|Zs]) :- X @> Y1, insert(X,Ys,Zs)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"reverse/2")
            {
                builtinClause = new Clause(@"reverse(Xs,Ys) :- reverse(Xs,[],Ys,Ys)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"reverse/4")
            {
                builtinClause = new Clause(@"reverse([],Ys,Ys,[])."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"reverse([X|Xs],Rs,Ys,[_|Bound]) :- reverse(Xs,[X|Rs],Ys,Bound)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"select/3")
            {
                builtinClause = new Clause(@"select(E,[E|Xs],Xs)."); TempProgClauses.Add(builtinClause);
                builtinClause = new Clause(@"select(E,[X|Xs],[X|Ys]) :- select(E,Xs,Ys)."); TempProgClauses.Add(builtinClause);
            }
            else if (tentativePredicateName == @"->/2")
            {
                builtinClause = new Clause("If -> Then ; _Else :- If, !, Then."); TempProgClauses.Add(builtinClause);   // IF succeeded, eliminate ELSE, >THEN  
                builtinClause = new Clause("If -> _Then ; Else :- !, Else."); TempProgClauses.Add(builtinClause);       // IF failed, eliminate THEN, >Else.
                builtinClause = new Clause("If -> Then :- If, !, Then."); TempProgClauses.Add(builtinClause);           // No Else.   IF succeeded, >Then.
            }

            //Process done localy, that create a new Clause, added on the STACKgoals
            else if (tentativePredicateName == "is/2")
            {
                int BindRegName2 = entryHEAPcellTarget + 2;
                String calcResult = EvaluateMath(BindRegName2, HEAP);

                Clause TemporaryClause = new Clause(" is( " + calcResult + " , _ ).");
                //Clause TemporaryClause = new Clause(calcResult + " is _ .");
                TempProgClauses.Add(TemporaryClause);
            }
            else if (tentativePredicateName == "string_chars/2")
            {
                //Should be something like:  string_chars( String, ListOfChars )   bi-directional                
                //Debug.WriteLine("string_chars/2 ");
                //PrintHEAP(HEAP);                               
                int pointerArg1 = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int targetArg1 = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                int pointerArg2 = WAM_deref(entryHEAPcellTarget + 2, HEAP);
                int targetArg2 = WAM_deref_target(entryHEAPcellTarget + 2, HEAP);

                if (HEAP[targetArg2].Tag == "." && HEAP[targetArg2].StoreAdress == 2)
                {   //Arg2 is a List of chars.
                    String CharsString = DisplayVarInstanciation(pointerArg2, HEAP, false, true);
                    CharsString = CharsString.Remove(CharsString.Length - 1, 1);
                    CharsString = CharsString.Remove(0, 1);
                    //CharsString = Regex.Replace(CharsString, @"[,]\s", "");
                    CharsString = Regex.Replace(CharsString, @"[,]", "");
                    CharsString = Regex.Replace(CharsString, @"[']", "");
                    if (char.IsUpper(CharsString[0]))
                        CharsString = "'" + CharsString + "'";
                    Clause TemporaryClause = new Clause(" string_chars( " + CharsString + ", _).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else if (HEAP[pointerArg1].Tag == "STR" && HEAP[targetArg1].StoreAdress == 0)
                {   //Arg1 is possibly a valid string, to be split into chars
                    String providedString = HEAP[targetArg1].Tag;

                    providedString = Regex.Replace(providedString, @"['](.*)[']", "$1");

                    List<char> Chars = providedString.ToList();
                    String CharsList = "[";
                    foreach (char c in Chars)
                    {
                        if (!Char.IsLower(c))
                            CharsList += "'" + c + "', ";
                        else
                            CharsList += c + ", ";
                    }
                    CharsList = CharsList.Remove(CharsList.Length - 2, 2) + "]";
                    Clause TemporaryClause = new Clause(" string_chars( _, " + CharsList + " ).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else
                    return null;
            }
            else if (tentativePredicateName == "=../2")
            {
                //Should be something like:  =..( Term , [ functor, a_1, ..., a_i ] ).
                int pointerArg1 = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int targetArg1 = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                int pointerArg2 = WAM_deref(entryHEAPcellTarget + 2, HEAP);
                int targetArg2 = WAM_deref_target(entryHEAPcellTarget + 2, HEAP);

                if (HEAP[pointerArg1].Tag == "STR")
                {
                    //Case the Term is provided. We have to create/instantiate the list [functor, args ...].
                    //Debug.WriteLine("functor1 >" + HEAP[targetArg1].Tag + " STRarity>" + HEAP[targetArg1].StoreAdress);
                    int STRarity = HEAP[targetArg1].StoreAdress;

                    String myTempClauseText = "=..( " + HEAP[targetArg1].Tag + "( ";
                    for (int i = 0; i < STRarity; i++)
                        myTempClauseText += " A" + i + " ,";
                    myTempClauseText = myTempClauseText.Remove((myTempClauseText.Length - 1), 1) + ")";

                    myTempClauseText += " , [ " + HEAP[targetArg1].Tag + " ,";
                    for (int i = 0; i < STRarity; i++)
                        myTempClauseText += " A" + i + " ,";
                    myTempClauseText = myTempClauseText.Remove((myTempClauseText.Length - 1), 1) + " ] ).";

                    //Debug.WriteLine("myTempClauseText >" + myTempClauseText);
                    TempProgClauses.Add(new Clause(myTempClauseText));
                }
                else if (HEAP[pointerArg1].Tag == "REF" && HEAP[pointerArg1].StoreAdress == pointerArg1 &&
                         HEAP[targetArg2].Tag == "." && HEAP[targetArg2].StoreAdress == 2)
                {
                    //Case the Term is NOT provided, it's a var. We have to create/instantiate the Term func(args ...) from the list items.
                    //Identify the functor
                    int ListFirstItemTarget = WAM_deref_target(targetArg2 + 1, HEAP);
                    String func = HEAP[ListFirstItemTarget].Tag;

                    //Count the list items, get the arity
                    String L = DisplayVarInstanciation(pointerArg2, HEAP, false, true);
                    L = L.Remove(L.Length - 1, 1);
                    L = L.Remove(0, 1);
                    List<String> lst = L.Split(',').ToList();
                    int STRarity = lst.Count() - 1;

                    String myTempClauseText = "=..( " + func + "( ";
                    for (int i = 0; i < STRarity; i++)
                        myTempClauseText += " A" + i + " ,";
                    myTempClauseText = myTempClauseText.Remove((myTempClauseText.Length - 1), 1) + " )";

                    myTempClauseText += ", [ _ ";
                    if (STRarity > 0)
                        myTempClauseText += ", ";

                    for (int i = 0; i < STRarity; i++)
                        myTempClauseText += " A" + i + " ,";
                    myTempClauseText = myTempClauseText.Remove((myTempClauseText.Length - 1), 1) + " ] ).";

                    //Debug.WriteLine("myTempClauseText >" + myTempClauseText);
                    TempProgClauses.Add(new Clause(myTempClauseText));
                }
                else
                    return null;
            }
            else if (tentativePredicateName == "arg/3")
            {
                //Should be something like:  arg( Arg_Index , term(a_1, ..., a_i, ...), Arg_Value ).
                int pointerArg1 = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int targetArg1 = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                int pointerArg2 = WAM_deref(entryHEAPcellTarget + 2, HEAP);
                int targetArg2 = WAM_deref_target(entryHEAPcellTarget + 2, HEAP);

                if (HEAP[pointerArg2].Tag != "STR")
                    throw new plixException("arg/3 Term has to be instanciated.  #732");

                string STRfunctor = HEAP[targetArg2].Tag;
                int STRarity = HEAP[targetArg2].StoreAdress;
                bool Arg1IsIndex = int.TryParse(HEAP[targetArg1].Tag, out int Arg_Index);
                bool Arg1IsVar = false;
                if (HEAP[targetArg1].Tag == "REF" && HEAP[targetArg1].StoreAdress == targetArg1)
                    Arg1IsVar = true;

                if (Arg1IsIndex && Arg_Index < 0)
                    throw new plixException("arg/3 index cannot be less than 0.  #731");
                else if (Arg1IsIndex && Arg_Index == 0)
                    return null;
                else if (Arg1IsIndex && Arg_Index > STRarity)
                    return null;

                else if(Arg1IsIndex)
                {   //Case : the Arg_Index is defined. Create a TemporaryClause relevant to this Arg_Index
                    String TemporaryTerm = STRfunctor + "(";
                    for (int i = 0; i < STRarity; i++)
                    {
                        if (i == Arg_Index - 1) TemporaryTerm += " X ,";
                        else TemporaryTerm += "_ ,";
                    }
                    TemporaryTerm = TemporaryTerm.Remove((TemporaryTerm.Length - 1), 1) + ")";
                    Clause TemporaryClause = new Clause("arg( _ , " + TemporaryTerm + " , X ).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else if(Arg1IsVar)
                {   //Case : the Arg_Index is not defined. Create TemporaryClauses for each possible Arg_Index
                    for (int i = 0; i < STRarity; i++)
                    {
                        String TemporaryTerm = STRfunctor + "(";
                        for (int ii = 0; ii < STRarity; ii++)
                        {
                            if (ii == i) TemporaryTerm += " X ,";
                            else TemporaryTerm += "_ ,";
                        }
                        TemporaryTerm = TemporaryTerm.Remove((TemporaryTerm.Length - 1), 1) + ")";
                        Clause TemporaryClause = new Clause("arg( " + i + ", " + TemporaryTerm + " , X ).");
                        TempProgClauses.Add(TemporaryClause);
                    }
                }

            }
            else if (tentativePredicateName == "functor/3")
            {
                //Should be something like: functor(term, functor, STRarity).  
                int pointerArg1 = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int targetArg1 = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                int targetArg2 = WAM_deref_target(entryHEAPcellTarget + 2, HEAP);
                int targetArg3 = WAM_deref_target(entryHEAPcellTarget + 3, HEAP);

                bool Arg3IsArity = int.TryParse(HEAP[targetArg3].Tag, out int Arity);
                Clause TemporaryClause = null;

                if (HEAP[pointerArg1].Tag == "STR")
                {
                    //Debug.WriteLine("functor1 >" + HEAP[targetArg1].Tag + " STRarity>" + HEAP[targetArg1].StoreAdress);
                    TemporaryClause = new Clause("functor( _, " + HEAP[targetArg1].Tag + ", " + HEAP[targetArg1].StoreAdress + " ).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else if (HEAP[targetArg1].Tag == "REF" && HEAP[targetArg1].StoreAdress == entryHEAPcellTarget + 1 && Arg3IsArity)    //TBC :  the second is an atom, and the third is a positive integer
                {
                    String clauseT = HEAP[targetArg2].Tag + "(  ";
                    for (int i = 1; i <= Arity; i += 1)
                        //clauseT = clauseT + "__" + i + tentativeTag + ", ";
                        clauseT = clauseT + "__" + i + "_" + tentative_bib + ", ";
                    clauseT = clauseT.Remove(clauseT.Length - 2, 2);
                    clauseT += " )";
                    TemporaryClause = new Clause("functor( " + clauseT + ", _, _).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else return null;
            }
            else if (tentativePredicateName == "read/1")
            {
                tentative_EntryHEAPcell = entryHEAPcellTarget + 1;
                // read the user input:
                String inputString = Microsoft.VisualBasic.Interaction.InputBox("term to be read:", Title:"plinxl", DefaultResponse:"");
                if (inputString == "")   
                    return null;       //Apply if user clicked the "Cancel" button

                Clause inputTerm;
                try
                { inputTerm = new Clause(inputString); }
                catch
                { return null; }
                TempProgClauses.Add(inputTerm);
            }

            //Process done localy, then call directly the following child clause with the remaining STACKgoals 
            else if (tentativePredicateName == "true/0")
            { }
            else if (tentativePredicateName == "fail/0")
            { return null; }
            else if (functor == ",")
            {
                for (int a = arity; a >= 1; a--)
                {
                    int BindRegName = entryHEAPcellTarget + a;
                    this.STACKgoals.AddLast(new STACKGoalitem
                    {
                        //STACKgoal = tentative_Goal,
                        STACKgoal = null,
                        STACKclause_bib = tentative_bib,
                        EntryHEAPcell = BindRegName,
                    });
                }
            }
            else if (tentativePredicateName == "call/1")
            {
                //Have to solve the first Arg.
                int BindRegName1 = entryHEAPcellTarget + 1;
                this.STACKgoals.AddLast(new STACKGoalitem
                {
                    //STACKgoal = tentative_Goal,
                    STACKgoal = null,
                    STACKclause_bib = tentative_bib,
                    EntryHEAPcell = BindRegName1,
                });
            }
            else if (tentativePredicateName == "/1")    //it's parenthesys such as   (5 * (5+4))  
            {
                //Debug.WriteLine("tentativePredicateName " + tentativePredicateName);
                //Have to solve the first Arg.
                int BindRegName1 = entryHEAPcellTarget + 1;
                this.STACKgoals.AddLast(new STACKGoalitem
                {
                    //STACKgoal = tentative_Goal,
                    STACKgoal = null,
                    STACKclause_bib = tentative_bib,
                    EntryHEAPcell = BindRegName1,
                });
            }
            else if (Regex.IsMatch(tentativePredicateName, @"^[@](>=|=<|>|<)\/2"))
            {
                Match kw = Regex.Match(tentativePredicateName, @"^[@](?<comparator>(>=|=<|>|<))\/2");
                int BindRegNameSup = -1;
                int BindRegNameInf = -1;
                if (kw.Groups["comparator"].Value == ">=" || kw.Groups["comparator"].Value == ">")
                {
                    BindRegNameSup = entryHEAPcellTarget + 1;
                    BindRegNameInf = entryHEAPcellTarget + 2;
                }
                else if (kw.Groups["comparator"].Value == "=<" || kw.Groups["comparator"].Value == "<")
                {
                    BindRegNameSup = entryHEAPcellTarget + 2;
                    BindRegNameInf = entryHEAPcellTarget + 1;
                }
                int r = OrderSuperior(BindRegNameSup, BindRegNameInf, HEAP);
                //Proceed case of equality
                if (r == 0 && (kw.Groups["comparator"].Value == ">=" || kw.Groups["comparator"].Value == "=<"))
                    r = 1;
                if (r != 1) return null;
            }
            else if (Regex.IsMatch(tentativePredicateName, @"^(>|<|>=|=<|=:=|=\\=)\/2"))
            {                
                //String calcResult = EvaluateMath(tentative_EntryHEAPcell, HEAP);
                string calcResult = EvaluateMath(entryHEAPcellTarget, HEAP);
                if (calcResult != "1") return null;
            }
            else if (tentativePredicateName == "=/2")
            {
                int BindArg1 = entryHEAPcellTarget + 1;
                int BindArg2 = entryHEAPcellTarget + 2;
                Boolean b = WAM_unify(BindArg1, BindArg2, HEAP);
                if (!b) return null;
            }
            else if (tentativePredicateName == "assertz/1" || tentativePredicateName == "assert/1" || tentativePredicateName == "asserta/1")
            {
                int a = entryHEAPcellTarget + 1;
                String termString = DisplayVarInstanciation(a, HEAP, VarAsUserInput: true, ListUserFriendly: true) + ".";
                Clause newClause = new Clause(termString);
                if (tentativePredicateName == "assertz/1" || tentativePredicateName == "assert/1")
                    CODE.assertz(newClause);
                else if (tentativePredicateName == "asserta/1")
                    CODE.asserta(newClause);
            }
            else if (tentativePredicateName == "write/1")
            {
                int BindArg1 = entryHEAPcellTarget + 1;
                string d = DisplayVarInstanciation(BindArg1, HEAP, VarAsUserInput: false, ListUserFriendly: true);
                //Remove 'quotes'
                d = Regex.Replace(d, @"^['](.*)[']", "$1");                
                ThisAddIn.OUTPUT(d, Color.Black, newLine: false);
                Debug.Print(d);
            }
            else if (tentativePredicateName == "writeln/1")
            {
                int BindArg1 = entryHEAPcellTarget + 1;
                string d = DisplayVarInstanciation(BindArg1, HEAP, VarAsUserInput: false, ListUserFriendly: true);
                //Remove 'quotes'
                d = Regex.Replace(d, @"^['](.*)[']", "$1");
                ThisAddIn.OUTPUT(d, Color.Black, newLine: true);
                Debug.Print(d);
            }
            else if (Regex.IsMatch(tentativePredicateName, @"nl"))
            {
                ThisAddIn.OUTPUT("", Color.Black, newLine: true);
            }
            else if (tentativePredicateName == "consult/0")
            {
                ThisAddIn.consult();
            }
            else if (tentativePredicateName == "trace/0")
            {
                _ = Globals.Ribbons.Ribbon1.tracer.Checked = true;
            }
            else if (tentativePredicateName == "notrace/0")
            {
                _ = Globals.Ribbons.Ribbon1.tracer.Checked = false;
            }
            else if (tentativePredicateName == "var/1")
            {
                int BindRegName = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int a = HEAP[BindRegName].StoreAdress;
                if (HEAP[a].Tag == "REF" && HEAP[a].StoreAdress == a)
                { }
                else
                    return null;
            }
            else if (tentativePredicateName == "nonvar/1")
            {
                int BindRegName = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int a = HEAP[BindRegName].StoreAdress;
                if (HEAP[a].Tag == "REF" && HEAP[a].StoreAdress == a)
                    return null;
            }
            else if (tentativePredicateName == "number/1")
            {
                int BindRegName = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int a = HEAP[BindRegName].StoreAdress;
                String tentativeNbr = HEAP[a].Tag;
                if (double.TryParse(tentativeNbr, out double Arg0))
                { }
                else
                    return null;
            }
            else if (tentativePredicateName == "atomic/1")
            {
                int BindRegName = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int a = HEAP[BindRegName].StoreAdress;
                if (HEAP[a].StoreAdress != 0)
                    return null;
            }
            else if (tentativePredicateName == "compound/1")
            {
                int BindRegName = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int EndReg = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                if (HEAP[BindRegName].Tag == "STR" && HEAP[EndReg].StoreAdress > 0)
                { }
                else
                    return null;
            }
            else if (tentativePredicateName == @"==/2" || tentativePredicateName == @"\==/2")
            {
                int BindArg1 = entryHEAPcellTarget + 1;
                int BindArg2 = entryHEAPcellTarget + 2;
                Boolean b = literalEquality(BindArg1, BindArg2, HEAP);
                if ((b && tentativePredicateName == @"==/2") || (!b && tentativePredicateName == @"\==/2"))
                { }
                else return null;
            }
            else if (tentativePredicateName == @"length/2")
            {
                //Should be something like: length( [ list ], length ).  
                int pointerArg1 = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int targetArg1 = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                int pointerArg2 = WAM_deref(entryHEAPcellTarget + 2, HEAP);
                int targetArg2 = WAM_deref_target(entryHEAPcellTarget + 2, HEAP);

                //Try first obvious cases, for fast solution.
                if (HEAP[targetArg1].Tag == "." && HEAP[targetArg1].StoreAdress == 2)
                {   //Arg2 is a List.
                    String L = DisplayVarInstanciation(pointerArg1, HEAP, false, true);
                    L = L.Remove(L.Length - 1, 1);
                    L = L.Remove(0, 1);
                    List<String> lst = L.Split(',').ToList();
                    int Length = lst.Count();
                    Clause TemporaryClause = new Clause("length( _, " + Length + " ).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else if (HEAP[targetArg1].Tag == "[]")
                {   //Arg2 is a empty List.
                    Clause TemporaryClause = new Clause("length( _, 0).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else if (HEAP[pointerArg2].Tag == "STR" && HEAP[targetArg2].StoreAdress == 0)
                {   //Arg2 is possibly a valid integer 
                    string LengthString = EvaluateMath(pointerArg2, HEAP);
                    if (!int.TryParse(LengthString, out int length))
                        throw new plixException(LengthString + " is not a valid number.  #778");
                    if (length < 0)
                        throw new plixException(LengthString + " is not a valid number.  #779");
                    String Lst = " ";
                    for (int i = 1; i <= length; i++)
                        Lst += "_,";
                    Lst = "[" + Lst.Remove(Lst.Length - 1, 1) + "]";
                    Clause TemporaryClause = new Clause("length( " + Lst + ", LengthString).");
                    TempProgClauses.Add(TemporaryClause);
                }
                else
                {   //Not an obvious case. Try to solve with prolog style resolution.
                    builtinClause = new Clause(@"length([],0)."); TempProgClauses.Add(builtinClause);
                    builtinClause = new Clause(@"length([_|Tail],N) :- length(Tail,N1), N is 1 + N1."); TempProgClauses.Add(builtinClause);
                }
            }

            // Predicates with CHOICEpoint
            else if (tentativePredicateName == ";/2")
            {
                int BindRegName1 = entryHEAPcellTarget + 1;
                int BindRegName2 = entryHEAPcellTarget + 2;
                //Create a list of the 2 possible OR goals.
                List<STACKGoalitem> OrGoals = new List<STACKGoalitem>();
                OrGoals.Add(new STACKGoalitem
                {
                    //STACKgoal = tentative_Goal,
                    STACKgoal = null,
                    STACKclause_bib = tentative_bib,
                    EntryHEAPcell = BindRegName1,
                });
                OrGoals.Add(new STACKGoalitem
                {
                    //STACKgoal = tentative_Goal,
                    STACKgoal = null,
                    STACKclause_bib = tentative_bib,
                    EntryHEAPcell = BindRegName2,
                });
                iteratorCHOICES = iterateCHOICES_Or(OrGoals, true);
            }
            else if (tentativePredicateName == "!/0")
            {
                iteratorCHOICES = iterateCHOICES_CUT(null, true);
            }
            else if (tentativePredicateName == "retract/1")
            {
                //Should be something like: functor(term T).  
                int HeapArg1 = WAM_deref(entryHEAPcellTarget + 1, HEAP);
                int HeapTargetArg1 = WAM_deref_target(entryHEAPcellTarget + 1, HEAP);
                String predToBeRetracted = HEAP[HeapTargetArg1].Tag + "/" + HEAP[HeapTargetArg1].StoreAdress;
                TempProgClauses = CODE.GetClauses(predToBeRetracted);
                iteratorCHOICES = iterateCHOICES_Retract(TempProgClauses, tentativeQueryEntryBindToHEAPindex: HeapArg1);
            }


            else if (tentativePredicateName == "findall/3")
            {
                //Should be something like  bagof( Var, Goal, [ Var1, Var2, ... ] ).

                int HeapArg1 = entryHEAPcellTarget + 1;
                int HeapArg2 = entryHEAPcellTarget + 2;
                string ResultList = "[";

                //Gather the possible goals to be adressed
                int goalHEAPtarget = WAM_deref_target(HeapArg2, HEAP);
                string goalPredicate = HEAP[goalHEAPtarget].Tag + "/" + HEAP[goalHEAPtarget].StoreAdress;

                LinkedList<STACKGoalitem> goalSTACKgoal = new LinkedList<STACKGoalitem>();
                //Have to solve the Second Arg only.                
                goalSTACKgoal.AddLast(new STACKGoalitem
                {
                    //STACKgoal = tentative_Goal,
                    STACKgoal = null,
                    STACKclause_bib = tentative_bib,
                    EntryHEAPcell = HeapArg2,
                });

                Solver GoalSolver = new Solver(
                    STACKgoals: goalSTACKgoal,
                    STACKBindings: this.STACKBindings,
                    HEAP: this.HEAP,
                    query_Solver: null);        //Q_querySolver = null  is important. Detected as a Flag by the first Solver Object constructor.

                Solver r = GoalSolver.NEXT_QuerySolution();
                while (r != null)
                {
                    string instanciation = r.DisplayVarInstanciation(HeapArg1, r.HEAP, VarAsUserInput: false, ListUserFriendly: true);
                    ResultList += instanciation + ", ";
                    r = GoalSolver.NEXT_QuerySolution();
                }

                ResultList += "]";
                ResultList = Regex.Replace(ResultList, @",\s]$", "]");

                Debug.Print("findall/3 ResultList: " + ResultList);

                builtinClause = new Clause(@"findall(_,_, " + ResultList + @" )."); TempProgClauses.Add(builtinClause);
            }


            //################################################
            //################################################
            //################################################
            //################################################

            //else if (tentativePredicateName == "findall/3" || tentativePredicateName == "collecttttttt/1")
            //{
            //    builtinClause = new Clause("findall(X,Goal,Xlist) :- call(Goal), assertz(queue(X)), fail ; assertz( queue(bottom) ), collecttttttt( Xlist)."); TempProgClauses.Add(builtinClause);
            //    builtinClause = new Clause("collecttttttt(T) :- retract(queue(X)), !, ( X == bottom, !, T = [] ; T = [X|Rest], collecttttttt(Rest) )."); TempProgClauses.Add(builtinClause);
            //}

            //else if (tentativePredicateName == "bagofXXXXXXXXXXXXXXXXXXXXX/3")
            //{
            //    //Should be something like  bagof( Var, Goal, [ Var1SatisfyingGoal, Var2SatisfyingGoal, ... ] ).

            //    int HeapArg1 = entryHEAPcellTarget + 1;
            //    int HeapArg2 = entryHEAPcellTarget + 2;
            //    LinkedList<STACKGoalitem> emptyRemaining_STACKgoals = new LinkedList<STACKGoalitem>();
            //    String ResultList = "[";

            //    //Gather the possible goals to be adressed
            //    int goalHEAPtarget = WAM_deref_target(HeapArg2, HEAP);
            //    string goalPredicate = HEAP[goalHEAPtarget].Tag + "/" + HEAP[goalHEAPtarget].StoreAdress;
            //    List<Clause> goals = CODE.GetClauses(goalPredicate);

            //    foreach (Clause goal in goals)
            //    {
            //        Solver r = applySingleClause(goal, HeapArg2, emptyRemaining_STACKgoals);
            //        if (r != null)
            //        {
            //            string instanciation = r.DisplayVarInstanciation(HeapArg1, r.HEAP, VarAsUserInput: false, ListUserFriendly: true);
            //            ResultList += instanciation + ", ";
            //        }
            //    }
            //    ResultList = ResultList.Remove(ResultList.Length - 2, 2) + "]";
            //    //Debug.Print("bagof/3 ResultList: " + ResultList);

            //    builtinClause = new Clause(@"bagof(_,_, " + ResultList + @" )."); TempProgClauses.Add(builtinClause);
            //}

            //else if (tentativePredicateName == "enumerateOnceXXXXXXXXXXXXXXXX/2")
            //{
            //    //Gather the possible goals to be adressed
            //    int HeapArg1 = entryHEAPcellTarget + 1;
            //    int HeapArg2 = entryHEAPcellTarget + 2;

            //    int goalHEAPtarget = WAM_deref_target(HeapArg2, HEAP);
            //    string goalPredicate = HEAP[goalHEAPtarget].Tag + "/" + HEAP[goalHEAPtarget].StoreAdress;
            //    TempProgClauses = CODE.GetClauses(goalPredicate);

            //    iteratorCHOICES = iterateCHOICES_EnumerateOnce(TempProgClauses, tentative_EntryHEAPcell);
            //}


            //################################################
            //################################################
            //################################################
            //################################################

            #endregion


            else
            {
                //Search for matching clause(s) in the user program.
                TempProgClauses = CODE.GetClauses(tentativePredicateName);
                if (TempProgClauses.Count == 0 && CODE.myClauses.Count > 0)
                    throw new plixException("Unknown procedure: " + tentativePredicateName + "    #934");
                else if (TempProgClauses.Count == 0 && CODE.myClauses.Count == 0)
                    throw new plixException("Unknown procedure: " + tentativePredicateName + Environment.NewLine 
                                          + "Consider '?- consult.'.    #935");
            }

            if (TempProgClauses.Count > 1 && iteratorCHOICES == null)
                iteratorCHOICES = iterateCHOICES_Clauses(TempProgClauses, tentative_EntryHEAPcell);

            //Trace
            //String tracemsg1 = DisplayVarInstanciation(tentative_EntryHEAPcell, HEAP, VarAsUserInput: false, ListUserFriendly: true);
            //_ = ThisAddIn.outputTrace(port: "CALL", tracemsg1, tentativeTag);

            //Launch next step:
            if (TempProgClauses.Count == 1 && iteratorCHOICES == null)
                return applySingleClause(TempProgClauses.First(), tentative_EntryHEAPcell, this.STACKgoals);
            else if (TempProgClauses.Count == 0 && iteratorCHOICES == null)
            {
                Solver NewSolver = new Solver(this.STACKgoals, STACKBindings, HEAP, this.Q_querySolver);
                return NewSolver.solveGoal();
            }
            else if (iteratorCHOICES != null)
            {
                this.previous_TailCHOICEpoint = this.Q_querySolver.Q_TailCHOICEpoint;
                this.Q_querySolver.Q_TailCHOICEpoint = this;
                tentative_enumeratorCHOICES = iteratorCHOICES.GetEnumerator();
                return this;
            }
            else
                return null;
        }

        private Solver CHOICEpoint()
        {
            //This Solver is a CHOICEpoint. Has to iterate through possible solutions.

            //Trace
            if (Globals.Ribbons.Ribbon1.tracer.Checked)
            {
                String tracemsg1 = DisplayVarInstanciation(tentative_EntryHEAPcell, HEAP, VarAsUserInput: false, ListUserFriendly: true);
                _ = Tracer.outputTrace(port: "CALL", tracemsg1, tentative_bib);
            }

            //Process possible CUT!
            if (this.Q_querySolver.Q_Cutted_bib != -1)
            {
                //We are under a CUT!. Do not consider this CHOICEpoint, recall previous_TailCHOICEpoint
                this.Q_querySolver.Q_TailCHOICEpoint = this.previous_TailCHOICEpoint;
                if (this.Q_querySolver.Q_Cutted_bib == next_bib)
                    //This CUT! is completely finished. Delete CUT! flag.
                    this.Q_querySolver.Q_Cutted_bib = -1;
                return null;
            }

            //Search solution by iteration of CHOICES (TempProgClauses or ORGoals or others iterable solutions)
            bool existCHOICEiteration = tentative_enumeratorCHOICES.MoveNext();

            if (!existCHOICEiteration)
            {
                //Debug.WriteLine("CHOICEpoint nomore CHOICE: " + tentativePredicateName + "_" + tentative_bib + "");
                //No more CHOICE. Go back to the previous CHOICEpoint
                this.Q_querySolver.Q_TailCHOICEpoint = this.previous_TailCHOICEpoint;
                this.previous_TailCHOICEpoint = null;
                //Trace
                if (Globals.Ribbons.Ribbon1.tracer.Checked)
                {
                    String tracemsg3 = DisplayVarInstanciation(tentative_EntryHEAPcell, HEAP, VarAsUserInput: false, ListUserFriendly: true);
                    _ = Tracer.outputTrace(port: "FAIL ", tracemsg3, tentative_bib);
                }
                return null;
            }

            Solver r2 = (Solver)tentative_enumeratorCHOICES.Current;
            //Debug.WriteLine("CHOICEpoint OK return: " + tentativePredicateName + "  " + "_" + tentative_bib + "");
            return r2;
        }


        //private IEnumerable<Solver> iterateCHOICES_EnumerateOnce(List<Clause> ProgClauses, int tentativeQueryEntryBindToHEAPindex)
        //{
        //    foreach (Clause ProgClause in ProgClauses)
        //    {
        //        Debug.WriteLine("iterateCHOICES_EnumerateOnce: " + ProgClause.GenuineUserTxtClause);

        //        Solver r2 = applySingleClause(ProgClause, tentativeQueryEntryBindToHEAPindex, this.STACKgoals);
        //        if (r2 != null)
        //            yield return r2;
        //        //else keep trying with next ProgClause
        //    }
        //}


        private IEnumerable<Solver> iterateCHOICES_Or(List<STACKGoalitem> OrGoals, bool requireFindall)
        {
            foreach (STACKGoalitem orGoal in OrGoals)
            {
                //Add 1 rule goals on the STACKgoals.
                this.STACKgoals.AddLast(orGoal);
                Solver NewSolver = new Solver(this.STACKgoals, this.STACKBindings, this.HEAP, this.Q_querySolver);
                Solver r2 = NewSolver.solveGoal();
                this.STACKgoals.RemoveLast();   //??????????? where to locate ?????
                if (r2 != null)
                    yield return r2;
            }
        }

        private IEnumerable<Solver> iterateCHOICES_CUT(List<STACKGoalitem> _, bool requireFindall)
        {
            //Enumerate only 1 solution. This solution basicaly keep solving the STACKgoals as it is.
            Solver NewSolver = new Solver(this.STACKgoals, this.STACKBindings, this.HEAP, this.Q_querySolver);
            Solver r2 = NewSolver.solveGoal();
            yield return r2;

            //After solving, thick the CUT! flags
            this.Q_querySolver.Q_Cutted_bib = tentative_bib;

            //The next calls yield nothing.
        }

        private IEnumerable<Solver> iterateCHOICES_Clauses(List<Clause> ProgClauses, int tentativeQueryEntryBindToHEAPindex)
        {
            foreach (Clause ProgClause in ProgClauses)
            {
                //Debug.WriteLine("iterateCHOICES_Clauses: " + ProgClause.GenuineUserTxtClause);
                //Trace
                if (Globals.Ribbons.Ribbon1.tracer.Checked)
                    Tracer.outputTrace(port: "TRY ", ProgClause.GenuineUserTxtClause, tentative_bib);

                Solver r2 = applySingleClause(ProgClause, tentativeQueryEntryBindToHEAPindex, this.STACKgoals);
                if (r2 != null)
                    yield return r2;
                //else keep trying with next ProgClause
            }
        }

        private IEnumerable<Solver> iterateCHOICES_Retract(List<Clause> ProgClauses, int tentativeQueryEntryBindToHEAPindex)
        {
            foreach (Clause ProgClause in ProgClauses)
            {
                //Trace
                if (Globals.Ribbons.Ribbon1.tracer.Checked) Tracer.outputTrace(port: "TRY ", ProgClause.GenuineUserTxtClause, tentative_bib);

                Solver r2 = applySingleClause(ProgClause, tentativeQueryEntryBindToHEAPindex, this.STACKgoals);
                if (r2 != null)
                {
                    CODE.myClauses.Remove(ProgClause);  //This is where the "retract" work.
                    yield return r2;
                }
                //else keep trying with next ProgClause
            }
        }

        private Solver applySingleClause(Clause ProgClause, int EntryIntoHEAP, LinkedList<STACKGoalitem> mySTACKgoals)
        {
            //Very first call of this procedure for this solver object.
            if (next_bib == -1)
                next_bib = CODE.next_bibNbr;
            string next_bibNbr_string = "_" + next_bib;

            //Create a tentativeSTACKgoals for this solve() iteration. 
            LinkedList<STACKGoalitem> tentativeSTACKgoals;
            if (this.Q_querySolver.Q_TailCHOICEpoint == this)
            {
                tentativeSTACKgoals = new LinkedList<STACKGoalitem>();
                //foreach (STACKGoalitem i in this.STACKgoals)
                foreach (STACKGoalitem i in mySTACKgoals)
                {
                    tentativeSTACKgoals.AddLast(new STACKGoalitem
                    {
                        STACKgoal = i.STACKgoal,
                        STACKclause_bib = i.STACKclause_bib,
                        EntryHEAPcell = i.EntryHEAPcell,
                    });
                }
            }
            else
                //tentativeSTACKgoals = this.STACKgoals;
                tentativeSTACKgoals = mySTACKgoals;

            //Create tentative copy of HEAP. In case of ProgClause failure, to recover original for next tentative.
            List<HEAPCell> tentativeHEAP;
            tentativeHEAP = new List<HEAPCell>();
            foreach (HEAPCell hc in HEAP)
                tentativeHEAP.Add(new HEAPCell { Tag = hc.Tag, StoreAdress = hc.StoreAdress });

            //Create a tentativeSTACKBindings for this solve() iteration. 
            Dictionary<String, int> tentativeSTACKBindings;
            if (this.Q_querySolver.Q_TailCHOICEpoint == this)
            {
                tentativeSTACKBindings = new Dictionary<String, int>();
                //Start with a copy of already existing STACKBindings.
                foreach (KeyValuePair<String, int> b in this.STACKBindings)
                    tentativeSTACKBindings.Add(b.Key, b.Value);
            }
            else
                tentativeSTACKBindings = this.STACKBindings;
            //Complete tentativeSTACKBindings with new STACKBindings relevant to this Clause. Default value is -1, meaning not binded.
            foreach (KeyValuePair<String, Term> r in ProgClause.Registers)
            {
                //tentativeSTACKBindings.Add(Regex.Replace(r.Key, "}", next_bibNbr_string + "}"), -1);


                //???????????????????????????? modified for bagof/3 trial
                string key_bib = Regex.Replace(r.Key, "}", next_bibNbr_string + "}");

                if (tentativeSTACKBindings.ContainsKey(key_bib))
                    tentativeSTACKBindings[key_bib] = -1;
                else
                    tentativeSTACKBindings.Add(key_bib, -1);
                //???????????????????????????? modified for bagof/3 trial


            }

            //Major step for HEAP instantiation:
            bool solveHeadReturn = matchClauseWithHEAP(ProgClause.Head, ProgClause.Registers, tentativeSTACKBindings, next_bibNbr_string, tentativeHEAP, EntryIntoHEAP);

            if (!solveHeadReturn)
                return null;

            else if (solveHeadReturn && ProgClause.QueryFactRule == "R")
            {
                if (Globals.Ribbons.Ribbon1.tracer.Checked)
                {
                    string tracemsg2 = DisplayVarInstanciation(EntryIntoHEAP, tentativeHEAP, VarAsUserInput: false, ListUserFriendly: true);
                    Tracer.outputTrace(port: "SUCC", tracemsg2, next_bib);
                }

                //Add bodyGoal onto the STACKgoals.
                tentativeSTACKgoals.AddLast(new STACKGoalitem
                {
                    STACKgoal = ProgClause.Body,
                    STACKclause_bib = next_bib,
                });
            }

            //Go recursively to solve the next goal on the STACKgoals.
            Solver NewSolver = new Solver(tentativeSTACKgoals, tentativeSTACKBindings, tentativeHEAP, this.Q_querySolver);
            Solver r2 = NewSolver.solveGoal();
            return r2;
        }

        //############################################
        //############################################
        #region


        //Hereafter are mainly the WAM (Waren Abstract Machine) routines.

        private Boolean WAM_putGoalUponTheHeap(Term tentativeGoal, ref List<HEAPCell> myHEAP)
        {
            //WAM major step : put new goal on the HEAP.

            Dictionary<String, Term> tentativeTerms = tentativeGoal.MotherClause.Registers;

            foreach (String regName in tentativeGoal.FlattenedTerms)
            {
                Term reg = tentativeTerms[regName];
                //Debug.WriteLine("Query2HEAP reg >" + reg.UserRepresentation);                

                //WAM.put_structure (f/n, Xi)
                string tentativeTag = "_" + this.tentative_bib.ToString();
                this.STACKBindings[Regex.Replace(regName, "}", tentativeTag + "}")] = myHEAP.Count;
                myHEAP.Add(new HEAPCell() { Tag = "STR", StoreAdress = myHEAP.Count + 1 });
                myHEAP.Add(new HEAPCell() { Tag = reg.Functor, StoreAdress = reg.Arity });
                foreach (String Arg in reg.args)
                {
                    //Debug.WriteLine("Query2HEAP regArg           >" + Arg.ToString());
                    //String ArgTagged = Regex.Replace(Arg, "}", tentativeTag + "}");
                    //int ArgTaggedHTarget = this.STACKBindings[Regex.Replace(Arg, "}", tentativeTag + "}")];
                    //Debug.WriteLine("Query2HEAP ArgTagged        >" + ArgTagged);
                    //Debug.WriteLine("Query2HEAP ArgTaggedHTarget >" + ArgTaggedHTarget);

                    //    WAM_set_void(myHEAP);
                    if (this.STACKBindings[Regex.Replace(Arg, "}", tentativeTag + "}")] != -1)
                        WAM_set_value(tentativeTerms[Arg], this.STACKBindings, tentativeTag, myHEAP);
                    else
                        WAM_set_variable(tentativeTerms[Arg], this.STACKBindings, tentativeTag, myHEAP);
                }
            }


            return true;   //???????????????

        }


        private Boolean matchClauseWithHEAP(Term targetGoal, Dictionary<string, Term> myREGISTERS, Dictionary<string, int> STACKBindings, string tentativeTag, List<HEAPCell> myHEAP, int tentativeQueryEntryBindToHEAPindex)
        {
            Dictionary<String, Term> registers = myREGISTERS;
            String key = targetGoal.FlattenedTermsReversed.First();
            String keyTag = Regex.Replace(key, "}", tentativeTag + "}");
            STACKBindings[keyTag] = tentativeQueryEntryBindToHEAPindex;

            foreach (String XiName in targetGoal.FlattenedTermsReversed)
            {
                Term Xi = myREGISTERS[XiName];
                //Debug.WriteLine("Xi  > " + Xi.UserRepresentation);
                //WAM.get_structure (f/n, Xi)
                int addr = WAM_deref(STACKBindings[Regex.Replace(Xi.RegisterKey, "}", tentativeTag + "}")], myHEAP);
                if (myHEAP[addr].Tag == "REF")
                {
                    myHEAP.Add(new HEAPCell() { Tag = "STR", StoreAdress = myHEAP.Count + 1 });
                    myHEAP.Add(new HEAPCell() { Tag = Xi.Functor, StoreAdress = Xi.Arity });
                    WAM_bind(addr, (myHEAP.Count - 2), myHEAP);
                    foreach (String Arg in Xi.args)
                    {
                        //if (registers[Arg].UserRepresentation == "_")   // Is it useful ???
                        //    WAM_set_void(myHEAP);      // Is it useful ???
                        if (STACKBindings[Regex.Replace(Arg, "}", tentativeTag + "}")] != -1)
                            WAM_set_value(registers[Arg], STACKBindings, tentativeTag, myHEAP);
                        else
                            WAM_set_variable(registers[Arg], STACKBindings, tentativeTag, myHEAP);
                    }
                }
                else if (myHEAP[addr].Tag == "STR")
                {
                    int a = myHEAP[addr].StoreAdress;
                    if (myHEAP[a].Tag == Xi.Functor && myHEAP[a].StoreAdress == Xi.Arity)
                    {
                        for (int argi = 0; argi < Xi.Arity; argi++)
                        {
                            int S = a + argi + 1;
                            String kt2 = Regex.Replace(Xi.args[argi], "}", tentativeTag + "}");
                            if (registers[Xi.args[argi]].UserRepresentation == "_")
                            { }   //Anonymous _ , read mode, do nothing    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            else if (STACKBindings[kt2] != -1)
                            {
                                //Debug.WriteLine("STACKBindings[kt2] >" + kt2 + " " + STACKBindings[kt2]);
                                Boolean r = WAM_unify(STACKBindings[kt2], S, myHEAP);
                                if (!r)
                                    return false;                           //Debug.WriteLine("WAM_unify failed ");
                            }
                            else
                            {
                                STACKBindings[kt2] = S;                    //Solver.unify_variable_readMode
                            }
                        }
                    }
                    else
                        return false;  //Debug.WriteLine("fail STR !!!!");
                }
                else
                    return false;   //Debug.WriteLine("fail get_structure !!!!");
            }
            return true;
        }


        private void WAM_set_variable(Term Xi, Dictionary<string, int> STACKBindings, string tentativeTag, List<HEAPCell> myH)
        {
            HEAPCell NewHeapCell = new HEAPCell() { Tag = "REF", StoreAdress = myH.Count };
            String keyTag = Regex.Replace(Xi.RegisterKey, "}", tentativeTag + "}");
            STACKBindings[keyTag] = myH.Count;
            myH.Add(NewHeapCell);
        }


        private void WAM_set_value(Term Xi, Dictionary<string, int> STACKBindings, string tentativeTag, List<HEAPCell> myH)
        {
            String keyTag = Regex.Replace(Xi.RegisterKey, "}", tentativeTag + "}");
            myH.Add(myH[STACKBindings[keyTag]]);
        }


        internal int WAM_deref(int a, List<HEAPCell> myH)
        {
            //Follow the "REF" and "STR" cascade. When reach the very last "STR", return the relevant HEAP adress. 
            if (myH[a].Tag == "REF" && myH[a].StoreAdress != a)
                return WAM_deref(myH[a].StoreAdress, myH);
            else if (myH[a].Tag == "STR" && myH[myH[a].StoreAdress].Tag == "STR")
                return WAM_deref(myH[a].StoreAdress, myH);
            else return a;
        }

        internal int WAM_deref_target(int a, List<HEAPCell> myH)
        {
            //Follow the "REF" and "STR" cascade. Beyond the very last "STR", reach an atom or var. Return the relevant HEAP adress. 
            a = WAM_deref(a, myH);
            return myH[a].StoreAdress;
        }

        private Boolean literalEquality(int a1, int a2, List<HEAPCell> myH)
        {
            //Debug.WriteLine("literalEquality >" + a1 + " " + a2);
            if (a1 == a2)
                return true;
            else if (myH[a1].Tag == "REF" && myH[a1].StoreAdress != a1)
                return literalEquality(myH[a1].StoreAdress, a2, myH);
            else if (myH[a2].Tag == "REF" && myH[a2].StoreAdress != a2)
                return literalEquality(a1, myH[a2].StoreAdress, myH);
            else if (myH[a1].Tag == "STR")
                return literalEquality(myH[a1].StoreAdress, a2, myH);
            else if (myH[a2].Tag == "STR")
                return literalEquality(a1, myH[a2].StoreAdress, myH);
            else if (myH[a1].Tag != myH[a2].Tag)
                return false;
            else if (myH[a1].StoreAdress != myH[a2].StoreAdress)
                return false;
            else
            {
                int argsCount = myH[a1].StoreAdress;
                for (int i = 0; i < argsCount; i++)
                    if (!literalEquality(a1 + i + 1, a2 + i + 1, myH))
                        return false;
                return true;
            }
        }


        private Boolean WAM_bind(int StoreAdressA, int StoreAdressB, List<HEAPCell> myH)
        {
            if (myH[StoreAdressB].Tag == "REF" && myH[StoreAdressB].StoreAdress == StoreAdressB)
                myH[StoreAdressB].StoreAdress = StoreAdressA;
            else if (myH[StoreAdressA].Tag == "REF" && myH[StoreAdressA].StoreAdress == StoreAdressA)
                myH[StoreAdressA].StoreAdress = StoreAdressB;
            else
                return false;
            return true;
        }


        private Boolean WAM_unify(int a1, int a2, List<HEAPCell> myH)
        {
            int d1 = WAM_deref(a1, myH);
            int d2 = WAM_deref(a2, myH);

            if (d1 == d2)
                return true;
            else if (myH[d1].Tag == "REF" || myH[d2].Tag == "REF")
                return WAM_bind(d1, d2, myH);
            else if (myH[d1].Tag == "STR" && myH[d2].Tag == "STR")
            {
                int adr1 = myH[d1].StoreAdress;
                int adr2 = myH[d2].StoreAdress;
                return WAM_unify(adr1, adr2, myH);
            }
            else if (myH[a1].Tag == myH[a2].Tag
                && myH[a1].StoreAdress == myH[a2].StoreAdress)
            {
                int argIndexMax = myH[a1].StoreAdress;
                for (int i = 0; i < argIndexMax; i++)
                {
                    int argi = i + 1;
                    int ttt1 = WAM_deref(a1 + argi, myH);
                    int ttt2 = WAM_deref(a2 + argi, myH);
                    Boolean isUnified = WAM_unify(ttt1, ttt2, myH);
                    if (!isUnified)
                        return false;
                }
                return true;
            }
            return false;
        }


        internal string DisplayVarInstanciation(int a, List<HEAPCell> heapCell, bool VarAsUserInput, bool ListUserFriendly)
        {
            //PrintHEAP(HEAP);                       
            //Debug.WriteLine("DisplayVarInstanciation " + a);   

            a = WAM_deref(a, heapCell);
            if (heapCell[a].Tag == "REF" && heapCell[a].StoreAdress == a)   //Unbound Variable
            {
                String varDisplay = "";
                if (VarAsUserInput)
                {
                    foreach (KeyValuePair<String, int> b in STACKBindings)
                        if (b.Value == a)
                        {
                            varDisplay = tentative_Goal.MotherClause.Registers[b.Key].UserRepresentation;
                            break;
                        }
                }
                else
                    varDisplay = " _";      //varDisplay = "?";
                return varDisplay;
            }
            else if (heapCell[a].Tag == "STR")
                return (DisplayVarInstanciation(heapCell[a].StoreAdress, heapCell, VarAsUserInput, ListUserFriendly));
            else if (heapCell[a].StoreAdress == 0)   //It's a simple atom we want to display
                return (heapCell[a].Tag);
            else if (ListUserFriendly && heapCell[a].Tag == "." && heapCell[a].StoreAdress == 2)    //It's a list
            {
                String d = "";
                String arg1 = DisplayVarInstanciation(a + 1, heapCell, VarAsUserInput, ListUserFriendly);
                String arg2 = DisplayVarInstanciation(a + 2, heapCell, VarAsUserInput, ListUserFriendly);
                if (arg2 == "[]")
                    d = "[" + arg1 + "]";
                else if (arg2.Substring(0, 1) == "[" && arg2.Substring(arg2.Length - 1) == "]")
                    d += "[" + arg1 + "," + arg2.Remove(0, 1);
                else
                    d += "[" + arg1 + "|" + arg2 + "]";
                return (d);
            }
            else if (ListUserFriendly && heapCell[a].Tag == "," && heapCell[a].StoreAdress > 1)    //It's a list
            {
                String d = "(";
                for(int i = 1; i <= heapCell[a].StoreAdress; i++)
                {
                    d += DisplayVarInstanciation(a + i, heapCell, VarAsUserInput, ListUserFriendly) + ",";
                }
                d = d.Remove(d.Length - 1, 1);
                d += ")";
                return (d);
            }

            else if (heapCell[a].StoreAdress > 0)    //It's a functor we want to display with its args
            {
                String d = " ";
                d += heapCell[a].Tag + "(";
                for (int i = 1; i <= heapCell[a].StoreAdress; i++)
                    d += DisplayVarInstanciation(a + i, heapCell, VarAsUserInput, ListUserFriendly) + ", ";
                d = d.Remove(d.Length - 2, 2);
                d += ")";
                return (d);
            }
            else return ("DisplayVarInstanciation error");
        }

        private int OrderSuperior(int SUP, int INF, List<HEAPCell> heap)
        {
            //Order of terms superiority is:    Variables < Numbers < Strings < Atoms < Compound Terms
            int SUP2 = WAM_deref(SUP, heap);
            int INF2 = WAM_deref(INF, heap);
            int sup = WAM_deref_target(SUP2, heap);
            int inf = WAM_deref_target(INF2, heap);
            int supWeight = 0;
            int infWeight = 0;

            if (heap[inf].Tag == "REF" && heap[inf].StoreAdress == inf)
                infWeight = 1000;       //it's an Unbound var
            else if (double.TryParse(heap[inf].Tag, out double _))
                infWeight = 2000;       //It's a number
            else if (heap[inf].StoreAdress == 0)
                infWeight = 4000;       //It's a simple atom
            else if (heap[INF2].Tag == "STR" && heap[inf].StoreAdress > 0)
                infWeight = 5000;       //It's a compound

            if (heap[sup].Tag == "REF" && heap[sup].StoreAdress == sup)
                supWeight = 1000;       //it's an Unbound var
            else if (double.TryParse(heap[sup].Tag, out double _))
                supWeight = 2000;       //It's a number
            else if (heap[sup].StoreAdress == 0)
                supWeight = 4000;       //It's a simple atom
            else if (heap[SUP2].Tag == "STR" && heap[sup].StoreAdress > 0)
                supWeight = 5000;       //It's a compound


            if (supWeight > infWeight)
                return 1;
            else if (supWeight < infWeight)
                return -1;

            else if (infWeight == 1000 && supWeight == 1000)
            {                           //Both Unbound vars. The adress is the age of vars, and make order.
                if (sup > inf) return 1;
                else if (sup < inf) return -1;
                else return 0;
            }

            else if (infWeight == 2000 && supWeight == 2000)
            {                           //Both numbers.
                double.TryParse(heap[inf].Tag, out double nbrInf);
                double.TryParse(heap[sup].Tag, out double nbrSup);
                if (nbrSup > nbrInf) return 1;
                else if (nbrSup < nbrInf) return -1;
                else return 0;
            }

            else if (infWeight == 4000 && supWeight == 4000)
            {                           //Both atoms. alphabetic order
                String atomInf = heap[inf].Tag;
                String atomSup = heap[sup].Tag;
                int c = string.CompareOrdinal(atomSup, atomInf);
                //return (c);
                if (c > 0) return 1;
                else if (c < 0) return -1;
                else return 0;
            }

            else if (infWeight == 5000 && supWeight == 5000)
            {                           //Both compounds. Compare STRarity, then functor, then each arg.
                //Compare STRarity.
                int arityInf = heap[inf].StoreAdress;
                int aritySup = heap[sup].StoreAdress;
                if (aritySup > arityInf) return 1;
                else if (aritySup < arityInf) return -1;

                //Compare functors
                String functorInf = heap[inf].Tag;
                String functorSup = heap[sup].Tag;
                int f = string.CompareOrdinal(functorSup, functorInf);
                if (f != 0)
                    return (f);

                //Need to compare each arg until a difference is established.
                else
                {
                    for (int a = 1; a <= arityInf; a++)
                    {
                        int BindRegNameInf = inf + a;
                        int BindRegNameSup = sup + a;
                        int r = OrderSuperior(BindRegNameSup, BindRegNameInf, HEAP);
                        if (r != 0)
                            return (r);
                    }
                }
                return 0;       // ???????????????????
            }
            return 0;       //Should never get here?????????????
        }


        private string EvaluateMath(int a, List<HEAPCell> heapCell)
        {
            
            //Debug.WriteLine("EvaluateMath " + a);
            
            a = WAM_deref(a, heapCell);
            if (heapCell[a].Tag == "REF" && heapCell[a].StoreAdress == a)   //Unbound Variable
                return ("_");
            else if (heapCell[a].Tag == "STR")
                return (EvaluateMath(heapCell[a].StoreAdress, heapCell));
            else if (heapCell[a].StoreAdress == 0)   //It's a simple atom, hopfully number, to be returned
            {
                String arg0 = (heapCell[a].Tag);
                if (!double.TryParse(arg0, out double Arg0))
                    throw new plixException(arg0 + " is not a number.  #770");
                return Arg0.ToString();
            }

            else if (heapCell[a].Tag == "" && heapCell[a].StoreAdress == 1)   //It's parenthesis, just go to inside it.
            {
                return (EvaluateMath( (a + heapCell[a].StoreAdress), heapCell));
            }
                                        
            else if (heapCell[a].StoreAdress > 0)    //It's a functor, hopefully math, we want to evaluate
            {
                int arity = heapCell[a].StoreAdress;

                String op = heapCell[a].Tag;
                double Result = 0;
                String arg1 = EvaluateMath(a + 1, heapCell);
                if (!double.TryParse(arg1, out double Arg1))
                    throw new plixException(arg1 + " is not a number.  #771");


                double Arg2 = 0;
                if (arity > 1)
                {
                    String arg2 = EvaluateMath(a + 2, heapCell);
                    if (!double.TryParse(arg2, out Arg2))
                        throw new plixException(arg2 + " is not a number.  #772");
                }

                try
                {
                    if (op == "+") Result = Arg1 + Arg2;
                    else if (op == "-") Result = Arg1 - Arg2;
                    else if (op == "/") Result = Arg1 / Arg2;
                    else if (op == "*") Result = Arg1 * Arg2;
                    else if (op == "**") Result = Math.Pow(Arg1, Arg2);



                    //!!!!!!!!!!!!!!!!!!!!
                    else if (op == "abs" && arity == 1) Result = Math.Abs(Arg1);



                    else if (op == "max") Result = Math.Max(Arg1, Arg2);
                    else if (op == "min") Result = Math.Min(Arg1, Arg2);

                    else if (op == "min") Result = Math.Min(Arg1, Arg2);
                    else if (op == "//")
                    {
                        int Arg1Int = Convert.ToInt32(Arg1);
                        int Arg2Int = Convert.ToInt32(Arg2);
                        int integerQuotient = Arg1Int / Arg2Int;
                        Result = integerQuotient;
                    }
                    else if (op == "mod")
                    {
                        int Arg1Int = Convert.ToInt32(Arg1);
                        int Arg2Int = Convert.ToInt32(Arg2);
                        int remainder = Arg1Int % Arg2Int;
                        Result = remainder;
                    }

                    else if (op == @">") if (Arg1 > Arg2) Result = 1; else Result = 0;
                    else if (op == @">=") if (Arg1 >= Arg2) Result = 1; else Result = 0;
                    else if (op == @"=<") if (Arg1 <= Arg2) Result = 1; else Result = 0;
                    else if (op == @"<") if (Arg1 < Arg2) Result = 1; else Result = 0;
                    else if (op == @"=:=") Result = Convert.ToDouble(Math.Equals(Arg1, Arg2));
                    else if (op == @"=\=") Result = Convert.ToDouble(!Math.Equals(Arg1, Arg2));
                }
                catch
                { return null; }

                String result2 = Result.ToString();
                String result3 = result2.Replace(",", ".");
                return result3;
            }
            else throw new plixException("EvaluateMath error.  #778");
        }


        public void PrintHEAP(List<HEAPCell> myH)
        {
            Debug.WriteLine("---   PrintHEAP  >");
            for (int i = 0; i < myH.Count; i++) Debug.WriteLine(i + " " + myH[i].ToString());
        }

        #endregion

    }
}
