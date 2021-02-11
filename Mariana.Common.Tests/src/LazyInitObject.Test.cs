using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace Mariana.Common.Tests {

    public class LazyInitObjectTest {

        [Fact]
        public void defaultValueTest() {
            LazyInitObject<int> lazy = default;
            Assert.Equal(LazyInitObjectState.UNINITIALIZED, lazy.currentState);
            Assert.Throws<InvalidOperationException>(() => lazy.value);
        }

        [Fact]
        public void constructorTest_invalidArguments() {
            Assert.Throws<ArgumentNullException>(() => new LazyInitObject<int>(null));
            Assert.Throws<ArgumentNullException>(() => new LazyInitObject<int>(null, null, (LazyInitRecursionHandling)255));

            Assert.Throws<ArgumentOutOfRangeException>(() => new LazyInitObject<int>(() => 0, null, (LazyInitRecursionHandling)3));
        }

        [Fact]
        public void currentStateTest_beforeValueAccessed() {
            var lazy = new LazyInitObject<int>(() => 0);
            Assert.Equal(LazyInitObjectState.UNINITIALIZED, lazy.currentState);

            lazy = new LazyInitObject<int>(() => 1000, new object(), LazyInitRecursionHandling.RETURN_DEFAULT);
            Assert.Equal(LazyInitObjectState.UNINITIALIZED, lazy.currentState);
        }

        [Fact]
        public void valueTest() {
            int initFuncCalledCount = 0;
            object value = new object();

            object initFunc() {
                initFuncCalledCount++;
                return value;
            }

            var lazy = new LazyInitObject<object>(initFunc);

            object lazyValue = lazy.value;
            Assert.Same(value, lazyValue);
            Assert.Equal(1, initFuncCalledCount);

            lazyValue = lazy.value;
            Assert.Same(value, lazyValue);
            Assert.Equal(1, initFuncCalledCount);
        }

        [Fact]
        public void valueTest_withUserSuppliedLock() {
            int initFuncCalledCount = 0;
            object value = new object();
            object mutex = new object();

            object initFunc() {
                Assert.True(Monitor.IsEntered(mutex));
                initFuncCalledCount++;
                return value;
            }

            var lazy = new LazyInitObject<object>(initFunc, mutex);
            Assert.False(Monitor.IsEntered(mutex));

            object lazyValue = lazy.value;
            Assert.Same(value, lazyValue);
            Assert.Equal(1, initFuncCalledCount);
            Assert.False(Monitor.IsEntered(mutex));

            lazyValue = lazy.value;
            Assert.Same(value, lazyValue);
            Assert.Equal(1, initFuncCalledCount);
            Assert.False(Monitor.IsEntered(mutex));
        }

        [Fact]
        public void currentStateTest_afterFirstValueAccess() {
            LazyInitObject<int> lazy = default;

            int initFunc() {
                Assert.Equal(LazyInitObjectState.IN_INITIALIZER, lazy.currentState);
                return 10000;
            }

            lazy = new LazyInitObject<int>(initFunc);
            _ = lazy.value;
            Assert.Equal(LazyInitObjectState.COMPLETE, lazy.currentState);

            _ = lazy.value;
            Assert.Equal(LazyInitObjectState.COMPLETE, lazy.currentState);
        }

        [Fact]
        public void valueTest_whenInitFuncThrows() {
            LazyInitObject<int> lazy = default;
            Exception ex = new Exception();

            int initFunc() => throw ex;

            lazy = new LazyInitObject<int>(initFunc);
            var caughtEx = Assert.Throws<Exception>(() => lazy.value);
            Assert.Same(ex, caughtEx);

            Assert.Equal(LazyInitObjectState.FAILED, lazy.currentState);
            Assert.Throws<InvalidOperationException>(() => lazy.value);
        }

        [Fact]
        public void recursionHandlingTest() {
            LazyInitObject<int> lazy = default;

            lazy = new LazyInitObject<int>(() => {
                Assert.Throws<InvalidOperationException>(() => lazy.value);
                return 10000;
            });

            Assert.Equal(10000, lazy.value);

            lazy = new LazyInitObject<int>(
                () => {
                    Assert.Equal(0, lazy.value);
                    return 10000;
                },
                null,
                LazyInitRecursionHandling.RETURN_DEFAULT
            );

            Assert.Equal(10000, lazy.value);

            int recCallCount = 0;

            lazy = new LazyInitObject<int>(
                () => {
                    recCallCount++;
                    if (recCallCount < 5)
                        Assert.Equal(5, lazy.value);
                    return recCallCount;
                },
                null,
                LazyInitRecursionHandling.RECURSIVE_CALL
            );

            Assert.Equal(5, lazy.value);
        }

        [Fact]
        public void valueTest_multipleThreads() {
            var threads = new Thread[16];
            var values = new ConcurrentBag<int>();

            int returnValue = 10000;

            var lazy = new LazyInitObject<int>(
                () => {
                    Thread.Sleep(100);
                    return Interlocked.Increment(ref returnValue);
                },
                recursionHandling: LazyInitRecursionHandling.RECURSIVE_CALL
            );

            for (int i = 0; i < threads.Length; i++)
                threads[i] = new Thread(() => values.Add(lazy.value));

            for (int i = 0; i < threads.Length; i++) {
                threads[i].Start();
                Thread.Sleep(10);
            }

            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();

            Assert.Equal(10001, returnValue);
            Assert.Equal(10001, lazy.value);
            Assert.Equal(LazyInitObjectState.COMPLETE, lazy.currentState);
            Assert.Equal<int>(new[] {10001}, values.ToArray().Distinct());
        }

        [Fact]
        public void valueTest_multipleThreads_initFuncThrows() {
            var threads = new Thread[16];
            var exceptions = new Exception[threads.Length];

            var lazy = new LazyInitObject<int>(() => {
                Thread.Sleep(100);
                throw new Exception();
            });

            for (int i = 0; i < threads.Length; i++) {
                int curIndex = i;
                threads[i] = new Thread(() => {
                    try {
                        _ = lazy.value;
                    }
                    catch (Exception e) {
                        exceptions[curIndex] = e;
                    }
                });
            }

            for (int i = 0; i < threads.Length; i++) {
                threads[i].Start();
                Thread.Sleep(10);
            }

            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();

            Assert.Equal(LazyInitObjectState.FAILED, lazy.currentState);
            Assert.DoesNotContain(exceptions, x => x == null);
        }

    }

}
