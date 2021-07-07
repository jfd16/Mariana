using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a method signature in an ABC file.
    /// </summary>
    public sealed class ABCMethodInfo {

        private int m_abcIndex;

        private ABCMultiname[] m_paramTypeNames;

        private string?[]? m_paramNames;

        private ASAny[]? m_paramDefaultValues;

        private ABCMultiname m_returnTypeName;

        private string m_methodName;

        private ABCMethodFlags m_flags;

        /// <summary>
        /// Gets the index of the method entry in the ABC file metadata.
        /// </summary>
        public int abcIndex => m_abcIndex;

        /// <summary>
        /// Gets a multiname representing the name of the method's return type.
        /// </summary>
        public ABCMultiname returnTypeName => m_returnTypeName;

        /// <summary>
        /// Gets the name of the method defined in the method entry in the ABC file metadata.
        /// </summary>
        public string methodName => m_methodName;

        /// <summary>
        /// Gets a set of flags from the <see cref="ABCMethodFlags"/> enumeration associated with
        /// this method.
        /// </summary>
        public ABCMethodFlags flags => m_flags;

        /// <summary>
        /// Gets the number of formal parameters declared by this method.
        /// </summary>
        public int paramCount => m_paramTypeNames.Length;

        /// <summary>
        /// Gets a multiname representing the name of the type of the formal parameter
        /// at the given index.
        /// </summary>
        /// <returns>A multiname representing the name of the type of the parameter at
        /// index <paramref name="index"/>.</returns>
        /// <param name="index">The zero-based index of the parameter.</param>
        public ABCMultiname getParamTypeName(int index) {
            if ((uint)index >= (uint)m_paramTypeNames.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(index));

            return m_paramTypeNames[index];
        }

        /// <summary>
        /// Gets the name of the formal parameter at the given index.
        /// </summary>
        /// <returns>The name of the parameter at index <paramref name="index"/>, or null if
        /// this method does not define parameter names.</returns>
        /// <param name="index">The zero-based index of the parameter.</param>
        public string? getParamName(int index) {
            if (m_paramNames == null)
                return null;

            if ((uint)index >= (uint)m_paramNames.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(index));

            return m_paramNames[index];
        }

        /// <summary>
        /// Gets a value indicating whether the formal parameter at the given index is optional.
        /// </summary>
        /// <returns>True if the parameter at index <paramref name="index"/> is optional, otherwise false.</returns>
        /// <param name="index">The zero-based index of the parameter.</param>
        public bool isParamOptional(int index) {
            if ((uint)index >= (uint)m_paramTypeNames.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(index));

            if (m_paramDefaultValues == null)
                return false;

            return index >= m_paramTypeNames.Length - m_paramDefaultValues.Length;
        }

        /// <summary>
        /// Gets the default value of the formal parameter at the given index.
        /// </summary>
        /// <returns>The default value of the parameter at index <paramref name="index"/>, or
        /// undefined if that parameter is not an optional parameter. (The <see cref="isParamOptional"/>
        /// method can be used to check whether a parameter is optional.)</returns>
        /// <param name="index">The zero-based index of the parameter.</param>
        public ASAny getParamDefaultValue(int index) {
            if ((uint)index >= (uint)m_paramTypeNames.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(index));

            if (m_paramDefaultValues == null)
                return ASAny.undefined;

            int optIndex = index - (m_paramTypeNames.Length - m_paramDefaultValues.Length);
            if (optIndex < 0)
                return ASAny.undefined;

            return m_paramDefaultValues[optIndex];
        }

        internal ABCMethodInfo(
            int abcIndex,
            ABCMultiname returnTypeName,
            string methodName,
            ABCMethodFlags flags,
            ABCMultiname[] paramTypeNames,
            string?[]? paramNames,
            ASAny[]? paramDefaultValues
        ) {
            m_abcIndex = abcIndex;
            m_paramTypeNames = paramTypeNames;
            m_paramNames = paramNames;
            m_paramDefaultValues = paramDefaultValues;
            m_returnTypeName = returnTypeName;
            m_methodName = methodName;
            m_flags = flags;
        }

    }

}
