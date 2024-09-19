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
        public void TestInheritedInterfaceVisibility()
        {
            PyDict locals = new PyDict();
            PythonEngine.Exec(@"
import clr
clr.AddReference('Python.EmbeddingTest')
from Python.EmbeddingTest import Concrete
a = Concrete(10)
b = Concrete.MakeAsInterface(10)
result1a = a.Doubled()
result1b = b.Doubled()
result2a = a.Property
result2b = b.Property
"
            , locals: locals);

            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result1a")));
            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result1b")));
            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result2a")));
            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result2b")));

            Assert.AreEqual(20, PyInt.AsInt(locals.GetItem("result1a")).ToInt32());
            Assert.AreEqual(20, PyInt.AsInt(locals.GetItem("result1b")).ToInt32());
            Assert.AreEqual(10, PyInt.AsInt(locals.GetItem("result2a")).ToInt32());
            Assert.AreEqual(10, PyInt.AsInt(locals.GetItem("result2b")).ToInt32());
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
}
