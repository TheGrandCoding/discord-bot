using Markdig.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace DiscordBot.Classes.Calculator
{
    public class CalculationTree
    {
        public TreeNode Root { get; set; }

        public Dictionary<char, Func<Result, Result, Result>> OperatorActions { get; } = new Dictionary<char, Func<Result, Result, Result>>();

        public static List<char> Operators { get; } = new List<char>()
        {
            ',', '^', '/', '*','+', '-', '|', '&'
        };
        public static Dictionary<string, double> Constants = new Dictionary<string, double>()
        {
            { "π", Math.PI },
            { "g", 9.81 }
        };
        public static List<MethodInfo> Functions { get; set; } = new List<MethodInfo>();

        public List<string> CalcLog { get; set; } = new List<string>();
        public void log(string c, int indent = 0)
        {
            if (indent > 0)
                c = new string(' ', indent * 2) + c;
            CalcLog.Add(c);
        }

        class manualToConstant
        {
            public static double PI() => Math.PI;
            public static double E() => Math.E;
            public static double C() => 3E8;
        }

        static CalculationTree() 
        {
            addFunctions(typeof(System.Math));
            addFunctions(typeof(manualToConstant));
        }

        public CalculationTree(string thing)
        {
            OperatorActions['^'] = (left, right) => left ^ right;
            OperatorActions['/'] = (left, right) => left / right;
            OperatorActions['+'] = (left, right) => left + right;
            OperatorActions['*'] = (left, right) => left * right;
            OperatorActions['-'] = (left, right) => left - right;
            OperatorActions['|'] = (left, right) => left | right;
            OperatorActions['&'] = (left, right) => left & right;
            Root = getNode(thing);
            Root.SetDepth(0);
        }

        static bool validType(Type type)
        {
            return type == typeof(bool)
                || type == typeof(int)
                || type == typeof(double)
                || type == typeof(string);
        }

        static void addFunctions(Type type)
        {
            var functions = type.GetMethods().Where(x => x.IsStatic);
            foreach(var fnc in functions)
            {
                if (!validType(fnc.ReturnType))
                    continue;
                var prms = fnc.GetParameters();
                var anyInvalid = prms.Any(x => validType(x.ParameterType) == false);
                if (anyInvalid)
                    continue;
                if (Functions.Any(x => x.Name == fnc.Name && x.GetParameters().Length == prms.Length))
                    continue;
                Functions.Add(fnc);
            }
        }

        public static List<string> tokenize(string text)
        {
            text = text.Replace(" ", "");
            var ls = new List<string>();
            int last = 0;
            for(int i = 0; i < text.Length; i++)
            {
                if(text[i] == '(')
                {
                    if(i == last)
                    {
                        ls.Add("(");
                    } else
                    {
                        ls.Add(text[last..i]);
                        ls.Add("(");
                    }
                    last = i + 1;
                }
                else if(text[i] == ')')
                {
                    ls.Add(text[last..i]);
                    ls.Add(")");
                    last = i + 1;
                }
                else if (Operators.Contains(text[i]) || text[i] == ',')
                {
                    ls.Add(text[last..i]);
                    ls.Add(text[i].ToString());
                    last = i + 1;
                }
            }
            ls.Add(text[(last)..]);
            return ls.Where(x => x != "").ToList();
        }

        int getIndexOfTerm(int direction, int current, List<string> tokens)
        {
            int brackets = 0;
            for(int i = current + direction; i >= 0 && i < tokens.Count; i += direction)
            {
                if (tokens[i] == "(")
                    brackets++;
                else if (tokens[i] == ")")
                    brackets--;
                else if(brackets == 0)
                    return i;
            }
            if (direction == 1)
                return tokens.Count + 1;
            return 0;
        }

        bool isOperator(string token)
        {
            return token.Length == 1 && Operators.Contains(token[0]);
        }

        public TreeNode getNode(List<string> tokenized)
        {
            if (tokenized.Count == 0)
                return null;
            for (int i = 0; i < tokenized.Count; i++)
            {
                var token = tokenized[i];
                foreach (var constant in Constants)
                {
                    if (token.Length <= constant.Key.Length)
                        continue;
                    var index = token.IndexOf(constant.Key);
                    if (index == -1)
                        continue;
                    var left = token[0..index];
                    var right = token[(index + constant.Key.Length)..];
                    tokenized.RemoveAt(i);
                    int add = 0;
                    if (!string.IsNullOrWhiteSpace(right))
                    {
                        tokenized.Insert(i, right);
                        add++;
                    }
                    tokenized.Insert(i, constant.Key);
                    if(!string.IsNullOrWhiteSpace(left))
                    {
                        tokenized.Insert(i, left);
                        add++;
                    }
                    i += add;
                    Console.WriteLine($"Split '{token}' into '{left}' '{constant.Key}' '{right}'");
                    break;
                }
            }
            for(int i = 0; i < tokenized.Count; i++)
            {
                var token = tokenized[i];
                bool isIn = Constants.ContainsKey(token);
                if(i > 0 && isIn)
                {
                    var before = tokenized[i - 1];
                    if(before == ")" || before.Last().IsDigit())
                    {
                        tokenized.Insert(i, "*");
                        Console.WriteLine($"Inserted * before {i}");
                        i++;
                    }
                }
                if(i < tokenized.Count - 1 && isIn)
                {

                    var after = tokenized[i + 1];
                    if (after == "(" || after.First().IsDigit())
                    {
                        tokenized.Insert(i + 1, "*");
                        Console.WriteLine($"Inserted * after {i}");
                        i++;
                    }
                }
            }
            var brackets = 0;
            var validOpers = new List<OperPosition>();
            for (int i = 0; i < tokenized.Count; i++)
            {
                var token = tokenized[i];
                if (token == "(")
                    brackets++;
                else if (token == ")")
                    brackets--;
                else if (brackets == 0 && isOperator(token))
                    validOpers.Add(new OperPosition(i, token[0]));
            }
            var commas = validOpers.Where(x => x.Value == ',').ToList();
            if(commas.Count > 0)
            {
                var ls = new List<string>();
                var indexes = commas.OrderBy(x => x.Index).Select(x => x.Index).ToList();
                int last = 0;
                while(indexes.Count > 0)
                {
                    var length = indexes[0] - last;
                    last = indexes[0] + 1;
                    var toAdd = tokenized.Take(length);
                    indexes.RemoveAt(0);
                    tokenized = tokenized.Skip(length + 1).ToList();
                    ls.Add(string.Join("", toAdd));
                }
                if (tokenized.Count > 0)
                    ls.Add(string.Join("", tokenized));
                return new ArrayNode(this, ls);
            }
            if(validOpers.Count == 1)
            {
                var oper = validOpers[0];
                var left = tokenized.Take(oper.Index).ToList();
                var right = tokenized.Skip(oper.Index + 1).ToList();
                return new OperatorNode(this, left, oper.Value.ToString(), right);
            }
            foreach (var oper in Operators)
            {
                var first = validOpers.FirstOrDefault(x => x.Value == oper);
                if (first == null)
                    continue;
                int before = getIndexOfTerm(-1, first, tokenized);
                if (before < 0)
                    before = 0;
                int after = getIndexOfTerm(1, first, tokenized) + 1;
                if (after > tokenized.Count)
                    after = tokenized.Count;
                tokenized.Insert(after, ")"); 
                tokenized.Insert(before, "(");
                return getNode(tokenized);
            }
            var text = string.Join("", tokenized);
            if (brackets == 0 && tokenized[0] == "(" && tokenized[tokenized.Count - 1] == ")")
                return new BracketNode(this, text);
            if (tokenized.Count > 1 && tokenized[1] == "(" && tokenized[0].All(x => x.IsAlpha()))
            {
                int firstBracket = text.IndexOf('(');
                var fName = text.Substring(0, firstBracket);
                var within = text[(firstBracket + 1)..^1];
                return new FunctionNode(this, fName, within);
            }
            return new ValueNode(this, text);
        }

        public static Result getValue(string thing)
        {
            if (int.TryParse(thing, out var i))
                return new IntegerResult(i);
            if (double.TryParse(thing, out var d))
                return new DoubleResult(d);
            if (bool.TryParse(thing, out var b))
                return new BooleanResult(b);
            if (Constants.TryGetValue(thing, out var value))
                return new DoubleResult(value);
            return new StringResult(thing);
        }

        public Result Calculate()
        {
            return Root.Calculate();
        }

        public Result callOperator(Result left, string oper, Result right)
        {
            return OperatorActions[oper[0]].Invoke(left, right);
        }

        object getValue(TreeNode node)
        {
            if (node == null)
                return null;
            return ((dynamic)node.Calculate()).Value;
        }

        public Result callFunction(string name, TreeNode node)
        {
            var args = new List<object>();
            if(node is ArrayNode arr)
            {
                foreach (var x in arr.Arguments)
                    args.Add(getValue(x));
            } else
            {
                if(node != null)
                    args.Add(getValue(node));
            }
            var fnc = Functions.FirstOrDefault(x => x.Name.ToLower() == name.ToLower() &&
                x.GetParameters().Length == args.Count);
            if (fnc == null)
                throw new Exception($"Could not find any function by that name with {args.Count} args");
            var result = fnc.Invoke(null, args.ToArray());
            if (result is double d)
                return new DoubleResult(d);
            if (result is int i)
                return new IntegerResult(i);
            if (result is bool b)
                return new BooleanResult(b);
            if(result is string s)
                return new StringResult(s);
            return new StringResult($"{result}");
        }

        [DebuggerDisplay("{Index} {Value}")]
        class OperPosition
        {
            public int Index { get; set; }
            public char Value { get; set; }
            public OperPosition(int i, char c)
            {
                Index = i;
                Value = c;
            }
            public static implicit operator int(OperPosition pos) => pos.Index;
            public static implicit operator char(OperPosition pos) => pos.Value;
        }

        public TreeNode getNode(string text)
        {
            var tokenized = tokenize(text);
            return getNode(tokenized);
        }
    }
    public abstract class TreeNode
    {
        public TreeNode(CalculationTree tree, string text)
        {
            Tree = tree;
            Text = text;
        }
        public CalculationTree Tree { get; set; }
        public string Text { get; set; }
        public override string ToString()
        {
            return Text;
        }

        public int Depth { get; set; }

        public virtual void SetDepth(int d)
        {
            Depth = d;
        }

        public virtual Result Calculate()
        {
            if (this is ValueNode)
                return GetResult();
            Tree.log("> " + Text, Depth);
            var result = GetResult();
            Tree.log("= " + result, Depth);
            return result;
        }
        public abstract Result GetResult();
    }
    public class ValueNode : TreeNode
    {
        public ValueNode(CalculationTree tree, string text) : base(tree, text)
        {
        }

        public override Result Calculate()
        {
            return GetResult();
        }
        public override Result GetResult()
        {
            return CalculationTree.getValue(Text);
        }
    }
    public class ArrayNode : TreeNode
    {
        public ArrayNode(CalculationTree tree, List<string> args) : base(tree, string.Join(',', args))
        {
            Arguments = new List<TreeNode>();
            foreach (var x in args)
                Arguments.Add(tree.getNode(x));
        }

        public List<TreeNode> Arguments { get; set; }

        public override Result GetResult()
        {
            return new StringResult($"[{string.Join(", ", Arguments.Select(x => x.GetResult()))}]");
        }
    }
    public class OperatorNode : TreeNode
    {
        public OperatorNode(CalculationTree tree, List<string> left, string oper, List<string> right) : base(tree, "")
        {
            Left = tree.getNode(left);
            Operator = oper;
            Right = tree.getNode(right);
            Text = $"({Left} {Operator} {Right})";
        }

        public override void SetDepth(int d)
        {
            base.SetDepth(d);
            Left.SetDepth(d + 1);
            Right.SetDepth(d + 1);
        }

        public string Operator { get; set; }
        public TreeNode Left { get; set; }
        public TreeNode Right { get; set; }

        public override Result GetResult()
        {
            return Tree.callOperator(Left.Calculate(), Operator, Right.Calculate());
        }
    }
    public class FunctionNode : TreeNode
    {
        public FunctionNode(CalculationTree tree, string fName, string within) : base(tree, $"{fName}({within})")
        {
            Function = fName;
            Contents = tree.getNode(within);
        }

        public string Function { get; set; }
        public TreeNode Contents { get; set; }

        public override void SetDepth(int d)
        {
            base.SetDepth(d);
            Contents?.SetDepth(d + 1);
        }

        public override Result GetResult()
        {
            return Tree.callFunction(Function, Contents);
        }
    }
    public class BracketNode : TreeNode
    {
        public BracketNode(CalculationTree tree, string text) : base(tree, text)
        {
            Inside = tree.getNode(text[1..^1]);
        }
        public TreeNode Inside { get; set; }
        public override void SetDepth(int d)
        {
            base.SetDepth(d);
            Inside.SetDepth(d);
        }

        public override Result Calculate()
        {
            return GetResult();
        }
        public override Result GetResult()
        {
            return Inside.Calculate();
        }
    }
}
