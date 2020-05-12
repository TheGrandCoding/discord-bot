using DiscordBot.Classes.Calculator;
using NUnit.Framework;

namespace DiscordBot.Tests.CalcTests
{
    public class BuiltIn
    {
        public Calculator Calculator { get; set; }
        [SetUp]
        public void Setup()
        {
            Calculator = new Calculator();
        }
        [Test]
        public void Add_Integer()
        {
            Assert.AreEqual(Calculator.Output("5+5"), 10);
            Assert.AreEqual(Calculator.Output("10+30"), 40);
            Assert.AreEqual(Calculator.Output("3 +1"), 4);
            Assert.AreEqual(Calculator.Output("1+ 5"), 6);
            Assert.AreEqual(Calculator.Output("10 + 1650"), 1660);
            Assert.AreEqual(Calculator.Output(" 10 + 1650 "), 1660);
        }
        [Test]
        public void Add_Decimal()
        {
            Assert.AreEqual(Calculator.Output("0.5 + 0.5"), 1);
            Assert.AreEqual(Calculator.Output("27.75 + 2.25"), 30);
        }
        [Test]
        public void Subtract_Positive()
        {
            Assert.AreEqual(Calculator.Output("5-0"), 5);
            Assert.AreEqual(Calculator.Output("5 - 0"), 5);
            Assert.AreEqual(Calculator.Output("5-1"), 4);
            Assert.AreEqual(Calculator.Output("35.75 - 0.75"), 35);
            Assert.AreEqual(Calculator.Output("10.5 - 0.5"), 10);
        }
        [Test]
        public void Subtract_Negative()
        {
            Assert.Positive(Calculator.Output("2-1"));
            Assert.AreEqual(Calculator.Output("2-2"), 0);
            Assert.Negative(Calculator.Output("2-3"));
        }
    }
}