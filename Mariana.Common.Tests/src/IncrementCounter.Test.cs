using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace Mariana.Common.Tests {

    public class IncrementCounterTest {

        [Fact]
        public void constructor_shouldSetInitialValue() {
            IncrementCounter ctr;

            ctr = new IncrementCounter();
            Assert.Equal(0, ctr.current);
            ctr = new IncrementCounter(12);
            Assert.Equal(12, ctr.current);
            ctr = new IncrementCounter(-5);
            Assert.Equal(-5, ctr.current);
        }

        [Fact]
        public void next_shouldGetCurrentValueAndIncrement() {
            IncrementCounter ctr;

            ctr = new IncrementCounter();
            Assert.Equal(0, ctr.next());
            Assert.Equal(1, ctr.next());
            Assert.Equal(2, ctr.current);
            Assert.Equal(2, ctr.next());
            Assert.Equal(3, ctr.current);

            ctr = new IncrementCounter(Int32.MaxValue - 1);
            Assert.Equal(Int32.MaxValue - 1, ctr.next());
            Assert.Equal(Int32.MaxValue, ctr.next());
            Assert.Equal(Int32.MinValue, ctr.next());
            Assert.Equal(Int32.MinValue + 1, ctr.current);

            ctr = new IncrementCounter(-2);
            Assert.Equal(-2, ctr.next());
            Assert.Equal(-1, ctr.next());
            Assert.Equal(0, ctr.next());
            Assert.Equal(1, ctr.next());
            Assert.Equal(2, ctr.next());
            Assert.Equal(3, ctr.current);
        }

        [Fact]
        public void atomicNext_shouldGetCurrentValueAndIncrementAtomic() {
            IncrementCounter ctr;

            ctr = new IncrementCounter();
            doTest(10, 100);

            ctr = new IncrementCounter(100);
            doTest(10, 2000);

            void doTest(int nThreads, int nReps) {
                int initialValue = ctr.current;
                ConcurrentBag<int> values = new ConcurrentBag<int>();

                var threads = new Thread[nThreads];
                for (int i = 0; i < threads.Length; i++) {
                    threads[i] = new Thread(() => {
                        for (int j = 0; j < nReps; j++)
                            values.Add(ctr.atomicNext());
                    });
                }

                for (int i = 0; i < threads.Length; i++)
                    threads[i].Start();

                for (int i = 0; i < threads.Length; i++)
                    threads[i].Join();

                Assert.Equal(ctr.current, initialValue + nThreads * nReps);

                var sortedValues = values.ToArray();
                Array.Sort(sortedValues);
                Assert.Equal<int>(Enumerable.Range(initialValue, nThreads * nReps), sortedValues);
            }
        }

    }

}
