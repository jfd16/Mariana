using System;
using System.Numerics;
using System.Threading;
using System.Collections.Concurrent;
using Xunit;
using System.Linq;

namespace Mariana.Common.Tests {

    public sealed class ThreadStaticRandomTest {

        [Fact]
        public void shouldProvideUniqueInstancePerThreadWithDifferentSeed() {
            var queue1 = new ConcurrentQueue<Random>();
            var queue2 = new ConcurrentQueue<Random>();
            var threads = new Thread[10];

            void worker() {
                queue1.Enqueue(ThreadStaticRandom.instance);
                queue2.Enqueue(ThreadStaticRandom.instance);
            }

            for (int i = 0; i < threads.Length; i++) {
                threads[i] = new Thread(worker);
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; i++)
                threads[i].Join();

            Assert.Equal(queue1.Count, threads.Length);
            Assert.Equal(queue2.Count, threads.Length);

            var instances = new RefWrapper<Random>[threads.Length];
            var instances2 = new RefWrapper<Random>[threads.Length];
            for (int i = 0; i < instances.Length; i++) {
                Random inst;
                queue1.TryDequeue(out inst);
                instances[i] = inst;
                queue2.TryDequeue(out inst);
                instances2[i] = inst;
            }

            Assert.Equal(instances.Length, instances.Distinct().Count());
            Assert.Equal(instances2.Length, instances2.Distinct().Count());
            Assert.True(instances.ToHashSet().SetEquals(instances2.ToHashSet()));

            var bigints = new BigInteger[instances.Length];
            for (int i = 0; i < instances.Length; i++)
                bigints[i] = randomBigInt(instances[i].value, 64);

            Assert.Equal(bigints.Length, bigints.Distinct().Count());

            BigInteger randomBigInt(Random r, int byteLength) {
                byte[] b = new byte[byteLength];
                r.NextBytes(b);
                return new BigInteger(b, isUnsigned: true);
            }
        }

    }

}
