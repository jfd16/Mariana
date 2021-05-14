using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

using static System.Buffers.Binary.BinaryPrimitives;

namespace Mariana.AVM2.Tests {

    public partial class ASArrayTest {

        [ThreadStatic]
        private static Random s_currentRandom;

        /// <summary>
        /// Sets the seed value for the random number generator. Call this before the start of each test.
        /// </summary>
        /// <param name="seed">The seed value for the random number generator.</param>
        private static void setRandomSeed(int seed) => s_currentRandom = new Random(seed);

        /// <summary>
        /// Generates a random non-negative integer that is less than <paramref name="max"/>.
        /// </summary>
        private static int randomInt(int max) => s_currentRandom.Next(max);

        /// <summary>
        /// Generates a random array index less than the given length.
        /// </summary>
        private static uint randomIndex(uint length = UInt32.MaxValue) {
            if (length <= (uint)Int32.MaxValue)
                return (uint)s_currentRandom.Next((int)length);

            uint maxSampleValue = UInt32.MaxValue - (UInt32.MaxValue % length);
            uint sample;
            Span<byte> sampleBytes = stackalloc byte[sizeof(uint)];

            do {
                s_currentRandom.NextBytes(sampleBytes);
                sample = ReadUInt32LittleEndian(sampleBytes);
            } while (sample >= maxSampleValue);

            return sample % length;
        }

        /// <summary>
        /// Returns a random element from the given array.
        /// </summary>
        private static T randomSample<T>(T[] arr) => RandomHelper.sampleArray(s_currentRandom, arr);

        /// <summary>
        /// A wrapper for an Array instance that prevents a long string being output in an error message when
        /// a test involving a large length array fails.
        /// </summary>
        public readonly struct ArrayWrapper {
            private const int TOSTRING_LENGTH_LIMIT = 16;

            public readonly ASArray instance;
            internal ArrayWrapper(ASArray instance) => this.instance = instance;

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append("Array(").Append(instance.length).Append(") [");

                for (uint i = 0; i < Math.Min(instance.length, (uint)TOSTRING_LENGTH_LIMIT); i++)
                    sb.Append((i > 0) ? ", " : "").Append(instance[i]);

                if (instance.length > TOSTRING_LENGTH_LIMIT)
                    sb.Append(", ...");

                sb.Append(']');
                return sb.ToString();
            }
        }

        /// <summary>
        /// Represents the state of an Array instance using a hash table for its elements.
        /// </summary>
        public readonly struct Image {
            public readonly uint length;
            public readonly Dictionary<uint, ASAny> elements;

            internal Image(uint length, Dictionary<uint, ASAny> elements) {
                this.length = length;
                this.elements = elements;
            }
        }

        /// <summary>
        /// Creates an <see cref="Image"/> instance that represents an empty array of the given
        /// length.
        /// </summary>
        private static Image makeEmptyImage(uint length = 0) => new Image(length, new Dictionary<uint, ASAny>());

        /// <summary>
        /// Creates an <see cref="Image"/> instance that represents an array that is initialized with
        /// the given values.
        /// </summary>
        private static Image makeImageWithValues(params ASAny[] values) {
            var image = new Image((uint)values.Length, new Dictionary<uint, ASAny>());
            for (int i = 0; i < values.Length; i++)
                image.elements[(uint)i] = values[i];

            return image;
        }

        /// <summary>
        /// Creates an <see cref="ASArray"/> instance that is initialized with the given values
        /// and a corresponding <see cref="Image"/> instance.
        /// </summary>
        private static (ASArray, Image) makeArrayAndImageWithValues(params ASAny[] values) =>
            (new ASArray(values), makeImageWithValues(values));

        /// <summary>
        /// Creates an <see cref="ASArray"/> instance that represents an empty array of the given
        /// length and a corresponding <see cref="Image"/> instance.
        /// </summary>
        private static (ASArray, Image) makeEmptyArrayAndImage(uint length = 0) {
            ASArray arr = (length == 0) ? new ASArray() : new ASArray(length);
            Image img = makeEmptyImage(length);
            return (arr, img);
        }

