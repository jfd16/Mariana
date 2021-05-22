using System;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// The compiler pass that decodes AVM2 bytecode to <see cref="Instruction"/> objects.
    /// </summary>
    internal sealed class InstructionDecoder {

        private MethodCompilation m_compilation;

        /// <summary>
        /// Creates a new instance of <see cref="InstructionDecoder"/>.
        /// </summary>
        /// <param name="compilation">The <see cref="MethodCompilation"/> representing the
        /// compiler instance that uses this <see cref="InstructionDecoder"/> instance.</param>
        public InstructionDecoder(MethodCompilation compilation) {
            m_compilation = compilation;
        }

        /// <summary>
        /// Runs the instruction decoder.
        /// </summary>
        public void run() {
            ReadOnlySpan<byte> code = m_compilation.methodBodyInfo.getCode().asSpan();
            int currentOffset = 0;

            if (code.Length == 0)
                // Fail on zero code length here. Subsequent passes assume that the method
                // body contains at least one instruction.
                throw m_compilation.createError(ErrorCode.INVALID_CODE_LENGTH, -1, code.Length);

            while (code.Length != 0) {
                ref Instruction instr = ref m_compilation.addInstruction();
                instr.byteOffset = currentOffset;

                int bytesRead = _readInstruction(code, ref instr);
                code = code.Slice(bytesRead);
                currentOffset += bytesRead;

                _instructionToNormalForm(ref instr);
            }
        }

        /// <summary>
        /// Reads an instruction.
        /// </summary>
        /// <param name="span">The span containing the instruction to be read.</param>
        /// <param name="instr">An <see cref="Instruction"/> instance to which the instruction
        /// data will be written.</param>
        /// <returns>The number of bytes consumed from <paramref name="span"/>.</returns>
        private int _readInstruction(ReadOnlySpan<byte> span, ref Instruction instr) {
            ABCOp opcode = (ABCOp)span[0];
            instr.opcode = opcode;

            int bytesRead = 1;

            ABCOpInfo opInfo = ABCOpInfo.getInfo(opcode);

            // We don't throw on illegal opcodes at this stage because they might be used in
            // unreachable code sections by obfuscators. We just treat them as zero-operand
            // instructions, and fail only if the invalid opcodes are found to be reachable
            // in a later pass.
            if (!opInfo.isValid)
                return bytesRead;

            if (opcode == ABCOp.pushshort) {
                bytesRead += _readU30(span, 1, false, out instr.data.pushShort.value);
                return bytesRead;
            }

            switch (opInfo.immediateType) {
                case ABCOpInfo.ImmediateType.NONE:
                    break;

                case ABCOpInfo.ImmediateType.BYTE:
                    instr.data.raw.op1 = _readSignedByte(span, 1);
                    bytesRead += 1;
                    break;

                case ABCOpInfo.ImmediateType.U30:
                    bytesRead += _readU30(span, 1, true, out instr.data.raw.op1);
                    break;

                case ABCOpInfo.ImmediateType.U30_U30:
                    bytesRead += _readU30(span, 1, true, out instr.data.raw.op1);
                    bytesRead += _readU30(span, bytesRead, true, out instr.data.raw.op2);
                    break;

                case ABCOpInfo.ImmediateType.S24:
                    instr.data.raw.op1 = _readS24(span, 1);
                    bytesRead += 3;
                    break;

                case ABCOpInfo.ImmediateType.DEBUG:
                    bytesRead += _readDebugInstrData(span, ref instr);
                    break;

                case ABCOpInfo.ImmediateType.SWITCH:
                    bytesRead += _readSwitchInstrData(span, ref instr);
                    break;
            }

            return bytesRead;
        }

        /// <summary>
        /// Reads a signed byte from the given span.
        /// </summary>
        /// <param name="span">The span from which to read.</param>
        /// <param name="index">The index of the byte in the span.</param>
        /// <returns>The byte value, sign extended to 32 bits.</returns>
        private int _readSignedByte(ReadOnlySpan<byte> span, int index) {
            if ((uint)index >= (uint)span.Length)
                throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);
            return (int)(sbyte)span[index];
        }

        /// <summary>
        /// Reads a u30 (variable-length unsigned integer) from the given span.
        /// </summary>
        /// <param name="span">The span from which to read.</param>
        /// <param name="index">The index of the first byte of the integer in the span.</param>
        /// <param name="validate">Set to throw an error if the value of the integer does not
        /// fit in 30 bits.</param>
        /// <param name="value">An output parameter to which the value of the integer will
        /// be written.</param>
        /// <returns>The number of bytes consumed.</returns>
        private int _readU30(ReadOnlySpan<byte> span, int index, bool validate, out int value) {
            uint val = 0;
            int shift = 0;
            int startIndex = index;

            for (int i = 0; i < 5; i++) {
                if ((uint)index >= (uint)span.Length)
                    throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);

                int b = span[index];
                index++;
                val |= (uint)((b & 127) << shift);
                if ((b & 128) == 0)
                    break;
                shift += 7;
            }

            if (validate && (val & 0xC0000000u) != 0u)
                throw m_compilation.createError(ErrorCode.MARIANA__ABC_ILLEGAL_U30_VALUE, -1);

            value = (int)val;
            return index - startIndex;
        }

        /// <summary>
        /// Reads a 24-bit signed integer from the given span.
        /// </summary>
        /// <param name="span">The span from which to read.</param>
        /// <param name="index">The index in <paramref name="span"/> of the first byte of the integer.</param>
        /// <returns>The value of the integer read, sign-extended to 32 bits.</returns>
        private int _readS24(ReadOnlySpan<byte> span, int index) {
            if (span.Length - index < 3)
                throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);

            return (span[index] << 8 | span[index + 1] << 16 | span[index + 2] << 24) >> 8;
        }

        /// <summary>
        /// Reads the immediate data of a 'debug' instruction.
        /// </summary>
        /// <param name="span">The span containing the instruction bytecode.</param>
        /// <param name="instr">A reference to the <see cref="Instruction"/> instance
        /// representing the instruction.</param>
        /// <returns>The number of bytes consumed.</returns>
        private int _readDebugInstrData(ReadOnlySpan<byte> span, ref Instruction instr) {
            int startIndex = 1;
            int index = startIndex;

            _readSignedByte(span, index);
            index++;

            index += _readU30(span, index, false, out _);

            _readSignedByte(span, index);
            index++;

            index += _readU30(span, index, false, out _);

            return index - startIndex;
        }

        /// <summary>
        /// Reads the immediate data of a 'switch' instruction.
        /// </summary>
        /// <param name="span">The span containing the instruction bytecode.</param>
        /// <param name="instr">A reference to the <see cref="Instruction"/> instance
        /// representing the instruction.</param>
        /// <returns>The number of bytes consumed.</returns>
        private int _readSwitchInstrData(ReadOnlySpan<byte> span, ref Instruction instr) {
            int startIndex = 1;
            int index = startIndex;

            int defaultCase = _readS24(span, index);
            index += 3;

            index += _readU30(span, index, true, out int caseCount);
            caseCount++;

            instr.data.@switch.caseCount = caseCount;
            instr.data.@switch.caseOffsets = m_compilation.staticIntArrayPool.allocate(caseCount + 1, out Span<int> caseOffsets);

            caseOffsets[0] = defaultCase;

            for (int i = 0; i < caseCount; i++) {
                if ((uint)index > (uint)span.Length)
                    throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);
                caseOffsets[i + 1] = _readS24(span, index);
                index += 3;
            }

            return index - startIndex;
        }

        /// <summary>
        /// Converts an instruction to its normal form.
        /// </summary>
        /// <param name="instr">The instruction to convert to normal form.</param>
        private static void _instructionToNormalForm(ref Instruction instr) {
            ABCOp opcode = instr.opcode;

            if (opcode >= ABCOp.getlocal0 && opcode <= ABCOp.getlocal3) {
                instr.opcode = ABCOp.getlocal;
                instr.data.getSetLocal.localId = (int)(opcode - ABCOp.getlocal0);
                return;
            }

            if (opcode >= ABCOp.setlocal0 && opcode <= ABCOp.setlocal3) {
                instr.opcode = ABCOp.setlocal;
                instr.data.getSetLocal.localId = (int)(opcode - ABCOp.setlocal0);
                return;
            }

            switch (opcode) {
                case ABCOp.finddef:
                    instr.opcode = ABCOp.findproperty;
                    break;
                case ABCOp.pushbyte:
                    instr.opcode = ABCOp.pushshort;
                    break;
                case ABCOp.pushnan:
                    instr.opcode = ABCOp.pushdouble;
                    instr.data.pushConst.poolId = 0;    // Constant pool index 0 is always NaN.
                    break;
                case ABCOp.coerce_b:
                    instr.opcode = ABCOp.convert_b;
                    break;
                case ABCOp.coerce_d:
                    instr.opcode = ABCOp.convert_d;
                    break;
                case ABCOp.coerce_i:
                    instr.opcode = ABCOp.convert_i;
                    break;
                case ABCOp.coerce_o:
                    instr.opcode = ABCOp.convert_o;
                    break;
                case ABCOp.coerce_u:
                    instr.opcode = ABCOp.convert_u;
                    break;
                // coerce_s cannot be changed to convert_s because the two opcodes have
                // different semantics for null and undefined.
            }
        }

    }

}
