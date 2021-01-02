using System;
using System.Collections.Generic;
using Mariana.Common;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The base class for all instantiations of the Vector class.
    /// </summary>
    ///
    /// <remarks>
    /// <para>This class cannot be instantiated directly; it can only be instantiated through an
    /// instance of <see cref="ASVector{T}"/>.</para>
    /// <para>
    /// This type represented by the <c>Vector.&lt;*&gt;</c> class in the AVM2. (A vector
    /// whose elements are of the "any" type and can have undefined values, i.e.
    /// <see cref="ASVector{ASAny}"/>, does not exist in the AVM2 and the <c>new
    /// Vector.&lt;*&gt;(...)</c> expression in AS3 actually creates a
    /// <c>Vector.&lt;Object&gt;</c>.)
    /// </para>
    /// </remarks>
    [AVM2ExportClass(
        name = "Vector.<*>",
        nsUri = "__AS3__.vec",
        hasPrototypeMethods = true,
        hasIndexLookupMethods = true,
        hiddenFromGlobal = true
    )]
    [AVM2ExportClassInternal(tag = ClassTag.VECTOR)]
    public class ASVectorAny : ASObject {

        // Most of this class's methods throw NotImplementedException; their actual
        // implementations are in ASVector<T>. This class should be abstract, but it
        // cannot since the AVM2 does not support abstract classes. However this is not
        // extendable by AVM2 code since this class does not export any constructor to the AVM2.

        internal ASVectorAny() { }

        /// <summary>
        /// Gets or sets the length of the Vector.
        /// </summary>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1005</term>
        /// <description>This property is set to a negative value.</description>
        /// </item>
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description>This property is changed on a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If this length is set to a new value that is less than the existing value, the Vector will
        /// be truncated to the new length. If it is set to a value greater than the existing value,
        /// the Vector will be filled with elements of the default value of the element type to make
        /// its length equal to the new length.
        /// </remarks>
        [AVM2ExportTrait]
        public int length {
            get => _VA_length;
            set => _VA_length = value;
        }

        protected private virtual int _VA_length {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// A Boolean value indicating whether the length of the vector is fixed. If the vector's
        /// length is fixed, changing it will throw an error.
        /// </summary>
        [AVM2ExportTrait]
        public bool @fixed {
            get => _VA_fixed;
            set => _VA_fixed = value;
        }

        protected private virtual bool _VA_fixed {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the Vector has an element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the Vector has an element at the given index, false
        /// otherwise.</returns>
        /// <remarks>
        /// This method returns true if the index is a positive integer less than the length of the
        /// Vector. Otherwise, it returns false.
        /// </remarks>
        public bool AS_hasElement(int index) => _VA_hasElement(index);

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1125</term>
        /// <description><paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASObject AS_getElement(int index) => _VA_getElement(index);

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1125</term>
        /// <description><paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description><paramref name="index"/> is equal to the length of the vector, and
        /// this is a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public void AS_setElement(int index, ASObject value) => _VA_setElement(index, value);

        /// <summary>
        /// Deletes the value of the element at the given index. For Vectors, this method has no
        /// effect and returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, false otherwise.</returns>
        public bool AS_deleteElement(int index) => _VA_deleteElement(index);

        /// <summary>
        /// Returns a Boolean value indicating whether the current instance has an element at the
        /// given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, otherwise
        /// false.</returns>
        /// <remarks>
        /// This method returns true if the index is a positive integer less than the length of the
        /// Vector. Otherwise, it returns false.
        /// </remarks>
        public bool AS_hasElement(uint index) => _VA_hasElement(index);

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1125</term>
        /// <description><paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASObject AS_getElement(uint index) => _VA_getElement(index);

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1125</term>
        /// <description><paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description><paramref name="index"/> is equal to the length of the vector, and this
        /// is a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public void AS_setElement(uint index, ASObject value) => _VA_setElement(index, value);

        /// <summary>
        /// Deletes the value of the element at the given index. For Vectors, this method has no
        /// effect and always returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, otherwise false.</returns>
        public bool AS_deleteElement(uint index) => _VA_deleteElement(index);

        /// <summary>
        /// Returns a Boolean value indicating whether the current instance has an element at the
        /// given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, otherwise
        /// false.</returns>
        /// <remarks>
        /// This method returns true if the index is a positive integer less than the length of the
        /// Vector. Otherwise, it returns false.
        /// </remarks>
        public bool AS_hasElement(double index) => _VA_hasElement(index);

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1125</term>
        /// <description><paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASObject AS_getElement(double index) => _VA_getElement(index);

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1125</term>
        /// <description><paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description><paramref name="index"/> is equal to the length of the vector, and
        /// this is a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public void AS_setElement(double index, ASObject value) => _VA_setElement(index, value);

        /// <summary>
        /// Deletes the value of the element at the given index. For Vectors, this method has no
        /// effect and always returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, otherwise false.</returns>
        public bool AS_deleteElement(double index) => _VA_deleteElement(index);

        protected private virtual bool _VA_hasElement(int index) => throw new NotImplementedException();
        protected private virtual bool _VA_hasElement(uint index) => throw new NotImplementedException();
        protected private virtual bool _VA_hasElement(double index) => throw new NotImplementedException();
        protected private virtual ASObject _VA_getElement(int index) => throw new NotImplementedException();
        protected private virtual ASObject _VA_getElement(uint index) => throw new NotImplementedException();
        protected private virtual ASObject _VA_getElement(double index) => throw new NotImplementedException();
        protected private virtual void _VA_setElement(int index, ASObject value) => throw new NotImplementedException();
        protected private virtual void _VA_setElement(uint index, ASObject value) => throw new NotImplementedException();
        protected private virtual void _VA_setElement(double index, ASObject value) => throw new NotImplementedException();
        protected private virtual bool _VA_deleteElement(int index) => throw new NotImplementedException();
        protected private virtual bool _VA_deleteElement(uint index) => throw new NotImplementedException();
        protected private virtual bool _VA_deleteElement(double index) => throw new NotImplementedException();

        /// <summary>
        /// Returns an instance of <see cref="IEnumerable{T}"/> that enumerates the elements of
        /// this Vector, converting them to type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The type to convert the Vector elements to.</typeparam>
        public IEnumerable<T> asEnumerable<T>() => _VA_asEnumerable<T>();

        // We can't make asEnumerable itself virtual because the JIT won't devirtualize it
        // when called on ASVector<T>, and generic virtual calls have a high performance impact.
        // See: https://github.com/dotnet/runtime/issues/32129
        protected private virtual IEnumerable<T> _VA_asEnumerable<T>() => throw new NotImplementedException();

        /// <summary>
        /// Copies the contents of the Vector into a span of the given type.
        /// </summary>
        ///
        /// <param name="srcIndex">The index into the current Vector instance from which to start
        /// copying.</param>
        /// <param name="dst">The span to copy the vector's contents to. The number of elements
        /// copied is the length of this span.</param>
        ///
        /// <typeparam name="T">The type of the destination span. If this is not the element type
        /// of the vector, the elements will be converted to the destination type.</typeparam>
        public void copyToSpan<T>(int srcIndex, Span<T> dst) => _VA_copyToSpan(srcIndex, dst);

        // We can't make copyToSpan itself virtual because the JIT won't devirtualize it
        // when called on ASVector<T>, and generic virtual calls have a high performance impact.
        // See: https://github.com/dotnet/runtime/issues/32129
        protected private virtual void _VA_copyToSpan<T>(int srcIndex, Span<T> dst) => throw new NotImplementedException();

        /// <summary>
        /// Creates and returns an array of the given type containing the vector's elements.
        /// </summary>
        /// <typeparam name="TDest">The type of the array to create. If this is not the element type
        /// of the vector, the elements will be converted to the destination type.</typeparam>
        /// <returns>An array of type <typeparamref name="TDest"/>.</returns>
        /// <remarks>
        /// The returned array is always a copy, even if <typeparamref name="TDest"/> is the same as
        /// the vector's element type.
        /// </remarks>
        public TDest[] toTypedArray<TDest>() {
            TDest[] arr = new TDest[length];
            copyToSpan<TDest>(0, arr);
            return arr;
        }

        /// <summary>
        /// Converts an object of any type into a Vector.
        /// </summary>
        /// <param name="obj">The object to convert to a Vector.</param>
        /// <returns>The given object converted to a Vector.</returns>
        ///
        /// <remarks>
        /// If the object is a Vector, the same object is returned (no copy is made). If it is an
        /// Array, a new Vector (of type <see cref="ASVector{ASObject}"/> is created having the same
        /// length as that of the Array and elements from it are copied into the new Vector. For any
        /// other object, an error is thrown.
        /// </remarks>
        public static ASVectorAny fromObject(ASObject obj) {
            if (obj is ASVectorAny vec)
                return vec;

            if (obj is ASArray array) {
                var objVec = new ASVector<ASObject>((int)array.length);
                array.copyToSpan(0, objVec.asSpan());
                return objVec;
            }

            throw ErrorHelper.createCastError(obj, typeof(ASVectorAny));
        }

        #region BindingMethodOverrides

        /// <inheritdoc/>
        public sealed override bool AS_hasProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, true, out uint index))
            {
                return AS_hasElement(index);
            }
            return base.AS_hasProperty(name, options);
        }

        /// <inheritdoc/>
        public sealed override bool AS_hasProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, true, out uint index))
            {
                return AS_hasElement(index);
            }
            return base.AS_hasProperty(name, nsSet, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_tryGetProperty(
            in QName name, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, true, out uint index))
            {
                value = AS_getElement(index);
                return BindStatus.SUCCESS;
            }
            return base.AS_tryGetProperty(name, out value, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_tryGetProperty(
            string name, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, true, out uint index))
            {
                value = AS_getElement(index);
                return BindStatus.SUCCESS;
            }
            return base.AS_tryGetProperty(name, nsSet, out value, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_trySetProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, true, out uint index))
            {
                AS_setElement(index, value.value);
                return BindStatus.SUCCESS;
            }
            return base.AS_trySetProperty(name, value, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_trySetProperty(
            string name, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, true, out uint index))
            {
                AS_setElement(index, value.value);
                return BindStatus.SUCCESS;
            }
            return base.AS_trySetProperty(name, nsSet, value, options);
        }

        /// <inheritdoc/>
        public sealed override bool AS_deleteProperty(
            in QName name, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, true, out uint index))
            {
                return AS_deleteElement(index);
            }
            return base.AS_deleteProperty(name, options);
        }

        /// <inheritdoc/>
        public sealed override bool AS_deleteProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, true, out uint index))
            {
                return AS_deleteElement(index);
            }
            return base.AS_deleteProperty(name, nsSet, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_tryCallProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, true, out uint index))
            {
                ASObject f = AS_getElement(index);
                return f.AS_tryInvoke(this, args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTFUNCTION;
            }
            return base.AS_tryCallProperty(name, args, out result, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_tryCallProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, true, out uint index))
            {
                ASObject f = AS_getElement(index);
                return f.AS_tryInvoke(this, args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTFUNCTION;
            }
            return base.AS_tryCallProperty(name, nsSet, args, out result, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_tryConstructProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, true, out uint index))
            {
                ASObject f = AS_getElement(index);
                return f.AS_tryConstruct(args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTCONSTRUCTOR;
            }
            return base.AS_tryCallProperty(name, args, out result, options);
        }

        /// <inheritdoc/>
        public sealed override BindStatus AS_tryConstructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, true, out uint index))
            {
                ASObject f = AS_getElement(index);
                return f.AS_tryConstruct(args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTCONSTRUCTOR;
            }
            return base.AS_tryCallProperty(name, nsSet, args, out result, options);
        }

        /// <summary>
        /// Gets the one-based index of the next enumerable dynamic property after the given index.
        /// </summary>
        /// <param name="index">The index of the property from where to search for the next property.
        /// A value of 0 will return the index of the first enumerable property.</param>
        /// <returns>The one-based index of the next enumerable property, or 0 if there are no more
        /// enumerable properties.</returns>
        public override int AS_nextIndex(int index) {
            int length = this.length;
            if (length == 0 || index >= length)
                return 0;
            return (index <= 0) ? 1 : index + 1;
        }

        /// <inheritdoc/>
        public override ASAny AS_nameAtIndex(int index) => ASAny.AS_fromInt(index - 1);

        /// <inheritdoc/>
        public override ASAny AS_valueAtIndex(int index) => AS_getElement(index - 1);

        #endregion

        /// <summary>
        /// Creates a copy of the Vector and appends the elements of each of the Vectors and/or Arrays
        /// given as arguments to it, in order.
        /// </summary>
        /// <param name="args">The Vectors and/or Arrays to concatenate to the copy of the current
        /// instance.</param>
        /// <returns>A copy of the current instance with the elements of all Arrays and/or Vectors
        /// concatenated to it.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1009</term>
        /// <description>One of the arguments is null.</description>
        /// </item>
        /// <item>
        /// <term>TypeError #1034</term>
        /// <description>One of the arguments is not an Array or Vector, or an element of one of the
        /// Arrays or Vectors given as arguments cannot be converted to the element type of the
        /// calling Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny concat(RestParam args = default) => _VA_concat(args);

        protected private virtual ASVectorAny _VA_concat(RestParam args) => throw new NotImplementedException();

        /// <summary>
        /// Calls a specified function for each element in the Vector, until it returns false for any
        /// element, in which case this method returns false, or the function returns true for all
        /// elements in the vector, in which case the method returns true.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Vector instance that called this method. If the callback
        /// function returns a non-Boolean value, it will be converted to a Boolean.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>True if <paramref name="callback"/> returns true for all elements in the
        /// Vector, otherwise false. If <paramref name="callback"/> is null, returns
        /// true.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item>The callback function throws an exception.</item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public bool every(ASFunction callback, ASObject thisObject = null) => _VA_every(callback, thisObject);

        protected private virtual bool _VA_every(ASFunction callback, ASObject thisObject) =>
            throw new NotImplementedException();

        /// <summary>
        /// Calls the specified callback function for each element in the Vector instance, and returns
        /// a Vector containing all elements for which the callback function returns true.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Vector instance that called this method. If the callback
        /// function returns a non-Boolean value, it will be converted to a Boolean.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>A Vector containing all elements for which the callback function returns true. If
        /// <paramref name="callback"/> is null, an empty Vector is returned.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item>The callback function throws an exception.</item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny filter(ASFunction callback, ASObject thisObject = null) => _VA_filter(callback, thisObject);

        protected private virtual ASVectorAny _VA_filter(ASFunction callback, ASObject thisObject) =>
            throw new NotImplementedException();

        /// <summary>
        /// Executes the specified callback function for each element in the Vector.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Vector instance that called this method. If the callback
        /// function returns a value, it is ignored. If this argument is null, this method does
        /// nothing.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item>The callback function throws an exception.</item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public void forEach(ASFunction callback, ASObject thisObject = null) => _VA_forEach(callback, thisObject);

        protected private virtual void _VA_forEach(ASFunction callback, ASObject thisObject) =>
            throw new NotImplementedException();

        /// <summary>
        /// Searches for the element whose value is <paramref name="searchElement"/> in the Vector,
        /// starting at the index <paramref name="fromIndex"/>, and returns the index of the first
        /// element with that value.
        /// </summary>
        ///
        /// <param name="searchElement">The value of the element to search in the Vector
        /// instance.</param>
        /// <param name="fromIndex">The index from where to start searching. If this greater than or
        /// equal to the length of the Vector, this method returns -1. If this is negative, the length
        /// of the Vector is added to it; if it is still negative after adding the length, it is set
        /// to 0.</param>
        ///
        /// <returns>The index of the first element, at or after <paramref name="fromIndex"/>, whose
        /// value is equal to <paramref name="searchElement"/>. If no element with that value is
        /// found, or if <paramref name="fromIndex"/> is equal to or greater than the length of the
        /// Vector, returns -1.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int indexOf(ASAny searchElement, int fromIndex = 0) => _VA_indexOf(searchElement, fromIndex);

        protected private virtual int _VA_indexOf(ASAny searchElement, int fromIndex) =>
            throw new NotImplementedException();

        /// <summary>
        /// Returns a string containing the string representations of all elements in the Vector
        /// concatenated with the specified separator string between values.
        /// </summary>
        /// <param name="sep">The separator string. If this is null, the default value "," is
        /// used.</param>
        /// <returns>A string containing the string representations of all elements in the Vector
        /// concatenated with <paramref name="sep"/> between values.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string join(string sep = ",") => _VA_join(sep);

        protected private virtual string _VA_join(string sep) => throw new NotImplementedException();

        /// <summary>
        /// Searches for the element whose value is <paramref name="searchElement"/> in the Vector,
        /// starting at the index <paramref name="fromIndex"/> and moving backwards, and returns the
        /// index of the first element with that value.
        /// </summary>
        ///
        /// <param name="searchElement">The value of the element to search in the Vector
        /// instance.</param>
        /// <param name="fromIndex">The index from where to start searching. If this is negative, the
        /// length of the Vector is added to it; if it is still negative after adding the length, it
        /// is set to 0. If it is greater than or equal to the length of the Vector, it is set to
        /// <c>length - 1</c>.</param>
        ///
        /// <returns>The index of the first element, at or before <paramref name="fromIndex"/>,
        /// whose value is equal to <paramref name="searchElement"/>. If no element with that value
        /// is found, returns -1.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int lastIndexOf(ASAny searchElement, int fromIndex = Int32.MaxValue) => _VA_lastIndexOf(searchElement, fromIndex);

        protected private virtual int _VA_lastIndexOf(ASAny searchElement, int fromIndex) =>
            throw new NotImplementedException();

        /// <summary>
        /// Executes the specified callback function for each element in the Vector and returns a new
        /// Vector with each index holding the return value of the callback function for the element
        /// of the current Vector instance at that index. The returned vector will have the same
        /// length and element type as the current vector.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Vector instance that called this method. If the callback
        /// function returns a value that is not of the element type of the current Vector, it will be
        /// converted to that type.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>A Vector instance containing the return values of the callback function for each
        /// element in the current instance. If <paramref name="callback"/> is null, an empty Vector
        /// is returned.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item>The callback function throws an exception.</item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny map(ASFunction callback, ASObject thisObject = null) => _VA_map(callback, thisObject);

        protected private virtual ASVectorAny _VA_map(ASFunction callback, ASObject thisObject) =>
            throw new NotImplementedException();

        /// <summary>
        /// Removes the last element from the Vector and returns the value of that element.
        /// </summary>
        /// <returns>The value of the last element in the Vector. If the Vector is empty, returns the
        /// default value of the element type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description>This method is called on a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny pop() => _VA_pop();

        protected private virtual ASAny _VA_pop() => throw new InvalidOperationException();

        /// <summary>
        /// Adds the values in the <paramref name="args"/> array to the end of the Vector, and
        /// returns the new length of the Vector.
        /// </summary>
        /// <param name="args">The values to add to the end of the Vector.</param>
        /// <returns>The new length of the Vector.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description>This method is called on a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int push(RestParam args = default) => _VA_push(args);

        protected private virtual int _VA_push(RestParam args) => throw new NotImplementedException();

        /// <summary>
        /// Reverses all elements in the current Vector.
        /// </summary>
        /// <returns>The current Vector.</returns>
        /// <remarks>
        /// This method does an in-place reverse; no copy of the vector is made.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny reverse() => _VA_reverse();

        protected private virtual ASVectorAny _VA_reverse() => throw new NotImplementedException();

        /// <summary>
        /// Removes the first element from the Vector and returns the value of that element. All other
        /// elements are shifted backwards by one index.
        /// </summary>
        /// <returns>The value of the first element in the Vector. If the Vector is empty, returns the
        /// default value of the element type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description>This method is called on a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny shift() => _VA_shift();

        protected private virtual ASAny _VA_shift() => throw new NotImplementedException();

        /// <summary>
        /// Returns a Vector containing all elements of the current Vector from
        /// <paramref name="startIndex"/> up to (but not including) <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="startIndex">
        /// The index from which elements should be included in the returned Vector. If this is
        /// negative, the length of the Vector is added to it; if it is still negative after adding
        /// the length, it is set to zero. If this is greater than the length of the Vector, it is set
        /// to its length. If this is greater than of equal to <paramref name="endIndex"/>, an empty
        /// Vector is returned.
        /// </param>
        /// <param name="endIndex">
        /// The index at which to stop adding elements to the returned Vector. If this is negative,
        /// the length of the Vector is added to it; if it is still negative after adding the length,
        /// it is set to zero. If this is greater than the length of the Vector, it is set to its
        /// length. If this is less than of equal to <paramref name="endIndex"/>, an empty array is
        /// returned. Elements up to, but not including, this index, will be included in the returned
        /// Vector.
        /// </param>
        ///
        /// <returns>A Vector containing all elements of the current Vector from
        /// <paramref name="startIndex"/> up to (but not including)
        /// <paramref name="endIndex"/>.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny slice(int startIndex = 0, int endIndex = Int32.MaxValue) => _VA_slice(startIndex, endIndex);

        protected private virtual ASVectorAny _VA_slice(int startIndex, int endIndex) => throw new NotImplementedException();

        /// <summary>
        /// Calls a specified function for each element in the Vector, until it returns true for any
        /// element, in which case this method returns true, or the function returns false for all
        /// elements in the Vector, in which case this method returns false.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Vector instance that called this method. If the callback
        /// function returns a non-Boolean value, it will be converted to a Boolean.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>True if <paramref name="callback"/> returns true for any element in the Vector,
        /// otherwise false. If <paramref name="callback"/> is null, returns false.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item>The callback function throws an exception.</item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public bool some(ASFunction callback, ASObject thisObject = null) => _VA_some(callback, thisObject);

        protected private virtual bool _VA_some(ASFunction callback, ASObject thisObject) =>
            throw new NotImplementedException();

        /// <summary>
        /// Sorts this Vector instance.
        /// </summary>
        ///
        /// <param name="sortComparer">
        /// An object representing the kind of comparison used in the sort. If this is a function, the
        /// function will be used as a comparer function. In this case, the function must take two
        /// arguments of the vector's element type, and must return an integer which is less than,
        /// equal to or greater than zero if the first argument is less than, equal to or greater than
        /// the second argument respectively. If this is not a function, it is converted to an integer
        /// and treated as a set of bit flags represented by the <see cref="ASArray"/> sorting
        /// constants (NUMERIC, DESCENDING, CASEINSENSITIVE and UNIQUESORT). RETURNINDEXEDARRAY is not
        /// supported by the Vector class and has no effect.
        /// </param>
        ///
        /// <returns>The instance that called this method.</returns>
        /// <remarks>
        /// If the <paramref name="sortComparer"/> parameter is a callback function, and it throws
        /// an exception during the sort, the state of the Vector is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny sort(ASObject sortComparer = null) => _VA_sort(sortComparer);

        protected private virtual ASVectorAny _VA_sort(ASObject sortComparer) => throw new NotImplementedException();

        /// <summary>
        /// Replaces the specified number of elements in the Vector, starting at a given index with
        /// values from the <paramref name="newValues"/> array, and returns another Vector
        /// containing the values that have been deleted.
        /// </summary>
        ///
        /// <param name="startIndex">The index from which elements should be removed and included in
        /// the returned array. If this is negative, the length of the Vector is added to it; if it is
        /// still negative after adding the length, it is set to zero. If this is greater than the
        /// length of the Vector, it is set to its length.</param>
        /// <param name="deleteCount">
        /// The number of elements to be removed from the Vector. If this is negative, it is the
        /// number of elements to be retained starting from the end of the array and moving backwards,
        /// with all other elements starting at <paramref name="startIndex"/> being removed (in this
        /// case, its magnitude must not be greater than <c>length - startIndex</c>, where
        /// <c>length</c> is the length of the Vector; if it is greater than this value, it is set
        /// to zero). If <c>startIndex + deleteCount</c> is greater than the value of the
        /// <see cref="length"/> property, this value is set to <c>length - startIndex</c>.
        /// </param>
        /// <param name="newValues">
        /// The new values to be added to the Vector, starting at <paramref name="startIndex"/>, in
        /// place of the deleted elements. For fixed-length vectors, the number of elements in this
        /// array must be equal to <paramref name="deleteCount"/>, otherwise a RangeError is thrown.
        /// For Vectors whose <see cref="@fixed"/> value is set to false, if the length of this
        /// array is not equal to <paramref name="deleteCount"/>, elements after the index
        /// <c>deleteCount - 1</c> are shifted backwards or forwards so that they occur
        /// immediately after the elements inserted from this array.
        /// </param>
        ///
        /// <returns>A Vector containing the values that have been deleted. It contains
        /// <paramref name="deleteCount"/> elements from the Vector (prior to this method being
        /// called), starting at <paramref name="startIndex"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description>The Vector is a fixed-length Vector, and <paramref name="deleteCount"/> is
        /// not equal to the number of arguments in <paramref name="newValues"/>.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASVectorAny splice(
            int startIndex, int deleteCount = Int32.MaxValue, RestParam newValues = default)
        {
            return _VA_splice(startIndex, deleteCount, newValues);
        }

        protected private virtual ASVectorAny _VA_splice(int startIndex, int deleteCount, RestParam newValues) =>
            throw new NotImplementedException();

        /// <summary>
        /// Returns a locale-specific string representation of the current Vector instance.
        /// </summary>
        /// <returns>A locale-specific string representation of the current vector.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new string toLocaleString() => _VA_toLocaleString();

        protected private virtual string _VA_toLocaleString() => throw new NotImplementedException();

        /// <summary>
        /// Returns the string representation of the current instance.
        /// </summary>
        /// <returns>The string representation of the current instance.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() => _VA_toString();

        protected private virtual string _VA_toString() => throw new NotImplementedException();

        /// <summary>
        /// Adds the values in the <paramref name="args"/> array to the beginning of the Vector
        /// instance, and returns the new length of the Vector.
        /// </summary>
        /// <param name="args">The values to add to the beginning of the Vector.</param>
        /// <returns>The new length of the Vector.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1126</term>
        /// <description>This method is called on a fixed-length vector</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// All existing elements of the Vector are shifted forwards by N indices, where N is the
        /// number of items in the <paramref name="args"/> array.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int unshift(RestParam args = default) => _VA_unshift(args);

        protected private virtual int _VA_unshift(RestParam args) => throw new NotImplementedException();

        /// <inheritdoc/>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public sealed override bool propertyIsEnumerable(ASAny name = default(ASAny)) {
            if (ASObject.AS_isUint(name.value))
                return (uint)name < (uint)length;

            string str = (string)name;
            if (NumberFormatHelper.parseArrayIndex(str, true, out uint index))
                return index < (uint)length;

            return false;
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length != 1)
                throw ErrorHelper.createError(ErrorCode.CLASS_COERCE_ARG_COUNT_MISMATCH, args.Length);
            return fromObject(args[0].value);
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) {
            if (args.Length > 2)
                throw ErrorHelper.createError(ErrorCode.ARG_COUNT_MISMATCH, "Vector.<*>()", 2, args.Length);

            int length = (args.Length >= 1) ? (int)args[0] : 0;
            bool isFixed = args.Length == 2 && (bool)args[1];
            return new ASVector<ASObject>(length, isFixed);
        }

    }

}
