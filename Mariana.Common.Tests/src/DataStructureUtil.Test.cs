using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mariana.Common.Tests {

    public class DataStructureUtilTest {

        public static IEnumerable<object[]> nextPrimeTest_data = new int[] {
            0, 1, 2, 3, 4, 6, 8, 12, 18, 25, 30, 43, 50, 60, 70, 80, 89, 90,
            100, 150, 200, 300, 373, 500, 787, 1000, 1500, 2000, 5000, 10000,
            50000, 100000, 500000, 1000000, 5000000, 7199369, 7199370, 10000000,
            50000000, 50000017, 100000000, 150000000, 167833927, 200000000,
            500578823, 783144942, 1356631784, 2000000413, Int32.MaxValue,
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(nextPrimeTest_data))]
        public void nextPrimeTest(int num) {
            int nextPrime = DataStructureUtil.nextPrime(num);
            Assert.True(nextPrime > 1, $"Expected {nextPrime} to be > 1.");
            Assert.True(nextPrime >= num, $"Expected {nextPrime} to be >= {num}.");

            for (int i = 2; (long)i * i <= (long)nextPrime; i++)
                Assert.True(nextPrime % i != 0, $"Expected {nextPrime} to be prime, but has has factor {i}.");
        }

        public static IEnumerable<object[]> nextPowerOf2Test_data = new int[] {
            Int32.MinValue, -1, 0,
            1, 2, 3, 4, 5, 6, 7, 8, 9, 12,
            15, 16, 17, 19, 31, 32, 33,
            48, 63, 64, 65, 127, 128, 129,
            255, 256, 257, 511, 512, 513,
            (1 << 10) - 1, 1 << 10, (1 << 10) + 1,
            (1 << 12) - 1, 1 << 12, (1 << 12) + 1,
            (1 << 15) - 1, 1 << 15, (1 << 15) + 1,
            (1 << 20) - 1, 1 << 20, (1 << 20) + 1,
            (1 << 24) - 1, 1 << 24, (1 << 24) + 1,
            (1 << 28) - 1, 1 << 28, (1 << 28) + 1,
            (1 << 29) - 1, 1 << 29, (1 << 29) + 1,
            (1 << 30) - 1,
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(nextPowerOf2Test_data))]
        public void nextPowerOf2Test(int num) {
            int nextPowerOf2 = DataStructureUtil.nextPowerOf2(num);
            Assert.NotEqual(0, nextPowerOf2);
            Assert.True((nextPowerOf2 & (nextPowerOf2 - 1)) == 0, $"Expected {nextPowerOf2} to be a power of 2.");
            Assert.True(nextPowerOf2 >= num, $"Expected {nextPowerOf2} to be >= {num}");

            uint upperBound = (uint)Math.Max(num, 1) * 2;
            Assert.True((uint)nextPowerOf2 < upperBound, $"Expected {nextPowerOf2} to be < {upperBound}.");
        }

        public static IEnumerable<object[]> getNextArraySizeTest_data = new (int, int)[] {
            (0, 1),
            (0, 2),
            (0, 4),
            (0, 0x40000001),
            (0, Int32.MaxValue / 2),
            (0, Int32.MaxValue),

            (2, 3),
            (2, 4),
            (2, 30),
            (2, 1000000),
            (2, Int32.MaxValue / 2),
            (2, Int32.MaxValue),

            (7, 8),
            (7, 12),
            (7, 14),
            (7, 15),
            (7, 27),
            (7, 28),
            (7, 29),
            (7, 111),
            (7, 114),
            (7, 83913),
            (7, 0x40000001),
            (7, Int32.MaxValue),

            (1456, 1839),
            (1672, 1738),
            (2371, 4742),
            (3471, 6948),
            (4412, 4498),
            (3279, 67412),
            (4351, 94813),
            (74817, 83918),
            (84928, 183774),
            (87313, 938471),
            (84931, 4637113),
            (64718, 13648138),
            (48773, 56473130),
            (837137, 928187381),
            (747139, 1273949123),
            (887391, 2039489198),

            (0x3FFFFFFF, 0x40000000),
            (0x3FFFFFFF, 0x50000000),
            (0x3FFFFFFF, 0x70000000),
            (0x3FFFFFFF, 0x7FFFFFFE),
            (0x3FFFFFFF, 0x7FFFFFFF),
            (0x40000000, 0x40000001),
            (0x40000000, 0x50000000),
            (0x40000000, 0x70000000),
            (0x40000000, 0x7FFFFFFE),
            (0x40000000, 0x7FFFFFFF),
            (0x60000000, 0x60000001),
            (0x60000000, 0x70000000),
            (0x60000000, 0x7FFFFFFE),
            (0x60000000, 0x7FFFFFFF),
            (0x7C000000, 0x7C000001),
            (0x7C000000, 0x7FFFFFFE),
            (0x7C000000, 0x7FFFFFFF),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(getNextArraySizeTest_data))]
        public void getNextArraySizeTest(int currentSize, int newSizeHint) {
            var newSize = DataStructureUtil.getNextArraySize(currentSize, newSizeHint);
            Assert.True(newSize >= newSizeHint, $"Expected newSize={newSize} to be >= newSizeHint={newSizeHint}.");

            if (currentSize != Int32.MaxValue)
                Assert.True(newSize > currentSize, $"Expected newSize={newSize} to be > currentSize={currentSize}.");

            if (currentSize > 4)
                Assert.True((uint)newSize < (uint)newSizeHint * 2, $"Expected newSize={newSize} to be < newSizeHint*2={(uint)newSizeHint * 2}.");

            if (currentSize <= 0x40000000)
                Assert.True(newSize >= currentSize * 2, $"Expected newSize={newSize} to be >= currentSize*2={currentSize * 2}.");
        }

        [Theory]
        [InlineData(-1, -1)]
        [InlineData(-1, 5)]
        [InlineData(5, -1)]
        [InlineData(5, -6)]
        [InlineData(5, 4)]
        [InlineData(5, 5)]
        [InlineData(0, 0)]
        [InlineData(0, -1)]
        [InlineData(Int32.MaxValue, Int32.MinValue)]
        [InlineData(Int32.MinValue, Int32.MaxValue)]
        public void getNextArraySizeTest_invalidArguments(int currentSize, int newSizeHint) {
            Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.getNextArraySize(currentSize, newSizeHint));
        }

        public static IEnumerable<object[]> resizeArrayTest_data = new (int, int, int)[] {
            (0, 0, 0),
            (0, 0, 2),
            (0, 0, 10000),

            (1, 0, 0),
            (1, 1, 0),
            (1, 0, 1),
            (1, 1, 1),
            (1, 0, 5),
            (1, 1, 5),

            (12, 11, 0),
            (12, 11, 10),
            (12, 11, 11),
            (12, 11, 12),
            (12, 11, 13),
            (12, 12, 0),
            (12, 12, 11),
            (12, 12, 12),
            (12, 12, 13),

            (20, 5, 39),
            (20, 5, 79),
            (20, 20, 39),
            (20, 20, 79),

            (50000, 50000, 50001),
            (1 << 20, 1 << 20, (1 << 20) + 1)
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(resizeArrayTest_data))]
        public void resizeArrayTest(int arrLen, int currentSize, int newSize) {
            run(exact: false);
            run(exact: true);

            void run(bool exact) {
                byte[] arr = new byte[arrLen];
                arr.AsSpan(0, currentSize).Fill(255);
                arr.AsSpan(currentSize).Fill(254);

                byte[] arr2 = arr;
                DataStructureUtil.resizeArray(ref arr2, currentSize, newSize, exact);

                if (newSize <= arr.Length) {
                    Assert.Same(arr, arr2);
                }
                else {
                    Assert.NotSame(arr, arr2);
                    int expectedLength = exact ? newSize : DataStructureUtil.getNextArraySize(currentSize, newSize);
                    Assert.Equal(expectedLength, arr2.Length);
                }

                if (newSize < currentSize) {
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(255, newSize), arr2.Take(newSize));
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(0, currentSize - newSize), arr2.Skip(newSize).Take(currentSize - newSize));
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(254, arr.Length - currentSize), arr2.Skip(currentSize));
                }
                else if (newSize <= arr.Length) {
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(255, currentSize), arr2.Take(currentSize));
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(0, newSize - currentSize), arr2.Skip(currentSize).Take(newSize - currentSize));
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(254, arr2.Length - newSize), arr2.Skip(newSize));
                }
                else {
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(255, currentSize), arr2.Take(currentSize));
                    Assert.Equal<byte>(Enumerable.Repeat<byte>(0, arr2.Length - currentSize), arr2.Skip(currentSize));
                }
            }
        }

        public static IEnumerable<object[]> expandArrayTest_data = new (int, int)[] {
            (0, 0),
            (1, 0),
            (100, 0),
            (0, 1),
            (0, 100),
            (1, 1),
            (1, 10),
            (10, 1),
            (10, 10),
            (10, 50),
            (100, 1),
            (100, 25),
            (100, 99),
            (100, 125),
            (141, 12834)
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(expandArrayTest_data))]
        public void expandArrayTest(int arrLen, int expandSize) {
            byte[] arr1 = new byte[arrLen];
            arr1.AsSpan().Fill(255);

            byte[] arr2 = arr1;
            DataStructureUtil.expandArray(ref arr2, expandSize);

            if (expandSize == 0) {
                Assert.Same(arr1, arr2);
            }
            else {
                Assert.NotSame(arr1, arr2);
                Assert.Equal(arr2.Length, DataStructureUtil.getNextArraySize(arrLen, arrLen + expandSize));
            }

            Assert.Equal<byte>(Enumerable.Repeat<byte>(255, arrLen), arr2.Take(arrLen));
            Assert.Equal<byte>(Enumerable.Repeat<byte>(0, arr2.Length - arrLen), arr2.Skip(arrLen));
        }

        [Fact]
        public void resizeArrayTest_invalidArguments() {
            run(exact: false);
            run(exact: true);

            void run(bool exact) {
                int[] arr1 = null;
                int[] arr2 = arr1;

                Assert.Throws<ArgumentNullException>(() => DataStructureUtil.resizeArray(ref arr2, 0, 0, exact));
                Assert.Throws<ArgumentNullException>(() => DataStructureUtil.resizeArray(ref arr2, 1, 0, exact));
                Assert.Throws<ArgumentNullException>(() => DataStructureUtil.resizeArray(ref arr2, 1, 2, exact));
                Assert.Throws<ArgumentNullException>(() => DataStructureUtil.resizeArray(ref arr2, -1, 0, exact));

                Assert.Same(arr1, arr2);

                arr1 = new int[0];
                arr2 = arr1;

                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, 1, 0, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, -1, 0, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, 0, -1, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, -1, -1, exact));

                Assert.Same(arr1, arr2);

                arr1 = new int[5000];
                arr2 = arr1;

                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, -1, 0, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, 0, -1, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, -1, -1, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, 5001, 5000, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, 5001, 5001, exact));
                Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.resizeArray(ref arr2, 5001, 5002, exact));

                Assert.Same(arr1, arr2);
            }
        }

        [Fact]
        public void expandArrayTest_invalidArguments() {
            int[] arr1 = null;
            int[] arr2 = arr1;

            Assert.Throws<ArgumentNullException>(() => DataStructureUtil.expandArray(ref arr2, -1));
            Assert.Throws<ArgumentNullException>(() => DataStructureUtil.expandArray(ref arr2, 0));
            Assert.Throws<ArgumentNullException>(() => DataStructureUtil.expandArray(ref arr2, 1));
            Assert.Throws<ArgumentNullException>(() => DataStructureUtil.expandArray(ref arr2, 100));
            Assert.Same(arr1, arr2);

            arr1 = new int[100];
            arr2 = arr1;

            Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.expandArray(ref arr2, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.expandArray(ref arr2, Int32.MinValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => DataStructureUtil.expandArray(ref arr2, Int32.MaxValue - 99));
            Assert.Same(arr1, arr2);
        }

        public static IEnumerable<object[]> compactNullsTest_data = new string[][] {
            new string[] {},
            new string[] {"A"},
            new string[] {"A", "B", "C"},
            new string[] {null},
            new string[] {null, null, null},
            new string[] {null, "A"},
            new string[] {"A", null},
            new string[] {null, "A", "B"},
            new string[] {"A", null, "B"},
            new string[] {"A", "B", null},
            new string[] {null, null, "A", "B"},
            new string[] {"A", "B", null, null},
            new string[] {"A", null, null, "B"},
            new string[] {"A", null, "B", null},
            new string[] {null, "A", null, "B"},
            new string[] {null, "A", null, "B", null, "C", "D", null},
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(compactNullsTest_data))]
        public void compactNullsTest(string[] arr) {
            var resultSpan = DataStructureUtil.compactNulls<string>(arr);
            Assert.True(resultSpan.Length <= arr.Length);
            Assert.True(resultSpan == arr.AsSpan(0, resultSpan.Length));

            Assert.Equal<string>(arr.Where(x => x != null), resultSpan.ToArray());
            Assert.Equal<string>(Enumerable.Repeat<string>(null, arr.Length - resultSpan.Length), arr.Skip(resultSpan.Length));
        }

        public static IEnumerable<object[]> sortSpanTest_data = sortSpanTest_dataGenerator().ToArray();

        private static IEnumerable<object[]> sortSpanTest_dataGenerator() {
            var random = new Random(184911493);

            var fixedArrays = new int[][] {
                new int[0],
                new[] {1},
                new[] {1, 2},
                new[] {2, 1},
                new[] {1, 1},
                new[] {1, 2, 3, 4},
                new[] {1, 2, 4, 3},
                new[] {2, 1, 3, 4},
                new[] {2, 1, 4, 3},
                new[] {3, 4, 1, 2},
                new[] {4, 3, 1, 2},
                new[] {4, 3, 2, 1},
                new[] {1, 1, 1, 1},
                new[] {1, 1, 2, 2},
                new[] {2, 2, 1, 1},
                new[] {1, 2, 1, 2},
                new[] {2, 1, 2, 1},
                new[] {1, 2, 2, 1},
                new[] {2, 1, 1, 2},
                new[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16},
                new[] {16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1},
                new[] {12, 8, 42, 4, 7, 32, 54, 18, 29, 43, 57, 11, 98, 32, 17, 67},
                new[] {1, 4, 3, 2, 4, 1, 3, 3, 2, 5, 3, 4, 3, 4, 2, 1},

                Enumerable.Range(0, 17).ToArray(),
                Enumerable.Repeat(0, 17).ToArray(),
                Enumerable.Range(0, 17).Select(x => 30 - x).ToArray(),
                Enumerable.Range(0, 17).Select(x => x * 1738857113).ToArray(),
                Enumerable.Range(0, 495).ToArray(),
                Enumerable.Repeat(0, 495).ToArray(),
                Enumerable.Range(0, 495).Select(x => x * 1738857113).ToArray(),
            };

            var randomArrayGen = new Func<int[]>[] {
                () => Enumerable.Range(0, 16).Select(x => random.Next(4)).ToArray(),
                () => Enumerable.Range(0, 16).Select(x => random.Next(1000)).ToArray(),
                () => Enumerable.Range(0, 16).Select(x => random.Next(Int32.MinValue, Int32.MaxValue)).ToArray(),

                () => Enumerable.Range(0, 24).Select(x => random.Next(5)).ToArray(),
                () => Enumerable.Range(0, 24).Select(x => random.Next(40)).ToArray(),
                () => Enumerable.Range(0, 24).Select(x => random.Next(Int32.MinValue, Int32.MaxValue)).ToArray(),

                () => Enumerable.Range(0, 497).Select(x => random.Next(35)).ToArray(),
                () => Enumerable.Range(0, 497).Select(x => random.Next(582)).ToArray(),
                () => Enumerable.Range(0, 497).Select(x => random.Next(4435)).ToArray(),
                () => Enumerable.Range(0, 497).Select(x => random.Next(Int32.MinValue, Int32.MaxValue)).ToArray(),

                () => Enumerable.Range(0, 1024).Select(x => random.Next(23)).ToArray(),
                () => Enumerable.Range(0, 1024).Select(x => random.Next(1024)).ToArray(),
                () => Enumerable.Range(0, 1024).Select(x => random.Next(65536)).ToArray(),
                () => Enumerable.Range(0, 1024).Select(x => random.Next(Int32.MinValue, Int32.MaxValue)).ToArray(),
            };

            const int RANDOM_GEN_REPS = 15;

            return Enumerable.Concat(
                    fixedArrays,
                    randomArrayGen.SelectMany(x => Enumerable.Repeat(0, RANDOM_GEN_REPS).Select(i => x()))
                ).Select(
                    x => new object[] {x}
                );
        }

        [Theory]
        [MemberData(nameof(sortSpanTest_data))]
        public void sortSpanTest(int[] arr) {
            var freqTable = makeFrequencyTable(arr);

            run(arrToSort => DataStructureUtil.sortSpan(arrToSort, (in int x, in int y) => x.CompareTo(y)));
            run(arrToSort => DataStructureUtil.sortSpan(arrToSort, (int x, int y) => x.CompareTo(y)));

            void run(Action<int[]> sorter) {
                const int REPS = 30;

                for (int i = 0; i < REPS; i++) {
                    int[] sortedArr = arr.AsSpan().ToArray();
                    sorter(sortedArr);

                    for (int j = 0; j < sortedArr.Length - 1; j++)
                        Assert.True(sortedArr[j] <= sortedArr[j + 1]);

                    var sortedFreqTable = makeFrequencyTable(sortedArr);

                    Assert.Equal(freqTable.Count, sortedFreqTable.Count);
                    foreach (var kv in freqTable) {
                        Assert.True(sortedFreqTable.TryGetValue(kv.Key, out int f));
                        Assert.Equal(kv.Value, f);
                    }
                }
            }

            Dictionary<int, int> makeFrequencyTable(int[] values) {
                var table = new Dictionary<int, int>();
                for (int i = 0; i < values.Length; i++) {
                    table.TryGetValue(values[i], out int f);
                    table[values[i]] = f + 1;
                }
                return table;
            }
        }

        [Fact]
        public void sortSpanTest_invalidArguments() {
            Assert.Throws<ArgumentNullException>(
                () => DataStructureUtil.sortSpan(new[] {1, 2}, (Comparison<int>)null)
            );
            Assert.Throws<ArgumentNullException>(
                () => DataStructureUtil.sortSpan(new[] {1, 2}, (DataStructureUtil.SortComparerIn<int>)null)
            );
        }

        [Theory]
        [MemberData(nameof(sortSpanTest_data))]
        public void getSpanSortPermutationTest(int[] arr) {
            const int REPS = 30;
            int[] permArr = new int[arr.Length];

            for (int i = 0; i < REPS; i++) {
                permArr.AsSpan().Fill(-1);
                DataStructureUtil.getSpanSortPermutation(arr, permArr, (in int x, in int y) => x.CompareTo(y));

                for (int j = 0; j < permArr.Length; j++)
                    Assert.True(permArr[j] >= 0 && permArr[j] < arr.Length);

                for (int j = 0; j < permArr.Length - 1; j++)
                    Assert.True(arr[permArr[j]] <= arr[permArr[j + 1]]);

                Assert.Equal(permArr.Length, permArr.Distinct().Count());
            }
        }

        [Fact]
        public void getSpanSortPermutationTest_invalidArguments() {
            Assert.Throws<ArgumentNullException>(
                () => DataStructureUtil.getSpanSortPermutation<int>(new[] {1, 2}, new int[2], null)
            );
            Assert.Throws<ArgumentNullException>(
                () => DataStructureUtil.getSpanSortPermutation<int>(new[] {1, 2}, new int[3], null)
            );

            Assert.Throws<ArgumentException>(
                () => DataStructureUtil.getSpanSortPermutation<int>(new[] {1, 2}, new int[1], (in int x, in int y) => x.CompareTo(y))
            );
            Assert.Throws<ArgumentException>(
                () => DataStructureUtil.getSpanSortPermutation<int>(new[] {1, 2}, new int[3], (in int x, in int y) => x.CompareTo(y))
            );
        }

    }

}
