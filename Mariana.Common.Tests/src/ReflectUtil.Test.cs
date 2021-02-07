using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;

namespace TEST {
    namespace A {
        namespace B {
            internal class C1 {}
            internal class C2<T1> {}
            internal class C3<T1, T2> {}

            internal class C4 {
                public C4() {}
                public C4(int x) {}
                public C4(string x) {}

                public int a = 0;
                public static int b = 0;
                public int c => 0;
                public static int d => 0;

                public int foo(int x) => 0;

                public int bar(int x, int y) => 0;
                public int bar(int x, string y) => 0;

                public static int bar(C1 x) => 0;

                public static implicit operator C4(int x) => null;
                public static implicit operator C4(string x) => null;
                public static implicit operator int(C4 x) => 0;
                public static implicit operator string(C4 x) => null;

                public static C4 operator +(C4 x, C4 y) => null;
                public static C4 operator ~(C4 x) => null;
            }

            internal class C5 {
                public void foo<T>() {}
            }

            internal class C6<T> {
                public T x = default(T);
                public T y => default(T);
                public void foo() {}
                public void bar<U>() {}
            }
        }
    }

    internal class X {}
    internal class Y {}
}

namespace Mariana.Common.Tests {

    public sealed class ReflectUtilTest {

        public static IEnumerable<object[]> getFullTypeNameTest_data = new (Type, string)[] {
            (typeof(TEST.A.B.C1), "TEST.A.B.C1"),

            (typeof(TEST.A.B.C2<>), "TEST.A.B.C2`1<T1>"),
            (typeof(TEST.A.B.C2<TEST.A.B.C1>), "TEST.A.B.C2`1<TEST.A.B.C1>"),
            (typeof(TEST.A.B.C2<TEST.A.B.C2<TEST.A.B.C2<TEST.A.B.C1>>>), "TEST.A.B.C2`1<TEST.A.B.C2`1<TEST.A.B.C2`1<TEST.A.B.C1>>>"),

            (typeof(TEST.A.B.C3<,>), "TEST.A.B.C3`2<T1, T2>"),
            (typeof(TEST.A.B.C3<TEST.X, TEST.X>), "TEST.A.B.C3`2<TEST.X, TEST.X>"),
            (typeof(TEST.A.B.C3<TEST.X, TEST.Y>), "TEST.A.B.C3`2<TEST.X, TEST.Y>"),
            (typeof(TEST.A.B.C3<TEST.A.B.C2<TEST.X>, TEST.X>), "TEST.A.B.C3`2<TEST.A.B.C2`1<TEST.X>, TEST.X>"),

            (
                typeof(TEST.A.B.C3<TEST.A.B.C2<TEST.X>, TEST.A.B.C3<TEST.Y, TEST.A.B.C3<TEST.A.B.C2<TEST.X>, TEST.A.B.C2<TEST.X>>>>),
                "TEST.A.B.C3`2<TEST.A.B.C2`1<TEST.X>, TEST.A.B.C3`2<TEST.Y, TEST.A.B.C3`2<TEST.A.B.C2`1<TEST.X>, TEST.A.B.C2`1<TEST.X>>>>"
            ),

            (typeof(TEST.A.B.C2<>).GetGenericArguments()[0], "T1"),
            (typeof(TEST.A.B.C3<,>).GetGenericArguments()[1], "T2"),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(getFullTypeNameTest_data))]
        public void getFullTypeNameTest(Type type, string expectedName) {
            Assert.Equal(expectedName, ReflectUtil.getFullTypeName(type));
        }

        [Fact]
        public void getFullTypeNameTest_invalidArguments() {
            Assert.Throws<ArgumentNullException>(() => ReflectUtil.getFullTypeName(null));
        }

