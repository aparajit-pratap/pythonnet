// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

// Copied from https://github.com/IronLanguages/dlr/blob/main/Src/Microsoft.Dynamic/Actions/Calls/TypeInferer.cs and modified.
// TODO: Once we're sure we have what we need from DLR and don't need to compare to the original as much, clean this and IdDispenser up a bit.
//   - Remove duplicated arguments in GetInferedType.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Python.Runtime
{
    internal static class TypeInferer
    {
        private static ArgumentInputs EnsureInputs(Dictionary<Type, ArgumentInputs> dict, Type type)
        {
            if (!dict.TryGetValue(type, out ArgumentInputs res))
            {
                dict[type] = res = new ArgumentInputs(type);
            }
            return res;
        }

        public static bool TryMakeGenericMethod(MethodInfo methodInfo, Type[] argumentTypes, out MethodInfo genericMethod)
        {
            genericMethod = null;
            Type[] genericArguments = methodInfo.GetGenericArguments();

            Dictionary<Type, List<Type>> dependencies = GetDependencyMapping(genericArguments);
            Type[] genArgs = GetSortedGenericArguments(genericArguments, dependencies);
            Dictionary<Type, ArgumentInputs> inputs = GetArgumentToInputMapping(methodInfo, argumentTypes);

            // now process the inputs
            var binding = new Dictionary<Type, Type>();
            bool noMethod = false;
            foreach (Type t in genArgs)
            {
                if (!inputs.TryGetValue(t, out ArgumentInputs inps))
                {
                    continue;
                }

                Type bestType = inps.GetBestType(binding);
                if (bestType == null)
                {
                    // we conflict with possible constraints
                    noMethod = true;
                    break;
                }
            }

            if (!noMethod)
            {
                // finally build a new MethodCandidate for the generic method
                genArgs = GetGenericArgumentsForInferedMethod(genericArguments, binding);
                if (genArgs == null)
                {
                    // not all types were inferred
                    return false;
                }

                genericMethod = methodInfo.MakeGenericMethod(genArgs);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the generic arguments for method based upon the constraints discovered during
        /// type inference.  Returns null if not all generic arguments had their types inferred.
        /// </summary>
        private static Type[] GetGenericArgumentsForInferedMethod(Type[] genArgs, Dictionary<Type, Type> constraints)
        {
            for (int i = 0; i < genArgs.Length; i++)
            {
                if (!constraints.TryGetValue(genArgs[i], out Type newType))
                {
                    // we didn't discover any types for this type argument
                    return null;
                }
                genArgs[i] = newType;
            }
            return genArgs;
        }

        /// <summary>
        /// Gets the generic type arguments sorted so that the type arguments
        /// that are depended upon by other type arguments are sorted before
        /// their dependencies.
        /// </summary>
        private static Type[] GetSortedGenericArguments(Type[] genArgs, Dictionary<Type, List<Type>> dependencies)
        {
            // Sort the arguments based upon those dependencies
            Array.Sort(genArgs, (x, y) =>
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                bool isDependent = IsDependentConstraint(dependencies, x, y);
                if (isDependent)
                {
                    return 1;
                }

                isDependent = IsDependentConstraint(dependencies, y, x);
                if (isDependent)
                {
                    return -1;
                }

                int xhash = x.GetHashCode(), yhash = y.GetHashCode();
                if (xhash != yhash)
                {
                    return xhash - yhash;
                }

                long idDiff = IdDispenser.GetId(x) - IdDispenser.GetId(y);
                return idDiff > 0 ? 1 : -1;
            });


            return genArgs;
        }

        /// <summary>
        /// Checks to see if the x type parameter is dependent upon the y type parameter.
        /// </summary>
        private static bool IsDependentConstraint(Dictionary<Type, List<Type>> dependencies, Type x, Type y)
        {
            if (dependencies.TryGetValue(x, out List<Type> childDeps))
            {
                foreach (Type t in childDeps)
                {
                    if (t == y)
                    {
                        return true;
                    }

                    bool isDependent = IsDependentConstraint(dependencies, t, y);
                    if (isDependent)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Builds a mapping based upon generic parameter constraints between related generic
        /// parameters.  This is then used to sort the generic parameters so that we can process
        /// the least dependent parameters first.  For example given the method:
        /// 
        /// void Foo{T0, T1}(T0 x, T1 y) where T0 : T1 
        /// 
        /// We need to first infer the type information for T1 before we infer the type information
        /// for T0 so that we can ensure the constraints are correct.
        /// </summary>
        private static Dictionary<Type, List<Type>> GetDependencyMapping(Type[] genericArguments)
        {
            Dictionary<Type, List<Type>> dependencies = new Dictionary<Type, List<Type>>();

            // need to calculate any dependencies between parameters.
            foreach (Type genArg in genericArguments)
            {
                Type[] constraints = genArg.GetGenericParameterConstraints();
                foreach (Type t in constraints)
                {
                    if (t.IsGenericParameter)
                    {
                        AddDependency(dependencies, genArg, t);
                    }
                    else if (t.ContainsGenericParameters)
                    {
                        AddNestedDependencies(dependencies, genArg, t);
                    }
                }
            }
            return dependencies;
        }

        private static void AddNestedDependencies(Dictionary<Type, List<Type>> dependencies, Type genArg, Type t)
        {
            Type[] innerArgs = t.GetGenericArguments();
            foreach (Type innerArg in innerArgs)
            {
                if (innerArg.IsGenericParameter)
                {
                    AddDependency(dependencies, genArg, innerArg);
                }
                else if (innerArg.ContainsGenericParameters)
                {
                    AddNestedDependencies(dependencies, genArg, innerArg);
                }
            }
        }

        private static void AddDependency(Dictionary<Type, List<Type>> dependencies, Type genArg, Type t)
        {
            if (!dependencies.TryGetValue(genArg, out List<Type> deps))
            {
                dependencies[genArg] = deps = new List<Type>();
            }

            deps.Add(t);
        }

        /// <summary>
        /// Returns a mapping from generic type parameter to the input DMOs which map to it.
        /// </summary>
        private static Dictionary<Type/*!*/, ArgumentInputs/*!*/>/*!*/ GetArgumentToInputMapping(MethodInfo/*!*/ candidate, IList<Type/*!*/>/*!*/ argTypes)
        {
            Dictionary<Type, ArgumentInputs> inputs = new Dictionary<Type, ArgumentInputs>();

            ParameterInfo[] parameters = candidate.GetParameters();
            for (int curParam = 0; curParam < parameters.Length; curParam++)
            {
                ParameterInfo param = parameters[curParam];
                if (param.IsDefined(typeof(ParamArrayAttribute), false))
                {
                    AddOneInput(inputs, argTypes[curParam], param.ParameterType.GetElementType());
                }
                else
                {
                    AddOneInput(inputs, argTypes[curParam], param.ParameterType);
                }
            }

            return inputs;
        }

        /// <summary>
        /// Adds any additional ArgumentInputs entries for the given object and parameter type.
        /// </summary>
        private static void AddOneInput(Dictionary<Type, ArgumentInputs> inputs, Type argType, Type paramType)
        {
            if (paramType.ContainsGenericParameters)
            {
                List<Type> containedGenArgs = new List<Type>();
                CollectGenericParameters(paramType, containedGenArgs);

                foreach (Type type in containedGenArgs)
                {
                    EnsureInputs(inputs, type).AddInput(argType, paramType);
                }
            }
        }

        /// <summary>
        /// Walks the nested generic hierarchy to construct all of the generic parameters referred
        /// to by this type.  For example if getting the generic parameters for the x parameter on
        /// the method:
        /// 
        /// void Foo{T0, T1}(Dictionary{T0, T1} x);
        /// 
        /// We would add both typeof(T0) and typeof(T1) to the list of generic arguments.
        /// </summary>
        private static void CollectGenericParameters(Type type, List<Type> containedGenArgs)
        {
            if (type.IsGenericParameter)
            {
                if (!containedGenArgs.Contains(type))
                {
                    containedGenArgs.Add(type);
                }
            }
            else if (type.ContainsGenericParameters)
            {
                if (type.IsArray || type.IsByRef)
                {
                    CollectGenericParameters(type.GetElementType(), containedGenArgs);
                }
                else
                {
                    Type[] genArgs = type.GetGenericArguments();
                    for (int i = 0; i < genArgs.Length; i++)
                    {
                        CollectGenericParameters(genArgs[i], containedGenArgs);
                    }
                }
            }

        }

        /// <summary>
        /// Maps a single type parameter to the possible parameters and DynamicMetaObjects
        /// we can get inference from.  For example for the signature:
        /// 
        /// void Foo{T0, T1}(T0 x, T1 y, IList{T1} z);
        /// 
        /// We would have one ArgumentInput for T0 which holds onto the DMO providing the argument
        /// value for x.  We would also have one ArgumentInput for T1 which holds onto the 2 DMOs
        /// for y and z.  Associated with y would be a GenericParameterInferer and associated with
        /// z would be a ConstructedParameterInferer.
        /// </summary>
        private class ArgumentInputs
        {
            private readonly List<Type>/*!*/ _parameterTypes = new List<Type>();
            private readonly List<Type>/*!*/ _argTypes = new List<Type>();
            private readonly Type/*!*/ _genericParam;

            public ArgumentInputs(Type/*!*/ genericParam)
            {
                Debug.Assert(genericParam != null);
                Debug.Assert(genericParam.IsGenericParameter);
                _genericParam = genericParam;
            }

            public void AddInput(Type argType, Type/*!*/ parameterType)
            {
                _parameterTypes.Add(parameterType);
                _argTypes.Add(argType);
            }

            public Type GetBestType(Dictionary<Type, Type>/*!*/ binding)
            {
                Type curType = null;

                for (int i = 0; i < _parameterTypes.Count; i++)
                {
                    Type nextType = GetInferedType(_genericParam, _parameterTypes[i], _argTypes[i], _argTypes[i], binding);

                    if (nextType == null)
                    {
                        // no mapping available
                        return null;
                    }

                    if (curType == null || curType.IsAssignableFrom(nextType))
                    {
                        curType = nextType;
                    }
                    else if (!nextType.IsAssignableFrom(curType))
                    {
                        // inconsistent constraint.
                        return null;
                    }
                    else
                    {
                        curType = nextType;
                    }
                }

                return curType;
            }
        }

        /// <summary>
        /// Provides generic type inference for a single parameter.
        /// </summary>
        /// <remarks>
        /// For example: 
        ///   M{T}(T x)
        ///   M{T}(IList{T} x)
        ///   M{T}(ref T x)
        ///   M{T}(T[] x)
        ///   M{T}(ref Dictionary{T,T}[] x)
        /// </remarks>
        public static Type GetInferedType(Type/*!*/ genericParameter, Type/*!*/ parameterType, Type/*!*/ inputType, Type argType, Dictionary<Type, Type>/*!*/ binding)
        {
            Debug.Assert(genericParameter.IsGenericParameter);

            if (parameterType.IsGenericParameter)
            {
                if (inputType != null)
                {
                    binding[genericParameter] = inputType;
                    if (ConstraintsViolated(inputType, genericParameter, binding))
                    {
                        return null;
                    }
                }

                return inputType;
            }

            if (parameterType.IsInterface)
            {
                return GetInferedTypeForInterface(genericParameter, parameterType, inputType, binding);
            }

            if (parameterType.IsArray)
            {
                return binding[genericParameter] = MatchGenericParameter(genericParameter, argType, parameterType, binding);
            }

            if (parameterType.IsByRef)
            {
                if (IsStrongBox(argType))
                {
                    argType = argType.GetGenericArguments()[0];
                }
                return binding[genericParameter] = MatchGenericParameter(genericParameter, argType, parameterType.GetElementType(), binding);
            }

            // see if we're anywhere in our base class hierarchy
            Type genType = parameterType.GetGenericTypeDefinition();
            while (argType != typeof(object))
            {
                if (argType.IsGenericType && argType.GetGenericTypeDefinition() == genType)
                {
                    // TODO: Merge w/ the interface logic?
                    return binding[genericParameter] = MatchGenericParameter(genericParameter, argType, parameterType, binding);
                }
                argType = argType.BaseType;
            }

            return null;
        }

        //
        // The argument can implement multiple instantiations of the same generic interface definition, e.g.
        // ArgType : I<C<X>>, I<D<Y>>
        // ParamType == I<C<T>>
        //
        // Unless X == Y we can't infer T.
        //
        private static Type GetInferedTypeForInterface(Type/*!*/ genericParameter, Type/*!*/ interfaceType, Type inputType, Dictionary<Type, Type>/*!*/ binding)
        {
            Debug.Assert(interfaceType.IsInterface);

            Type match = null;
            Type genTypeDef = interfaceType.GetGenericTypeDefinition();

            var interfacesToCheck = new List<Type>(inputType.GetInterfaces());
            if (inputType.IsInterface)
            {
                interfacesToCheck.Add(inputType);
            }

            foreach (Type ifaceType in interfacesToCheck)
            {
                if (ifaceType.IsGenericType && ifaceType.GetGenericTypeDefinition() == genTypeDef)
                {
                    if (!MatchGenericParameter(genericParameter, ifaceType, interfaceType, binding, ref match))
                    {
                        return null;
                    }
                }
            }

            binding[genericParameter] = match;
            return match;
        }

        /// <summary>
        /// Checks if the constraints are violated by the given input for the specified generic method parameter.
        /// 
        /// This method must be supplied with a mapping for any dependent generic method type parameters which
        /// this one can be constrained to.  For example for the signature "void Foo{T0, T1}(T0 x, T1 y) where T0 : T1".
        /// we cannot know if the constraints are violated unless we know what we have calculated T1 to be.
        /// </summary>
        private static bool ConstraintsViolated(Type inputType, Type genericMethodParameterType, Dictionary<Type, Type> binding)
        {
            return ConstraintsViolated(genericMethodParameterType, inputType, binding, false);
        }

        private static Type MatchGenericParameter(Type genericParameter, Type closedType, Type openType, Dictionary<Type, Type> binding)
        {
            Type match = null;
            return MatchGenericParameter(genericParameter, closedType, openType, binding, ref match) ? match : null;
        }

        /// <summary>
        /// Finds all occurences of <c>genericParameter</c> in <c>openType</c> and the corresponding concrete types in <c>closedType</c>.
        /// Returns true iff all occurences of the generic parameter in the open type correspond to the same concrete type in the closed type 
        /// and this type satisfies given <c>constraints</c>. Returns the concrete type in <c>match</c> if so.
        /// </summary>
        private static bool MatchGenericParameter(Type genericParameter, Type closedType, Type openType, Dictionary<Type, Type> binding, ref Type match)
        {
            Type m = match;

            bool result = BindGenericParameters(openType, closedType, (parameter, type) =>
            {
                if (parameter == genericParameter)
                {
                    if (m != null)
                    {
                        return m == type;
                    }

                    if (ConstraintsViolated(type, genericParameter, binding))
                    {
                        return false;
                    }

                    m = type;
                }

                return true;
            });

            match = m;
            return result;
        }

        public static bool IsStrongBox(Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(StrongBox<>);
        }

        internal static Dictionary<Type, Type> BindGenericParameters(Type/*!*/ openType, Type/*!*/ closedType, bool ignoreUnboundParameters)
        {
            var binding = new Dictionary<Type, Type>();
            BindGenericParameters(openType, closedType, (parameter, type) =>
            {
                if (binding.TryGetValue(parameter, out Type existing))
                {
                    return type == existing;
                }

                binding[parameter] = type;

                return true;
            });

            return ConstraintsViolated(binding, ignoreUnboundParameters) ? null : binding;
        }

        #region ReflectionUtils

        public static readonly Type[] EmptyTypes = Array.Empty<TypeInfo>();

        /// <summary>
        /// Binds occurances of generic parameters in <paramref name="openType"/> against corresponding types in <paramref name="closedType"/>.
        /// Invokes <paramref name="binder"/>(parameter, type) for each such binding.
        /// Returns false if the <paramref name="openType"/> is structurally different from <paramref name="closedType"/> or if the binder returns false.
        /// </summary>
        internal static bool BindGenericParameters(Type/*!*/ openType, Type/*!*/ closedType, Func<Type, Type, bool>/*!*/ binder)
        {
            if (openType.IsGenericParameter)
            {
                return binder(openType, closedType);
            }

            if (openType.IsArray)
            {
                if (!closedType.IsArray)
                {
                    return false;
                }
                return BindGenericParameters(openType.GetElementType(), closedType.GetElementType(), binder);
            }

            if (!openType.IsGenericType || !closedType.IsGenericType)
            {
                return openType == closedType;
            }

            if (openType.GetGenericTypeDefinition() != closedType.GetGenericTypeDefinition())
            {
                return false;
            }

            Type[] closedArgs = closedType.GetGenericArguments();
            Type[] openArgs = openType.GetGenericArguments();

            for (int i = 0; i < openArgs.Length; i++)
            {
                if (!BindGenericParameters(openArgs[i], closedArgs[i], binder))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool ConstraintsViolated(Dictionary<Type, Type>/*!*/ binding, bool ignoreUnboundParameters)
        {
            foreach (var entry in binding)
            {
                if (ConstraintsViolated(entry.Key, entry.Value, binding, ignoreUnboundParameters))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ConstraintsViolated(Type/*!*/ genericParameter, Type/*!*/ closedType, Dictionary<Type, Type>/*!*/ binding, bool ignoreUnboundParameters)
        {
            if ((genericParameter.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0 && closedType.IsValueType)
            {
                // value type to parameter type constrained as class
                return true;
            }

            if ((genericParameter.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 &&
                (!closedType.IsValueType || (closedType.IsGenericType && closedType.GetGenericTypeDefinition() == typeof(Nullable<>))))
            {
                // nullable<T> or class/interface to parameter type constrained as struct
                return true;
            }

            if ((genericParameter.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0 &&
                (!closedType.IsValueType && closedType.GetConstructor(EmptyTypes) == null))
            {
                // reference type w/o a default constructor to type constrianed as new()
                return true;
            }

            Type[] constraints = genericParameter.GetGenericParameterConstraints();
            for (int i = 0; i < constraints.Length; i++)
            {
                Type instantiation = InstantiateConstraint(constraints[i], binding);

                if (instantiation == null)
                {
                    if (ignoreUnboundParameters)
                    {
                        continue;
                    }

                    return true;
                }

                if (!instantiation.IsAssignableFrom(closedType))
                {
                    return true;
                }
            }

            return false;
        }

        internal static Type InstantiateConstraint(Type/*!*/ constraint, Dictionary<Type, Type>/*!*/ binding)
        {
            Debug.Assert(!constraint.IsArray && !constraint.IsByRef && !constraint.IsGenericTypeDefinition);
            if (!constraint.ContainsGenericParameters)
            {
                return constraint;
            }

            Type closedType;
            if (constraint.IsGenericParameter)
            {
                return binding.TryGetValue(constraint, out closedType) ? closedType : null;
            }

            Type[] args = constraint.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] = InstantiateConstraint(args[i], binding)) == null)
                {
                    return null;
                }
            }

            return constraint.GetGenericTypeDefinition().MakeGenericType(args);
        }

        #endregion
    }
}