        /// <summary>
        /// Creates a dictionary from the given indices, with a unique value for each index.
        /// </summary>
        /// <param name="pairs">The indices to be used as keys in the dictionary.</param>
        /// <returns>The dictionary created from <paramref name="indices"/>.</returns>
        private static Dictionary<uint, ASAny> makeIndexDict(params uint[] indices) {
            var dict = new Dictionary<uint, ASAny>(indices.Length);
            for (int i = 0; i < indices.Length; i++)
                dict.Add(indices[i], new ConvertibleMockObject(stringValue: "!" + i));
            return dict;
        }

        private static ASObject s_arrayClassPrototype = Class.fromType(typeof(ASArray)).prototypeObject;

        private static Dictionary<uint, ASAny> s_currentProtoProperties;

        private static object s_protoGlobalLock = new object();

        /// <summary>
        /// Creates a string representation of an array index.
        /// </summary>
        private static string indexToString(uint index) => index.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Sets the properties on the prototype of the Array class.
        /// </summary>
        ///
        /// <param name="propDict">A dictionary containing the properties to be set to the Array
        /// prototype.</param>
        ///
        /// <remarks>
        /// Calling this method will acquire a global lock to ensure that tests that depend on the
        /// state of the global Array prototype are not run concurrently. Call
        /// <see cref="resetPrototypeProperties"/> at the end of the test to release the lock.
        /// </remarks>
        private static void setPrototypeProperties(Dictionary<uint, ASAny> propDict) {
            if (!Monitor.IsEntered(s_protoGlobalLock))
                Monitor.Enter(s_protoGlobalLock);

            if (s_currentProtoProperties != null) {
                foreach (var key in s_currentProtoProperties.Keys)
                    s_arrayClassPrototype.AS_deleteProperty(indexToString(key));
            }

            if (propDict != null) {
                foreach (var (key, val) in propDict)
                    s_arrayClassPrototype.AS_setProperty(indexToString(key), val);
            }

            s_currentProtoProperties = propDict;
        }

        /// <summary>
        /// Removes all properties that were set on the Array prototype using the
        /// <see cref="setPrototypeProperties"/> method. Call this at the end of a test that
        /// involves modifying the Array class prototype.
        /// </summary>
        private static void resetPrototypeProperties() {
            if (s_currentProtoProperties != null) {
                foreach (var key in s_currentProtoProperties.Keys)
                    s_arrayClassPrototype.AS_deleteProperty(indexToString(key));

                s_currentProtoProperties = null;
            }

            if (Monitor.IsEntered(s_protoGlobalLock))
                Monitor.Exit(s_protoGlobalLock);
        }

        public enum MutationKind {
            SET,
            SET_INDEX,
            DELETE,
            SET_LENGTH,
            PUSH,
            POP,
            SHIFT,
            UNSHIFT,
        }

        /// <summary>
        /// Represents a mutation that can be applied to an Array instance or an image.
        /// </summary>
        public readonly struct Mutation {
            public readonly MutationKind kind;
            public readonly object[] args;

            internal Mutation(MutationKind kind, params object[] args) {
                this.kind = kind;
                this.args = args;
            }

            public override string ToString() {
                string argsStr = (args.Length == 1 && args[0] is ASAny[] arr)
                    ? String.Join(", ", arr)
                    : String.Join(", ", args);

                return kind.ToString().ToLowerInvariant() + "(" + argsStr + ")";
            }
        }

        private static Mutation mutSetInt(uint index, ASAny value) => new Mutation(MutationKind.SET, (int)index, value);

        private static Mutation mutSetUint(uint index, ASAny value) => new Mutation(MutationKind.SET, index, value);

        private static Mutation mutSetNumber(uint index, ASAny value) => new Mutation(MutationKind.SET, (double)index, value);

        private static Mutation mutSetIndexInt(uint index, ASAny value) => new Mutation(MutationKind.SET_INDEX, (int)index, value);

        private static Mutation mutSetIndexUint(uint index, ASAny value) => new Mutation(MutationKind.SET_INDEX, index, value);

        private static Mutation mutDelInt(uint index) => new Mutation(MutationKind.DELETE, (int)index);

        private static Mutation mutDelUint(uint index) => new Mutation(MutationKind.DELETE, index);

        private static Mutation mutDelNumber(uint index) => new Mutation(MutationKind.DELETE, (double)index);

        private static Mutation mutSetLength(uint length) => new Mutation(MutationKind.SET_LENGTH, length);

