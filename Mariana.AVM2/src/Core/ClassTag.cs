using System;
using System.Runtime.CompilerServices;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Class tags are used to identify certain core classes in the AVM2.
    /// </summary>
    ///
    /// <remarks>
    /// The tag of a class can be obtained using the <see cref="Class.tag" qualifyHint="true"/>
    /// property and can be used to check if an object is an instance of a type whose tag is in
    /// this list.
    /// </remarks>
    ///
    /// <seealso cref="Class.tag" qualifyHint="true" />
    public enum ClassTag : byte {

        /// <summary>
        /// Any class which is not represented by any other tag in this enumeration.
        /// </summary>
        OBJECT,

        /// <summary>
        /// The <c>int</c> class.
        /// </summary>
        INT,

        /// <summary>
        /// The <c>uint</c> class.
        /// </summary>
        UINT,

        /// <summary>
        /// The <c>Number</c> class.
        /// </summary>
        NUMBER,

        /// <summary>
        /// The <c>Boolean</c> class.
        /// </summary>
        BOOLEAN,

        /// <summary>
        /// The <c>String</c> class.
        /// </summary>
        STRING,

        /// <summary>
        /// The <c>QName</c> class.
        /// </summary>
        QNAME,

        /// <summary>
        /// The <c>Namespace</c> class.
        /// </summary>
        NAMESPACE,

        /// <summary>
        /// The <c>XML</c> class.
        /// </summary>
        XML,

        /// <summary>
        /// The <c>XMLList</c> class.
        /// </summary>
        XML_LIST,

        /// <summary>
        /// The <c>Function</c> class.
        /// </summary>
        FUNCTION,

        /// <summary>
        /// The <c>Array</c> class.
        /// </summary>
        ARRAY,

        /// <summary>
        /// Any instantiation of the Vector class, i.e. <c>Vector.&lt;T&gt;</c> for some type T.
        /// (Note: This tag is not used for the uninstantiated <c>Vector</c> class).
        /// </summary>
        VECTOR,

        /// <summary>
        /// The <c>Date</c> class.
        /// </summary>
        DATE,

        /// <summary>
        /// The <c>RegExp</c> class.
        /// </summary>
        REGEXP,

    }

    /// <summary>
    /// A bitset for fast type checking of objects based on <see cref="ClassTag"/> values.
    /// </summary>
    internal readonly struct ClassTagSet {

        private readonly int m_mask;

        /// <summary>
        /// Contains the int and uint types.
        /// </summary>
        public static readonly ClassTagSet integer = new ClassTagSet(ClassTag.INT, ClassTag.UINT);

        /// <summary>
        /// Contains all numeric types (int, uint and Number).
        /// </summary>
        public static readonly ClassTagSet numeric = new ClassTagSet(ClassTag.INT, ClassTag.UINT, ClassTag.NUMBER);

        /// <summary>
        /// Contains all numeric types and Boolean.
        /// </summary>
        public static readonly ClassTagSet numericOrBool =
            new ClassTagSet(ClassTag.INT, ClassTag.UINT, ClassTag.NUMBER, ClassTag.BOOLEAN);

        /// <summary>
        /// Contains all primitive types (int, uint, Number, Boolean, String)
        /// </summary>
        public static readonly ClassTagSet primitive =
            new ClassTagSet(ClassTag.INT, ClassTag.UINT, ClassTag.NUMBER, ClassTag.STRING, ClassTag.BOOLEAN);

        /// <summary>
        /// Contains the String and Date types.
        /// </summary>
        public static readonly ClassTagSet stringOrDate = new ClassTagSet(ClassTag.STRING, ClassTag.DATE);

        /// <summary>
        /// Contains the XML and XMLList types.
        /// </summary>
        public static readonly ClassTagSet xmlOrXmlList = new ClassTagSet(ClassTag.XML, ClassTag.XML_LIST);

        /// <summary>
        /// Contains the Array and Vector types.
        /// </summary>
        public static readonly ClassTagSet arrayLike = new ClassTagSet(ClassTag.ARRAY, ClassTag.VECTOR);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ClassTagSet(int mask) => m_mask = mask;

        /// <summary>
        /// Creates a new <see cref="ClassTagSet"/> containing one tag.
        /// </summary>
        /// <param name="tag1">The tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet(ClassTag tag1) => m_mask = 1 << (int)tag1;

        /// <summary>
        /// Creates a new <see cref="ClassTagSet"/> containing two tags.
        /// </summary>
        /// <param name="tag1">The first tag.</param>
        /// <param name="tag2">The second tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet(ClassTag tag1, ClassTag tag2) => m_mask = 1 << (int)tag1 | 1 << (int)tag2;

        /// <summary>
        /// Creates a new <see cref="ClassTagSet"/> containing three tags.
        /// </summary>
        /// <param name="tag1">The first tag.</param>
        /// <param name="tag2">The second tag.</param>
        /// <param name="tag3">The third tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet(ClassTag tag1, ClassTag tag2, ClassTag tag3) =>
            m_mask = 1 << (int)tag1 | 1 << (int)tag2 | 1 << (int)tag3;

        /// <summary>
        /// Creates a new <see cref="ClassTagSet"/> containing four tags.
        /// </summary>
        /// <param name="tag1">The first tag.</param>
        /// <param name="tag2">The second tag.</param>
        /// <param name="tag3">The third tag.</param>
        /// <param name="tag4">The fourth tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet(ClassTag tag1, ClassTag tag2, ClassTag tag3, ClassTag tag4) =>
            m_mask = 1 << (int)tag1 | 1 << (int)tag2 | 1 << (int)tag3 | 1 << (int)tag4;

        /// <summary>
        /// Creates a new <see cref="ClassTagSet"/> containing five tags.
        /// </summary>
        /// <param name="tag1">The first tag.</param>
        /// <param name="tag2">The second tag.</param>
        /// <param name="tag3">The third tag.</param>
        /// <param name="tag4">The fourth tag.</param>
        /// <param name="tag5">The fifth tag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet(ClassTag tag1, ClassTag tag2, ClassTag tag3, ClassTag tag4, ClassTag tag5) =>
            m_mask = 1 << (int)tag1 | 1 << (int)tag2 | 1 << (int)tag3 | 1 << (int)tag4 | 1 << (int)tag5;

        /// <summary>
        /// Returns a new <see cref="ClassTagSet"/> containing the tags in this set in addition
        /// to the given tag.
        /// </summary>
        /// <param name="tag">The tag to add to the current set.</param>
        /// <returns>The new <see cref="ClassTagSet"/> with <paramref name="tag"/> added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet add(ClassTag tag) => new ClassTagSet(m_mask | (1 << (int)tag));

        /// <summary>
        /// Returns a new <see cref="ClassTagSet"/> containing the tags in this set in addition
        /// to those in the given set.
        /// </summary>
        /// <param name="tagSet">The set of tags to add to the current set.</param>
        /// <returns>The new <see cref="ClassTagSet"/> with the tags in <paramref name="tagSet"/> added.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ClassTagSet add(ClassTagSet tagSet) => new ClassTagSet(m_mask | tagSet.m_mask);

        /// <summary>
        /// Returns a value indicating whether this set contains the given tag.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns>True if this set contains <paramref name="tag"/>, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool contains(ClassTag tag) => (m_mask & (1 << (int)tag)) != 0;

        /// <summary>
        /// Returns a value indicating whether this set contains all of the tags in the
        /// given set.
        /// </summary>
        /// <param name="set">The tag set.</param>
        /// <returns>True if this set contains all tags in <paramref name="set"/>, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool containsAll(ClassTagSet set) => (m_mask | set.m_mask) == m_mask;

        /// <summary>
        /// Returns a value indicating whether this set contains at least one of the tags in the
        /// given set.
        /// </summary>
        /// <param name="set">The tag set.</param>
        /// <returns>True if this set contains at least one tag in <paramref name="set"/>, otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool containsAny(ClassTagSet set) => (m_mask & set.m_mask) != 0;

        /// <summary>
        /// Returns a value indicating whether this set contains only one tag which is equal
        /// to the given tag.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns>True if this set contains exactly one tag and it is equal to <paramref name="tag"/>,
        /// otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool isSingle(ClassTag tag) => m_mask == 1 << (int)tag;

    }

}
