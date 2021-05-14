using System;
using System.Collections.Generic;
using System.Text;

namespace Mariana.AVM2.Tests.Helpers {

    internal static class RandomHelper {

        /// <summary>
        /// Generates a random string.
        /// </summary>
        /// <param name="random">A <see cref="Random"/> instance to use for generating the
        /// random string.</param>
        /// <param name="minLength">The minimum length (inclusive) of the string to be generated.</param>
        /// <param name="maxLength">The maximum length (inclusive) of the string to be generated.</param>
        /// <param name="minChar">The minimum character value (inclusive) of the range of characters to be
        /// used for generating the string.</param>
        /// <param name="maxChar">The maximum character value (inclusive) of the range of characters to be
        /// used for generating the string.</param>
        /// <returns>A randomly generated string.</returns>
        public static string randomString(
            Random random, int minLength, int maxLength, char minChar = Char.MinValue, char maxChar = Char.MaxValue)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < minLength; i++)
                sb.Append((char)random.Next(minChar, maxChar + 1));

            for (int i = minLength; i < maxLength; i++) {
                int sample = random.Next(minChar, maxChar + 2);
                if (sample == maxChar + 1)
                    break;
                sb.Append((char)sample);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Picks a random element from an array.
        /// </summary>
        /// <typeparam name="T">The element type of the array.</typeparam>
        /// <param name="random">An instance of <see cref="Random"/> to use for generating the
        /// sample index.</param>
        /// <param name="array">The array from which to sample.</param>
        /// <returns>A randomly chosen element from <paramref name="array"/>.</returns>
        public static T sampleArray<T>(Random random, T[] array) => array[random.Next(array.Length)];

        /// <summary>
        /// Generates a stream of objects sampled by randomly calling functions from the given list.
        /// </summary>
        /// <param name="random">An instance of <see cref="Random"/>.</param>
        /// <param name="funcs">The sample-generating functions. Each function must return a tuple of
        /// two elements, the first of which is a boolean. If the first element of the tuple is true,
        /// the enumerator will yield the value of the second element as the sample. If it is false,
        /// the sample is rejected and another function will be called.</param>
        /// <param name="weights">An array of weights (of the same length as <paramref name="funcs"/>).
        /// The probability of a function in <paramref name="funcs"/> being called is proportional
        /// to its corresponding weight in this array.</param>
        /// <typeparam name="T">The type of the sampled objects.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> implementation that produces the sampled objects.</returns>
        public static IEnumerable<T> sampleFunctions<T>(Random random, Func<(bool, T)>[] funcs, int[] weights) {
            int[] cumulativeWeights = new int[weights.Length];
            cumulativeWeights[0] = weights[0];

            for (int i = 1; i < weights.Length; i++)
                cumulativeWeights[i] = cumulativeWeights[i - 1] + weights[i];

            while (true) {
                int selection = random.Next(cumulativeWeights[weights.Length - 1]);
                for (int i = 0; i < cumulativeWeights.Length; i++) {
                    if (cumulativeWeights[i] > selection) {
                        var (accept, sample) = funcs[i]();
                        if (accept)
                            yield return sample;
                        break;
                    }
                }
            }
        }

    }

}