        private static Mutation mutPushOne(ASAny value) => new Mutation(MutationKind.PUSH, value);

        private static Mutation mutPush(params ASAny[] values) => new Mutation(MutationKind.PUSH, new object[] {values});

        private static Mutation mutUnshift(params ASAny[] values) => new Mutation(MutationKind.UNSHIFT, new object[] {values});

        private static Mutation mutPop() => new Mutation(MutationKind.POP);

        private static Mutation mutShift() => new Mutation(MutationKind.SHIFT);

        /// <summary>
        /// Applies the given mutation to an array instance without any assertion checks.
        /// </summary>
        /// <param name="array">The array to mutate.</param>
        /// <param name="mutation">A <see cref="Mutation"/> representing the mutation to apply.</param>
        /// <returns>The return value of the mutation function, or null if there is no return value.</returns>
        private static object applyMutation(ASArray array, Mutation mutation) {
            switch (mutation.kind) {
                case MutationKind.SET: {
                    object index = mutation.args[0];
                    ASAny value = (ASAny)mutation.args[1];

                    if (index is int indexInt)
                        array.AS_setElement(indexInt, value);
                    else if (index is uint indexUint)
                        array.AS_setElement(indexUint, value);
                    else if (index is double indexNum)
                        array.AS_setElement(indexNum, value);

                    return null;
                }

                case MutationKind.SET_INDEX: {
                    ASAny value = (ASAny)mutation.args[1];

                    if (mutation.args[0] is int iIndex)
                        array[iIndex] = value;
                    else if (mutation.args[0] is uint uIndex)
                        array[uIndex] = value;

                    return null;
                }

                case MutationKind.DELETE: {
                    object index = mutation.args[0];

                    if (index is int indexInt)
                        return array.AS_deleteElement(indexInt);
                    else if (index is uint indexUint)
                        return array.AS_deleteElement(indexUint);
                    else if (index is double indexNum)
                        return array.AS_deleteElement(indexNum);
                    else
                        return false;
                }

                case MutationKind.SET_LENGTH: {
                    uint newLength = (uint)mutation.args[0];
                    array.length = newLength;
                    return null;
                }

                case MutationKind.PUSH: {
                    if (mutation.args[0] is ASAny[] elements)
                        return array.push(new RestParam(elements));
                    else
                        return array.push((ASAny)mutation.args[0]);
                }

                case MutationKind.UNSHIFT:
                    return array.unshift(new RestParam((ASAny[])mutation.args[0]));

                case MutationKind.POP:
                    return array.pop();

                case MutationKind.SHIFT:
                    return array.shift();
            }

            return null;
        }

