using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.Common;

using static System.Buffers.Binary.BinaryPrimitives;

namespace Mariana.CodeGen.IL {

    /// <summary>
    /// Generates IL code for method bodies.
    /// </summary>
    public sealed class ILBuilder {

        private enum _OperandKind : byte {
            NONE,
            INT8,
            INT16,
            INT32,
            INT64,
            FLOAT32,
            FLOAT64,
            BR8,
            BR32,
            VAR0,
            VAR8,
            VAR16,
            ARG0,
            ARG8,
            ARG16,
            TOKEN,
            SWITCH,
            INVALID,
        }

        private enum _LocalStatus : byte {
            PERSISTENT,
            TEMP_ACTIVE,
            TEMP_DISPOSED,
        }

        private sealed class _ExceptionHandler {
            public int tryStart;
            public int tryLength;
            public int handlerStart;
            public int handlerLength;
            public int filterStart;
            public Label handlerEndLabel;
            public ExceptionRegionKind clauseType;
            public bool isContinuation;
            public EntityHandle catchType;
        }

        private struct _LocalInfo {
            public _LocalStatus status;
            public Type? type;
        }

        private struct _LabelInfo {
            public int targetPosition;
            public int stackSize;
        }

        private struct _BranchInfo {
            public int offsetPosition;
            public int basePosition;
            public Label target;
            public ILOp opcode;
            public bool useShortForm;
        }

        private struct _RelocationInfo {
            public int startOffset;
            public int shift;
        }

        private struct _OpcodeInfo {
            public readonly sbyte stackDelta;
            public readonly _OperandKind operandKind;

            public _OpcodeInfo(sbyte stackDelta, _OperandKind operandKind) {
                this.stackDelta = stackDelta;
                this.operandKind = operandKind;
            }
        }

        private static readonly Comparison<_ExceptionHandler> s_excHandlerSorter = (h1, h2) => {
            int tryEnd1 = h1.tryStart + h1.tryLength;
            int tryEnd2 = h2.tryStart + h2.tryLength;
            return (tryEnd1 == tryEnd2) ? 0 : ((tryEnd1 < tryEnd2) ? -1 : 1);
        };

        /// <summary>
        /// A token representing a label that can be used as a branch target in emitted code.
        /// </summary>
        public readonly struct Label : IEquatable<Label> {

            private readonly int m_id;

            // Store id + 1 internally so that the default value has an invalid id (-1).
            internal Label(int id) => m_id = id + 1;

            internal int id => m_id - 1;

            /// <summary>
            /// Returns true if this <see cref="Label"/> is the default value of the type. Such
            /// a value never represents a valid label and is never returned by the
            /// <see cref="ILBuilder.createLabel"/> method.
            /// </summary>
            public bool isDefault => m_id == 0;

            /// <summary>
            /// Determines whether <paramref name="label"/> is equal to this <see cref="Label"/>
            /// instance.
            /// </summary>
            /// <param name="label">The <see cref="Label"/> to compare with this
            /// <see cref="Label"/>.</param>
            /// <returns>True if <paramref name="label"/> is equal to this <see cref="Label"/>
            /// instance, otherwise false.</returns>
            /// <remarks>
            /// If two <see cref="Label"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public bool Equals(Label label) => m_id == label.m_id;

            /// <summary>
            /// Determines whether <paramref name="o"/> is equal to this <see cref="Label"/> instance.
            /// </summary>
            /// <param name="o">The object to compare with this <see cref="Label"/>.</param>
            /// <returns>True if <paramref name="o"/> is equal to this <see cref="Label"/> instance,
            /// otherwise false.</returns>
            /// <remarks>
            /// If two <paramref name="o"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public override bool Equals(object o) => o is Label label && label.m_id == m_id;

            /// <summary>
            /// Gets a hash code for this <see cref="Label"/> instance.
            /// </summary>
            /// <returns>A hash code for this <see cref="Label"/> instance.</returns>
            public override int GetHashCode() => m_id;

            /// <summary>
            /// Determines whether <paramref name="x"/> is equal to <paramref name="y"/>.
            /// </summary>
            /// <param name="x">The first <see cref="Label"/> instance to compare.</param>
            /// <param name="y">The second <see cref="Label"/> instance to compare.</param>
            /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise
            /// false.</returns>
            /// <remarks>
            /// If two <see cref="Label"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public static bool operator ==(Label x, Label y) => x.m_id == y.m_id;

            /// <summary>
            /// Determines whether <paramref name="x"/> is not equal to <paramref name="y"/>.
            /// </summary>
            /// <param name="x">The first <see cref="Label"/> instance to compare.</param>
            /// <param name="y">The second <see cref="Label"/> instance to compare.</param>
            /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise
            /// false.</returns>
            /// <remarks>
            /// If two <see cref="Label"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public static bool operator !=(Label x, Label y) => x.m_id != y.m_id;

        }

        /// <summary>
        /// A structure representing a group of labels that can be used as targets for a switch
        /// instruction. This is returned by the <see cref="ILBuilder.createLabelGroup"
        /// qualifyHint="true"/> method.
        /// </summary>
        public readonly struct LabelGroup {

            internal readonly int m_startId;
            internal readonly int m_length;

            internal LabelGroup(int startId, int length) {
                m_startId = startId;
                m_length = length;
            }

            /// <summary>
            /// The label in the group at the given index.
            /// </summary>
            /// <param name="index">The index.</param>
            public Label this[int index] {
                get {
                    if ((uint)index >= (uint)m_length)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    return new Label(m_startId + index);
                }
            }

        }

        /// <summary>
        /// A structure representing a local variable. An instance of this type is returned when a
        /// local variable or temporary is declared, and can be used to refer to the variable in
        /// emitted code.
        /// </summary>
        public readonly struct Local : IEquatable<Local> {

            private readonly ushort m_id;

            // Store id + 1 internally so that the default value has an invalid id (-1).
            internal Local(ushort id) => m_id = (ushort)(id + 1);

            internal int id => m_id - 1;

            /// <summary>
            /// Returns true if this <see cref="Local"/> is the default value of the type. Such
            /// a value never represents a valid local variable and is never returned by any
            /// of the <see cref="ILBuilder"/> methods that create local variables.
            /// </summary>
            public bool isDefault => m_id == 0;

            /// <summary>
            /// Determines whether <paramref name="localvar"/> is equal to this <see cref="Local"/>
            /// instance.
            /// </summary>
            /// <param name="localvar">The <see cref="Local"/> to compare with this
            /// <see cref="Local"/>.</param>
            /// <returns>True if <paramref name="localvar"/> is equal to this <see cref="Local"/>
            /// instance, otherwise false.</returns>
            /// <remarks>
            /// If two <see cref="Local"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public bool Equals(Local localvar) => m_id == localvar.m_id;

            /// <summary>
            /// Determines whether <paramref name="o"/> is equal to this <see cref="Local"/>
            /// instance.
            /// </summary>
            /// <param name="o">The object to compare with this <see cref="Local"/>.</param>
            /// <returns>True if <paramref name="o"/> is equal to this <see cref="Local"/>
            /// instance, otherwise false.</returns>
            /// <remarks>
            /// If two <paramref name="o"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public override bool Equals(object o) => o is Local local && local.m_id == m_id;

            /// <summary>
            /// Gets a hash code for this <see cref="Local"/> instance.
            /// </summary>
            /// <returns>A hash code for this <see cref="Local"/> instance.</returns>
            public override int GetHashCode() => m_id;

            /// <summary>
            /// Determines whether <paramref name="x"/> is equal to <paramref name="y"/>.
            /// </summary>
            /// <param name="x">The first <see cref="Local"/> instance to compare.</param>
            /// <param name="y">The second <see cref="Local"/> instance to compare.</param>
            /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise
            /// false.</returns>
            /// <remarks>
            /// If two <see cref="Local"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public static bool operator ==(Local x, Local y) => x.m_id == y.m_id;

            /// <summary>
            /// Determines whether <paramref name="x"/> is not equal to <paramref name="y"/>.
            /// </summary>
            /// <param name="x">The first <see cref="Local"/> instance to compare.</param>
            /// <param name="y">The second <see cref="Local"/> instance to compare.</param>
            /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise
            /// false.</returns>
            /// <remarks>
            /// If two <see cref="Local"/> instances obtained from different <see cref="ILBuilder"/>
            /// instances are compared for equality, the result of the comparison is undefined.
            /// </remarks>
            public static bool operator !=(Local x, Local y) => x.m_id != y.m_id;

        }

