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

        private DynamicArray<NamespaceSet> m_nssets = new DynamicArray<NamespaceSet>();
        private int m_regexpCount = 0;

        private ASObject m_globalObject;

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

        /// <summary>
        /// Creates a new instance of <see cref="EmitConstantData"/>.
        /// </summary>
        /// <param name="assembly">The <see cref="AssemblyBuilder"/></param>
        public EmitConstantData(AssemblyBuilder assembly) {
            m_containerType = assembly.defineType(
                "{ConstData}",
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
        /// Sets the object that will be used as the global object.
        /// </summary>
        /// <param name="globalObject">The global object.</param>
        public void setGlobalObject(ASObject globalObject) => m_globalObject = globalObject;

        /// <summary>
        /// Adds a namespace set to the namespace set constant array.
        /// </summary>
        /// <param name="nsSet">The namespace set to add.</param>
        /// <returns>The index of the new entry for <paramref name="nsSet"/> in the namespace
        /// set constant array.</returns>
        public int addNamespaceSet(in NamespaceSet nsSet) {
            m_nssets.add(nsSet);
            return m_nssets.length - 1;
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

            ilBuilder.emit(ILOp.ldc_i4, m_nssets.length);
            ilBuilder.emit(ILOp.newarr, typeof(NamespaceSet));
            ilBuilder.emit(ILOp.stsfld, m_nssetArrayField.handle);

            ilBuilder.emit(ILOp.ldc_i4, m_regexpCount);
            ilBuilder.emit(ILOp.newarr, typeof(ASRegExp));
            ilBuilder.emit(ILOp.stsfld, m_regexpArrayField.handle);

            ilBuilder.emit(ILOp.ret);

            methodBuilder.setMethodBody(ilBuilder.createMethodBody());
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
            var nss = getField<Namespace[]>(m_nsArrayField);
            var qnames = getField<QName[]>(m_qnameArrayField);
            var xnss = getField<ASNamespace[]>(m_xnsArrayField);
            var xqnames = getField<ASQName[]>(m_xqnameArrayField);
            var nssets = getField<NamespaceSet[]>(m_nssetArrayField);

            for (int i = 0; i < m_classes.count; i++)
                classes[i] = m_classes[i].value;

            for (int i = 0; i < m_traits.count; i++)
                traits[i] = m_traits[i].value;

            for (int i = 0; i < m_xnss.count; i++)
                xnss[i] = new ASNamespace(m_xnss[i].uri);

            for (int i = 0; i < m_xqnames.count; i++) {
                QName qname = m_xqnames[i];
                xqnames[i] = new ASQName(qname.ns.uri, qname.localName);
            }

            m_nss.copyTo(nss);
            m_qnames.copyTo(qnames);
            m_nssets.asSpan().CopyTo(nssets);

            // Regexps are lazily initialized, so no initializing here.

            module.ResolveField(tokenMapping.getMappedToken(m_globalObjField.handle)).SetValue(null, m_globalObject);
        }

    }

}