        /// <summary>
        /// Applies the given mutation to an array image without any assertion checks.
        /// </summary>
        ///
        /// <param name="image">An image representing array to mutate. This will be updated with the new image.</param>
        /// <param name="mutation">A <see cref="Mutation"/> representing the mutation to apply.</param>
        ///
        /// <returns>The return value of the mutation function (if it had been applied to a real array),
        /// or null if there is no return value.</returns>
        private static object applyMutation(ref Image image, Mutation mutation) {
            switch (mutation.kind) {
                case MutationKind.SET: {
                    object index = mutation.args[0];
                    ASAny value = (ASAny)mutation.args[1];

                    uint indexUint = 0;
                    if (index is uint u)
                        indexUint = u;
                    else if (index is int i)
                        indexUint = (uint)i;
                    else if (index is double d)
                        indexUint = (uint)d;

                    image.elements[indexUint] = value;
                    image = new Image(Math.Max(image.length, indexUint + 1), image.elements);

                    return null;
                }

                case MutationKind.SET_INDEX: {
                    ASAny value = (ASAny)mutation.args[1];

                    uint index;
                    if (mutation.args[0] is int iIndex)
                        index = (uint)iIndex;
                    else
                        index = (uint)mutation.args[0];

                    image.elements[index] = value;
                    image = new Image(Math.Max(image.length, index + 1), image.elements);
                    return null;
                }

                case MutationKind.DELETE: {
                    object index = mutation.args[0];

                    if (index is int indexInt)
                        return image.elements.Remove((uint)indexInt);
                    else if (index is uint indexUint)
                        return image.elements.Remove(indexUint);
                    else if (index is double indexNum)
                        return image.elements.Remove((uint)indexNum);

                    return null;
                }

                case MutationKind.SET_LENGTH: {
                    uint newLength = (uint)mutation.args[0];
                    Image newImage = makeEmptyImage(newLength);

                    foreach (var (key, val) in image.elements) {
                        if (key < newLength)
                            newImage.elements[key] = val;
                    }

                    image = newImage;
                    return null;
                }

                case MutationKind.PUSH: {
                    if (mutation.args[0] is ASAny[] elements) {
                        uint elementCount = Math.Min((uint)elements.Length, UInt32.MaxValue - image.length);

                        for (uint i = 0; i < elementCount; i++)
                            image.elements[image.length + i] = elements[(int)i];

                        image = new Image(image.length + elementCount, image.elements);
                    }
                    else if (image.length != UInt32.MaxValue) {
                        image.elements[image.length] = (ASAny)mutation.args[0];
                        image = new Image(image.length + 1, image.elements);
                    }

                    return image.length;
                }

                case MutationKind.SHIFT: {
                    if (image.length == 0)
                        return ASAny.undefined;

                    var newImage = makeEmptyImage(image.length - 1);
                    image.elements.TryGetValue(0, out ASAny removedValue);

                    foreach (var (key, val) in image.elements) {
                        if (key != 0)
                            newImage.elements[key - 1] = val;
                    }

                    image = newImage;
                    return removedValue;
                }

                case MutationKind.POP: {
                    ASAny removedValue = default;
                    if (image.length != 0) {
                        image.elements.Remove(image.length - 1, out removedValue);
                        image = new Image(image.length - 1, image.elements);
                    }
                    return removedValue;
                }

                case MutationKind.UNSHIFT: {
                    ASAny[] elements = (ASAny[])mutation.args[0];
                    uint shift = (uint)elements.Length;

                    Image newImage = makeEmptyImage(image.length + Math.Min(UInt32.MaxValue - image.length, shift));

                    for (uint i = 0; i < elements.Length; i++)
                        newImage.elements[i] = elements[(int)i];

                    foreach (var (key, val) in image.elements) {
                        if (key < UInt32.MaxValue - shift)
                            newImage.elements[key + shift] = val;
                    }

                    image = newImage;
                    return newImage.length;
                }
            }

            return default;
        }

        /// <summary>
        /// Applies a list of mutations to an array instance without any assertion checks.
        /// </summary>
        /// <param name="array">The array to mutate.</param>
        /// <param name="mutation">The mutations to apply.</param>
        private static void applyMutations(ASArray array, IEnumerable<Mutation> mutations) {
            if (mutations == null)
                return;

            foreach (Mutation m in mutations)
                applyMutation(array, m);
        }

        /// <summary>
        /// Applies a list of mutations to an <see cref="Image"/> instance without any assertion checks.
        /// </summary>
        /// <param name="image">The <see cref="Image"/> to mutate.</param>
        /// <param name="mutation">The mutations to apply.</param>
        private static void applyMutations(ref Image image, IEnumerable<Mutation> mutations) {
            if (mutations == null)
                return;

            foreach (Mutation m in mutations)
                applyMutation(ref image, m);
        }

        /// <summary>
        /// Creates an Array instance and its corresponding image by starting with an empty array of length 0 and
        /// applying the given mutations in order.
        /// </summary>
        private static (ASArray, Image) makeArrayAndImageWithMutations(IEnumerable<Mutation> mutations) {
            var array = new ASArray();
            var image = makeEmptyImage();

            foreach (Mutation m in mutations) {
                applyMutation(array, m);
                applyMutation(ref image, m);
            }

            return (array, image);
        }

        /// <summary>
        /// Applies a mutation simultaneously to both an Array instance and an image of it, and
        /// verifies that the new state of the array following the mutation matches the new image.
        /// </summary>
        /// <param name="array">The array to mutate.</param>
        /// <param name="image">The image that represents the state of <paramref name="array"/> before
        /// the mutation is applied. This will be updated with the new image after mutation.</param>
        /// <param name="mutation">The <see cref="Mutation"/> representing the mutation to apply
        /// to the array and image.</param>
        private static void applyMutationAndVerify(ASArray array, ref Image image, Mutation mutation) {
            applyMutation(array, mutation);
            applyMutation(ref image, mutation);
            verifyArrayMatchesImage(array, image);
        }

