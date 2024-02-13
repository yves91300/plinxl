using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms.VisualStyles;
using Microsoft.Win32;


namespace plinxl
{

    internal class Clause
    {
        internal String GenuineUserTxtClause;
        internal String txtClause= "";
        internal String QueryFactRule;
        internal Dictionary<String, Term> Registers = new Dictionary<String, Term>();
        internal Term TOPterm;
        internal Term Head = new Term();
        internal Term Body = new Term();

        private List<String> Atom_keywords_tokens = new List<String>();



        internal Clause(String txtClause = "")
        {
            QueryFactRule = "";
            
            if (txtClause != "")
            {
                this.GenuineUserTxtClause = txtClause;
                this.txtClause = "";
                //Major step. Tokenize atoms, keywords, and other obvious signs. 
                _ = AddAndtokenizeObviousTerms(GenuineUserTxtClause);
                //Debug.WriteLine("ObviousTerms  " + txtClause);
            }
        }

        internal String FinalizeClauseCreation()
        {
            //Major step. Each structured token (functor + args) is gathered into a parent token. 
            this.txtClause = tokenizeSTR(this.txtClause);
            //Debug.WriteLine("tokenizeSTR " + AdditionaltxtClause);

            if (!Regex.IsMatch(txtClause, @"^{\d+\}\.$"))        //AdditionaltxtClause have to be a simple single token.
            {
                QueryFactRule = "E";
                return "Syntax error.  #552";
            }

            //Tokenized Terms has to be a Query, a Rule, a Fact. Finalize Clause creation.
            //Or a directive :-/2.
            _ = TOPterm.flattenTerms(this.Registers);
            if (TOPterm.PredicateName == ":-/2")               //It's a RULE
            {
                this.QueryFactRule = "R";
                this.Head = Registers[TOPterm.args.First()];
                this.Body = Registers[TOPterm.args.Last()];
            }

            else if (TOPterm.PredicateName == "::-/1")           //It's an infix directive
            {
                this.QueryFactRule = "D";

                Term directive = Registers[TOPterm.args[0]];
                if (directive.PredicateName != "op/3")
                    return "Syntax error.  #458";

                String precedenceStr = Registers[directive.args[0]].Functor;
                int.TryParse( precedenceStr, out int precedence);
                if (precedence > 1200 || precedence < 0)
                    return "Precedence must be between 0 and 1200.  #435";
                else precedence += 5000;   //Garanty exactly 4 digits for all precedence internal representation

                String fixType = Registers[directive.args[1]].Functor;
                if (fixType.Length == 2) fixType = "_" + fixType;   //fixType has to be 3 digits, such as "xfx" or "_fx"

                String Operator = Registers[directive.args[2]].Functor;

                Debug.WriteLine("op directive " + precedence + fixType + Operator);

                if (CODE._fixOpsUser_Dict.ContainsKey(Operator))
                    CODE._fixOpsUser_Dict.Remove(Operator);

                if (precedence > 5000)          //precedence 0 means to delete this keyword.
                    CODE._fixOpsUser_Dict.Add(Operator, precedence + fixType);

                CODE.keywordsUser = "";
                foreach (KeyValuePair<String, String>kw in CODE._fixOpsUser_Dict)
                    CODE.keywordsUser += "|" + kw.Key;

                //Debug.WriteLine("keywordsUser " + CODE.keywordsUser);
            }

            else if (TOPterm.PredicateName == "?-/1")               //It's a QUERY
            {
                this.QueryFactRule = "Q";
                this.Body = Registers[TOPterm.args.First()];
            }
            else if (Regex.IsMatch(txtClause, @"^{\d+\}\.$"))     //It's a FACT
            {
                this.QueryFactRule = "F";
                this.Head = TOPterm;
            }
            else
                return "Clause is not a Query, a Rule, or a Fact.  #451";

            return this.QueryFactRule;
        }


