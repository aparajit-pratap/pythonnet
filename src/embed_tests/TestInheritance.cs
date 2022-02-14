using System;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPropertiesBase
    {
        public string PropertyA { get { return "PropertyA"; } set { } }
        public double PropertyB { get { return 1.1; } }
        public int PropertyC { set { } }

        public string this[int flag]
        {
            // using get accessor 
            get
            {
                return "FromBase";
            }

        }
    }

    public class TestPropertiesDerived : TestPropertiesBase
    {
        public new int PropertyA { get { return 0; } set { } }
        public int PropertyD { get { return 0; } }

        public new string this[int flag]
        {
            // using get accessor 
            get
            {
                return "FromDerived";
            }
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
                scope.Exec($"result=b.PropertyA");
                var result = scope.Get("result").ToString();
                Assert.AreEqual(result, "PropertyA");
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
                    scope.Exec($"result=b.PropertyD");
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
                scope.Exec($"result=d.PropertyB;" +
                    $"d.PropertyC=4;");

                var result = scope.Get("result").ToString();
                Assert.AreEqual(result, "1.1");
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
                    scope.Exec($"d.PropertyA=test");
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
                scope.Exec($"result1=d.PropertyA;" +
                    $"result2=d.PropertyD;");

                var result1 = scope.Get("result1").ToString();
                var result2 = scope.Get("result2").ToString();

                Assert.AreEqual(result1, "0");
                Assert.AreEqual(result2, "0");
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

                Assert.AreEqual(result1, "FromDerived");
                Assert.AreEqual(result2, "FromBase");
            }
        }
    }
}
