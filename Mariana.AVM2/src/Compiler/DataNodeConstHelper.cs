using System;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Functions used by the compiler for performing compile-time evaluation of operations
    /// involving constant operands.
    /// </summary>
    internal static class DataNodeConstHelper {

        /// <summary>
        /// Assigns a numeric constant value to a data node. If the value is representable
        /// as a signed or unsigned integer then the type of the node will be set to that
        /// integral type, otherwise the node type will be set to Number (floating-point).
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <param name="value">The numeric constant value.</param>
        public static void setToConstant(ref DataNode node, double value) {
            node.isConstant = true;
            node.isNotNull = true;

            if (Double.IsFinite(value) && (value != 0.0 || !Double.IsNegative(value))) {
                int ival = (int)value;
                if ((double)ival == value) {
                    node.dataType = DataNodeType.INT;
                    node.constant = new DataNodeConstant(ival);
                    return;
                }

                uint uval = (uint)value;
                if ((double)uval == value) {
                    node.dataType = DataNodeType.UINT;
                    node.constant = new DataNodeConstant((int)uval);
                    return;
                }
            }

            node.dataType = DataNodeType.NUMBER;
            node.constant = new DataNodeConstant(value);
        }

        /// <summary>
        /// Assigns a signed integer constant value to a data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <param name="value">The integer constant value.</param>
        public static void setToConstant(ref DataNode node, int value) {
            node.dataType = DataNodeType.INT;
            node.constant = new DataNodeConstant(value);
            node.isConstant = true;
            node.isNotNull = true;
        }

        /// <summary>
        /// Returns true if the given node holds a constant value of zero.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>True if <paramref name="node"/> holds a constant zero value, otherwise
        /// false.</returns>
        public static bool isConstantZero(ref DataNode node) {
            return DataNodeTypeHelper.isNumeric(node.dataType)
                && tryGetConstant(ref node, out double nodeVal)
                && nodeVal == 0.0;
        }

        /// <summary>
        /// Returns true if the given node holds a constant value that can be represented as a signed
        /// integer.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>True if <paramref name="node"/> holds a constant value that can be represented
        /// as a signed integer, otherwise false.</returns>
        public static bool isConstantInt(ref DataNode node) {
            return DataNodeTypeHelper.isNumeric(node.dataType)
                && tryGetConstant(ref node, out double nodeVal)
                && (double)(int)nodeVal == nodeVal;
        }

        /// <summary>
        /// Returns true if the given node holds a constant value that can be represented as an unsigned
        /// integer.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>True if <paramref name="node"/> holds a constant value that can be represented
        /// as an unsigned integer, otherwise false.</returns>
        public static bool isConstantUint(ref DataNode node) {
            return DataNodeTypeHelper.isNumeric(node.dataType)
                && tryGetConstant(ref node, out double nodeVal)
                && (double)(uint)nodeVal == nodeVal;
        }

        /// <summary>
        /// Assigns a constant value to a data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance</param>
        /// <param name="constantValue">The constant value to assign. This must be null, undefined
        /// or its type must be a numeric type, boolean, string, Namespace or QName.</param>
        public static void setToConstant(ref DataNode node, ASAny constantValue) {
            node.constant = default;

            if (constantValue.isUndefined) {
                node.dataType = DataNodeType.UNDEFINED;
            }
            else if (constantValue.isNull) {
                node.dataType = DataNodeType.NULL;
            }
            else {
                switch (constantValue.AS_class.tag) {
                    case ClassTag.INT:
                        node.dataType = DataNodeType.INT;
                        node.constant = new DataNodeConstant((int)constantValue.value);
                        break;
                    case ClassTag.UINT:
                        node.dataType = DataNodeType.UINT;
                        node.constant = new DataNodeConstant((int)constantValue.value);
                        break;
                    case ClassTag.NUMBER:
                        setToConstant(ref node, (double)constantValue.value);
                        break;
                    case ClassTag.STRING:
                        node.dataType = DataNodeType.STRING;
                        node.constant = new DataNodeConstant((string)constantValue.value);
                        break;
                    case ClassTag.BOOLEAN:
                        node.dataType = DataNodeType.BOOL;
                        node.constant = new DataNodeConstant((bool)constantValue.value);
                        break;
                    case ClassTag.NAMESPACE:
                        node.dataType = DataNodeType.NAMESPACE;
                        node.constant = new DataNodeConstant(Namespace.fromASNamespace((ASNamespace)constantValue.value));
                        break;
                    case ClassTag.QNAME:
                        node.dataType = DataNodeType.QNAME;
                        node.constant = new DataNodeConstant(QName.fromASQName((ASQName)constantValue.value));
                        break;
                }
            }

            node.isConstant = true;
            node.isNotNull = DataNodeTypeHelper.isNonNullable(node.dataType);
        }

        /// <summary>
        /// Attempts to retreive a numeric constant value from a data node that is of a numeric
        /// type, boolean or a constant null or undefined.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <param name="value">An output parameter into which the numeric constant value of
        /// <paramref name="node"/> will be written.</param>
        /// <returns>True if a numeric constant value could be retrieved from <paramref name="node"/>,
        /// otherwise false.</returns>
        public static bool tryGetConstant(ref DataNode node, out double value) {
            if (!node.isConstant) {
                value = 0;
                return false;
            }

            switch (node.dataType) {
                case DataNodeType.INT:
                    value = (double)node.constant.intValue;
                    break;
                case DataNodeType.UINT:
                    value = (double)(uint)node.constant.intValue;
                    break;
                case DataNodeType.NUMBER:
                    value = node.constant.doubleValue;
                    break;
                case DataNodeType.BOOL:
                    value = node.constant.boolValue ? 1.0 : 0.0;
                    break;
                case DataNodeType.NULL:
                    value = 0.0;
                    break;
                case DataNodeType.UNDEFINED:
                    value = Double.NaN;
                    break;
                default:
                    value = 0;
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to retreive a signed integer constant value from a data node that is of a numeric
        /// type, boolean or a constant null or undefined.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <param name="value">An output parameter into which the constant value of
        /// <paramref name="node"/> will be written.</param>
        /// <returns>True if a signed integer constant value could be retrieved from <paramref name="node"/>,
        /// otherwise false.</returns>
        public static bool tryGetConstant(ref DataNode node, out int value) {
            if (!node.isConstant) {
                value = 0;
                return false;
            }

            switch (node.dataType) {
                case DataNodeType.INT:
                case DataNodeType.UINT:
                    value = node.constant.intValue;
                    break;
                case DataNodeType.NUMBER:
                    value = ASNumber.AS_toInt(node.constant.doubleValue);
                    break;
                case DataNodeType.BOOL:
                    value = node.constant.boolValue ? 1 : 0;
                    break;
                case DataNodeType.NULL:
                case DataNodeType.UNDEFINED:
                    value = 0;
                    break;
                default:
                    value = 0;
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to evaluate a unary operation with constant inputs.
        /// </summary>
        /// <param name="input">A reference to a <see cref="DataNode"/> representing the input node.</param>
        /// <param name="output">A reference to a <see cref="DataNode"/> representing the output node.</param>
        /// <param name="opcode">The opcode for the unary operation.</param>
        /// <returns>True if the operation could be evaluated at compile time, otherwise false.</returns>
        public static bool tryEvalConstUnaryOp(ref DataNode input, ref DataNode output, ABCOp opcode) {
            if (!input.isConstant)
                return false;

            output.isConstant = false;

            switch (opcode) {
                case ABCOp.convert_b: {
                    bool value;

                    switch (input.dataType) {
                        case DataNodeType.BOOL:
                            value = input.constant.boolValue;
                            break;
                        case DataNodeType.INT:
                        case DataNodeType.UINT:
                            value = input.constant.intValue != 0;
                            break;
                        case DataNodeType.NUMBER:
                            value = ASNumber.AS_toBoolean(input.constant.doubleValue);
                            break;
                        case DataNodeType.NULL:
                        case DataNodeType.UNDEFINED:
                            value = false;
                            break;
                        default:
                            return false;
                    }

                    output.dataType = DataNodeType.BOOL;
                    output.isConstant = true;
                    output.constant = new DataNodeConstant(value);

                    break;
                }

                case ABCOp.convert_d: {
                    double value;

                    switch (input.dataType) {
                        case DataNodeType.BOOL:
                            value = input.constant.boolValue ? 1.0 : 0.0;
                            break;
                        case DataNodeType.INT:
                            value = (double)input.constant.intValue;
                            break;
                        case DataNodeType.UINT:
                            value = (double)(uint)input.constant.intValue;
                            break;
                        case DataNodeType.NUMBER:
                            value = input.constant.doubleValue;
                            break;
                        case DataNodeType.NULL:
                            value = 0;
                            break;
                        case DataNodeType.UNDEFINED:
                            value = Double.NaN;
                            break;
                        default:
                            return false;
                    }

                    output.dataType = DataNodeType.NUMBER;
                    output.isConstant = true;
                    output.constant = new DataNodeConstant(value);

                    break;
                }

                case ABCOp.convert_i:
                case ABCOp.convert_u:
                {
                    int value;
                    bool isUnsigned = opcode == ABCOp.convert_u;

                    switch (input.dataType) {
                        case DataNodeType.BOOL:
                            value = input.constant.boolValue ? 1 : 0;
                            break;
                        case DataNodeType.INT:
                        case DataNodeType.UINT:
                            value = input.constant.intValue;
                            break;
                        case DataNodeType.STRING:
                            value = isUnsigned
                                ? (int)ASString.AS_toUint(input.constant.stringValue)
                                : ASString.AS_toInt(input.constant.stringValue);
                            break;
                        case DataNodeType.NULL:
                        case DataNodeType.UNDEFINED:
                            value = 0;
                            break;
                        default:
                            return false;
                    }

                    output.dataType = isUnsigned ? DataNodeType.UINT : DataNodeType.INT;
                    output.isConstant = true;
                    output.constant = new DataNodeConstant(value);

                    break;
                }

                case ABCOp.convert_o:
                case ABCOp.coerce_s:
                {
                    if (input.dataType == DataNodeType.NULL || input.dataType == DataNodeType.UNDEFINED) {
                        output.dataType = DataNodeType.NULL;
                        output.isConstant = true;
                        break;
                    }
                    return false;
                }

                case ABCOp.bitnot: {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, ~value);
                    break;
                }

                case ABCOp.negate: {
                    if (tryGetConstant(ref input, out double value))
                        setToConstant(ref output, -value);
                    break;
                }

                case ABCOp.negate_i: {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, -value);
                    break;
                }

                case ABCOp.increment:
                case ABCOp.inclocal:
                {
                    if (tryGetConstant(ref input, out double value))
                        setToConstant(ref output, value + 1.0);
                    break;
                }

                case ABCOp.decrement:
                case ABCOp.declocal:
                {
                    if (tryGetConstant(ref input, out double value))
                        setToConstant(ref output, value - 1.0);
                    break;
                }

                case ABCOp.increment_i:
                case ABCOp.inclocal_i:
                {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, value + 1);
                    break;
                }

                case ABCOp.decrement_i:
                case ABCOp.declocal_i:
                {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, value - 1);
                    break;
                }

                case ABCOp.not: {
                    bool value;

                    switch (input.dataType) {
                        case DataNodeType.INT:
                        case DataNodeType.UINT:
                            value = input.constant.intValue == 0;
                            break;
                        case DataNodeType.NUMBER:
                            value = !ASNumber.AS_toBoolean(input.constant.doubleValue);
                            break;
                        case DataNodeType.BOOL:
                            value = !input.constant.boolValue;
                            break;
                        case DataNodeType.STRING:
                            value = !ASString.AS_toBoolean(input.constant.stringValue);
                            break;
                        case DataNodeType.NULL:
                        case DataNodeType.UNDEFINED:
                            value = true;
                            break;
                        default:
                            return false;
                    }

                    output.dataType = DataNodeType.BOOL;
                    output.constant = new DataNodeConstant(value);
                    output.isConstant = true;

                    break;
                }

                case ABCOp.sxi1: {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, -(value & 1));
                    break;
                }

                case ABCOp.sxi8: {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, (int)(sbyte)value);
                    break;
                }

                case ABCOp.sxi16: {
                    if (tryGetConstant(ref input, out int value))
                        setToConstant(ref output, (int)(short)value);
                    break;
                }

                default:
                    return false;
            }

            if (output.isConstant) {
                output.isNotNull = output.dataType != DataNodeType.NULL && output.dataType != DataNodeType.UNDEFINED;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to evaluate a binary operation with constant inputs.
        /// </summary>
        /// <param name="input1">A reference to a <see cref="DataNode"/> representing the first input node.</param>
        /// <param name="input2">A reference to a <see cref="DataNode"/> representing the second input node.</param>
        /// <param name="output">A reference to a <see cref="DataNode"/> representing the output node.</param>
        /// <param name="opcode">The opcode for the binary operation.</param>
        /// <returns>True if the operation could be evaluated at compile time, otherwise false.</returns>
        public static bool tryEvalConstBinaryOp(ref DataNode input1, ref DataNode input2, ref DataNode output, ABCOp opcode) {
            if (!input1.isConstant || !input2.isConstant)
                return false;

            output.isConstant = false;

            double dval1, dval2;
            int ival1, ival2;

            switch (opcode) {
                case ABCOp.add: {
                    if (input1.dataType == DataNodeType.STRING && input2.dataType == DataNodeType.STRING) {
                        string result = ASString.AS_add(input1.constant.stringValue, input2.constant.stringValue);

                        output.isConstant = true;
                        if (result != null) {
                            output.dataType = DataNodeType.STRING;
                            output.constant = new DataNodeConstant(result);
                        }
                        else {
                            output.dataType = DataNodeType.NULL;
                        }
                    }
                    else if (tryGetConstant(ref input1, out dval1) && tryGetConstant(ref input2, out dval2)) {
                        setToConstant(ref output, dval1 + dval2);
                    }
                    break;
                }

                case ABCOp.subtract:
                    if (tryGetConstant(ref input1, out dval1) && tryGetConstant(ref input2, out dval2))
                        setToConstant(ref output, dval1 - dval2);
                    break;

                case ABCOp.multiply:
                    if (tryGetConstant(ref input1, out dval1) && tryGetConstant(ref input2, out dval2))
                        setToConstant(ref output, dval1 * dval2);
                    break;

                case ABCOp.divide:
                    if (tryGetConstant(ref input1, out dval1) && tryGetConstant(ref input2, out dval2))
                        setToConstant(ref output, dval1 / dval2);
                    break;

                case ABCOp.modulo:
                    if (tryGetConstant(ref input1, out dval1) && tryGetConstant(ref input2, out dval2))
                        setToConstant(ref output, dval1 % dval2);
                    break;

                case ABCOp.add_i:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 + ival2);
                    break;

                case ABCOp.subtract_i:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 - ival2);
                    break;

                case ABCOp.multiply_i:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 * ival2);
                    break;

                case ABCOp.bitand:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 & ival2);
                    break;

                case ABCOp.bitor:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 | ival2);
                    break;

                case ABCOp.bitxor:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 ^ ival2);
                    break;

                case ABCOp.lshift:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 << ival2);
                    break;

                case ABCOp.rshift:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2))
                        setToConstant(ref output, ival1 >> ival2);
                    break;

                case ABCOp.urshift:
                    if (tryGetConstant(ref input1, out ival1) && tryGetConstant(ref input2, out ival2)) {
                        setToConstant(ref output, (int)((uint)ival1 >> ival2));
                        output.dataType = DataNodeType.UINT;
                    }
                    break;

                default:
                    return false;
            }

            if (output.isConstant) {
                output.isNotNull = output.dataType != DataNodeType.NULL && output.dataType != DataNodeType.UNDEFINED;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to evaluate a binary relational operation with constant inputs.
        /// </summary>
        /// <param name="input1">A reference to a <see cref="DataNode"/> representing the first input node.</param>
        /// <param name="input2">A reference to a <see cref="DataNode"/> representing the second input node.</param>
        /// <param name="output">A reference to a <see cref="DataNode"/> representing the output node.</param>
        /// <param name="opcode">The opcode for the binary operation.</param>
        /// <returns>True if the operation could be evaluated at compile time, otherwise false.</returns>
        public static bool tryEvalConstCompareOp(ref DataNode input1, ref DataNode input2, ref DataNode output, ABCOp opcode) {
            if (!input1.isConstant || !input2.isConstant)
                return false;

            output.isConstant = false;
            bool result;

            if (input1.dataType == DataNodeType.STRING && input2.dataType == DataNodeType.STRING) {
                int compareResult = String.CompareOrdinal(input1.constant.stringValue, input2.constant.stringValue);

                switch (opcode) {
                    case ABCOp.equals:
                    case ABCOp.strictequals:
                    case ABCOp.ifeq:
                    case ABCOp.ifstricteq:
                        result = compareResult == 0;
                        break;
                    case ABCOp.ifne:
                    case ABCOp.ifstrictne:
                        result = compareResult != 0;
                        break;
                    case ABCOp.lessthan:
                    case ABCOp.iflt:
                    case ABCOp.ifnge:
                        result = compareResult < 0;
                        break;
                    case ABCOp.lessequals:
                    case ABCOp.ifle:
                    case ABCOp.ifngt:
                        result = compareResult <= 0;
                        break;
                    case ABCOp.greaterthan:
                    case ABCOp.ifgt:
                    case ABCOp.ifnle:
                        result = compareResult > 0;
                        break;
                    case ABCOp.greaterequals:
                    case ABCOp.ifge:
                    case ABCOp.ifnlt:
                        result = compareResult >= 0;
                        break;
                    default:
                        return false;
                }
            }
            else if (tryGetConstant(ref input1, out double dval1) && tryGetConstant(ref input2, out double dval2)) {
                switch (opcode) {
                    case ABCOp.equals:
                    case ABCOp.strictequals:
                    case ABCOp.ifeq:
                    case ABCOp.ifstricteq:
                        result = dval1 == dval2;
                        break;
                    case ABCOp.ifne:
                    case ABCOp.ifstrictne:
                        result = dval1 != dval2;
                        break;
                    case ABCOp.lessthan:
                    case ABCOp.iflt:
                        result = dval1 < dval2;
                        break;
                    case ABCOp.ifnlt:
                        result = !(dval1 < dval2);
                        break;
                    case ABCOp.lessequals:
                    case ABCOp.ifle:
                        result = dval1 <= dval2;
                        break;
                    case ABCOp.ifnle:
                        result = !(dval1 <= dval2);
                        break;
                    case ABCOp.greaterthan:
                    case ABCOp.ifgt:
                        result = dval1 > dval2;
                        break;
                    case ABCOp.ifngt:
                        result = !(dval1 > dval2);
                        break;
                    case ABCOp.greaterequals:
                    case ABCOp.ifge:
                        result = dval1 >= dval2;
                        break;
                    case ABCOp.ifnge:
                        result = !(dval1 >= dval2);
                        break;
                    default:
                        return false;
                }
            }
            else {
                return false;
            }

            output.isConstant = true;
            output.isNotNull = true;
            output.dataType = DataNodeType.BOOL;
            output.constant = new DataNodeConstant(result);

            return true;
        }

    }

}