        public static IEnumerable<object[]> makeDelegateTest_data() {
            var bar1 = typeof(TEST.A.B.C4).GetMethod(nameof(TEST.A.B.C4.bar), new[] {typeof(int), typeof(int)});
            var bar2 = typeof(TEST.A.B.C4).GetMethod(nameof(TEST.A.B.C4.bar), BindingFlags.Public | BindingFlags.Static);
            var c1 = new TEST.A.B.C1();
            var c4 = new TEST.A.B.C4();

            return new (MethodInfo, Type, object, bool)[] {
                    (bar1, typeof(Func<int, int, int>), c4, true),
                    (bar1, typeof(Func<TEST.A.B.C4, int, int, int>), null, true),
                    (bar2, typeof(Func<int>), c1, true),
                    (bar2, typeof(Func<TEST.A.B.C1, int>), null, true),

                    (bar1, typeof(Action<int, int>), c4, false),
                    (bar1, typeof(Func<int, int>), c4, false),
                    (bar1, typeof(Func<int, int, int, int>), c4, false),
                    (bar1, typeof(Func<int, byte, int>), c4, false),
                    (bar1, typeof(Func<int, int, long>), c4, false),
                    (bar1, typeof(Action<TEST.A.B.C4, int, int, int>), null, false),
                    (bar1, typeof(Func<int, int, int>), null, false),
                    (bar1, typeof(Func<int, int, int>), c1, false),
                    (bar1, typeof(Func<TEST.A.B.C1, int, int, int>), null, false),

                    (bar2, typeof(Action), c1, false),
                    (bar2, typeof(Func<long>), c1, false),
                    (bar2, typeof(Func<int>), null, false),
                    (bar2, typeof(Func<int>), c4, false),
                    (bar2, typeof(Action<TEST.A.B.C1>), null, false),
                    (bar2, typeof(Func<TEST.A.B.C4, int>), null, false),
                    (bar2, typeof(Func<TEST.A.B.C1>), null, false),
                    (bar2, typeof(Func<TEST.A.B.C1, int, int>), null, false),
                }.Select(x => new object[] {x.Item1, x.Item2, x.Item3, x.Item4});
        }

        [Theory]
        [MemberData(nameof(makeDelegateTest_data))]
        public void makeDelegateTest(MethodInfo method, Type delegateType, object target, bool expectSuccess) {
            var makeDelegateMethod =
                typeof(ReflectUtil)
                    .GetMethod(
                        nameof(ReflectUtil.makeDelegate),
                        1,
                        new[] {typeof(MethodInfo), typeof(object), typeof(bool)}
                    )
                    .MakeGenericMethod(delegateType);


            Delegate createdDelegate;

            if (expectSuccess) {
                createdDelegate = (Delegate)makeDelegateMethod.Invoke(null, new object[] {method, target, false});
                Assert.IsType(delegateType, createdDelegate);
                Assert.Same(method, createdDelegate.Method);

                createdDelegate = (Delegate)makeDelegateMethod.Invoke(null, new object[] {method, target, true});
                Assert.IsType(delegateType, createdDelegate);
                Assert.Same(method, createdDelegate.Method);
            }
            else {
                createdDelegate = (Delegate)makeDelegateMethod.Invoke(null, new object[] {method, target, false});
                Assert.Null(createdDelegate);

                var ex = Assert.Throws<TargetInvocationException>(() => makeDelegateMethod.Invoke(null, new object[] {method, target, true}));
                Assert.IsType<ArgumentException>(ex.InnerException);
            }
        }

