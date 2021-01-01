using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using Mariana.AVM2.Core;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    using CapturedScopeItems = ReadOnlyArrayView<CapturedScopeItem>;

    /// <summary>
    /// Represents the scope stack captured by a class or function from the context in which
    /// it was created.
    /// </summary>
    internal sealed class CapturedScope {

        private CapturedScopeItems m_items;
        private CapturedScopeContainerType m_containerType;
        private bool m_hasClass = false;

        /// <summary>
        /// Creates a new instance of <see cref="CapturedScope"/>.
        /// </summary>
        /// <param name="items">A read-only array view of <see cref="CapturedScopeItem"/> instances
        /// representing the captured values, in a bottom-to-top order.</param>
        /// <param name="klass">The class for which to create the captured scope, or null when
        /// creating a captured scope for a function.</param>
        /// <param name="container">A <see cref="CapturedScopeContainerType"/> representing the
        /// container class used for holding the captured values.</param>
        public CapturedScope(CapturedScopeItems items, Class klass, CapturedScopeContainerType container) {
            _initItems(items, klass);
            m_containerType = container;
        }

        private void _initItems(CapturedScopeItems items, Class klass) {
            if (klass == null) {
                m_items = items;
                m_hasClass = false;
            }
            else {
                var newItems = new CapturedScopeItem[items.length + 1];
                items.asSpan().CopyTo(newItems.AsSpan(0, items.length));
                newItems[items.length] = new CapturedScopeItem(DataNodeType.CLASS, klass, false);
                m_hasClass = true;
                m_items = new CapturedScopeItems(newItems);
            }
        }

        /// <summary>
        /// Returns a read-only array view containing <see cref="CapturedScopeItem"/> instances
        /// representing the values captured.
        /// </summary>
        /// <param name="isStatic">If this <see cref="CapturedScope"/> represents the captured scope
        /// of a class, set to true to return the captured scope stack used for static class methods
        /// or false to return the scope used for instance methods. If this represents the captured
        /// scope of a function, this argument is ignored.</param>
        /// <returns>A read-only array view containing <see cref="CapturedScopeItem"/> instances
        /// representing the values captured, in bottom-to-top order.</returns>
        public CapturedScopeItems getItems(bool isStatic = false) =>
            (isStatic && m_hasClass) ? m_items.slice(0, m_items.length - 1) : m_items;

        /// <summary>
        /// Gets the <see cref="CapturedScopeContainerType"/> representing the emitted
        /// scope container for this <see cref="CapturedScope"/>.
        /// </summary>
        public CapturedScopeContainerType container => m_containerType;

    }

    /// <summary>
    /// Represents a value on a scope stack captured by a class or function.
    /// </summary>
    internal readonly struct CapturedScopeItem {

        /// <summary>
        /// A value from the <see cref="DataNodeType"/> enumeration representing the type
        /// of the value captured.
        /// </summary>
        public readonly DataNodeType dataType;

        /// <summary>
        /// True if the captured value was pushed onto the scope stack as a "with" scope.
        /// </summary>
        public readonly bool isWithScope;

        /// <summary>
        /// The <see cref="Class"/> for the type of the captured value (if <see cref="dataType"/>
        /// is <see cref="DataNodeType.OBJECT"/>) or the value of a class constant (if
        /// <see cref="dataType"/> is <see cref="DataNodeType.CLASS"/>).
        /// </summary>
        public readonly Class objClass;

        /// <summary>
        /// Creates a new instance of <see cref="CapturedScopeItem"/>.
        /// </summary>
        /// <param name="dataType">A value from the <see cref="DataNodeType"/> enumeration
        /// representing the type of the value captured.</param>
        /// <param name="objClass">The <see cref="Class"/> for the type of the captured value
        /// (if <see cref="dataType"/> is <see cref="DataNodeType.OBJECT"/>) or the value of
        /// a class constant (if <see cref="dataType"/> is <see cref="DataNodeType.CLASS"/>).</param>
        /// <param name="isWithScope">True if the captured value was pushed onto the scope
        /// stack as a "with" scope.</param>
        public CapturedScopeItem(DataNodeType dataType, Class objClass, bool isWithScope) {
            this.dataType = dataType;
            this.isWithScope = isWithScope;
            this.objClass = objClass;
        }

        /// <summary>
        /// Returns a string representation of this <see cref="CapturedScopeItem"/> instance.
        /// </summary>
        /// <returns>A string representation of this <see cref="CapturedScopeItem"/> instance.</returns>
        public override string ToString() {
            switch (dataType) {
                case DataNodeType.INT:
                    return "[I]";
                case DataNodeType.UINT:
                    return "[U]";
                case DataNodeType.NUMBER:
                    return "[N]";
                case DataNodeType.STRING:
                    return "[S]";
                case DataNodeType.BOOL:
                    return "[B]";
                case DataNodeType.OBJECT:
                    return "[O " + objClass.name.ToString() + "]";
                case DataNodeType.CLASS:
                    return "[C " + objClass.name.ToString() + "]";
                case DataNodeType.GLOBAL:
                    return "[global]";
                default:
                    return "";
            }
        }

    }

    /// <summary>
    /// A factory that creates captured scopes and their container classes.
    /// </summary>
    internal sealed class CapturedScopeFactory {

        private class _KeyComparer : IEqualityComparer<CapturedScopeItems> {

            public static _KeyComparer instance = new _KeyComparer();

            public bool Equals(CapturedScopeItems x, CapturedScopeItems y) {
                if (x.length != y.length)
                    return false;

                for (int i = 0; i < x.length; i++) {
                    ref readonly CapturedScopeItem item1 = ref x[i];
                    ref readonly CapturedScopeItem item2 = ref y[i];

                    if (item1.dataType != item2.dataType || item1.objClass != item2.objClass
                        || item1.isWithScope != item2.isWithScope)
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(CapturedScopeItems obj) {
                int hash = 91697839;
                for (int i = 0; i < obj.length; i++) {
                    ref readonly CapturedScopeItem item = ref obj[i];
                    hash += item.dataType.GetHashCode();
                    hash *= 54811607;
                    if (item.objClass != null) {
                        hash += item.objClass.GetHashCode();
                        hash *= 65181817;
                    }
                    hash += item.isWithScope.GetHashCode();
                    hash *= 63704189;
                }
                return hash;
            }

        }

        private readonly Dictionary<CapturedScopeItems, CapturedScopeContainerType> m_cachedContainers;
        private readonly IncrementCounter m_counter;
        private readonly ScriptCompileContext m_context;
        private readonly ILBuilder m_ilBuilder;

        /// <summary>
        /// Creates a new instance of <see cref="CapturedScopeFactory"/>.
        /// </summary>
        /// <param name="context">The compilation context in which the
        /// <see cref="CapturedScope"/> instances created by the factory will be used.</param>
        public CapturedScopeFactory(ScriptCompileContext context) {
            m_context = context;
            m_ilBuilder = new ILBuilder(context.assemblyBuilder.metadataContext.ilTokenProvider);
            m_cachedContainers = new Dictionary<CapturedScopeItems, CapturedScopeContainerType>(_KeyComparer.instance);
            m_counter = new IncrementCounter();
        }

        /// <summary>
        /// Creates a captured scope for the given class.
        /// </summary>
        /// <param name="klass">The class for which to create a captured scope.</param>
        /// <param name="scopeItems">A <see cref="ReadOnlyArrayView{CapturedScopeItem}"/> containing
        /// the type information for the values on the captured scope stack, in a bottom-to-top
        /// order.</param>
        /// <returns>A <see cref="CapturedScope"/> representing the captured scope created for
        /// <paramref name="klass"/>.</returns>
        public CapturedScope getCapturedScopeForClass(Class klass, in CapturedScopeItems scopeItems) =>
            new CapturedScope(scopeItems, klass, _getContainer(scopeItems));

        /// <summary>
        /// Creates a captured scope for a function.
        /// </summary>
        /// <param name="scopeItems">A <see cref="ReadOnlyArrayView{CapturedScopeItem}"/> containing
        /// the type information for the values on the captured scope stack, in a bottom-to-top
        /// order.</param>
        /// <returns>A <see cref="CapturedScope"/> representing the captured scope created.</returns>
        public CapturedScope getCapturedScopeForFunction(in CapturedScopeItems scopeItems) =>
            new CapturedScope(scopeItems, null, _getContainer(scopeItems));

        /// <summary>
        /// Creates a container type for a captured scope given the types of its values,
        /// reusing an existing container if possible.
        /// </summary>
        /// <param name="scopeItems">A <see cref="ReadOnlyArrayView{CapturedScopeItem}"/> containing
        /// the type information for the values on the captured scope stack, in a bottom-to-top
        /// order.</param>
        /// <returns>A <see cref="CapturedScopeContainerType"/> reprsenting the container type.</returns>
        private CapturedScopeContainerType _getContainer(in CapturedScopeItems scopeItems) {
            CapturedScopeContainerType container;

            if (!m_cachedContainers.TryGetValue(scopeItems, out container)) {
                string containerName = NameMangler.createScopeContainerName(m_counter.next());
                container = new CapturedScopeContainerType(scopeItems, containerName, m_context, m_ilBuilder);
                m_cachedContainers.Add(scopeItems, container);
            }

            return container;
        }

    }

    internal sealed class CapturedScopeContainerType {

        private readonly EntityHandle m_typeHandle;
        private readonly EntityHandle m_ctorHandle;
        private readonly EntityHandle[] m_fieldHandles;
        private readonly EntityHandle m_rtStackFieldHandle;
        private readonly EntityHandle m_rtStackGetMethodHandle;

        public CapturedScopeContainerType(
            in CapturedScopeItems items, string containerName, ScriptCompileContext context, ILBuilder ilBuilder)
        {
            var metadataContext = context.assemblyBuilder.metadataContext;

            TypeBuilder type = context.assemblyBuilder.defineType(containerName, TypeAttributes.Public | TypeAttributes.Sealed);
            m_typeHandle = type.handle;

            MethodBuilder ctor = type.defineConstructor(MethodAttributes.Public);
            m_ctorHandle = ctor.handle;

            FieldBuilder rtStackField = type.defineField(
                "rtstack", metadataContext.getTypeSignature(typeof(RuntimeScopeStack)), FieldAttributes.Public);
            m_rtStackFieldHandle = rtStackField.handle;

            MethodBuilder rtStackGetMethod = type.defineMethod(
                "getRuntimeStack",
                MethodAttributes.Public,
                metadataContext.getTypeSignature(typeof(RuntimeScopeStack))
            );
            m_rtStackGetMethodHandle = rtStackGetMethod.handle;

            m_fieldHandles = new EntityHandle[items.length];

            _emitItemFields(items, type, context);
            _emitConstructor(ctor, ilBuilder);
            _emitGetRuntimeStackMethod(rtStackGetMethod, items, context, ilBuilder);
        }

        public EntityHandle typeHandle => m_typeHandle;

        public EntityHandle ctorHandle => m_ctorHandle;

        public EntityHandle rtStackFieldHandle => m_rtStackFieldHandle;

        public EntityHandle rtStackGetMethodHandle => m_rtStackGetMethodHandle;

        public EntityHandle getFieldHandle(int height) => m_fieldHandles[height];

        private void _emitConstructor(MethodBuilder ctor, ILBuilder il) {
            il.emit(ILOp.ldarg_0);
            il.emit(ILOp.call, KnownMembers.systemObjectCtor, -1);
            il.emit(ILOp.ret);
            ctor.setMethodBody(il.createMethodBody());
        }

        private void _emitItemFields(in CapturedScopeItems items, TypeBuilder type, ScriptCompileContext context) {
            for (int i = 0; i < items.length; i++) {
                ref readonly var item = ref items[i];
                if (item.dataType == DataNodeType.CLASS || item.dataType == DataNodeType.GLOBAL)
                    continue;

                Class fieldType = (item.dataType == DataNodeType.OBJECT)
                    ? item.objClass
                    : DataNodeTypeHelper.getClass(item.dataType);

                var fieldBuilder = type.defineField(
                    "item" + i.ToString(CultureInfo.InvariantCulture),
                    context.getTypeSignature(fieldType),
                    FieldAttributes.Public
                );

                m_fieldHandles[i] = fieldBuilder.handle;
            }
        }

        private void _emitGetRuntimeStackMethod(
            MethodBuilder method, in CapturedScopeItems items, ScriptCompileContext context, ILBuilder il)
        {
            var label = il.createLabel();
            var local = il.declareLocal(typeof(RuntimeScopeStack));

            il.emit(ILOp.ldarg_0);
            il.emit(ILOp.volatile_);
            il.emit(ILOp.ldfld, m_rtStackFieldHandle);
            il.emit(ILOp.dup);
            il.emit(ILOp.brfalse, label);
            il.emit(ILOp.ret);

            il.markLabel(label);
            il.emit(ILOp.pop);

            il.emit(ILOp.ldc_i4, items.length);
            il.emit(ILOp.ldnull);
            il.emit(ILOp.newobj, KnownMembers.rtScopeStackNew, -1);
            il.emit(ILOp.stloc, local);

            for (int i = 0; i < items.length; i++) {
                il.emit(ILOp.ldloc, local);

                ref readonly var item = ref items[i];

                if (item.dataType == DataNodeType.GLOBAL) {
                    il.emit(ILOp.ldsfld, context.emitConstData.globalObjFieldHandle);
                }
                else if (item.dataType == DataNodeType.CLASS) {
                    int classId = context.emitConstData.getClassIndex(item.objClass);

                    il.emit(ILOp.ldsfld, context.emitConstData.classesArrayFieldHandle);
                    il.emit(ILOp.ldc_i4, classId);
                    il.emit(ILOp.ldelem_ref);
                    il.emit(ILOp.callvirt, KnownMembers.classGetClassObj, 0);
                }
                else {
                    il.emit(ILOp.ldarg_0);
                    il.emit(ILOp.ldfld, m_fieldHandles[i]);

                    Class fieldType = (item.dataType == DataNodeType.OBJECT)
                        ? item.objClass
                        : DataNodeTypeHelper.getClass(item.dataType);

                    ILEmitHelper.emitTypeCoerceToObject(il, fieldType);
                }

                BindOptions bindOpts = BindOptions.SEARCH_TRAITS;
                if (item.dataType == DataNodeType.GLOBAL)
                    bindOpts |= BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC;
                else if (item.isWithScope)
                    bindOpts |= BindOptions.SEARCH_DYNAMIC;

                il.emit(ILOp.ldc_i4, (int)bindOpts);
                il.emit(ILOp.call, KnownMembers.rtScopeStackPush, -3);
            }

            il.emit(ILOp.ldarg_0);
            il.emit(ILOp.ldloc, local);
            il.emit(ILOp.volatile_);
            il.emit(ILOp.stfld, m_rtStackFieldHandle);
            il.emit(ILOp.ldloc, local);
            il.emit(ILOp.ret);

            method.setMethodBody(il.createMethodBody());
        }

    }

}
