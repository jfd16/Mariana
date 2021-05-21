using System;
using System.Collections.Generic;
using System.Text;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    using KVPair = KeyValuePair<string, string>;

    /// <summary>
    /// Represents additional metadata associated with an AVM2 trait.
    /// </summary>
    /// <remarks>
    /// Metadata in a tag is represented by key-value pairs of strings. The keys do not necessarily have to
    /// be unique, and may be null.
    /// </remarks>
    public sealed class MetadataTag {

        private string m_name;
        private KVPair[] m_keyvalues;

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
            m_keyvalues = (keys.Length != 0) ? new KVPair[keys.Length] : Array.Empty<KVPair>();

            for (int i = 0; i < keys.Length; i++)
                m_keyvalues[i] = new KVPair(keys[i], values[i]);
        }

        /// <summary>
        /// Gets the name of this metadata tag.
        /// </summary>
        public string name => m_name;

        /// <summary>
        /// Gets a Boolean value indicating whether the metadata tag contains a key-value pair with
        /// the given key.
        /// </summary>
        /// <param name="key">The key. This must not be null.</param>
        /// <returns>True if a key-value pair with the given key exists, false otherwise.</returns>
        /// <exception cref="AVM2Exception">ArgumentError #10060: <paramref name="key"/> is null.</exception>
        public bool hasValue(string key) {
            if (key == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(key));

            var kvs = m_keyvalues;
            for (int i = 0; i < kvs.Length; i++) {
                if (kvs[i].Key == key)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the value of the key-value pair in the tag with the specified key.
        /// </summary>
        /// <returns>The value of the key-value pair in the tag with the specified key. If no key-value
        /// pair with the key exists, returns null. If multiple key-value pairs exist with the given key,
        /// returns the value of the last such pair.</returns>
        /// <param name="key">The key. This must not be null.</param>
        /// <exception cref="AVM2Exception">ArgumentError #10060: <paramref name="key"/> is null.</exception>
        public string this[string key] {
            get {
                if (key == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(key));

                var kvs = m_keyvalues;
                for (int i = kvs.Length - 1; i >= 0; i--) {
                    if (kvs[i].Key == key)
                        return kvs[i].Value;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets all the key-value pairs in this tag.
        /// </summary>
        /// <returns>A read-only array view of of <see cref="KeyValuePair{String, String}"/> instances
        /// containing the key-value pairs in this tag.</returns>
        public ReadOnlyArrayView<KVPair> getKeyValuePairs() => new ReadOnlyArrayView<KVPair>(m_keyvalues);

        /// <summary>
        /// Returns a string representation of this tag.
        /// </summary>
        /// <returns>A string representation of the current tag.</returns>
        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            appendEscaped(m_name);

            var keyvalues = m_keyvalues;

            if (keyvalues.Length > 0) {
                sb.Append('(');
                for (int i = 0; i < keyvalues.Length; i++) {
                    if (i != 0)
                        sb.Append(',').Append(' ');

                    if (keyvalues[i].Key != null) {
                        appendEscaped(keyvalues[i].Key);
                        sb.Append(" = ");
                    }
                    appendEscaped(keyvalues[i].Value);
                }
                sb.Append(')');
            }

            sb.Append(']');
            return sb.ToString();

            void appendEscaped(string strToEscape) {
                strToEscape = strToEscape ?? "null";
                bool mustEscape = strToEscape.Length == 0;

                for (int i = 0; i < strToEscape.Length && !mustEscape; i++) {
                    char c = strToEscape[i];
                    if (c >= 0x80) {
                        mustEscape = true;
                    }
                    else {
                        mustEscape =
                            c != '_'
                            && (uint)(c - '0') > 9
                            && (uint)(c - 'A') > 'Z' - 'A'
                            && (uint)(c - 'a') > 'z' - 'a';
                    }
                }

                if (!mustEscape) {
                    sb.Append(strToEscape);
                    return;
                }

                sb.Append('"');
                for (int i = 0; i < strToEscape.Length; i++) {
                    char ch = strToEscape[i];
                    if (ch == '\\' || ch == '"')
                        sb.Append('\\');
                    sb.Append(ch);
                }
                sb.Append('"');
            }
        }
    }

    /// <summary>
    /// Represents a collection of metadata tags associated with a trait.
    /// </summary>
    public sealed class MetadataTagCollection {

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
            m_tags = tags.ToArray();
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
        /// in the collection, the first one is returned.</returns>
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="name"/> is null.</description></item>
        /// </list>
        /// </exception>
        public MetadataTag getTag(string name) {
            if (name == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(name));

            var tags = m_tags;
            for (int i = 0; i < tags.Length; i++) {
                if (tags[i].name == name)
                    return tags[i];
            }

            return null;
        }

    }
}
