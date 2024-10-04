using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    internal class TestInterface
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
        public void TestInterfaceExtensions()
        {
            PyDict locals = new();
            PythonEngine.Exec(@"
import clr
clr.AddReference('Python.EmbeddingTest')
import Python.EmbeddingTest
clr.ImportExtensions(Python.EmbeddingTest)
from Python.EmbeddingTest import Concrete
a = Concrete(10)
result1 = a.TripledInterface().Property
result2 = a.TripledConcrete().TripledConcrete().Property
result3 = a.TripledInterface().TripledInterface().Property
"
            , locals: locals);

            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result1")));
            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result2")));
            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result3")));

            Assert.AreEqual(30, PyInt.AsInt(locals.GetItem("result1")).ToInt32());
            Assert.AreEqual(90, PyInt.AsInt(locals.GetItem("result2")).ToInt32());
            Assert.AreEqual(90, PyInt.AsInt(locals.GetItem("result3")).ToInt32());
        }
    }

    public class Concrete : IInterface
    {
        public int Property { get; set; }

        public int Doubled()
        {
            return Property * 2;
        }

        public Concrete(int value)
        {
            Property = value;
        }

        public static IInterface MakeAsInterface(int value)
        {
            return new Concrete(value);
        }
    }

    public interface IBase
    {
        int Doubled();

        int Property { get; set; }
    }

    public interface IInterface : IBase
    {
    }

    public static class IBaseExtension
    {
        public static IBase TripledInterface(this IBase ibase)
        {
            var newBase = new Concrete(ibase.Property);

            newBase.Property *= 3;

            return newBase;
        }

        public static Concrete TripledConcrete(this Concrete ibase)
        {
            return (Concrete)TripledInterface(ibase);
        }
    }
}
