using System;
using System.Reflection;
using System.Reflection.Metadata;
using Mariana.AVM2.Core;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents the constant pool used by a dynamic assembly generated by the compiler
    /// to reference constants that cannot be emitted directly in IL such as classes,
    /// traits and namespaces.
    /// </summary>
    internal sealed class EmitConstantData {

        private IndexedSet<RefWrapper<Class>> m_classes = new IndexedSet<RefWrapper<Class>>();
        private IndexedSet<RefWrapper<Trait>> m_traits = new IndexedSet<RefWrapper<Trait>>();
        private IndexedSet<Namespace> m_nss = new IndexedSet<Namespace>();
        private IndexedSet<Namespace> m_xnss = new IndexedSet<Namespace>();
        private IndexedSet<QName> m_qnames = new IndexedSet<QName>();
        private IndexedSet<QName> m_xqnames = new IndexedSet<QName>();

        private DynamicArray<int> m_nsSetIndices = new DynamicArray<int>();
        private DynamicArray<int> m_nsSetLengths = new DynamicArray<int>();
        private int m_nsSetCount = 0;

        private int m_regexpCount = 0;

        private ApplicationDomain m_domain;

        private TypeBuilder m_containerType;
        private FieldBuilder m_classesArrayField;
        private FieldBuilder m_traitsArrayField;
        private FieldBuilder m_nsArrayField;
        private FieldBuilder m_xnsArrayField;
        private FieldBuilder m_qnameArrayField;
        private FieldBuilder m_xqnameArrayField;
        private FieldBuilder m_nssetArrayField;
        private FieldBuilder m_regexpArrayField;
        private FieldBuilder m_globalObjField;
        private FieldBuilder m_appDomainField;

        /// <summary>
        /// Creates a new instance of <see cref="EmitConstantData"/>.
        /// </summary>
        /// <param name="domain">The application domain of the script being compiled.</param>
        /// <param name="assembly">The <see cref="AssemblyBuilder"/></param>
        public EmitConstantData(ApplicationDomain domain, AssemblyBuilder assembly) {
            m_containerType = assembly.defineType(
                new TypeName(NameMangler.INTERNAL_NAMESPACE, "ConstData"),
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit
            );

            FieldBuilder defineField(string name, Type type, bool initOnly = true) {
                return m_containerType.defineField(
                    name,
                    assembly.metadataContext.getTypeSignature(type),
                    FieldAttributes.Public | FieldAttributes.Static | (initOnly ? FieldAttributes.InitOnly : 0)
                );
            }

            m_classesArrayField = defineField("classes", typeof(Class[]));
            m_traitsArrayField = defineField("traits", typeof(Trait[]));
            m_nsArrayField = defineField("nss", typeof(Namespace[]));
            m_xnsArrayField = defineField("xnss", typeof(ASNamespace[]));
            m_qnameArrayField = defineField("qnames", typeof(QName[]));
            m_xqnameArrayField = defineField("xqnames", typeof(ASQName[]));
            m_nssetArrayField = defineField("nssets", typeof(NamespaceSet[]));
            m_regexpArrayField = defineField("regexps", typeof(ASRegExp[]));
            m_globalObjField = defineField("global", typeof(ASObject), initOnly: false);
            m_appDomainField = defineField("domain", typeof(ApplicationDomain), initOnly: false);

            m_domain = domain;
        }

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// class constants.
        /// </summary>
        public EntityHandle classesArrayFieldHandle => m_classesArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// trait constants.
        /// </summary>
        public EntityHandle traitsArrayFieldHandle => m_traitsArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// namespace constants.
        /// </summary>
        public EntityHandle nsArrayFieldHandle => m_nsArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// XML namespace constants.
        /// </summary>
        public EntityHandle xmlNsArrayFieldHandle => m_xnsArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// QName constants.
        /// </summary>
        public EntityHandle qnameArrayFieldHandle => m_qnameArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// XML QName constants.
        /// </summary>
        public EntityHandle xmlQnameArrayFieldHandle => m_xqnameArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the emitted
        /// namespace set constants.
        /// </summary>
        public EntityHandle nsSetArrayFieldHandle => m_nssetArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing an array holding the slots
        /// for regular expression constants.
        /// </summary>
        public EntityHandle regexpArrayFieldHandle => m_regexpArrayField.handle;

        /// <summary>
        /// Returns the handle to the static field containing the script's application domain.
        /// </summary>
        public EntityHandle appDomainFieldHandle => m_appDomainField.handle;

        /// <summary>
        /// Returns the handle to the static field containing the global object.
        /// </summary>
        public EntityHandle globalObjFieldHandle => m_globalObjField.handle;

        /// <summary>
        /// Returns the index of the given class in the class constant array. If the class does not
        /// exist, an entry for it is created.
        /// </summary>
        /// <param name="klass">The class for which to find or add an entry in the class constant array.</param>
        /// <returns>The index of <paramref name="klass"/> in the class constant array.</returns>
        public int getClassIndex(Class klass) => m_classes.findOrAdd(klass);

        /// <summary>
        /// Returns the index of the given trait in the trait constant array. If the trait does not
        /// exist, an entry for it is created.
        /// </summary>
        /// <param name="trait">The trait for which to find or add an entry in the trait constant array.</param>
        /// <returns>The index of <paramref name="trait"/> in the trait constant array.</returns>
        public int getTraitIndex(Trait trait) => m_traits.findOrAdd(trait);

        /// <summary>
        /// Returns the index of the given namespace in the namespace constant array. If the trait does not
        /// exist, an entry for it is created.
        /// </summary>
        /// <param name="ns">The namespace for which to find or add an entry in the namespace constant array.</param>
        /// <returns>The index of <paramref name="ns"/> in the namespace constant array.</returns>
        public int getNamespaceIndex(Namespace ns) => m_nss.findOrAdd(ns);

        /// <summary>
        /// Returns the index of the given namespace in the XML namespace constant array. If the trait does not
        /// exist, an entry for it is created.
        /// </summary>
        /// <param name="ns">The namespace for which to find or add an entry in the XML namespace constant array.</param>
        /// <returns>The index of <paramref name="ns"/> in the XML namespace constant array.</returns>
        public int getXMLNamespaceIndex(Namespace ns) => m_xnss.findOrAdd(ns);

        /// <summary>
        /// Returns the index of the given QName in the QName constant array. If the trait does not
        /// exist, an entry for it is created.
        /// </summary>
        /// <param name="qname">The QName for which to find or add an entry in the QName constant array.</param>
        /// <returns>The index of <paramref name="qname"/> in the QName constant array.</returns>
        public int getQNameIndex(QName qname) => m_qnames.findOrAdd(qname);

        /// <summary>
        /// Returns the index of the given QName in the XML QName constant array. If the trait does not
        /// exist, an entry for it is created.
        /// </summary>
        /// <param name="qname">The QName for which to find or add an entry in the XML QName constant array.</param>
        /// <returns>The index of <paramref name="qname"/> in the XML QName constant array.</returns>
        public int getXMLQNameIndex(QName qname) => m_xqnames.findOrAdd(qname);

        /// <summary>
        /// Adds a namespace set to the namespace set constant array.
        /// </summary>
        /// <param name="nsSet">The namespace set to add.</param>
        /// <returns>The index of the new entry for <paramref name="nsSet"/> in the namespace
        /// set constant array.</returns>
        public int addNamespaceSet(in NamespaceSet nsSet) {
            var nsInSet = nsSet.getNamespaces();
            m_nsSetLengths.add(nsInSet.length);

            for (int i = 0; i < nsInSet.length; i++)
                m_nsSetIndices.add(getNamespaceIndex(nsInSet[i]));

            m_nsSetCount++;
            return m_nsSetCount - 1;
        }

        /// <summary>
        /// Adds a regex to the RegExp constant array.
        /// </summary>
        /// <param name="pattern">The regex pattern string.</param>
        /// <param name="flags">The regex flags string.</param>
        /// <returns>The index of the new entry for the regexp represented by <paramref name="pattern"/>
        /// and <paramref name="flags"/> in the RegExp constant array.</returns>
        public int addRegExp(string pattern, string flags) {
            m_regexpCount++;
            return m_regexpCount - 1;
        }

        /// <summary>
        /// Emits the static initializer for the constant pool. Call this only after emitting
        /// all constants.
        /// </summary>
        /// <param name="ilBuilder">An <see cref="ILBuilder"/> to be used for emitting the initializer.</param>
        public void emitConstDataInitializer(ILBuilder ilBuilder) {
            var methodBuilder = m_containerType.defineConstructor(
                MethodAttributes.Private | MethodAttributes.Static,
                ReadOnlySpan<TypeSignature>.Empty
            );

            ilBuilder.emit(ILOp.ldc_i4, m_classes.count);
            ilBuilder.emit(ILOp.newarr, typeof(Class));
            ilBuilder.emit(ILOp.stsfld, m_classesArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_traits.count);
            ilBuilder.emit(ILOp.newarr, typeof(Trait));
            ilBuilder.emit(ILOp.stsfld, m_traitsArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_nss.count);
            ilBuilder.emit(ILOp.newarr, typeof(Namespace));
            ilBuilder.emit(ILOp.stsfld, m_nsArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_xnss.count);
            ilBuilder.emit(ILOp.newarr, typeof(ASNamespace));
            ilBuilder.emit(ILOp.stsfld, m_xnsArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_qnames.count);
            ilBuilder.emit(ILOp.newarr, typeof(QName));
            ilBuilder.emit(ILOp.stsfld, m_qnameArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_xqnames.count);
            ilBuilder.emit(ILOp.newarr, typeof(ASQName));
            ilBuilder.emit(ILOp.stsfld, m_xqnameArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_nsSetCount);
            ilBuilder.emit(ILOp.newarr, typeof(NamespaceSet));
            ilBuilder.emit(ILOp.stsfld, m_nssetArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_regexpCount);
            ilBuilder.emit(ILOp.newarr, typeof(ASRegExp));
            ilBuilder.emit(ILOp.stsfld, m_regexpArrayField.handle);

            _emitInitializeNamespaces(ilBuilder);
            _emitInitializeQNames(ilBuilder);
            _emitInitializeNamespaceSets(ilBuilder);
            _emitInitializeXMLNamespaces(ilBuilder);
            _emitInitializeXMLQNames(ilBuilder);

            ilBuilder.emit(ILOp.ret);

            methodBuilder.setMethodBody(ilBuilder.createMethodBody());
        }

        /// <summary>
        /// Emits code to initialize the namespace constant pool in the constant pool static initializer.
        /// </summary>
        /// <param name="ilBuilder">The <see cref="ILBuilder"/> into which to emit the code.</param>
        private void _emitInitializeNamespaces(ILBuilder ilBuilder) {
            if (m_nss.count == 0)
                return;

            var nsArrLocal = ilBuilder.acquireTempLocal(typeof(Namespace[]));
            ilBuilder.emit(ILOp.ldsfld, nsArrayFieldHandle);
            ilBuilder.emit(ILOp.stloc, nsArrLocal);

            // Initialize in reverse order to allow range checks to be eliminated.
            for (int i = m_nss.count - 1; i >= 0; i--) {
                Namespace ns = m_nss[i];

                ilBuilder.emit(ILOp.ldloc, nsArrLocal);
                ilBuilder.emit(ILOp.ldc_i4, i);

                switch (ns.kind) {
                    case NamespaceKind.ANY:
                        ilBuilder.emit(ILOp.ldelema, typeof(Namespace));
                        ilBuilder.emit(ILOp.initobj, typeof(ASNamespace));
                        break;

                    case NamespaceKind.PRIVATE:
                        ilBuilder.emit(ILOp.ldc_i4, ns.privateNamespaceId);
                        ilBuilder.emit(ILOp.call, KnownMembers.namespaceCreatePrivateFromId, 0);
                        ilBuilder.emit(ILOp.stelem, typeof(Namespace));
                        break;

                    case NamespaceKind.NAMESPACE:
                        ilBuilder.emit(ILOp.ldelema, typeof(Namespace));
                        ilBuilder.emit(ILOp.ldstr, ns.uri);
                        ilBuilder.emit(ILOp.call, KnownMembers.namespaceCtorFromURI, -2);
                        break;

                    default:
                        ilBuilder.emit(ILOp.ldelema, typeof(Namespace));
                        ilBuilder.emit(ILOp.ldc_i4, (int)ns.kind);
                        ilBuilder.emit(ILOp.ldstr, ns.uri);
                        ilBuilder.emit(ILOp.call, KnownMembers.namespaceCtorFromKindAndURI, -3);
                        break;
                }
            }

            ilBuilder.releaseTempLocal(nsArrLocal);
        }

        /// <summary>
        /// Emits code to initialize the QName constant pool in the constant pool static initializer.
        /// </summary>
        /// <param name="ilBuilder">The <see cref="ILBuilder"/> into which to emit the code.</param>
        private void _emitInitializeQNames(ILBuilder ilBuilder) {
            if (m_qnames.count == 0)
                return;

            ILBuilder.Local qnameArrLocal = ilBuilder.acquireTempLocal(typeof(QName[]));
            ilBuilder.emit(ILOp.ldsfld, qnameArrayFieldHandle);
            ilBuilder.emit(ILOp.stloc, qnameArrLocal);

            ILBuilder.Local nsTempLocal = default;

            // Initialize in reverse order to allow range checks to be eliminated.
            for (int i = m_qnames.count - 1; i >= 0; i--) {
                QName qname = m_qnames[i];

                ilBuilder.emit(ILOp.ldloc, qnameArrLocal);
                ilBuilder.emit(ILOp.ldc_i4, i);

                if (qname.ns.isPublic) {
                    ilBuilder.emit(ILOp.ldstr, qname.localName);
                    ilBuilder.emit(ILOp.call, KnownMembers.qnamePublicName, 0);
                    ilBuilder.emit(ILOp.stelem, typeof(QName));
                    continue;
                }

                if (qname.ns.kind == NamespaceKind.NAMESPACE) {
                    ilBuilder.emit(ILOp.ldelema, typeof(QName));
                    ilBuilder.emit(ILOp.ldstr, qname.ns.uri);
                    ilBuilder.emit(ILOp.ldstr, qname.localName);
                    ilBuilder.emit(ILOp.call, KnownMembers.qnameCtorFromUriAndLocalName, -3);
                    continue;
                }

                ilBuilder.emit(ILOp.ldelema, typeof(QName));

                if (nsTempLocal.isDefault)
                    nsTempLocal = ilBuilder.acquireTempLocal(typeof(Namespace));

                if (qname.ns.kind == NamespaceKind.ANY) {
                    ilBuilder.emit(ILOp.ldloca, nsTempLocal);
                    ilBuilder.emit(ILOp.dup);
                    ilBuilder.emit(ILOp.initobj, typeof(Namespace));
                }
                else if (qname.ns.kind == NamespaceKind.PRIVATE) {
                    ilBuilder.emit(ILOp.ldc_i4, qname.ns.privateNamespaceId);
                    ilBuilder.emit(ILOp.call, KnownMembers.namespaceCreatePrivateFromId, 0);
                    ilBuilder.emit(ILOp.stloc, nsTempLocal);
                    ilBuilder.emit(ILOp.ldloca, nsTempLocal);
                }
                else {
                    ilBuilder.emit(ILOp.ldloca, nsTempLocal);
                    ilBuilder.emit(ILOp.dup);
                    ilBuilder.emit(ILOp.ldc_i4, (int)qname.ns.kind);
                    ilBuilder.emit(ILOp.ldstr, qname.ns.uri);
                    ilBuilder.emit(ILOp.call, KnownMembers.namespaceCtorFromKindAndURI, -3);
                }

                ilBuilder.emit(ILOp.ldstr, qname.localName);
                ilBuilder.emit(ILOp.call, KnownMembers.qnameCtorFromNsAndLocalName, -3);
            }

            ilBuilder.releaseTempLocal(qnameArrLocal);

            if (!nsTempLocal.isDefault)
                ilBuilder.releaseTempLocal(nsTempLocal);
        }

        /// <summary>
        /// Emits code to initialize the XML namespace constant pool in the constant pool static initializer.
        /// </summary>
        /// <param name="ilBuilder">The <see cref="ILBuilder"/> into which to emit the code.</param>
        private void _emitInitializeXMLNamespaces(ILBuilder ilBuilder) {
            if (m_xnss.count == 0)
                return;

            var nsArrLocal = ilBuilder.acquireTempLocal(typeof(ASNamespace[]));
            ilBuilder.emit(ILOp.ldsfld, xmlNsArrayFieldHandle);
            ilBuilder.emit(ILOp.stloc, nsArrLocal);

            // Initialize in reverse order to allow range checks to be eliminated.
            for (int i = m_xnss.count - 1; i >= 0; i--) {
                Namespace ns = m_xnss[i];

                ilBuilder.emit(ILOp.ldloc, nsArrLocal);
                ilBuilder.emit(ILOp.ldc_i4, i);
                ilBuilder.emit(ILOp.ldstr, ns.uri);
                ilBuilder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromURI);
                ilBuilder.emit(ILOp.stelem_ref);
            }

            ilBuilder.releaseTempLocal(nsArrLocal);
        }

        /// <summary>
        /// Emits code to initialize the XML QName constant pool in the constant pool static initializer.
        /// </summary>
        /// <param name="ilBuilder">The <see cref="ILBuilder"/> into which to emit the code.</param>
        private void _emitInitializeXMLQNames(ILBuilder ilBuilder) {
            if (m_xqnames.count == 0)
                return;

            var qnameArrLocal = ilBuilder.acquireTempLocal(typeof(ASQName[]));
            ilBuilder.emit(ILOp.ldsfld, xmlQnameArrayFieldHandle);
            ilBuilder.emit(ILOp.stloc, qnameArrLocal);

            // Initialize in reverse order to allow range checks to be eliminated.
            for (int i = m_xqnames.count - 1; i >= 0; i--) {
                QName qname = m_xqnames[i];

                ilBuilder.emit(ILOp.ldloc, qnameArrLocal);
                ilBuilder.emit(ILOp.ldc_i4, i);
                ilBuilder.emit(ILOp.ldstr, qname.ns.uri);
                ilBuilder.emit(ILOp.ldstr, qname.localName);
                ilBuilder.emit(ILOp.newobj, KnownMembers.xmlQnameCtorFromUriAndLocal);
                ilBuilder.emit(ILOp.stelem_ref);
            }

            ilBuilder.releaseTempLocal(qnameArrLocal);
        }

        /// <summary>
        /// Emits code to initialize the namespace set constant pool in the constant pool static initializer.
        /// </summary>
        /// <param name="ilBuilder">The <see cref="ILBuilder"/> into which to emit the code.</param>
        private void _emitInitializeNamespaceSets(ILBuilder ilBuilder) {
            if (m_nsSetCount == 0)
                return;

            var nsSetArrLocal = ilBuilder.acquireTempLocal(typeof(NamespaceSet[]));
            var nsArrLocal = ilBuilder.acquireTempLocal(typeof(Namespace[]));
            var tempNsArrayLocal = ilBuilder.acquireTempLocal(typeof(Namespace[]));

            ilBuilder.emit(ILOp.ldsfld, nsSetArrayFieldHandle);
            ilBuilder.emit(ILOp.stloc, nsSetArrLocal);
            ilBuilder.emit(ILOp.ldsfld, nsArrayFieldHandle);
            ilBuilder.emit(ILOp.stloc, nsArrLocal);

            var setLengths = m_nsSetLengths.asSpan();
            var setIndices = m_nsSetIndices.asSpan();

            // Create a temporary array to hold the namespaces that will be used for
            // constructing sets. The length of this array must be the largest length of
            // any namespace set in the pool.

            int maxSetLength = 0;
            for (int i = 0; i < setLengths.Length; i++)
                maxSetLength = Math.Max(maxSetLength, setLengths[i]);

            ilBuilder.emit(ILOp.ldc_i4, maxSetLength);
            ilBuilder.emit(ILOp.newarr, typeof(Namespace));
            ilBuilder.emit(ILOp.stloc, tempNsArrayLocal);

            // Initialize in reverse order to allow range checks to be eliminated.
            for (int i = setLengths.Length - 1; i >= 0; i--) {
                Span<int> indicesForThisSet = setIndices.Slice(setIndices.Length - setLengths[i]);

                // Copy the namespaces in the set from the namespace constant pool to the
                // temporary array.
                for (int j = 0; j < indicesForThisSet.Length; j++) {
                    ilBuilder.emit(ILOp.ldloc, tempNsArrayLocal);
                    ilBuilder.emit(ILOp.ldc_i4, j);
                    ilBuilder.emit(ILOp.ldloc, nsArrLocal);
                    ilBuilder.emit(ILOp.ldc_i4, indicesForThisSet[j]);
                    ilBuilder.emit(ILOp.ldelem, typeof(Namespace));
                    ilBuilder.emit(ILOp.stelem, typeof(Namespace));
                }

                ilBuilder.emit(ILOp.ldloc, nsSetArrLocal);
                ilBuilder.emit(ILOp.ldc_i4, i);
                ilBuilder.emit(ILOp.ldelema, typeof(NamespaceSet));

                if (indicesForThisSet.Length == maxSetLength) {
                    ilBuilder.emit(ILOp.ldloc, tempNsArrayLocal);
                    ilBuilder.emit(ILOp.call, KnownMembers.nsSetCtorFromArray, -2);
                }
                else {
                    ilBuilder.emit(ILOp.ldloc, tempNsArrayLocal);
                    ilBuilder.emit(ILOp.ldc_i4_0);
                    ilBuilder.emit(ILOp.ldc_i4, indicesForThisSet.Length);
                    ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanNamespaceFromSubArray, -2);
                    ilBuilder.emit(ILOp.call, KnownMembers.nsSetCtorFromSpan, -2);
                }

                setIndices = setIndices.Slice(0, setIndices.Length - indicesForThisSet.Length);
            }

            ilBuilder.releaseTempLocal(nsSetArrLocal);
            ilBuilder.releaseTempLocal(nsArrLocal);
            ilBuilder.releaseTempLocal(tempNsArrayLocal);
        }

        /// <summary>
        /// Loads any constants that need to be initialized after the emitted dynamic assembly has
        /// been loaded.
        /// </summary>
        /// <param name="loadedAssembly">An <see cref="Assembly"/> representing the loaded
        /// dynamic assembly.</param>
        /// <param name="tokenMapping">The <see cref="TokenMapping"/> that was created when the
        /// dynamic assembly was serialized.</param>
        public void loadData(Assembly loadedAssembly, TokenMapping tokenMapping) {
            Module module = loadedAssembly.ManifestModule;

            T getField<T>(FieldBuilder fb) =>
                (T)module.ResolveField(tokenMapping.getMappedToken(fb.handle)).GetValue(null);

            var classes = getField<Class[]>(m_classesArrayField);
            var traits = getField<Trait[]>(m_traitsArrayField);

            for (int i = 0; i < m_classes.count; i++)
                classes[i] = m_classes[i].value;

            for (int i = 0; i < m_traits.count; i++)
                traits[i] = m_traits[i].value;

            module.ResolveField(tokenMapping.getMappedToken(m_appDomainField.handle)).SetValue(null, m_domain);
            module.ResolveField(tokenMapping.getMappedToken(m_globalObjField.handle)).SetValue(null, m_domain.globalObject);
        }

    }

}
