using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Mariana.Common {

    /// <summary>
    /// Utility functions for reflection.
    /// </summary>
    public static class ReflectUtil {

        /// <summary>
        /// Gets the full name of the given type, including type parameters of generic types.
        /// </summary>
        /// <param name="t">The type whose name is to be returned.</param>
        /// <returns>The full name of the given type. This includes its namespace, declared name and
        /// the names of the type parameters (for constructed generic types).</returns>
        public static string getFullTypeName(Type t) {
            if (t.IsGenericType) {
                var typeParams = t.GetGenericArguments();
                var typeParamNames = new string[typeParams.Length];
                for (int i = 0; i < typeParams.Length; i++)
                    typeParamNames[i] = getFullTypeName(typeParams[i]);
                return t.GetGenericTypeDefinition().FullName + "<" + String.Join(", ", typeParamNames) + ">";
            }

            if (t.IsGenericParameter)
                return t.Name;  // FullName is null in this case.

            return t.FullName;
        }

        /// <summary>
        /// Creates a delegate for the given method.
        /// </summary>
        ///
        /// <param name="method">The method for which to create the delegate.</param>
        /// <param name="target">The target object to which to bind the delegate, if
        /// <paramref name="method"/> is an instance method.</param>
        /// <param name="throwOnFail">Set this to true to throw an exception if the delegate could not
        /// be created.</param>
        ///
        /// <typeparam name="T">The type of the delegate to create.</typeparam>
        /// <returns>The created delegate, or null if the delegate could not be created (and
        /// <paramref name="throwOnFail"/> is false).</returns>
        public static T makeDelegate<T>(
            MethodInfo method, object target = null, bool throwOnFail = false) where T : Delegate
        {
            return (T)Delegate.CreateDelegate(typeof(T), target, method, throwOnFail);
        }

        /// <summary>
        /// Creates a delegate for the given dynamic method.
        /// </summary>
        /// <param name="method">The dynamic method for which to create the delegate.</param>
        /// <param name="target">The target object to which to bind the delegate, or null if the
        /// delegate must not be bound.</param>
        /// <typeparam name="T">The type of the delegate to create.</typeparam>
        /// <returns>The created delegate.</returns>
        public static T makeDelegate<T>(DynamicMethod method, object target = null) where T : Delegate {
            if (target == null)
                return (T)(object)method.CreateDelegate(typeof(T));
            return (T)(object)method.CreateDelegate(typeof(T), target);
        }

        /// <summary>
        /// Extracts a <see cref="MemberInfo"/> from a lambda expression tree.
        /// </summary>
        /// <param name="expr">An expression tree. This must be a field access, property access,
        /// method call, delegate creation, object construction or user-defined conversion expression.</param>
        /// <param name="stripTypeArgs">Set to true to remove any generic type or method arguments from
        /// the member in <paramref name="expr"/> and return the generic type or method definition.</param>
        /// <typeparam name="TFunc">The type of the lambda function.</typeparam>
        /// <typeparam name="TMember">The subclass of <see cref="MemberInfo"/> for the type of the
        /// member (field, property, method or constructor) to return.</typeparam>
        /// <returns>The <see cref="MemberInfo"/> extracted from the expression.</returns>
        public static TMember getMemberFromExpr<TFunc, TMember>(Expression<TFunc> expr, bool stripTypeArgs = false)
            where TMember : MemberInfo
        {
            MemberInfo member = _getMemberFromExprInternal(expr.Body);
            if (stripTypeArgs)
                member = member.Module.ResolveMember(member.MetadataToken);

            return (TMember)member;
        }

        private static MemberInfo _getMemberFromExprInternal(Expression expr) {
            UnaryExpression unaryExpr = expr as UnaryExpression;

            if (unaryExpr != null && unaryExpr.NodeType == ExpressionType.Convert
                && unaryExpr.Method != null && unaryExpr.Operand.NodeType == ExpressionType.Parameter)
            {
                // For a user-defined conversion on a parameter, return that conversion method.
                return unaryExpr.Method;
            }

            while (unaryExpr != null) {
                // Strip implicit conversions.
                expr = unaryExpr.Operand;
                unaryExpr = expr as UnaryExpression;
            }

            if (expr is MemberExpression memberExpr) {
                // Field or property access.
                return memberExpr.Member;
            }

            if (expr is MethodCallExpression methodExpr) {
                // Check for constants of type MethodInfo in the arguments.
                // (This is used to detect delegate creation expressions and
                // return the method from which the delegate is being created)
                ConstantExpression constantExpr;

                constantExpr = methodExpr.Object as ConstantExpression;
                if (constantExpr != null) {
                    MethodInfo member = constantExpr.Value as MethodInfo;
                    if (member != null)
                        return member;
                }

                for (int i = 0, n = methodExpr.Arguments.Count; i < n; i++) {
                    constantExpr = methodExpr.Arguments[i] as ConstantExpression;
                    if (constantExpr != null) {
                        MethodInfo member = constantExpr.Value as MethodInfo;
                        if (member != null)
                            return member;
                    }
                }

                return methodExpr.Method;
            }

            if (expr is NewExpression newExpr)
                return newExpr.Constructor;

            if (expr is BinaryExpression binExpr && binExpr.Method != null)
                return binExpr.Method;

            throw new ArgumentException("No member found in expression.");
        }
    }
}
