using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    public class TestPyDict
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
        public void TestToDictionary()
        {
            var pyObj = PythonEngine.Eval("{ 'one': 1 }");
            var pyDict = new PyDict(pyObj);
            var untypedDictionary = pyDict.ToDictionary();
            var typedDictionary = pyDict.ToDictionary<IDictionary<string, PyInt>>();
            var expected = new Dictionary<string, PyInt>
            {
                { "one", new PyInt(1) }
            };
            DictionaryAssert(expected, untypedDictionary);
            DictionaryAssert(expected, (IDictionary)typedDictionary);
        }

        private void DictionaryAssert(IDictionary expected, IDictionary actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            foreach (var key in expected.Keys)
            {
                Assert.AreEqual(expected[key], actual[key]);
            }
        }
    }
}
