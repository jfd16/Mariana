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
        /// <param name="type">The type whose name is to be returned.</param>
        /// <returns>The full name of the given type. This includes its namespace, declared name and
        /// the names of the type parameters (for constructed generic types).</returns>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
        public static string getFullTypeName(Type type) {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsGenericType) {
                var typeParams = type.GetGenericArguments();
                var typeParamNames = new string[typeParams.Length];
                for (int i = 0; i < typeParams.Length; i++)
                    typeParamNames[i] = getFullTypeName(typeParams[i]);
                return type.GetGenericTypeDefinition().FullName + "<" + String.Join(", ", typeParamNames) + ">";
            }

            if (type.IsGenericParameter)
                return type.Name;  // FullName is null in this case.

            return type.FullName;
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
        ///
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
        /// <exception cref="ArgumentException">The signature of <paramref name="method"/> is not
        /// compatible with the delegate type <typeparamref name="T"/>, and <paramref name="throwOnFail"/>
        /// is true.</exception>
        public static T? makeDelegate<T>(
            MethodInfo method, object? target = null, bool throwOnFail = false) where T : Delegate
        {
            if (target == null)
                return (T?)Delegate.CreateDelegate(typeof(T), method, throwOnFail);

            return (T?)Delegate.CreateDelegate(typeof(T), target, method, throwOnFail);
        }

        /// <summary>
        /// Creates a delegate for the given dynamic method.
        /// </summary>
        /// <param name="method">The dynamic method for which to create the delegate.</param>
        /// <param name="target">The target object to which to bind the delegate, or null if the
        /// delegate must not be bound.</param>
        /// <typeparam name="T">The type of the delegate to create.</typeparam>
        /// <returns>The created delegate.</returns>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
        /// <exception cref="ArgumentException">The signature of <paramref name="method"/> is not
        /// compatible with the delegate type <typeparamref name="T"/>.</exception>
        public static T makeDelegate<T>(DynamicMethod method, object? target = null) where T : Delegate {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (target == null)
                return (T)method.CreateDelegate(typeof(T));

            return (T)method.CreateDelegate(typeof(T), target);
        }

        /// <summary>
        /// Extracts a <see cref="MemberInfo"/> from a lambda expression tree.
        /// </summary>
        ///
        /// <param name="expr">An expression tree. This must be a field access, property access,
        /// method call, delegate creation, object construction or user-defined conversion expression.</param>
        /// <param name="stripTypeArgs">Set to true to remove any generic type or method arguments from
        /// the member in <paramref name="expr"/> and return the generic type or method definition.</param>
        ///
        /// <typeparam name="TFunc">The type of the lambda function.</typeparam>
        /// <typeparam name="TMember">The subclass of <see cref="MemberInfo"/> for the type of the
        /// member (field, property, method or constructor) to return.</typeparam>
        ///
        /// <returns>The <see cref="MemberInfo"/> extracted from the expression.</returns>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="expr"/> is null.</exception>
        /// <exception cref="ArgumentException">No member of the type <typeparamref name="TMember"/>
        /// could be extracted from <paramref name="expr"/>.</exception>
        public static TMember getMemberFromExpr<TFunc, TMember>(Expression<TFunc> expr, bool stripTypeArgs = false)
            where TMember : MemberInfo
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));

            MemberInfo member = _getMemberFromExprInternal(expr.Body);

            if (stripTypeArgs) {
                if (!(member is PropertyInfo))
                    member = member.Module.ResolveMember(member.MetadataToken);
                else if (member.DeclaringType.IsConstructedGenericType)
                    member = member.DeclaringType.GetGenericTypeDefinition().GetProperty(member.Name);
            }

            var memberAsType = member as TMember;
            if (memberAsType == null)
                throw new ArgumentException("No member of type '" + typeof(TMember).Name + "' found in expression.", nameof(expr));

            return memberAsType;
        }

        private static MemberInfo _getMemberFromExprInternal(Expression expr) {
            var unaryExpr = expr as UnaryExpression;

            if (unaryExpr != null && unaryExpr.Method != null
                && unaryExpr.Operand.NodeType == ExpressionType.Parameter)
            {
                // For a user-defined conversion or operator on a parameter, return its method.
                return unaryExpr.Method;
            }

            while (unaryExpr != null && unaryExpr.NodeType == ExpressionType.Convert) {
                // Strip implicit conversions.
                expr = unaryExpr.Operand;
                unaryExpr = expr as UnaryExpression;
            }

            if (expr is MemberExpression memberExpr) {
                // Field or property access.
                return memberExpr.Member;
            }

            if (expr is MethodCallExpression methodExpr) {
                // Check for delegate creation expressions
                if (methodExpr.Object is ConstantExpression methodObjectConst
                    && methodObjectConst.Value is MethodInfo constMethodInfo)
                {
                    return constMethodInfo;
                }

                return methodExpr.Method;
            }

            if (expr is NewExpression newExpr)
                return newExpr.Constructor;

            if (expr is UnaryExpression unExpr && unExpr.Method != null)
                return unExpr.Method;

            if (expr is BinaryExpression binExpr && binExpr.Method != null)
                return binExpr.Method;

            throw new ArgumentException("No member found in expression.", nameof(expr));
        }
    }
}
