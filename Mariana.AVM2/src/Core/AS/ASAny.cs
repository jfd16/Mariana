using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A <see cref="ASAny"/> structure is used to hold a value of the "any" (*) type in the
    /// AVM2.
    /// </summary>
    ///
    /// <remarks>
    /// A <see cref="ASAny"/> object may be created by passing an object value to the
    /// constructor, which creates a <see cref="ASAny"/> structure for a defined object (which
    /// may be null). The default value of this structure represents the "undefined" value in AS3,
    /// and may be obtained using the appropriate language-specific expression (e.g.
    /// <c>default(ASAny)</c> or <c>new <see cref="ASAny"/>()</c> in C#), by creating an
    /// array of this type (which sets all elements to undefined initially), or from the static
    /// <see cref="undefined"/> field.
    /// </remarks>
    public readonly struct ASAny : IEquatable<ASAny> {

        /// <summary>
        /// The <see cref="ASAny"/> representation of the "undefined" value.
        /// </summary>
        public static readonly ASAny undefined = default(ASAny);

        /// <summary>
        /// An empty <see cref="ASObject"/> used to represent a null value in the
        /// <see cref="m_internalValue"/> field.
        /// </summary>
        private static readonly ASObject s_internalNull = new ASObject();

        /// <summary>
        /// The <see cref="ASAny"/> representation of the "null" value.
        /// </summary>
        public static readonly ASAny @null = new ASAny(null);

        /// <summary>
        /// The internal value of the <see cref="ASAny"/> instance.
        /// </summary>
        ///
        /// <remarks>
        /// This is null for the undefined value, and the object value of the
        /// <see cref="s_internalNull"/> field for the null value. Otherwise, this value is the
        /// object wrapped by the <see cref="ASAny"/> instance. The null value is chosen to
        /// represent undefined so that variables and/or array elements of the <see cref="ASAny"/>
        /// type are initialized to undefined (and not null) by default.
        /// </remarks>
        private readonly ASObject m_internalValue;

        /// <summary>
        /// Creates a new instance of the <see cref="ASAny"/> struct with a defined value.
        /// </summary>
        /// <param name="value">The object value (which may be null).</param>
        public ASAny(ASObject value) {
            m_internalValue = value ?? s_internalNull;
        }

        /// <summary>
        /// Gets the object value of the <see cref="ASAny"/> instance. If this instance is the
        /// undefined or null value, returns null.
        /// </summary>
        public ASObject value => (m_internalValue == s_internalNull) ? null : m_internalValue;

        /// <summary>
        /// Gets a Boolean value indicating whether the <see cref="ASAny"/> instance is not
        /// equal to the undefined value.
        /// </summary>
        public bool isDefined => m_internalValue != null;

        /// <summary>
        /// Gets a Boolean value indicating whether the <see cref="ASAny"/> instance is
        /// equal to the undefined value.
        /// </summary>
        public bool isUndefined => m_internalValue == null;

        /// <summary>
        /// Gets a Boolean value indicating whether the <see cref="ASAny"/> instance is
        /// equal to the null value.
        /// </summary>
        public bool isNull => m_internalValue == s_internalNull;

         /// <summary>
        /// Gets a Boolean value indicating whether the <see cref="ASAny"/> instance is
        /// equal to the undefined or null value.
        /// </summary>
        public bool isUndefinedOrNull => m_internalValue == null || m_internalValue == s_internalNull;

        /// <summary>
        /// Gets the <see cref="Class"/> instance representing the object's class. If this
        /// <see cref="ASAny"/> instance is null or undefined, returns null.
        /// </summary>
        public Class AS_class => (m_internalValue == null || m_internalValue == s_internalNull) ? null : m_internalValue.AS_class;

        #region PropertyBinding

        /// <summary>
        /// Returns true if a property with the given name exists.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property with the name exists, false otherwise.</returns>
        public bool AS_hasProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return value.AS_hasProperty(name, options);
        }

        /// <summary>
        /// Returns true if a property with the given name exists in one of the namespaces of the
        /// given set.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to find the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property exists, false otherwise.</returns>
        public bool AS_hasProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return value.AS_hasProperty(name, nsSet, options);
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetProperty(
            in QName name, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetProperty(name, out value, options);
        }

        /// <summary>
        /// Gets the value of the property with the given name in one of the namespaces of the given
        /// set.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetProperty(
            string name, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetProperty(name, nsSet, out value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_trySetProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_trySetProperty(name, value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given name in one of the namespaces of the given
        /// set.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_trySetProperty(
            string name, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_trySetProperty(name, nsSet, value, options);
        }

        /// <summary>
        /// Invokes the value of the property with the given name as a function.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryCallProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryCallProperty(name, args, out result, options);
        }

        /// <summary>
        /// Invokes the value of the property with the given name in one of the namespaces of the
        /// given set as a function.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryCallProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryCallProperty(name, nsSet, args, out result, options);
        }

        /// <summary>
        /// Invokes the value of the property with the given name as a constructor.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryConstructProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryConstructProperty(name, args, out result, options);
        }

        /// <summary>
        /// Invokes the value of the property with the given name in one of the namespaces of the
        /// given set as a constructor.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryConstructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryConstructProperty(name, nsSet, args, out result, options);
        }

        /// <summary>
        /// Deletes a property from the object.
        /// </summary>
        /// <param name="name">The name of the property to delete.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        public bool AS_deleteProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_deleteProperty(name, options);
        }

        /// <summary>
        /// Deletes a property from the object.
        /// </summary>
        ///
        /// <param name="name">The name of the property to delete.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property to
        /// delete.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        /// <remarks>
        /// If a derived class does not override this method to provide custom behaviour, this method
        /// only deletes dynamic properties. Traits declared by the object's class cannot be deleted.
        /// </remarks>
        public bool AS_deleteProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_deleteProperty(name, nsSet, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a qualified name.
        /// </summary>
        ///
        /// <param name="name">The name argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetDescendants(
            in QName name, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetDescendants(name, out result, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a name and
        /// namespace set.
        /// </summary>
        ///
        /// <param name="name">The local name argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetDescendants(
            string name, in NamespaceSet nsSet, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetDescendants(name, nsSet, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getProperty(name, options);
        }

        /// <summary>
        /// Gets the value of the property with the given name in the given set of namespaces.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getProperty(name, nsSet, options);
        }

        /// <summary>
        /// Sets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            this.value.AS_setProperty(name, value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given name in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setProperty(
            string name, in NamespaceSet nsSet,
            ASAny value, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            this.value.AS_setProperty(name, nsSet, value, options);
        }

        /// <summary>
        /// Calls the value of the property with the given name as a function.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callProperty(
            in QName name, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_callProperty(name, args, options);
        }

        /// <summary>
        /// Calls the value of the property with the given name in the given set of namespaces as a
        /// function.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_callProperty(name, nsSet, args, options);
        }

        /// <summary>
        /// Calls the value of the property with the given name as a constructor.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The object created by the constructor call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructProperty(
            in QName name, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_constructProperty(name, args, options);
        }

        /// <summary>
        /// Calls the value of the property with the given name in the given set of namespaces as a
        /// constructor.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The object created by the constructor call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_constructProperty(name, nsSet, args, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a qualified name.
        /// </summary>
        /// <param name="name">The name argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendants(in QName name, BindOptions options = 0) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getDescendants(name, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a name and
        /// namespace set.
        /// </summary>
        /// <param name="name">The local name argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendants(string name, in NamespaceSet nsSet, BindOptions options = 0) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getDescendants(name, nsSet, options);
        }

        /// <summary>
        /// Checks if a property with the given object key exists.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property with the key exists, false otherwise.</returns>
        public bool AS_hasPropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_hasPropertyObj(key, options);
        }

        /// <summary>
        /// Checks if a property with the given object key exists in the given set of namespaces.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property with the key exists, false otherwise.</returns>
        public bool AS_hasPropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_hasPropertyObj(key, nsSet, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetPropertyObj(
            ASAny key, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetPropertyObj(key, out value, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetPropertyObj(
            ASAny key, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetPropertyObj(key, nsSet, out value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_trySetPropertyObj(
            ASAny key, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_trySetPropertyObj(key, value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given object key in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_trySetPropertyObj(
            ASAny key, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_trySetPropertyObj(key, nsSet, value, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a function.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryCallPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryCallPropertyObj(key, args, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key in the given set of namespaces
        /// and invokes it as a function.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryCallPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryCallPropertyObj(key, nsSet, args, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a constructor.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryConstructPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryConstructPropertyObj(key, args, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key in the given set of namespaces
        /// and invokes it as a constructor.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryConstructPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryConstructPropertyObj(key, nsSet, args, out result, options);
        }

        /// <summary>
        /// Deletes the property with the given object key.
        /// </summary>
        /// <param name="key">The object key to look up the property to delete.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        public bool AS_deletePropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_deletePropertyObj(key, options);
        }

        /// <summary>
        /// Deletes the property with the given object key in the given set of namespaces.
        /// </summary>
        /// <param name="key">The object key to look up the property to delete.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        public bool AS_deletePropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_deletePropertyObj(key, nsSet, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object using an object key.
        /// </summary>
        ///
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetDescendantsObj(
            ASAny key, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetDescendantsObj(key, out result, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object using an object key and
        /// namespace set.
        /// </summary>
        ///
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public BindStatus AS_tryGetDescendantsObj(
            ASAny key, in NamespaceSet nsSet, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_tryGetDescendantsObj(key, nsSet, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getPropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getPropertyObj(key, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getPropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getPropertyObj(key, nsSet, options);
        }

        /// <summary>
        /// Sets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setPropertyObj(
            ASAny key, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            this.value.AS_setPropertyObj(key, value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given object key.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setPropertyObj(
            ASAny key, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            this.value.AS_setPropertyObj(key, nsSet, value, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a function.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_callPropertyObj(key, args, options);
        }

        /// <summary>
        /// Gets the value of the property with the object key and invokes it as a function.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_callPropertyObj(key, nsSet, args, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a constructor.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The return value of the constructor call, i.e. the created object.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_constructPropertyObj(key, args, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a constructor.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The return value of the constructor call, i.e. the created object.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_constructPropertyObj(key, nsSet, args, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with an object key.
        /// </summary>
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendantsObj(ASAny key, BindOptions options = 0) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getDescendantsObj(key, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object using object key
        /// lookup. An error is thrown if the object does not implement this operator.
        /// </summary>
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendantsObj(ASAny key, in NamespaceSet nsSet, BindOptions options = 0) {
            if(m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_getDescendantsObj(key, nsSet, options);
        }

        /// <summary>
        /// Gets the one-based index of the next enumerable dynamic property after the given index.
        /// </summary>
        /// <param name="index">The index of the property from where to search for the next property.
        /// A value of 0 will return the index of the first enumerable property.</param>
        /// <returns>The one-based index of the next enumerable property, or 0 if there are no more
        /// enumerable properties.</returns>
        public int AS_nextIndex(int index) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_nextIndex(index);
        }

        /// <summary>
        /// Gets the name of the dynamic property at the given index. If the object is not dynamic,
        /// undefined is returned, unless a subclass provides an overridden implementation.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually obtained
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property name.</returns>
        ///
        /// <remarks>
        /// This method (along with <see cref="AS_nextIndex"/> and <see cref="AS_valueAtIndex"/>)
        /// is used to iterate for-in loops in ActionScript 3. Subclasses can override these methods
        /// for custom for-in loop behaviour.
        /// </remarks>
        public ASAny AS_nameAtIndex(int index) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_nameAtIndex(index);
        }

        /// <summary>
        /// Gets the value of the dynamic property at the given index. If the object is not dynamic,
        /// undefined is returned, unless a subclass provides an overridden implementation.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually obtained
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property value.</returns>
        ///
        /// <remarks>
        /// This method (along with <see cref="AS_nextIndex"/> and <see cref="AS_nameAtIndex"/>)
        /// is used to iterate for-in loops in ActionScript 3. Subclasses can override these methods
        /// for custom for-in loop behaviour.
        /// </remarks>
        public ASAny AS_valueAtIndex(int index) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            return this.value.AS_valueAtIndex(index);
        }

        /// <summary>
        /// Invokes the current <see cref="ASAny"/> instance as a function.
        /// </summary>
        /// <param name="receiver">The receiver of the call.</param>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <returns>True, if the call was successful, false otherwise.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1006</term>
        /// <description>The value of this <see cref="ASAny"/> instance is undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        public bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.INVOKE_NON_FUNCTION, "undefined");
            return this.value.AS_tryInvoke(receiver, args, out result);
        }

        /// <summary>
        /// Invokes the current <see cref="ASAny"/> instance as a constructor.
        /// </summary>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The object created by the constructor call.</param>
        /// <returns>True, if the call was successful, false otherwise.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1006</term>
        /// <description>The value of this <see cref="ASAny"/> instance is undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        public bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            if (m_internalValue == null)
                throw ErrorHelper.createError(ErrorCode.INSTANTIATE_NON_CONSTRUCTOR);
            return this.value.AS_tryConstruct(args, out result);
        }

        /// <summary>
        /// Invokes the object as a function.
        /// </summary>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments of the function call.</param>
        /// <returns>The return value of the function call.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1006</term>
        /// <description>The value of this <see cref="ASAny"/> instance is undefined, or not a
        /// callable object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_invoke(ASAny receiver, ReadOnlySpan<ASAny> args) {
            if (AS_tryInvoke(receiver, args, out ASAny returnValue))
                return returnValue;
            throw ErrorHelper.createError(ErrorCode.INVOKE_NON_FUNCTION, "value");
        }

        /// <summary>
        /// Invokes the object as a constructor.
        /// </summary>
        /// <param name="args">The arguments of the constructor call.</param>
        /// <returns>The object created by the constructor.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1007</term>
        /// <description>The value of this <see cref="ASAny"/> instance is undefined, or not a
        /// constructible object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_construct(ReadOnlySpan<ASAny> args) {
            if (AS_tryConstruct(args, out ASAny returnValue))
                return returnValue;
            throw ErrorHelper.createError(ErrorCode.INSTANTIATE_NON_CONSTRUCTOR);
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Converts an object into a <see cref="ASAny"/> structure which represents the "*" type in
        /// AS3. This has the same effect as calling the constructor.
        /// </summary>
        /// <param name="x">The object.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the object.</returns>
        public static implicit operator ASAny(ASObject x) {
            return new ASAny(x);
        }

        /// <summary>
        /// Converts a <see cref="ASAny"/> structure into a plain Object. All undefined values will
        /// be converted to null. This has the same effect as getting the value of the
        /// <see cref="value"/> field.
        /// </summary>
        ///
        /// <param name="x">The <see cref="ASAny"/> to be converted to an object.</param>
        /// <returns>The object wrapped by <paramref name="x"/>.</returns>
        public static explicit operator ASObject(ASAny x) {
            return (x.m_internalValue == s_internalNull) ? null : x.m_internalValue;
        }

        /// <summary>
        /// Converts a Boolean value to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The Boolean value to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static implicit operator ASAny(bool x) => new ASAny(ASBoolean.box(x));

        /// <summary>
        /// Converts a Boolean value to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The Boolean value to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static ASAny AS_fromBoolean(bool x) => new ASAny(ASBoolean.box(x));

        /// <summary>
        /// Converts an integer to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The integer to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static implicit operator ASAny(int x) => new ASAny(ASint.box(x));

        /// <summary>
        /// Converts an integer to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The integer to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static ASAny AS_fromInt(int x) => new ASAny(ASint.box(x));

        /// <summary>
        /// Converts an unsigned integer to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The unsigned integer to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static implicit operator ASAny(uint x) => new ASAny(ASuint.box(x));

        /// <summary>
        /// Converts an unsigned integer to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The unsigned integer to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static ASAny AS_fromUint(uint x) => new ASAny(ASuint.box(x));

        /// <summary>
        /// Converts a floating-point number to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The number to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static implicit operator ASAny(double x) => new ASAny(ASNumber.box(x));

        /// <summary>
        /// Converts a floating-point number to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The number to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static ASAny AS_fromNumber(double x) => new ASAny(ASNumber.box(x));

        /// <summary>
        /// Converts a string to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The string to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static implicit operator ASAny(string x) => new ASAny(ASString.box(x));

        /// <summary>
        /// Converts a string to a boxed object wrapped in a <see cref="ASAny"/>.
        /// </summary>
        /// <param name="x">The string to convert.</param>
        /// <returns>A <see cref="ASAny"/> wrapping the boxed object.</returns>
        public static ASAny AS_fromString(string x) => new ASAny(ASString.box(x));

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a Boolean value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The Boolean value</returns>
        public static explicit operator bool(ASAny x) => ASObject.AS_toBoolean(x.value);

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a Boolean value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The Boolean value</returns>
        public static bool AS_toBoolean(ASAny x) => ASObject.AS_toBoolean(x.value);

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a floating-point number value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The floating-point number value.</returns>
        public static explicit operator double(ASAny x) => x.isDefined ? ASObject.AS_toNumber(x.value) : Double.NaN;

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a floating-point number value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The floating-point number value.</returns>
        public static double AS_toNumber(ASAny x) => x.isDefined ? ASObject.AS_toNumber(x.value) : Double.NaN;

        /// <summary>
        /// Converts a <see cref="ASAny"/> to an integer value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The integer value.</returns>
        public static explicit operator int(ASAny x) => x.isDefined ? ASObject.AS_toInt(x.value) : 0;

        /// <summary>
        /// Converts a <see cref="ASAny"/> to an integer value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The integer value.</returns>
        public static int AS_toInt(ASAny x) => x.isDefined ? ASObject.AS_toInt(x.value) : 0;

        /// <summary>
        /// Converts a <see cref="ASAny"/> to an unsigned integer value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The unsigned integer value.</returns>
        public static explicit operator uint(ASAny x) => x.isDefined ? ASObject.AS_toUint(x.value) : 0u;

        /// <summary>
        /// Converts a <see cref="ASAny"/> to an unsigned integer value.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The unsigned integer value.</returns>
        public static uint AS_toUint(ASAny x) => x.isDefined ? ASObject.AS_toUint(x.value) : 0u;

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a string value. This operator uses the
        /// <see cref="AS_coerceString"/> method, which converts null and undefined to null.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The string value.</returns>
        public static explicit operator string(ASAny x) {
            if (x.m_internalValue == null || x.m_internalValue == s_internalNull)
                return null;
            return ASObject.AS_coerceString(x.m_internalValue);
        }

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a string value. Null and undefined are converted to
        /// the strings "null" and "undefined" respectively.
        /// </summary>
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The string value.</returns>
        public static string AS_convertString(ASAny x) {
            ASObject v = x.m_internalValue;
            if (v == null)
                return "undefined";
            if (v == s_internalNull)
                return "null";
            return ASObject.AS_convertString(v);
        }

        /// <summary>
        /// Converts a <see cref="ASAny"/> to a string value. This differs from
        /// <see cref="AS_convertString"/> in that null and undefined are converted to the null
        /// string.
        /// </summary>
        ///
        /// <param name="x">The <see cref="ASAny"/> to convert.</param>
        /// <returns>The string value.</returns>
        public static string AS_coerceString(ASAny x) {
            if (x.m_internalValue == null || x.m_internalValue == s_internalNull)
                return null;
            return ASObject.AS_coerceString(x.m_internalValue);
        }

        /// <summary>
        /// Creates an <see cref="ASAny"/> instance from the given argument, of an object or boxed
        /// primitive type.
        /// </summary>
        /// <param name="obj">An instance of <see cref="ASObject"/> or a boxed primitive.</param>
        ///
        /// <returns>
        /// If <paramref name="obj"/> is an instance of <see cref="ASObject"/>, returns the object
        /// converted to the <see cref="ASAny"/> type. If <paramref name="obj"/> is a (boxed)
        /// instance of <see cref="ASAny"/>, returns its value. If <paramref name="obj"/> is a boxed
        /// primitive, returns the AS3 boxed form of that primitive (e.g. <see cref="ASint"/> for
        /// the type <see cref="Int32"/>) converted to <see cref="ASAny"/>. If the primitive type of
        /// <paramref name="obj"/> is <see cref="Byte"/>, <see cref="SByte"/>,
        /// <see cref="Int16"/> or <see cref="UInt16"/>, it is widened to <see cref="Int32"/>; if
        /// it is <see cref="Single"/>, it is widened to <see cref="Double"/>.
        /// </returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #10058</term>
        /// <description><paramref name="obj"/> is not an object of type <see cref="ASObject"/> or a
        /// type deriving from it, a boxed instance of <see cref="ASAny"/>, or a boxed instance of a
        /// valid primitive type.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static ASAny AS_fromBoxed(object obj) {
            if (obj == null)
                return @null;
            if (obj is ASAny asAny)
                return asAny;
            if (obj is ASObject asObj)
                return new ASAny(asObj);

            switch (Type.GetTypeCode(obj.GetType())) {
                case TypeCode.Byte:
                    return AS_fromInt((byte)obj);
                case TypeCode.SByte:
                    return AS_fromInt((sbyte)obj);
                case TypeCode.Int16:
                    return AS_fromInt((short)obj);
                case TypeCode.UInt16:
                    return AS_fromInt((ushort)obj);
                case TypeCode.Int32:
                    return AS_fromInt((int)obj);
                case TypeCode.UInt32:
                    return AS_fromUint((uint)obj);
                case TypeCode.Single:
                    return AS_fromNumber((float)obj);
                case TypeCode.Double:
                    return AS_fromNumber((double)obj);
                case TypeCode.String:
                    return AS_fromString((string)obj);
                case TypeCode.Boolean:
                    return AS_fromBoolean((bool)obj);
            }

            throw ErrorHelper.createError(ErrorCode.MARIANA__OBJECT_FROMPRIMITIVE_INVALID);
        }

        /// <summary>
        /// Performs a class cast of an <see cref="ASAny"/> instance to the type
        /// <typeparamref name="T"/>. An error is thrown if the value is not of the specified type.
        /// </summary>
        /// <param name="obj">The object to cast.</param>
        /// <typeparam name="T">The type to cast the object to.</typeparam>
        /// <returns><paramref name="obj"/> cast to type <typeparamref name="T"/>, or null if
        /// <paramref name="obj"/> is undefined or null.</returns>
        public static T AS_cast<T>(ASAny obj) where T : class {
            if (obj.m_internalValue == s_internalNull)
                return null;
            T x = obj.m_internalValue as T;
            if (x == null && obj.m_internalValue != null)
                throw ErrorHelper.createCastError(obj.m_internalValue, typeof(T));
            return x;
        }

        /// <summary>
        /// Coerces the given object to the given type. Use this method when the type to coerce to is
        /// not known at compile time.
        /// </summary>
        /// <param name="obj">The object to coerce.</param>
        /// <param name="toClass">The <see cref="Class"/> instance representing the type to coerce
        /// the object to.</param>
        /// <returns>The object, coerced to the given type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1034</term>
        /// <description>The type conversion is unsuccessful.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static ASAny AS_coerceType(ASAny obj, Class toClass) {
            if (toClass == null)
                return obj;

            ASObject v = obj.m_internalValue;
            if (v != null && v != s_internalNull && v.AS_class == toClass)
                return obj;

            switch (toClass.tag) {
                case ClassTag.INT:
                    return (int)obj;
                case ClassTag.UINT:
                    return (uint)obj;
                case ClassTag.NUMBER:
                    return (double)obj;
                case ClassTag.BOOLEAN:
                    return (bool)obj;
                case ClassTag.STRING:
                    return (string)obj;
                default:
                    if (v == null || v == s_internalNull || v.AS_class.canAssignTo(toClass))
                        return obj;
                    throw ErrorHelper.createCastError(obj, toClass);
            }
        }

        #endregion

        #region Operators

        /// <summary>
        /// Compares two <see cref="ASAny"/> objects using reference equality.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> and <paramref name="y"/> are equal, otherwise
        /// false.</returns>
        ///
        /// <remarks>
        /// Two objects compare equal using this operator if they are both undefined, both null, or
        /// both referring to the same object. For value equality, use the <see cref="AS_weakEq"/>
        /// or <see cref="AS_strictEq"/> methods.
        /// </remarks>
        public static bool operator ==(ASAny x, ASAny y) => x.m_internalValue == y.m_internalValue;

        /// <summary>
        /// Compares two <see cref="ASAny"/> objects using reference equality.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> and <paramref name="y"/> are not equal,
        /// otherwise false.</returns>
        ///
        /// <remarks>
        /// Two objects compare equal using this operator if they are both undefined, both null, or
        /// both referring to the same object. For value equality, use the <see cref="AS_weakEq"/>
        /// or <see cref="AS_strictEq"/> methods.
        /// </remarks>
        public static bool operator !=(ASAny x, ASAny y) => x.m_internalValue != y.m_internalValue;

        /// <summary>
        /// Compares two <see cref="ASAny"/> objects using the definition of the weak equality
        /// operator (==) is ActionScript 3.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True, if the two objects are equal, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>The comparison is done as follows (in order):</para>
        /// <list type="bullet">
        /// <item>If one of the objects is null or undefined, both objects are equal if and only if
        /// the other object is either null or undefined.</item>
        /// <item>If the object values of both objects are equal by reference, they are considered
        /// equal.</item>
        /// <item>If one of the objects is of a numeric type (int, uint or Number) or Boolean, then
        /// both objects are converted to the Number type and the floating-point number values are
        /// compared.</item>
        /// <item>If both the objects are Strings, then both objects are converted to the String type
        /// and the string values are compared. The comparison is based on character code points and
        /// is locale-independent.</item>
        /// <item>Two Namespace objects are equal if they have the same URIs.</item>
        /// <item>Two QName objects are equal if they have the same URIs and local names.</item>
        /// <item>
        /// XML and XMLList objects are compared by value. If one object is an XML and the other is
        /// an XMLList, they are considered equal if and only if (i) the XMLList has only one item,
        /// and (ii) that item in the XMLList is equal to the other XML object according to the weak
        /// equality comparison rules for XML objects. Undefined is considered to be equal to an empty
        /// XMLList.
        /// </item>
        /// <item>If one of the objects is an XML object having simple content, both objects are
        /// converted to strings and a string comparison is done.</item>
        /// <item>Otherwise, the two objects are not equal.</item>
        /// </list>
        /// </remarks>
        public static bool AS_weakEq(ASAny x, ASAny y) {
            ASObject vx = x.value, vy = y.value;

            ClassTagSet tagSet = default;
            if (vx != null)
                tagSet = tagSet.add(vx.AS_class.tag);
            if (vy != null)
                tagSet = tagSet.add(vy.AS_class.tag);

            if (ClassTagSet.xmlOrXmlList.containsAny(tagSet))
                // We need to check this case first because an empty XMLList and
                // undefined compare as equal, and so does an XML simple content node having
                // the string "null" or "undefined" with a null or undefined value.
                return XMLHelper.weakEquals(x, y);

            if (vx == vy)  // Equal by reference, or null == undefined
                return true;
            if (vx == null || vy == null)
                return false;

            if (ClassTagSet.numericOrBool.containsAny(tagSet))
                return (double)vx == (double)vy;

            if (tagSet.isSingle(ClassTag.STRING))
                return (string)vx == (string)vy;

            if (tagSet.isSingle(ClassTag.QNAME))
                return ASQName.AS_equals((ASQName)vx, (ASQName)vy);

            if (tagSet.isSingle(ClassTag.NAMESPACE))
                return ((ASNamespace)vx).uri == ((ASNamespace)vy).uri;

            if (tagSet.isSingle(ClassTag.FUNCTION)) {
                return vx is ASMethodClosure mc1 && vy is ASMethodClosure mc2
                    && mc1.method == mc2.method
                    && mc1.storedReceiver == mc2.storedReceiver;
            }

            return false;
        }

        /// <summary>
        /// Compares two <see cref="ASAny"/> objects using the definition of the strict equality
        /// operator (===) is ActionScript 3.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if the two objects are equal, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>The comparison is done as follows (in order):</para>
        /// <list type="bullet">
        /// <item>If one of the objects is null or undefined, the objects are equal if and only if the
        /// other object is null (if the first object is null) or undefined (if the first object is
        /// undefined). Unlike weak equality, null and undefined are not equal.</item>
        /// <item>If the object values of both objects are equal by reference, they are considered
        /// equal.</item>
        /// <item>If both the objects are of numeric types (int, uint or Number), then both objects
        /// are converted to the Number type and the floating-point number values are
        /// compared.</item>
        /// <item>If both the objects are Strings, the string values are compared. The comparison is
        /// based on character code points and is locale-independent.</item>
        /// <item>Otherwise, the two objects are not equal. Unlike weak equality, strict equality
        /// considers XML and XMLList objects as ordinary objects and they are compared by reference
        /// only.</item>
        /// </list>
        /// </remarks>
        public static bool AS_strictEq(ASAny x, ASAny y) =>
            x.isDefined == y.isDefined && ASObject.AS_strictEq(x.value, y.value);

        /// <summary>
        /// Compares two <see cref="ASAny"/> objects and returns true if the first object is less
        /// than the second.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is less than <paramref name="y"/>, otherwise false.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the less than operator in ActionScript 3.
        /// If both operands are strings, a string comparison is done. The string comparison is based
        /// on character code points and is locale-independent. Otherwise, the objects are converted
        /// to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_lessThan(ASAny x, ASAny y) {
            ASObject v1 = x.value, v2 = y.value;

            if (v1 == v2 || x.isUndefined || y.isUndefined)
                // Equal by reference, or one of the values is undefined
                return false;

            if (v1 is ASString && v2 is ASString)
                return String.CompareOrdinal(ASObject.AS_coerceString(v1), ASObject.AS_coerceString(v2)) < 0;

            return (double)x < (double)y;
        }

        /// <summary>
        /// Compares two objects and returns true if the first object is less than or equal to the
        /// second.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is less than or equal to
        /// <paramref name="y"/>, otherwise false.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the less than or equal to operator in
        /// ActionScript 3. If both operands are strings, a string comparison is done. The string
        /// comparison is based on character code points and is locale-independent. Otherwise, the
        /// objects are converted to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_lessEq(ASAny x, ASAny y) {
            ASObject v1 = x.value, v2 = y.value;

            if (v1 == v2)
                // Equal by reference
                // A comparison involving undefined and null always returns false.
                return x.isDefined == y.isDefined;

            if (v1 is ASString && v2 is ASString)
                return String.CompareOrdinal(ASObject.AS_coerceString(v1), ASObject.AS_coerceString(v2)) <= 0;

            return (double)x <= (double)y;
        }

        /// <summary>
        /// Compares two objects and returns true if the first object is greater than the second.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is greater than <paramref name="y"/>,
        /// otherwise false.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the greater than operator in ActionScript
        /// 3. If both operands are strings, a string comparison is done. The string comparison is
        /// based on character code points and is locale-independent. Otherwise, the objects are
        /// converted to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_greaterThan(ASAny x, ASAny y) => AS_lessThan(y, x);

        /// <summary>
        /// Compares two objects and returns true if the first object is greater than or equal to the
        /// second.
        /// </summary>
        ///
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is greater than or equal to
        /// <paramref name="y"/>, otherwise false.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the greater than or equal to operator in
        /// ActionScript 3. If both operands are strings, a string comparison is done. The string
        /// comparison is based on character code points and is locale-independent. Otherwise, the
        /// objects are converted to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_greaterEq(ASAny x, ASAny y) => AS_lessEq(y, x);

        /// <summary>
        /// Adds two <see cref="ASAny"/> instances using the definition of the addition (+) operator
        /// in ActionScript 3.
        /// </summary>
        /// <param name="x">The first instance.</param>
        /// <param name="y">The second instance.</param>
        /// <returns>The result of the addition operation.</returns>
        ///
        /// <remarks>
        /// <para>The definition of the addition operator is as follows:</para>
        /// <list type="bullet">
        /// <item>If none of the objects is of a type other than null, undefined, int, uint, Number or
        /// Boolean, the objects are converted to Numbers and their floating-point values are
        /// added.</item>
        /// <item>If one of the objects is a string or Date, both objects are converted to Strings and
        /// the string values are concatenated.</item>
        /// <item>If both objects are of the XML or XMLList types, the two objects are concatenated
        /// into a new XMLList.</item>
        /// <item>Otherwise, the two objects are converted to primitive objects by calling their
        /// <c>valueOf</c> or <c>toString</c> methods (with no hint) and the primitive objects
        /// are added.</item>
        /// </list>
        /// </remarks>
        public static ASObject AS_add(ASAny x, ASAny y) {
            ClassTagSet tagSet = default;
            if (x.value != null)
                tagSet = tagSet.add(x.AS_class.tag);
            if (y.value != null)
                tagSet = tagSet.add(y.AS_class.tag);

            if (ClassTagSet.numericOrBool.containsAll(tagSet))
                return (double)x + (double)y;

            if (ClassTagSet.stringOrDate.containsAny(tagSet))
                return ASAny.AS_convertString(x) + ASAny.AS_convertString(y);

            if (ClassTagSet.xmlOrXmlList.containsAll(tagSet) && x.value != null && y.value != null)
                return XMLHelper.concatenateXMLObjects(x.value, y.value);

            ASAny p1 = ASObject.AS_toPrimitive(x.value);
            ASAny p2 = ASObject.AS_toPrimitive(y.value);

            tagSet = default;
            if (x.value != null)
                tagSet = tagSet.add(p1.AS_class.tag);
            if (y.value != null)
                tagSet = tagSet.add(p2.AS_class.tag);

            if (ClassTagSet.numericOrBool.containsAll(tagSet))
                return (double)p1 + (double)p2;

            return ASAny.AS_convertString(p1) + ASAny.AS_convertString(p2);
        }

        /// <summary>
        /// Returns a string indicating the type of the given <see cref="ASAny"/> instance. This method uses
        /// the definition of the typeof operator in ActionScript 3.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>A string indicating the type of the given object.</returns>
        ///
        /// <remarks>
        /// The strings returned for different types are as follows: "number" (for int, uint and
        /// Number types), "boolean" (for Boolean), "string" (for String), "function" (for Function
        /// objects), "xml" (for XML and XMLList objects) or "object" (for null or objects of any
        /// other type).
        /// </remarks>
        public static string AS_typeof(ASAny obj) =>
            obj.isDefined ? ASObject.AS_typeof(obj.value) : "undefined";

        #endregion

        /// <summary>
        /// Serves as a hash function for a <see cref="ASAny"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and
        /// data structures such as a hash table.</returns>
        public override int GetHashCode() =>
            (m_internalValue == null || m_internalValue == s_internalNull) ? 0 : m_internalValue.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise,
        /// false.</returns>
        ///
        /// <remarks>
        /// This method is equivalent to the <see cref="AS_strictEq"/> method, which is the
        /// definition of the strict equality operator (===) in ActionScript 3. For a comparison using
        /// the weak equality (==) operator, use the <see cref="AS_weakEq"/> method.
        /// </remarks>
        public override bool Equals(object obj) => obj is ASAny asAny && AS_strictEq(this, asAny);

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise,
        /// false.</returns>
        ///
        /// <remarks>
        /// This method is equivalent to the <see cref="AS_strictEq"/> method, which is the
        /// definition of the strict equality operator (===) in ActionScript 3. For a comparison using
        /// the weak equality (==) operator, use the <see cref="AS_weakEq"/> method.
        /// </remarks>
        public bool Equals(ASAny obj) => AS_strictEq(this, obj);

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() => AS_convertString(this);

    }

}
