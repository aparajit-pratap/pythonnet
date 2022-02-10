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
            var typedDictionary = pyDict.ToDictionary<IDictionary<string, int>>();
            var expected = new Dictionary<string, int>
            {
                { "one", 1 }
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
