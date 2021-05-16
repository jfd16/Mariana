using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    internal sealed class Intrinsic {

        public static readonly Intrinsic
            OBJECT_NEW_0        = new Intrinsic(IntrinsicName.OBJECT_NEW_0),
            OBJECT_NEW_1        = new Intrinsic(IntrinsicName.OBJECT_NEW_1),
            INT_NEW_0           = new Intrinsic(IntrinsicName.INT_NEW_0),
            INT_NEW_1           = new Intrinsic(IntrinsicName.INT_NEW_1),
            UINT_NEW_0          = new Intrinsic(IntrinsicName.UINT_NEW_0),
            UINT_NEW_1          = new Intrinsic(IntrinsicName.UINT_NEW_1),
            NUMBER_NEW_0        = new Intrinsic(IntrinsicName.NUMBER_NEW_0),
            NUMBER_NEW_1        = new Intrinsic(IntrinsicName.NUMBER_NEW_1),
            BOOLEAN_NEW_0       = new Intrinsic(IntrinsicName.BOOLEAN_NEW_0),
            BOOLEAN_NEW_1       = new Intrinsic(IntrinsicName.BOOLEAN_NEW_1),
            STRING_NEW_0        = new Intrinsic(IntrinsicName.STRING_NEW_0),
            STRING_NEW_1        = new Intrinsic(IntrinsicName.STRING_NEW_1),
            DATE_CALL_0         = new Intrinsic(IntrinsicName.DATE_CALL_0),
            DATE_NEW_0          = new Intrinsic(IntrinsicName.DATE_NEW_0),
            DATE_NEW_1          = new Intrinsic(IntrinsicName.DATE_NEW_1),
            DATE_NEW_7          = new Intrinsic(IntrinsicName.DATE_NEW_7),
            ARRAY_NEW_0         = new Intrinsic(IntrinsicName.ARRAY_NEW_0),
            ARRAY_NEW_1_LEN     = new Intrinsic(IntrinsicName.ARRAY_NEW_1_LEN),
            ARRAY_NEW           = new Intrinsic(IntrinsicName.ARRAY_NEW),
            VECTOR_ANY_CALL_1   = new Intrinsic(IntrinsicName.VECTOR_ANY_CALL_1),
            VECTOR_ANY_CTOR     = new Intrinsic(IntrinsicName.VECTOR_ANY_CTOR),
            REGEXP_NEW_PATTERN  = new Intrinsic(IntrinsicName.REGEXP_NEW_PATTERN),
            REGEXP_NEW_CONST    = new Intrinsic(IntrinsicName.REGEXP_NEW_CONST),
            REGEXP_CALL_RE      = new Intrinsic(IntrinsicName.REGEXP_CALL_RE),
            REGEXP_NEW_RE       = new Intrinsic(IntrinsicName.REGEXP_NEW_RE),
            NAMESPACE_NEW_0     = new Intrinsic(IntrinsicName.NAMESPACE_NEW_0),
            NAMESPACE_NEW_1     = new Intrinsic(IntrinsicName.NAMESPACE_NEW_1),
            NAMESPACE_NEW_2     = new Intrinsic(IntrinsicName.NAMESPACE_NEW_2),
            QNAME_NEW_1         = new Intrinsic(IntrinsicName.QNAME_NEW_1),
            QNAME_NEW_2         = new Intrinsic(IntrinsicName.QNAME_NEW_2),
            XML_CALL_1          = new Intrinsic(IntrinsicName.XML_CALL_1),
            XML_NEW_0           = new Intrinsic(IntrinsicName.XML_NEW_0),
            XML_NEW_1           = new Intrinsic(IntrinsicName.XML_NEW_1),
            XMLLIST_CALL_1      = new Intrinsic(IntrinsicName.XMLLIST_CALL_1),
            XMLLIST_NEW_0       = new Intrinsic(IntrinsicName.XMLLIST_NEW_0),
            XMLLIST_NEW_1       = new Intrinsic(IntrinsicName.XMLLIST_NEW_1),
            MATH_MIN_2          = new Intrinsic(IntrinsicName.MATH_MIN_2),
            MATH_MAX_2          = new Intrinsic(IntrinsicName.MATH_MAX_2),
            MATH_MIN_2_I        = new Intrinsic(IntrinsicName.MATH_MIN_2_I),
            MATH_MAX_2_I        = new Intrinsic(IntrinsicName.MATH_MAX_2_I),
            MATH_MIN_2_U        = new Intrinsic(IntrinsicName.MATH_MIN_2_U),
            MATH_MAX_2_U        = new Intrinsic(IntrinsicName.MATH_MAX_2_U),
            ARRAY_PUSH_1        = new Intrinsic(IntrinsicName.ARRAY_PUSH_1),
            STRING_CHARAT       = new Intrinsic(IntrinsicName.STRING_CHARAT),
            STRING_CHARAT_I     = new Intrinsic(IntrinsicName.STRING_CHARAT_I),
            STRING_CHARAT_CMP   = new Intrinsic(IntrinsicName.STRING_CHARAT_CMP),
            STRING_CCODEAT      = new Intrinsic(IntrinsicName.STRING_CCODEAT),
            STRING_CCODEAT_I    = new Intrinsic(IntrinsicName.STRING_CCODEAT_I),
            STRING_CCODEAT_I_I  = new Intrinsic(IntrinsicName.STRING_CCODEAT_I_I),
            STRING_CCODEAT_CMP  = new Intrinsic(IntrinsicName.STRING_CCODEAT_CMP);

        public static Intrinsic VECTOR_T_CALL_1(Class arg) => new Intrinsic(IntrinsicName.VECTOR_T_CALL_1, arg);
        public static Intrinsic VECTOR_T_PUSH_1(Class arg) => new Intrinsic(IntrinsicName.VECTOR_T_PUSH_1, arg);

        public readonly IntrinsicName name;
        public readonly object arg;

        private Intrinsic(IntrinsicName name, object arg = null) {
            this.name = name;
            this.arg = arg;
        }

    }

    internal enum IntrinsicName {
        OBJECT_NEW_0,
        OBJECT_NEW_1,

        INT_NEW_0,
        INT_NEW_1,

        UINT_NEW_0,
        UINT_NEW_1,

        NUMBER_NEW_0,
        NUMBER_NEW_1,

        BOOLEAN_NEW_0,
        BOOLEAN_NEW_1,

        STRING_NEW_0,
        STRING_NEW_1,

        DATE_CALL_0,
        DATE_NEW_0,
        DATE_NEW_1,
        DATE_NEW_7,

        ARRAY_NEW_0,
        ARRAY_NEW_1_LEN,
        ARRAY_NEW,

        VECTOR_ANY_CALL_1,
        VECTOR_T_CALL_1,
        VECTOR_ANY_CTOR,

        REGEXP_NEW_PATTERN,
        REGEXP_NEW_CONST,
        REGEXP_CALL_RE,
        REGEXP_NEW_RE,

        NAMESPACE_NEW_0,
        NAMESPACE_NEW_1,
        NAMESPACE_NEW_2,

        QNAME_NEW_1,
        QNAME_NEW_2,

        XML_CALL_1,
        XML_NEW_0,
        XML_NEW_1,
        XMLLIST_CALL_1,
        XMLLIST_NEW_0,
        XMLLIST_NEW_1,

        MATH_MIN_2,
        MATH_MIN_2_I,
        MATH_MIN_2_U,
        MATH_MAX_2,
        MATH_MAX_2_I,
        MATH_MAX_2_U,
        ARRAY_PUSH_1,
        VECTOR_T_PUSH_1,

        STRING_CHARAT,
        STRING_CHARAT_I,
        STRING_CHARAT_CMP,
        STRING_CCODEAT,
        STRING_CCODEAT_I,
        STRING_CCODEAT_I_I,
        STRING_CCODEAT_CMP,
    }

}
