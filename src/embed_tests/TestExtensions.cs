using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    internal class TestExtensions
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
        public void TestImportExtensionsSimple()
        {
            PyDict locals = new PyDict();
            PythonEngine.Exec(@"
import clr
clr.AddReference('Python.EmbeddingTest')
import Python.EmbeddingTest
clr.ImportExtensions(Python.EmbeddingTest)
from Python.EmbeddingTest import MyCalculator
calc = MyCalculator()
calc.Sum(3)
calc.Sub(1)
calc.Sub(1)
result1 = calc.Value
calc.Reset()
result2 = calc.Value
"
            , locals: locals);

            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result1")));
            Assert.AreEqual(1, PyInt.AsInt(locals.GetItem("result1")).ToInt32());
            Assert.IsTrue(PyInt.IsIntType(locals.GetItem("result2")));
            Assert.AreEqual(0, PyInt.AsInt(locals.GetItem("result2")).ToInt32());
        }

        [Test]
        public void TestImportExtensionsGenerics()
        {
            PyDict locals = new PyDict();
            PythonEngine.Exec(@"
import clr
from System import String
from System.Collections.Generic import List
list = List[String]()
list.Add('hello')
list.Add('beautiful')
list.Add('world')
clr.AddReference('Python.EmbeddingTest')
import Python.EmbeddingTest
clr.ImportExtensions(Python.EmbeddingTest)
result1 = list.Concat(' ')
list.GetMiddleItem[String]()
result2 = list.GetMiddleItem()
", locals: locals);

            Assert.IsTrue(PyString.IsStringType(locals.GetItem("result1")));
            Assert.AreEqual("hello beautiful world", new PyString(locals.GetItem("result1")).ToString());
            Assert.IsTrue(PyString.IsStringType(locals.GetItem("result2")));
            Assert.AreEqual("beautiful", new PyString(locals.GetItem("result2")).ToString());
        }

        [Test]
        public void TestImportExtensionsGenericClass()
        {
            PyDict locals = new PyDict();
            PythonEngine.Exec(@"
import clr
from System import Int32
from System.Collections.Generic import List
list = List[Int32]()
list.Add(1)
list.Add(2)
list.Add(3)
clr.AddReference('Python.EmbeddingTest')
import Python.EmbeddingTest
clr.ImportExtensions(Python.EmbeddingTest)
from Python.EmbeddingTest import GenericCalculator
calc = GenericCalculator[List[Int32]](list)
result1 = calc.GetMiddleItem()
result2 = calc.GetFirstItem()
result3 = calc.Append(list)
", locals: locals);

            Assert.AreEqual(2, new PyInt(locals.GetItem("result1")).ToInt32());
            Assert.AreEqual(1, new PyInt(locals.GetItem("result2")).ToInt32());
            Assert.That(PyList.AsList(locals.GetItem("result3")).ToList(), Has.Count.EqualTo(6));
        }
    }

    public class MyCalculator : IIntegerCalculator
    {
        public int Value { get; set; }

        public MyCalculator()
        {
            Value = 0;
        }

        public void Sum(int value)
        {
            Value += value;
        }
    }

    public interface IIntegerCalculator
    {
        int Value { get; set; }
    }

    public static class MyCalculatorExtensions
    {
        public static void Sub(this MyCalculator calc, int value)
        {
            calc.Value -= value;
        }

        public static void Reset(this IIntegerCalculator calc)
        {
            calc.Value = 0;
        }
    }

    public static class MyListExtensions
    {
        public static string Concat(this IEnumerable<string> list, string separator)
        {
            StringBuilder result = new StringBuilder();
            foreach (var item in list)
            {
                result.Append(item);
                result.Append(separator);
            }

            if (list.Any())
            {
                result.Length -= separator.Length;
            }
            return result.ToString();
        }

        public static T GetMiddleItem<T>(this IList<T> list)
        {
            var middle = list.Count / 2;
            return middle < list.Count ? list[middle] : default;
        }
    }

    public class GenericCalculator<T>
         where T : IEnumerable<int>
    {
        public GenericCalculator(T values)
        {
            Values = values;
        }

        public T Values { get; set; }

        public int Sum()
        {
            return Values.Sum();
        }
    }

    public static class MyGenericCalculatorExtensions
    {
        public static void Test()
        {
            var calc = new GenericCalculator<List<int>>(new List<int>());

            calc.Append(new List<int> { 1, 2, 3 });
        }

        public static int GetMiddleItem<T>(this GenericCalculator<T> list)
            where T : IEnumerable<int>
        {
            int middle = list.Values.Count() / 2;
            return list.Values.ElementAt(middle);
        }

        public static int GetFirstItem(this GenericCalculator<List<int>> list)
        {
            return list.Values[0];
        }

        public static List<int> Append<T>(this GenericCalculator<List<int>> list, T newItems)
            where T : IEnumerable<int>
        {
            list.Values.AddRange(newItems);
            return list.Values;
        }

        public static List<int> Append(this GenericCalculator<List<int>> list, List<int> newItems, int hi)
        {
            list.Values.AddRange(newItems);
            return list.Values;
        }
    }
}
