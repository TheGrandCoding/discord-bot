using DiscordBot.Classes.Calculator;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Tests.CalcTests
{
    public class OrderOperations
    {
        // B I D M A S

        public Calculator Calculator { get; set; }

        [SetUp]
        public void Setup()
        {
            Calculator = new Calculator();
        }

        [Test]
        public void Indices_Before_Division()
        {
            // 2^6 = 64
            // 64/4 = 16.
            Assert.AreEqual(Calculator.Output("2^6/4"), 16);
        }

        [Test]
        public void Division_Before_Multiplication()
        {
            // (10 / 5) * 4 = 2 * 4 = 8
            // vs
            // 10 / (5 * 4) = 10 / 20 = 0.5
            Assert.AreEqual(Calculator.Output("10/5*4"), 8);
        }

        [Test]
        public void Multiply_Before_Add()
        {
            Assert.AreEqual(Calculator.Output("2*3+5"), 11);
        }

        [Test]
        public void Add_Before_Subtract()
        { // Shouldnt technically make a difference which way round
            // (10 + 5) - 3 = 15 - 3 = 12
            // vs
            // 10 + (5 - 3) = 10 + 2 = 12
            Assert.AreEqual(Calculator.Output("10+5-3"), 12);
        }

        [Test]
        public void Many_Multiply()
        {
            Assert.AreEqual(Calculator.Output("2*2*2+3"), 11);
        }

        [Test]
        public void Many_All()
        {
            var o = Calculator.Output("5^2*3+1-10/2");
            // 5 ^ 2 * 3 + 1 - 10 / 2
            // (5 ^ 2) * 3 + 1 - 10 / 2
            // 25 * 3 + 1 - (10 / 2)
            // 25 * 3 + 1 - 5
            // 75 + 1 - 5
            // 76 - 5
            // 71
            TestContext.WriteLine(string.Join("\n", Calculator.Steps));
            Assert.AreEqual(o, 71);
        }

        [Test]
        public void Bracket_Test()
        {
            Assert.AreEqual(Calculator.Output("10 / (5 * 4)"), 0.5);
        }

        [Test]
        public void Bracket_SameLevel()
        {
            Assert.AreEqual(Calculator.Output("(45 - 10) * (7 * 5)"), 35 * 35);
        }

        [Test]
        public void Bracket_Invalid()
        {
            Assert.Throws<InvalidOperationException>(() => { Calculator.Output("(4 + 3"); }, "Fail because no closing bracket");
        }
    }
}
