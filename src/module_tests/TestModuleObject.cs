using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    internal class TestModuleObject
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
        public void TestGetAttr()
        {
            PyDict locals = new PyDict();
            PythonEngine.Exec(@"
import clr
clr.setPreload(True)
import Python.Runtime
dict = dir(Python.Runtime)
values = []
keys = []
for key in dict:
  keys.append(key)
  values.append(getattr(Python.Runtime, key))
clr.setPreload(False)
", null, locals);

            // At this point dir/getattr consistency was tested
            // Assert attributes of different types are included
            using var keys = PyList.AsList(locals.GetItem("keys"));
            using var values = PyList.AsList(locals.GetItem("values"));
            var keyList = keys.ToList();
            var valueList = values.ToList();

            // class
            CollectionAssert.Contains(keyList, "PyObject");
            var index = keyList.IndexOf("PyObject");
            Assert.AreEqual(typeof(PyObject), valueList[index]);

            // nested namespace
            CollectionAssert.Contains(keyList, "Codecs");
            index = keyList.IndexOf("Codecs");
            Assert.AreEqual(typeof(ModuleObject), valueList[index].GetType());
            Assert.AreEqual("Python.Runtime.Codecs", ((ModuleObject)valueList[index]).moduleName);

            // custom module attribute
            CollectionAssert.Contains(keyList, "__class__");
            index = keyList.IndexOf("__class__");
            Assert.AreEqual(typeof(PyObject), valueList[index].GetType());
            StringAssert.Contains("ModuleObject", valueList[index].ToString());

            // inherited/native attribute
            CollectionAssert.Contains(keyList, "__delattr__");
            index = keyList.IndexOf("__delattr__");
            Assert.AreEqual(typeof(PyObject), valueList[index].GetType());
            StringAssert.Contains("delattr", valueList[index].ToString());
        }

        [Test]
        public void TestNestedNamespacePreload()
        {
            PyDict locals = new PyDict();
            PythonEngine.Exec(@"
import clr
clr.setPreload(True)
import Python.Runtime
dict = dir(getattr(Python.Runtime, 'Codecs'))
keys = []
for key in dict:
  keys.append(key)
clr.setPreload(False)
", null, locals);

            using var keys = PyList.AsList(locals.GetItem("keys"));
            var keyList = keys.ToList();
            CollectionAssert.Contains(keyList, "DecoderGroup");
        }
    }
}