        /// <summary>
        /// Verifies that an Array instance matches the given image. Any mismatch is a failed
        /// assertion.
        /// </summary>
        /// <param name="array">An Array instance.</param>
        /// <param name="image">The image to match with the array.</param>
        /// <param name="primitiveValueEqual">If this is true, use value equality instead of object
        /// identity for primitive types.</param>
        /// <param name="dontSampleOutsideArrayLength">If this is set to true, skip the checks on random indices
        /// greater than the array's length (to verify that they don't contain values).</param>
        private static void verifyArrayMatchesImage(
            ASArray array, Image image, bool primitiveValueEqual = false, bool dontSampleOutsideArrayLength = false)
        {
            void assertEqual(ASAny expected, ASAny actual) {
                if (primitiveValueEqual)
                    AssertHelper.valueIdentical(expected, actual);
                else
                    AssertHelper.identical(expected, actual);
            }

            // Length property check
            Assert.Equal(image.length, array.length);

            // Elements in the array
            foreach (var (imageKey, imageVal) in image.elements)
                checkIndexHasValue(imageKey, imageVal);

            // Properties on the Array prototype with index-like keys
            if (s_currentProtoProperties != null) {
                foreach (var (protoKey, protoVal) in s_currentProtoProperties) {
                    if (!image.elements.ContainsKey(protoKey))
                        checkIndexHasValue(protoKey, protoVal, true);
                }
            }

            const uint smallArrayLength = 500;
            const int lessThanLengthSampleCount = 500;
            const int moreThanLengthSampleCount = 500;

            var sampledSet = new HashSet<uint>();

            if (image.length <= smallArrayLength) {
                // If the length of the array is small then do an exhaustive check for all empty
                // slots from 0 to the array length.
                for (uint i = 0; i < image.length; i++) {
                    if (!isIndexInImageOrPrototype(i))
                        checkIndexIsEmpty(i);
                }
            }
            else {
                // For large arrays, check a random sample of indices.
                for (int i = 0; i < lessThanLengthSampleCount; i++) {
                    uint index = randomIndex(image.length);
                    if (sampledSet.Add(index) && !isIndexInImageOrPrototype(index))
                        checkIndexIsEmpty(index);
                }
            }

            // Check a random sample of indices greater than or equal to the array length
            // and verify that they do not contain any value.
            if (image.length < UInt32.MaxValue && !dontSampleOutsideArrayLength) {
                for (int i = 0; i < moreThanLengthSampleCount; i++) {
                    uint index = image.length + randomIndex(UInt32.MaxValue - image.length);
                    if (sampledSet.Add(index))
                        checkIndexIsEmpty(index);
                }
            }

            void checkIndexHasValue(uint index, ASAny expectedValue, bool isOnPrototype = false) {
                Assert.True(array.AS_hasElement(index));
                Assert.True(array.AS_hasElement((double)index));

                if (index <= (uint)Int32.MaxValue)
                    Assert.True(array.AS_hasElement((int)index));

                assertEqual(expectedValue, array.AS_getElement(index));

                if (index <= (uint)Int32.MaxValue) {
                    assertEqual(expectedValue, array.AS_getElement((int)index));
                    assertEqual(expectedValue, array[(int)index]);
                }

                assertEqual(expectedValue, array.AS_getElement((double)index));
                assertEqual(expectedValue, array[index]);

                if (isOnPrototype)
                    Assert.False(array.AS_deleteElement(index));
            }

            void checkIndexIsEmpty(uint index) {
                Assert.False(array.AS_hasElement(index));

                if (index <= (uint)Int32.MaxValue)
                    Assert.False(array.AS_hasElement((int)index));

                Assert.False(array.AS_hasElement((double)index));

                AssertHelper.identical(ASAny.undefined, array.AS_getElement(index));

                if (index <= (uint)Int32.MaxValue) {
                    AssertHelper.identical(ASAny.undefined, array.AS_getElement((int)index));
                    AssertHelper.identical(ASAny.undefined, array[(int)index]);
                }

                AssertHelper.identical(ASAny.undefined, array.AS_getElement((double)index));
                AssertHelper.identical(ASAny.undefined, array[index]);
            }

            bool isIndexInImageOrPrototype(uint index) =>
                image.elements.ContainsKey(index) || (s_currentProtoProperties != null && s_currentProtoProperties.ContainsKey(index));
        }

