using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        private static readonly HashSet<ClassBase> extensionTypes = new();
        /// <summary>
        /// Caches method objects by name and concrete instance type
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, MethodObject>> cache = new();

        /// <summary>
        /// Registers an extension type, making its extension methods available.
        /// For this to happen the internal cache is purged.
        /// </summary>
        /// <param name="classBase">Class to be registered</param>
        internal static void RegisterExtensionType(ClassBase classBase)
        {
            cache.Remove(classBase.type.Value);
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
        /// <param name="methodObject">The extension method object, if found.</param>
        /// <returns>True if an extension method is found.</returns>
        internal static bool TryGetExtensionMethodObject(Type type, string name, out MethodObject methodObject)
        {
            if (TryGetFromCache(type, name, out methodObject))
            {
                return true;
            }            

            var extensionMethods = GetExtensionMethods(type, name).ToArray();
            if (extensionMethods.Length > 0)
            {
                methodObject = new MethodObject(type, name, extensionMethods);
                CacheMethodObject(type, name, methodObject);
                return true;
            }

            return false;
        }

        private static bool TryGetFromCache(Type type, string name, out MethodObject? methodObject)
        {
            methodObject = null;
            bool result = cache.TryGetValue(type, out var nameDict) && nameDict.TryGetValue(name, out methodObject);

            return result;
        }

        private static void CacheMethodObject(Type type, string name, MethodObject methodObj)
        {
            if (!cache.TryGetValue(type, out var methodInfo))
            {
                methodInfo = new Dictionary<string, MethodObject>();
                cache[type] = methodInfo;
            }

            methodInfo[name] = methodObj;
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
