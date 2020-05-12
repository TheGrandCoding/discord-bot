using DiscordBot.Classes.Calculator;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Tests.CalcTests
{
    public class Constants
    {
        public Calculator Calculator { get; set; }
        [SetUp]
        public void Setup()
        {
            Calculator = new Calculator();
        }

        [Test]
        public void CheckPI()
        {
            Assert.AreEqual(Calculator.Output("pi()"), Math.PI);
        }
        [Test]
        public void CheckE()
        {
            Assert.AreEqual(Calculator.Output("e()"), Math.E);
        }
    }
}