        /// <summary>
        /// Generates a random dense array
        /// </summary>
        ///
        /// <param name="length">The length of the array.</param>
        /// <param name="valueDomain">The domain from which to randomly choose the array elements. An
        /// element from this array may be selected more than once, or may not be selected at all.</param>
        ///
        /// <returns>A tuple containing created <see cref="ASArray"/> instance and a corresponding
        /// <see cref="Image"/> instance.</returns>
        private static (ASArray, Image) makeRandomDenseArrayAndImage(int length, ASAny[] valueDomain) {
            Random rng = s_currentRandom;

            ASAny[] elements = new ASAny[length];
            for (int i = 0; i < elements.Length; i++)
                elements[i] = RandomHelper.sampleArray(rng, valueDomain);

            return (new ASArray(elements), makeImageWithValues(elements));
        }

        /// <summary>
        /// Returns an array containing unique objects. This can be used as a value domain when
        /// generating random arrays.
        /// </summary>
        /// <param name="size">The number of objects.</param>
        /// <returns>An array of length <paramref name="size"/> containing unique objects.</returns>
        private static ASAny[] makeUniqueValues(int size) {
            ASAny[] values = new ASAny[size];
            for (int i = 0; i < values.Length; i++)
                values[i] = new ConvertibleMockObject(stringValue: "#" + i);
            return values;
        }

        /// <summary>
        /// Creates a dense array for the given <see cref="Image"/> instance. The elements
        /// of the created array include those in <paramref name="image"/> and those in the
        /// current Array prototype (for which no element exists in <paramref name="image"/>
        /// at the corresponding index).
        /// </summary>
        private static ASAny[] getArrayElementsFromImage(in Image image) {
            uint length = Math.Min(image.length, (uint)Int32.MaxValue);
            ASAny[] arr = new ASAny[(int)length];

            if (s_currentProtoProperties != null) {
                foreach (var (k, v) in s_currentProtoProperties) {
                    if (k < length)
                        arr[(int)k] = v;
                }
            }

            foreach (var (k, v) in image.elements) {
                if (k < length)
                    arr[(int)k] = v;
            }

            return arr;
        }

        /// <summary>
        /// Creates a mutation that sets the element at the given index to the given value, with the
        /// index type being randomly chosen.
        /// </summary>
        private static Mutation mutSetRandomIndexType(uint index, ASAny value) {
            var choice = s_currentRandom.Next(4);
            switch (choice) {
                case 0:
                    return (index <= (uint)Int32.MaxValue) ? mutSetInt(index, value) : mutSetUint(index, value);
                case 1:
                    return mutSetUint(index, value);
                case 2:
                    return mutSetNumber(index, value);
                case 3:
                    return (index <= (uint)Int32.MaxValue) ? mutSetIndexInt(index, value) : mutSetIndexUint(index, value);
                default:
                    return mutSetIndexUint(index, value);
            }
        }

        /// <summary>
        /// Creates a mutation that deletes the element at the given index to the given value, with the
        /// index type being randomly chosen.
        /// </summary>
        private static Mutation mutDelRandomIndexType(uint index) {
            var choice = s_currentRandom.Next(3);
            switch (choice) {
                case 0:
                    return (index <= (uint)Int32.MaxValue) ? mutDelInt(index) : mutDelUint(index);
                case 1:
                    return mutDelUint(index);
                default:
                    return mutDelNumber(index);
            }
        }

        /// <summary>
        /// Calls the given function for each integer value in the given range and returns an array
        /// containing the values returned by the function.
        /// </summary>
        /// <param name="start">The beginning value (inclusive) of the range.</param>
        /// <param name="end">The end value (inclusive) of the range.</param>
        /// <param name="selector">The function to call for each integer in the range defined
        /// by <paramref name="start"/> and <paramref name="end"/>.</param>
        private static T[] rangeSelect<T>(uint start, uint end, Func<uint, T> selector) {
            int step = (start > end) ? -1 : 1;
            T[] array = new T[Math.Abs((int)end - (int)start) + 1];
            uint currentValue = start;

            for (int i = 0; i < array.Length; i++) {
                array[i] = selector(currentValue);
                currentValue += (uint)step;
            }

            return array;
        }

