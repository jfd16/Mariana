using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    internal sealed class ScriptCompileContext {

        private enum ClassCompileState : byte {
            INIT,
            DEF_CREATING,
            DEF_CREATED,
            TRAITS_CREATING,
            TRAITS_CREATED,
            TRAITS_EMITTED,
        }

        private enum MethodNameMangleMode {
            NONE,
            METHOD,
            GETTER,
            SETTER,
        }

        private class ClassData {
            public Class[] declaredInterfaces;
            public ScriptMethod staticInit;

            public bool isExported;
            public bool isActivation;

            public ClassCompileState compileState;

            public TypeBuilder typeBuilder;
            public MethodBuilder ctorBuilder;

            public FieldBuilder capturedScopeField;
            public CapturedScope capturedScope;
        }

        private readonly struct InterfaceMethodImpl {
            public readonly MethodTrait methodDef;
            public readonly ScriptClass implByClass;

            public InterfaceMethodImpl(MethodTrait methodDef, ScriptClass implByClass) {
                this.methodDef = methodDef;
                this.implByClass = implByClass;
            }
        }

        private class MethodInfoData {
            public object methodOrCtor;
            public ABCMethodBodyInfo body;
        }

        private class MethodTraitData {
            public MethodBuilder methodBuilder;
            public MethodTrait overrideMethodDef;
            public DynamicArray<InterfaceMethodImpl> interfaceMethodImpls;

            public bool isFunction;
            public CapturedScope funcCapturedScope;
        }

        private class ScriptData {
            public ScriptMethod initMethod;
            public DynamicArray<Trait> traits;
            public SlotMap slotMap;
        }

        private struct MethodCompilationParams {
            public object methodOrCtor;
            public MethodBuilder methodBuilder;
            public CapturedScope capturedScope;
            public MethodCompilationFlags initFlags;
        }

        private static readonly IncrementCounter s_dynamicAssemblyCounter = new IncrementCounter();

        private static readonly Class s_objectClass = Class.fromType<ASObject>();

        private object m_lockObj = new object();

        private ScriptCompileOptions m_options;

        private ApplicationDomain m_domain;

        private ABCFile m_abcFile;

        private ClassTraitTable m_unexportedClassTraits;

        private DynamicArray<ScriptData> m_abcScriptDataByIndex;

        private DynamicArray<MethodInfoData> m_abcMethodInfoDataByIndex;

        private DynamicArray<ScriptClass> m_abcClassesByIndex;

        private ReferenceDictionary<ScriptClass, ClassData> m_classData =
            new ReferenceDictionary<ScriptClass, ClassData>();

        private ReferenceDictionary<ScriptField, ASAny> m_fieldDefaultValues =
            new ReferenceDictionary<ScriptField, ASAny>();

        private ReferenceDictionary<MethodTrait, MethodTraitData> m_methodTraitData =
            new ReferenceDictionary<MethodTrait, MethodTraitData>();

        private ReferenceDictionary<Trait, ABCScriptInfo> m_traitExportScripts =
            new ReferenceDictionary<Trait, ABCScriptInfo>();

        private Dictionary<ABCMultiname, Trait> m_globalTraitsByMultiname = new Dictionary<ABCMultiname, Trait>();

        private Dictionary<ABCMultiname, Class> m_classesByMultiname = new Dictionary<ABCMultiname, Class>();

        private Dictionary<QName, ScriptProperty> m_tempPropsInst = new Dictionary<QName, ScriptProperty>();

        private Dictionary<QName, ScriptProperty> m_tempPropsStatic = new Dictionary<QName, ScriptProperty>();

        private ReferenceDictionary<object, EntityHandle> m_traitEntityHandles = new ReferenceDictionary<object, EntityHandle>();

        private ReferenceDictionary<Class, TypeSignature> m_classTypeSignatures = new ReferenceDictionary<Class, TypeSignature>();

        private ReferenceDictionary<Class, EntityHandle> m_objectCastMethodHandles = new ReferenceDictionary<Class, EntityHandle>();

        private ReferenceDictionary<Class, EntityHandle> m_anyCastMethodHandles = new ReferenceDictionary<Class, EntityHandle>();

        private ReferenceDictionary<Class, TypeSignature> m_optionalParamTypeSig = new ReferenceDictionary<Class, TypeSignature>();

        private Dictionary<int, MemberInfo> m_vectorMembersByDefToken = new Dictionary<int, MemberInfo>();

        private DynamicArray<int> m_nsSetEmitConstIdsByABCIndex;

        private int m_publicNsSetEmitConstId;

        private DynamicArray<ScriptClass> m_catchScopeClasses;

        private Queue<ScriptMethod> m_functionCompileQueue = new Queue<ScriptMethod>();

        private AssemblyBuilder m_assemblyBuilder;

        private TypeBuilder m_globalTraitsContainer;

        private ILBuilder m_contextILBuilder;

        private EmitConstantData m_emitConstantData;

        private CapturedScopeFactory m_capturedScopeFactory;

        private readonly NameMangler m_nameMangler = new NameMangler();

        private readonly IncrementCounter m_scriptInitCounter = new IncrementCounter();

        private readonly IncrementCounter m_catchScopeCounter = new IncrementCounter();

        private readonly IncrementCounter m_activationCounter = new IncrementCounter();

        private readonly IncrementCounter m_newFunctionCounter = new IncrementCounter();

        private AssemblyBuilderEmitResult m_emitResult;

        private Assembly m_loadedAssembly;

        private DynamicArray<MethodTrait> m_scriptInitializers;

        internal ScriptCompileContext(ApplicationDomain domain, ScriptCompileOptions options) {
            m_options = options;
            m_domain = domain;

            _initAssembly();
        }

        /// <summary>
        /// Gets the <see cref="ScriptCompileOptions"/> instance containing the current compiler
        /// configuration.
        /// </summary>
        public ScriptCompileOptions compileOptions => m_options;

        /// <summary>
        /// Gets the application domain in which the global classes and traits defined in the
        /// compilation are registered.
        /// </summary>
        public ApplicationDomain applicationDomain => m_domain;

        /// <summary>
        /// Returns the <see cref="EmitConstantData"/> representing the dynamic assembly
        /// constant pool for this compilation.
        /// </summary>
        public EmitConstantData emitConstData => m_emitConstantData;

        /// <summary>
        /// Returns a <see cref="LockedObject{ScriptCompileContext}"/> instance that provides thread-safe
        /// locked access to this context when parallel method compilation is enabled. To release the lock,
        /// call Dispose on the returned instance. No lock is taken if parallel compilation is not enabled.
        /// </summary>
        /// <returns>A <see cref="LockedObject{ScriptCompileContext}"/> instance</returns>
        public LockedObject<ScriptCompileContext> getLocked() =>
            new LockedObject<ScriptCompileContext>(this, (m_options.numParallelCompileThreads > 1) ? m_lockObj : null);

        /// <summary>
        /// Compiles an ABC file in this context.
        /// </summary>
        /// <param name="file">The <see cref="ABCFile"/> instance to be compiled.</param>
        public void compileFile(ABCFile file) {
            m_abcFile = file;

            m_abcClassesByIndex.clearAndAddDefault(m_abcFile.getClassInfo().length);
            m_abcMethodInfoDataByIndex.clearAndAddDefault(m_abcFile.getMethodInfo().length);
            m_abcScriptDataByIndex.clearAndAddDefault(m_abcFile.getScriptInfo().length);
            m_classData.clear();
            m_methodTraitData.clear();
            m_fieldDefaultValues.clear();
            m_traitExportScripts.clear();

            m_nsSetEmitConstIdsByABCIndex.clearAndAddUninitialized(m_abcFile.getNamespaceSetPool().length);
            m_nsSetEmitConstIdsByABCIndex.asSpan().Fill(-1);

            m_publicNsSetEmitConstId = -1;

            m_unexportedClassTraits = new ClassTraitTable(null, true);

            _loadABCData();

            foreach (var entry in m_classData)
                _createClassDefIfInContext(entry.Key);

            foreach (var entry in m_classData)
                _createTypeBuilderIfInContext(entry.Key);

            _createScriptAndClassTraits();
            _emitScriptAndClassMembers();
            _emitMethodOverrides();
            _compileMethodBodies();
        }

        /// <summary>
        /// Finishes the current compilation and loads the emitted assembly.
        /// </summary>
        public void finishCompilationAndLoad() {
            m_emitConstantData.setGlobalObject(m_domain.globalObject);
            m_emitConstantData.emitConstDataInitializer(m_contextILBuilder);

            m_emitResult = m_assemblyBuilder.emit();

            if (m_options.emitAssemblySavePath != null)
                File.WriteAllBytes(m_options.emitAssemblySavePath, m_emitResult.peImageBytes);

            m_loadedAssembly = Assembly.Load(m_emitResult.peImageBytes);

            _setUnderlyingDefinitionsForTraits();
            VectorInstFromScriptClass.completeInstances();

            m_emitConstantData.loadData(m_loadedAssembly, m_emitResult.tokenMapping);
        }

        /// <summary>
        /// Returns the script initializer methods defined by the ABC file(s) that have been
        /// compiled in this context.
        /// </summary>
        /// <returns>A read-only array view containing <see cref="MethodTrait"/> instances representing
        /// the script initializer methods defined by the ABC file(s) that have been
        /// compiled in this context.</returns>
        public ReadOnlyArrayView<MethodTrait> getAllScriptInitMethods() => m_scriptInitializers.asReadOnlyArrayView();

        private void _loadABCData() {
            var methodBodyInfo = m_abcFile.getMethodBodyInfo();
            var scriptInfo = m_abcFile.getScriptInfo();
            var classInfo = m_abcFile.getClassInfo();

            // Link methods to method bodies.

            for (int i = 0; i < methodBodyInfo.length; i++) {
                ABCMethodInfo mi = methodBodyInfo[i].methodInfo;
                if (getMethodBodyInfo(mi) != null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_METHOD_MULTIPLE_BODIES);

                var methodInfoData = new MethodInfoData();
                m_abcMethodInfoDataByIndex[mi.abcIndex] = methodInfoData;

                methodInfoData.body = methodBodyInfo[i];
            }

            // Create the internal class data.

            for (int i = 0; i < classInfo.length; i++) {
                ScriptClass klass = new ScriptClass(classInfo[i], m_domain);
                m_abcClassesByIndex[i] = klass;

                ClassData classData = new ClassData();
                m_classData[klass] = classData;
            }

            // Load the script classes, since their names may be used.

            for (int i = 0; i < scriptInfo.length; i++) {
                m_abcScriptDataByIndex[i] = new ScriptData();
                _loadScriptClasses(scriptInfo[i]);
            }

            // Store classes that have not been exported by scripts (or exported only as aliases)
            // into a separate table specific to the ABC file to allow references to their names.

            for (int i = 0; i < classInfo.length; i++) {
                ScriptClass klass = m_abcClassesByIndex[i];
                ClassData classData = m_classData[klass];
                if (classData.isExported)
                    continue;

                if (!m_unexportedClassTraits.tryAddTrait(klass))
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, klass.name);
            }
        }

        private void _loadScriptClasses(ABCScriptInfo scriptInfo) {
            var scriptData = m_abcScriptDataByIndex[scriptInfo.abcIndex];
            var traitInfo = scriptInfo.getTraits();

            scriptData.slotMap = new SlotMap();

            for (int i = 0; i < traitInfo.length; i++) {
                ABCTraitInfo ti = traitInfo[i];
                if (ti.kind != ABCTraitFlags.Class)
                    continue;

                if (ti.name == QName.publicName("void"))
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, ti.name);

                ScriptClass klass = m_abcClassesByIndex[ti.classInfo.abcIndex];
                ClassData classData = m_classData[klass];
                Trait traitToAdd;

                if (ti.name == klass.name) {
                    classData.isExported = true;
                    klass.setMetadata(ti.metadata);
                    traitToAdd = klass;
                    m_traitExportScripts[klass] = scriptInfo;
                }
                else {
                    traitToAdd = new ClassAlias(ti.name, null, m_domain, klass, ti.metadata);
                }

                bool classAdded = m_domain.tryDefineGlobalTrait(traitToAdd, m_options.hideParentDomainDefinitions);
                if (!classAdded && m_options.failOnGlobalNameConflict)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, traitToAdd.name);

                int slotId = ti.slotId;
                if (slotId > 0) {
                    if (!scriptData.slotMap.tryAddSlot(slotId, traitToAdd))
                        throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_SLOT_ID_ALREADY_TAKEN, slotId, "global");
                }
            }
        }

        /// <summary>
        /// Defines a getter or setter for a property in the temporary property list and returns
        /// the corresponding <see cref="ScriptMethod"/> instance. If no property with the given
        /// name exists in the temporary property list, one is created.
        /// </summary>
        /// <returns>The <see cref="ScriptMethod"/> instance representing the defined accessor.</returns>
        /// <param name="traitInfo">The <see cref="ABCTraitInfo"/> instance representing the property
        /// accessor.</param>
        /// <param name="declClass">The class on which the property is declared, or null for a global property.</param>
        /// <param name="isStatic">Set to true for a static or global property, false for an instance property.</param>
        private ScriptMethod _definePropertyAccessor(ABCTraitInfo traitInfo, ScriptClass declClass, bool isStatic) {
            QName name = traitInfo.name;

            Dictionary<QName, ScriptProperty> propMap = isStatic ? m_tempPropsStatic : m_tempPropsInst;
            if (!propMap.TryGetValue(name, out ScriptProperty prop)) {
                prop = new ScriptProperty(name, declClass, m_domain, isStatic);
                propMap[name] = prop;
            }

            bool isFinal = (traitInfo.flags & ABCTraitFlags.ATTR_Final) != 0;
            bool isOverride = (traitInfo.flags & ABCTraitFlags.ATTR_Override) != 0;

            if (traitInfo.kind == ABCTraitFlags.Getter) {
                if (prop.getter != null)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, traitInfo.name.ToString());

                QName getterName = m_nameMangler.createGetterQualifiedName(traitInfo.name);
                var getter = new ScriptMethod(
                    traitInfo.methodInfo, getterName, declClass, m_domain,
                    isStatic, isFinal, isOverride, traitInfo.metadata
                );

                _setMethodForMethodInfo(traitInfo.methodInfo, getter);
                m_methodTraitData[getter] = new MethodTraitData();
                prop.setAccessors(getter, prop.setter);
                return getter;
            }
            else {
                if (prop.setter != null)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, traitInfo.name.ToString());

                QName setterName = m_nameMangler.createSetterQualifiedName(traitInfo.name);
                var setter = new ScriptMethod(
                    traitInfo.methodInfo, setterName, declClass, m_domain,
                    isStatic, isFinal, isOverride, traitInfo.metadata
                );

                _setMethodForMethodInfo(traitInfo.methodInfo, setter);
                m_methodTraitData[setter] = new MethodTraitData();
                prop.setAccessors(prop.getter, setter);
                return setter;
            }
        }

        private void _createClassDefIfInContext(Class klass) {
            var scriptClass = klass.getClassImpl() as ScriptClass;
            if (scriptClass == null || scriptClass.abcClassInfo == null)
                return;

            ClassData classData = m_classData[scriptClass];
            if (classData == null || classData.compileState == ClassCompileState.DEF_CREATED)
                return;

            if (classData.compileState == ClassCompileState.DEF_CREATING)
                // Circular reference detected.
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_CLASS_CIRCULAR_REFERENCE);

            classData.compileState = ClassCompileState.DEF_CREATING;

            ABCMultiname parentName = scriptClass.abcClassInfo.parentName;
            Class parent = null;

            if (!scriptClass.isInterface) {
                parent = getClassByMultiname(parentName);
                if (parent.isInterface || parent.isFinal)
                    throw ErrorHelper.createError(ErrorCode.CANNOT_EXTEND_CLASS, klass.name, parent.name);
            }
            else {
                // An interface cannot extend a class.
                if (!m_abcFile.isAnyName(parentName)) {
                    throw ErrorHelper.createError(
                        ErrorCode.CANNOT_EXTEND_CLASS, klass.name, m_abcFile.multinameToString(parentName));
                }
            }

            if (parent != null) {
                _createClassDefIfInContext(parent);
                scriptClass.setParent(parent);
            }

            var ifaceNames = scriptClass.abcClassInfo.getInterfaceNames();
            Class[] declaredInterfaces = Array.Empty<Class>();

            if (ifaceNames.length != 0) {
                declaredInterfaces = new Class[ifaceNames.length];
                for (int i = 0; i < ifaceNames.length; i++) {
                    Class iface = getClassByMultiname(ifaceNames[i]);
                    if (!iface.isInterface)
                        throw ErrorHelper.createError(ErrorCode.CANNOT_IMPLEMENT_INTERFACE, klass.name, iface.name);

                    _createClassDefIfInContext(iface);
                    declaredInterfaces[i] = iface;
                }
            }

            classData.declaredInterfaces = declaredInterfaces;
            scriptClass.setInterfaces(_interfaceTransitiveClosure(declaredInterfaces, parent));

            classData.compileState = ClassCompileState.DEF_CREATED;
        }

        /// <summary>
        /// Computes the transitive closure of the interfaces implemented by a class.
        /// </summary>
        /// <returns>An array containing the transitive closure of the interfaces implemented.</returns>
        /// <param name="declared">The interfaces declared by the class as being implemented.</param>
        /// <param name="parent">The base class of the class. Null if the class is an interface.</param>
        private Class[] _interfaceTransitiveClosure(Class[] declared, Class parent) {
            if (declared.Length == 0 && (parent == null || parent.getImplementedInterfaces().length == 0))
                return Array.Empty<Class>();

            HashSet<Class> interfaceSet = new HashSet<Class>();

            for (int i = 0; i < declared.Length; i++) {
                interfaceSet.Add(declared[i]);

                // Assume that the transitive closure of the parents of the interfaces
                // have already been computed.
                var interfaceBases = declared[i].getImplementedInterfaces();
                for (int j = 0; j < interfaceBases.length; j++)
                    interfaceSet.Add(interfaceBases[j]);
            }

            if (parent != null) {
                // Also assume that the transitive closure of the base class interfaces
                // has been computed.
                var parentInterfaces = parent.getImplementedInterfaces();
                for (int i = 0; i < parentInterfaces.length; i++)
                    interfaceSet.Add(parentInterfaces[i]);
            }

            var arr = new Class[interfaceSet.Count];
            interfaceSet.CopyTo(arr);
            return arr;
        }

        /// <summary>
        /// Initialises the dynamic assembly into which the compiled code will be emitted.
        /// </summary>
        private void _initAssembly() {
            string assemblyName = m_options.emitAssemblyName;
            if (assemblyName == null) {
                string countStr = ASuint.AS_convertString((uint)s_dynamicAssemblyCounter.atomicNext());
                assemblyName = "AVM2DynamicAssembly_" + countStr;
            }

            m_assemblyBuilder = new AssemblyBuilder(assemblyName);
            m_globalTraitsContainer = m_assemblyBuilder.defineType(
                "{Global}", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            m_emitConstantData = new EmitConstantData(m_assemblyBuilder);
            m_contextILBuilder = new ILBuilder(m_assemblyBuilder.metadataContext.ilTokenProvider);

            m_capturedScopeFactory = new CapturedScopeFactory(this);
        }

        /// <summary>
        /// Emits the TypeBuilder for the given class if it belongs to this context.
        /// </summary>
        /// <param name="klass">The class whose TypeBuilder is to be emitted.</param>
        private void _createTypeBuilderIfInContext(Class klass) {
            var scriptClass = klass.getClassImpl() as ScriptClass;
            if (scriptClass == null || scriptClass.abcClassInfo == null)
                return;

            ClassData classData = m_classData[scriptClass];
            if (classData.typeBuilder != null)
                return;

            TypeAttributes typeAttrs = TypeAttributes.Public | TypeAttributes.Class;
            if (scriptClass.isInterface)
                typeAttrs |= TypeAttributes.Interface | TypeAttributes.Abstract;
            else if (scriptClass.isFinal)
                typeAttrs |= TypeAttributes.Sealed;

            TypeBuilder tb = m_assemblyBuilder.defineType(m_nameMangler.createTypeName(scriptClass.name), typeAttrs);
            classData.typeBuilder = tb;
            m_traitEntityHandles[scriptClass] = tb.handle;

            if (!scriptClass.isInterface) {
                _createTypeBuilderIfInContext(scriptClass.parent);
                tb.setParent(getEntityHandle(scriptClass.parent));
            }

            Class[] declaredInterfaces = classData.declaredInterfaces;
            for (int i = 0; i < declaredInterfaces.Length; i++) {
                _createTypeBuilderIfInContext(declaredInterfaces[i]);
                tb.addInterface(getEntityHandle(declaredInterfaces[i]));
            }
        }

        /// <summary>
        /// Creates all the global traits and class traits loaded into this context.
        /// </summary>
        private void _createScriptAndClassTraits() {
            var scriptInfo = m_abcFile.getScriptInfo();

            for (int i = 0; i < scriptInfo.length; i++) {
                ABCScriptInfo si = scriptInfo[i];
                ScriptData scriptData = m_abcScriptDataByIndex[i];

                string initName = m_nameMangler.createScriptInitName(m_scriptInitCounter.next());

                var initMethod = new ScriptMethod(
                    si.initMethod, QName.publicName(initName), null, m_domain, true, true, false, null);

                scriptData.initMethod = initMethod;
                m_scriptInitializers.add(initMethod);
                m_methodTraitData[initMethod] = new MethodTraitData();

                m_traitExportScripts[initMethod] = si;
                _setMethodForMethodInfo(si.initMethod, initMethod);

                _createMethodTraitSig(initMethod);

                if (initMethod.requiredParamCount > 0)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_METHOD_NO_REQUIRED_PARAMS_ALLOWED, initMethod.name);

                var traitInfo = si.getTraits();
                for (int j = 0; j < traitInfo.length; j++) {
                    if (traitInfo[j].kind == ABCTraitFlags.Class)
                        // Class traits have already been loaded at this point.
                        continue;

                    Trait trait = _createScriptTraitFromTraitInfo(traitInfo[j], scriptData);
                    m_traitExportScripts[trait] = si;
                }

                foreach (ScriptProperty prop in m_tempPropsStatic.Values) {
                    bool isAdded = m_domain.tryDefineGlobalTrait(prop, m_options.hideParentDomainDefinitions);
                    if (isAdded) {
                        if (prop.getter != null)
                            scriptData.traits.add(prop.getter);
                        if (prop.setter != null)
                            scriptData.traits.add(prop.setter);
                    }
                    else if (m_options.failOnGlobalNameConflict) {
                        throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, prop.name);
                    }
                }

                m_tempPropsStatic.Clear();
            }

            foreach (var p in m_classData)
                _createClassTraitsIfInContext(p.Key);
        }

        /// <summary>
        /// Creates a global trait from the given <see cref="ABCTraitInfo"/> instance.
        /// </summary>
        /// <returns>The <see cref="Trait"/> instance representing the created trait.</returns>
        /// <param name="traitInfo">The <see cref="ABCTraitInfo"/> instance representing the
        /// global trait to create.</param>
        /// <param name="scriptData">A <see cref="ScriptData"/> instance representing the script
        /// that defined the trait.</param>
        private Trait _createScriptTraitFromTraitInfo(ABCTraitInfo traitInfo, ScriptData scriptData) {
            if (traitInfo.kind == ABCTraitFlags.Function)
                // Not sure where this is used, don't support it for now.
                throw ErrorHelper.createError(ErrorCode.INVALID_TRAIT_KIND, "Function");

            if (traitInfo.name == QName.publicName("void"))
                throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, traitInfo.name);

            Trait trait = null;
            bool isProperty = false;

            if (traitInfo.kind == ABCTraitFlags.Slot || traitInfo.kind == ABCTraitFlags.Const) {
                trait = _createFieldTrait(traitInfo, null, true);
            }
            else if (traitInfo.kind == ABCTraitFlags.Method) {
                ScriptMethod method = new ScriptMethod(traitInfo, null, m_domain, true);
                _setMethodForMethodInfo(traitInfo.methodInfo, method);
                trait = method;
                m_methodTraitData[method] = new MethodTraitData();
                _createMethodTraitSig(method);
            }
            else if (traitInfo.kind == ABCTraitFlags.Getter || traitInfo.kind == ABCTraitFlags.Setter) {
                isProperty = true;
                ScriptMethod method = _definePropertyAccessor(traitInfo, null, true);
                trait = method;
                _createMethodTraitSig(method);
            }

            bool traitAdded;

            if (!isProperty) {
                // We don't do this for properties because they are held in a temporary table
                // and added to the global trait table only after all the traits of the script
                // have been created.

                traitAdded = m_domain.tryDefineGlobalTrait(trait, m_options.hideParentDomainDefinitions);
                if (traitAdded) {
                    scriptData.traits.add(trait);

                    int slotId = traitInfo.slotId;
                    if (slotId > 0) {
                        // Store in the slot map.
                        if (!scriptData.slotMap.tryAddSlot(slotId, trait))
                            throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_SLOT_ID_ALREADY_TAKEN, slotId, "global");
                    }
                }
                else if (m_options.failOnGlobalNameConflict) {
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, traitInfo.name);
                }
            }

            return trait;
        }

        /// <summary>
        /// Creates the traits of a class if it belongs to this context.
        /// </summary>
        /// <param name="klass">The class whose traits to create.</param>
        private void _createClassTraitsIfInContext(Class klass) {
            var scriptClass = klass.getClassImpl() as ScriptClass;
            if (scriptClass == null || scriptClass.abcClassInfo == null)
                return;

            ClassData classData = m_classData[scriptClass];
            if (classData == null || classData.compileState == ClassCompileState.TRAITS_CREATED)
                return;

            classData.compileState = ClassCompileState.TRAITS_CREATING;

            if (scriptClass.parent != null)
                _createClassTraitsIfInContext(scriptClass.parent);

            var ifaces = scriptClass.getImplementedInterfaces();
            for (int i = 0; i < ifaces.length; i++)
                _createClassTraitsIfInContext(ifaces[i]);

            if (!scriptClass.isInterface) {
                var instanceInitMethodInfo = scriptClass.abcClassInfo.instanceInitMethod;
                var instanceCtor = new ScriptClassConstructor(instanceInitMethodInfo, scriptClass);
                _setMethodForMethodInfo(instanceInitMethodInfo, instanceCtor);
                _createConstructorSig(instanceCtor);
                scriptClass.setConstructor(instanceCtor);
            }

            var staticInitMethod = new ScriptMethod(
                methodInfo: scriptClass.abcClassInfo.staticInitMethod,
                name: QName.publicName("{staticinit}"),
                declClass: scriptClass,
                domain: m_domain,
                isStatic: true,
                isFinal: true,
                isOverride: false,
                metadata: null
            );

            m_methodTraitData[staticInitMethod] = new MethodTraitData();
            _createMethodTraitSig(staticInitMethod);

            if (staticInitMethod.requiredParamCount > 0)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_METHOD_NO_REQUIRED_PARAMS_ALLOWED, staticInitMethod.name);

            classData.staticInit = staticInitMethod;

            var instTraitInfo = scriptClass.abcClassInfo.getInstanceTraits();
            var staticTraitInfo = scriptClass.abcClassInfo.getStaticTraits();

            for (int i = 0; i < instTraitInfo.length; i++)
                _createClassTraitFromTraitInfo(instTraitInfo[i], scriptClass, false);

            for (int i = 0; i < staticTraitInfo.length; i++)
                _createClassTraitFromTraitInfo(staticTraitInfo[i], scriptClass, true);

            foreach (ScriptProperty prop in m_tempPropsInst.Values) {
                bool propAdded = scriptClass.tryDefineTrait(prop);
                if (!propAdded)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, _createErrMsgTraitName(prop));
            }

            foreach (ScriptProperty prop in m_tempPropsStatic.Values) {
                bool propAdded = scriptClass.tryDefineTrait(prop);
                if (!propAdded)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, _createErrMsgTraitName(prop));
            }

            m_tempPropsInst.Clear();
            m_tempPropsStatic.Clear();

            if (!scriptClass.isInterface) {
                _checkMethodOverrides(scriptClass);

                for (int i = 0; i < ifaces.length; i++) {
                    // We only need to check the implementation of interfaces that are not implemented
                    // by the base class.
                    if (!scriptClass.parent.canAssignTo(ifaces[i]))
                        _checkInterfaceImplementation(scriptClass, ifaces[i]);
                }
            }

            classData.compileState = ClassCompileState.TRAITS_CREATED;
        }

        /// <summary>
        /// Creates a class trait from the given <see cref="ABCTraitInfo"/> instance.
        /// </summary>
        /// <returns>The <see cref="Trait"/> instance representing the trait defined on the class.</returns>
        /// <param name="traitInfo">A <see cref="ABCTraitInfo"/> instance representing the trait to define.</param>
        /// <param name="declClass">The class in which the trait is to be defined.</param>
        /// <param name="isStatic">Set this to true if the trait is a static trait, false for an instance trait.</param>
        private Trait _createClassTraitFromTraitInfo(
            ABCTraitInfo traitInfo, ScriptClass declClass, bool isStatic)
        {
            if (traitInfo.kind == ABCTraitFlags.Function)
                // Not sure where this is used, don't support it for now.
                throw ErrorHelper.createError(ErrorCode.INVALID_TRAIT_KIND, "Function");

            Trait trait = null;
            bool isProperty = false;

            if (traitInfo.kind == ABCTraitFlags.Class) {
                if (!isStatic)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_INSTANCE_CLASS_TRAIT, declClass.name);
                ABCClassInfo classInfo = traitInfo.classInfo;
                trait = new ClassAlias(
                    classInfo.name, declClass, m_domain, m_abcClassesByIndex[classInfo.abcIndex], traitInfo.metadata);
            }
            else if (traitInfo.kind == ABCTraitFlags.Slot || traitInfo.kind == ABCTraitFlags.Const) {
                trait = _createFieldTrait(traitInfo, declClass, isStatic);
            }
            else if (traitInfo.kind == ABCTraitFlags.Method) {
                ScriptMethod method = new ScriptMethod(traitInfo, declClass, m_domain, isStatic);
                trait = method;
                m_methodTraitData[method] = new MethodTraitData();
                _createMethodTraitSig(method);
            }
            else if (traitInfo.kind == ABCTraitFlags.Getter || traitInfo.kind == ABCTraitFlags.Setter) {
                isProperty = true;
                ScriptMethod method = _definePropertyAccessor(traitInfo, declClass, isStatic);
                trait = method;
                _createMethodTraitSig(method);
            }

            if (!isProperty) {
                bool traitDefined = declClass.tryDefineTrait(trait);
                if (!traitDefined)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, traitInfo.name.ToString());
            }

            int slotId = traitInfo.slotId;
            if (slotId > 0)
                declClass.defineTraitSlot(trait, slotId);

            int dispId = traitInfo.methodDispId;
            if (dispId > 0)
                declClass.defineMethodDispId((ScriptMethod)trait, dispId);

            return trait;
        }

        /// <summary>
        /// Creates a field trait.
        /// </summary>
        /// <param name="traitInfo">A <see cref="ABCTraitInfo"/> instance representing the trait_info
        /// in the ABC file from which the field was created.</param>
        /// <param name="declClass">The class declaring the field, or null for a global field.</param>
        /// <param name="isStatic">True for a static or global field, false for instance fields.</param>
        private ScriptField _createFieldTrait(ABCTraitInfo traitInfo, Class declClass, bool isStatic) {
            Class fieldType = getClassByMultiname(traitInfo.fieldTypeName, allowAny: true);
            var field = new ScriptField(traitInfo, declClass, m_domain, isStatic, fieldType);

            if (traitInfo.fieldHasDefault) {
                ASAny defaultVal = _coerceDefaultValue(traitInfo.fieldDefaultValue, fieldType);
                m_fieldDefaultValues[field] = defaultVal;
            }

            return field;
        }

        /// <summary>
        /// Creates the signature of a method.
        /// </summary>
        /// <param name="method">A <see cref="ScriptMethod"/> instance.</param>
        /// <param name="hasScopedReceiver">Set to true to add a <see cref="ScopedClosureReceiver"/>
        /// as the first argument of the method. This is used for functions created by the newfunction
        /// instruction.</param>
        private void _createMethodTraitSig(ScriptMethod method, bool hasScopedReceiver = false) {
            _createMethodOrConstructorSig(
                method,
                method.abcMethodInfo,
                method.declaringClass,
                method.isStatic,
                method.name,
                method.isOverride,
                hasScopedReceiver
            );
        }

        /// <summary>
        /// Creates the signature of a constructor.
        /// </summary>
        /// <param name="ctor">A <see cref="ScriptClassConstructor"/> instance.</param>
        private void _createConstructorSig(ScriptClassConstructor ctor) {
            _createMethodOrConstructorSig(
                ctor,
                ctor.abcMethodInfo,
                ctor.declaringClass,
                isStatic: false,
                QName.publicName("constructor"),
                isOverride: false,
                hasScopedReceiver: false
            );
        }

        /// <summary>
        /// Creates the signature of a method or constructor.
        /// </summary>
        /// <param name="methodOrCtor">A <see cref="ScriptMethod"/> or
        /// <see cref="ScriptClassConstructor"/> instance.</param>
        /// <param name="methodInfo">The <see cref="ABCMethodInfo"/> instance representing the method_info
        /// structure in the ABC file from which the method or constructor was created.</param>
        /// <param name="declClass">The class declaring the method or constructor; null for a global method.</param>
        /// <param name="isStatic">Set to true for a static or global method.</param>
        /// <param name="methodName">The qualified name of the method.</param>
        /// <param name="isOverride">Set to true if the method is an override of a base class method.</param>
        /// <param name="hasScopedReceiver">Set to true to add a <see cref="ScopedClosureReceiver"/> as the
        /// first parameter of the method. This is used for functions created with the newfunction
        /// instruction.</param>
        private void _createMethodOrConstructorSig(
            object methodOrCtor,
            ABCMethodInfo methodInfo,
            Class declClass,
            bool isStatic,
            in QName methodName,
            bool isOverride,
            bool hasScopedReceiver)
        {
            bool mustHaveBody = isStatic || !declClass.isInterface;
            ABCMethodBodyInfo methodBody = getMethodBodyInfo(methodInfo);

            if (mustHaveBody && methodBody == null) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_METHOD_MISSING_BODY, _createErrMsgTraitName(methodOrCtor));
            }

            if (!mustHaveBody && methodBody != null) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_METHOD_NO_BODY_ALLOWED, _createErrMsgTraitName(methodOrCtor));
            }

            if (isOverride && (isStatic || declClass.isInterface)) {
                // The override flag cannot be used in these cases.
                throw ErrorHelper.createError(
                    ErrorCode.ILLEGAL_OVERRIDE, methodName.ToString(), _createErrMsgTraitName(methodOrCtor));
            }

            // Resolve return type.

            bool hasReturn;
            Class returnType;

            if (_isVoidName(methodInfo.returnTypeName)) {
                hasReturn = false;
                returnType = null;
            }
            else {
                hasReturn = true;
                returnType = getClassByMultiname(methodInfo.returnTypeName, allowAny: true);
            }

            // Resolve parameters.

            bool hasRest = false;
            if ((methodInfo.flags & (ABCMethodFlags.NEED_REST | ABCMethodFlags.NEED_ARGUMENTS)) != 0)
                hasRest = true;

            int paramCount = methodInfo.paramCount;
            if (hasScopedReceiver)
                paramCount++;

            var parameters = (paramCount != 0) ? new MethodTraitParameter[paramCount] : Array.Empty<MethodTraitParameter>();

            Span<MethodTraitParameter> paramsSpan = parameters;

            if (hasScopedReceiver) {
                paramsSpan[0] = new MethodTraitParameter(
                    "$recv", Class.fromType(typeof(ScopedClosureReceiver)), false, false, default);
                paramsSpan = paramsSpan.Slice(1);
            }

            for (int i = 0, n = methodInfo.paramCount; i < n; i++) {
                ABCMultiname typeName = methodInfo.getParamTypeName(i);
                Class paramType = getClassByMultiname(typeName, allowAny: true);

                bool isOptional = methodInfo.isParamOptional(i);
                ASAny defaultVal = default(ASAny);
                if (isOptional)
                    defaultVal = _coerceDefaultValue(methodInfo.getParamDefaultValue(i), paramType);

                paramsSpan[i] = new MethodTraitParameter(
                    methodInfo.getParamName(i), paramType, isOptional, isOptional, defaultVal);
            }

            if (methodOrCtor is ScriptMethod method)
                method.setSignature(hasReturn, returnType, parameters, hasRest);
            else if (methodOrCtor is ScriptClassConstructor ctor)
                ctor.setSignature(parameters, hasRest);
        }

        /// <summary>
        /// Coerces a field or parameter default value to the given type.
        /// </summary>
        /// <returns>The coerced value.</returns>
        /// <param name="val">The value to coerce.</param>
        /// <param name="type">The type to coerce the value to.</param>
        private static ASAny _coerceDefaultValue(ASAny val, Class type) {
            // Only a limited number of conversions are allowed in this context:
            // Undefined to null, undefined/null to numeric types, conversion between
            // numeric types, and conversion to the Object or "any" type. All other
            // conversions are not valid.

            if (val.value == null) {
                if (type == null)
                    return val;
                if (type.tag == ClassTag.INT)
                    return 0;
                if (type.tag == ClassTag.UINT)
                    return 0u;
                if (type.tag == ClassTag.NUMBER)
                    return val.isDefined ? 0.0 : Double.NaN;
                return ASAny.@null;
            }
            else if (ASObject.AS_isNumeric(val.value)) {
                if (type == null)
                    return val.value;
                if (type.tag == ClassTag.INT)
                    return (val.value is ASint) ? val : (ASAny)(int)val;
                if (type.tag == ClassTag.UINT)
                    return (val.value is ASuint) ? val : (ASAny)(uint)val;
                if (type.tag == ClassTag.NUMBER)
                    return (val.value is ASNumber) ? val : (ASAny)(double)val;
                throw ErrorHelper.createError(ErrorCode.ILLEGAL_DEFAULT_VALUE, type.name);
            }
            else {
                // In all other cases, the types must have an exact match, or the field
                // type must be the "any" or Object type.
                if (type != null && type.underlyingType != typeof(ASObject) && val.AS_class != type)
                    throw ErrorHelper.createError(ErrorCode.ILLEGAL_DEFAULT_VALUE, type.name);
                return val;
            }
        }

        /// <summary>
        /// Checks any overridden of methods in <paramref name="klass"/> and ensures that the
        /// overrides are legal. This method also stores the base methods of overrides in
        /// the MethodTraitData table entries.
        /// </summary>
        /// <param name="klass">The class whose method overrides to check.</param>
        private void _checkMethodOverrides(ScriptClass klass) {
            Class parent = klass.parent;
            var declTraits = klass.getTraits(TraitType.ALL, TraitScope.DECLARED);

            Trait getOverriddenTraitOnParent(in QName traitName) {
                var trait = parent.getTrait(traitName);
                if (trait != null)
                    return trait;

                if (traitName.ns != klass.abcClassInfo.protectedNamespace)
                    return null;

                // If the trait name is in the derived class's protected namespace, check in the protected
                // namespace(s) of base classes. Since nothing other than a ScriptClass can derive from a
                // ScriptClass, we can stop when the inheritance chain reaches a non-ScriptClass.

                var curClass = parent as ScriptClass;
                while (curClass != null) {
                    QName traitNameInParentNs = new QName(curClass.abcClassInfo.protectedNamespace, traitName.localName);
                    trait = curClass.getTrait(traitNameInParentNs);
                    if (trait != null)
                        return trait;
                    curClass = curClass.parent as ScriptClass;
                }

                return null;
            }

            for (int i = 0; i < declTraits.length; i++) {
                if (declTraits[i] is ScriptMethod method && method.declaringClass == klass && method.isOverride) {
                    var baseMethod = getOverriddenTraitOnParent(method.name) as MethodTrait;
                    if (!_checkOverrideSignature(method, baseMethod)) {
                        throw ErrorHelper.createError(
                            ErrorCode.ILLEGAL_OVERRIDE, method.name.ToString(), klass.name.ToString());
                    }

                    m_methodTraitData[method].overrideMethodDef = baseMethod;
                }
                else if (declTraits[i] is ScriptProperty prop) {
                    var baseProp = getOverriddenTraitOnParent(prop.name) as PropertyTrait;
                    var getter = prop.getter as ScriptMethod;
                    var setter = prop.setter as ScriptMethod;

                    if (getter?.declaringClass == klass && getter.isOverride) {
                        if (baseProp == null || !_checkOverrideSignature(getter, baseProp.getter)) {
                            throw ErrorHelper.createError(
                                ErrorCode.ILLEGAL_OVERRIDE, prop.name.ToString(), klass.name.ToString());
                        }
                        m_methodTraitData[getter].overrideMethodDef = baseProp.getter;
                    }

                    if (setter?.declaringClass == klass && setter.isOverride) {
                        if (baseProp == null || !_checkOverrideSignature(setter, baseProp.setter)) {
                            throw ErrorHelper.createError(
                                ErrorCode.ILLEGAL_OVERRIDE, prop.name.ToString(), klass.name.ToString());
                        }
                        m_methodTraitData[setter].overrideMethodDef = baseProp.setter;
                    }
                }
            }
        }

        /// <summary>
        /// Checks the implementation of the interface <paramref name="iface"/> by the class
        /// <paramref name="klass"/> and ensures that all methods of the interface are properly
        /// implemented. This method also stores the interface method definitions being implemented
        /// in the MethodTraitData table entries of the class methods. Only declared interface methods
        /// are checked; inherited methods are not checked.
        /// </summary>
        /// <param name="klass">The class.</param>
        /// <param name="iface">The interface whose implementation in <paramref name="klass"/>
        /// must be checked.</param>
        private void _checkInterfaceImplementation(ScriptClass klass, Class iface) {
            var ifaceTraits = iface.getTraits(TraitType.ALL, TraitScope.INSTANCE_DECLARED);

            for (int i = 0; i < ifaceTraits.length; i++) {
                Trait ifaceTrait = ifaceTraits[i];
                Trait classTrait = klass.getTrait(ifaceTrait.name);

                if (classTrait == null && !ifaceTrait.name.ns.isPublic)
                    // If no implementation is found in the namespace of the interface
                    // trait's name, check in the public namespace.
                    classTrait = klass.getTrait(QName.publicName(ifaceTrait.name.localName));

                if (classTrait == null || classTrait.traitType != ifaceTrait.traitType) {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__ABC_INTERFACE_METHOD_NOT_IMPLEMENTED,
                        _createErrMsgTraitName(ifaceTrait), klass.name.ToString());
                }

                if (classTrait is MethodTrait method) {
                    MethodTrait ifaceMethod = (MethodTrait)ifaceTrait;
                    if (!_checkOverrideSignature(method, ifaceMethod)) {
                        throw ErrorHelper.createError(
                            ErrorCode.ILLEGAL_OVERRIDE, method.name.ToString(), klass.name.ToString());
                    }
                    _addInterfaceMethodImpl(method, ifaceMethod, klass);
                }
                else if (classTrait is PropertyTrait prop) {
                    PropertyTrait ifaceProp = (PropertyTrait)ifaceTrait;

                    if (ifaceProp.getter != null) {
                        if (prop.getter == null || !_checkOverrideSignature(prop.getter, ifaceProp.getter)) {
                            throw ErrorHelper.createError(
                                ErrorCode.ILLEGAL_OVERRIDE, prop.name.ToString(), klass.name.ToString());
                        }
                        _addInterfaceMethodImpl(prop.getter, ifaceProp.getter, klass);
                    }

                    if (ifaceProp.setter != null) {
                        if (prop.setter == null || !_checkOverrideSignature(prop.setter, ifaceProp.setter)) {
                            throw ErrorHelper.createError(
                                ErrorCode.ILLEGAL_OVERRIDE, prop.name.ToString(), klass.name.ToString());
                        }
                        _addInterfaceMethodImpl(prop.setter, ifaceProp.setter, klass);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if <paramref name="method"/> can override or implement <paramref name="baseMethod"/>.
        /// </summary>
        /// <returns>True if <paramref name="method"/> is allowed to override <paramref name="baseMethod"/>,
        /// otherwise false.</returns>
        /// <param name="method">The method whose signature to check.</param>
        /// <param name="baseMethod">The base class method that <paramref name="method"/> overrides,
        /// or the interface method that it implements.</param>
        private bool _checkOverrideSignature(MethodTrait method, MethodTrait baseMethod) {
            bool hasRestArg(MethodTrait m) =>
                m is ScriptMethod sm ? (sm.abcMethodInfo.flags & ABCMethodFlags.NEED_REST) != 0 : m.hasRest;

            if (baseMethod == null
                || baseMethod.isFinal
                || method.hasReturn != baseMethod.hasReturn
                || method.returnType != baseMethod.returnType
                || hasRestArg(method) != hasRestArg(baseMethod))
            {
                return false;
            }

            var params1 = method.getParameters();
            var params2 = baseMethod.getParameters();

            if (params1.length != params2.length)
                return false;

            for (int i = 0; i < params1.length; i++) {
                var p1 = params1[i];
                var p2 = params2[i];
                if (p1.type != p2.type || p1.isOptional != p2.isOptional || p1.hasDefault != p2.hasDefault)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Adds an interface implementation to the MethodTraitData table entries.
        /// </summary>
        /// <param name="implMethod">The class method that implements the interface method.</param>
        /// <param name="ifaceMethod">The interface method implemented by <paramref name="implMethod"/>.</param>
        /// <param name="implByClass">The class that implements the interface that declares
        /// <paramref name="ifaceMethod"/> and declares or inherits <paramref name="implMethod"/>.</param>
        private void _addInterfaceMethodImpl(MethodTrait implMethod, MethodTrait ifaceMethod, ScriptClass implByClass) {
            ref MethodTraitData methodData = ref m_methodTraitData.getValueRef(implMethod, true);
            if (methodData == null)
                methodData = new MethodTraitData();

            methodData.interfaceMethodImpls.add(new InterfaceMethodImpl(ifaceMethod, implByClass));
        }

        /// <summary>
        /// Emits all global and class traits into the dynamic assembly.
        /// </summary>
        private void _emitScriptAndClassMembers() {
            var scriptInfo = m_abcFile.getScriptInfo();

            for (int i = 0; i < scriptInfo.length; i++) {
                ScriptData scriptData = m_abcScriptDataByIndex[i];
                _emitMethodBuilder(scriptData.initMethod, m_globalTraitsContainer, scriptData.initMethod.name, MethodNameMangleMode.NONE);

                for (int j = 0, n = scriptData.traits.length; j < n; j++)
                    _emitTraitIntoTypeBuilder(scriptData.traits[j], null, m_globalTraitsContainer);
            }

            foreach (var p in m_classData)
                _emitClassMembersIfInContext(p.Key);
        }

        /// <summary>
        /// Emits the class members for the given class if it is a part of the current
        /// compilation context.
        /// </summary>
        /// <param name="klass">The class for which to emit the class members.</param>
        private void _emitClassMembersIfInContext(Class klass) {
            var scriptClass = klass.getClassImpl() as ScriptClass;
            if (scriptClass == null || scriptClass.abcClassInfo == null)
                return;

            ClassData classData = m_classData[scriptClass];
            if (classData == null || classData.compileState == ClassCompileState.TRAITS_EMITTED)
                return;

            if (scriptClass.parent != null)
                _emitClassMembersIfInContext(scriptClass.parent);

            var ifaces = scriptClass.getImplementedInterfaces();
            for (int i = 0; i < ifaces.length; i++)
                _emitClassMembersIfInContext(ifaces[i]);

            TypeBuilder typeBuilder = classData.typeBuilder;

            if (!scriptClass.isInterface)
                _emitConstructorBuilder((ScriptClassConstructor)scriptClass.constructor, typeBuilder);

            _emitMethodBuilder(classData.staticInit, typeBuilder, classData.staticInit.name, MethodNameMangleMode.NONE);

            var traits = scriptClass.getTraits(TraitType.ALL, TraitScope.DECLARED);
            for (int i = 0; i < traits.length; i++)
                _emitTraitIntoTypeBuilder(traits[i], scriptClass, typeBuilder);

            classData.compileState = ClassCompileState.TRAITS_EMITTED;
        }

        /// <summary>
        /// Emits a field or method corresponding to the given trait into a TypeBuilder.
        /// </summary>
        /// <param name="trait">The trait to emit. This must be a trait created in this context.</param>
        /// <param name="declClass">The class declaring this trait, or null for a global trait.</param>
        /// <param name="typeBuilder">The <see cref="TypeBuilder"/> into which to emit the field or method
        /// definition.</param>
        private void _emitTraitIntoTypeBuilder(Trait trait, ScriptClass declClass, TypeBuilder typeBuilder) {
            if (trait is ScriptField field) {
                _emitFieldBuilder(field, typeBuilder, true);
            }
            else if (trait is ScriptMethod method) {
                _emitMethodBuilder(method, typeBuilder, method.name, MethodNameMangleMode.METHOD);
            }
            else if (trait is ScriptProperty prop) {
                var getter = prop.getter as ScriptMethod;
                var setter = prop.setter as ScriptMethod;

                if (getter?.declaringClass == declClass)
                    _emitMethodBuilder(getter, typeBuilder, prop.name, MethodNameMangleMode.GETTER);
                if (setter?.declaringClass == declClass)
                    _emitMethodBuilder(setter, typeBuilder, prop.name, MethodNameMangleMode.SETTER);

                if (m_options.emitPropertyDefinitions)
                    _emitPropertyBuilder(prop, typeBuilder);
            }
        }

        /// <summary>
        /// Creates a FieldBuilder for the given field trait in the dynamic assembly.
        /// </summary>
        /// <param name="field">A <see cref="ScriptField"/> instance for which to emit a field
        /// into the given TypeBuilder.</param>
        /// <param name="typeBuilder">The <see cref="TypeBuilder"/> into which to emit the field.</param>
        /// <param name="mangleName">Set this to true if the field's name must be mangled.</param>
        private void _emitFieldBuilder(ScriptField field, TypeBuilder typeBuilder, bool mangleName) {
            string fieldBldrName = mangleName ? m_nameMangler.createName(field.name) : field.name.ToString();
            FieldAttributes fieldBldrAttrs = FieldAttributes.Public;
            if (field.isStatic)
                fieldBldrAttrs |= FieldAttributes.Static;

            var fieldBuilder = typeBuilder.defineField(fieldBldrName, getTypeSignature(field.fieldType), fieldBldrAttrs);
            m_traitEntityHandles[field] = fieldBuilder.handle;
        }

        /// <summary>
        /// Returns a <see cref="TypeSignature"/> representing the return type of the given method.
        /// </summary>
        /// <param name="method">The method whose return type signature is to be obtained</param>
        /// <returns>The return type signature.</returns>
        private TypeSignature _getReturnTypeSignature(MethodTrait method) =>
            method.hasReturn ? getTypeSignature(method.returnType) : TypeSignature.forPrimitiveType(PrimitiveTypeCode.Void);

        /// <summary>
        /// Returns an array of <see cref="TypeSignature"/> instances representing the parameter
        /// types of the given method. This includes the rest parameter, if the method defines it.
        /// </summary>
        /// <returns>The array of parameter type signatures.</returns>
        /// <param name="method">The method whose parameter types are to be obtained.</param>
        private TypeSignature[] _getParamTypeSignatures(MethodTrait method) {
            int paramCount = method.paramCount;
            if (method.hasRest)
                paramCount++;

            if (paramCount == 0)
                return Array.Empty<TypeSignature>();

            var types = new TypeSignature[paramCount];
            var parameters = method.getParameters();

            for (int i = 0; i < parameters.length; i++)
                types[i] = getTypeSignature(parameters[i].type);
            if (method.hasRest)
                types[paramCount - 1] = m_assemblyBuilder.metadataContext.getTypeSignature(typeof(RestParam));

            return types;
        }

        /// <summary>
        /// Creates a MethodBuilder for the given method trait in the dynamic assembly.
        /// </summary>
        /// <param name="method">A <see cref="ScriptMethod"/> instance for which to emit a
        /// method into the given TypeBuilder.</param>
        /// <param name="typeBuilder">The <see cref="TypeBuilder"/> into which to emit the method.</param>
        /// <param name="name">The name to be set as the name of the emitted method.</param>
        /// <param name="nameMangleMode">Controls name mangling of <paramref name="name"/>.</param>
        private void _emitMethodBuilder(
            ScriptMethod method,
            TypeBuilder typeBuilder,
            in QName name,
            MethodNameMangleMode nameMangleMode = MethodNameMangleMode.METHOD
        ) {
            string methodBldrName;
            switch (nameMangleMode) {
                case MethodNameMangleMode.METHOD:
                    methodBldrName = m_nameMangler.createName(name);
                    break;
                case MethodNameMangleMode.GETTER:
                    methodBldrName = m_nameMangler.createGetterName(name);
                    break;
                case MethodNameMangleMode.SETTER:
                    methodBldrName = m_nameMangler.createSetterName(name);
                    break;
                default:
                    methodBldrName = name.ToString();
                    break;
            }

            MethodTraitData methodData = m_methodTraitData[method];

            TypeSignature methodBldrReturnType = _getReturnTypeSignature(method);
            TypeSignature[] methodBldrParamTypes = _getParamTypeSignatures(method);

            MethodAttributes methodBldrAttrs = MethodAttributes.Public | MethodAttributes.HideBySig;

            if (method.isStatic) {
                methodBldrAttrs |= MethodAttributes.Static;
            }
            else if (!method.isFinal
                || method.declaringClass.isInterface
                || methodData.overrideMethodDef != null
                || methodData.interfaceMethodImpls.length > 0)
            {
                methodBldrAttrs |= MethodAttributes.Virtual;

                if (methodData.overrideMethodDef == null)
                    methodBldrAttrs |= MethodAttributes.NewSlot;

                if (method.isFinal)
                    methodBldrAttrs |= MethodAttributes.Final;
                else if (method.declaringClass.isInterface)
                    methodBldrAttrs |= MethodAttributes.Abstract | MethodAttributes.NewSlot;
            }

            MethodBuilder methodBldr = typeBuilder.defineMethod(
                methodBldrName, methodBldrAttrs, methodBldrReturnType, methodBldrParamTypes);

            methodData.methodBuilder = methodBldr;
            m_traitEntityHandles[method] = methodBldr.handle;

            // Emit parameter names if that option is set.
            if (m_options.emitParamNames) {
                var methodParams = method.getParameters();
                for (int i = 0; i < methodParams.length; i++) {
                    string paramName = methodParams[i].name;
                    if (paramName == null || paramName.Length == 0)
                        paramName = "param" + ASint.AS_convertString(i);

                    methodBldr.defineParameter(i, paramName);
                }

                if (method.hasRest)
                    methodBldr.defineParameter(methodBldrParamTypes.Length - 1, "$rest");
            }
        }

        /// <summary>
        /// Creates a MethodBuilder for the given constructor in the dynamic assembly.
        /// </summary>
        /// <param name="ctor">A <see cref="ScriptClassConstructor"/> instance for which to emit a
        /// constructor into the given TypeBuilder.</param>
        /// <param name="typeBuilder">The <see cref="TypeBuilder"/> into which to emit the constructor.</param>
        private void _emitConstructorBuilder(ScriptClassConstructor ctor, TypeBuilder typeBuilder) {
            TypeSignature[] ctorBldrParams = Array.Empty<TypeSignature>();
            int paramCount = ctor.paramCount + (ctor.hasRest ? 1 : 0);

            if (paramCount > 0) {
                ctorBldrParams = new TypeSignature[paramCount];
                var parameters = ctor.getParameters();
                for (int i = 0; i < parameters.length; i++)
                    ctorBldrParams[i] = getTypeSignature(parameters[i].type);
                if (ctor.hasRest)
                    ctorBldrParams[paramCount - 1] = m_assemblyBuilder.metadataContext.getTypeSignature(typeof(RestParam));
            }

            MethodAttributes ctorBldrAttrs = MethodAttributes.Public | MethodAttributes.HideBySig;
            MethodBuilder ctorBldr = typeBuilder.defineConstructor(ctorBldrAttrs, ctorBldrParams);

            m_classData[(ScriptClass)ctor.declaringClass].ctorBuilder = ctorBldr;

            if (m_options.emitParamNames) {
                for (int i = 0, n = ctor.paramCount; i < n; i++) {
                    ctorBldr.defineParameter(
                        i, ctor.abcMethodInfo.getParamName(i) ?? "param" + ASint.AS_convertString(i));
                }
                if (ctor.hasRest)
                    ctorBldr.defineParameter(paramCount - 1, "rest");
            }
        }


        /// <summary>
        /// Creates a PropertyBuilder for the given property in the dynamic module, if the
        /// appropriate options are set and the property accessor signatures are valid.
        /// </summary>
        /// <param name="prop">A <see cref="ScriptProperty"/> instance for which to emit a
        /// property into the given TypeBuilder.</param>
        /// <param name="typeBuilder">The <see cref="TypeBuilder"/> into which to emit the property.</param>
        private void _emitPropertyBuilder(ScriptProperty prop, TypeBuilder typeBuilder) {
            if (!m_options.emitPropertyDefinitions)
                return;

            MethodTrait getter = prop.getter;
            MethodTrait setter = prop.setter;

            if ((getter == null || getter.declaringClass != prop.declaringClass)
                && (setter == null || setter.declaringClass != prop.declaringClass))
            {
                return;
            }

            if (getter != null && (!getter.hasReturn || getter.hasRest || getter.paramCount > 0))
                return;
            if (setter != null && (setter.hasReturn || setter.hasRest || setter.paramCount != 1))
                return;
            if (getter != null && setter != null && getter.returnType != setter.getParameters()[0].type)
                return;

            var propBuilder = typeBuilder.defineProperty(
                m_nameMangler.createName(prop.name),
                PropertyAttributes.None,
                prop.isStatic,
                getTypeSignature(prop.propertyType)
            );

            if (getter?.declaringClass == prop.declaringClass)
                propBuilder.setGetMethod(m_methodTraitData[getter].methodBuilder);
            if (setter?.declaringClass == prop.declaringClass)
                propBuilder.setSetMethod(m_methodTraitData[setter].methodBuilder);
        }

        /// <summary>
        /// Checks the MethodTraitData table for overrides and interface implementations and emits
        /// method override directives into the dynamic assembly where neccessary.
        /// </summary>
        private void _emitMethodOverrides() {
            foreach (var entry in m_methodTraitData) {
                MethodTrait method = entry.Key;
                var declClass = method.declaringClass as ScriptClass;

                if (entry.Value.overrideMethodDef != null)
                    _emitMethodImplIfNeeded(method, entry.Value.overrideMethodDef, declClass);

                var interfaceImpls = entry.Value.interfaceMethodImpls.asSpan();

                for (int i = 0; i < interfaceImpls.Length; i++) {
                    InterfaceMethodImpl impl = interfaceImpls[i];
                    _emitMethodImplIfNeeded(method, impl.methodDef, impl.implByClass);
                }
            }
        }

        private void _emitMethodImplIfNeeded(MethodTrait method, MethodTrait baseMethod, ScriptClass implByClass) {
            bool areNamesDifferent = _getUnderlyingMethodName(method) != _getUnderlyingMethodName(baseMethod);

            bool isStubNeeded =
                implByClass != method.declaringClass
                || method.hasRest != baseMethod.hasRest
                || (_getUnderlyingMethodAttrs(method) & MethodAttributes.Virtual) == 0;

            if (!areNamesDifferent && !isStubNeeded)
                return;

            var implTypeBuilder = m_classData[implByClass].typeBuilder;

            if (!isStubNeeded) {
                // We can define the MethodImpl directly, no need to emit a stub method.
                implTypeBuilder.defineMethodImpl(getEntityHandle(baseMethod), getEntityHandle(method));
                return;
            }

            TypeSignature retType = _getReturnTypeSignature(baseMethod);
            TypeSignature[] paramTypes = _getParamTypeSignatures(baseMethod);

            string stubMethodName = m_nameMangler.createMethodImplStubName(
                _getUnderlyingTypeName(baseMethod.declaringClass).ToString(),
                _getUnderlyingMethodName(baseMethod)
            );

            var stubMethodAttrs = MethodAttributes.Private | MethodAttributes.HideBySig
                | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;

            MethodBuilder stubMethodBldr = implTypeBuilder.defineMethod(stubMethodName, stubMethodAttrs, retType, paramTypes);
            implTypeBuilder.defineMethodImpl(getEntityHandle(baseMethod), stubMethodBldr.handle);

            var ilBuilder = m_contextILBuilder;

            ilBuilder.emit(ILOp.ldarg, 0);
            for (int j = 1; j <= baseMethod.paramCount; j++)
                ilBuilder.emit(ILOp.ldarg, j);

            // hasRest may not be the same in the declaration and implementation because the
            // compiler may add a rest parameter to a method when the NEED_ARGUMENTS flag is
            // set (and this flag is not part of the signature that is checked when overriding
            // or implementing interface methods).

            if (method.hasRest && baseMethod.hasRest) {
                ilBuilder.emit(ILOp.ldarg, baseMethod.paramCount + 1);
            }
            else if (method.hasRest) {
                // Implementation takes a rest argument, so pass an empty one.
                var emptyRestLocal = ilBuilder.declareLocal(typeof(RestParam));
                ilBuilder.emit(ILOp.ldloc, emptyRestLocal);
            }

            // If method declaration has a rest parameter but the implementation does not, the
            // rest argument is discarded.

            ilBuilder.emit(ILOp.callvirt, getEntityHandle(method));
            ilBuilder.emit(ILOp.ret);

            stubMethodBldr.setMethodBody(m_contextILBuilder.createMethodBody());
        }

        /// <summary>
        /// Returns the name of the underlying type (in assembly metadata) for the given class.
        /// </summary>
        /// <param name="klass">The class.</param>
        /// <returns>The name of the underlying type for the class in assembly metadata.</returns>
        private TypeName _getUnderlyingTypeName(Class klass) {
            Type underlyingType = klass.underlyingType;
            if (underlyingType != null)
                return new TypeName(underlyingType.Namespace, underlyingType.Name);

            if (klass.isVectorInstantiation) {
                underlyingType = typeof(ASVector<>);
                return new TypeName(underlyingType.Namespace, underlyingType.Name);
            }

            if (klass.getClassImpl() is ScriptClass scriptClass)
                return m_classData[scriptClass].typeBuilder.typeName;

            return null;
        }

        /// <summary>
        /// Returns the name of the underlying method (in assembly metadata) for the given method trait.
        /// </summary>
        /// <param name="trait">The method trait.</param>
        /// <returns>The name of the underlying method in assembly metadata.</returns>
        private string _getUnderlyingMethodName(MethodTrait trait) {
            if (trait.underlyingMethodInfo != null)
                return trait.underlyingMethodInfo.Name;

            var methodTraitData = m_methodTraitData[trait];
            if (methodTraitData != null && methodTraitData.methodBuilder != null)
                return methodTraitData.methodBuilder.name;

            return null;
        }

        /// <summary>
        /// Returns the attributes of the underlying method (in assembly metadata) for the given method trait.
        /// </summary>
        /// <param name="trait">The method trait.</param>
        /// <returns>The attributes of the underlying method in assembly metadata.</returns>
        private MethodAttributes _getUnderlyingMethodAttrs(MethodTrait trait) {
            if (trait.underlyingMethodInfo != null)
                return trait.underlyingMethodInfo.Attributes;

            var methodTraitData = m_methodTraitData[trait];
            return (methodTraitData?.methodBuilder != null) ? methodTraitData.methodBuilder.attributes : 0;
        }

        /// <summary>
        /// Returns a string name for a trait or constructor suitable for use in an error message.
        /// </summary>
        /// <returns>A string representing the name of <paramref name="traitOrCtor"/>.</returns>
        /// <param name="traitOrCtor">An instance of <see cref="Trait"/> or <see cref="ClassConstructor"/>.</param>
        private static string _createErrMsgTraitName(object traitOrCtor) {
            if (traitOrCtor is ClassConstructor ctor)
                return ctor.declaringClass.name.ToString() + "/constructor";

            Trait trait = (Trait)traitOrCtor;
            if (trait.declaringClass == null)
                return trait.name.ToString();

            return trait.declaringClass.name.ToString() + "/" + trait.name.ToString();
        }

        /// <summary>
        /// Gets the <see cref="AssemblyBuilder"/> representing the assembly to which the compiled
        /// code is being emitted.
        /// </summary>
        public AssemblyBuilder assemblyBuilder => m_assemblyBuilder;

         /// <summary>
        /// Gets the <see cref="ABCFile"/> instance representing the ABC file being compiled.
        /// </summary>
        public ABCFile abcFile => m_abcFile;

        /// <summary>
        /// Returns a <see cref="Trait"/> instance representing the global trait matching the given
        /// multiname.
        /// </summary>
        /// <returns>The global trait matching the given multiname, or null if no trait could be found.</returns>
        /// <param name="multiname">The multiname. This must not contain runtime arguments.</param>
        public Trait getGlobalTraitByMultiname(in ABCMultiname multiname) {
            if (!m_globalTraitsByMultiname.TryGetValue(multiname, out Trait t)) {
                t = resolve(multiname);
                m_globalTraitsByMultiname.Add(multiname, t);
            }
            return t;

            Trait resolve(in ABCMultiname mn) {
                if (mn.kind == ABCConstKind.GenericClassName) {
                    // Currently, Vector is the only generic type in the AVM2. There
                    // is no support for full generics.

                    ABCMultiname defName = m_abcFile.resolveMultiname(mn.genericDefIndex);
                    Trait defTrait = getGlobalTraitByMultiname(defName);

                    if (defTrait == null)
                        return null;

                    if (!(defTrait is Class defClass))
                        throw ErrorHelper.createError(ErrorCode.MARIANA__APPLYTYPE_NON_CLASS);
                    if (defClass.underlyingType != typeof(ASVector<>))
                        throw ErrorHelper.createError(ErrorCode.NONGENERIC_TYPE_APPLICATION, defClass.name);

                    var typeArgNames = m_abcFile.resolveGenericArgList(mn.genericArgListIndex);
                    if (typeArgNames.length != 1) {
                        throw ErrorHelper.createError(
                            ErrorCode.TYPE_ARGUMENT_COUNT_INCORRECT, defClass.name, 1, typeArgNames.length);
                    }

                    ref readonly ABCMultiname argName = ref typeArgNames[0];
                    if (m_abcFile.isAnyName(argName))
                        return Class.fromType<ASVectorAny>();  // This is the Vector.<*> type.

                    if (getGlobalTraitByMultiname(argName) is Class argClass)
                        return argClass.getVectorClass();

                    throw ErrorHelper.createError(ErrorCode.MARIANA__APPLYTYPE_NON_CLASS);
                }

                if (mn.hasRuntimeLocalName || mn.hasRuntimeNamespace)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_INVALID_RUNTIME_NAME);
                if (mn.isAttributeName)
                    return null;

                Trait trait;
                BindStatus status;

                string localName = m_abcFile.resolveString(mn.localNameIndex);
                if (localName == null)
                    return null;

                if (mn.kind == ABCConstKind.QName) {
                    QName qname = new QName(m_abcFile.resolveNamespace(mn.namespaceIndex), localName);
                    if (qname.ns.kind == NamespaceKind.ANY)
                        return null;
                    status = m_domain.lookupGlobalTrait(qname, false, out trait);
                }
                else {
                    // The only possible name kind at this point is Multiname.
                    NamespaceSet nsSet = m_abcFile.resolveNamespaceSet(mn.namespaceIndex);
                    if (nsSet.count == 0)
                        return null;
                    status = m_domain.lookupGlobalTrait(localName, nsSet, false, out trait);
                }

                if (status == BindStatus.NOT_FOUND)
                    return null;

                if (status == BindStatus.AMBIGUOUS)
                    throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, m_abcFile.multinameToString(mn));

                return trait;
            }
        }

        /// <summary>
        /// Returns a <see cref="Class"/> instance representing the class matching the given
        /// multiname. An exception is thrown if the class could not be found.
        /// </summary>
        /// <returns>The class matching the given multiname.</returns>
        /// <param name="multiname">The multiname. This must not contain runtime arguments.</param>
        /// <param name="allowAny">If this is set to true and <paramref name="multiname"/> represents
        /// the "any" name, this method returns null instead of throwing.</param>
        public Class getClassByMultiname(in ABCMultiname multiname, bool allowAny = false) {
            if (!m_classesByMultiname.TryGetValue(multiname, out Class c)) {
                c = resolve(multiname);
                m_classesByMultiname.Add(multiname, c);
            }

            if (c == null && !allowAny)
                throw ErrorHelper.createError(ErrorCode.CLASS_NOT_FOUND, m_abcFile.multinameToString(multiname));

            return c;

            Class resolve(in ABCMultiname mn) {
                if (m_abcFile.isAnyName(mn))
                    return null;

                if (getGlobalTraitByMultiname(mn) is Class klass)
                    return klass.getClassImpl();

                Debug.Assert(!mn.hasRuntimeLocalName && !mn.hasRuntimeNamespace);

                // Check for the class in the unexported class table of the ABC file.

                string localName = m_abcFile.resolveString(mn.localNameIndex);
                if (localName == null)
                    throw ErrorHelper.createError(ErrorCode.CLASS_NOT_FOUND, m_abcFile.multinameToString(mn));

                BindStatus status = BindStatus.NOT_FOUND;
                Trait trait = null;

                if (mn.kind == ABCConstKind.QName) {
                    QName qname = new QName(m_abcFile.resolveNamespace(mn.namespaceIndex), localName);
                    if (qname.ns.kind == NamespaceKind.ANY)
                        throw ErrorHelper.createError(ErrorCode.CLASS_NOT_FOUND, m_abcFile.multinameToString(mn));
                    status = m_unexportedClassTraits.tryGetTrait(qname, true, out trait);
                }
                else if (mn.kind == ABCConstKind.Multiname) {
                    NamespaceSet nsSet = m_abcFile.resolveNamespaceSet(mn.namespaceIndex);
                    if (nsSet.count == 0)
                        throw ErrorHelper.createError(ErrorCode.CLASS_NOT_FOUND, m_abcFile.multinameToString(mn));
                    status = m_unexportedClassTraits.tryGetTrait(localName, nsSet, true, out trait);
                }

                if (status == BindStatus.NOT_FOUND)
                    throw ErrorHelper.createError(ErrorCode.CLASS_NOT_FOUND, m_abcFile.multinameToString(mn));

                if (status == BindStatus.AMBIGUOUS)
                    throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, m_abcFile.multinameToString(mn));

                return (Class)trait;
            }
        }

        /// <summary>
        /// Returns a value indicating whether the given multiname represents the name
        /// of the void type.
        /// </summary>
        /// <returns>True if <paramref name="multiname"/> represents the void type name, otherwise false.</returns>
        /// <param name="multiname">A multiname.</param>
        private bool _isVoidName(ABCMultiname multiname) {
            if (multiname.kind == ABCConstKind.QName) {
                return m_abcFile.resolveString(multiname.localNameIndex) == "void"
                    && m_abcFile.resolveNamespace(multiname.namespaceIndex).isPublic;
            }
            else if (multiname.kind == ABCConstKind.Multiname) {
                return m_abcFile.resolveString(multiname.localNameIndex) == "void"
                    && m_abcFile.resolveNamespaceSet(multiname.namespaceIndex).containsPublic;
            }
            return false;
        }

        /// <summary>
        /// Returns the method body associated with the given method.
        /// </summary>
        /// <returns>A <see cref="ABCMethodBodyInfo"/> instance representing the method's body, or
        /// null if the method does not have a body.!----></returns>
        /// <param name="methodInfo">The <see cref="ABCMethodInfo"/> representing the method whose
        /// body is to be obtained.</param>
        public ABCMethodBodyInfo getMethodBodyInfo(ABCMethodInfo methodInfo) =>
            m_abcMethodInfoDataByIndex[methodInfo.abcIndex]?.body;

        /// <summary>
        /// Returns a list of all global traits declared by a script.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Trait}"/> instance containing the
        /// traits declared by the given script.</returns>
        /// <param name="scriptInfo">The <see cref="ABCScriptInfo"/> instance representing the
        /// script whose declared traits must be returned.</param>
        public ReadOnlyArrayView<Trait> getScriptTraits(ABCScriptInfo scriptInfo) =>
            m_abcScriptDataByIndex[scriptInfo.abcIndex].traits.asReadOnlyArrayView();

        /// <summary>
        /// Returns the global trait at the given slot index declared by a script.
        /// </summary>
        /// <returns>The global trait declared by the script represented by <paramref name="scriptInfo"/>
        /// at the slot index <paramref name="index"/>, or null of no trait is declared at that index.</returns>
        /// <param name="scriptInfo">The <see cref="ABCScriptInfo"/> instance representing the script
        /// that declared the trait.</param>
        /// <param name="index">The slot index for which to obtain the trait. This must be greater than zero.</param>
        public Trait getScriptTraitSlot(ABCScriptInfo scriptInfo, int index) {
            ScriptData scriptData = m_abcScriptDataByIndex[scriptInfo.abcIndex];
            return scriptData.slotMap.getSlot(index, true);
        }

        /// <summary>
        /// Returns the <see cref="ScriptMethod"/> or <see cref="ScriptClassConstructor"/> instance
        /// associated with the method_info entry in the ABC file at the given index.
        /// </summary>
        /// <param name="index">The index of the method_info entry in the ABC file being compiled.</param>
        /// <returns>The <see cref="ScriptMethod"/> or <see cref="ScriptClassConstructor"/> instance
        /// representing the method or constructor, or null if no method has been created yet.</returns>
        public object getMethodOrCtorForMethodInfo(int index) => m_abcMethodInfoDataByIndex[index]?.methodOrCtor;

        /// <summary>
        /// Returns the <see cref="ScriptMethod"/> or <see cref="ScriptClassConstructor"/> instance
        /// associated with a method_info entry in the ABC file.
        /// </summary>
        /// <param name="methodInfo">A <see cref="MethodInfo"/> instance representing the method_info entry
        /// in the ABC file being compiled.</param>
        /// <returns>The <see cref="ScriptMethod"/> or <see cref="ScriptClassConstructor"/> instance
        /// representing the method or constructor, or null if no method has been created yet.</returns>
        public object getMethodOrCtorForMethodInfo(ABCMethodInfo methodInfo) => getMethodOrCtorForMethodInfo(methodInfo.abcIndex);

        /// <summary>
        /// Associates a method or constructor with an <see cref="ABCMethodInfo"/> instance.
        /// </summary>
        /// <param name="mthdInfo">The <see cref="ABCMethodInfo"/> instance representing the method
        /// entry in the ABC file.</param>
        /// <param name="methodOrCtor">An instance of <see cref="ScriptMethod"/> or
        /// <see cref="ScriptClassConstructor"/>.</param>
        private void _setMethodForMethodInfo(ABCMethodInfo mthdInfo, object methodOrCtor) {
            MethodInfoData mthdInfoData = m_abcMethodInfoDataByIndex[mthdInfo.abcIndex];

            if (mthdInfoData == null) {
                mthdInfoData = new MethodInfoData();
                m_abcMethodInfoDataByIndex[mthdInfo.abcIndex] = mthdInfoData;
            }

            if (mthdInfoData.methodOrCtor != null) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_METHOD_INFO_ALREADY_ASSIGNED,
                    _createErrMsgTraitName(methodOrCtor),
                    _createErrMsgTraitName(mthdInfoData.methodOrCtor)
                );
            }

            mthdInfoData.methodOrCtor = methodOrCtor;
        }

        /// <summary>
        /// Returns the class that was created from the class_info at the given index in
        /// the ABC file.
        /// </summary>
        /// <returns>The <see cref="ScriptClass"/> instance representing the class created from
        /// the class_info in the ABC file at <paramref name="classInfoId"/>.</returns>
        /// <param name="classInfoId">The index of the class_info entry in the ABC file.</param>
        public ScriptClass getClassFromClassInfo(int classInfoId) => m_abcClassesByIndex[classInfoId];

        /// <summary>
        /// Returns the class that was created from the given <see cref="ABCClassInfo"/>.
        /// </summary>
        /// <returns>The <see cref="ScriptClass"/> instance representing the class created from
        /// <paramref name="classInfo"/>.</returns>
        /// <param name="classInfo">A <see cref="ABCClassInfo"/> instance for which to obtain the
        /// created class.</param>
        public ScriptClass getClassFromClassInfo(ABCClassInfo classInfo) => m_abcClassesByIndex[classInfo.abcIndex];

        /// <summary>
        /// Gets the <see cref="ABCScriptInfo"/> instance representing the script that declared
        /// the given trait.
        /// </summary>
        /// <returns>The <see cref="ABCScriptInfo"/> instance representing the script that declared
        /// the given trait, or null if no script exported the trait.</returns>
        /// <param name="trait">The trait whose exporting script must be obtained.</param>
        public ABCScriptInfo getExportingScript(Trait trait) => m_traitExportScripts[trait.declaringClass ?? trait];

        /// <summary>
        /// Gets the default value with which the given field should be initialized.
        /// </summary>
        /// <param name="field">The field</param>
        /// <param name="defaultVal">An output into which the field default value will be written,
        /// if the field has a default value.</param>
        /// <returns>True if <paramref name="field"/> has a default value, otherwise false.</returns>
        public bool tryGetDefaultValueOfField(ScriptField field, out ASAny defaultVal) =>
            m_fieldDefaultValues.tryGetValue(field, out defaultVal);

        /// <summary>
        /// Returns a metadata handle that refers to the underlying type of the given class in
        /// the assembly being emitted.
        /// </summary>
        /// <param name="klass">The class for which to obtain a metadata handle.</param>
        /// <param name="noPrimitiveTypes">If true, always return a handle for the object type
        /// of <paramref name="klass"/> even if it represents a primitive type.</param>
        /// <returns>The metadata handle associated with the underlying type of <paramref name="klass"/>.</returns>
        public EntityHandle getEntityHandle(Class klass, bool noPrimitiveTypes = false) {
            if (klass == null)
                return m_assemblyBuilder.metadataContext.getTypeHandle(typeof(ASAny));

            if (klass.underlyingType != null) {
                Type type = noPrimitiveTypes ? klass.underlyingType : Class.getUnderlyingOrPrimitiveType(klass);
                return m_assemblyBuilder.metadataContext.getTypeHandle(type);
            }

            klass = klass.getClassImpl();
            EntityHandle handle;

            if (m_traitEntityHandles.tryGetValue(klass, out handle))
                return handle;

            if (klass.isVectorInstantiation) {
                handle = m_assemblyBuilder.metadataContext.getTypeHandle(getTypeSignature(klass));
                m_traitEntityHandles[klass] = handle;
            }

            Debug.Assert(!handle.IsNil);
            return handle;
        }

        /// <summary>
        /// Returns a metadata handle that refers to the given field in the assembly being emitted.
        /// </summary>
        /// <param name="field">The <see cref="FieldTrait"/> representing the field for which to obtain
        /// a metadata handle.</param>
        /// <returns>The metadata handle associated with <paramref name="field"/>.</returns>
        public EntityHandle getEntityHandle(FieldTrait field) {
            EntityHandle handle;

            if (m_traitEntityHandles.tryGetValue(field, out handle))
                return handle;

            if (field.declaringClass is VectorInstFromScriptClass vecInst) {
                int defToken = field.underlyingFieldInfo.MetadataToken;

                if (!m_vectorMembersByDefToken.TryGetValue(defToken, out MemberInfo vectorFieldDef)) {
                    vectorFieldDef = typeof(ASVector<>).Module.ResolveField(defToken);
                    m_vectorMembersByDefToken[defToken] = vectorFieldDef;
                }

                handle = m_assemblyBuilder.metadataContext.getMemberHandle(vectorFieldDef, getEntityHandle(vecInst));
            }
            else if (field.underlyingFieldInfo != null) {
                handle = m_assemblyBuilder.metadataContext.getMemberHandle(field.underlyingFieldInfo);
            }

            Debug.Assert(!handle.IsNil);
            m_traitEntityHandles[field] = handle;
            return handle;
        }

        /// <summary>
        /// Returns a metadata handle that refers to the given method in the assembly being emitted.
        /// </summary>
        /// <param name="method">The <see cref="MethodTrait"/> representing the method for which to obtain
        /// a metadata handle.</param>
        /// <returns>The metadata handle associated with <paramref name="method"/>.</returns>
        public EntityHandle getEntityHandle(MethodTrait method) {
            EntityHandle handle;

            if (m_traitEntityHandles.tryGetValue(method, out handle))
                return handle;

            if (method.declaringClass is VectorInstFromScriptClass vecInst) {
                int defToken = method.underlyingMethodInfo.MetadataToken;

                if (!m_vectorMembersByDefToken.TryGetValue(defToken, out MemberInfo vectorMethodDef)) {
                    vectorMethodDef = typeof(ASVector<>).Module.ResolveMethod(defToken);
                    m_vectorMembersByDefToken[defToken] = vectorMethodDef;
                }

                handle = m_assemblyBuilder.metadataContext.getMemberHandle(vectorMethodDef, getEntityHandle(vecInst));
            }
            else if (method.underlyingMethodInfo != null) {
                handle = m_assemblyBuilder.metadataContext.getMemberHandle(method.underlyingMethodInfo);
            }

            Debug.Assert(!handle.IsNil);
            m_traitEntityHandles[method] = handle;
            return handle;
        }

        /// <summary>
        /// Returns a metadata handle that refers to the constructor of the given class in the assembly
        /// being emitted.
        /// </summary>
        /// <param name="klass">The <see cref="Class"/> representing the class for which to obtain
        /// a metadata handle for its constructor.</param>
        /// <returns>The metadata handle associated with the constructor of <paramref name="klass"/>
        /// or the null handle if there is no constructor associated with <paramref name="klass"/>.</returns>
        public EntityHandle getEntityHandleForCtor(Class klass) {
            if (klass is ScriptClass scriptClass)
                return m_classData[scriptClass].ctorBuilder.handle;

            ClassConstructor ctor = klass.constructor;
            if (ctor == null)
                return default(EntityHandle);

            EntityHandle handle;

            if (m_traitEntityHandles.tryGetValue(ctor, out handle))
                return handle;

            if (klass is VectorInstFromScriptClass vecInst) {
                int defToken = vecInst.constructor.underlyingConstructorInfo.MetadataToken;

                if (!m_vectorMembersByDefToken.TryGetValue(defToken, out MemberInfo vectorCtorDef)) {
                    vectorCtorDef = typeof(ASVector<>).Module.ResolveMethod(defToken);
                    m_vectorMembersByDefToken[defToken] = vectorCtorDef;
                }

                handle = m_assemblyBuilder.metadataContext.getMemberHandle(vectorCtorDef, getEntityHandle(vecInst));
            }
            else if (ctor.underlyingConstructorInfo != null) {
                handle = m_assemblyBuilder.metadataContext.getMemberHandle(ctor.underlyingConstructorInfo);
            }

            Debug.Assert(!handle.IsNil);
            m_traitEntityHandles[ctor] = handle;
            return handle;
        }

        /// <summary>
        /// Returns the <see cref="TypeSignature"/> for the underlying type of the given class.
        /// </summary>
        /// <param name="klass">The <see cref="Class"/> instance representing the class for which to
        /// obtain a type signature.</param>
        /// <returns>A <see cref="TypeSignature"/> for the class type represented by <paramref name="klass"/>
        /// that can be used in the assembly being emitted.</returns>
        public TypeSignature getTypeSignature(Class klass) {
            if (klass == null || klass.underlyingType != null)
                return m_assemblyBuilder.metadataContext.getTypeSignature(Class.getUnderlyingOrPrimitiveType(klass));

            klass = klass.getClassImpl();
            TypeSignature signature;

            if (m_classTypeSignatures.tryGetValue(klass, out signature))
                return signature;

            if (klass.isVectorInstantiation) {
                var vectorSig = m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASVector<>));
                signature = vectorSig.makeGenericInstance(new[] {getTypeSignature(klass.vectorElementType)});
            }
            else {
                signature = TypeSignature.forClassType(m_traitEntityHandles[klass]);
            }

            m_classTypeSignatures[klass] = signature;
            return signature;
        }

        /// <summary>
        /// Returns a metadata handle that refers to an instantiation of the <see cref="ASObject.AS_cast{T}"/>
        /// method whose type argument is the underlying type of the given class.
        /// </summary>
        /// <param name="toClass">The class to be used as the type argument.</param>
        /// <returns>A metadata handle that refers to the instantiation of the <see cref="ASObject.AS_cast{T}"/>
        /// method with the given type argument.</returns>
        public EntityHandle getEntityHandleForObjectCast(Class toClass) {
            EntityHandle handle;
            if (m_objectCastMethodHandles.tryGetValue(toClass, out handle))
                return handle;

            var classSig = getTypeSignature(toClass);
            var methodInst = new MethodInstantiation(
                m_assemblyBuilder.metadataContext.getMemberHandle(KnownMembers.objectCast),
                new TypeSignature[] {classSig}
            );
            handle = m_assemblyBuilder.metadataContext.getMethodSpecHandle(methodInst);

            m_objectCastMethodHandles[toClass] = handle;
            return handle;
        }

        /// <summary>
        /// Returns a metadata handle that refers to an instantiation of the <see cref="ASAny.AS_cast{T}"/>
        /// method whose type argument is the underlying type of the given class.
        /// </summary>
        /// <param name="toClass">The class to be used as the type argument.</param>
        /// <returns>A metadata handle that refers to the instantiation of the <see cref="ASAny.AS_cast{T}"/>
        /// method with the given type argument.</returns>
        public EntityHandle getEntityHandleForAnyCast(Class toClass) {
            EntityHandle handle;
            if (m_anyCastMethodHandles.tryGetValue(toClass, out handle))
                return handle;

            var classSig = getTypeSignature(toClass);
            var methodInst = new MethodInstantiation(
                m_assemblyBuilder.metadataContext.getMemberHandle(KnownMembers.anyCast),
                new TypeSignature[] {classSig}
            );
            handle = m_assemblyBuilder.metadataContext.getMethodSpecHandle(methodInst);

            m_anyCastMethodHandles[toClass] = handle;
            return handle;
        }

        /// <summary>
        /// Returns a <see cref="TypeSignature"/> that refers to an instantiation of the <see cref="OptionalParam{T}"/>
        /// type whose type argument is the underlying type of the given class.
        /// </summary>
        /// <param name="klass">The class to be used as the type argument.</param>
        /// <returns>A type signature that refers to the instantiation of the <see cref="OptionalParam{T}"/>
        /// type with the given type argument.</returns>
        public TypeSignature getTypeSigForOptionalParam(Class klass) {
            TypeSignature signature;
            if (m_optionalParamTypeSig.tryGetValue(klass, out signature))
                return signature;

            var optionalTypeSig = m_assemblyBuilder.metadataContext.getTypeSignature(typeof(OptionalParam<>));
            signature = optionalTypeSig.makeGenericInstance(new[] {getTypeSignature(klass)});

            m_optionalParamTypeSig[klass] = signature;
            return signature;
        }

        /// <summary>
        /// Returns the <see cref="ScriptMethod"/> representing the static initializer
        /// of the given class.
        /// </summary>
        /// <param name="klass">A class in the current compilation.</param>
        /// <returns>A <see cref="ScriptMethod"/> representing the static initializer, or
        /// null if the class does not have a static initializer defined.</returns>
        public ScriptMethod getClassStaticInitMethod(ScriptClass klass) => m_classData[klass]?.staticInit;

        /// <summary>
        /// Creates a catch scope class for an ABC exception handler.
        /// </summary>
        /// <param name="excInfo">An <see cref="ABCExceptionInfo"/> instance representing the exception
        /// handler.</param>
        /// <returns>A <see cref="ScriptClass"/> representing the created catch scope class.</returns>
        public ScriptClass createCatchScopeClass(ABCExceptionInfo excInfo) {
            var catchTypeName = excInfo.catchTypeName;
            var catchVarName = excInfo.catchVarName;

            Class catchType = getClassByMultiname(catchTypeName, allowAny: true);

            QName propName = default;
            bool noVarNameSpecified = false;

            if (catchVarName.hasRuntimeArguments)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_INVALID_RUNTIME_NAME);

            if (catchVarName.kind == ABCConstKind.QName) {
                propName = new QName(
                    abcFile.resolveNamespace(catchVarName.namespaceIndex),
                    abcFile.resolveString(catchVarName.localNameIndex)
                );

                if (propName.ns.kind == NamespaceKind.ANY && propName.localName == null)
                    noVarNameSpecified = true;
            }
            else if (catchVarName.kind == ABCConstKind.Multiname) {
                // If the variable name is a multiname, take the first namespace from the set. (This
                // is what Flash Player does.)
                NamespaceSet nsSet = abcFile.resolveNamespaceSet(catchVarName.namespaceIndex);
                if (nsSet.count != 0) {
                    propName = new QName(
                        nsSet.getNamespaces()[0],
                        abcFile.resolveString(catchVarName.localNameIndex)
                    );
                }
            }

            if (!noVarNameSpecified && (propName.ns.kind == NamespaceKind.ANY || propName.localName == null))
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_EXCEPTION_VAR_INVALID_NAME);

            // Check if we can reuse an existing scope class.
            for (int i = 0; i < m_catchScopeClasses.length; i++) {
                ScriptClass existingClass = m_catchScopeClasses[i];
                var traits = existingClass.getTraits(TraitType.ALL, TraitScope.DECLARED);

                if (noVarNameSpecified && traits.length == 0)
                    return existingClass;

                if (!noVarNameSpecified
                    && traits.length == 1
                    && traits[0] is FieldTrait field
                    && field.fieldType == catchType
                    && field.name == propName)
                {
                    return existingClass;
                }
            }

            string classMangledName = m_nameMangler.createCatchScopeClassName(m_catchScopeCounter.next());

            var syntheticClassInfo = new ABCClassInfo(
                abcIndex: -1,
                name: QName.publicName(classMangledName),
                parentName: default(ABCMultiname),
                ifaceNames: Array.Empty<ABCMultiname>(),
                protectedNs: default(Namespace),
                flags: ABCClassFlags.ClassFinal | ABCClassFlags.ClassSealed
            );

            var klass = new ScriptClass(syntheticClassInfo, m_domain);
            klass.setParent(s_objectClass);
            m_catchScopeClasses.add(klass);

            var classData = new ClassData();
            m_classData[klass] = classData;

            // Define type builder
            var typeBuilder = m_assemblyBuilder.defineType(
                classMangledName, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public);

            classData.typeBuilder = typeBuilder;
            m_traitEntityHandles[klass] = typeBuilder.handle;

            typeBuilder.setParent(getEntityHandle(s_objectClass));

            // Define no-arg constructor
            var ctorBuilder = typeBuilder.defineConstructor(MethodAttributes.Public, ReadOnlySpan<TypeSignature>.Empty);
            classData.ctorBuilder = ctorBuilder;
            m_contextILBuilder.emit(ILOp.ldarg_0);
            m_contextILBuilder.emit(ILOp.call, KnownMembers.objectCtor);
            m_contextILBuilder.emit(ILOp.ret);
            ctorBuilder.setMethodBody(m_contextILBuilder.createMethodBody());

            // Define the catch slot if a variable name is specified.
            if (!noVarNameSpecified) {
                var field = new ScriptField(propName, klass, m_domain, false, catchType, false);
                klass.tryDefineTrait(field);
                klass.defineTraitSlot(field, 1);

                var fb = typeBuilder.defineField(m_nameMangler.createName(propName), getTypeSignature(catchType), FieldAttributes.Public);
                m_traitEntityHandles[field] = fb.handle;
            }

            return klass;
        }

        /// <summary>
        /// Creates a class for the activation object of a method.
        /// </summary>
        /// <param name="traits">A read-only array view of <see cref="ABCTraitInfo"/> instances
        /// representing the fields of the activation object.</param>
        /// <returns>The created class.</returns>
        public ScriptClass createActivationClass(ReadOnlyArrayView<ABCTraitInfo> traits) {
            string classMangledName = m_nameMangler.createActivationClassName(m_activationCounter.next());

            var syntheticClassInfo = new ABCClassInfo(
                abcIndex: -1,
                name: QName.publicName(classMangledName),
                parentName: default(ABCMultiname),
                ifaceNames: Array.Empty<ABCMultiname>(),
                protectedNs: default(Namespace),
                flags: ABCClassFlags.ClassFinal | ABCClassFlags.ClassSealed
            );

            var klass = new ScriptClass(syntheticClassInfo, m_domain);
            klass.setParent(s_objectClass);

            var classData = new ClassData();
            m_classData[klass] = classData;
            classData.isActivation = true;

            // Create TypeBuilder
            var typeBuilder = m_assemblyBuilder.defineType(
                classMangledName, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public);

            classData.typeBuilder = typeBuilder;
            m_traitEntityHandles[klass] = typeBuilder.handle;

            typeBuilder.setParent(getEntityHandle(s_objectClass));

            // Define constructor
            var ctorBuilder = typeBuilder.defineConstructor(MethodAttributes.Public, ReadOnlySpan<TypeSignature>.Empty);
            classData.ctorBuilder = ctorBuilder;

            var ctorIlGen = m_contextILBuilder;
            ctorIlGen.emit(ILOp.ldarg_0);
            ctorIlGen.emit(ILOp.call, KnownMembers.objectCtor);

            for (int i = 0; i < traits.length; i++) {
                var ti = traits[i];
                Debug.Assert(ti.kind == ABCTraitFlags.Slot || ti.kind == ABCTraitFlags.Const);

                // Activation fields shouldn't be readonly.
                Class fieldType = getClassByMultiname(ti.fieldTypeName, allowAny: true);
                var field = new ScriptField(ti.name, klass, m_domain, false, fieldType, false);

                if (!klass.tryDefineTrait(field))
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, ti.name);

                var slotId = ti.slotId;
                if (slotId > 0)
                    klass.defineTraitSlot(field, slotId);

                var fieldBuilder = typeBuilder.defineField(
                    m_nameMangler.createName(field.name), getTypeSignature(field.fieldType), FieldAttributes.Public);

                m_traitEntityHandles[field] = fieldBuilder.handle;

                // Emit code to initialize the field in the constructor.
                if (ti.fieldHasDefault) {
                    ASAny defaultVal = _coerceDefaultValue(ti.fieldDefaultValue, fieldType);
                    if (!ILEmitHelper.isImplicitDefault(defaultVal, fieldType)) {
                        ctorIlGen.emit(ILOp.ldarg_0);

                        if (fieldType == null)
                            ILEmitHelper.emitPushConstantAsAny(ctorIlGen, defaultVal);
                        else if (fieldType == s_objectClass)
                            ILEmitHelper.emitPushConstantAsObject(ctorIlGen, defaultVal);
                        else
                            ILEmitHelper.emitPushConstant(ctorIlGen, defaultVal);

                        ctorIlGen.emit(ILOp.stfld, fieldBuilder.handle);
                    }
                }
                else if (fieldType != null && fieldType.tag == ClassTag.NUMBER) {
                    // Number should be initialized to NaN if no default specified.
                    ctorIlGen.emit(ILOp.ldarg_0);
                    ctorIlGen.emit(ILOp.ldc_r4, Single.NaN);
                    ctorIlGen.emit(ILOp.stfld, fieldBuilder.handle);
                }
            }

            ctorIlGen.emit(ILOp.ret);
            ctorBuilder.setMethodBody(ctorIlGen.createMethodBody());

            return klass;
        }

        /// <summary>
        /// Returns the <see cref="CapturedScope"/> instance representing the captured scope
        /// of a class.
        /// </summary>
        /// <param name="klass">The class in this compilation for which to return the captured scope.</param>
        /// <returns>The <see cref="CapturedScope"/> instance representing the captured scope
        /// of <paramref name="klass"/>, or null if no captured scope has been set.</returns>
        public CapturedScope getClassCapturedScope(ScriptClass klass) => m_classData[klass]?.capturedScope;

        /// <summary>
        /// Returns the <see cref="CapturedScope"/> instance representing the captured scope
        /// of a function.
        /// </summary>
        /// <param name="function">The <see cref="ScriptMethod"/> representing the function (that
        /// was created using <see cref="createNewFunction"/>) for which to return the captured
        /// scope.</param>
        /// <returns>The <see cref="CapturedScope"/> instance representing the captured scope
        /// of <paramref name="function"/>, or null if no captured scope exists for that method.</returns>
        public CapturedScope getFunctionCapturedScope(ScriptMethod function) => m_methodTraitData[function]?.funcCapturedScope;

        /// <summary>
        /// Returns the handle for the static field used for storing the captured
        /// scope of the given class.
        /// </summary>
        /// <param name="klass">The class for which to obtain the field that contains its
        /// captured scope.</param>
        /// <returns>An <see cref="EntityHandle"/> representing the field that contains
        /// the captured scope of <paramref name="klass"/>.</returns>
        public EntityHandle getClassCapturedScopeFieldHandle(ScriptClass klass) {
            var classData = m_classData[klass];
            return (classData == null) ? default : classData.capturedScopeField.handle;
        }

        /// <summary>
        /// Sets the captured scope stack for a class.
        /// </summary>
        /// <param name="klass">The class for which to set the captured scope.</param>
        /// <param name="scopeItems">The types of the values on the scope stack captured by the
        /// class from the context in which it is created, as a read-only array view of
        /// <see cref="CapturedScopeItem"/> instances.</param>
        /// <param name="captureDxns">Set to true if the default XML namespace should be captured.</param>
        public void setClassCapturedScope(
            ScriptClass klass, ReadOnlyArrayView<CapturedScopeItem> scopeItems, bool captureDxns)
        {
            var classData = m_classData[klass];
            Debug.Assert(classData.capturedScope == null);

            classData.capturedScope = m_capturedScopeFactory.getCapturedScopeForClass(klass, scopeItems, captureDxns);
            classData.capturedScopeField = classData.typeBuilder.defineField(
                "{capturedScope}",
                TypeSignature.forClassType(classData.capturedScope.container.typeHandle),
                FieldAttributes.Public | FieldAttributes.Static
            );
        }

        /// <summary>
        /// Creates a new function from the given method and captured scope. This is used for compiling
        /// a newfunction instruction.
        /// </summary>
        /// <param name="methodInfo">The <see cref="ABCMethodInfo"/> representing the method_info entry in
        /// the ABC file from which the function is to be created.</param>
        /// <param name="creatingScript">The <see cref="ABCScriptInfo"/> representing the script from
        /// which the function is created.</param>
        /// <param name="capturedScopeItems">The types of the values on the scope stack captured by the
        /// function from the context in which it is created, as a read-only array view of
        /// <see cref="CapturedScopeItem"/> instances.</param>
        /// <param name="captureDxns">Set to true if the default XML namespace should be captured.</param>
        public ScriptMethod createNewFunction(
            ABCMethodInfo methodInfo,
            ABCScriptInfo creatingScript,
            ReadOnlyArrayView<CapturedScopeItem> capturedScopeItems,
            bool captureDxns
        ) {
            MethodInfoData methodInfoData = m_abcMethodInfoDataByIndex[methodInfo.abcIndex];
            MethodTraitData methodTraitData;

            // If a function was already created with the same method_info, it can be reused provided
            // the captured scope types match exactly.
            if (methodInfoData.methodOrCtor is ScriptMethod existingMethod) {
                methodTraitData = m_methodTraitData[existingMethod];

                if (methodTraitData == null || !methodTraitData.isFunction)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_NEWFUNCTION_INVALID_METHOD, methodInfo.abcIndex);

                if (!capturedScopeTypesMatch(capturedScopeItems, methodTraitData.funcCapturedScope.getItems())
                    || captureDxns != methodTraitData.funcCapturedScope.capturesDxns)
                {
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_NEWFUNCTION_SCOPE_MISMATCH, methodInfo.abcIndex);
                }

                return existingMethod;
            }

            if (methodInfoData.methodOrCtor != null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_NEWFUNCTION_INVALID_METHOD, methodInfo.abcIndex);

            string mangledName = m_nameMangler.createAnonFunctionName(m_newFunctionCounter.atomicNext());

            ScriptMethod funcMethod = new ScriptMethod(
                methodInfo, QName.publicName(mangledName), null, m_domain,
                isStatic: true, isFinal: true, isOverride: false, MetadataTagCollection.empty
            );

            methodInfoData.methodOrCtor = funcMethod;
            m_traitExportScripts[funcMethod] = creatingScript;

            methodTraitData = new MethodTraitData();
            m_methodTraitData[funcMethod] = methodTraitData;
            methodTraitData.isFunction = true;

            _createMethodTraitSig(funcMethod, true);
            _emitMethodBuilder(funcMethod, m_globalTraitsContainer, funcMethod.name, MethodNameMangleMode.NONE);

            methodTraitData.funcCapturedScope = m_capturedScopeFactory.getCapturedScopeForFunction(capturedScopeItems, captureDxns);

            m_functionCompileQueue.Enqueue(funcMethod);
            return funcMethod;

            bool capturedScopeTypesMatch(
                ReadOnlyArrayView<CapturedScopeItem> scope1, ReadOnlyArrayView<CapturedScopeItem> scope2)
            {
                if (scope1.length != scope2.length)
                    return false;

                for (int i = 0; i < scope1.length; i++) {
                    ref readonly var item1 = ref scope1[i];
                    ref readonly var item2 = ref scope1[i];

                    if (item1.dataType != item2.dataType || item1.objClass != item2.objClass
                        || item1.isWithScope != item2.isWithScope)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Returns a value indicating whether the given method was created using a newfunction
        /// instruction.
        /// </summary>
        /// <param name="method">A <see cref="MethodTrait"/> representing the method.</param>
        /// <returns>True if <paramref name="method"/> was created with a newfunction instruction,
        /// otherwise false.</returns>
        public bool isMethodUsedAsFunction(MethodTrait method) => m_methodTraitData[method].isFunction;

        /// <summary>
        /// Returns the index of a namespace set in the emitted namespace set constant pool
        /// given its index in the ABC file constant pool.
        /// </summary>
        /// <param name="abcIndex">The index of the namespace set in the ABC file namespace set
        /// constant pool.</param>
        /// <returns>The index of the namespace set in the emitted constant pool.</returns>
        public int getEmitConstDataIdForNamespaceSet(int abcIndex) {
            ref int emitPoolIndex = ref m_nsSetEmitConstIdsByABCIndex[abcIndex];
            if (emitPoolIndex == -1)
                emitPoolIndex = m_emitConstantData.addNamespaceSet(m_abcFile.resolveNamespaceSet(abcIndex));

            return emitPoolIndex;
        }

        /// <summary>
        /// Returns the index in the emitted namespace set constant pool for the special namespace
        /// set containing only the public namespace.
        /// </summary>
        /// <returns>The index of the namespace set in the emitted constant pool.</returns>
        public int getEmitConstDataIdForPublicNamespaceSet() {
            if (m_publicNsSetEmitConstId == -1)
                m_publicNsSetEmitConstId = m_emitConstantData.addNamespaceSet(new NamespaceSet(Namespace.@public));

            return m_publicNsSetEmitConstId;
        }

        private void _compileMethodBodies() {
            if (compileOptions.numParallelCompileThreads <= 1)
                _compileMethodBodiesSingleThread();
            else
                _compileMethodBodiesParallel();
        }

        private void _compileMethodBodiesSingleThread() {
            var methodCompilation = new MethodCompilation(this);

            // First pass: Compile script initializers.
            foreach (var cp in _getScriptInitCompileParams())
                methodCompilation.compile(cp.methodOrCtor, cp.capturedScope, cp.methodBuilder, cp.initFlags);

            // Second pass: Compile class static constructors.
            foreach (var cp in _getClassInitCompileParams())
                methodCompilation.compile(cp.methodOrCtor, cp.capturedScope, cp.methodBuilder, cp.initFlags);

            // Third pass: Compile script and class methods.
            foreach (var cp in _getScriptAndClassMethodCompileParams())
                methodCompilation.compile(cp.methodOrCtor, cp.capturedScope, cp.methodBuilder, cp.initFlags);

            // Fourth pass: compile functions created in method bodies (with the newfunction instruction)
            while (m_functionCompileQueue.Count > 0) {
                ScriptMethod method = m_functionCompileQueue.Dequeue();
                methodCompilation.compile(
                    method,
                    getFunctionCapturedScope(method),
                    m_methodTraitData[method].methodBuilder,
                    MethodCompilationFlags.IS_SCOPED_FUNCTION
                );
            }
        }

        private void _compileMethodBodiesParallel() {
            var parallelOptions = new ParallelOptions {
                MaxDegreeOfParallelism = compileOptions.numParallelCompileThreads
            };

            var localCompilation = new ThreadLocal<MethodCompilation>(() => new MethodCompilation(this));

            try {
                Action<MethodCompilationParams> worker = cp =>
                    localCompilation.Value.compile(cp.methodOrCtor, cp.capturedScope, cp.methodBuilder, cp.initFlags);

                // First pass: Compile script initializers. No captured scope here.
                Parallel.ForEach( _getScriptInitCompileParams(), parallelOptions, worker);

                // Second pass: Compile class static constructors.
                Parallel.ForEach(_getClassInitCompileParams(), parallelOptions, worker);

                // Third pass: Compile script and class methods.
                Parallel.ForEach(_getScriptAndClassMethodCompileParams(), parallelOptions, worker);

                // Fourth pass: compile functions created in method bodies (with the newfunction instruction)

                DynamicArray<MethodCompilationParams> queuedFuncCompileParams = default;

                while (m_functionCompileQueue.Count > 0) {
                    queuedFuncCompileParams.clear();
                    queuedFuncCompileParams.ensureCapacity(m_functionCompileQueue.Count);

                    while (m_functionCompileQueue.Count > 0) {
                        ScriptMethod method = m_functionCompileQueue.Dequeue();
                        queuedFuncCompileParams.add(new MethodCompilationParams {
                            methodOrCtor = method,
                            capturedScope = getFunctionCapturedScope(method),
                            methodBuilder = m_methodTraitData[method].methodBuilder,
                            initFlags = MethodCompilationFlags.IS_SCOPED_FUNCTION,
                        });
                    }

                    Parallel.ForEach(queuedFuncCompileParams.asReadOnlyArrayView(), parallelOptions, worker);
                }
            }
            catch (AggregateException e) {
                // Throw the first AVM2Exception that occured.
                foreach (var innerEx in e.InnerExceptions) {
                    if (innerEx is AVM2Exception)
                        ExceptionDispatchInfo.Throw(innerEx);
                }
                throw;
            }
            finally {
                localCompilation.Dispose();
            }
        }

        private IEnumerable<MethodCompilationParams> _getScriptInitCompileParams() {
            for (int i = 0; i < m_abcScriptDataByIndex.length; i++) {
                ScriptData scriptData = m_abcScriptDataByIndex[i];
                if (scriptData.initMethod == null)
                    continue;

                yield return new MethodCompilationParams {
                    methodOrCtor = scriptData.initMethod,
                    capturedScope = null,
                    methodBuilder = m_methodTraitData[scriptData.initMethod].methodBuilder,
                    initFlags = MethodCompilationFlags.IS_SCRIPT_INIT,
                };
            }
        }

        private IEnumerable<MethodCompilationParams> _getClassInitCompileParams() {
            foreach (var classDataEntry in m_classData) {
                ClassData classData = classDataEntry.Value;
                if (classData.staticInit == null)
                    continue;

                yield return new MethodCompilationParams {
                    methodOrCtor = classData.staticInit,
                    capturedScope = getClassCapturedScope(classDataEntry.Key),
                    methodBuilder = m_methodTraitData[classData.staticInit].methodBuilder,
                    initFlags = MethodCompilationFlags.IS_STATIC_INIT,
                };
            }
        }

        private IEnumerable<MethodCompilationParams> _getScriptAndClassMethodCompileParams() {
            for (int i = 0; i < m_abcScriptDataByIndex.length; i++) {
                foreach (var cp in getCompileParamsForTraits(m_abcScriptDataByIndex[i].traits.asReadOnlyArrayView(), null))
                    yield return cp;
            }

            foreach (var classDataEntry in m_classData) {
                ScriptClass klass = classDataEntry.Key;
                CapturedScope capturedScope = getClassCapturedScope(klass);

                if (klass.constructor is ScriptClassConstructor ctor) {
                    yield return new MethodCompilationParams {
                        methodOrCtor = ctor,
                        capturedScope = capturedScope,
                        methodBuilder = classDataEntry.Value.ctorBuilder
                    };
                }

                var declaredTraits = klass.getTraits(TraitType.ALL, TraitScope.DECLARED);
                foreach (var cp in getCompileParamsForTraits(declaredTraits, capturedScope))
                    yield return cp;
            }

            IEnumerable<MethodCompilationParams> getCompileParamsForTraits(
                ReadOnlyArrayView<Trait> traits, CapturedScope capturedScope)
            {
                for (int i = 0; i < traits.length; i++) {
                    Trait trait = traits[i];

                    if (trait is ScriptMethod method && getMethodBodyInfo(method.abcMethodInfo) != null) {
                        yield return new MethodCompilationParams {
                            methodOrCtor = method,
                            methodBuilder = m_methodTraitData[method].methodBuilder,
                            capturedScope = capturedScope,
                        };
                    }

                    if (!(trait is PropertyTrait prop))
                        continue;

                    if (prop.getter is ScriptMethod getter && getter.declaringClass == prop.declaringClass
                        && getMethodBodyInfo(getter.abcMethodInfo) != null)
                    {
                        yield return new MethodCompilationParams {
                            methodOrCtor = getter,
                            methodBuilder = m_methodTraitData[getter].methodBuilder,
                            capturedScope = capturedScope,
                        };
                    }
                    if (prop.setter is ScriptMethod setter && setter.declaringClass == prop.declaringClass
                        && getMethodBodyInfo(setter.abcMethodInfo) != null)
                    {
                        yield return new MethodCompilationParams {
                            methodOrCtor = setter,
                            methodBuilder = m_methodTraitData[setter].methodBuilder,
                            capturedScope = capturedScope,
                        };
                    }
                }
            }
        }

        private void _setUnderlyingDefinitionsForTraits() {
            Module loadedModule = m_loadedAssembly.ManifestModule;
            TokenMapping tkMap = m_emitResult.tokenMapping;

            foreach (var classDataEntry in m_classData) {
                ScriptClass klass = classDataEntry.Key;

                EntityHandle typeBuilderHandle = classDataEntry.Value.typeBuilder.handle;

                klass.setUnderlyingType(loadedModule.ResolveType(tkMap.getMappedToken(typeBuilderHandle)));
                ClassTypeMap.addClass(klass.underlyingType, klass);

                var ctor = (ScriptClassConstructor)klass.constructor;
                if (ctor != null) {
                    EntityHandle ctorBuilderHandle = classDataEntry.Value.ctorBuilder.handle;
                    ctor.setUnderlyingCtorInfo((ConstructorInfo)loadedModule.ResolveMethod(tkMap.getMappedToken(ctorBuilderHandle)));
                }

                var declaredTraits = klass.getTraits(TraitType.ALL, TraitScope.DECLARED);
                for (int i = 0; i < declaredTraits.length; i++)
                    _setUnderlyingDefinitionForTrait(declaredTraits[i], loadedModule);
            }

            for (int i = 0; i < m_abcScriptDataByIndex.length; i++) {
                var scriptData = m_abcScriptDataByIndex[i];
                _setUnderlyingDefinitionForTrait(scriptData.initMethod, loadedModule);

                var traits = scriptData.traits.asSpan();
                for (int j = 0; j < traits.Length; j++)
                    _setUnderlyingDefinitionForTrait(traits[j], loadedModule);
            }

            foreach (var methodDataEntry in m_methodTraitData) {
                if (methodDataEntry.Key is ScriptMethod method && methodDataEntry.Value.isFunction)
                    _setUnderlyingDefinitionForTrait(method, loadedModule);
            }
        }

        private void _setUnderlyingDefinitionForTrait(Trait trait, Module loadedModule) {
            if (trait == null || !m_traitEntityHandles.tryGetValue(trait, out EntityHandle handle))
                return;

            int tokenInModule = m_emitResult.tokenMapping.getMappedToken(handle);

            if (trait is ScriptField field) {
                field.setUnderlyingFieldInfo(loadedModule.ResolveField(tokenInModule));
            }
            else if (trait is ScriptMethod method) {
                method.setUnderlyingMethodInfo((MethodInfo)loadedModule.ResolveMethod(tokenInModule));
            }
            else if (trait is PropertyTrait prop) {
                _setUnderlyingDefinitionForTrait(prop.getter, loadedModule);
                _setUnderlyingDefinitionForTrait(prop.setter, loadedModule);
            }
        }

    }

}
