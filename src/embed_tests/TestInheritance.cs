using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPropertiesBase
    {
        public string ChangePropertyType { get => "ChangePropertyType"; set { } }

        public double InheritedGetter { get => 1.1; }

        public int InheritedSetter { set { } }

        public double HidePropertyWithMethod { get => 1.1; }

        public int HiddenMethod()
        {
            return 0;
        }

        public int OverloadedMethod(int a)
        {
            return a;
        }

        public int OverloadedMethod(int a, int b)
        {
            return a + b;
        }

        public string this[int flag]
        {
            get => "FromBase";
        }
    }

    public class TestPropertiesDerived : TestPropertiesBase
    {
        public new int ChangePropertyType { get => 0; set { } }

        public int OnlyInDerived { get => 0; }

        public new string HiddenMethod()
        {
            return "HiddenMethod";
        }

        public new string HidePropertyWithMethod()
        {
            return "HidePropertyWithMethod";
        }

        public new int OverloadedMethod(int a)
        {
            return 7;
        }

        public int OverloadedMethod(int a, string b)
        {
            return a + int.Parse(b);
        }

        public new string this[int flag]
        {
            // using get accessor 
            get => "FromDerived";
        }
    }

    class TestInheritance
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
        [Description("Verify that the base class can call it's own property")]
        public void TestBaseCanCallBaseProperty()
        {
            var b = new TestPropertiesBase();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("b", b);
                scope.Exec($"result=b.ChangePropertyType");
                var result = scope.Get("result").ToString();
                Assert.AreEqual("ChangePropertyType", result);
            }
        }

        [Test]
        [Description("Verify that the base class can't call a derived class's property")]
        public void TestBaseCannotCallDerivedProperty()
        {
            var b = new TestPropertiesBase();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("b", b);

                Exception expected = null;
                try
                {
                    scope.Exec($"result=b.OnlyInDerived");
                }
                catch (Exception e)
                {
                    expected = e;
                }
                Assert.IsNotNull(expected);
            }
        }

        [Test]
        [Description("Verify that the derived class can call a visible property from its base class")]
        public void TestDerivedCanCallBaseProperty()
        {
            var d = new TestPropertiesDerived();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("d", d);
                scope.Exec($"result=d.InheritedGetter;" +
                    $"d.InheritedSetter=4;");

                var result = scope.Get("result").ToString();
                Assert.AreEqual("1.1", result);
            }
        }

        [Test]
        [Description("Verify that the derived class can't call a hidden method from its base class")]
        public void TestDerivedCannotCallHiddenMethod()
        {
            var d = new TestPropertiesDerived();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("d", d);
                scope.Exec($"result=d.HiddenMethod();");

                var result = scope.Get("result").ToString();
                Assert.AreEqual("HiddenMethod", result);
            }
        }

        [Test]
        [Description("Verify that the derived class can hide a property from its base class with a method")]
        public void TestDerivedHidesPropertyWithMethod()
        {
            var d = new TestPropertiesDerived();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("d", d);
                scope.Exec($"result=d.HidePropertyWithMethod();");

                var result = scope.Get("result").ToString();
                Assert.AreEqual("HidePropertyWithMethod", result);
            }
        }

        [Test]
        [Description("Verify that the derived class can't call a hidden property from its base class")]
        public void TestDerivedCannotCallHiddenProperty()
        {
            var d = new TestPropertiesDerived();
            var test = "value";
            using (PyModule scope = Py.CreateScope())
            {
                Exception expected = null;
                scope.Set("d", d);
                scope.Set("test", test);
                try
                {
                    scope.Exec($"d.ChangePropertyType=test");
                }
                catch (Exception e)
                {
                    expected = e;
                }

                Assert.IsNotNull(expected);
            }
        }

        [Test]
        [Description("Verify that the derived class can call its properties")]
        public void TestDerivedCanCallVisibleProperty()
        {
            var d = new TestPropertiesDerived();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("d", d);
                scope.Exec($"result1=d.ChangePropertyType;" +
                    $"result2=d.OnlyInDerived;");

                var result1 = scope.Get("result1").ToString();
                var result2 = scope.Get("result2").ToString();

                Assert.AreEqual("0", result1);
                Assert.AreEqual("0", result2);
            }
        }

        [Test]
        [Description("Verify that the derived class can hide overloaded methods and non-overloaded methods are still visible")]
        public void TestOverloadedMethodsAreHiddenCorrectly()
        {
            var d = new TestPropertiesDerived();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("d", d);
                scope.Exec($"result1=d.OverloadedMethod(4);" +
                    $"result2=d.OverloadedMethod(4, 4);" +
                    $"result3=d.OverloadedMethod(4, '4');");


                var result1 = scope.Get("result1").ToString();
                var result2 = scope.Get("result2").ToString();
                var result3 = scope.Get("result3").ToString();

                Assert.AreEqual("7", result1);
                Assert.AreEqual("8", result2);
                Assert.AreEqual("8", result3);
            }
        }

        [Test]
        [Description("Verify that the each class can call its indexer")]
        public void TestCanCallIndexerProperty()
        {
            var d = new TestPropertiesDerived();
            var b = new TestPropertiesBase();
            using (PyModule scope = Py.CreateScope())
            {
                scope.Set("b", b);
                scope.Set("d", d);
                scope.Exec($"result1=d[0];" +
                    $"result2=b[0]");

                var result1 = scope.Get("result1").ToString();
                var result2 = scope.Get("result2").ToString();

                Assert.AreEqual("FromDerived", result1);
                Assert.AreEqual("FromBase", result2);
            }
        }
    }
}