        /// <summary>
        /// Calls the given function for each integer value in the given range and returns the
        /// concatenation of the arrays returned by the function.
        /// </summary>
        /// <param name="start">The beginning value (inclusive) of the range.</param>
        /// <param name="end">The end value (inclusive) of the range.</param>
        /// <param name="selector">The function to call for each integer in the range defined
        /// by <paramref name="start"/> and <paramref name="end"/>.</param>
        private static T[] rangeMultiSelect<T>(uint start, uint end, Func<uint, T[]> selector) {
            int step = (start > end) ? -1 : 1;
            int count = Math.Abs((int)end - (int)start) + 1;
            uint currentValue = start;

            List<T> list = new List<T>();

            for (int i = 0; i < count; i++) {
                list.AddRange(selector(currentValue));
                currentValue += (uint)step;
            }

            return list.ToArray();
        }

        /// <summary>
        /// Returns an array containing <paramref name="element"/>, repeated <paramref name="count"/> times.
        /// </summary>
        private static T[] repeat<T>(T element, int count) {
            T[] arr = new T[count];
            arr.AsSpan().Fill(element);
            return arr;
        }

        /// <summary>
        /// Returns a flattened list of <see cref="Mutation"/> instances constructed from the given
        /// arguments, which must be instances of <see cref="Mutation"/> or <see cref="IEnumerable{Mutation}"/>.
        /// </summary>
        private static List<Mutation> buildMutationList(params object[] mutOrMutArrays) {
            var flatList = new List<Mutation>();

            for (int i = 0; i < mutOrMutArrays.Length; i++) {
                if (mutOrMutArrays[i] is Mutation mut)
                    flatList.Add(mut);
                else
                    flatList.AddRange((IEnumerable<Mutation>)mutOrMutArrays[i]);
            }

            return flatList;
        }

        private static IEnumerable<double> generateEquivalentIndices(uint index, uint arrayLength) {
            var indices = new HashSet<double>();

            indices.Add(index);
            indices.Add(index + 0.3);
            indices.Add(index + 0.7);

            if (index == 0) {
                indices.Add(-0.5);
                indices.Add(-(double)arrayLength - 1.0);
                indices.Add(-(double)arrayLength - 100000.0);
                indices.Add(Double.NegativeInfinity);
                indices.Add(Double.NaN);
            }

            if (index == arrayLength) {
                indices.Add(arrayLength + 1.0);
                indices.Add(arrayLength + 100000.0);
                indices.Add(Double.PositiveInfinity);
            }

            if (index < arrayLength) {
                indices.Add(-(double)(arrayLength - index));
                indices.Add(-(double)(arrayLength - index) - 0.3);
                indices.Add(-(double)(arrayLength - index) - 0.7);
            }

            return indices;
        }

