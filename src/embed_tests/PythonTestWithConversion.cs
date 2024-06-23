using System;
using System.Linq;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// Tests for method binding in presence of overloads that require conversion
    /// </summary>
    internal class TestOverloadWithConversion
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        [Test]
        public void TestSupportedCast()
        {
            var instance = new Overloads();
            using (Py.GIL())
            {
                dynamic callSumWithInts = PythonEngine.Eval("lambda o: o.Sum(1,2)");
                var result = callSumWithInts(instance.ToPython());
                Assert.IsTrue(PyFloat.IsFloatType(result));
                object value;

                Assert.IsTrue(Converter.ToManagedValue((result as PyObject).Reference, typeof(double), out value, false));
                Assert.AreEqual(3.0, value);
            }
        }

        [Test]
        public void TestParamsArray()
        {
            var instance = new Overloads();
            using (Py.GIL())
            {
                dynamic callSumWithStrings = PythonEngine.Eval("lambda o: o.Sum('1','2')");
                var result = callSumWithStrings(instance.ToPython());
                Assert.IsTrue(PyString.IsStringType(result));
                object value;
                Assert.IsTrue(Converter.ToManagedValue((result as PyObject).Reference, typeof(string), out value, false));
                Assert.AreEqual("12", value);
            }
        }

        [Test]
        public void TestParamsArrayAgainstCastable()
        {
            var instance = new Overloads();
            using (Py.GIL())
            {
                dynamic callSum2WithInts = PythonEngine.Eval("lambda o: o.Sum2(1,2)");
                var result = callSum2WithInts(instance.ToPython());

                Assert.IsTrue(PyFloat.IsFloatType(result));
                object value;
                Assert.IsTrue(Converter.ToManagedValue((result as PyObject).Reference, typeof(double), out value, false));
                Assert.AreEqual(3, value);
            }
        }

        [Test]
        public void TestDefaultValue()
        {
            var instance = new Overloads();
            using (Py.GIL())
            {
                dynamic callMultiplyWithInts = PythonEngine.Eval("lambda o: o.Multiply(2)");
                var result = callMultiplyWithInts(instance.ToPython());

                // USed to be int in the internal pythonent fork with changes from this commit 132deadfff19095e8f1dc80e2dcf8507b8a01ce4
                Assert.IsTrue(PyFloat.IsFloatType(result));
                object value;
                Assert.IsTrue(Converter.ToManagedValue((result as PyObject).Reference, typeof(double), out value, false));
                Assert.AreEqual(2.0, value);
            }
        }

        public class Overloads
        {
            public double Sum(double d, long l)
            {
                return d + l;
            }

            public string Sum(int a, string b)
            {
                return a.ToString() + b;
            }

            public string Sum(params string[] values)
            {
                return string.Concat(values);
            }

            public double Multiply(double a, double b = 1)
            {
                return a * b;
            }

            public int Multiply(int a, int b)
            {
                return a * b;
            }

            public int Sum2(params int[] values)
            {
                return values.Sum();
            }

            public double Sum2(double a, double b)
            {
                return a + b;
            }
        }
    }
}
