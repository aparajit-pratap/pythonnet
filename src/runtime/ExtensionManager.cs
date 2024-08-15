using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Python.Runtime
{
    /// <summary>
    /// Provides extension method related functionality
    /// </summary>
    internal static class ExtensionManager
    {
        /// <summary>
        /// Keeps track of currently imported extension types
        /// </summary>
        private static HashSet<ClassBase> extensionTypes = new HashSet<ClassBase>();
        /// <summary>
        /// Caches method objects by name and concrete instance type
        /// </summary>
        private static Dictionary<string, Dictionary<Type, MethodObject>> cache = new Dictionary<string, Dictionary<Type, MethodObject>>();

        /// <summary>
        /// Registers an extension type, making its extension methods available.
        /// For this to happen the internal cache is purged.
        /// </summary>
        /// <param name="classBase">Class to be registered</param>
        internal static void RegisterExtensionType(ClassBase classBase)
        {
            if (extensionTypes.Contains(classBase))
            {
                return;
            }

            extensionTypes.Add(classBase);
        }

        /// <summary>
        /// Checks whether a class is an extension type.
        /// </summary>
        /// <param name="classBase">Class to check</param>
        /// <returns>True if it is an extension type, false if not</returns>
        internal static bool IsExtensionType(ClassBase classBase)
        {
            return classBase.type.Value.IsDefined(typeof(ExtensionAttribute), false);
        }

        /// <summary>
        /// Gets an extension method object, given the type of the instance being called and the
        /// method name. The method object is obtained from cache if possible.
        /// </summary>
        /// <param name="type">Type of the instance being called</param>
        /// <param name="name">Name of the method being called</param>
        /// <returns>Method object for an extension or null if there isn't one</returns>
        internal static MethodObject GetExtensionMethodObject(Type type, string name)
        {
            var extensionMethods = GetExtensionMethods(type, name).ToArray();
            if (extensionMethods.Length > 0)
            {
                return new MethodObject(type, name, extensionMethods);
            }

            return null;
        }

        private static List<MethodInfo> GetExtensionMethods(Type type, string name)
        {
            var result = new List<MethodInfo>();
            foreach (var extensionType in extensionTypes)
            {
                foreach (var method in extensionType.type.Value.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (method.Name == name && method.IsDefined(typeof(ExtensionAttribute), false))
                    {
                        var parameters = method.GetParameters();
                        var extendedType = parameters.Length > 0 ? parameters[0].ParameterType : null;
                        if (extendedType != null && CanApplyExtension(type, extendedType))
                            result.Add(method);
                    }
                }
            }

            return result;
        }

        private static bool CanApplyExtension(Type type, Type extendedType)
        {
            // Open generic types need to be closed before checking assignability
            if (extendedType.ContainsGenericParameters)
            {
                // TODO: This is still a long way from working in all cases.
                // It probably makes sense to copy TypeInferer and some related classes entirely:
                // https://github.com/IronLanguages/dlr/blob/master/Src/Microsoft.Dynamic/Actions/Calls/TypeInferer.cs

                if (!type.IsGenericType)
                {
                    return false;
                }

                var extendedTypeArgs = extendedType.GetGenericArguments();
                var typeArgs = type.GetGenericArguments();
                if (extendedTypeArgs.Length != typeArgs.Length)
                {
                    return false;
                }

                var types = new List<Type>();
                for (int i = 0; i < extendedTypeArgs.Length; i++)
                {
                    if (extendedTypeArgs[i].IsGenericParameter)
                    {
                        types.Add(typeArgs[i]);
                    }
                }

                if (!extendedType.IsGenericTypeDefinition)
                {
                    extendedType = extendedType.GetGenericTypeDefinition();
                }

                extendedType = extendedType.MakeGenericType(types.ToArray());
            }

            return extendedType.IsAssignableFrom(type);
        }
    }
}
