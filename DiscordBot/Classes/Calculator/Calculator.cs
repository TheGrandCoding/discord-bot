using DiscordBot.Classes.Calculator.Process;
using System;
using System.Collections.Generic;
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
        public Calculator() : this(null)
        {
            Processes.Add(new Brackets(this));
            Processes.Add(new SumDice(this));
            Processes.Add(new Indices(this));
            Processes.Add(new Factorial(this));
            Processes.Add(new Division(this));
            Processes.Add(new Multiplication(this));
            Processes.Add(new Addition(this));
            Processes.Add(new Subtraction(this));
        }


        public void AddStep(string i)
        {
            Parent?.AddStep("  " + i);
            Steps.Add(i);
        } 

        public string Output(string input)
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
                            result = x.Process(mtch.Value, mtch);
                        }
                        catch (Exception ex)
                        {
                            Program.LogMsg(ex, "Calc");
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
            return input;
        }
    }
}