        internal String AddAndtokenizeObviousTerms(String AdditionaltxtClause)
        {
            //Debug.WriteLine("AddAndtokenizeObviousTerms  " + AdditionaltxtClause);
            GenuineUserTxtClause += AdditionaltxtClause;

            //Detect dot[.] = end of Clause. Avoid builtin predicate "=.."
            //Match matchEndOfClause = Regex.Match(AdditionaltxtClause, @"^(?<before>.*)\.\s*$");
            Match matchEndOfClause = Regex.Match(AdditionaltxtClause, @"^(?<before>.*)(?<!\=\.)\.\s*$");
            //?????????????????????????????????????????????????????????????

            if (matchEndOfClause.Success)
            {
                String txtClauseWithoutEnd = matchEndOfClause.Groups["before"].Value;
                this.txtClause += tokenizeObviousTerms(txtClauseWithoutEnd) + ".";
                String r = FinalizeClauseCreation();
                return r;
            }

            else
            {
                this.txtClause += tokenizeObviousTerms(AdditionaltxtClause);
                return "?";
            }           
        }

        private String tokenizeObviousTerms(String txtClause)
        {
            
            //Debug.WriteLine("tokenizeObviousTerms   " + txtClause);


            //Tokenize 'quoted sentences'
            Regex reg;
            reg = new Regex(@"(?<tk>['].+?['])");
            txtClause = reg.Replace(txtClause, delegate (Match m) {
                return this.getOrCreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
            });

            //Debug.WriteLine("txt2obviousTokens   " + AdditionaltxtClause);

            //Tokenize  []emptyList  and !cut
            reg = new Regex(@"(?<tk>(\[\]|[!]))");
            txtClause = reg.Replace(txtClause, delegate (Match m) {
                return this.getOrCreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
            });

            //Help to understand double meaning of "-" in cases of "5 - 3" or "5 + -3"
            txtClause = Regex.Replace(txtClause, @"(?<=[\d)])(\s*[\+\-])(?=\d)", " $1 ");
            //Debug.WriteLine("txt2obviousTokens05 " + AdditionaltxtClause);
            //Help to differentiate keyword with ( as functor, or keyword with ( as group.
            txtClause = Regex.Replace(txtClause, @"(?<=\d+)(\s*[\+\-\*/])(?=[(])", " $1 ");
            //Add a space after ()][   .So compiler detect negative nbr -2 inside (-2*3) 
            txtClause = Regex.Replace(txtClause, @"([()\]\[]\s*)", "$1 ");
            //Add a comma before ]    We'll need it to compile the list 
            txtClause = Regex.Replace(txtClause, @"(\s*[\]]\s*)", "," + " $1 ");
            //Ease to understand meaning of ",". Add spaces
            txtClause = Regex.Replace(txtClause, @"(\s*[,]\s*)", " $1 ");                      
            // :- can be a rule sign :-/3 or directive sign :-/2. Case of directive, transform it into ::-/2 for distinct treatment. 
            Match matchDirective = Regex.Match(this.GenuineUserTxtClause, @"(^\s*[:][-]\s*)");
            if (Regex.Match(this.GenuineUserTxtClause, @"(^\s*[:][-]\s*)").Success)
                txtClause = Regex.Replace(txtClause, @"(^\s*[:][-]\s*)", " ::- ");

            //Tokenize AnonymousVar
            reg = new Regex(@"(?<=\W)[_](?=[\s)\],;])");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {   //To be noticed : we do not 'getOrCreateTermS'. We systematically 'CreateTermS' for each anonymous var in this Clause.
                return this.CreateTermT(UserRepresentation: "_", Functor: "_", isVar: true).RegisterKey;
            });