        [Fact]
        public void makeDelegateTest_dynamicMethod() {
            var dynamicMethod = new DynamicMethod("a", typeof(int), new[] {typeof(TEST.A.B.C1), typeof(int)});
            var ilGen = dynamicMethod.GetILGenerator();
            ilGen.Emit(OpCodes.Ldnull);
            ilGen.Emit(OpCodes.Throw);

            ReflectUtil.makeDelegate<Func<TEST.A.B.C1, int, int>>(dynamicMethod);
            ReflectUtil.makeDelegate<Func<int, int>>(dynamicMethod, new TEST.A.B.C1());

            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C4, int, int>>(dynamicMethod)
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C1, byte, int>>(dynamicMethod)
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C1, int, long>>(dynamicMethod)
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Action<TEST.A.B.C1, int>>(dynamicMethod)
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C1, int>>(dynamicMethod)
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C1, int, int, int>>(dynamicMethod)
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<int, int>>(dynamicMethod, new TEST.A.B.C4())
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C1, int, int>>(dynamicMethod, new TEST.A.B.C1())
            );
            Assert.Throws<ArgumentException>(
                () => ReflectUtil.makeDelegate<Func<TEST.A.B.C1, int, int>>(dynamicMethod, new TEST.A.B.C4())
            );
        }

        [Fact]
        public void makeDelegateTest_invalidArguments() {
            Assert.Throws<ArgumentNullException>(() => ReflectUtil.makeDelegate<Action>((MethodInfo)null, null, false));
            Assert.Throws<ArgumentNullException>(() => ReflectUtil.makeDelegate<Action>((DynamicMethod)null, null));
        }

        public static IEnumerable<object[]> getMemberFromExprTest_data() {
            var c4type = typeof(TEST.A.B.C4);

            var c4_a = c4type.GetField("a");
            var c4_b = c4type.GetField("b");
            var c4_c = c4type.GetProperty("c");
            var c4_d = c4type.GetProperty("d");

            var c4_ctor = c4type.GetConstructor(new Type[] {});
            var c4_ctor_i = c4type.GetConstructor(new[] {typeof(int)});
            var c4_ctor_s = c4type.GetConstructor(new[] {typeof(string)});

            var c4_foo = c4type.GetMethod("foo");
            var c4_bar_ii = c4type.GetMethod("bar", new[] {typeof(int), typeof(int)});
            var c4_bar_is = c4type.GetMethod("bar", new[] {typeof(int), typeof(string)});
            var c4_bar_static = c4type.GetMethod("bar", new[] {typeof(TEST.A.B.C1)});

            var c4_oper_from_i = c4type.GetMethod("op_Implicit", new[] {typeof(int)});
            var c4_oper_from_s = c4type.GetMethod("op_Implicit", new[] {typeof(string)});
            var c4_oper_to_i = c4type.GetMethods().First(x => x.Name == "op_Implicit" && x.ReturnType == typeof(int));
            var c4_oper_to_s = c4type.GetMethods().First(x => x.Name == "op_Implicit" && x.ReturnType == typeof(string));

            var c4_oper_plus = c4type.GetMethod("op_Addition");
            var c4_oper_not = c4type.GetMethod("op_OnesComplement");

            var c5_foo_t = typeof(TEST.A.B.C5).GetMethod("foo");
            var c5_foo_i = typeof(TEST.A.B.C5).GetMethod("foo").MakeGenericMethod(typeof(int));

            var c6_t_x = typeof(TEST.A.B.C6<>).GetField("x");
            var c6_t_y = typeof(TEST.A.B.C6<>).GetProperty("y");
            var c6_i_x = typeof(TEST.A.B.C6<int>).GetField("x");
            var c6_i_y = typeof(TEST.A.B.C6<int>).GetProperty("y");
            var c6_t_foo = typeof(TEST.A.B.C6<>).GetMethod("foo");
            var c6_i_foo = typeof(TEST.A.B.C6<int>).GetMethod("foo");
            var c6_t_bar_u = typeof(TEST.A.B.C6<>).GetMethod("bar");
            var c6_i_bar_i = typeof(TEST.A.B.C6<int>).GetMethod("bar").MakeGenericMethod(typeof(int));

            (LambdaExpression, Type, Type, MemberInfo) make<T, U>(Expression<T> e, MemberInfo member) where U : MemberInfo =>
                (e, typeof(T), typeof(U), member as U);

            (LambdaExpression, Type, Type, bool, MemberInfo) make2<T, U>(Expression<T> e, MemberInfo member, bool stripTypeArgs) where U : MemberInfo =>
                (e, typeof(T), typeof(U), stripTypeArgs, member as U);

            var testCases1 = new (LambdaExpression, Type, Type, MemberInfo)[] {
                make<Func<TEST.A.B.C4, int>, FieldInfo>(x => x.a, c4_a),
                make<Func<TEST.A.B.C4, int>, PropertyInfo>(x => x.a, null),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => x.a, null),
                make<Func<TEST.A.B.C4, long>, FieldInfo>(x => x.a, c4_a),

                make<Func<int>, FieldInfo>(() => TEST.A.B.C4.b, c4_b),
                make<Func<int>, PropertyInfo>(() => TEST.A.B.C4.b, null),
                make<Func<int>, MethodInfo>(() => TEST.A.B.C4.b, null),
                make<Func<long>, FieldInfo>(() => TEST.A.B.C4.b, c4_b),

                make<Func<TEST.A.B.C4, int>, FieldInfo>(x => x.c, null),
                make<Func<TEST.A.B.C4, int>, PropertyInfo>(x => x.c, c4_c),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => x.c, null),
                make<Func<TEST.A.B.C4, long>, PropertyInfo>(x => x.c, c4_c),

                make<Func<int>, FieldInfo>(() => TEST.A.B.C4.d, null),
                make<Func<int>, PropertyInfo>(() => TEST.A.B.C4.d, c4_d),
                make<Func<int>, MethodInfo>(() => TEST.A.B.C4.d, null),
                make<Func<long>, PropertyInfo>(() => TEST.A.B.C4.d, c4_d),

                make<Func<TEST.A.B.C4>, ConstructorInfo>(() => new TEST.A.B.C4(), c4_ctor),
                make<Func<int, TEST.A.B.C4>, ConstructorInfo>(x => new TEST.A.B.C4(x), c4_ctor_i),
                make<Func<string, TEST.A.B.C4>, ConstructorInfo>(x => new TEST.A.B.C4(x), c4_ctor_s),
                make<Func<int, object>, ConstructorInfo>(x => new TEST.A.B.C4(x), c4_ctor_i),

                make<Func<TEST.A.B.C4>, MethodInfo>(() => new TEST.A.B.C4(), null),
                make<Func<int, TEST.A.B.C4>, MethodInfo>(x => new TEST.A.B.C4(x), null),
                make<Func<string, TEST.A.B.C4>, MethodInfo>(x => new TEST.A.B.C4(x), null),

                make<Func<TEST.A.B.C4, int, int, int>, MethodInfo>((x, y, z) => x.bar(y, z), c4_bar_ii),
                make<Func<TEST.A.B.C4, short, byte, long>, MethodInfo>((x, y, z) => x.bar(y, z), c4_bar_ii),
                make<Func<TEST.A.B.C4, int>, MethodInfo>((x) => x.bar(1, 2), c4_bar_ii),
                make<Func<TEST.A.B.C4, int, string, int>, MethodInfo>((x, y, z) => x.bar(y, z), c4_bar_is),
                make<Func<TEST.A.B.C4, int, int>, MethodInfo>((x, y) => x.bar(y, ""), c4_bar_is),

                make<Func<TEST.A.B.C1, int>, MethodInfo>(x => TEST.A.B.C4.bar(x), c4_bar_static),
                make<Func<TEST.A.B.C1, long>, MethodInfo>(x => TEST.A.B.C4.bar(x), c4_bar_static),
                make<Func<int>, MethodInfo>(() => TEST.A.B.C4.bar(new TEST.A.B.C1()), c4_bar_static),

                make<Func<TEST.A.B.C4, int, int, int>, FieldInfo>((x, y, z) => x.bar(y, z), null),
                make<Func<TEST.A.B.C1, int>, FieldInfo>((x) => TEST.A.B.C4.bar(x), null),

                make<Func<int, TEST.A.B.C4>, MethodInfo>(x => x, c4_oper_from_i),
                make<Func<string, TEST.A.B.C4>, MethodInfo>(x => x, c4_oper_from_s),
                make<Func<int, TEST.A.B.C4>, MethodInfo>(x => (TEST.A.B.C4)x, c4_oper_from_i),
                make<Func<string, TEST.A.B.C4>, MethodInfo>(x => (TEST.A.B.C4)x, c4_oper_from_s),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => x, c4_oper_to_i),
                make<Func<TEST.A.B.C4, string>, MethodInfo>(x => x, c4_oper_to_s),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => (int)x, c4_oper_to_i),
                make<Func<TEST.A.B.C4, string>, MethodInfo>(x => (string)x, c4_oper_to_s),

                make<Func<TEST.A.B.C4, TEST.A.B.C4, TEST.A.B.C4>, MethodInfo>((x, y) => x + y, c4_oper_plus),
                make<Func<TEST.A.B.C4, TEST.A.B.C4, object>, MethodInfo>((x, y) => x + y, c4_oper_plus),
                make<Func<TEST.A.B.C4, TEST.A.B.C4>, MethodInfo>((x) => ~x, c4_oper_not),
                make<Func<TEST.A.B.C4, int>, MethodInfo>((x) => ~x, c4_oper_not),

                make<Func<TEST.A.B.C4, Func<int, int, int>>, MethodInfo>(x => x.bar, c4_bar_ii),
                make<Func<TEST.A.B.C4, Func<int, string, int>>, MethodInfo>(x => x.bar, c4_bar_is),
            };

            var testCases2 = new (LambdaExpression, Type, Type, bool, MemberInfo)[] {
                make2<Action<TEST.A.B.C5>, MethodInfo>(x => x.foo<int>(), c5_foo_i, false),
                make2<Action<TEST.A.B.C5>, MethodInfo>(x => x.foo<int>(), c5_foo_t, true),

                make2<Func<TEST.A.B.C6<int>, int>, FieldInfo>(x => x.x, c6_i_x, false),
                make2<Func<TEST.A.B.C6<int>, int>, FieldInfo>(x => x.x, c6_t_x, true),

                make2<Func<TEST.A.B.C6<int>, int>, PropertyInfo>(x => x.y, c6_i_y, false),
                make2<Func<TEST.A.B.C6<int>, int>, PropertyInfo>(x => x.y, c6_t_y, true),

                make2<Action<TEST.A.B.C6<int>>, MethodInfo>(x => x.foo(), c6_i_foo, false),
                make2<Action<TEST.A.B.C6<int>>, MethodInfo>(x => x.foo(), c6_t_foo, true),

                make2<Action<TEST.A.B.C6<int>>, MethodInfo>(x => x.bar<int>(), c6_i_bar_i, false),
                make2<Action<TEST.A.B.C6<int>>, MethodInfo>(x => x.bar<int>(), c6_t_bar_u, true),
            };

            return Enumerable.Concat(
                testCases1.SelectMany(x => new object[][] {
                    new object[] {x.Item1, x.Item2, x.Item3, false, x.Item4},
                    new object[] {x.Item1, x.Item2, x.Item3, true, x.Item4},
                }),
                testCases2.Select(x => new object[] {x.Item1, x.Item2, x.Item3, x.Item4, x.Item5})
            );
        }

        [Theory]
        [MemberData(nameof(getMemberFromExprTest_data))]
        public void getMemberFromExprTest(
            LambdaExpression expr, Type delegateType, Type memberType, bool stripTypeArgs, MemberInfo expectedMember)
        {
            var getMemberFromExprMethod =
                typeof(ReflectUtil)
                    .GetMethod(nameof(ReflectUtil.getMemberFromExpr))
                    .MakeGenericMethod(delegateType, memberType);

            var invokeArgs = new object[] {expr, stripTypeArgs};

            if (expectedMember == null) {
                var ex = Assert.Throws<TargetInvocationException>(() => getMemberFromExprMethod.Invoke(null, invokeArgs));
                Assert.IsType<ArgumentException>(ex.InnerException);
            }
            else {
                Assert.Same(expectedMember, getMemberFromExprMethod.Invoke(null, invokeArgs));
            }
        }

        public static IEnumerable<object[]> getMemberFromExprTest_noMember_data() {
            (LambdaExpression, Type, Type) make<T, U>(Expression<T> e) => (e, typeof(T), typeof(U));

            return new (LambdaExpression, Type, Type)[] {
                make<Func<int, int>, FieldInfo>(x => x),
                make<Func<int, int>, MethodInfo>(x => x),
                make<Func<int>, FieldInfo>(() => 0),
                make<Func<int>, MethodInfo>(() => 0),
                make<Func<int, int>, FieldInfo>(x => ~x),
                make<Func<int, int, int>, MethodInfo>((x, y) => x + y),
                make<Func<TEST.A.B.C4, int>, FieldInfo>(x => x.a + 1),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => ~x.bar(0, 0)),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => x.bar(0, 0) + x.bar(0, 1)),
                make<Func<TEST.A.B.C4, int>, MethodInfo>(x => x.foo(0) * x.bar(1, 2)),
            }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});
        }

        [Theory]
        [MemberData(nameof(getMemberFromExprTest_noMember_data))]
        public void getMemberFromExprTest_noMember(LambdaExpression expr, Type delegateType, Type memberType) {
            var getMemberFromExprMethod =
                typeof(ReflectUtil)
                    .GetMethod(nameof(ReflectUtil.getMemberFromExpr))
                    .MakeGenericMethod(delegateType, memberType);

            var invokeArgs = new object[] {expr, false};
            TargetInvocationException ex;

            ex = Assert.Throws<TargetInvocationException>(() => getMemberFromExprMethod.Invoke(null, invokeArgs));
            Assert.IsType<ArgumentException>(ex.InnerException);

            invokeArgs[1] = true;
            ex = Assert.Throws<TargetInvocationException>(() => getMemberFromExprMethod.Invoke(null, invokeArgs));
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public void getMemberFromExprTest_invalidArguments() {
            Assert.Throws<ArgumentNullException>(() => ReflectUtil.getMemberFromExpr<Action, MemberInfo>(null, false));
            Assert.Throws<ArgumentNullException>(() => ReflectUtil.getMemberFromExpr<Action, MemberInfo>(null, true));
        }

    }

}
