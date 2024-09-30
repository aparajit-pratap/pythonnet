using System;

namespace Python.Runtime
{
    /// <summary>
    /// Extension methods to connect the Python "with" statement with .Net's <see cref="IDisposable"/>.
    /// </summary>
    internal static class WithExtensions
    {
        /// <summary>
        /// This method is called when the "with" statement is entered.
        /// </summary>
        public static IDisposable OnEnter(this IDisposable o)
        {
            return o;
        }

        /// <summary>
        /// This method is called when the "with" statement is exited.
        /// </summary>
        public static bool OnExit(this IDisposable o, PyObject et, PyObject ev, PyObject tb)
        {
            o.Dispose();
            // return false so that if there are any exceptions arising from the body
            // of the "with" statement in Python, it will be rethrown and bubble up.
            // returning true will suppress the exceptions if any.
            return false;
        }
    }
}