        /// <summary>
        /// Single-byte opcode information.
        /// </summary>
        private static readonly _OpcodeInfo[] s_singleByteOpcodeInfo = {
            // Special values for stackDelta:
            // -128: Invalid instruction
            // -127: Empties the stack
            //  127: Stack change dependent on operand
            new _OpcodeInfo( 0, _OperandKind.NONE),      // nop
            new _OpcodeInfo( 0, _OperandKind.NONE),      // break
            new _OpcodeInfo( 1, _OperandKind.ARG0),      // ldarg_0
            new _OpcodeInfo( 1, _OperandKind.ARG0),      // ldarg_1
            new _OpcodeInfo( 1, _OperandKind.ARG0),      // ldarg_2
            new _OpcodeInfo( 1, _OperandKind.ARG0),      // ldarg_3
            new _OpcodeInfo( 1, _OperandKind.VAR0),      // ldloc_0
            new _OpcodeInfo( 1, _OperandKind.VAR0),      // ldloc_1
            new _OpcodeInfo( 1, _OperandKind.VAR0),      // ldloc_2
            new _OpcodeInfo( 1, _OperandKind.VAR0),      // ldloc_3
            new _OpcodeInfo(-1, _OperandKind.VAR0),      // stloc_0
            new _OpcodeInfo(-1, _OperandKind.VAR0),      // stloc_1
            new _OpcodeInfo(-1, _OperandKind.VAR0),      // stloc_2
            new _OpcodeInfo(-1, _OperandKind.VAR0),      // stloc_3
            new _OpcodeInfo( 1, _OperandKind.ARG8),      // ldarg_s
            new _OpcodeInfo( 1, _OperandKind.ARG8),      // ldarga_s
            new _OpcodeInfo(-1, _OperandKind.ARG8),      // starg_s
            new _OpcodeInfo( 1, _OperandKind.VAR8),      // ldloc_s
            new _OpcodeInfo( 1, _OperandKind.VAR8),      // ldloca_s
            new _OpcodeInfo(-1, _OperandKind.VAR8),      // stloc_s
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldnull
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_m1
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_0
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_1
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_2
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_3
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_4
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_5
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_6
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_7
            new _OpcodeInfo( 1, _OperandKind.NONE),      // ldc_i4_8
            new _OpcodeInfo( 1, _OperandKind.INT8),      // ldc_i4_s
            new _OpcodeInfo( 1, _OperandKind.INT32),     // ldc_i4
            new _OpcodeInfo( 1, _OperandKind.INT64),     // ldc_i8
            new _OpcodeInfo( 1, _OperandKind.FLOAT32),   // ldc_r4
            new _OpcodeInfo( 1, _OperandKind.FLOAT64),   // ldc_r8
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 1, _OperandKind.NONE),      // dup
            new _OpcodeInfo(-1, _OperandKind.NONE),      // pop
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // jmp
            new _OpcodeInfo( 127, _OperandKind.TOKEN),   // call
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-127, _OperandKind.NONE),    // ret
            new _OpcodeInfo( 0, _OperandKind.BR8),       // br_s
            new _OpcodeInfo(-1, _OperandKind.BR8),       // brfalse_s
            new _OpcodeInfo(-1, _OperandKind.BR8),       // brtrue_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // beq_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // bge_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // bgt_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // ble_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // blt_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // bne_un_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // bge_un_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // bgt_un_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // ble_un_s
            new _OpcodeInfo(-2, _OperandKind.BR8),       // blt_un_s
            new _OpcodeInfo( 0, _OperandKind.BR32),      // br
            new _OpcodeInfo(-1, _OperandKind.BR32),      // brfalse
            new _OpcodeInfo(-1, _OperandKind.BR32),      // brtrue
            new _OpcodeInfo(-2, _OperandKind.BR32),      // beq
            new _OpcodeInfo(-2, _OperandKind.BR32),      // bge
            new _OpcodeInfo(-2, _OperandKind.BR32),      // bgt
            new _OpcodeInfo(-2, _OperandKind.BR32),      // ble
            new _OpcodeInfo(-2, _OperandKind.BR32),      // blt
            new _OpcodeInfo(-2, _OperandKind.BR32),      // bne_un
            new _OpcodeInfo(-2, _OperandKind.BR32),      // bge_un
            new _OpcodeInfo(-2, _OperandKind.BR32),      // bgt_un
            new _OpcodeInfo(-2, _OperandKind.BR32),      // ble_un
            new _OpcodeInfo(-2, _OperandKind.BR32),      // blt_un
            new _OpcodeInfo(-1, _OperandKind.SWITCH),    // switch
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_i1
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_u1
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_i2
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_u2
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_i4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_u4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_i8
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_i
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_r4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_r8
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldind_ref
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_ref
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_i1
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_i2
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_i4
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_i8
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_r4
            new _OpcodeInfo(-2, _OperandKind.NONE),      // stind_r8
            new _OpcodeInfo(-1, _OperandKind.NONE),      // add
            new _OpcodeInfo(-1, _OperandKind.NONE),      // sub
            new _OpcodeInfo(-1, _OperandKind.NONE),      // mul
            new _OpcodeInfo(-1, _OperandKind.NONE),      // div
            new _OpcodeInfo(-1, _OperandKind.NONE),      // div_un
            new _OpcodeInfo(-1, _OperandKind.NONE),      // rem
            new _OpcodeInfo(-1, _OperandKind.NONE),      // rem_un
            new _OpcodeInfo(-1, _OperandKind.NONE),      // and
            new _OpcodeInfo(-1, _OperandKind.NONE),      // or
            new _OpcodeInfo(-1, _OperandKind.NONE),      // xor
            new _OpcodeInfo(-1, _OperandKind.NONE),      // shl
            new _OpcodeInfo(-1, _OperandKind.NONE),      // shr
            new _OpcodeInfo(-1, _OperandKind.NONE),      // shr_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // neg
            new _OpcodeInfo( 0, _OperandKind.NONE),      // not
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_i1
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_i2
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_i4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_i8
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_r4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_r8
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_u4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_u8
            new _OpcodeInfo( 127, _OperandKind.TOKEN),   // callvirt
            new _OpcodeInfo(-2, _OperandKind.NONE),      // cpobj
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // ldobj
            new _OpcodeInfo( 1, _OperandKind.TOKEN),     // ldstr
            new _OpcodeInfo( 127, _OperandKind.TOKEN),   // newobj
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // castclass
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // isinst
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_r_un
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // unbox
            new _OpcodeInfo(-127, _OperandKind.NONE),    // throw
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // ldfld
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // ldflda
            new _OpcodeInfo(-2, _OperandKind.TOKEN),     // stfld
            new _OpcodeInfo( 1, _OperandKind.TOKEN),     // ldsfld
            new _OpcodeInfo( 1, _OperandKind.TOKEN),     // ldsflda
            new _OpcodeInfo(-1, _OperandKind.TOKEN),     // stsfld
            new _OpcodeInfo(-2, _OperandKind.TOKEN),     // stobj
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i1_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i2_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i4_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i8_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u1_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u2_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u4_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u8_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u_un
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // box
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // newarr
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ldlen
            new _OpcodeInfo(-1, _OperandKind.TOKEN),     // ldelema
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_i1
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_u1
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_i2
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_u2
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_i4
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_u4
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_i8
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_i
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_r4
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_r8
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ldelem_ref
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_i
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_i1
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_i2
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_i4
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_i8
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_r4
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_r8
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stelem_ref
            new _OpcodeInfo(-1, _OperandKind.TOKEN),     // ldelem
            new _OpcodeInfo(-3, _OperandKind.TOKEN),     // stelem
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // unbox_any
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 0, _OperandKind.NONE),      //  conv_ovf_i1
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u1
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i2
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u2
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u4
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i8
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u8
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // refanyval
            new _OpcodeInfo( 0, _OperandKind.NONE),      // ckfinite
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // mkrefany
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 1, _OperandKind.TOKEN),     // ldtoken
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_u2
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_u1
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_i
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_i
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_ovf_u
            new _OpcodeInfo(-1, _OperandKind.NONE),      // add_ovf
            new _OpcodeInfo(-1, _OperandKind.NONE),      // add_ovf_un
            new _OpcodeInfo(-1, _OperandKind.NONE),      // mul_ovf
            new _OpcodeInfo(-1, _OperandKind.NONE),      // mul_ovf_un
            new _OpcodeInfo(-1, _OperandKind.NONE),      // sub_ovf
            new _OpcodeInfo(-1, _OperandKind.NONE),      // sub_ovf_un
            new _OpcodeInfo( 0, _OperandKind.NONE),      // endfinally
            new _OpcodeInfo(-127, _OperandKind.BR32),    // leave
            new _OpcodeInfo(-127, _OperandKind.BR8),     // leave_s
            new _OpcodeInfo(-3, _OperandKind.NONE),      // stind_i
            new _OpcodeInfo( 0, _OperandKind.NONE),      // conv_u
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
        };

        /// <summary>
        /// Double-byte opcode information. This array is indexed by the low byte of the opcode (high
        /// byte is always 0xFE).
        /// </summary>
        private static readonly _OpcodeInfo[] s_doubleByteOpcodeInfo = {
            new _OpcodeInfo( 1, _OperandKind.NONE),      // arglist
            new _OpcodeInfo(-1, _OperandKind.NONE),      // ceq
            new _OpcodeInfo(-1, _OperandKind.NONE),      // cgt
            new _OpcodeInfo(-1, _OperandKind.NONE),      // cgt_un
            new _OpcodeInfo(-1, _OperandKind.NONE),      // clt
            new _OpcodeInfo(-1, _OperandKind.NONE),      // clt_un
            new _OpcodeInfo( 1, _OperandKind.TOKEN),     // ldftn
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // ldvirtftn
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 1, _OperandKind.ARG16),     // ldarg
            new _OpcodeInfo( 1, _OperandKind.ARG16),     // ldarga
            new _OpcodeInfo(-1, _OperandKind.ARG16),     // starg
            new _OpcodeInfo( 1, _OperandKind.VAR16),     // ldloc
            new _OpcodeInfo( 1, _OperandKind.VAR16),     // ldloca
            new _OpcodeInfo(-1, _OperandKind.VAR16),     // stloc
            new _OpcodeInfo( 0, _OperandKind.NONE),      // localloc
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 0, _OperandKind.NONE),      // endfilter
            new _OpcodeInfo( 0, _OperandKind.NONE),      // unaligned_
            new _OpcodeInfo( 0, _OperandKind.NONE),      // volatile_
            new _OpcodeInfo( 0, _OperandKind.NONE),      // tail_
            new _OpcodeInfo(-1, _OperandKind.TOKEN),     // initobj
            new _OpcodeInfo( 0, _OperandKind.TOKEN),     // constrained_
            new _OpcodeInfo(-3, _OperandKind.NONE),      // cpblk
            new _OpcodeInfo(-3, _OperandKind.NONE),      // initblk
            new _OpcodeInfo( 0, _OperandKind.INT8),      // no_
            new _OpcodeInfo(-127, _OperandKind.NONE),    // rethrow
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
            new _OpcodeInfo( 1, _OperandKind.TOKEN),     // sizeof
            new _OpcodeInfo( 0, _OperandKind.NONE),      // refanytype
            new _OpcodeInfo( 0, _OperandKind.NONE),      // readonly_
            new _OpcodeInfo(-128, _OperandKind.INVALID), // [unused]
        };

        private const int MAX_EXCEPTION_HANDLERS = (0xFFFFFF - 4) / 24;

        /// <summary>
        /// The <see cref="ILTokenProvider"/> that provides the tokens for referring to types,
        /// members and strings in code emitted by this <see cref="ILBuilder"/>.
        /// </summary>
        private ILTokenProvider? m_tokenProvider;

        /// <summary>
        /// The byte array containing the emitted IL code.
        /// </summary>
        private byte[] m_codeBuffer = new byte[64];

        /// <summary>
        /// The current write position in the IL buffer.
        /// </summary>
        private int m_position;

        /// <summary>
        /// The current computed size of the IL evaluation stack.
        /// </summary>
        private int m_currentStackSize;

        /// <summary>
        /// The maximum evaluation stack size recorded so far.
        /// </summary>
        private int m_maxStackSize;

        /// <summary>
        /// Contains the types of the local variables.
        /// </summary>
        private DynamicArray<LocalTypeSignature> m_localSig;

        /// <summary>
        /// Contains additional local variable information.
        /// </summary>
        private DynamicArray<_LocalInfo> m_localInfo;

        /// <summary>
        /// Contains label information.
        /// </summary>
        private DynamicArray<_LabelInfo> m_labelInfo;

        /// <summary>
        /// Contains exception handler information.
        /// </summary>
        private DynamicArray<_ExceptionHandler> m_excHandlers;

        /// <summary>
        /// A stack of the currently open exception handlers.
        /// </summary>
        private DynamicArray<_ExceptionHandler> m_excHandlerStack;

        /// <summary>
        /// A list of byte offsets of virtual token locations in the code stream.
        /// </summary>
        private DynamicArray<int> m_virtualTokenLocations;

        /// <summary>
        /// The branch locations generated when emitting code.
        /// </summary>
        private DynamicArray<_BranchInfo> m_branches;

        /// <summary>
        /// Contains relocation information which is computed when long form branch instructions
        /// are converted to short form.
        /// </summary>
        private DynamicArray<_RelocationInfo> m_relocations;

        /// <summary>
        /// The position in the IL stream at which the last instruction was emitted.
        /// </summary>
        private int m_lastEmittedInstrPos = -1;

        /// <summary>
        /// This is true if a localloc instruction has been emitted.
        /// </summary>
        private bool m_hasLocAlloc = false;

        /// <summary>
        /// This is true if the method body should have the initlocals flag set.
        /// </summary>
        private bool m_initLocals = true;

        /// <summary>
        /// Creates a new <see cref="ILBuilder"/> instance.
        /// </summary>
        ///
        /// <param name="tokenProvider">The <see cref="ILTokenProvider"/> that will provide the
        /// tokens for referring to types, members and strings in code emitted by this
        /// <see cref="ILBuilder"/>. If this is null, the overloads of the emit method
        /// that take a type, member or string operand cannot be used.</param>
        public ILBuilder(ILTokenProvider? tokenProvider = null) {
            m_tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Resets the state of the <see cref="ILBuilder"/> so that it can be used to emit code for a new
        /// method.
        /// </summary>
        public void reset() {
            m_position = 0;
            m_lastEmittedInstrPos = -1;
            m_currentStackSize = 0;
            m_maxStackSize = 0;
            m_hasLocAlloc = false;
            m_initLocals = true;
            m_branches.clear();
            m_relocations.clear();
            m_labelInfo.clear();
            m_localSig.clear();
            m_localInfo.clear();
            m_excHandlers.clear();
            m_excHandlerStack.clear();
            m_virtualTokenLocations.clear();
        }

        /// <summary>
        /// Sets the token provider for this <see cref="ILBuilder"/> instance. This can
        /// only be done when no code has been emitted yet.
        /// </summary>
        ///
        /// <param name="tokenProvider">The <see cref="ILTokenProvider"/> that will provide the
        /// tokens for referring to types, members and strings in code emitted by this
        /// <see cref="ILBuilder"/>. If this is null, the overloads of the emit method
        /// that take a type, member or string operand cannot be used.</param>
        ///
        /// <exception cref="InvalidOperationException">This method is called when the <see cref="ILBuilder"/>
        /// instance contains emitted code.</exception>
        public void setTokenProvider(ILTokenProvider tokenProvider) {
            if (m_position > 0 || m_localInfo.length > 0 || m_excHandlers.length > 0)
                throw new InvalidOperationException("The token provider cannot be changed when code has already been emitted.");

            m_tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Throws if no token provider has been set.
        /// </summary>
        private void _throwIfTokenProviderNotSet() {
            if (m_tokenProvider == null)
                throw new NotSupportedException("A token provider is needed for using this method.");
        }

        /// <summary>
        /// Ensures that there is space in the code buffer for writing a given number of bytes. If the
        /// available space is insufficient, the buffer is expanded.
        /// </summary>
        /// <param name="nBytes">The number of bytes of space needed.</param>
        private void _ensureCodeBufferSpace(int nBytes) {
            if (m_codeBuffer.Length - m_position >= nBytes)
                return;
            byte[] newBuffer = new byte[Math.Max(m_position + nBytes, m_codeBuffer.Length * 2)];
            m_codeBuffer.AsSpan(0, m_position).CopyTo(newBuffer);
            m_codeBuffer = newBuffer;
        }

        /// <summary>
        /// Gets the kind of operand used with the given opcode.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        /// <returns>The kind of operand used with the given opcode.</returns>
        private static _OperandKind _getOperandKind(ILOp opcode) {
            return (((int)opcode & 0xFF00) != 0)
                ? s_doubleByteOpcodeInfo[(int)opcode & 255].operandKind
                : s_singleByteOpcodeInfo[(int)opcode].operandKind;
        }

        /// <summary>
        /// Gets the opcode of the instruction that was last emitted in the <see cref="ILBuilder"/>.
        /// </summary>
        /// <returns>The last opcode. If this is called before the first instruction is emitted, or
        /// after a call to <see cref="markLabel"/> and before any instruction is emitted after it,
        /// the value 0xFFFF is returned.</returns>
        private ILOp _getLastEmittedOpcode() {
            if (m_lastEmittedInstrPos == -1)
                return (ILOp)0xFFFF;
            int op = m_codeBuffer[m_lastEmittedInstrPos];
            return (op == 0xFE)
                ? (ILOp)((op << 8) | m_codeBuffer[m_lastEmittedInstrPos + 1])
                : (ILOp)op;
        }

        /// <summary>
        /// Declares a persistent local variable of the given type.
        /// </summary>
        /// <param name="type">A <see cref="TypeSignature"/> representing the type of the
        /// local variable.</param>
        /// <param name="isPinned">If true, declare that the object that the local variable refers to
        /// must be pinned in memory.</param>
        /// <returns>A <see cref="Local"/> that can be used to refer to the local variable in
        /// emitted code.</returns>
        /// <exception cref="NotSupportedException">The number of declared local variables exceeds
        /// 65534.</exception>
        ///
        /// <remarks>
        /// <para>
        /// If the <see cref="Local"/> instance returned by this method is passed to an emit method on
        /// another <see cref="ILBuilder"/>, it may refer to another (unspecified) local variable in
        /// the other method or may result in an exception being thrown.
        /// </para>
        /// <para>
        /// This overload cannot be used when the token provider for this <see cref="ILBuilder"/>
        /// has <see cref="ILTokenProvider.useLocalSigHelper"/> set to true (for instance, when
        /// using <see cref="DynamicMethodTokenProvider"/>) and <paramref name="type"/> does not
        /// represent a primitive type, otherwise an exception will be thrown when attempting
        /// to serialize the method body. Use the overload that takes a <see cref="Type"/>
        /// argument in this case.
        /// </para>
        /// </remarks>
        public Local declareLocal(in TypeSignature type, bool isPinned = false) {
            if (m_localInfo.length == 65535)
                throw new NotSupportedException("Local variable limit exceeded.");

            var signature = new LocalTypeSignature(type, isPinned);
            _LocalInfo localInfo = default;

            localInfo.status = _LocalStatus.PERSISTENT;
            localInfo.type = signature.type.getPrimitiveType();

            m_localSig.add(in signature);
            m_localInfo.add(in localInfo);

            return new Local((ushort)(m_localInfo.length - 1));
        }

        /// <summary>
        /// Declares a persistent local variable of the given type.
        /// </summary>
        /// <param name="type">A <see cref="Type"/> representing the type of the
        /// local variable.</param>
        /// <param name="isPinned">If true, declare that the object that the local variable refers to
        /// must be pinned in memory.</param>
        /// <returns>A <see cref="Local"/> that can be used to refer to the local variable in
        /// emitted code.</returns>
        /// <exception cref="NotSupportedException">The number of declared local variables exceeds
        /// 65534, or no token provider has been set.</exception>
        ///
        /// <remarks>
        /// If the <see cref="Local"/> instance returned by this method is passed to an emit method on
        /// another <see cref="ILBuilder"/>, it may refer to another (unspecified) local variable in
        /// the other method or may result in an exception being thrown.
        /// </remarks>
        public Local declareLocal(Type type, bool isPinned = false) {
            _throwIfTokenProviderNotSet();

            var local = declareLocal(m_tokenProvider!.getTypeSignature(type), isPinned);
            m_localInfo[local.id].type = type;

            return local;
        }

        /// <summary>
        /// Acquires a temporary local variable of the given type.
        /// </summary>
        /// <param name="type">A <see cref="TypeSignature"/> representing the type of the local variable.</param>
        /// <returns>A <see cref="Local"/> that can be used to refer to the local variable in
        /// emitted code.</returns>
        /// <exception cref="NotSupportedException">The number of declared local variables exceeds
        /// 65534.</exception>
        ///
        /// <remarks>
        /// <para>
        /// If the <see cref="Local"/> object returned by this method is passed to an emit method on
        /// another <see cref="ILBuilder"/>, it may refer to another (unspecified) local variable in
        /// the other method or may result in an exception being thrown.
        /// </para>
        /// <para>
        /// This overload cannot be used when the token provider for this <see cref="ILBuilder"/>
        /// has <see cref="ILTokenProvider.useLocalSigHelper"/> set to true (for instance, when
        /// using <see cref="DynamicMethodTokenProvider"/>) and <paramref name="type"/> does not
        /// represent a primitive type, otherwise an exception will be thrown when attempting
        /// to serialize the method body. Use the overload that takes a <see cref="Type"/>
        /// argument in this case.
        /// </para>
        /// </remarks>
        public Local acquireTempLocal(in TypeSignature type) {
            int localId = -1;

            for (int i = 0, n = m_localInfo.length; i < n; i++) {
                ref var localInfo = ref m_localInfo[i];
                if (localInfo.status == _LocalStatus.TEMP_DISPOSED && m_localSig[i].type.Equals(type)) {
                    localInfo.status = _LocalStatus.TEMP_ACTIVE;
                    localId = i;
                    break;
                }
            }

            if (localId == -1) {
                localId = declareLocal(type).id;
                m_localInfo[localId].status = _LocalStatus.TEMP_ACTIVE;
            }

            return new Local((ushort)localId);
        }

        /// <summary>
        /// Acquires a temporary local variable of the given type.
        /// </summary>
        /// <param name="type">A <see cref="Type"/> representing the type of the local variable.</param>
        /// <returns>A <see cref="Local"/> that can be used to refer to the local variable in
        /// emitted code.</returns>
        /// <exception cref="NotSupportedException">The number of declared local variables exceeds
        /// 65534, or no token provider has been set.</exception>
        ///
        /// <remarks>
        /// If the <see cref="Local"/> object returned by this method is passed to an emit method on
        /// another <see cref="ILBuilder"/>, it may refer to another (unspecified) local variable in
        /// the other method or may result in an exception being thrown. No explicit checking is done.
        /// </remarks>
        public Local acquireTempLocal(Type type) {
            _throwIfTokenProviderNotSet();

            var local = acquireTempLocal(m_tokenProvider!.getTypeSignature(type));

            ref _LocalInfo localInfo = ref m_localInfo[local.id];
            if (localInfo.type == null)
                localInfo.type = type;

            return local;
        }

        /// <summary>
        /// Releases a temporary local variable that has been obtained by a call to
        /// <see cref="acquireTempLocal(Type)"/> or <see cref="acquireTempLocal(in TypeSignature)"/>.
        /// The released variable will be available for reuse when a temporary variable of the same
        /// type is requested by a later call to <see cref="acquireTempLocal(Type)"/> or
        /// <see cref="acquireTempLocal(in TypeSignature)"/>.
        /// </summary>
        ///
        /// <param name="tempLocal">The <see cref="Local"/> representing the temporary variable to
        /// be released.</param>
        ///
        /// <exception cref="ArgumentException"><paramref name="tempLocal"/> does not represent an
        /// active temporary variable.</exception>
        public void releaseTempLocal(Local tempLocal) {
            if ((uint)tempLocal.id >= (uint)m_localInfo.length
                || m_localInfo[tempLocal.id].status != _LocalStatus.TEMP_ACTIVE)
            {
                throw new ArgumentException("tempLocal must be an active temporary variable.", nameof(tempLocal));
            }

            m_localInfo[tempLocal.id].status = _LocalStatus.TEMP_DISPOSED;
        }

        /// <summary>
        /// Gets the type of a declared local variable.
        /// </summary>
        /// <param name="localVar">A <see cref="Local"/> obtained from this <see cref="ILBuilder"/>
        /// representing the local variable whose type is to be obtained.</param>
        /// <returns>A <see cref="LocalTypeSignature"/> representing the type of the local variable.</returns>
        /// <exception cref="ArgumentException"><paramref name="localVar"/> does not represent a
        /// defined local variable.</exception>
        public LocalTypeSignature getTypeOfLocalVariable(Local localVar) {
            if ((uint)localVar.id >= (uint)m_localInfo.length
               || m_localInfo[localVar.id].status == _LocalStatus.TEMP_DISPOSED)
            {
                throw new ArgumentException("localVar is not defined.", nameof(localVar));
            }
            return m_localSig[localVar.id];
        }

        /// <summary>
        /// Defines a label that can be used as a branch target.
        /// </summary>
        /// <returns>A <see cref="Label"/> object representing the label. The Label instance can be
        /// passed to the <see cref="emit(ILOp, Label)"/> method to use it as a branch target. To
        /// mark the position of the label in the code, use the <see cref="markLabel"/>
        /// method.</returns>
        ///
        /// <remarks>
        /// If the <see cref="Label"/> object returned by this method is passed to a method on
        /// another <see cref="ILBuilder"/>, it may refer to another (unspecified) label in the
        /// other method or may result in an exception being thrown.
        /// </remarks>
        public Label createLabel() {
            var labelInfo = new _LabelInfo {targetPosition = -1, stackSize = -1};
            m_labelInfo.add(in labelInfo);
            return new Label(m_labelInfo.length - 1);
        }

        /// <summary>
        /// Defines a set of labels that can be used as branch targets for a switch instruction.
        /// </summary>
        /// <param name="count">The number of labels in the group.</param>
        /// <returns>A <see cref="LabelGroup"/> representing the set of labels created.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or
        /// equal to 0.</exception>
        ///
        /// <remarks>
        /// If any label in the <see cref="LabelGroup"/> returned by this method is passed to a
        /// method on another <see cref="ILBuilder"/>, it may refer to another (unspecified) label in
        /// the other method or may result in an exception being thrown.
        /// </remarks>
        public LabelGroup createLabelGroup(int count) {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            _LabelInfo labelInfo;
            labelInfo.targetPosition = -1;
            labelInfo.stackSize = -1;

            int firstLabelId = m_labelInfo.length;
            for (int i = 0; i < count; i++)
                m_labelInfo.add(in labelInfo);

            return new LabelGroup(firstLabelId, count);
        }

        /// <summary>
        /// Sets the position of the given label to the current position in the code stream.
        /// </summary>
        /// <param name="label">The label whose position is to be marked.</param>
        /// <exception cref="ArgumentException">The position of the given label has already been
        /// set.</exception>
        public void markLabel(Label label) {
            if ((uint)label.id >= (uint)m_labelInfo.length)
                throw new ArgumentOutOfRangeException(nameof(label), "The label is not defined.");

            _LabelInfo labelInfo = m_labelInfo[label.id];
            if (labelInfo.targetPosition != -1)
                throw new ArgumentException("Label has already been marked.", nameof(label));

            m_labelInfo[label.id].targetPosition = m_position;

            if (labelInfo.stackSize != -1 && m_currentStackSize < labelInfo.stackSize)
                m_currentStackSize = labelInfo.stackSize;

            m_lastEmittedInstrPos = -1;
        }

        /// <summary>
        /// Begins an exception handler. Code emitted after this method is called will belong to the
        /// "try" region of the handler. To end the "try" region and emit the handling clauses (catch,
        /// filter, fault or finally), call one of the associated methods.
        /// </summary>
        ///
        /// <exception cref="NotSupportedException">A filter clause is currently being
        /// emitted.</exception>
        public void beginExceptionHandler() {
            if (m_excHandlerStack.length != 0 && m_excHandlerStack[m_excHandlerStack.length - 1].filterStart != -1)
                throw new NotSupportedException("Filter clauses cannot contain nested exception handlers.");

            if (m_excHandlers.length == MAX_EXCEPTION_HANDLERS)
                throw new NotSupportedException("Exception handler limit exceeded.");

            _ExceptionHandler excHandler = new _ExceptionHandler();
            excHandler.tryStart = m_position;
            excHandler.tryLength = -1;
            excHandler.filterStart = -1;
            excHandler.handlerStart = -1;
            excHandler.handlerLength = -1;
            excHandler.isContinuation = false;
            excHandler.catchType = default(EntityHandle);
            excHandler.clauseType = ExceptionRegionKind.Catch;
            excHandler.handlerEndLabel = createLabel();

            m_excHandlers.add(excHandler);
            m_excHandlerStack.add(excHandler);
        }

        /// <summary>
        /// Begins a filter clause for the current exception handler.
        /// </summary>
        /// <exception cref="InvalidOperationException">This method is called when a filter block is
        /// being emitted or there is no open exception handler.</exception>
        public void beginFilterClause() {
            if (m_excHandlerStack.length == 0)
                throw new InvalidOperationException("No exception handlers currently open.");

            _ExceptionHandler excHandler = m_excHandlerStack[m_excHandlerStack.length - 1];

            if (excHandler.tryLength != -1) {
                if (excHandler.handlerStart == -1)
                    throw new InvalidOperationException("Exception filter clause must be follwed by a catch clause.");
                excHandler = _duplicateExceptionHandler(false);
            }
            else {
                _emitLeaveAtEndOfBlock(excHandler);
                excHandler.tryLength = m_position - excHandler.tryStart;
            }
            excHandler.filterStart = m_position;

            m_currentStackSize = 1;
            m_maxStackSize = Math.Max(m_maxStackSize, m_currentStackSize);
        }

        /// <summary>
        /// Begins a catch clause for the current exception handler.
        /// </summary>
        /// <param name="catchType">A handle to the type of exception that the clause must catch.
        /// If this is a null handle (the default), any type of exception will be caught. This must
        /// be a null handle if a filter clause was emitted before starting the catch clause.</param>
        /// <exception cref="ArgumentException"><paramref name="catchType"/> is not null and a filter
        /// clause was emitted before calling this method; or <paramref name="catchType"/> is null and
        /// no filter clause was emitted before calling this method; or <paramref name="catchType"/>
        /// is not a handle to a type.</exception>
        /// <exception cref="InvalidOperationException">There is no open exception handler.</exception>
        public void beginCatchClause(EntityHandle catchType = default) {
            if (m_excHandlerStack.length == 0)
                throw new InvalidOperationException("No exception handlers are currently open.");

            _ExceptionHandler excHandler = m_excHandlerStack[m_excHandlerStack.length - 1];
            bool isFiltered = excHandler.filterStart != -1 && excHandler.handlerStart == -1;

            if (excHandler.tryLength != -1) {
                if (isFiltered) {
                    if (!catchType.IsNil)
                        throw new ArgumentException("A filtered exception handler must not have a catch type.", nameof(catchType));

                    if (_getLastEmittedOpcode() != ILOp.endfilter)
                        emit(ILOp.endfilter);
                }
                else {
                    if (catchType.IsNil)
                        throw new ArgumentException("A non-filtered exception handler must have a catch type.", nameof(catchType));

                    if (catchType.Kind != HandleKind.TypeDefinition
                        && catchType.Kind != HandleKind.TypeReference
                        && catchType.Kind != HandleKind.TypeSpecification)
                    {
                        throw new ArgumentException("Catch type handle must be a TypeDef, TypeRef or TypeSpec.", nameof(catchType));
                    }

                    _ExceptionHandler dupExcHandler = _duplicateExceptionHandler(false);
                    if (excHandler.clauseType == ExceptionRegionKind.Fault
                        || excHandler.clauseType == ExceptionRegionKind.Finally)
                    {
                        dupExcHandler.tryLength = m_position - excHandler.tryStart;
                    }
                    excHandler = dupExcHandler;
                }
            }
            else {
                _emitLeaveAtEndOfBlock(excHandler);
                excHandler.tryLength = m_position - excHandler.tryStart;
            }

            excHandler.handlerStart = m_position;
            excHandler.clauseType = isFiltered ? ExceptionRegionKind.Filter : ExceptionRegionKind.Catch;
            excHandler.catchType = catchType;

            m_currentStackSize = 1;
            m_maxStackSize = Math.Max(m_maxStackSize, m_currentStackSize);
        }

        /// <summary>
        /// Begins a catch clause for the current exception handler.
        /// </summary>
        /// <param name="catchType">A <see cref="Type"/> representing the type of exception that the
        /// clause must catch. This must be null if a filter clause was emitted before starting the
        /// catch clause, and non-null otherwise.</param>
        /// <exception cref="ArgumentException"><paramref name="catchType"/> is not null and a filter
        /// clause was emitted before calling this method, or <paramref name="catchType"/> is null and
        /// no filter clause was emitted before calling this method.</exception>
        /// <exception cref="InvalidOperationException">There is no open exception handler.</exception>
        /// <exception cref="NotSupportedException">No token provider has been set.</exception>
        public void beginCatchClause(Type catchType) {
            if (catchType == null) {
                beginCatchClause();
            }
            else {
                _throwIfTokenProviderNotSet();
                beginCatchClause(m_tokenProvider!.getHandle(catchType));
            }
        }

        /// <summary>
        /// Begins a fault clause for the current exception handler.
        /// </summary>
        /// <exception cref="InvalidOperationException">This method is called when a filter block is
        /// being emitted OR there is no open exception handler.</exception>
        public void beginFaultClause() {
            if (m_excHandlerStack.length == 0)
                throw new InvalidOperationException("No exception handlers currently open.");

            _ExceptionHandler excHandler = m_excHandlerStack[m_excHandlerStack.length - 1];
            if (excHandler.filterStart != -1 && excHandler.handlerStart == -1)
                throw new InvalidOperationException("Exception filter clause must be follwed by a catch clause.");

            if (excHandler.tryLength != -1) {
                excHandler = _duplicateExceptionHandler(true);
                excHandler.tryLength = m_position - excHandler.tryStart;
            }
            else {
                _emitLeaveAtEndOfBlock(excHandler);
                excHandler.tryLength = m_position - excHandler.tryStart;
            }

            excHandler.handlerStart = m_position;
            excHandler.clauseType = ExceptionRegionKind.Fault;

            m_currentStackSize = 0;
        }

        /// <summary>
        /// Begins a finally clause for the current exception handler.
        /// </summary>
        /// <exception cref="InvalidOperationException">This method is called when a filter block is
        /// being emitted OR there is no open exception handler.</exception>
        public void beginFinallyClause() {
            if (m_excHandlerStack.length == 0)
                throw new InvalidOperationException("No exception handlers currently open.");

            _ExceptionHandler excHandler = m_excHandlerStack[m_excHandlerStack.length - 1];
            if (excHandler.filterStart != -1 && excHandler.handlerStart == -1)
                throw new InvalidOperationException("Exception filter clause must be follwed by a catch clause.");

            if (excHandler.tryLength != -1) {
                excHandler = _duplicateExceptionHandler(true);
                excHandler.tryLength = m_position - excHandler.tryStart;
            }
            else {
                _emitLeaveAtEndOfBlock(excHandler);
                excHandler.tryLength = m_position - excHandler.tryStart;
            }

            excHandler.handlerStart = m_position;
            excHandler.clauseType = ExceptionRegionKind.Finally;

            m_currentStackSize = 0;
        }

        /// <summary>
        /// Ends the current exception handler. If a nested exception handler is being emitted, code
        /// emitted after calling this method will belong to its parent handler.
        /// </summary>
        /// <exception cref="InvalidOperationException">The current exception handler has only a try
        /// block and no catch, filter, fault or finally clauses; or an unfinished filter clause (one
        /// that is not followed by a catch clause); or there is no open exception handler.</exception>
        public void endExceptionHandler() {
            if (m_excHandlerStack.length == 0)
                throw new InvalidOperationException("No exception handlers currently open.");

            _ExceptionHandler excHandler = m_excHandlerStack[m_excHandlerStack.length - 1];

            if (excHandler.tryLength == -1)
                throw new InvalidOperationException("Exception handler does not have any catch, fault or finally clauses.");

            if (excHandler.filterStart != -1 && excHandler.handlerStart == -1)
                throw new InvalidOperationException("Exception filter clause must be follwed by a catch clause.");

            if ((excHandler.clauseType == ExceptionRegionKind.Fault
                    || excHandler.clauseType == ExceptionRegionKind.Finally)
                && _getLastEmittedOpcode() != ILOp.endfinally)
            {
                emit(ILOp.endfinally);
            }
            else {
                _emitLeaveAtEndOfBlock(excHandler);
            }

            excHandler.handlerLength = m_position - excHandler.handlerStart;
            markLabel(excHandler.handlerEndLabel);

            while (excHandler.isContinuation) {
                m_excHandlerStack.removeLast();
                excHandler = m_excHandlerStack[m_excHandlerStack.length - 1];
            }
            m_excHandlerStack.removeLast();
        }

        /// <summary>
        /// Ends the current exception handling clause and pushes a new handler onto the stack with
        /// the same "try" region as the current handler.
        /// </summary>
        /// <param name="isFaultOrFinally">Set to true if the new handler is intended to have a
        /// fault or finally clause.</param>
        /// <returns>The new handler, which becomes the current handler after calling this method.</returns>
        private _ExceptionHandler _duplicateExceptionHandler(bool isFaultOrFinally) {
            if (m_excHandlers.length == MAX_EXCEPTION_HANDLERS)
                throw new NotSupportedException("Exception handler limit exceeded.");

            _ExceptionHandler handler = m_excHandlerStack[m_excHandlerStack.length - 1];
            _ExceptionHandler newHandler = new _ExceptionHandler();

            bool sharedTryAllowed = !isFaultOrFinally;

            if (handler.clauseType == ExceptionRegionKind.Fault
                || handler.clauseType == ExceptionRegionKind.Finally)
            {
                if (_getLastEmittedOpcode() != ILOp.endfinally)
                    emit(ILOp.endfinally);
                sharedTryAllowed = false;
            }
            else {
                _emitLeaveAtEndOfBlock(handler);
            }

            handler.handlerLength = m_position - handler.handlerStart;

            newHandler.tryStart = handler.tryStart;
            newHandler.tryLength = sharedTryAllowed ? handler.tryLength : m_position - handler.tryStart;
            newHandler.filterStart = -1;
            newHandler.handlerStart = -1;
            newHandler.handlerLength = -1;
            newHandler.handlerEndLabel = handler.handlerEndLabel;
            newHandler.isContinuation = true;

            m_excHandlers.add(newHandler);
            m_excHandlerStack.add(newHandler);

            return newHandler;
        }

        /// <summary>
        /// Emits a leave instruction whose target is the end of the given exception handler's region,
        /// if the last instruction emitted was not a leave instruction.
        /// </summary>
        /// <param name="handler">The handler whose end is to be used as the leave target.</param>
        private void _emitLeaveAtEndOfBlock(_ExceptionHandler handler) {
            ILOp lastOp = _getLastEmittedOpcode();
            if (lastOp != ILOp.leave && lastOp != ILOp.@throw && lastOp != ILOp.rethrow)
                emit(ILOp.leave, handler.handlerEndLabel);
        }

        /// <summary>
        /// Sets the size of the IL evaluation stack at the current position in the code stream.
        /// </summary>
        /// <param name="stackSize">The number of elements on the stack.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="stackSize"/> is less than
        /// 0.</exception>
        ///
        /// <remarks>
        /// The only situation in which the stack size has to be manually provided is after marking the
        /// target of a backward branch with a non-empty stack. In all other cases, the
        /// <see cref="ILBuilder"/> computes the stack size automatically as instructions are
        /// emitted.
        /// </remarks>
        public void setCurrentStackSize(int stackSize) {
            if (stackSize < 0)
                throw new ArgumentOutOfRangeException(nameof(stackSize));
            m_currentStackSize = stackSize;
            m_maxStackSize = Math.Max(m_maxStackSize, m_currentStackSize);
        }

        /// <summary>
        /// Sets the value of the initlocals flag for the method body.
        /// </summary>
        /// <param name="value">The value of the initlocals flag.</param>
        /// <remarks>
        /// If the initlocals flag is true, all local variables and memory allocated with the
        /// localloc instruction will be zero-initialized. Setting it to false will result in
        /// the code being unverifiable. The default value (when a new <see cref="ILBuilder"/> is
        /// constructed or after calling <see cref="reset"/>) is true.
        /// </remarks>
        public void setInitLocals(bool value) {
            m_initLocals = value;
        }

        /// <summary>
        /// Updates the stack size after emitting an instruction.
        /// </summary>
        /// <param name="opcode">The opcode emitted.</param>
        private void _updateStack(ILOp opcode) {
            int stackDelta = (((int)opcode & 0xFF00) == 0)
                ? s_singleByteOpcodeInfo[(int)opcode].stackDelta
                : s_doubleByteOpcodeInfo[(int)opcode & 255].stackDelta;

            m_currentStackSize = (stackDelta == -127) ? 0 : m_currentStackSize + stackDelta;
            m_maxStackSize = Math.Max(m_maxStackSize, m_currentStackSize);
        }

        /// <summary>
        /// Writes the given opcode to the IL stream.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        private void _writeOpcode(ILOp opcode) {
            m_lastEmittedInstrPos = m_position;

            if (((int)opcode & 0xFF00) == 0) {
                m_codeBuffer[m_position] = (byte)opcode;
                m_position++;
            }
            else {
                m_codeBuffer[m_position] = (byte)((int)opcode >> 8);
                m_codeBuffer[m_position + 1] = (byte)opcode;
                m_position += 2;
            }
        }

        /// <summary>
        /// Writes an opcode that refers to an argument or a local variable.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        /// <param name="localId">The index of the local variable or argument. This must be set to -1
        /// if the identifier is in the opcode itself.</param>
        private void _writeInstructionWithLocalRef(ILOp opcode, int localId) {
            if (localId == -1) {
                // The order of the checks is important here!
                if ((int)opcode >= (int)ILOp.stloc_0) {
                    localId = (int)opcode - (int)ILOp.ldloc_0;
                    opcode = ILOp.ldloc;
                }
                else if ((int)opcode >= (int)ILOp.ldloc_0) {
                    localId = (int)opcode - (int)ILOp.ldloc_0;
                    opcode = ILOp.stloc;
                }
                else if ((int)opcode >= (int)ILOp.ldarg_0) {
                    localId = (int)opcode - (int)ILOp.ldarg_0;
                    opcode = ILOp.ldarg;
                }
            }
            else if ((int)opcode < 256) {
                opcode = (ILOp)((int)opcode + 0xFDFB);  // Convert short form to long form
            }

            if (localId < 4) {
                // Use single-byte forms wherever possible
                if (opcode == ILOp.ldarg) {
                    opcode = (ILOp)((int)ILOp.ldarg_0 + localId);
                    localId = -1;
                }
                else if (opcode == ILOp.ldloc) {
                    opcode = (ILOp)((int)ILOp.ldloc_0 + localId);
                    localId = -1;
                }
                else if (opcode == ILOp.stloc) {
                    opcode = (ILOp)((int)ILOp.stloc_0 + localId);
                    localId = -1;
                }
            }

            if (localId != -1 && localId < 256)
                opcode = (ILOp)((int)opcode - 0xFDFB);  // Convert back to short form

            m_lastEmittedInstrPos = m_position;
            _ensureCodeBufferSpace(4);

            if ((int)opcode < 256) {
                m_codeBuffer[m_position++] = (byte)opcode;
                if (localId != -1)
                    m_codeBuffer[m_position++] = (byte)localId;
            }
            else {
                WriteInt32LittleEndian(
                    m_codeBuffer.AsSpan(m_position, 4),
                    (byte)((int)opcode >> 8) | (byte)opcode << 8 | localId << 16
                );
                m_position += 4;
            }
        }

        /// <summary>
        /// Writes a metadata token into the IL stream.
        /// </summary>
        /// <param name="token">The metadata token.</param>
        private void _writeToken(EntityHandle token) {
            if (m_tokenProvider != null && m_tokenProvider.isVirtualHandle(token))
                m_virtualTokenLocations.add(m_position);

            WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), MetadataTokens.GetToken(token));
            m_position += 4;
        }

        /// <summary>
        /// Emits an instruction into the IL stream.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        public void emit(ILOp opcode) {
            _OperandKind opKind = _getOperandKind(opcode);
            if (opKind == _OperandKind.NONE) {
                _ensureCodeBufferSpace(2);
                _writeOpcode(opcode);
            }
            else if (opKind == _OperandKind.ARG0) {
                _writeInstructionWithLocalRef(opcode, -1);
            }
            else {
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with no argument.", nameof(opcode));
            }

            _updateStack(opcode);

            if (opcode == ILOp.localloc)
                m_hasLocAlloc = true;
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a 32-bit integer operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The integer operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        ///
        /// <remarks>
        /// <para>Only opcodes taking the following kinds of operands are permitted to be emitted by
        /// this overload of the emit method: integer (any size), floating-point or argument
        /// reference.</para>
        /// <para>If the opcode requires a floating point or 64-bit integer operand, the corresponding
        /// numeric conversion is done before emitting. Floating point conversions may result in
        /// precision loss.</para>
        /// </remarks>
        public void emit(ILOp opcode, int arg) {
            switch (_getOperandKind(opcode)) {
                case _OperandKind.INT8:
                    _ensureCodeBufferSpace(3);
                    _writeOpcode(opcode);
                    m_codeBuffer[m_position] = (byte)arg;
                    m_position++;
                    _updateStack(opcode);
                    break;

                case _OperandKind.INT16:
                    _ensureCodeBufferSpace(4);
                    _writeOpcode(opcode);
                    WriteInt16LittleEndian(m_codeBuffer.AsSpan(m_position, 2), (short)arg);
                    m_position += 2;
                    _updateStack(opcode);
                    break;

                case _OperandKind.INT32: {
                    if (opcode == ILOp.ldc_i4_s)
                        opcode = ILOp.ldc_i4;

                    bool isLdcI4ShortForm = false;

                    if (opcode == ILOp.ldc_i4) {
                        if ((uint)(arg + 1) <= 9) {
                            // arg is between -1 and 8
                            _ensureCodeBufferSpace(1);
                            _writeOpcode((ILOp)((int)ILOp.ldc_i4_0 + arg));
                            _updateStack(opcode);
                            isLdcI4ShortForm = true;
                        }
                        else if ((uint)(arg + 128) <= 255) {
                            // arg is between -128 and 127
                            _ensureCodeBufferSpace(2);
                            _writeOpcode(ILOp.ldc_i4_s);
                            m_codeBuffer[m_position] = (byte)arg;
                            m_position++;
                            isLdcI4ShortForm = true;
                        }
                    }

                    if (!isLdcI4ShortForm) {
                        _ensureCodeBufferSpace(6);
                        _writeOpcode(opcode);
                        WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), arg);
                        m_position += 4;
                    }

                    _updateStack(opcode);
                    break;
                }

                case _OperandKind.INT64:
                    emit(opcode, (long)arg);
                    break;

                case _OperandKind.FLOAT32:
                case _OperandKind.FLOAT64:
                    emit(opcode, (double)arg);
                    break;

                case _OperandKind.ARG8:
                case _OperandKind.ARG16:
                    _writeInstructionWithLocalRef(opcode, arg);
                    _updateStack(opcode);
                    break;

                default:
                    throw new ArgumentException("Opcode '" + opcode + "' cannot be used with an integer argument.", nameof(opcode));
            }
        }

        /// <summary>
        /// Emits a branch instruction into the IL stream.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        /// <param name="arg">The label to be used as the target of the branch.</param>
        /// <exception cref="ArgumentException">The label is not valid OR the opcode is not that of a
        /// branch instruction.</exception>
        ///
        /// <remarks>
        /// Both the short forms of the branch instructions (with the _s prefixes) and their
        /// corresponding long forms have identical behaviour with respect to this method. The form of
        /// the instruction actually emitted is determined by the distance in the IL byte stream
        /// between where the instruction is emitted and where the label is marked.
        /// </remarks>
        public void emit(ILOp opcode, Label arg) {
            if ((uint)arg.id >= (uint)m_labelInfo.length)
                throw new ArgumentException("The label is not defined.", nameof(arg));

            _OperandKind opKind = _getOperandKind(opcode);
            if (opKind == _OperandKind.BR8) {
                // Convert to corresponding long branch. Long branches are converted
                // to short branches wherever possible during branch patching.
                opcode = (opcode == ILOp.leave_s) ? ILOp.leave : (ILOp)((int)opcode + 13);
                opKind = _OperandKind.BR32;
            }

            if (opKind != _OperandKind.BR32)
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a label argument.", nameof(opcode));

            var branchInfo = new _BranchInfo {
                offsetPosition = m_position + 1,
                basePosition = m_position + 5,
                target = arg,
                opcode = opcode,
            };
            m_branches.add(in branchInfo);

            _ensureCodeBufferSpace(5);
            _writeOpcode(opcode);
            m_position += 4;
            _updateStack(opcode);

            if (m_labelInfo[arg.id].stackSize == -1)
                m_labelInfo[arg.id].stackSize = (opcode == ILOp.leave) ? 0 : m_currentStackSize;

            if (opcode == ILOp.br || opcode == ILOp.leave) {
                // Assume stack to be empty after an unconditional branch. This may be
                // updated if a label with a known stack size is marked at this point.
                // (See CLI spec, Partition III, Section 1.7.5)
                // If the stack is intended to be nonempty (which is non-standard IL)
                // then callers must call the setCurrentStackSize() method.
                m_currentStackSize = 0;
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream whose operand is a reference to a local variable.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        /// <param name="arg">A <see cref="Local"/> representing the local variable to be used as
        /// the operand.</param>
        /// <exception cref="ArgumentException"><paramref name="arg"/> does not represent a defined
        /// local variable OR the opcode does not use a local variable operand.</exception>
        ///
        /// <remarks>
        /// Both the short form opcodes (the ones with the _s suffixes) and their corresponding long
        /// form opcodes have identical behaviour with respect to this method. The form of the opcode
        /// actually emitted is determined by the internal index of the local variable. The
        /// zero-operand opcodes (e.g. ldloc_0) cannot be used with this method but may be emitted
        /// into the IL stream, if the index of a local variable allows for its use.
        /// </remarks>
        public void emit(ILOp opcode, Local arg) {
            if ((uint)arg.id >= (uint)m_localInfo.length)
                throw new ArgumentException("The local variable is not defined.", nameof(arg));

            _OperandKind opKind = _getOperandKind(opcode);
            if (opKind != _OperandKind.VAR8 && opKind != _OperandKind.VAR16) {
                throw new ArgumentException(
                    "Opcode '" + opcode + "' cannot be used with a local variable argument.", nameof(opcode));
            }

            _writeInstructionWithLocalRef(opcode, arg.id);
            _updateStack(opcode);
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a 64-bit integer operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The integer operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        ///
        /// <remarks>
        /// <para>Only opcodes taking 64-bit integer or floating-point operands are permitted to be
        /// emitted by this overload of the emit method.</para>
        /// <para>If the opcode requires a floating point operand, the corresponding numeric
        /// conversion is done before emitting. Floating point conversions may result in precision
        /// loss.</para>
        /// </remarks>
        public void emit(ILOp opcode, long arg) {
            _OperandKind operandType = _getOperandKind(opcode);

            if (operandType == _OperandKind.INT64) {
                bool isMorphed = false;

                if (opcode == ILOp.ldc_i8) {
                    if ((ulong)(arg + 1) <= 9) {
                        // arg is between -1 and 8
                        _ensureCodeBufferSpace(2);
                        _writeOpcode((ILOp)((int)ILOp.ldc_i4_0 + (int)arg));
                        _writeOpcode(ILOp.conv_i8);
                        isMorphed = true;
                    }
                    else if ((ulong)(arg + 128) <= 255) {
                        // arg is between -128 and 127
                        _ensureCodeBufferSpace(4);
                        _writeOpcode(ILOp.ldc_i4_s);
                        m_codeBuffer[m_position++] = (byte)arg;
                        _writeOpcode(ILOp.conv_i8);
                        isMorphed = true;
                    }
                    else if (arg == (int)arg) {
                        _ensureCodeBufferSpace(7);
                        _writeOpcode(ILOp.ldc_i4_s);
                        WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), (int)arg);
                        m_position += 4;
                        _writeOpcode(ILOp.conv_i8);
                        isMorphed = true;
                    }
                }

                if (!isMorphed) {
                    _ensureCodeBufferSpace(10);
                    _writeOpcode(opcode);
                    WriteInt64LittleEndian(m_codeBuffer.AsSpan(m_position, 8), arg);
                    m_position += 8;
                }
            }
            else if (operandType == _OperandKind.FLOAT32) {
                emit(opcode, (float)arg);
            }
            else if (operandType == _OperandKind.FLOAT64) {
                emit(opcode, (double)arg);
            }
            else {
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a long integer argument.", nameof(opcode));
            }

            _updateStack(opcode);
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a 32-bit integer operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The integer operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        ///
        /// <remarks>
        /// <para>Only opcodes taking the following kinds of operands are permitted to be emitted by
        /// this overload of the emit method: integer (any size), floating-point or argument
        /// reference.</para>
        /// <para>If the opcode requires a floating point or 64-bit integer operand, the corresponding
        /// numeric conversion is done before emitting. Floating point conversions may result in
        /// precision loss.</para>
        /// </remarks>
        public void emit(ILOp opcode, uint arg) {
            switch (_getOperandKind(opcode)) {
                case _OperandKind.FLOAT32:
                    emit(opcode, (float)arg);
                    break;
                case _OperandKind.FLOAT64:
                    emit(opcode, (double)arg);
                    break;
                default:
                    emit(opcode, (int)arg);
                    break;
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a 64-bit integer operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The integer operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        ///
        /// <remarks>
        /// <para>Only opcodes taking 64-bit integer or floating-point operands are permitted to be
        /// emitted by this overload of the emit method.</para>
        /// <para>If the opcode requires a floating point operand, the corresponding numeric
        /// conversion is done before emitting. Floating point conversions may result in precision
        /// loss.</para>
        /// </remarks>
        public void emit(ILOp opcode, ulong arg) {
            switch (_getOperandKind(opcode)) {
                case _OperandKind.FLOAT32:
                    emit(opcode, (float)arg);
                    break;
                case _OperandKind.FLOAT64:
                    emit(opcode, (double)arg);
                    break;
                default:
                    emit(opcode, (long)arg);
                    break;
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream with floating-point operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The floating-point operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        ///
        /// <remarks>
        /// This method accepts a 64-bit floating point number as the operand. If the opcode uses a
        /// 32-bit float operand, a narrowing conversion is done, which may result in precision loss,
        /// if the value of the operand passed is not the result of a widening conversion from float
        /// to double.
        /// </remarks>
        public void emit(ILOp opcode, double arg) {
            if (opcode == ILOp.ldc_r8) {
                // Double
                _ensureCodeBufferSpace(9);
                _writeOpcode(ILOp.ldc_r8);
                WriteInt64LittleEndian(m_codeBuffer.AsSpan(m_position, 8), BitConverter.DoubleToInt64Bits(arg));
                m_position += 8;
            }
            else if (opcode == ILOp.ldc_r4) {
                // Float
                _ensureCodeBufferSpace(5);
                _writeOpcode(ILOp.ldc_r4);
                WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), BitConverter.SingleToInt32Bits((float)arg));
                m_position += 4;
            }
            else {
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a float argument.", nameof(opcode));
            }

            _updateStack(opcode);
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a string literal operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The string whose token to emit as the operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <exception cref="NotSupportedException">No token provider has been set.</exception>
        /// <remarks>
        /// Only the ldstr instruction can be emitted using this overload of the emit method.
        /// </remarks>
        public void emit(ILOp opcode, string? arg) {
            if (opcode != ILOp.ldstr)
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a string argument.", nameof(opcode));

            if (arg == null) {
                _ensureCodeBufferSpace(1);
                _writeOpcode(ILOp.ldnull);
                _updateStack(opcode);
            }
            else {
                _throwIfTokenProviderNotSet();
                emit(opcode, m_tokenProvider!.getHandle(arg));
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a string handle operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The string handle to emit as the operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <remarks>
        /// Only the ldstr instruction can be emitted using this overload of the emit method.
        /// </remarks>
        public void emit(ILOp opcode, UserStringHandle arg) {
            if (opcode != ILOp.ldstr)
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a string argument.", nameof(opcode));

            int token = MetadataTokens.GetToken(arg);

            _ensureCodeBufferSpace(5);
            _writeOpcode(ILOp.ldstr);
            WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), token);
            m_position += 4;

            _updateStack(opcode);
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a token operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The <see cref="EntityHandle"/> representing the metadata token to
        /// be emitted as the operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        public void emit(ILOp opcode, EntityHandle arg) {
            if (_getOperandKind(opcode) != _OperandKind.TOKEN)
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a handle argument.", nameof(opcode));

            _ensureCodeBufferSpace(6);
            _writeOpcode(opcode);
            _writeToken(arg);

            if (opcode == ILOp.call || opcode == ILOp.callvirt || opcode == ILOp.newobj) {
                int delta = (m_tokenProvider != null) ? m_tokenProvider.getMethodStackDelta(arg, opcode) : 1;
                setCurrentStackSize(m_currentStackSize + delta);
            }
            else {
                _updateStack(opcode);
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a token operand and specifies the change in
        /// the stack height.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The <see cref="EntityHandle"/> representing the metadata token to
        /// be emitted as the operand.</param>
        /// <param name="stackDelta">The change in the stack height after executing the instruction.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <remarks>
        /// This method can be used to emit a call, callvirt or newobj instruction (where the
        /// stack change depends on the method operand) when there is no token provider that
        /// provides stack change information.
        /// </remarks>
        public void emit(ILOp opcode, EntityHandle arg, int stackDelta) {
            if (opcode == ILOp.call || opcode == ILOp.callvirt || opcode == ILOp.newobj) {
                _ensureCodeBufferSpace(6);
                _writeOpcode(opcode);
                _writeToken(arg);
                setCurrentStackSize(Math.Max(m_currentStackSize + stackDelta, 0));
            }
            else {
                throw new ArgumentException(
                    "The emit overload with a stack delta can only be used to emit a call, callvirt or newobj instruction.", nameof(opcode));
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a field token operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The <see cref="FieldInfo"/> representing the field whose token is to
        /// be emitted as the operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <exception cref="NotSupportedException">No token provider has been set.</exception>
        public void emit(ILOp opcode, FieldInfo arg) {
            if (arg == null)
                throw new ArgumentNullException(nameof(arg));

            _throwIfTokenProviderNotSet();
            emit(opcode, m_tokenProvider!.getHandle(arg));
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a method token operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The <see cref="MethodBase"/> representing the method or constructor whose token is to
        /// be emitted as the operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <exception cref="NotSupportedException">No token provider has been set.</exception>
        public void emit(ILOp opcode, MethodBase arg) {
            if (arg == null)
                throw new ArgumentNullException(nameof(arg));

            _throwIfTokenProviderNotSet();
            emit(opcode, m_tokenProvider!.getHandle(arg));
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a method token operand and specifies the change in
        /// the stack height.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The <see cref="MethodBase"/> representing the method or constructor whose token is to
        /// be emitted as the operand.</param>
        /// <param name="stackDelta">The change in the stack height after executing the instruction.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <exception cref="NotSupportedException">No token provider has been set.</exception>
        /// <remarks>
        /// This method can be used to emit a call, callvirt, newobj or ldftn instruction (where the
        /// stack change depends on the method operand) when there is no token provider that
        /// provides stack change information.
        /// </remarks>
        public void emit(ILOp opcode, MethodBase arg, int stackDelta) {
            if (arg == null)
                throw new ArgumentNullException(nameof(arg));

            _throwIfTokenProviderNotSet();
            emit(opcode, m_tokenProvider!.getHandle(arg), stackDelta);
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a type token operand.
        /// </summary>
        /// <param name="opcode">The opcode of the instruction.</param>
        /// <param name="arg">The Type object representing the field whose token is to be emitted as
        /// the operand.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method.</exception>
        /// <exception cref="NotSupportedException">No token provider has been set.</exception>
        public void emit(ILOp opcode, Type arg) {
            if (arg == null)
                throw new ArgumentNullException(nameof(arg));

            if (opcode == ILOp.ldelem || opcode == ILOp.stelem || opcode == ILOp.ldobj || opcode == ILOp.stobj) {
                // These instructions have shorthand forms for certain types
                // (integers, boolean, char, floating-point and object references)
                ILOp shortOpcode = _getShortOpcodeForType(opcode, arg);
                if (shortOpcode != opcode) {
                    _ensureCodeBufferSpace(1);
                    _writeOpcode(shortOpcode);
                    _updateStack(opcode);
                    return;
                }
            }

            _throwIfTokenProviderNotSet();
            emit(opcode, m_tokenProvider!.getHandle(arg));
        }

        /// <summary>
        /// Gets the short form of the given opcode taking a type operand.
        /// </summary>
        /// <param name="opcode">The opcode whose short form is to be obtained.</param>
        /// <param name="type">The type operand.</param>
        /// <returns>The short form of the opcode for the given type operand if available. Otherwise
        /// returns the opcode itself.</returns>
        private static ILOp _getShortOpcodeForType(ILOp opcode, Type type) {
        	if (!type.IsValueType) {
                return opcode switch {
                    ILOp.ldelem => ILOp.ldelem_ref,
                    ILOp.stelem => ILOp.stelem_ref,
                    ILOp.ldobj => ILOp.ldind_ref,
                    ILOp.stobj => ILOp.stind_ref
                };
            }

            if (type == (object)typeof(IntPtr) || type == (object)typeof(UIntPtr)) {
                return opcode switch {
                    ILOp.ldelem => ILOp.ldelem_i,
                    ILOp.stelem => ILOp.stelem_i,
                    ILOp.ldobj => ILOp.ldind_i,
                    ILOp.stobj => ILOp.stind_i
                };
            }

            return (Type.GetTypeCode(type), opcode) switch {
                (TypeCode.Boolean or TypeCode.SByte, ILOp.ldelem) => ILOp.ldelem_i1,
                (TypeCode.Byte, ILOp.ldelem) => ILOp.ldelem_u1,
                (TypeCode.Boolean or TypeCode.SByte or TypeCode.Byte, ILOp.stelem) => ILOp.stelem_i1,
                (TypeCode.Boolean or TypeCode.SByte, ILOp.ldobj) => ILOp.ldind_i1,
                (TypeCode.Byte, ILOp.ldobj) => ILOp.ldind_u1,
                (TypeCode.Boolean or TypeCode.SByte or TypeCode.Byte, ILOp.stobj) => ILOp.stind_i1,

                (TypeCode.Int16, ILOp.ldelem) => ILOp.ldelem_i2,
                (TypeCode.UInt16 or TypeCode.Char, ILOp.ldelem) => ILOp.ldelem_u2,
                (TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char, ILOp.stelem) => ILOp.stelem_i2,
                (TypeCode.Int16, ILOp.ldobj) => ILOp.ldind_i2,
                (TypeCode.UInt16 or TypeCode.Char, ILOp.ldobj) => ILOp.ldind_u2,
                (TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char, ILOp.stobj) => ILOp.stind_i2,

                (TypeCode.Int32, ILOp.ldelem) => ILOp.ldelem_i4,
                (TypeCode.UInt32, ILOp.ldelem) => ILOp.ldelem_u4,
                (TypeCode.Int32 or TypeCode.UInt32, ILOp.stelem) => ILOp.stelem_i4,
                (TypeCode.Int32, ILOp.ldobj) => ILOp.ldind_i4,
                (TypeCode.UInt32, ILOp.ldobj) => ILOp.ldind_u4,
                (TypeCode.Int32 or TypeCode.UInt32, ILOp.stobj) => ILOp.stind_i4,

                (TypeCode.Int64 or TypeCode.UInt64, ILOp.ldelem) => ILOp.ldelem_i8,
                (TypeCode.Int64 or TypeCode.UInt64, ILOp.stelem) => ILOp.stelem_i8,
                (TypeCode.Int64 or TypeCode.UInt64, ILOp.ldobj) => ILOp.ldind_i8,
                (TypeCode.Int64 or TypeCode.UInt64, ILOp.stobj) => ILOp.stind_i8,

                (TypeCode.Single, ILOp.ldelem) => ILOp.ldelem_r4,
                (TypeCode.Single, ILOp.stelem) => ILOp.stelem_r4,
                (TypeCode.Single, ILOp.ldobj) => ILOp.ldind_r4,
                (TypeCode.Single, ILOp.stobj) => ILOp.stind_r4,

                (TypeCode.Double, ILOp.ldelem) => ILOp.ldelem_r8,
                (TypeCode.Double, ILOp.stelem) => ILOp.stelem_r8,
                (TypeCode.Double, ILOp.ldobj) => ILOp.ldind_r8,
                (TypeCode.Double, ILOp.stobj) => ILOp.stind_r8,

                _ => opcode
            };
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a group of branch targets as the operand. In
        /// particular, this overload of the emit method is used to emit the switch instruction.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        /// <param name="arg">An array of labels to be used as the branch targets.</param>
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method OR the label group is invalid.</exception>
        public void emit(ILOp opcode, LabelGroup arg) {
            if (opcode != ILOp.@switch)
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a label group argument.", nameof(opcode));

            if (arg.m_length <= 0)
                throw new ArgumentException("Invalid label group: size <= 0.", nameof(arg));

            if ((uint)(arg.m_startId + arg.m_length) < (uint)arg.m_startId
                || (uint)(arg.m_startId + arg.m_length) > (uint)m_labelInfo.length)
            {
                throw new ArgumentException("Invalid label group.", nameof(arg));
            }

            _ensureCodeBufferSpace(arg.m_length * 4 + 5);

            _writeOpcode(opcode);
            _updateStack(opcode);

            WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), arg.m_length);
            m_position += 4;

            int basePosition = m_position + arg.m_length * 4;

            for (int i = 0, n = arg.m_length; i < n; i++) {
                int labelId = arg.m_startId + i;
                ref _LabelInfo labelInfo = ref m_labelInfo[labelId];

                if (labelInfo.stackSize == -1)
                    labelInfo.stackSize = m_currentStackSize;

                var branchInfo = new _BranchInfo {
                    offsetPosition = m_position,
                    basePosition = basePosition,
                    target = new Label(labelId),
                    opcode = opcode,
                };

                m_branches.add(in branchInfo);
                m_position += 4;
            }
        }

        /// <summary>
        /// Emits an instruction into the IL stream with a sequence of branch targets as the operand. In
        /// particular, this overload of the emit method is used to emit the switch instruction.
        /// </summary>
        /// <param name="opcode">The opcode.</param>
        /// <param name="arg">A span of labels to be used as the branch targets.</param>
        ///
        /// <exception cref="ArgumentException">The given opcode is invalid or cannot be used with
        /// this overload of the emit method OR the label array is null or empty OR one of the labels
        /// in the array is invalid.</exception>
        public void emit(ILOp opcode, ReadOnlySpan<Label> arg) {
            if (arg == null || arg.Length == 0)
                throw new ArgumentException("Opcode '" + opcode + "' cannot be used with a label group argument.", nameof(opcode));

            if (opcode != ILOp.@switch)
                throw new ArgumentException("Opcode cannot be used with the given emit method overload.", nameof(opcode));

            for (int i = 0; i < arg.Length; i++) {
                if ((uint)arg[i].id >= (uint)m_labelInfo.length)
                    throw new ArgumentException("One of the target labels is not valid.", nameof(arg));
            }

            _ensureCodeBufferSpace(arg.Length * 4 + 5);

            _writeOpcode(opcode);
            _updateStack(opcode);

            WriteInt32LittleEndian(m_codeBuffer.AsSpan(m_position, 4), arg.Length);
            m_position += 4;

            int basePosition = m_position + arg.Length * 4;

            for (int i = 0; i < arg.Length; i++) {
                Label label = arg[i];
                ref _LabelInfo labelInfo = ref m_labelInfo[label.id];

                if (labelInfo.stackSize == -1)
                    labelInfo.stackSize = m_currentStackSize;

                var branchInfo = new _BranchInfo {
                    offsetPosition = m_position,
                    basePosition = basePosition,
                    target = label,
                    opcode = opcode,
                };

                m_branches.add(in branchInfo);
                m_position += 4;
            }
        }

        /// <summary>
        /// Generates an <see cref="ILMethodBody"/> instance from the code emitted into the <see cref="ILBuilder"/>.
        /// </summary>
        /// <returns>An instance of <see cref="ILMethodBody"/> containing the emitted code.</returns>
        /// <exception cref="NotSupportedException">
        /// <list type="bullet">
        /// <item><description>The size of the evaluation stack exceeds 65535 at any point in the execution of the method body.</description></item>
        /// <item><description>A local variable was created using the <see cref="declareLocal(in TypeSignature, Boolean)"/>
        /// or <see cref="acquireTempLocal(in TypeSignature)"/> overloads, and the current token
        /// provider has <see cref="ILTokenProvider.useLocalSigHelper"/> as true.</description></item>
        /// </list>
        /// </exception>
        /// <exception cref="InvalidOperationException">There are open exception handlers, or labels that are used
        /// as branch targets but have not been marked using <see cref="markLabel"/>.</exception>
        /// <remarks>
        /// Calling this method will call <see cref="reset"/>, so that the <see cref="ILBuilder"/> can
        /// be reused to emit another method body after calling this method.
        /// </remarks>
        public ILMethodBody createMethodBody() {
            if (m_maxStackSize > 65535)
                throw new NotSupportedException("Maximum stack height limit exceeded.");

            if (m_excHandlerStack.length > 0)
                throw new InvalidOperationException("Open exception handling regions must be closed before a method body can be created.");

            ushort maxStack = (ushort)m_maxStackSize;
            bool initLocals = m_initLocals && (m_localSig.length > 0 || m_hasLocAlloc);

            byte[]? localSignature = null;
            int localSigToken = 0;

            if (m_localSig.length > 0) {
                if (m_tokenProvider != null && m_tokenProvider.useLocalSigHelper)
                    localSignature = _makeLocalSignatureWithHelper();
                else
                    localSignature = LocalTypeSignature.makeLocalSignature(m_localSig.asSpan());

                if (m_tokenProvider != null)
                    localSigToken = MetadataTokens.GetToken(m_tokenProvider.getLocalSignatureHandle(localSignature));
            }

            _validateAndShortenBranches();

            byte[] methodBody = _generateMethodBody();
            byte[]? ehSection = _generateExceptionHandlingSection();
            int[] virtualTokenLocations = _generateVirtualTokenLocations();

            reset();

            return new ILMethodBody(maxStack, initLocals, localSignature, localSigToken, methodBody, ehSection, virtualTokenLocations);
        }

        private byte[] _makeLocalSignatureWithHelper() {
            var sigHelper = SignatureHelper.GetLocalVarSigHelper();

            for (int i = 0; i < m_localInfo.length; i++) {
                Type? type = m_localInfo[i].type;
                if (type == null)
                    throw new NotSupportedException("A local variable only has a type signature available, but the current token provider requires a Type.");

                sigHelper.AddArgument(type, m_localSig[i].isPinned);
            }

            return sigHelper.GetSignature();
        }

        private void _validateAndShortenBranches() {
            var branches = m_branches.asSpan();
            int shift = 0;

            for (int i = 0; i < branches.Length; i++) {
                ref var branch = ref branches[i];

                int targetPosition = m_labelInfo[branch.target.id].targetPosition;
                if (targetPosition == -1)
                    throw new InvalidOperationException("All labels used as branch targets must be marked before a method body can be created.");

                if (branch.opcode == ILOp.@switch)
                    continue;

                int difference = targetPosition - branch.basePosition;
                branch.useShortForm = difference >= -128 && difference < 127;

                if (branch.useShortForm) {
                    shift -= 3;
                    branch.basePosition = branch.offsetPosition + 1;
                    m_relocations.add(new _RelocationInfo {startOffset = branch.offsetPosition + 4, shift = shift});
                }
            }

            if (m_relocations.length > 0) {
                _RelocationInfo lastReloc = m_relocations[m_relocations.length - 1];
                if (lastReloc.startOffset < m_position)
                    m_relocations.add(new _RelocationInfo {startOffset = m_position, shift = lastReloc.shift});
            }
        }

        private int _computeRelocatedPosition(int originalPosition) {
            if (m_relocations.length == 0)
                return originalPosition;

            var relocs = m_relocations.asSpan();
            int low = 0, high = relocs.Length;

            while (true) {
                if (low == high) {
                    return (originalPosition >= relocs[low].startOffset)
                        ? originalPosition + relocs[low].shift
                        : originalPosition;
                }

                int mid = low + ((high - low) >> 1);

                if (originalPosition < relocs[mid].startOffset)
                    high = mid;
                else if (mid + 1 < relocs.Length && originalPosition >= relocs[mid + 1].startOffset)
                    low = mid + 1;
                else
                    return originalPosition + relocs[mid].shift;
            }
        }

        private byte[] _generateMethodBody() {
            int codeSize = m_position;
            var relocations = m_relocations.asSpan();

            if (relocations.Length > 0)
                codeSize += relocations[^1].shift;

            byte[] code = new byte[codeSize];

            if (relocations.Length == 0) {
                m_codeBuffer.AsSpan(0, codeSize).CopyTo(code);
            }
            else {
                _RelocationInfo lastReloc = default;

                for (int i = 0; i < relocations.Length; i++) {
                    int copyLength =
                        relocations[i].startOffset - lastReloc.startOffset + relocations[i].shift - lastReloc.shift;

                    m_codeBuffer.AsSpan(lastReloc.startOffset, copyLength).CopyTo(code.AsSpan(lastReloc.startOffset + lastReloc.shift));
                    lastReloc = relocations[i];
                }
            }

            var branches = m_branches.asSpan();

            for (int i = 0; i < branches.Length; i++) {
                ref var branch = ref branches[i];
                int offsetPosition = _computeRelocatedPosition(branch.offsetPosition);
                int basePosition = _computeRelocatedPosition(branch.basePosition);
                int targetPosition = _computeRelocatedPosition(m_labelInfo[branch.target.id].targetPosition);

                if (branch.useShortForm) {
                    ILOp opcode = (branch.opcode == ILOp.leave) ? ILOp.leave_s : (ILOp)((byte)branch.opcode - 13);
                    code[offsetPosition - 1] = (byte)opcode;
                    code[offsetPosition] = (byte)(targetPosition - basePosition);
                }
                else {
                    WriteInt32LittleEndian(code.AsSpan(offsetPosition, 4), targetPosition - basePosition);
                }
            }

            return code;
        }

        private byte[]? _generateExceptionHandlingSection() {
            if (m_excHandlers.length == 0)
                return null;

            // The nested exception handlers must be declared before their parent handlers,
            // so sort the handler array in ascending order of the offset of the end of the try
            // block.
            DataStructureUtil.sortSpan(m_excHandlers.asSpan(), s_excHandlerSorter);

            var handlers = m_excHandlers.asSpan();

            if (m_relocations.length > 0) {
                // Compute the relocated exception handler regions.
                // We can modify them in place because everything will be cleared once the method body
                // is generated.

                for (int i = 0; i < handlers.Length; i++) {
                    var h = handlers[i];
                    int tryStart = _computeRelocatedPosition(h.tryStart);
                    int tryEnd = _computeRelocatedPosition(h.tryStart + h.tryLength);
                    int handlerStart = _computeRelocatedPosition(h.handlerStart);
                    int handlerEnd = _computeRelocatedPosition(h.handlerStart + h.handlerLength);
                    int filterStart = (h.clauseType == ExceptionRegionKind.Filter) ? _computeRelocatedPosition(h.filterStart) : -1;

                    h.tryStart = tryStart;
                    h.tryLength = tryEnd - tryStart;
                    h.handlerStart = handlerStart;
                    h.handlerLength = handlerEnd - handlerStart;
                    h.filterStart = filterStart;
                }
            }

            byte[] ehSection;

            if (_canUseSmallExceptionHandlers()) {
                ehSection = new byte[handlers.Length * 12 + 4];
                ehSection[0] = 0x1;
                ehSection[1] = (byte)ehSection.Length;

                Span<byte> span = ehSection.AsSpan(4);
                for (int i = 0; i < handlers.Length; i++, span = span.Slice(12)) {
                    var h = handlers[i];
                    WriteUInt16LittleEndian(span, (ushort)h.clauseType);
                    WriteUInt16LittleEndian(span.Slice(2), (ushort)h.tryStart);
                    span[4] = (byte)h.tryLength;
                    WriteUInt16LittleEndian(span.Slice(5), (ushort)h.handlerStart);
                    span[7] = (byte)h.handlerLength;
                    WriteInt32LittleEndian(
                        span.Slice(8),
                        (h.clauseType == ExceptionRegionKind.Filter) ? h.filterStart : MetadataTokens.GetToken(h.catchType)
                    );
                }
            }
            else {
                ehSection = new byte[handlers.Length * 24 + 4];
                ehSection[0] = 0x41;
                WriteInt32LittleEndian(ehSection.AsSpan(1), ehSection.Length);

                Span<byte> span = ehSection.AsSpan(4);
                for (int i = 0; i < handlers.Length; i++, span = span.Slice(24)) {
                    var h = handlers[i];
                    WriteInt32LittleEndian(span, (int)h.clauseType);
                    WriteInt32LittleEndian(span.Slice(4), h.tryStart);
                    WriteInt32LittleEndian(span.Slice(8), h.tryLength);
                    WriteInt32LittleEndian(span.Slice(12), h.handlerStart);
                    WriteInt32LittleEndian(span.Slice(16), h.handlerLength);
                    WriteInt32LittleEndian(
                        span.Slice(20),
                        (h.clauseType == ExceptionRegionKind.Filter) ? h.filterStart : MetadataTokens.GetToken(h.catchType)
                    );
                }
            }

            return ehSection;
        }

        private bool _canUseSmallExceptionHandlers() {
            var handlers = m_excHandlers.asSpan();
            if (handlers.Length * 12 + 4 > 255)
                return false;

            for (int i = 0; i < handlers.Length; i++) {
                var h = handlers[i];
                if (h.tryStart > 65535 || h.handlerStart > 65535 || h.tryLength > 255 || h.handlerLength > 255)
                    return false;
            }

            return true;
        }

        private int[] _generateVirtualTokenLocations() {
            Span<int> vTokenLocations = m_virtualTokenLocations.asSpan();
            int[] relocatedLocations = new int[vTokenLocations.Length];

            for (int i = 0; i < vTokenLocations.Length; i++)
                relocatedLocations[i] = _computeRelocatedPosition(vTokenLocations[i]);

            return relocatedLocations;
        }

    }

}
