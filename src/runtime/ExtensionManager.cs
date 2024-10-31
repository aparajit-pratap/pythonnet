using System;
using System.Collections.Generic;
using System.Linq;
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
                        if (extendedType != null && InheritsOrImplements(type, extendedType))
                            result.Add(method);
                    }
                }
            }

            return result;
        }

        private static bool InheritsOrImplements(Type child, Type parent)
        {
            parent = GetFullTypeDefinition(parent);

            var currentChild = GetFullTypeDefinition(child);

            while (currentChild != typeof(object))
            {
                if (parent == currentChild || HasAnyInterfaces(parent, currentChild))
                    return true;

                currentChild = currentChild.BaseType != null
                               && currentChild.BaseType.IsGenericType
                                   ? currentChild.BaseType.GetGenericTypeDefinition()
                                   : currentChild.BaseType;

                if (currentChild == null)
                    return false;
            }
            return false;
        }

        private static bool HasAnyInterfaces(Type parent, Type child)
        {
            return child.GetInterfaces()
                .Any(childInterface =>
                {
                    var currentInterface = childInterface.IsGenericType
                        ? childInterface.GetGenericTypeDefinition()
                        : childInterface;

                    return currentInterface == parent;
                });
        }

        private static Type GetFullTypeDefinition(Type type)
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        }
    }
}
