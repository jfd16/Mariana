
using System.Runtime.InteropServices;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Used for attaching additional opcode-specific data to an <see cref="Instruction"/> instance.
    /// </summary>
    /// <seealso cref="Instruction.data"/>
    [StructLayout(LayoutKind.Explicit)]
    internal struct InstructionData {

        [FieldOffset(0)] public Raw raw;

        [FieldOffset(0)] public PushConst pushConst;
        [FieldOffset(0)] public PushShort pushShort;
        [FieldOffset(0)] public Jump jump;
        [FieldOffset(0)] public Switch @switch;
        [FieldOffset(0)] public ReturnVoidOrValue returnVoidOrValue;
        [FieldOffset(0)] public NewArrOrObj newArrOrObj;
        [FieldOffset(0)] public ApplyType applyType;
        [FieldOffset(0)] public GetSetLocal getSetLocal;
        [FieldOffset(0)] public Hasnext2 hasnext2;
        [FieldOffset(0)] public DupOrSwap dupOrSwap;
        [FieldOffset(0)] public PushScope pushScope;
        [FieldOffset(0)] public GetScopeObject getScopeObject;
        [FieldOffset(0)] public CoerceOrIsType coerceOrIsType;
        [FieldOffset(0)] public Add add;
        [FieldOffset(0)] public Compare compare;
        [FieldOffset(0)] public CompareBranch compareBranch;
        [FieldOffset(0)] public NewCatch newCatch;
        [FieldOffset(0)] public NewClass newClass;
        [FieldOffset(0)] public NewFunction newFunction;
        [FieldOffset(0)] public AccessProperty accessProperty;
        [FieldOffset(0)] public GetSetSlot getSetSlot;
        [FieldOffset(0)] public CallOrConstruct callOrConstruct;
        [FieldOffset(0)] public ConstructSuper constructSuper;
        [FieldOffset(0)] public CallMethod callMethod;
        [FieldOffset(0)] public CallProperty callProperty;
        [FieldOffset(0)] public FindProperty findProperty;
        [FieldOffset(0)] public GetDescendants getDescendants;
        [FieldOffset(0)] public CheckFilter checkFilter;
        [FieldOffset(0)] public Dxns dxns;
        [FieldOffset(0)] public DebugFile debugFile;
        [FieldOffset(0)] public DebugLine debugLine;

        // This view of InstructionData is used by the decoder for assigning immediate
        // operands. (debug and switch instructions do not use this)
        public struct Raw {
            public int op1;
            public int op2;
        }

        /// <summary>
        /// Instruction-specific data for an instruction that pushes a constant.
        /// </summary>
        public struct PushConst {
            /// <summary>
            /// The index of the constant value in the ABC file constant pool.
            /// </summary>
            public int poolId;
        }

        /// <summary>
        /// Instruction-specific data for a pushshort instruction.
        /// </summary>
        public struct PushShort {
            /// <summary>
            /// The value of the integer pushed.
            /// </summary>
            public int value;
        }

        /// <summary>
        /// Instruction-specific data for a jump/branch instruction.
        /// </summary>
        public struct Jump {
            /// <summary>
            /// The byte offset (relative to the end of the instruction) by which to jump.
            /// </summary>
            public int targetOffset;
        }

        /// <summary>
        /// Instruction-specific data for a switch instruction.
        /// </summary>
        public struct Switch {
            /// <summary>
            /// The number of switch cases (excluding the default case).
            /// </summary>
            public int caseCount;

            /// <summary>
            /// A token in the compilation's static integer array pool for an array containing
            /// the byte offsets for all case targets (the first being the default case).
            /// </summary>
            public StaticArrayPoolToken<int> caseOffsets;
        }

        /// <summary>
        /// Instruction-specific data for a returnvoid or returnvalue instruction.
        /// </summary>
        public struct ReturnVoidOrValue {
            /// <summary>
            /// A token in the compilation's static integer array pool for an array containing
            /// the ids of any nodes left on the stack (after popping the return value, if any).
            /// </summary>
            public StaticArrayPoolToken<int> excessStackNodeIds;
        }

        /// <summary>
        /// Instruction-specific data for a newarr/newobj instruction.
        /// </summary>
        public struct NewArrOrObj {
            /// <summary>
            /// The number of array elements or object key-value pairs to pop from the stack.
            /// </summary>
            public int elementCount;
        }

        /// <summary>
        /// Instruction-specific data for an applytype instruction.
        /// </summary>
        public struct ApplyType {
            /// <summary>
            /// The number of type arguments to pop from the stack.
            /// </summary>
            public int argCount;
        }

        /// <summary>
        /// Instruction-specific data for a pushscope/pushwith instruction.
        /// </summary>
        public struct PushScope {
            /// <summary>
            /// The data node id for the pushed scope stack node.
            /// </summary>
            public int pushedNodeId;
        }

        /// <summary>
        /// Instruction-specific data for a get/set/increment/decrement/kill local instruction.
        /// </summary>
        public struct GetSetLocal {
            /// <summary>
            /// The id of the local variable being accessed.
            /// </summary>
            public int localId;

            /// <summary>
            /// The id of the data node representing the definition of the local variable being
            /// read or replaced.
            /// </summary>
            public int nodeId;

            /// <summary>
            /// The id of the data node representing the new definition of the local variable
            /// (for set/increment/decrement/kill only)
            /// </summary>
            public int newNodeId;
        }

        /// <summary>
        /// Instruction-specific data for a hasnext2 instruction.
        /// </summary>
        public struct Hasnext2 {
            /// <summary>
            /// The index of the local variable slot for the object variable.
            /// </summary>
            public int localId1;

            /// <summary>
            /// The index of the local variable slot for the index variable.
            /// </summary>
            public int localId2;

            /// <summary>
            /// A token in the static integer array pool representing a four-element array.
            /// The elements of this array are the data node ids for the following in order:
            /// old node for the object variable, old node for the index variable, new node
            /// for the object variable, new node for the index variable.
            /// </summary>
            public StaticArrayPoolToken<int> nodeIds;
        }

        /// <summary>
        /// Instruction-specific data for a dup or swap instruction.
        /// </summary>
        public struct DupOrSwap {
            /// <summary>
            /// The data node id of the stack node being duplicated or the first node being
            /// swapped.
            /// </summary>
            public int nodeId1;

            /// <summary>
            /// The data node id of the duplicate stack node pushed or the second node being
            /// swapped.
            /// </summary>
            public int nodeId2;
        }

        /// <summary>
        /// Instruction-specific data for a getscopeobject instruction.
        /// </summary>
        public struct GetScopeObject {
            /// <summary>
            /// The zero-based index (from the bottom) of the scope stack object to retrieve.
            /// </summary>
            public int index;

            /// <summary>
            /// The data node id of the scope stack node retrieved.
            /// </summary>
            public int nodeId;
        }

        /// <summary>
        /// Instruction-specific data for a coerce/istype/astype instruction.
        /// </summary>
        public struct CoerceOrIsType {
            /// <summary>
            /// The index of the class name in the ABC file multiname constant pool.
            /// </summary>
            public int multinameId;
        }

        /// <summary>
        /// Instruction-specific data for an add instruction.
        /// </summary>
        public struct Add {
            /// <summary>
            /// This is true if the operands of the addition must be coerced to the "any" type.
            /// This is false if the compiler is able to statically determine the operation
            /// (numeric addition or string concatenation) from the operand type information
            /// or the operands must be coerced to the Object type.
            /// </summary>
            public bool argsAreAnyType;
        }

        /// <summary>
        /// Instruction-specific data for a compare instruction.
        /// </summary>
        public struct Compare {
            /// <summary>
            /// The type of comparison to be performed, based on the operand types.
            /// </summary>
            public ComparisonType compareType;
        }

        /// <summary>
        /// Instruction-specific data for a compare-and-branch instruction.
        /// </summary>
        public struct CompareBranch {
            /// <summary>
            /// The byte offset (relative to the end of the instruction) by which to branch if the
            /// result of the comparison is true.
            /// </summary>
            public int targetOffset;

            /// <summary>
            /// The type of comparison to be performed, based on the operand types.
            /// </summary>
            public ComparisonType compareType;
        }

        /// <summary>
        /// Instruction-specific data for a newcatch instruction.
        /// </summary>
        public struct NewCatch {
            /// <summary>
            /// The index in the exception_info of the method body in the ABC file.
            /// </summary>
            public int excInfoId;
        }

        /// <summary>
        /// Instruction-specific data for a newclass instruction.
        /// </summary>
        public struct NewClass {
            /// <summary>
            /// The index of the class in the class_info array in the ABC file.
            /// </summary>
            public int classInfoId;

            /// <summary>
            /// A token in the compilation's static integer array pool for an array containing
            /// the data node ids for the captured scope stack.
            /// </summary>
            public StaticArrayPoolToken<int> capturedScopeNodeIds;
        }

        /// <summary>
        /// Instruction-specific data for a newfunction instruction.
        /// </summary>
        public struct NewFunction {
            /// <summary>
            /// The index of the function in the method_info array in the ABC file.
            /// </summary>
            public int methodInfoId;

            /// <summary>
            /// A token in the compilation's static integer array pool for an array containing
            /// the data node ids for the captured scope stack.
            /// </summary>
            public StaticArrayPoolToken<int> capturedScopeNodeIds;
        }

        /// <summary>
        /// Instruction-specific data for a getproperty, setproperty, initproperty, getsuper, setsuper,
        /// deleteproperty or getdescendants instruction.
        /// </summary>
        public struct AccessProperty {
            /// <summary>
            /// The index of the multiname in the ABC file multiname constant pool that
            /// represents the property name.
            /// </summary>
            public int multinameId;

            /// <summary>
            /// The id of the <see cref="ResolvedProperty"/> representing the resolved property.
            /// </summary>
            public int resolvedPropId;
        }

        /// <summary>
        /// Instruction-specific data for a getslot, setslot, getglobalslot or setglobalslot instruction.
        /// </summary>
        public struct GetSetSlot {
            /// <summary>
            /// The index of the slot.
            /// </summary>
            public int slotId;

            /// <summary>
            /// The id of the <see cref="ResolvedProperty"/> representing the resolved property.
            /// </summary>
            public int resolvedPropId;
        }

        /// <summary>
        /// Instruction-specific data for a callproperty, constructprop, callproplex, callpropvoid,
        /// callsuper or callsupervoid instruction.
        /// </summary>
        public struct CallProperty {
            /// <summary>
            /// The index of the multiname in the ABC file multiname constant pool that
            /// represents the property name.
            /// </summary>
            public int multinameId;

            /// <summary>
            /// The number of arguments from the stack to pass to the method call.
            /// </summary>
            public int argCount;

            /// <summary>
            /// The id of the <see cref="ResolvedProperty"/> representing the resolved property.
            /// </summary>
            public int resolvedPropId;
        }

        /// <summary>
        /// Instruction-specific data for a call or construct instruction.
        /// </summary>
        public struct CallOrConstruct {
            /// <summary>
            /// The number of arguments from the stack to pass to the method call.
            /// </summary>
            public int argCount;

            /// <summary>
            /// The id of the <see cref="ResolvedProperty"/> representing the resolved property.
            /// </summary>
            public int resolvedPropId;
        }

        /// <summary>
        /// Instruction-specific data for a constructsuper instruction.
        /// </summary>
        public struct ConstructSuper {
            /// <summary>
            /// The number of arguments from the stack to pass to the constructor call.
            /// </summary>
            public int argCount;
        }

        /// <summary>
        /// Instruction-specific data for a callmethod or callstatic instruction.
        /// </summary>
        public struct CallMethod {
            /// <summary>
            /// The method's disp_id (for callmethod) or the index of the method in the ABC
            /// file's method_info table (for callstatic).
            /// </summary>
            public int methodOrDispId;

            /// <summary>
            /// The number of arguments from the stack to pass to the method call.
            /// </summary>
            public int argCount;

            /// <summary>
            /// The id of the <see cref="ResolvedProperty"/> representing the method to be called.
            /// </summary>
            public int resolvedPropId;
        }

        /// <summary>
        /// Instruction-specific data for a findproperty, findpropstrict or getlex instruction.
        /// </summary>
        public struct FindProperty {
            /// <summary>
            /// The index of the multiname in the ABC file multiname constant pool that
            /// represents the property name.
            /// </summary>
            public int multinameId;

            /// <summary>
            /// The data node id of the scope stack node on which the property is resolved.
            /// If resolution is deferred to runtime, this value is equal to
            /// <see cref="LocalOrCapturedScopeRef.nullRef"/>.
            /// </summary>
            public LocalOrCapturedScopeRef scopeRef;

            /// <summary>
            /// The id of the <see cref="ResolvedProperty"/> representing the resolved property.
            /// </summary>
            public int resolvedPropId;
        }

        /// <summary>
        /// Instruction-specific data for a getdescendants instruction.
        /// </summary>
        public struct GetDescendants {
            /// <summary>
            /// The id of the multiname in the ABC file multiname constant pool.
            /// </summary>
            public int multinameId;
        }

        /// <summary>
        /// Instruction-specific data for a checkfilter instruction.
        /// </summary>
        public struct CheckFilter {
            /// <summary>
            /// The id of the stack node being checked.
            /// </summary>
            public int stackNodeId;
        }

        /// <summary>
        /// Instruction-specific data for a dxns instruction.
        /// </summary>
        public struct Dxns {
            /// <summary>
            /// The index of the URI to set as the default XML namespace in the ABC file string constant pool.
            /// </summary>
            public int uriId;
        }

        /// <summary>
        /// Instruction-specific data for a debugfile instruction.
        /// </summary>
        public struct DebugFile {
            /// <summary>
            /// The index of the file name in the ABC file string constant pool.
            /// </summary>
            public int fileNameId;
        }

        /// <summary>
        /// Instruction-specific data for a debugline instruction.
        /// </summary>
        public struct DebugLine {
            /// <summary>
            /// The line number
            /// </summary>
            public int lineNumber;
        }

    }

}