            //Tokenize Vars
            reg = new Regex(@"\b(?<tk>[_A-Z][_a-zA-Z0-9]*)\b");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                return this.getOrCreateTermT(UserRepresentation: m.Groups["tk"].Value, isVar: true).RegisterKey;
            });

            //Tokenize NbrSigned
            reg = new Regex(@"\s+(?<tk>[+-]\d+(\.\d+)?)");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                return " " + this.CreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey + " ";
            });

            //Tokenize NbrUnsigned      Avoid confusion with existing token such as {123} or atom1
            reg = new Regex(@"(?<![{\w])(?<tk>\d+(\.\d+)?)(?![}])");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                return " " + this.CreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey + " ";
            });

            //Tokenize ( False_keyword ) that are a simple atom, not a keyword 
            reg = new Regex(@"(?<=[({\[;,]\s*)(?<tk>" + CODE.keywords + CODE.keywordsUser + @")(?=\s*[)}\];,])");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                //Debug.WriteLine("False ( keyword ) " + m.Groups["tk"].Value);
                return this.getOrCreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
            });

            //Tokenize infix keyword                                  
            reg = new Regex(@"(?<=^|\s|})(?<tk>" + CODE.keywords + CODE.keywordsUser + @")(?=\s|{)");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                String tentativeKW = m.Groups["tk"].Value;
                //Debug.WriteLine("infix keyword " + tentativeKW + " from " + txtClause);                
                Term infixTerm = this.CreateTermT(UserRepresentation: tentativeKW, Functor: tentativeKW);

                String _fixValue;
                if (CODE._fixOpsUser_Dict.ContainsKey(tentativeKW))
                    _fixValue = CODE._fixOpsUser_Dict[tentativeKW];
                else
                    _fixValue = CODE._fixOps_Dict[tentativeKW];
                //_fixValue looks like this:            "5700xfx"
                //Isolate Precedence such as            "5700"
                String strPrecedence = _fixValue.Substring(0, 4);
                int.TryParse(_fixValue.Substring(0, 4), out int precedence);
                //Isolate fixType such as               "xfx"
                String fixType = _fixValue.Substring(4);
                infixTerm.Precedence = precedence;
                infixTerm._fixType = fixType;

                //Atom_keywords_tokens is a list such as [ "5700.9992{8}", ...]     precedence.order{key}
                int intKey = infixTerm.KeyIndex;

                //The order in wich the keys with the same precedence will be tentatively solved.
                //infix type such as xfy has to count down.
                int order;
                if (fixType.Substring(fixType.Length - 1) == "y")
                    order = 9999 - intKey;
                else
                    order = 1000 + intKey;
                Atom_keywords_tokens.Add(strPrecedence + "." + order + infixTerm.RegisterKey);
                Atom_keywords_tokens.Sort();
                //return key;
                return infixTerm.RegisterKey;
            });

            //Debug.WriteLine("txt2obviousTokens61 " + txtClause);

            //Tokenize Functor_Keyword
            reg = new Regex(@"(?<=^|\s|})(?<tk>" + CODE.keywords + @")[(]");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                String key = this.CreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
                return key.Substring(0, key.Length - 1) + "(";
            });

            //Debug.WriteLine("txt2obviousTokens42 " + AdditionaltxtClause);

            //Tokenize Atom_word
            reg = new Regex(@"\b(?<tk>[a-z][_a-zA-Z0-9]*)\b(?![(])");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                //Debug.WriteLine("Atom_word " + m.Groups["tk"].Value);
                return this.getOrCreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
            });

            //Debug.WriteLine("txt2obviousTokens63 " + AdditionaltxtClause);

            //Tokenize Functor_Word
            reg = new Regex(@"\b(?<tk>[a-z][_a-zA-Z0-9]*)[(]");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                //Debug.WriteLine("FunctorAtomWord " + m.Groups["tk"].Value);
                String key = this.CreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
                return key.Substring(0, key.Length - 1) + "(";
            });

            //Tokenize Atom_Symbol
            reg = new Regex(@"(?<=^|\s|})(?<tk>[\+\-\*\/><=:\.&_~]+)(?=\s|{)");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                //Debug.WriteLine("Atom_Symbol " + m.Groups["tk"].Value);
                return this.getOrCreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
            });

            //Tokenize Functor_Symbol
            reg = new Regex(@"(?<=^|\s|})(?<tk>[\+\-\*\/><=:\.&_~]+)[(]");
            txtClause = reg.Replace(txtClause, delegate (Match m)
            {
                //Debug.WriteLine("Functor_Symbol " + m.Groups["tk"].Value);
                String key = this.CreateTermT(UserRepresentation: m.Groups["tk"].Value, Functor: m.Groups["tk"].Value).RegisterKey;
                return key.Substring(0, key.Length - 1) + "(";
            });

            //Delete spaces
            txtClause = Regex.Replace(txtClause, @"\s*", @"");
            //Debug.WriteLine("txt2obviousTokens65 " + txtClause);
            return txtClause;
        }


        private String tokenizeSTR(String tokensClause)
        {
            //Debug.WriteLine("tokenizeSTR       " + tokensClause);

            Match matchFunctorArg = Regex.Match(tokensClause, @"^(?<before>.*?)" +
                @"\{(?<functor>\d+)[(]" + @"(?<arg>\{\d+\})?[)]" + @"(?<after>.*)$");

            Match matchArgs = Regex.Match(tokensClause, @"^(?<before>.*?)" +   //No functor, and one or several args
                @"(?<=\d[(])(?<str>(\{\d+\})(\{\d+\})+)(?=[)])" +
                @"(?<after>.*)$");

            Match matchPriorityParenthesys = Regex.Match(tokensClause, @"^(?<before>.*?)" +   //No functor, and one or several args
                @"(?<!\d)[(](?<str>(?<arg>\{\d+\})+)[)]" +
                @"(?<after>.*)$");

            Match listHT = Regex.Match(tokensClause, @"^(?<before>.*?)" + 
                            @"\[(?<listItem>\{\d+\})[|](?<listTail>\{\d+\})(?<listComma>\{\d+\})\]" + @"(?<after>.*)$");
            Match listStart = Regex.Match(tokensClause, @"^(?<before>.*?)" + 
                            @"\[(?<listItem>\{\d+\})(?<listComma>\{\d+\})(?<rest>(\{\d+\}|[|])+)\]" + @"(?<after>.*)$");
            Match listLastI = Regex.Match(tokensClause, @"^(?<before>.*?)" + 
                            @"\[(?<listItem>\{\d+\})(?<listComma>\{\d+\})\]" + @"(?<after>.*)$");


            //Treat particular patterns.
            if (matchFunctorArg.Success)
            {
                String functorKey = "{" + matchFunctorArg.Groups["functor"].Value + "}";
                String functorName = Registers[functorKey].Functor;
                Term myFunctorTerm = Registers[functorKey];
                
                //Debug.WriteLine("functorKeyFull   " + functorKey + functorName);

                String myArgTermname = matchFunctorArg.Groups["arg"].Value;
                Term myArgTerm = null;
                try
                {  myArgTerm = Registers[myArgTermname];  }
                catch { }

                //This clause may have several args. In this case, args are gathered into AND ,/2 clauses. We deconstruct these ,/2 clauses to extract each arg.
                while (myArgTerm != null && myArgTerm.PredicateName == ",/2")
                {
                    String myArgTermTAILname = myArgTerm.args[1];
                    Term myArgTermTAIL = Registers[myArgTermTAILname];
                    String myArgTermHEADname = myArgTerm.args[0];
                    Term myArgTermHEAD = Registers[myArgTermHEADname];

                    myFunctorTerm.args.Add(myArgTermHEADname);

                    if (myArgTermHEAD.Precedence > myFunctorTerm.Precedence)
                        myFunctorTerm.Precedence = myArgTermHEAD.Precedence;

                    myArgTerm = myArgTermTAIL;
                }

                if (myArgTerm != null)
                {
                    myFunctorTerm.args.Add(myArgTerm.RegisterKey);
                    if (myArgTerm.Precedence > myFunctorTerm.Precedence)
                        myFunctorTerm.Precedence = myArgTerm.Precedence;
                }

                myFunctorTerm.PredicateName = myFunctorTerm.Functor + "/" + myFunctorTerm.Arity;
                //String key = myFunctorTerm.RegisterKey;
                tokensClause = matchFunctorArg.Groups["before"].Value + myFunctorTerm.RegisterKey + matchFunctorArg.Groups["after"].Value;
                //Debug.WriteLine("functorKeyFullEnd   " + myFunctorTerm.PredicateName);
                TOPterm = myFunctorTerm;
                return tokenizeSTR(tokensClause);
            }

            else if (matchArgs.Success)
            {                
                //Debug.WriteLine("matchArgs " + matchArgs.Groups["str"].Value);
                
                String tokensParenthesisContent = tokenizeSTR(matchArgs.Groups["str"].Value);
                String argsTokenized = tokenizeSTR(tokensParenthesisContent);
                tokensClause = matchArgs.Groups["before"].Value + argsTokenized + matchArgs.Groups["after"].Value;
                //Debug.WriteLine("matchArgs After " + tokensClause);
                return tokenizeSTR(tokensClause);
            }

            else if (matchPriorityParenthesys.Success)
            {
                //Debug.WriteLine("matchPriorityParenthesys " + matchPriorityParenthesys.Groups["str"].Value);
                String tokensParenthesisContent = tokenizeSTR(matchPriorityParenthesys.Groups["str"].Value);
                
                //Term myParenthesisTerm = this.CreateTermT(UserRepresentation: "()", Functor: "()");
                Term myParenthesisTerm = this.CreateTermT(UserRepresentation: "", Functor: "");                
                myParenthesisTerm.args.Add(tokensParenthesisContent);
                myParenthesisTerm.PredicateName = "/1";                
                myParenthesisTerm.Precedence = 0;
                tokensClause = matchPriorityParenthesys.Groups["before"].Value + myParenthesisTerm.RegisterKey + matchPriorityParenthesys.Groups["after"].Value;
                return tokenizeSTR(tokensClause);
            }

            else if (listHT.Success)
            {
                //Debug.WriteLine("listHT            " + tokensClause);                
                Term myNewTerm = new Term()
                {
                    UserRepresentation = ".",
                    Functor = ".",
                    PredicateName = "./2",
                };
                Term myReturnedT = this.AddTerm(myNewTerm);
                tokensClause = listHT.Groups["before"].Value + myReturnedT.functorizedKey + listHT.Groups["listItem"].Value + listHT.Groups["listComma"].Value + listHT.Groups["listTail"].Value + ")" + listHT.Groups["after"].Value;
                return tokenizeSTR(tokensClause);
            }

            else if (listStart.Success)
            {
                //Debug.WriteLine("listStart         " + tokensClause);
                Term myNewTerm = new Term()
                {
                    UserRepresentation = ".",
                    Functor = ".",
                    PredicateName = "./2",
                };
                //String key = this.AddTerm(myNewTerm);
                //String keyFunctorized = key.Substring(0, key.Length - 1) + "(";
                Term myReturnedT = this.AddTerm(myNewTerm);
                tokensClause = listStart.Groups["before"].Value + myReturnedT.functorizedKey + listStart.Groups["listItem"].Value + listStart.Groups["listComma"].Value + "[" + listStart.Groups["rest"].Value + "])" + listStart.Groups["after"].Value;
                return tokenizeSTR(tokensClause);
            }

            else if (listLastI.Success)
            {
                //Debug.WriteLine("listLastI         " + tokensClause);
                String emptyListRegister = this.getOrCreateTermT(UserRepresentation: "[]", Functor: "[]").RegisterKey;

                Term myNewTerm = new Term()
                {
                    UserRepresentation = ".",
                    Functor = ".",
                    PredicateName = "./2",
                };
                
                //String key = this.AddTerm(myNewTerm);
                //String keyFunctorized = key.Substring(0, key.Length - 1) + "(";
                Term myReturnedT = this.AddTerm(myNewTerm);
                tokensClause = listLastI.Groups["before"].Value + myReturnedT.functorizedKey + listLastI.Groups["listItem"].Value + listLastI.Groups["listComma"].Value + emptyListRegister + ")" + listLastI.Groups["after"].Value;
                return tokenizeSTR(tokensClause);
            }

            else if (Atom_keywords_tokens.Count > 0)
            {
                //Debug.WriteLine("match_InfixToken  " + tokensClause);
                foreach (String PredenceToken in Atom_keywords_tokens)
                {
                    //Debug.WriteLine("Try token         " + PredenceToken);
                    //PredenceToken looks like this:              "5700.1005{12}"
                    String matching_token = PredenceToken.Substring(9);
                    String matching_op1 = Registers[matching_token].Functor;
                    String matching_tokenNbr = PredenceToken.Substring(10);
                    matching_tokenNbr = matching_tokenNbr.Substring(0, matching_tokenNbr.Length - 1);
                    String matching_op2 = @"[{](?<op>" + matching_tokenNbr + @")[}]";

                    String _fixParameters;
                    if (CODE._fixOpsUser_Dict.ContainsKey(matching_op1))
                        _fixParameters = CODE._fixOpsUser_Dict[matching_op1];
                    else
                        _fixParameters = CODE._fixOps_Dict[matching_op1];

                    //_fixParameters looks like this:               @"5700xfx"
                    //Isolate Precedence such as                    "5700"
                    int.TryParse(_fixParameters.Substring(0, 4), out int precedence);
                    //Isolate fixType such as               xfx
                    String fixType = _fixParameters.Substring(4);

                    String matching_op3 = "";
                    if (fixType == "_fx" || fixType == "_fy")
                        matching_op3 = matching_op2 + @"\s*(?<postarg>\{\d+\})";
                    else if (fixType == "xf_" || fixType == "yf_")
                        matching_op3 = @"(?<prearg>\{\d+\})\s*" + matching_op2;
                    else if (fixType == "xfx" || fixType == "xfy" || fixType == "yfx")
                        matching_op3 = @"(?<prearg>\{\d+\})\s*" + matching_op2 + @"\s*(?<postarg>\{\d+\})";

                    if (Regex.IsMatch(tokensClause, matching_op3))
                    {
                        //Debug.WriteLine("matching_op3 " + precedence + " " + fixType + " " + matching_op1);
                        // Complete the Regex with before an after. 
                        // nota: xfx make regex<before> lazy .*?    xfy make regex.<before> not lazy .*
                        String matching_op4 = "";
                        if (fixType == "xfy")
                            matching_op4 = @"^(?<before>.*)" + matching_op3 + @"(?<after>.*)$";
                        else if (fixType == "xfx")
                            matching_op4 = @"^(?<before>.*)" + matching_op3 + @"(?<after>.*)$";
                        else if (fixType == "yfx")
                            matching_op4 = @"^(?<before>.*?)" + matching_op3 + @"(?<after>.*)$";
                        else if (fixType == "_fx")
                            matching_op4 = @"^(?<before>.*)" + matching_op3 + @"(?<after>.*)$";
                        else if (fixType == "_fy")
                            matching_op4 = @"^(?<before>.*?)" + matching_op3 + @"(?<after>.*)$";
                        else if (fixType == "xf_")
                            matching_op4 = @"^(?<before>.*?)" + matching_op3 + @"(?<after>.*)$";
                        else if (fixType == "yf_")
                            matching_op4 = @"^(?<before>.*)" + matching_op3 + @"(?<after>.*)$";
                        else
                            throw new plixException("Invalid infix, prefix or postfix notation.  #524");

                        Match m = Regex.Match(tokensClause, matching_op4);
                        //Debug.WriteLine("matching_op4  " + matching_op4);

                        int preargPrecedence = 0;
                        int postargPrecedence = 0;
                        if (fixType == "xf_" || fixType == "yf_" || fixType == "xfx" || fixType == "yfy")
                            preargPrecedence = this.Registers[m.Groups["prearg"].Value].Precedence;
                        else if (fixType == "_fx" || fixType == "_fy" || fixType == "xfx" || fixType == "yfy")
                            postargPrecedence = this.Registers[m.Groups["postarg"].Value].Precedence;
                        //Debug.WriteLine("pre post Precedence  " + preargPrecedence + " " + postargPrecedence);

                        if (fixType.Substring(0, 1) == "x" && !(preargPrecedence < precedence))
                            throw new plixException("Invalid precedence.  #525");
                        else if (fixType.Substring(0, 1) == "y" && !(preargPrecedence <= precedence))
                            throw new plixException("Invalid precedence.  #526");
                        if (fixType.Substring(fixType.Length - 1) == "x" && !(postargPrecedence < precedence))
                            throw new plixException("Invalid precedence.  #527");
                        else if (fixType.Substring(0, 1) == "y" && !(postargPrecedence <= precedence))
                            throw new plixException("Invalid precedence.  #528");

                        Term myNewTerm = Registers[matching_token];
                      
                        if (fixType == "_fx" || fixType == "_fy")
                        {
                            myNewTerm.UserRepresentation = matching_op1 + m.Groups["postarg"].Value;
                            myNewTerm.PredicateName = matching_op1 + "/1";
                            myNewTerm.args.Add(m.Groups["postarg"].Value);
                        }
                        else if (fixType == "xf_" || fixType == "yf_")
                        {
                            myNewTerm.UserRepresentation = m.Groups["prearg"].Value + matching_op1;
                            myNewTerm.PredicateName = matching_op1 + "/1";
                            myNewTerm.args.Add(m.Groups["prearg"].Value);
                        }
                        else
                        {
                            myNewTerm.UserRepresentation = m.Groups["prearg"].Value + matching_op1 + m.Groups["postarg"].Value;
                            myNewTerm.PredicateName = matching_op1 + "/2";
                            myNewTerm.args.Add(m.Groups["prearg"].Value);
                            myNewTerm.args.Add(m.Groups["postarg"].Value);
                        }

                        tokensClause = m.Groups["before"].Value + matching_token + m.Groups["after"].Value;
                        Atom_keywords_tokens.RemoveAll(p => p == PredenceToken);
                        TOPterm = myNewTerm;
                        //Debug.WriteLine("tokenizeStructures end     " + tokensClause);
                        return tokenizeSTR(tokensClause);
                    }
                }
            }

            return tokensClause;
        }


        private Term getOrCreateTermT(String UserRepresentation, String Functor = "", Boolean isVar = false, Boolean isKeyword = false, int Precedence = 0)
        {
            foreach (KeyValuePair<String, Term> r in Registers)
            {
                if (UserRepresentation == r.Value.UserRepresentation)
                    return r.Value;
            }
            //return CreateTermS(UserRepresentation, Functor, isVar, isKeyword, Precedence);
            return CreateTermT(UserRepresentation, Functor, isVar, isKeyword, Precedence);
        }

        internal Term CreateTermT(String UserRepresentation, String Functor = "", Boolean isVar = false, Boolean isKeyword = false, int Precedence = 0)
        {
            Term myNewTerm = new Term()
            {
                UserRepresentation = UserRepresentation,
                Functor = Functor,
                isVar = isVar,
                isKeyword = isKeyword,
                Precedence = Precedence,
                PredicateName = Functor + "/0",     //Default value. 
            };

            Term myReturnedT = this.AddTerm(myNewTerm);
            return myReturnedT;
        }

        private Term AddTerm(Term t)     //Return the key for this Term
        {
            t.KeyIndex = Registers.Count;
            //String key = "{" + (Registers.Count) + "}";
            //t.RegisterKey = key;
            t.MotherClause = this;
            Registers.Add(t.RegisterKey, t);
            TOPterm = t;

            //return t.RegisterKey;
            return t;

        }

        internal void PrintClause()
        {
            Debug.WriteLine("PrintREGISTERS >" + txtClause);
            foreach (KeyValuePair<String, Term> element in Registers) Debug.WriteLine("Reg " + element.ToString());
            Debug.WriteLine("Head  >");
            Head.ToString();
            Debug.WriteLine("Body  >");
            Body.ToString();
        }
    }

    internal class Term
    {
        internal String UserRepresentation;
        internal int KeyIndex;
        //internal String RegisterKey;
        internal Boolean isVar = false;
        internal Boolean isKeyword = false;
        internal Boolean isFlattenedGoal = false;
        internal String Functor;
        internal List<String> args = new List<string>();
        internal List<String> FlattenedTerms = new List<String>();
        internal List<String> FlattenedTermsReversed = new List<String>();
        internal String PredicateName = "";
        internal Clause MotherClause;
        internal int Arity { get => args.Count(); }
        internal String RegisterKey { get => "{" + KeyIndex + "}"; }
        internal String functorizedKey { get => "{" + KeyIndex + "("; }


        internal int Precedence;
        internal String _fixType = "";


        public override String ToString()
        {
            String myReturn = "";
            if (String.IsNullOrEmpty(Functor))
                myReturn += UserRepresentation;
            else
            {
                myReturn += Functor + " ( ";
                foreach (String a in args) myReturn += a + " ";
                myReturn += ")";
            }
            return myReturn;
        }

        internal List<String> flattenTerms(Dictionary<String, Term> myRegisters)
        {
            foreach (String argName in this.args)
            {
                Term SubTerm = myRegisters[argName];
                List<String> flattenSubTerm = SubTerm.flattenTerms(myRegisters);
                this.FlattenedTerms.AddRange(flattenSubTerm);
            }

            if (!this.isVar)
                this.FlattenedTerms.Add(this.RegisterKey);

            //Avoid duplicates inside FlattenedTerms lists
            this.FlattenedTerms = this.FlattenedTerms.Distinct().ToList();

            //FlattenedTerms   is ordered from small atom up to whole term. Suitable for Query.
            //FlattenedTermsReversed   is ordered from whole term down to small atom. Suitable for Clause.Head.
            foreach (String t in this.FlattenedTerms) this.FlattenedTermsReversed.Add(t);
            this.FlattenedTermsReversed.Reverse();

            return FlattenedTerms;
        }
    }
}
