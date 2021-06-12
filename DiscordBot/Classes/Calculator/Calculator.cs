using DiscordBot.Classes.Calculator.Functions;
using DiscordBot.Classes.Calculator.Process;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Calculator
{
    public class Calculator
    {
        public Calculator Parent { get; }
        public List<CalcProcess> Processes { get; }
        public string Original { get; private set; }
        public List<string> Steps { get; }
        public int Depth { get; set; } 
        public Calculator(Calculator parent)
        {
            Parent = parent;
            Depth = (parent?.Depth ?? -1) + 1;
            Processes = parent?.Processes ?? new List<CalcProcess>();
            Steps = new List<string>();
        }

        bool validArg(Type argType)
        {
            return argType == typeof(int)
                || argType == typeof(double);
        }

        class manualToConstant
        {
            public static double PI() => throw new ReplaceStringException("π");
            public static double E() => throw new ReplaceStringException("e");
        }

        void LoadMathFunctions(Type mathsType)
        {
            foreach(var method in mathsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (validArg(method.ReturnType) == false)
                    continue;
                var args = method.GetParameters();
                bool anyInvalid = args.Any(x => validArg(x.ParameterType) == false);
                if (anyInvalid)
                    continue;
                var methFunction = new MethodFunction(this, method);
                var existing = Processes.Where(x => x is MethodFunction)
                    .Select(x => x as MethodFunction)
                    .Any(x => x.Name == methFunction.Name && x.Arguments.Count == methFunction.Arguments.Count);
                if(!existing)
                    Processes.Add(methFunction);
            }
        }

        public static Dictionary<string, double> Constants = new Dictionary<string, double>()
        {
            { "π", Math.PI },
            { "e", Math.E },
            { "g", 9.81 },
            { "c", 3E8 },
            { "eV", 1.6E-19 },
            { "TRUE", 1 },
            { "FALSE", 0 },
        };

        public Calculator() : this(null)
        {
            LoadMathFunctions(typeof(manualToConstant));
            LoadMathFunctions(typeof(Math));
            Processes.Add(new Brackets(this));
            Processes.Add(new SumDice(this));
            Processes.Add(new MultIndices(this));
            Processes.Add(new Indices(this));
            Processes.Add(new Factorial(this));
            Processes.Add(new Division(this));
            Processes.Add(new Multiplication(this));
            Processes.Add(new Addition(this));
            Processes.Add(new Subtraction(this));
            Processes.Add(new BooleanEQ(this));
            Processes.Add(new BooleanAND(this));
            Processes.Add(new BooleanOR(this));
        }


        public void AddStep(string i)
        {
            Parent?.AddStep("  " + i);
            Steps.Add(i);
        } 

        public double Output(string input)
        {
            foreach (var x in Processes)
                x.Calculator = this;
            Steps.Clear();
            AddStep((Parent == null ? "" : "> ") + input);
            bool performed = false;
            do
            {
                performed = false;
                foreach (var x in Processes)
                {
                    var mtch = x.RegEx.Match(input);
                    if (mtch.Success)
                    {
                        performed = true;
                        string result;
                        try
                        {
                            result = x.Process(mtch.Value, mtch).ToString();
                        } catch (ReplaceStringException ex)
                        {
                            result = ex.Message;
                        } catch(TargetInvocationException ex)
                        {
                            if (ex.InnerException is ReplaceStringException e)
                                result = e.Message;
                            else
                                throw ex.InnerException;
                        }
                        catch (Exception ex)
                        {
                            Program.LogError(ex, "Calc");
                            AddStep("<> Error: " + ex.Message);
                            throw;
                        }
                        var strB = new StringBuilder(input);
                        strB.Remove(mtch.Index, mtch.Length);
                        strB.Insert(mtch.Index, result);
                        input = strB.ToString();
                        AddStep("= " + input);
                        break; // re-start order of operations
                    }
                }
            } while (performed);
            foreach (var x in Processes)
                x.Calculator = Parent;
            if (CalcProcess.TryParseDouble(input, out var s))
                return s;
            throw new InvalidOperationException($"Could not complete calculation");
        }
    }
}