        private static IEnumerable<(double, double)> generateEquivalentStartEndRanges(uint start, uint end, uint arrayLength) {
            var ranges = new HashSet<(double, double)>();

            ranges.Add((start, end));
            ranges.Add((start + 0.5, end + 0.5));
            ranges.Add((start + 0.7, end + 0.3));

            if (start == end) {
                ranges.Add((start + 1.0, end));
                ranges.Add((start + 100000.0, end));
                ranges.Add((Double.PositiveInfinity, end));
            }

            if (start == 0) {
                ranges.Add((-0.5, end));
                ranges.Add((-(double)arrayLength - 1.0, end));
                ranges.Add((-(double)arrayLength - 100000.0, end));
                ranges.Add((Double.NegativeInfinity, end));
                ranges.Add((Double.NaN, end));
            }

            if (start == arrayLength) {
                ranges.Add((start + 1.0, end));
                ranges.Add((start + 100000.0, end));
                ranges.Add((Double.PositiveInfinity, end));
            }

            if (end == 0) {
                ranges.Add((start, -0.5));
                ranges.Add((start, -(double)arrayLength - 1.0));
                ranges.Add((start, -(double)arrayLength - 100000.0));
                ranges.Add((start, Double.NegativeInfinity));
                ranges.Add((start, Double.NaN));
            }

            if (end == arrayLength) {
                ranges.Add((start, end + 1.0));
                ranges.Add((start, end + 100000.0));
                ranges.Add((start, Double.PositiveInfinity));
            }

            if (start < arrayLength) {
                ranges.Add((-(double)(arrayLength - start), end));
                ranges.Add((-(double)(arrayLength - start) - 0.3, end));
                ranges.Add((-(double)(arrayLength - start) - 0.7, end));
            }

            if (end < arrayLength) {
                ranges.Add((start, -(double)(arrayLength - end)));
                ranges.Add((start, -(double)(arrayLength - end) - 0.3));
                ranges.Add((start, -(double)(arrayLength - end) - 0.7));
            }

            if (start < arrayLength && end < arrayLength) {
                ranges.Add((-(double)(arrayLength - start), -(double)(arrayLength - end)));
                ranges.Add((-(double)(arrayLength - start) - 0.5, -(double)(arrayLength - end) - 0.5));
                ranges.Add((-(double)(arrayLength - start) - 0.7, -(double)(arrayLength - end) - 0.3));
            }

            if (start == 0 && end == 0) {
                ranges.Add((-(double)arrayLength - 100000.0, -(double)arrayLength - 100000.0));
                ranges.Add((-(double)arrayLength - 100000.0, -(double)arrayLength - 200000.0));
                ranges.Add((-(double)arrayLength - 200000.0, -(double)arrayLength - 100000.0));
                ranges.Add((Double.NegativeInfinity, Double.NegativeInfinity));
                ranges.Add((Double.NaN, Double.NaN));
            }

            if (start == end && end > 0) {
                ranges.Add((start, end - 1.0));
                ranges.Add((start, 0.0));
                ranges.Add((start, -(double)(arrayLength - end) - 1.0));
                ranges.Add((start, -(double)arrayLength));
            }

            if (start == arrayLength && end == arrayLength) {
                ranges.Add((start + 100000.0, end + 100000.0));
                ranges.Add((start + 100000.0, end + 200000.0));
                ranges.Add((start + 200000.0, end + 100000.0));
                ranges.Add((Double.PositiveInfinity, Double.PositiveInfinity));
            }

            return ranges;
        }

        private IEnumerable<(double, double)> generateEquivalentStartLengthRanges(uint start, uint length, uint arrayLength) {
            var ranges = new HashSet<(double, double)>();

            ranges.Add((start, length));
            ranges.Add((start + 0.5, length + 0.5));
            ranges.Add((start + 0.7, length + 0.3));

            if (start == 0) {
                ranges.Add((-0.5, length));
                ranges.Add((-(double)arrayLength - 1.0, length));
                ranges.Add((-(double)arrayLength - 100000.0, length));
                ranges.Add((Double.NegativeInfinity, length));
                ranges.Add((Double.NaN, length));
            }

            if (start == arrayLength) {
                ranges.Add((arrayLength + 1.0, length));
                ranges.Add((arrayLength + 100000.0, length));
                ranges.Add((Double.PositiveInfinity, length));
            }

            if (start < arrayLength) {
                ranges.Add((-(double)(arrayLength - start), length));
                ranges.Add((-(double)(arrayLength - start) - 0.3, length));
                ranges.Add((-(double)(arrayLength - start) - 0.7, length));
            }

            if (length == 0) {
                ranges.Add((start, -0.5));
                ranges.Add((start, -1));
                ranges.Add((start, Double.NegativeInfinity));
                ranges.Add((start, Double.NaN));
            }

            if (length == arrayLength - start) {
                ranges.Add((start, length + 1.0));
                ranges.Add((start, length + 100000.0));
                ranges.Add((start, Double.PositiveInfinity));
            }

            if (start == arrayLength && length == 0) {
                ranges.Add((start + 2000.0, 1000.0));
                ranges.Add((Double.PositiveInfinity, Double.PositiveInfinity));
            }

            if (start == 0 && length == 0) {
                ranges.Add((-0.9, 0.9));
                ranges.Add((0.9, -9999.9));
                ranges.Add((Double.NaN, Double.NaN));
            }

            if (start < arrayLength && length == arrayLength - start) {
                ranges.Add((-(double)(arrayLength - start), length + 1.0));
                ranges.Add((-(double)(arrayLength - start), length + 100000.0));
                ranges.Add((-(double)(arrayLength - start), Double.PositiveInfinity));
            }

            return ranges;
        }

    }

}
