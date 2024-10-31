using System.Collections;
using System.Collections.Generic;

namespace Python.Test
{
    /// <summary>
    /// Supports CLR Exception unit tests.
    /// </summary>
    public class NumberList : IEnumerable<int>
    {
        private readonly List<int> list = new();

        public NumberList(){
            list.Add(1);
            list.Add(2);
            list.Add(3);
        }
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }
}
