using System;
using System.Collections.Generic;
using System.Text;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    using KVPair = KeyValuePair<string, string>;

    /// <summary>
    /// A metadata tag applied to a trait.
    /// </summary>
    /// <remarks>
    /// A metadata tag has two parts: an array of strings that may be looked up by a numeric
    /// index, and a table of key-value pairs.
    /// </remarks>
    public sealed class MetadataTag {

        private class _KeyComparer : IComparer<KVPair> {
            public static _KeyComparer instance = new _KeyComparer();
            public int Compare(KVPair x, KVPair y) => String.CompareOrdinal(x.Key, y.Key);
        }

        private const int SMALL_KEYVALUE_SIZE = 8;

        private string m_name;
        private string[] m_indexed;
        private KVPair[] m_keyvalues;

        /// <summary>
        /// Creates a new instance of <see cref="MetadataTag"/>.
        /// </summary>
        ///
        /// <param name="name">The name of the metadata tag.</param>
        /// <param name="indexed">The indexed values of the tag.</param>
        /// <param name="keys">The keys of the key-value pairs of this tag.</param>
        /// <param name="values">The values of the key-value pairs of this tag. The
        /// length of this span must be the same as that of <paramref name="keys"/></param>
        internal MetadataTag(
            string name, ReadOnlySpan<string> indexed, ReadOnlySpan<string> keys, ReadOnlySpan<string> values)
        {
            m_name = name;
            m_indexed = indexed.ToArray();

            if (keys.Length > 0) {
                m_keyvalues = new KVPair[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                    m_keyvalues[i] = new KVPair(keys[i], values[i]);

                if (keys.Length > SMALL_KEYVALUE_SIZE)
                    Array.Sort(m_keyvalues, _KeyComparer.instance);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="MetadataTag"/>.
        /// </summary>
        ///
        /// <param name="name">The name of the metadata tag.</param>
        /// <param name="keys">A span containing the keys of the key-value pairs of this tag,
        /// with null for each indexed value.</param>
        /// <param name="values">A span containing each value for the key at the corresponding
        /// index in the <paramref name="keys"/> array.</param>
        internal MetadataTag(string name, ReadOnlySpan<string> keys, ReadOnlySpan<string> values) {
            m_name = name;

            int indexedCount = 0, kvCount = 0;
            for (int i = 0; i < keys.Length; i++) {
                if (keys[i] == null)
                    indexedCount++;
                else
                    kvCount++;
            }

            m_indexed = (indexedCount != 0) ? new string[indexedCount] : Array.Empty<string>();
            m_keyvalues = (kvCount != 0) ? new KVPair[kvCount] : Array.Empty<KVPair>();

            int curIndex = 0, curKvIndex = 0;

            for (int i = 0; i < keys.Length; i++) {
                if (keys[i] == null)
                    m_indexed[curIndex++] = values[i];
                else
                    m_keyvalues[curKvIndex++] = new KVPair(keys[i], values[i]);
            }

            if (kvCount > SMALL_KEYVALUE_SIZE)
                Array.Sort(m_keyvalues, _KeyComparer.instance);
        }

        /// <summary>
        /// Gets the name of this metadata tag.
        /// </summary>
        public string name => m_name;

        /// <summary>
        /// Gets the number of indexed values in this metadata tag.
        /// </summary>
        public int indexedValueCount => m_indexed.Length;

        /// <summary>
        /// Gets a Boolean value indicating whether the metadata tag contains a key-value pair with
        /// the given key.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True if a key-value pair with the given key exists, false otherwise.</returns>
        public bool hasValue(string key) {
            if (m_keyvalues.Length > SMALL_KEYVALUE_SIZE)
                return Array.BinarySearch(m_keyvalues, new KVPair(key, null), _KeyComparer.instance) >= 0;

            var kvs = m_keyvalues;
            for (int i = 0; i < kvs.Length; i++) {
                if (kvs[i].Key == key)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets an array containing the indexed values in this tag.
        /// </summary>
        /// <returns>An array containing the indexed values in this tag.</returns>
        public ReadOnlyArrayView<string> getIndexedValues() => new ReadOnlyArrayView<string>(m_indexed);

        /// <summary>
        /// Gets all the key-value pairs in this tag.
        /// </summary>
        /// <returns>A read-only array view of of <see cref="KeyValuePair{String, String}"/> instances
        /// containing the key-value pairs in this tag.</returns>
        public ReadOnlyArrayView<KVPair> getKeyValuePairs() => new ReadOnlyArrayView<KVPair>(m_keyvalues);

        /// <summary>
        /// Gets the indexed tag value at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>ArgumentError #10061</term>
        /// <description><paramref name="index"/> is out of bounds.</description>
        /// </item>
        /// </list>
        /// </exception>
        public string this[int index] {
            get {
                if ((uint)index >= (uint)m_indexed.Length)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(index));
                return m_indexed[index];
            }
        }

        /// <summary>
        /// Gets the value of the key-value pair in the tag with the specified key. If no key-value
        /// pair with the key exists, returns null.
        /// </summary>
        /// <param name="key">The key.</param>
        public string this[string key] {
            get {
                if (m_keyvalues.Length > SMALL_KEYVALUE_SIZE) {
                    int index = Array.BinarySearch(m_keyvalues, new KVPair(key, null), _KeyComparer.instance);
                    return (index >= 0) ? m_keyvalues[index].Value : null;
                }

                var kvs = m_keyvalues;
                for (int i = 0; i < kvs.Length; i++) {
                    if (kvs[i].Key == key)
                        return kvs[i].Value;
                }

                return null;
            }
        }

        /// <summary>
        /// Returns a string representation of this tag.
        /// </summary>
        /// <returns>A string representation of the current tag.</returns>
        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            appendEscaped(m_name);

            if (m_indexed.Length == 0 && m_keyvalues.Length == 0) {
                sb.Append(']');
                return sb.ToString();
            }

            sb.Append('(');

            bool first = true;
            var indexed = m_indexed;
            var keyvalues = m_keyvalues;

            for (int i = 0; i < indexed.Length; i++) {
                appendSep();
                appendEscaped(indexed[i]);
            }

            for (int i = 0; i < keyvalues.Length; i++) {
                appendSep();
                appendEscaped(keyvalues[i].Key);
                sb.Append(" = ");
                appendEscaped(keyvalues[i].Value);
            }

            sb.Append(')');
            sb.Append(']');
            return sb.ToString();

            void appendSep() {
                if (first)
                    first = false;
                else
                    sb.Append(',').Append(' ');
            }

            void appendEscaped(string strToEscape) {
                int i, n = strToEscape.Length;
                char c;
                bool mustEscape = false;

                for (i = 0; i < n; i++) {
                    c = strToEscape[i];
                    if (((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '_') || c >= 0x80) {
                        mustEscape = true;
                        break;
                    }
                }
                if (!mustEscape) {
                    sb.Append(strToEscape);
                    return;
                }

                sb.Append('"');
                for (int j = 0; j < i; j++)
                    sb.Append(strToEscape[j]);
                while (i < n) {
                    c = strToEscape[i];
                    if (c == '"' || c == '\\')
                        sb.Append('\\');
                    sb.Append(c);
                    i++;
                }
                sb.Append('"');
            }
        }
    }

    /// <summary>
    /// Represents a collection of metadata tags associated with a trait.
    /// </summary>
    public sealed class MetadataTagCollection {

        private class _TagComparer : IComparer<MetadataTag> {
            public static _TagComparer instance = new _TagComparer();
            public int Compare(MetadataTag x, MetadataTag y) => String.CompareOrdinal(x.name, y.name);
        }

        /// <summary>
        /// A <see cref="MetadataTagCollection"/> containing no tags.
        /// </summary>
        internal static readonly MetadataTagCollection empty = new MetadataTagCollection(ReadOnlySpan<MetadataTag>.Empty);

        private MetadataTag[] m_tags;

        /// <summary>
        /// Creates a new <see cref="MetadataTagCollection"/> instance.
        /// </summary>
        /// <param name="tags">A read-only span containing the tags to store in the collection.</param>
        internal MetadataTagCollection(ReadOnlySpan<MetadataTag> tags) {
            m_tags = new MetadataTag[tags.Length];
            tags.CopyTo(m_tags);
            Array.Sort(m_tags, _TagComparer.instance);
        }

        /// <summary>
        /// Gets a <see cref="ReadOnlyArrayView{MetadataTag}"/> containing all of the
        /// metadata tags in this collection.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{MetadataTag}"/> containing all of the
        /// metadata tags in this collection.</returns>
        public ReadOnlyArrayView<MetadataTag> getTags() => new ReadOnlyArrayView<MetadataTag>(m_tags);

        /// <summary>
        /// Returns the metadata tag in this collection with the given name.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <returns>The metadata tag in this collection with the given name, or null if no
        /// tag with the name exists. If there is more than one tag with the given name
        /// in the collection, it is unspecified as to which one will be returned.</returns>
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>ArgumentError #10060</term>
        /// <description><paramref name="name"/> is null.</description>
        /// </item>
        /// </list>
        /// </exception>
        public MetadataTag getTag(string name) {
            if (name == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(name));

            if (m_tags.Length > 8) {
                int index = Array.BinarySearch(m_tags, _TagComparer.instance);
                return (index >= 0) ? m_tags[index] : null;
            }

            var tags = m_tags;
            for (int i = 0; i < tags.Length; i++) {
                if (tags[i].name == name)
                    return tags[i];
            }

            return null;
        }

        /// <summary>
        /// Returns a read-only array view containing all the metadata tags in this collection
        /// with the given name.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{MetadataTag}"/> containing all the tags in this
        /// collection whose name is equal to <paramref name="name"/>.</returns>
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>ArgumentError #10060</term>
        /// <description><paramref name="name"/> is null.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ReadOnlyArrayView<MetadataTag> getTags(string name) {
            if (name == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(name));

            var tags = m_tags;
            int startIndex = -1, endIndex = tags.Length;

            for (int i = 0; i < tags.Length; i++) {
                string tagName = tags[i].name;
                if (startIndex == -1 && tagName == name)
                    startIndex = i;
                else if (startIndex != -1 && tagName != name)
                    endIndex = i;
            }

            if (startIndex == -1)
                return default;

            return new ReadOnlyArrayView<MetadataTag>(m_tags, startIndex, endIndex - startIndex);
        }

    }
}
