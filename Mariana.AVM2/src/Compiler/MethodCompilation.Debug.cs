#if DEBUG

using System;
using System.Globalization;
using System.Text;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    internal partial class MethodCompilation {

        /// <summary>
        /// Returns a string representation of the instruction with the given id.
        /// </summary>
        /// <returns>A string representation of the instruction in this compilation whose id is
        /// <paramref name="instrId"/>.</returns>
        /// <param name="instrId">The id of the instruction.</param>
        public string instructionToString(int instrId) => instructionToString(getInstruction(instrId));

        /// <summary>
        /// Returns a string representation of an instruction.
        /// </summary>
        /// <returns>A string representation of the <paramref name="instr"/>.</returns>
        /// <param name="instr">A reference to the <see cref="Instruction"/> instance whose
        /// string representation must be returned.</param>
        public string instructionToString(in Instruction instr) {
            var sb = new StringBuilder();
            CultureInfo ic = CultureInfo.InvariantCulture;

            sb.AppendFormat(ic, "[{0,3}] ", instr.id);

            ABCOpInfo opInfo = ABCOpInfo.getInfo(instr.opcode);

            if (!opInfo.isValid) {
                sb.AppendFormat(ic, "<invalid> 0x{0:X2}", (int)instr.opcode);
                return sb.ToString();
            }

            sb.AppendFormat("{0,-16} ", ABCOpInfo.getName(instr.opcode));

            switch (instr.opcode) {
                case ABCOp.astype:
                case ABCOp.coerce:
                case ABCOp.istype:
                    sb.Append(abcFile.multinameToString(abcFile.resolveMultiname(instr.data.coerceOrIsType.multinameId)));
                    break;

                case ABCOp.deleteproperty:
                case ABCOp.getdescendants:
                case ABCOp.getproperty:
                case ABCOp.getsuper:
                case ABCOp.initproperty:
                case ABCOp.setsuper:
                case ABCOp.setproperty:
                    sb.Append(abcFile.multinameToString(abcFile.resolveMultiname(instr.data.accessProperty.multinameId)));
                    if (instr.opcode != ABCOp.getdescendants)
                        sb.Append(", resolved = ").Append(resolvedPropertyToString(instr.data.accessProperty.resolvedPropId));
                    break;

                case ABCOp.finddef:
                case ABCOp.findproperty:
                case ABCOp.findpropstrict:
                case ABCOp.getlex:
                    sb.Append(abcFile.multinameToString(abcFile.resolveMultiname(instr.data.findProperty.multinameId)));
                    if (!instr.data.findProperty.scopeRef.isNull) {
                        sb.AppendFormat(
                            ic,
                            ", resolved = {0} {1}",
                            instr.data.findProperty.scopeRef,
                            resolvedPropertyToString(instr.data.findProperty.resolvedPropId)
                        );
                    }
                    break;

                case ABCOp.callproplex:
                case ABCOp.callproperty:
                case ABCOp.callpropvoid:
                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                case ABCOp.constructprop:
                    sb.Append(abcFile.multinameToString(abcFile.resolveMultiname(instr.data.callProperty.multinameId)));
                    sb.AppendFormat(ic, ", argCount = {0}", instr.data.callProperty.argCount);
                    sb.Append(", resolved = ").Append(resolvedPropertyToString(instr.data.callProperty.resolvedPropId));
                    break;

                case ABCOp.applytype:
                    sb.AppendFormat(ic, "argCount = {0}", instr.data.applyType.argCount);
                    break;

                case ABCOp.newarray:
                case ABCOp.newobject:
                    sb.Append(instr.data.newArrOrObj.elementCount.ToString(ic));
                    break;

                case ABCOp.call:
                case ABCOp.construct:
                    sb.AppendFormat(
                        ic,
                        "argCount = {0}, resolved = {1}",
                        instr.data.callOrConstruct.argCount,
                        resolvedPropertyToString(instr.data.callOrConstruct.resolvedPropId)
                    );
                    break;

                case ABCOp.constructsuper:
                    sb.AppendFormat(ic, "argCount = {0}", instr.data.constructSuper.argCount);
                    break;

                case ABCOp.getscopeobject:
                    sb.Append(instr.data.getScopeObject.index.ToString(ic));
                    break;

                case ABCOp.newcatch:
                case ABCOp.newfunction:
                    sb.Append(instr.data.raw.op1.ToString(ic));
                    break;

                case ABCOp.getlocal:
                    sb.AppendFormat(ic, "{0}  {1}", instr.data.getSetLocal.localId, dataNodeToString(instr.data.getSetLocal.nodeId));
                    break;

                case ABCOp.setlocal:
                case ABCOp.kill:
                    sb.AppendFormat(ic, "{0}  {1}", instr.data.getSetLocal.localId, dataNodeToString(instr.data.getSetLocal.newNodeId));
                    break;

                case ABCOp.inclocal:
                case ABCOp.inclocal_i:
                case ABCOp.declocal:
                case ABCOp.declocal_i:
                    sb.AppendFormat(
                        ic,
                        "{0}  {1} => {2}",
                        instr.data.getSetLocal.localId,
                        dataNodeToString(instr.data.getSetLocal.nodeId),
                        dataNodeToString(instr.data.getSetLocal.newNodeId)
                    );
                    break;

                case ABCOp.hasnext2:
                {
                    var nodeIds = staticIntArrayPool.getSpan(instr.data.hasnext2.nodeIds);
                    sb.AppendFormat(
                        ic,
                        "{0}  {2} => {4} ; {1}  {3} => {5}",
                        instr.data.hasnext2.localId1,
                        instr.data.hasnext2.localId2,
                        dataNodeToString(nodeIds[0]),
                        dataNodeToString(nodeIds[1]),
                        dataNodeToString(nodeIds[2]),
                        dataNodeToString(nodeIds[3])
                    );
                    break;
                }

                case ABCOp.getslot:
                case ABCOp.setslot:
                case ABCOp.getglobalslot:
                case ABCOp.setglobalslot:
                    sb.AppendFormat(
                        ic,
                        "{0}, resolved = {1}",
                        instr.data.getSetSlot.slotId,
                        resolvedPropertyToString(instr.data.getSetSlot.resolvedPropId)
                    );
                    break;

                case ABCOp.pushscope:
                    sb.Append(dataNodeToString(instr.data.pushScope.pushedNodeId));
                    break;

                case ABCOp.newclass:
                {
                    var classInfo = abcFile.getClassInfo();
                    var index = instr.data.newClass.classInfoId;
                    sb.Append((index < classInfo.length) ? classInfo[index].name.ToString() : index.ToString(ic));
                    break;
                }

                case ABCOp.callmethod:
                case ABCOp.callstatic:
                    sb.AppendFormat(ic, "{0}, argCount = {1}", instr.data.callMethod.methodOrDispId, instr.data.callMethod.argCount);
                    break;

                case ABCOp.pushint:
                    sb.Append(abcFile.resolveInt(instr.data.pushConst.poolId).ToString(ic));
                    break;

                case ABCOp.pushuint:
                    sb.Append(abcFile.resolveUint(instr.data.pushConst.poolId).ToString(ic));
                    break;

                case ABCOp.pushdouble:
                    sb.Append(abcFile.resolveDouble(instr.data.pushConst.poolId).ToString("R", ic));
                    break;

                case ABCOp.pushnamespace:
                    sb.Append(abcFile.resolveNamespace(instr.data.pushConst.poolId).ToString());
                    break;

                case ABCOp.pushbyte:
                case ABCOp.pushshort:
                    sb.Append(instr.data.pushShort.value.ToString(ic));
                    break;

                case ABCOp.pushstring:
                    _escapeStringForOutput(abcFile.resolveString(instr.data.pushConst.poolId), sb);
                    break;

                case ABCOp.dxns:
                    _escapeStringForOutput(abcFile.resolveString(instr.data.pushConst.poolId), sb);
                    break;

                case ABCOp.debugfile:
                    _escapeStringForOutput(abcFile.resolveString(instr.data.debugFile.fileNameId), sb);
                    break;

                case ABCOp.debugline:
                    sb.Append(instr.data.debugLine.lineNumber.ToString(ic));
                    break;

                case ABCOp.jump: {
                    var exitBlocks = staticIntArrayPool.getSpan(getBasicBlockOfInstruction(instr).exitBlockIds);
                    sb.AppendFormat(ic, "BB({0})", exitBlocks[0]);
                    break;
                }

                case ABCOp.iftrue:
                case ABCOp.iffalse:
                {
                    var exitBlocks = staticIntArrayPool.getSpan(getBasicBlockOfInstruction(instr).exitBlockIds);
                    sb.AppendFormat(ic, "BB({0})", exitBlocks[0]);
                    break;
                }

                case ABCOp.ifeq:
                case ABCOp.ifstricteq:
                case ABCOp.ifne:
                case ABCOp.ifstrictne:
                case ABCOp.iflt:
                case ABCOp.ifnlt:
                case ABCOp.ifle:
                case ABCOp.ifnle:
                case ABCOp.ifgt:
                case ABCOp.ifngt:
                case ABCOp.ifge:
                case ABCOp.ifnge:
                {
                    var exitBlocks = staticIntArrayPool.getSpan(getBasicBlockOfInstruction(instr).exitBlockIds);
                    var compareType = instr.data.compareBranch.compareType;
                    sb.AppendFormat(ic, "BB({0}), compareType = {1}", exitBlocks[0], compareType);
                    break;
                }

                case ABCOp.equals:
                case ABCOp.strictequals:
                case ABCOp.lessthan:
                case ABCOp.lessequals:
                case ABCOp.greaterthan:
                case ABCOp.greaterequals:
                    sb.AppendFormat("compareType = {0}", instr.data.compare.compareType);
                    break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the string representation of an exception handler.
        /// </summary>
        /// <returns>A string representation of <paramref name="excHandler"/>.</returns>
        /// <param name="excHandler">A reference to the <see cref="ExceptionHandler"/> instance whose
        /// string representation is to be returned.</param>
        /// <param name="indent">Set this argument to the level at which to indent the output, in
        /// units of four spaces.</param>
        public string exceptionHandlerToString(ref ExceptionHandler excHandler, int indent = 0) {
            var sb = new StringBuilder();
            int indentSpaces = (indent <= 0) ? 0 : indent * 4;

            sb.Append(' ', indentSpaces);
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "Exception handler #{0}: Parent = {1}, Flags = {2}, Range: {3}-{4}, Catch: {5}, ErrorType: {6}",
                excHandler.id,
                excHandler.parentId,
                excHandler.flags,
                excHandler.tryStartInstrId,
                excHandler.tryEndInstrId - 1,
                excHandler.catchTargetInstrId,
                (excHandler.errorType == null) ? "*" : excHandler.errorType.name.ToString()
            );

            return sb.ToString();
        }

        private static void _escapeStringForOutput(string str, StringBuilder outSb) {
            outSb.Append('"');
            for (int i = 0; i < str.Length; i++) {
                switch (str[i]) {
                    case '\\':
                        outSb.Append('\\').Append('\\');
                        break;
                    case '"':
                        outSb.Append('\\').Append('"');
                        break;
                    case '\r':
                        outSb.Append('\\').Append('r');
                        break;
                    case '\n':
                        outSb.Append('\\').Append('n');
                        break;
                    default:
                        outSb.Append(str[i]);
                        break;
                }
            }
            outSb.Append('"');
        }

        /// <summary>
        /// Returns the string representation of the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <param name="includeFlow">True if data flow information should be included in the string, otherwise
        /// false.</param>
        /// <returns>A string representation of the node in this compilation whose id is <paramref name="nodeId"/>.</returns>
        public string dataNodeToString(int nodeId, bool includeFlow = false) => dataNodeToString(getDataNode(nodeId), includeFlow);

        /// <summary>
        /// Returns the string representation of a data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> created in this compilation for which
        /// to return a string representation.</param>
        /// <param name="includeDefUseInfo">True if data flow information should be included in the string, otherwise
        /// false.</param>
        /// <returns>A string representation of <paramref name="node"/>.</returns>
        public string dataNodeToString(in DataNode node, bool includeDefUseInfo = false) {
            var sb = new StringBuilder();
            var ic = CultureInfo.InvariantCulture;

            sb.AppendFormat(ic, "[#{0} ", node.id);

            switch (node.dataType) {
                case DataNodeType.ANY:
                    sb.Append('*');
                    break;
                case DataNodeType.BOOL:
                    sb.Append('B');
                    if (node.isConstant)
                        sb.Append(' ').Append(node.constant.boolValue ? "true" : "false");
                    break;
                case DataNodeType.CLASS:
                    sb.Append("C ").Append(node.constant.classValue.name.ToString());
                    break;
                case DataNodeType.FUNCTION: {
                    var method = node.constant.methodValue;
                    sb.Append("F ");
                    if (method.declaringClass != null)
                        sb.Append(method.declaringClass.name.ToString()).Append('/');
                    sb.Append(method.name.ToString());
                    break;
                }
                case DataNodeType.GLOBAL:
                    sb.Append("global");
                    break;
                case DataNodeType.INT:
                    sb.Append('I');
                    if (node.isConstant)
                        sb.AppendFormat(ic, " {0}", node.constant.intValue);
                    break;
                case DataNodeType.NAMESPACE:
                    sb.Append('N');
                    if (node.isConstant)
                        sb.AppendFormat(ic, " {0}", node.constant.namespaceValue);
                    break;
                case DataNodeType.UNKNOWN:
                    sb.Append('?');
                    break;
                case DataNodeType.NULL:
                    sb.Append("null");
                    break;
                case DataNodeType.NUMBER:
                    sb.Append('D');
                    if (node.isConstant)
                        sb.AppendFormat(ic, " {0:G7}", node.constant.doubleValue);
                    break;
                case DataNodeType.OBJECT:
                    sb.Append("O ").Append(node.constant.classValue.name.ToString());
                    break;
                case DataNodeType.QNAME:
                    sb.Append('Q');
                    if (node.isConstant)
                        sb.AppendFormat(ic, " {0}", node.constant.qnameValue);
                    break;
                case DataNodeType.REST:
                    sb.Append("rest");
                    break;
                case DataNodeType.STRING: {
                    sb.Append('S');
                    if (node.isConstant) {
                        string val = node.constant.stringValue;
                        sb.Append(' ');
                        _escapeStringForOutput((val.Length <= 16) ? val : val.Substring(0, 16) + "...", sb);
                    }
                    break;
                }
                case DataNodeType.THIS:
                    sb.Append("this");
                    break;
                case DataNodeType.UINT:
                    sb.Append('U');
                    if (node.isConstant)
                        sb.AppendFormat(ic, " {0}", (uint)node.constant.intValue);
                    break;
                case DataNodeType.UNDEFINED:
                    sb.Append("undef");
                    break;
            }

            if ((node.flags & DataNodeFlags.ARGUMENT) != 0)
                sb.Append(" arg");
            if ((node.flags & DataNodeFlags.EXCEPTION) != 0)
                sb.Append(" exc");

            if ((node.flags & DataNodeFlags.NOT_NULL) != 0) {
                if ((node.flags & DataNodeFlags.CONSTANT) == 0 && !DataNodeTypeHelper.isNonNullable(node.dataType))
                    sb.Append(" nn");
            }

            if ((node.flags & DataNodeFlags.PHI) != 0)
                sb.Append(" phi");
            if ((node.flags & DataNodeFlags.WITH_SCOPE) != 0)
                sb.Append(" with");
            if ((node.flags & DataNodeFlags.NO_PUSH) != 0)
                sb.Append(" np");
            if ((node.flags & DataNodeFlags.PUSH_OPTIONAL_PARAM) != 0)
                sb.Append(" opt");
            if ((node.flags & DataNodeFlags.LATE_MULTINAME_BINDING) != 0)
                sb.Append(" lb");

            if (node.onPushCoerceType != DataNodeType.UNKNOWN) {
                sb.Append(" cv:");
                switch (node.onPushCoerceType) {
                    case DataNodeType.INT:
                        sb.Append('I');
                        break;
                    case DataNodeType.UINT:
                        sb.Append('U');
                        break;
                    case DataNodeType.NUMBER:
                        sb.Append('D');
                        break;
                    case DataNodeType.STRING:
                        sb.Append('S');
                        break;
                    case DataNodeType.BOOL:
                        sb.Append('B');
                        break;
                    case DataNodeType.OBJECT:
                        sb.Append('O');
                        break;
                    case DataNodeType.ANY:
                        sb.Append('*');
                        break;
                }
            }

            if (includeDefUseInfo) {
                var defs = getDataNodeDefs(node.id);
                var uses = getDataNodeUses(node.id);

                void appendNodeRef(int i, DataNodeOrInstrRef nodeRef) => sb.AppendFormat(
                    ic,
                    "{0}{1}{2}",
                    (i != 0) ? "," : "",
                    nodeRef.isInstruction ? "i" : "#",
                    nodeRef.instrOrNodeId
                );

                if (defs.Length > 0) {
                    sb.Append(' ');
                    for (int i = 0; i < defs.Length; i++)
                        appendNodeRef(i, defs[i]);
                }
                if (uses.Length > 0) {
                    sb.Append(" => ");
                    for (int i = 0; i < uses.Length; i++)
                        appendNodeRef(i, uses[i]);
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Returns a string representation of the <see cref="ResolvedProperty"/> in this
        /// compilation with the given id.
        /// </summary>
        /// <param name="resolvedPropId">The id of the <see cref="ResolvedProperty"/> instance.</param>
        /// <returns>A string representation of the <see cref="ResolvedProperty"/> in this
        /// compilation whose id is equal to <paramref name="resolvedPropId"/>.</returns>
        public string resolvedPropertyToString(int resolvedPropId) => resolvedPropertyToString(ref m_resolvedProperties[resolvedPropId]);

        /// <summary>
        /// Returns a string representation of the given <see cref="ResolvedProperty"/> instance.
        /// </summary>
        /// <param name="prop">A reference to a <see cref="ResolvedProperty"/> instance.</param>
        /// <returns>A string representation of the resolved property represented by
        /// <paramref name="prop"/>.</returns>
        public string resolvedPropertyToString(ref ResolvedProperty prop) {
            var sb = new StringBuilder();
            sb.Append(prop.propKind.ToString().ToLowerInvariant());

            switch (prop.propKind) {
                case ResolvedPropertyKind.TRAIT:
                case ResolvedPropertyKind.TRAIT_RT_INVOKE:
                {
                    var trait = (Trait)prop.propInfo;
                    sb.Append(' ');
                    sb.Append((trait.declaringClass != null) ? trait.declaringClass.name.ToString() : "global");
                    sb.Append('/');
                    sb.Append(trait.name.ToString());
                    break;
                }

                case ResolvedPropertyKind.INTRINSIC:
                    sb.Append(' ').Append(((Intrinsic)prop.propInfo).name);
                    break;

                case ResolvedPropertyKind.INDEX: {
                    var valueType = ((IndexProperty)prop.propInfo).valueType;
                    sb.Append(" [").Append((valueType != null) ? valueType.name.ToString() : "*").Append(']');
                    break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Creates and returns a string describing the basic block with the given id
        /// and all its instructions.
        /// </summary>
        /// <returns>The created string representation of the basic block in this compilation whose
        /// id is <paramref name="blockId"/>.</returns>
        /// <param name="blockId">The id of the basic block.</param>
        /// <param name="indent">Set this to the level at which to indent each line of the string
        /// output by this method, in units of four spaces.</param>
        public string basicBlockToString(int blockId, int indent = 0) => basicBlockToString(getBasicBlock(blockId), indent);

        /// <summary>
        /// Creates and returns a string describing the given basic block and all its instructions.
        /// </summary>
        /// <returns>The created string representation of the basic block.</returns>
        /// <param name="bb">A reference to the <see cref="BasicBlock"/> instance representing
        /// the basic block.</param>
        /// <param name="indent">Set this to the level at which to indent each line of the string
        /// output by this method, in units of four spaces.</param>
        public string basicBlockToString(in BasicBlock bb, int indent = 0) {
            var sb = new StringBuilder();
            var ic = CultureInfo.InvariantCulture;
            int indentSpaces = (indent <= 0) ? 0 : indent * 4;

            sb.Append(' ', indentSpaces);
            sb.AppendFormat(
                ic,
                "Basic block #{0} [Range: {1}-{2}]",
                bb.id,
                bb.firstInstrId,
                bb.firstInstrId + bb.instrCount - 1
            );
            sb.AppendLine();

            if (bb.flags != 0)
                sb.Append(' ', indentSpaces).Append("Flags: ").Append(bb.flags.ToString()).AppendLine();

            sb.Append(' ', indentSpaces).Append("Entry points: [");

            var entryPoints = cfgNodeRefArrayPool.getSpan(bb.entryPoints);
            for (int i = 0; i < entryPoints.Length; i++)
                sb.AppendFormat(ic, "{0}{1}", (i != 0) ? ", " : "", entryPoints[i]);

            sb.Append(']');

            if (bb.exitType != BasicBlockExitType.RETURN && bb.exitType != BasicBlockExitType.THROW) {
                sb.Append(", Exit points: [");

                var exitBlockIds = staticIntArrayPool.getSpan(bb.exitBlockIds);
                for (int i = 0; i < exitBlockIds.Length; i++)
                    sb.AppendFormat(ic, "{0}BB({1})", (i != 0) ? ", " : "", exitBlockIds[i]);

                sb.Append(']');
            }

            sb.AppendFormat(ic, ", IDOM: {0}", bb.immediateDominator);
            sb.AppendLine();

            if (bb.excHandlerId != -1) {
                sb.Append(' ', indentSpaces);
                sb.AppendFormat(ic, "Try region: EH({0})", bb.excHandlerId);
                sb.AppendLine();
            }

            var entryStack = m_staticIntArrayPool.getSpan(bb.stackAtEntry);
            var entryScope = m_staticIntArrayPool.getSpan(bb.scopeStackAtEntry);
            var entryLocals = m_staticIntArrayPool.getSpan(bb.localsAtEntry);

            sb.Append(' ', indentSpaces).Append("Stack: ");
            for (int i = 0; i < entryStack.Length; i++)
                sb.Append(' ').Append(dataNodeToString(entryStack[i], true));
            sb.AppendLine();

            sb.Append(' ', indentSpaces).Append("Scope: ");
            for (int i = 0; i < entryScope.Length; i++)
                sb.Append(' ').Append(dataNodeToString(entryScope[i], true));
            sb.AppendLine();

            sb.Append(' ', indentSpaces).Append("Locals:");
            for (int i = 0; i < entryLocals.Length; i++)
                sb.Append(' ').Append(dataNodeToString(entryLocals[i], true));
            sb.AppendLine();

            sb.Append(' ', indentSpaces).Append('{').AppendLine();

            var instructions = getInstructionsInBasicBlock(bb.id);

            for (int i = 0; i < instructions.Length; i++) {
                ref Instruction instr = ref instructions[i];
                sb.Append(' ', indentSpaces + 4);
                sb.AppendLine(instructionToString(instr));

                ReadOnlySpan<int> poppedNodeIds = getInstructionStackPoppedNodes(ref instr);
                int pushedNodeId = instr.stackPushedNodeId;

                if (poppedNodeIds.Length > 0 || pushedNodeId != -1) {
                    sb.Append(' ', indentSpaces + 8);

                    if (poppedNodeIds.Length > 0) {
                        sb.Append("Popped:");
                        for (int j = 0; j < poppedNodeIds.Length; j++)
                            sb.Append(' ').Append(dataNodeToString(poppedNodeIds[j]));
                        sb.Append("   ");
                    }

                    if (pushedNodeId != -1)
                        sb.Append("Pushed: ").Append(dataNodeToString(pushedNodeId));

                    sb.AppendLine();
                }

                Span<int> capturedScope = default;
                if (instr.opcode == ABCOp.newclass)
                    capturedScope = staticIntArrayPool.getSpan(instr.data.newClass.capturedScopeNodeIds);
                else if (instr.opcode == ABCOp.newfunction)
                    capturedScope = staticIntArrayPool.getSpan(instr.data.newFunction.capturedScopeNodeIds);

                if (capturedScope.Length > 0) {
                    sb.Append(' ', indentSpaces + 8).Append("Captured scope:");
                    for (int j = 0; j < capturedScope.Length; j++)
                        sb.Append(' ').Append(dataNodeToString(capturedScope[j]));
                    sb.AppendLine();
                }
            }

            sb.Append(' ', indentSpaces).AppendFormat(ic, "}} // End of basic block #{0}", bb.id);
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Outputs the current compilation state to the console.
        /// </summary>
        public void trace() {
            string className, methodName;

            ScriptMethod method = getCurrentMethod();
            if (method != null) {
                className = (method.declaringClass == null) ? "global" : method.declaringClass.name.ToString();
                methodName = method.name.ToString();
            }
            else {
                ScriptClassConstructor ctor = getCurrentConstructor();
                className = ctor.declaringClass.name.ToString();
                methodName = "constructor";
            }

            Console.WriteLine("Method {0}/{1}", className, methodName);
            Console.WriteLine("MethodCompilationFlags: {0}", m_flags);

            Console.WriteLine("Captured scope: {0}", String.Join(' ', getCapturedScopeItems()));

            Console.WriteLine("{");
            Console.WriteLine("    MaxStack: {0} (computed: {1})", maxStackSize, computedMaxStackSize);
            Console.WriteLine("    MaxScope: {0} (computed: {1})", maxScopeStackSize, computedMaxScopeSize);
            Console.WriteLine();

            for (int i = 0; i < m_instructions.length; i++) {
                ref Instruction instr = ref m_instructions[i];
                if ((instr.flags & InstructionFlags.STARTS_BASIC_BLOCK) != 0)
                    Console.WriteLine(basicBlockToString(instr.blockId, 1));
            }

            for (int i = 0; i < m_excHandlers.length; i++) {
                Console.WriteLine(exceptionHandlerToString(ref m_excHandlers[i], 1));
                if (i == m_excHandlers.length - 1)
                    Console.WriteLine();
            }

            Console.WriteLine("}} // End of method {0}/{1}\n", className, methodName);
        }

    }

}

#endif
