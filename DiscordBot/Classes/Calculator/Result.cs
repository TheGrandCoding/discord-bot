using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Calculator
{
    public abstract class Result
    {
        public static Result operator +(Result left, Result right)
        {
            dynamic leftV = left;
            dynamic rightV = right;
            return CalculationTree.getValue((leftV.Value + rightV.Value).ToString());
        }
        public static Result operator *(Result left, Result right)
        {
            dynamic leftV = left;
            dynamic rightV = right;
            return CalculationTree.getValue((leftV.Value * rightV.Value).ToString());
        }
        public static Result operator /(Result left, Result right)
        {
            dynamic leftV = left;
            dynamic rightV = right;
            return CalculationTree.getValue((leftV.Value / rightV.Value).ToString());
        }
        public static Result operator ^(Result left, Result right)
        {
            dynamic leftV = left;
            dynamic rightV = right;
            dynamic r = Math.Pow((double)leftV.Value, (double)rightV.Value);
            return CalculationTree.getValue(r.ToString());
        }
        public static Result operator -(Result left, Result right)
        {
            dynamic leftV = left;
            dynamic rightV = right;
            return CalculationTree.getValue((leftV.Value - rightV.Value).ToString());
        }
        public static Result operator &(Result left, Result right)
        {
            if (left is BooleanResult l && right is BooleanResult r)
                return new BooleanResult(l.Value && r.Value);
            dynamic leftV = left;
            dynamic rightV = right;
            return CalculationTree.getValue((leftV.Value & rightV.Value).ToString());
        }
        public static Result operator |(Result left, Result right)
        {
            if (left is BooleanResult l && right is BooleanResult r)
                return new BooleanResult(l.Value || r.Value);
            dynamic leftV = left;
            dynamic rightV = right;
            return CalculationTree.getValue((leftV.Value | rightV.Value).ToString());
        }
    }
    public class DoubleResult : Result
    {
        public DoubleResult(double d)
        {
            Value = d;
        }
        public double Value { get; set; }

        public static implicit operator DoubleResult(double d) => new DoubleResult(d);
        public override string ToString() => Value.ToString();
    }
    public class IntegerResult : Result
    {
        public IntegerResult(int i)
        {
            Value = i;
        }
        public int Value { get; set; }
        public override string ToString() => Value.ToString();
        public static implicit operator IntegerResult(int d) => new IntegerResult(d);

    }
    public class StringResult : Result
    {
        public StringResult(string s)
        {
            Value = s;
        }
        public string Value { get; set; }

        public override string ToString() => Value.ToString();
    }
    public class BooleanResult : Result
    {
        public BooleanResult(bool b)
        {
            Value = b;
        }
        public bool Value { get; set; }
    }
}
