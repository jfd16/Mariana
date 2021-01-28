using System;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An XMLList represents an ordered collection of XML nodes.
    /// </summary>
    [AVM2ExportClass(name = "XMLList", hasPrototypeMethods = true, hasIndexLookupMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.XML_LIST)]
    public sealed class ASXMLList : ASObject {

        /// <summary>
        /// Contains link information for a linked XMLList.
        /// </summary>
        private class LinkInfo {
            internal ASObject target;
            internal string localName;
            internal string uri;
            internal bool isAttribute;

            internal LinkInfo(ASObject target, string uri, string localName, bool isAttribute) {
                this.target = target;
                this.uri = uri;
                this.localName = localName;
                this.isAttribute = isAttribute;
            }
        }

        /// <summary>
        /// The value of the "length" property of the AS3 XMLList class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        private DynamicArray<ASXML> m_items;
        private LinkInfo m_link;

        /// <summary>
        /// Creates a new, empty XMLList.
        /// </summary>
        public ASXMLList() {

        }

        /// <summary>
        /// Creates a new XMLList and initializes it with the given items.
        /// </summary>
        /// <param name="items">A span containing the XML nodes to add to the created list.</param>
        public ASXMLList(ReadOnlySpan<ASXML> items) {
            _initItems(items.ToArray(), items.Length, true);
        }

        /// <summary>
        /// Creates a new XMLList instance.
        /// </summary>
        /// <param name="items">The array containing the items of the XMLList.</param>
        /// <param name="length">The number of items in the XMLList.</param>
        /// <param name="noCopy">If set to true, use the <paramref name="items"/> array directly
        /// instead of making a copy.</param>
        /// <param name="targetObject">If this XMLList must be linked to a target object, set this
        /// to the target object. Otherwise set this to null.</param>
        /// <param name="targetNameIsAttr">If this XMLList must contain attributes, set this to true.
        /// Only applicable if <paramref name="targetObject"/> is not null.</param>
        internal ASXMLList(
            ASXML[] items, int length, bool noCopy,
            ASObject targetObject = null, bool targetNameIsAttr = false)
        {
            _initItems(items, length, noCopy);
            if (targetObject != null)
                m_link = new LinkInfo(targetObject, null, null, targetNameIsAttr);
        }

        /// <summary>
        /// Creates a new XMLList instance.
        /// </summary>
        /// <param name="items">The array containing the items of the XMLList.</param>
        /// <param name="length">The number of items in the XMLList.</param>
        /// <param name="noCopy">If set to true, use the <paramref name="items"/> array directly
        /// instead of making a copy.</param>
        /// <param name="targetObject">If this XMLList must be linked to a target object, set this
        /// to the target object. Otherwise set this to null.</param>
        /// <param name="targetName">If this XMLList must be linked to a particuar property
        /// name on the target object, pass the generalized name to this argument. Only applicable
        /// if <paramref name="targetObject"/> is not null.</param>
        internal ASXMLList(
            ASXML[] items, int length, bool noCopy, ASObject targetObject, in XMLGenName targetName)
        {
            _initItems(items, length, noCopy);
            if (targetObject != null)
                m_link = new LinkInfo(targetObject, targetName.uri, targetName.localName, targetName.isAttr);
        }

        private void _initItems(ASXML[] items, int length, bool noCopy) {
            if (length == 0)
                return;

            if (items == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(items));

            if (length < 0)
                length = items.Length;

            if (noCopy) {
                m_items = new DynamicArray<ASXML>(items, length);
                return;
            }

            m_items = new DynamicArray<ASXML>(items.Length, true);
            for (int i = 0; i < items.Length; i++) {
                if (items[i] == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, $"{nameof(items)}[{i}]");
                m_items[i] = items[i];
            }
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This method is called internally from the AVM2 and from code compiled with the
        /// ABCIL compiler to invoke the ActionScript XMLList constructor. This method must not
        /// be used by .NET code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            ASAny arg = (args.Length == 0) ? ASAny.undefined : args[0];

            if (arg.value == null)
                return new ASXMLList();

            if (arg.value is ASXMLList argXmlList)
                return argXmlList;

            if (arg.value is ASXML argXml)
                return fromXML(argXml);

            return parse(ASAny.AS_convertString(arg));
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This method is called internally from the AVM2 and from code compiled with the
        /// ABCIL compiler to invoke the ActionScript XMLList constructor. This method must not
        /// be used by .NET code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) {
            ASAny arg = (args.Length == 0) ? ASAny.undefined : args[0];

            if (arg.value is ASXMLList argXmlList)
                return shallowCopy(argXmlList);

            return __AS_INVOKE(args);
        }

        /// <summary>
        /// Converts an XML instance to an XMLList instance.
        /// </summary>
        /// <returns>The converted XMLList. This will be a list with <paramref name="xml"/> as
        /// a single item.</returns>
        /// <param name="xml">The XML instance to convert to XMLList.</param>
        public static ASXMLList fromXML(ASXML xml) {
            if (xml == null)
                return new ASXMLList();

            ASXML[] arr = {xml};
            ASQName qname = xml.internalGetName();
            bool isAttr = xml.nodeType == XMLNodeType.ATTRIBUTE;

            if (qname == null)
                return new ASXMLList(arr, 1, true, xml.parent(), isAttr);

            XMLGenName targetName = XMLGenName.qualifiedName(qname.uri, qname.localName, isAttr);
            return new ASXMLList(arr, 1, true, xml.parent(), targetName);
        }

        /// <summary>
        /// Returns a shallow copy of the given XMLList instance.
        /// </summary>
        /// <param name="list">An XMLList instance from which to create a shallow copy.</param>
        /// <returns>A shallow copy of <paramref name="list"/>, or an empty XMLList if
        /// <paramref name="list"/> is null.</returns>
        public static ASXMLList shallowCopy(ASXMLList list) {
            if (list == null)
                return new ASXMLList();

            return new ASXMLList(list.getItems().asSpan());
        }

        /// <summary>
        /// Returns the item of this XMLList at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The item of this XMLList at the index <paramref name="index"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>ArgumentError #10061</term>
        /// <description><paramref name="index"/> is negative or greater than or equal to the number
        /// of items in this XMLList.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASXML this[int index] {
            get {
                if ((uint)index >= (uint)m_items.length)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(index));
                return m_items[index];
            }
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{ASXML}"/> containing the items of this XMLList.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ASXML}"/> containing the items of this
        /// XMLList.</returns>
        /// <remarks>
        /// The instance returned is a direct view of the internal array of this XMLList.
        /// If the length of this XMLList is changed, the view may no longer be valid and
        /// any further changes may not be visible.
        /// </remarks>
        public ReadOnlyArrayView<ASXML> getItems() => m_items.asReadOnlyArrayView();

        /// <summary>
        /// Parses the given string into an XMLList.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <returns>The list of XML nodes parsed from the given string.</returns>
        public static ASXMLList parse(string str) {
            if (str == null || str.Length == 0)
                return new ASXMLList();
            XMLParser parser = new XMLParser();
            return parser.parseList(str);
        }

        /// <summary>
        /// Performs a value equality test between two XMLList objects. This method checks for the
        /// equality of the nodes at each index in both the lists, using the
        /// <see cref="ASXML.deepEquals" qualifyHint="true"/> method.
        /// </summary>
        ///
        /// <param name="list1">The first operand.</param>
        /// <param name="list2">The second operand.</param>
        /// <returns>True if <paramref name="list1"/> is equal to <paramref name="list2"/>,
        /// otherwise false.</returns>
        ///
        /// <remarks>
        /// The comparison done by this method is different from the equality operator in ECMA-357, in
        /// the sense that simple-content nodes are not converted to strings and are compared directly
        /// as XML nodes. For a comparison using ECMA-357 definition, use the <see cref="AS_weakEq"/>.
        /// method.
        /// </remarks>
        public static bool deepEquals(ASXMLList list1, ASXMLList list2) {
            if (list1 == list2)
                return true;
            if (list1 == null || list2 == null || list1.m_items.length != list2.m_items.length)
                return false;

            ref var arr1 = ref list1.m_items;
            ref var arr2 = ref list2.m_items;
            for (int i = 0, n = list1.m_items.length; i < n; i++) {
                if (!ASXML.deepEquals(arr1[i], arr2[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Performs a value equality test between two XMLList objects, using the definition of the
        /// weak equality (==) operator in ECMA-357.
        /// </summary>
        /// <param name="list1">The first XMLList.</param>
        /// <param name="list2">The second XMLList.</param>
        /// <returns>True if <paramref name="list1"/> is equal to <paramref name="list2"/>
        /// according to the definition of the weak equality operator, otherwise false.</returns>
        public static bool AS_weakEq(ASXMLList list1, ASXMLList list2) {
            if (list1 == list2)
                return true;
            if (list1 == null || list2 == null || list1.m_items.length != list2.m_items.length)
                return false;

            ref var arr1 = ref list1.m_items;
            ref var arr2 = ref list2.m_items;
            for (int i = 0, n = list1.m_items.length; i < n; i++) {
                if (!ASXML.AS_weakEq(arr1[i], arr2[i]))
                    return false;
            }
            return true;
        }

        #region Property binding methods

        /// <inheritdoc/>
        public override bool AS_hasProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;
            if (name.ns.kind != NamespaceKind.NAMESPACE && name.ns.kind != NamespaceKind.ANY)
                return false;

            var genName = XMLGenName.fromQName(name, options);

            if (genName.isIndex)
                return (uint)genName.index < (uint)m_items.length;

            for (int i = 0, n = m_items.length; i < n; i++) {
                if (m_items[i].internalFindNodeByGenName(genName) != null)
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override bool AS_hasProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromMultiname(name, nsSet, options);

            if (genName.isIndex)
                return (uint)genName.index < (uint)m_items.length;

            for (int i = 0, n = m_items.length; i < n; i++) {
                if (m_items[i].internalFindNodeByGenName(genName) != null)
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetProperty(
            in QName name, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            value = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromQName(name, options);

            if (name.ns.kind != NamespaceKind.NAMESPACE && name.ns.kind != NamespaceKind.ANY) {
                value = new ASXMLList(Array.Empty<ASXML>(), 0, true, this, genName.isAttr);
                return BindStatus.SUCCESS;
            }

            if (genName.isIndex) {
                value = ((uint)genName.index < (uint)m_items.length) ? m_items[genName.index] : default(ASAny);
                return BindStatus.SUCCESS;
            }

            var resultList = new ASXMLList(Array.Empty<ASXML>(), 0, true, this, genName);
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref resultList.m_items);

            value = resultList;
            return (resultList.m_items.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetProperty(
            string name, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            value = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromMultiname(name, nsSet, options);

            if (genName.isIndex) {
                value = ((uint)genName.index < (uint)m_items.length) ? m_items[genName.index] : default(ASAny);
                return BindStatus.SUCCESS;
            }

            var resultList = new ASXMLList(Array.Empty<ASXML>(), 0, true, this, genName);
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref resultList.m_items);

            value = resultList;
            return (resultList.m_items.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromQName(name, options);
            _internalSetPropGenName(genName, value);
            return BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetProperty(
            string name, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromMultiname(name, nsSet, options);
            _internalSetPropGenName(genName, value);
            return BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override bool AS_deleteProperty(
            in QName name, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromQName(name, options);
            return _internalDeletePropGenName(genName);
        }

        /// <inheritdoc/>
        public override bool AS_deleteProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromMultiname(name, nsSet, options);
            return _internalDeletePropGenName(genName);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetDescendants(
            in QName name, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            result = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            if (name.ns.kind != NamespaceKind.NAMESPACE && name.ns.kind != NamespaceKind.ANY) {
                result = new ASXMLList();
                return BindStatus.SUCCESS;
            }

            var genName = XMLGenName.fromQName(name, options);
            if (genName.isIndex) {
                result = new ASXMLList();
                return BindStatus.SUCCESS;
            }

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchDescByGenName(genName, ref list);

            result = new ASXMLList(list.getUnderlyingArray(), list.length, true);
            return (list.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetDescendants(
            string name, in NamespaceSet nsSet, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            result = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromMultiname(name, nsSet, options);
            if (genName.isIndex) {
                result = new ASXMLList();
                return BindStatus.SUCCESS;
            }

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchDescByGenName(genName, ref list);

            result = new ASXMLList(list.getUnderlyingArray(), list.length, true);
            return (list.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override bool AS_hasPropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromObject(key, options);
            if (genName.isIndex)
                return (uint)genName.index < (uint)m_items.length;

            for (int i = 0, n = m_items.length; i < n; i++) {
                if (m_items[i].internalFindNodeByGenName(genName) != null)
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override bool AS_hasPropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromObjectMultiname(key, nsSet, options);
            if (genName.isIndex)
                return (uint)genName.index < (uint)m_items.length;

            for (int i = 0, n = m_items.length; i < n; i++) {
                if (m_items[i].internalFindNodeByGenName(genName) != null)
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetPropertyObj(
            ASAny key, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            value = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromObject(key, options);

            if (genName.isIndex) {
                value = ((uint)genName.index < (uint)m_items.length) ? m_items[genName.index] : default(ASAny);
                return BindStatus.SUCCESS;
            }

            var list = new DynamicArray<ASXML>(genName.isAttr ? 1 : 0);
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref list);

            value = new ASXMLList(list.getUnderlyingArray(), list.length, true, this, genName);
            return (list.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetPropertyObj(
            ASAny key, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            value = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromObjectMultiname(key, nsSet, options);

            if (genName.isIndex) {
                value = ((uint)genName.index < (uint)m_items.length) ? m_items[genName.index] : default(ASAny);
                return BindStatus.SUCCESS;
            }

            DynamicArray<ASXML> list = new DynamicArray<ASXML>(genName.isAttr ? 1 : 0);
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref list);

            value = new ASXMLList(list.getUnderlyingArray(), list.length, true, this, genName);
            return (list.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetPropertyObj(
            ASAny key, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromObject(key, options);
            _internalSetPropGenName(genName, value);
            return BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetPropertyObj(
            ASAny key, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromObjectMultiname(key, nsSet, options);
            _internalSetPropGenName(genName, value);
            return BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override bool AS_deletePropertyObj(
            ASAny key, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromObject(key, options);
            return _internalDeletePropGenName(genName);
        }

        /// <inheritdoc/>
        public override bool AS_deletePropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;

            var genName = XMLGenName.fromObjectMultiname(key, nsSet, options);
            return _internalDeletePropGenName(genName);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetDescendantsObj(
            ASAny key, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            result = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromObject(key, options);

            if (genName.isIndex) {
                result = new ASXMLList();
                return BindStatus.SUCCESS;
            }

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchDescByGenName(genName, ref list);

            result = new ASXMLList(list.getUnderlyingArray(), list.length, true);
            return (list.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetDescendantsObj(
            ASAny key, in NamespaceSet nsSet, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            result = default(ASAny);

            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return BindStatus.NOT_FOUND;

            var genName = XMLGenName.fromObjectMultiname(key, nsSet, options);

            if (genName.isIndex) {
                result = new ASXMLList();
                return BindStatus.SUCCESS;
            }

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchDescByGenName(genName, ref list);

            result = new ASXMLList(list.getUnderlyingArray(), list.length, true);
            return (list.length == 0) ? BindStatus.SOFT_SUCCESS : BindStatus.SUCCESS;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            // ECMA-357, sec. 11.2.2.1 (CallMethod) says that if the binding fails
            // and the XMLList has only one item, the call must be delegated to it.
            // However, since AS3 provides wrapper methods on XMLList for all XML methods,
            // delegating a failed call binding on an XMLList to an XML would be unnecessary.
            // But there's an exception: when the only item in the list has simple content,
            // it must be converted to a string and the call delegated to it.
            BindStatus bindStatus = base.AS_tryCallProperty(name, args, out result, options);
            if (bindStatus == BindStatus.NOT_FOUND && m_items.length == 1 && m_items[0].hasSimpleContent()) {
                string str = m_items[0].internalSimpleToString();
                return ASObject.AS_fromString(str).AS_tryCallProperty(name, args, out result, options);
            }
            return bindStatus;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = base.AS_tryCallProperty(name, nsSet, args, out result, options);
            if (bindStatus == BindStatus.NOT_FOUND && m_items.length == 1 && m_items[0].hasSimpleContent()) {
                string str = m_items[0].internalSimpleToString();
                return ASObject.AS_fromString(str).AS_tryCallProperty(name, nsSet, args, out result, options);
            }
            return bindStatus;
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            return (key.value is ASQName qName)
                ? AS_tryCallProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryCallProperty(QName.publicName(ASAny.AS_convertString(key)), args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            return (key.value is ASQName qName)
                ? AS_tryCallProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryCallProperty(ASAny.AS_convertString(key), nsSet, args, out result, options);
        }

        /// <inheritdoc/>
        public override int AS_nextIndex(int index) =>
            ((uint)index < (uint)m_items.length) ? index + 1 : 0;

        /// <inheritdoc/>
        public override ASAny AS_nameAtIndex(int index) => (ASAny)(index - 1);

        /// <inheritdoc/>
        public override ASAny AS_valueAtIndex(int index) =>
            (index > 0 && index <= m_items.length) ? m_items[index] : default(ASAny);

        /// <summary>
        /// Gets the item of this XMLList at the given index.
        /// </summary>
        /// <returns>The value at the given index. This is an XML object, or undefined if
        /// <paramref name="index"/> is out of bounds.</returns>
        /// <param name="index">The index.</param>
        public ASAny AS_getElement(int index) {
            if (index < 0)
                return AS_getPropertyObj(index);
            if (index >= m_items.length)
                return ASAny.undefined;
            return m_items[index];
        }

        /// <summary>
        /// Gets the item of this XMLList at the given index.
        /// </summary>
        /// <returns>The value at the given index. This is an XML object, or undefined if
        /// <paramref name="index"/> is out of bounds.</returns>
        /// <param name="index">The index.</param>
        public ASAny AS_getElement(uint index) {
            if (index >= (uint)m_items.length)
                return ASAny.undefined;
            return m_items[(int)index];
        }

        /// <summary>
        /// Gets the item of this XMLList at the given index.
        /// </summary>
        /// <returns>The value at the given index. This is an XML object, or undefined if
        /// <paramref name="index"/> is out of bounds.</returns>
        /// <param name="index">The index.</param>
        public ASAny AS_getElement(double index) {
            uint uintIndex = (uint)index;
            if (index != (double)uintIndex)
                return AS_getPropertyObj(index);
            if (uintIndex >= (uint)m_items.length)
                return ASAny.undefined;
            return m_items[(int)uintIndex];
        }

        /// <summary>
        /// Sets the item of this XMLList at the given index.
        /// </summary>
        /// <returns>The value at the given index. This is an XML object, or undefined if
        /// <paramref name="index"/> is out of bounds.</returns>
        /// <param name="index">The index.</param>
        /// <param name="value">The value to assign to the index <paramref name="index"/></param>
        public void AS_setElement(int index, ASAny value) {
            if (index < 0)
                AS_setPropertyObj(index, value);
            else
                _internalSetPropIndex(index, value);
        }

        /// <summary>
        /// Sets the item of this XMLList at the given index.
        /// </summary>
        /// <returns>The value at the given index. This is an XML object, or undefined if
        /// <paramref name="index"/> is out of bounds.</returns>
        /// <param name="index">The index.</param>
        /// <param name="value">The value to assign to the index <paramref name="index"/></param>
        public void AS_setElement(uint index, ASAny value) =>
            _internalSetPropIndex((int)Math.Min(index, (uint)Int32.MaxValue), value);

        /// <summary>
        /// Sets the item of this XMLList at the given index.
        /// </summary>
        /// <returns>The value at the given index. This is an XML object, or undefined if
        /// <paramref name="index"/> is out of bounds.</returns>
        /// <param name="index">The index.</param>
        /// <param name="value">The value to assign to the index <paramref name="index"/></param>
        public void AS_setElement(double index, ASAny value) {
            uint uintIndex = (uint)index;
            if (index != (double)uintIndex)
                AS_setPropertyObj(index, value);
            else
                AS_setElement(uintIndex, value);
        }

        /// <summary>
        /// Deletes the item of this XMLList at the given index.
        /// </summary>
        /// <returns>This method always returns true.</returns>
        /// <param name="index">The index at which to delete the item.</param>
        public bool AS_deleteElement(int index) {
            if (index < 0)
                return AS_deletePropertyObj(index);
            return _internalDeletePropIndex(index);
        }

        /// <summary>
        /// Deletes the item of this XMLList at the given index.
        /// </summary>
        /// <returns>This method always returns true.</returns>
        /// <param name="index">The index at which to delete the item.</param>
        public bool AS_deleteElement(uint index) {
            if (index > (uint)m_items.length)
                return true;
            return _internalDeletePropIndex((int)index);
        }

        #endregion

        #region Internal methods

        private bool _internalDeletePropGenName(in XMLGenName genName) {
            if (genName.isIndex)
                return _internalDeletePropIndex(genName.index);

            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalDeletePropGenName(genName);

            return true;
        }

        private bool _internalDeletePropIndex(int index) {
            if (index >= m_items.length)
                return true;

            ASXML nodeToDelete = m_items[index];
            m_items.removeRange(index, 1);

            ASXML parent = nodeToDelete.parent();
            if (parent != null)
                parent.internalDeleteChildOrAttr(nodeToDelete);

            return true;
        }

        private void _internalSetPropGenName(in XMLGenName genName, ASAny value) {
            if (genName.isIndex) {
                _internalSetPropIndex(genName.index, value);
                return;
            }

            if (m_items.length == 0) {
                if (_internalResolveValue() is ASXML resolvedValue)
                    m_items.add(resolvedValue);
            }

            if (m_items.length == 1)
                m_items[0].internalSetPropGenName(genName, value);
        }

        /// <summary>
        /// Assigns the given value to a numeric index property of this XMLList.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value to assign to the given index.</param>
        private void _internalSetPropIndex(int index, ASAny value) {
            ASXML resolvedValue = null;
            ASXML appendAfterNode = null;
            string assignText = null;
            bool isAppend = false;

            if (index >= m_items.length) {
                if (m_link == null || m_link.target == null) {
                    // Fast path for an unlinked XMLList (no target object)
                    assignText = _getTextForAssignment(value);
                    if (assignText != null) {
                        m_items.add(ASXML.createNode(XMLNodeType.TEXT, null, assignText));
                        return;
                    }

                    if (value.value is ASXMLList valueXmlList) {
                        for (int i = 0, n = valueXmlList.m_items.length; i < n; i++)
                            m_items.add(valueXmlList.m_items[i]);
                    }
                    else {
                        m_items.add((ASXML)value.value);
                    }
                    return;
                }

                index = m_items.length;
                isAppend = true;

                // Call ResolveValue on the target object. Since this implementation of ResolveValue
                // returns the element of a single-element XMLList, we only need to cast the result
                // to XML to check if it is valid.
                resolvedValue = (m_link.target is ASXMLList targetXmlList)
                    ? targetXmlList._internalResolveValue() as ASXML
                    : m_link.target as ASXML;

                if (resolvedValue == null || resolvedValue.nodeType != XMLNodeType.ELEMENT)
                    return;

                if (m_link.isAttribute) {
                    // If this XMLList is linked to an attribute name, ensure that the attribute
                    // does not exist and create the attribute node on the target object.
                    if (m_link.localName == null)
                        return;

                    foreach (ASXML attr in resolvedValue.getAttributeEnumerator()) {
                        ASQName attrName = attr.internalGetName();
                        if (attrName.localName == m_link.localName
                            && (m_link.uri == null || m_link.uri == attrName.uri))
                        {
                            return;
                        }
                    }

                    ASXML newAttr = resolvedValue.createChildNode(
                        XMLNodeType.ATTRIBUTE, new ASQName(m_link.uri, m_link.localName));
                    if (newAttr == null)
                        return;
                    m_items.add(newAttr);
                }
                else {
                    // Determine the location where the new node must be inserted into the
                    // target object's child list. If the last element of this XMLList is a
                    // child of the target object, the new node must be inserted after it.
                    if (m_items.length != 0) {
                        ASXML lastItem = m_items[m_items.length - 1];
                        foreach (ASXML c in resolvedValue.getChildEnumerator()) {
                            if (lastItem == c) {
                                appendAfterNode = c;
                                break;
                            }
                        }
                    }

                    if (appendAfterNode == null)
                        // Insert after the last child.
                        appendAfterNode = resolvedValue.getChildAtIndex(-1);

                    // If a target property local name exists and the value being assigned
                    // is a text node or a primitive value, create a child element into which
                    // the text will be inserted.

                    assignText = _getTextForAssignment(value);

                    if (m_link.localName != null && assignText != null) {
                        ASQName newElemName;

                        if (value.value is ASXML valueXml){
                            newElemName = valueXml.internalGetName();
                        }
                        else if (value.value is ASXMLList valueXmlList
                            && valueXmlList.m_link != null && valueXmlList.m_link.localName != null)
                        {
                            newElemName = new ASQName(valueXmlList.m_link.uri, valueXmlList.m_link.localName);
                        }
                        else {
                            newElemName = new ASQName(m_link.uri, m_link.localName);
                        }

                        ASXML newElem = ASXML.createNode(XMLNodeType.ELEMENT, newElemName);
                        if (newElem == null)
                            return;

                        resolvedValue.internalInsertChildAfter(appendAfterNode, newElem, false);
                        m_items.add(newElem);
                    }
                }
            }

            if (index < m_items.length && m_items[index].nodeType == XMLNodeType.ATTRIBUTE) {
                // Assign to an attribute.
                m_items[index].nodeText = XMLHelper.objectToAttrString(value);
                return;
            }

            if (!isAppend && assignText == null)
                assignText = _getTextForAssignment(value);

            if (assignText != null)
                _internalAssignIndexString(index, assignText, resolvedValue, appendAfterNode);
            else if (value.value is ASXMLList valueXmlList)
                _internalAssignIndexXMLList(index, valueXmlList, resolvedValue, appendAfterNode);
            else
                _internalAssignIndexXML(index, (ASXML)value.value, resolvedValue, appendAfterNode);
        }

        private ASObject _internalResolveValue() {
            if (m_items.length != 0)
                return (m_items.length == 1) ? (ASObject)m_items[0] : this;

            if (m_link == null || m_link.isAttribute || m_link.localName == null)
                return null;

            ASObject baseObj = (m_link.target is ASXMLList targetObjXmlList)
                ? targetObjXmlList._internalResolveValue()
                : m_link.target;    // [[ResolveValue]] on an XML returns the object itself.

            if (baseObj == null)
                return null;

            QName qname = new QName(m_link.uri, m_link.localName);
            BindOptions bindOptions = BindOptions.SEARCH_DYNAMIC;
            if (m_link.isAttribute)
                bindOptions |= BindOptions.ATTRIBUTE;

            if (!(baseObj.AS_getProperty(qname, bindOptions).value is ASXMLList target))
                return null;

            if (target.m_items.length != 0)
                return (target.m_items.length == 1) ? (ASObject)target.m_items[0] : target;

            ASXML baseNode = null;

            if (baseObj is ASXMLList baseXmlList) {
                if (baseXmlList.m_items.length > 1)
                    return null;
                if (baseXmlList.m_items.length == 1)
                    baseNode = baseXmlList.m_items[0];
            }
            else {
                baseNode = baseObj as ASXML;
            }

            if (baseNode != null) {
                // Optimize this case.
                XMLNodeType nodeType = m_link.isAttribute ? XMLNodeType.ATTRIBUTE : XMLNodeType.ELEMENT;
                return baseNode.createChildNode(nodeType, new ASQName(m_link.uri, m_link.localName));
            }

            baseObj.AS_setProperty(qname, "", bindOptions);
            target = baseObj.AS_getProperty(qname, bindOptions).value as ASXMLList;

            return (target != null && target.m_items.length == 1) ? target.m_items[0] : (ASObject)target;
        }

        /// <summary>
        /// Returns the string for a primitive string assignment, or null if the given value
        /// is not a primitive.
        /// </summary>
        /// <returns>The primitive string, or null if <paramref name="obj"/> is not a primitive
        /// value, an XML text or attribute node, or an XMLList containing exactly one such node.</returns>
        /// <param name="obj">An ActionScript object.</param>
        private string _getTextForAssignment(ASAny obj) {
            ASXML xml = obj.value as ASXML;
            ASXMLList xmlList = obj.value as ASXMLList;

            if (xmlList != null && xmlList.length() == 1) {
                xml = xmlList[0];
                xmlList = null;
            }

            if (xml != null)
                return (xml.nodeType == XMLNodeType.TEXT || xml.nodeType == XMLNodeType.ATTRIBUTE) ? xml.nodeText : null;

            if (xmlList != null)
                return null;

            return ASAny.AS_convertString(obj);
        }

        /// <summary>
        /// Assigns a primitive string to the given index in this XMLList.
        /// </summary>
        /// <param name="index">The index to be assigned to.</param>
        /// <param name="str">The string value to be assigned to <paramref name="index"/>.</param>
        /// <param name="appendTarget">An append target, used if an item must be added to this
        /// list. Pass null if no append target is needed.</param>
        /// <param name="appendAfter">If an append target is given, the child of the append target
        /// after which the added node must be inserted into it.</param>
        private void _internalAssignIndexString(int index, string str, ASXML appendTarget, ASXML appendAfter) {
            ASXML nodeToAssign = (index == m_items.length) ? null : m_items[index];

            if (nodeToAssign != null && nodeToAssign.nodeType == XMLNodeType.ELEMENT) {
                nodeToAssign.internalClear();
                if (str.Length != 0)
                    nodeToAssign.createChildNode(XMLNodeType.TEXT, null, str);
                return;
            }

            ASXML newNode = ASXML.createNode(XMLNodeType.TEXT, null, str);
            if (nodeToAssign != null) {
                if (nodeToAssign.parent() != null)
                    newNode = nodeToAssign.parent().internalReplaceChild(nodeToAssign, newNode, false);
                m_items[index] = newNode;
            }
            else {
                if (appendTarget != null)
                    appendTarget.internalInsertChildAfter(appendAfter, newNode, false);
                m_items.add(newNode);
            }
        }

        /// <summary>
        /// Assigns an XML object to the given index in this XMLList.
        /// </summary>
        /// <param name="index">The index to be assigned to.</param>
        /// <param name="xml">The XML object to be assigned to <paramref name="index"/>.</param>
        /// <param name="appendTarget">An append target, used if an item must be added to this
        /// list. Pass null if no append target is needed.</param>
        /// <param name="appendAfter">If an append target is given, the child of the append target
        /// after which the added node must be inserted into it.</param>
        private void _internalAssignIndexXML(int index, ASXML xml, ASXML appendTarget, ASXML appendAfter) {
            ASXML nodeToAssign = (index == m_items.length) ? null : m_items[index];

            if (nodeToAssign != null) {
                if (nodeToAssign.parent() != null)
                    xml = nodeToAssign.parent().internalReplaceChild(nodeToAssign, xml, true);
                m_items[index] = xml;
            }
            else {
                if (appendTarget != null)
                    xml = appendTarget.internalInsertChildAfter(appendAfter, xml, true);
                m_items.add(xml);
            }
        }

        /// <summary>
        /// Assigns an XMLList object to the given index in this XMLList.
        /// </summary>
        /// <param name="index">The index to be assigned to.</param>
        /// <param name="xmlList">The XMLList object to be assigned to <paramref name="index"/>.</param>
        /// <param name="appendTarget">An append target, used if an item must be added to this
        /// list. Pass null if no append target is needed.</param>
        /// <param name="appendAfter">If an append target is given, the child of the append target
        /// after which the added node must be inserted into it.</param>
        private void _internalAssignIndexXMLList(int index, ASXMLList xmlList, ASXML appendTarget, ASXML appendAfter) {
            int nNewItems = xmlList.m_items.length;

            if (nNewItems == 0) {
                _internalDeletePropIndex(index);
                return;
            }

            if (nNewItems == 1) {
                _internalAssignIndexXML(index, xmlList.m_items[0], appendTarget, appendAfter);
                return;
            }

            if (index == m_items.length && appendTarget == null) {
                for (int i = 0; i < nNewItems; i++)
                    m_items.add(xmlList.m_items[i]);
                return;
            }

            ASXML nodeToAssign = (index == m_items.length) ? null : m_items[index];
            Span<ASXML> newItems;

            if (nodeToAssign != null && nodeToAssign.parent() != null) {
                newItems = xmlList.m_items.toArray(true);
                nodeToAssign.parent().internalReplaceChild(nodeToAssign, newItems, true);
            }
            else if (nodeToAssign == null && appendTarget != null) {
                newItems = new ASXML[nNewItems];
                for (int i = 0; i < nNewItems; i++) {
                    ASXML newItem = appendTarget.internalInsertChildAfter(appendAfter, xmlList.m_items[i], true);
                    newItems[i] = newItem;
                    appendAfter = newItem;
                }
            }
            else {
                newItems = xmlList.m_items.asSpan();
            }

            if (index == m_items.length) {
                m_items.addDefault(nNewItems);
            }
            else {
                int shiftSize = m_items.length - index - 1;
                m_items.addDefault(nNewItems - 1);
                m_items.asSpan(index + 1, shiftSize).CopyTo(m_items.asSpan(index + nNewItems, shiftSize));
            }

            newItems.CopyTo(m_items.asSpan(index, nNewItems));
        }

        #endregion

        /// <summary>
        /// Adds a namespace declaration to the list of namespace prefix declarations for this node.
        /// This method can only be called on an XMLList having exactly one item.
        /// </summary>
        /// <param name="ns">The namespace to add to the list of prefix declarations. This must be a
        /// Namespace or QName object. If this is a QName, the namespace of that QName is used. The
        /// namespace must have a non-null prefix.</param>
        /// <returns>This method always returns the instance on which it is called.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.addNamespace" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXML addNamespace(ASAny ns) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(addNamespace));
            return m_items[0].addNamespace(ns);
        }

        /// <summary>
        /// Adds a new child node at the end of this XML node's child list. This method can only be
        /// called on an XMLList having exactly one item.
        /// </summary>
        /// <param name="value">The child node to append.</param>
        /// <returns>This method always returns the instance on which it is called.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.appendChild" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXML appendChild(ASAny value) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(appendChild));
            return m_items[0].appendChild(value);
        }

        /// <summary>
        /// Returns an XMLList containing all the attribute(s) of all element nodes in this list that
        /// match the given name.
        /// </summary>
        /// <param name="name">A QName or a string.</param>
        /// <returns>An XMLList containing all the attribute(s) of this node that match the name
        /// <paramref name="name"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1010</term>
        /// <description><paramref name="name"/> is null or undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.attribute" qualifyHint="true"/> method on
        /// each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList attribute(ASAny name) {
            if (name.value == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            var genName = XMLGenName.fromObjectAttrName(name);
            DynamicArray<ASXML> attrList = new DynamicArray<ASXML>(
                (genName.uri == null && genName.localName == null) ? 0 : 1);

            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref attrList);
            return new ASXMLList(attrList.getUnderlyingArray(), attrList.length, true, this, genName);
        }

        /// <summary>
        /// Returns an XMLList containing all the attributes of all element nodes in this list.
        /// </summary>
        /// <returns>An XMLList containing all the attributes of all element nodes in this
        /// list.</returns>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.attributes" qualifyHint="true"/> method on
        /// each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList attributes() {
            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            XMLGenName genName = XMLGenName.anyAttribute();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref list);
            return new ASXMLList(list.getUnderlyingArray(), list.length, true, this, true);
        }

        /// <summary>
        /// Returns an XMLList containing the child nodes of all elements of this list that match the
        /// given name.
        /// </summary>
        /// <param name="name">A string, QName or integer index.</param>
        /// <returns>An XMLList containing all the child nodes of all elements of this list that match
        /// the given name.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1010</term>
        /// <description><paramref name="name"/> is null or undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.child" qualifyHint="true"/> method on each
        /// item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList child(ASAny name) {
            if (name.value == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            var genName = XMLGenName.fromObject(name, 0);
            DynamicArray<ASXML> list = new DynamicArray<ASXML>();

            if (genName.isIndex) {
                if (!genName.isAttr) {
                    for (int i = 0, n = m_items.length; i < n; i++) {
                        ASXML childAtIndex = m_items[i].getChildAtIndex(genName.index);
                        if (childAtIndex != null)
                            list.add(childAtIndex);
                    }
                }
            }
            else {
                for (int i = 0, n = m_items.length; i < n; i++)
                    m_items[i].internalFetchNodesByGenName(genName, ref list);
            }

            return new ASXMLList(list.getUnderlyingArray(), list.length, true, this, genName);
        }

        /// <summary>
        /// Returns an XMLList containing all the children of all element nodes in this list.
        /// </summary>
        /// <returns>An XMLList containing all the children of all element nodes in this
        /// list.</returns>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.children" qualifyHint="true"/> method on
        /// each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList children() {
            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            XMLGenName genName = XMLGenName.anyChild();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref list);
            return new ASXMLList(list.getUnderlyingArray(), list.length, true, this);
        }

        /// <summary>
        /// Returns an XMLList containing all the children of all element nodes in the XMLList that are
        /// comments.
        /// </summary>
        /// <returns>An XMLList containing all the children of all element nodes in the XMLList that are
        /// comments.</returns>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.comments" qualifyHint="true"/> method on
        /// each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList comments() {
            DynamicArray<ASXML> commentsList = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByType(XMLNodeType.COMMENT, ref commentsList);
            return new ASXMLList(commentsList.getUnderlyingArray(), commentsList.length, true, this);
        }

        /// <summary>
        /// Gets the zero-based index of the current node with respect to its parent. This method can
        /// only be called on an XMLList containing exactly one item.
        /// </summary>
        /// <returns>The zero-based index of the current node in the child list of its parent. If the
        /// node does not have a parent, or is an attribute node, this method returns -1.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.childIndex" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int childIndex() {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(childIndex));
            return m_items[0].childIndex();
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the XMLList instance contains the given value.
        /// This method uses the weak equality operator (==) for comparison.
        /// </summary>
        /// <param name="value">The item to compare against the items of this list.</param>
        /// <returns>True if the XMLList contains an item that compares equal to
        /// <paramref name="value"/> using the definition of the weak equality operator, otherwise
        /// false.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public bool contains(ASAny value) {
            for (int i = 0, n = m_items.length; i < n; i++) {
                if (ASAny.AS_weakEq(m_items[i], value))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a deep copy of this XMLList instance. The returned XMLList will contain deep
        /// copies of each node in the this XMLList.
        /// </summary>
        /// <returns>A deep copy of this XMLList instance. The returned copy is always unlinked even
        /// if the calling XMLList is linked.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList copy() {
            ASXMLList newList = new ASXMLList();
            newList.m_items.setCapacity(m_items.length);
            for (int i = 0, n = m_items.length; i < n; i++)
                newList.m_items.add(m_items[i].copy());
            return newList;
        }

        /// <summary>
        /// Returns an XMLList containing all the nodes matching the given name, which are descendants
        /// of all element nodes in this list.
        /// </summary>
        /// <param name="name">A QName or string.</param>
        /// <returns>An XMLList containing all the nodes matching the given name, which are
        /// descendants of all element nodes in this list.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1010</term>
        /// <description><paramref name="name"/> is null or undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.descendants" qualifyHint="true"/> method on
        /// each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList descendants([ParamDefaultValue("*")] ASAny name) {
            if (name.value == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            var genName = XMLGenName.fromObject(name, 0);

            if (genName.isIndex)
                return new ASXMLList();

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchDescByGenName(genName, ref list);
            return new ASXMLList(list.getUnderlyingArray(), list.length, true);
        }

        /// <summary>
        /// Returns an XMLList containing all the child element(s) of all items in this list that
        /// match the given name.
        /// </summary>
        /// <param name="name">A string or QName.</param>
        /// <returns>An XMLList containing all the child element(s) of all items in this list that
        /// match the name <paramref name="name"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1010</term>
        /// <description><paramref name="name"/> is null or undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.elements" qualifyHint="true"/> method on
        /// each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList elements([ParamDefaultValue("*")] ASAny name) {
            if (name.value == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            var genName = XMLGenName.fromObject(name, 0);

            if (genName.index != -1 || genName.isAttr)
                return new ASXMLList(Array.Empty<ASXML>(), 0, true, this);

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();

            if (genName.uri == null && genName.localName == null) {
                for (int i = 0, n = m_items.length; i < n; i++)
                    m_items[i].internalFetchNodesByType(XMLNodeType.ELEMENT, ref list);
            }
            else {
                for (int i = 0, n = m_items.length; i < n; i++)
                    m_items[i].internalFetchNodesByGenName(genName, ref list);
            }

            return new ASXMLList(list.getUnderlyingArray(), list.length, true, this, genName);
        }

        /// <summary>
        /// Gets a value indicating whether this XMLList has complex content.
        /// </summary>
        /// <returns>True if this XMLList has complex content, false otherwise.</returns>
        /// <remarks>
        /// An XMLList has complex content if it is non-empty and either has a single item having
        /// complex content, or has more than one item of which at least one is an element.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public bool hasComplexContent() {
            if (m_items.length == 0)
                return false;
            if (m_items.length == 1)
                return m_items[0].hasComplexContent();

            for (int i = 0, n = m_items.length; i < n; i++) {
                if (m_items[i].nodeType == XMLNodeType.ELEMENT)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a value indicating whether this XMLList has simple content.
        /// </summary>
        /// <returns>True if this XMLList has simple content, false otherwise.</returns>
        /// <remarks>
        /// An XMLList has simple content if it is empty, has a single item having simple content, or
        /// has more than one item of which none are elements.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public bool hasSimpleContent() {
            if (m_items.length == 0)
                return false;
            if (m_items.length == 1)
                return m_items[0].hasSimpleContent();

            for (int i = 0, n = m_items.length; i < n; i++) {
                if (m_items[i].nodeType == XMLNodeType.ELEMENT)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the number of items in the XMLList.
        /// </summary>
        /// <returns>The number of items in the XMLList.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int length() {
            return m_items.length;
        }

        /// <summary>
        /// Returns an Array containing all the namespace declarations available to the current node.
        /// This method can only be called on an XMLList containing exactly one item.
        /// </summary>
        /// <returns>An Array containing <see cref="ASNamespace"/> objects representing all the
        /// namespace declarations available to this node.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.inScopeNamespaces" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASArray inScopeNamespaces() {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(inScopeNamespaces));
            return m_items[0].inScopeNamespaces();
        }

        /// <summary>
        /// Adds a new child node into this XML node's child list after another child node. This
        /// method can only be called on an XMLList containing exactly one item.
        /// </summary>
        ///
        /// <param name="child1">A child node of this node after which to insert
        /// <paramref name="child2"/>. If this is null, the child node is inserted at the beginning
        /// of the child list of this node.</param>
        /// <param name="child2">The child to insert into this node's child list after
        /// <paramref name="child1"/>.</param>
        ///
        /// <returns>
        /// If this node is not an element node, or <paramref name="child1"/> is not null or an XML
        /// object, is an XML instance representing an attribute, or an XML instance representing a
        /// node that is not a child of this node, returns undefined. Otherwise, returns this instance
        /// (on which the method is called).
        /// </returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.insertChildAfter" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny insertChildAfter(ASAny child1, ASAny child2) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(insertChildAfter));
            return m_items[0].insertChildAfter(child1, child2);
        }

        /// <summary>
        /// Adds a new child node into this XML node's child list before another child node. This
        /// method can only be called on an XMLList containing exactly one item.
        /// </summary>
        ///
        /// <param name="child1">A child node of this node before which to insert
        /// <paramref name="child2"/>. If this is null, the child node is inserted at the end of the
        /// child list of this node.</param>
        /// <param name="child2">The child to insert into this node's child list before
        /// <paramref name="child1"/>.</param>
        ///
        /// <returns>
        /// If this node is not an element node, or <paramref name="child1"/> is not null or an XML
        /// object, is an XML instance representing an attribute, or an XML instance representing a
        /// node that is not a child of this node, returns undefined. Otherwise, returns this instance
        /// (on which the method is called).
        /// </returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.insertChildBefore" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny insertChildBefore(ASAny child1, ASAny child2) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(insertChildBefore));
            return m_items[0].insertChildBefore(child1, child2);
        }

        /// <summary>
        /// Gets the local name of this node. This method can only be called on an XMLList containing
        /// exactly one item.
        /// </summary>
        /// <returns>The local name of this node.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.localName" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string localName() {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(localName));
            return m_items[0].localName();
        }

        /// <summary>
        /// Gets the qualified name of this node. This method can only be called on an XMLList
        /// containing exactly one item.
        /// </summary>
        /// <returns>The qualified name of this node.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.name" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASQName name() {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(name));
            return m_items[0].name();
        }

        /// <summary>
        /// Gets the namespace of this node's name, or a namespace declaration with the given prefix.
        /// This method can only be called on an XMLList containing exactly one item.
        /// </summary>
        /// <param name="prefix">If specified, returns the namespace associated with the given prefix,
        /// otherwise returns the namespace of the node's name.</param>
        ///
        /// <returns>
        /// Returns the namespace declared with the given prefix on the node or any of its ancestors,
        /// if <paramref name="prefix"/> is not null; otherwise, returns the namespace of the node's
        /// name, or null if this node is not an element or attribute node. If
        /// <paramref name="prefix"/> is not null and no namespace declaration with that prefix was
        /// found, returns undefined.
        /// </returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.@namespace" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny @namespace(OptionalParam<ASAny> prefix = default) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(@namespace));
            return m_items[0].@namespace(prefix);
        }

        /// <summary>
        /// Gets an array containing all the namespaces declared by this node. This method can only be
        /// called on an XMLList containing exactly one item.
        /// </summary>
        /// <returns>An array containing all the namespace declarations of this node.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.namespaceDeclarations" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASArray namespaceDeclarations() {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(namespaceDeclarations));
            return m_items[0].namespaceDeclarations();
        }

        /// <summary>
        /// Gets a string representing the type of this node. This method can only be called on an
        /// XMLList containing exactly one item.
        /// </summary>
        /// <returns>A string representing the type of this node. This is be one of the following:
        /// "element", "attribute", "text", "comment" or "processing-instruction". (CDATA nodes will
        /// return "text")</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.nodeKind" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string nodeKind() {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(nodeKind));
            return m_items[0].nodeKind();
        }

        /// <summary>
        /// Normalises this XMLList instance, all element nodes in it and all their descendants by
        /// merging adjacent text nodes and removing empty text nodes.
        /// </summary>
        /// <returns>This method always returns the instance on which it is called.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList normalize() {
            DynamicArray<string> tempList = new DynamicArray<string>();
            bool mustCompact = false;

            for (int i = 0, n = m_items.length; i < n; i++) {

                ASXML childNode = m_items[i];

                if (childNode.nodeType != XMLNodeType.TEXT && childNode.nodeType != XMLNodeType.CDATA) {
                    if (childNode.nodeType == XMLNodeType.ELEMENT)
                        childNode.normalize();
                    continue;
                }

                int runNodes = 1;
                int runChars = childNode.nodeText.Length;

                for (int j = i + 1; j < n; j++) {
                    if (m_items[j].nodeType != XMLNodeType.TEXT && m_items[j].nodeType != XMLNodeType.CDATA)
                        break;
                    runNodes++;
                    runChars += m_items[j].nodeText.Length;
                }

                if (runChars == 0) {
                    for (int j = i; j < i + runNodes; j++) {
                        if (m_items[j].parent() != null)
                            m_items[j].parent().internalDeleteChildOrAttr(m_items[j]);
                        m_items[j] = null;
                    }
                    mustCompact = true;
                }
                else if (runNodes != 1) {
                    string concatText;

                    if (runNodes == 2) {
                        concatText = m_items[i].nodeText + m_items[i + 1].nodeText;
                    }
                    else {
                        for (int j = i; j < i + runNodes; j++)
                            tempList.add(m_items[j].nodeText);
                        concatText = String.Join("", tempList.getUnderlyingArray(), 0, tempList.length);
                        tempList.clear();
                    }

                    childNode.nodeText = concatText;

                    for (int j = i + 1; j < i + runNodes; j++) {
                        if (m_items[j].parent() != null)
                            m_items[j].parent().internalDeleteChildOrAttr(m_items[j]);
                        m_items[j] = null;
                    }

                    mustCompact = true;
                }

                i += runNodes - 1;

            }

            if (mustCompact) {
                int newSize = DataStructureUtil.compactArray(m_items.getUnderlyingArray(), m_items.length);
                m_items = new DynamicArray<ASXML>(m_items.getUnderlyingArray(), newSize);
            }

            return this;
        }

        /// <summary>
        /// Gets the common parent element of all items in this XMLList.
        /// </summary>
        /// <returns>The common parent element of all items in this XMLList. If all items in this list
        /// do not have a common parent, or this list is empty, returns undefined.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny parent() {
            if (m_items.length == 0)
                return ASAny.undefined;

            ASXML parentNode = m_items[0].parent();
            for (int i = 1, n = m_items.length; i < n; i++) {
                if (parentNode != m_items[i].parent())
                    return ASAny.undefined;
            }

            return parentNode;
        }

        /// <summary>
        /// Adds a new child node at the beginning of this XML node's child list. This method can only
        /// be called on an XMLList containing exactly one item.
        /// </summary>
        /// <param name="value">The child node to append.</param>
        /// <returns>This method always returns the instance on which it is called.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.prependChild" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXML prependChild(ASAny value) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(prependChild));
            return m_items[0].prependChild(value);
        }

        /// <summary>
        /// Returns an XMLList containing all the children of all element nodes in this list that are
        /// processing instructions with the given name.
        /// </summary>
        /// <param name="name">A string or QName.</param>
        /// <returns>An XMLList containing all the children of all element nodes in this list that are
        /// processing instructions with the given name.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1010</term>
        /// <description><paramref name="name"/> is null or undefined.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The XMLList returned by this method is the same as what would be obtained by concatenating
        /// the results of calling the <see cref="ASXML.processingInstructions" qualifyHint="true"/>
        /// method on each item of this list in order.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList processingInstructions([ParamDefaultValue("*")] ASAny name) {
            if (name.value == null)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            var genName = XMLGenName.fromObjectProcInstrName(name);

            if (genName.uri != null && genName.uri.Length != 0)
                // Since processing instructions can only be in the public namespace, fail early
                // if a QName is provided that has any other namespace.
                return new ASXMLList(Array.Empty<ASXML>(), 0, true, this);

            DynamicArray<ASXML> piList = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByGenName(genName, ref piList);

            return new ASXMLList(piList.getUnderlyingArray(), piList.length, true, this);
        }

        /// <summary>
        /// Removes the given namespace from the namespace declaration list of this node, removing any
        /// prefix associated with it from this node and all its descendants. This method can only be
        /// called on an XMLList containing exactly one item.
        /// </summary>
        ///
        /// <param name="ns">The namespace to remove from the namespace declarations of this node. If
        /// this is a QName, the namespace of that QName will be used. If this is not a Namespace or
        /// QName object, it will be converted to a string and used as the namespace URI.</param>
        /// <returns>This method always returns the instance on which it is called.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.removeNamespace" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXML removeNamespace(ASAny ns) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(removeNamespace));
            return m_items[0].removeNamespace(ns);
        }

        /// <summary>
        /// Replaces any child nodes having a given name or the child node at a given index with a new
        /// node. This method can only be called on an XMLList containing exactly one item.
        /// </summary>
        /// <param name="name">A string, QName or numeric index.</param>
        /// <param name="newValue">The replacement value.</param>
        /// <returns>This method always returns the instance on which it is called.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.replace" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXML replace(ASAny name, ASAny newValue) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(replace));
            return m_items[0].replace(name, newValue);
        }

        /// <summary>
        /// Replaces all existing children of this node with one or more new children. This method can
        /// only be called on an XMLList containing exactly one item.
        /// </summary>
        /// <param name="value">The node to be set as the new child of this node.</param>
        /// <returns>This method always returns the instance on which it is called.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.setChildren" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXML setChildren(ASAny value) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(setChildren));
            return m_items[0].setChildren(value);
        }

        /// <summary>
        /// Changes the local name of this node to the given name. This method can only be called on
        /// an XMLList containing exactly one item.
        /// </summary>
        /// <param name="name">A string or QName.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.setLocalName" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public void setLocalName(ASAny name) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(setLocalName));
            m_items[0].setLocalName(name);
        }

        /// <summary>
        /// Changes the name of this node to the given name. This method can only be called on an
        /// XMLList containing exactly one item.
        /// </summary>
        /// <param name="name">A QName or string to be used as the new name of this node.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.setName" qualifyHint="true" />
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public void setName(ASAny name) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(setName));
            m_items[0].setName(name);
        }

        /// <summary>
        /// Changes the namespace of this node's name to the given namespace. This method can only be
        /// called on an XMLList containing exactly one item.
        /// </summary>
        /// <param name="ns">The namespace to set as this node's namespace. If this is a QName, the
        /// namespace of that QName will be used. If this is not a Namespace or QName object, it will
        /// be converted to a string and used as the namespace URI.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1086</term>
        /// <description>This XMLList does not contain exactly one item.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <seealso cref="ASXML.setNamespace" qualifyHint="true" />
        public void setNamespace(ASAny ns) {
            if (m_items.length != 1)
                throw ErrorHelper.createError(ErrorCode.XML_LIST_METHOD_ONE_ITEM_ONLY, nameof(setNamespace));
            m_items[0].setNamespace(ns);
        }

        /// <summary>
        /// Returns an XMLList containing all the children of all element nodes in this list that are
        /// text nodes.
        /// </summary>
        /// <returns>An XMLList containing all the children of all element nodes in this list that are
        /// text nodes.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASXMLList text() {
            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            for (int i = 0, n = m_items.length; i < n; i++)
                m_items[i].internalFetchNodesByType(XMLNodeType.TEXT, ref list);
            return new ASXMLList(list.getUnderlyingArray(), list.length, true, this);
        }

        /// <summary>
        /// Returns the object that is used in place of the XMLList instance in JSON output. This
        /// method can be redefined at the prototype level.
        /// </summary>
        /// <param name="key">The name of the object property of which this object is the
        /// value.</param>
        /// <returns>The object that is used in place of the XMLList instance in JSON. For a XMLList
        /// object, the default method returns the string "XMLList".</returns>
        //[AVM2ExportTrait(nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny toJSON(ASAny key) {
            return "XMLList";
        }

        /// <summary>
        /// Returns a string representation of the XMLList object.
        /// </summary>
        /// <returns>The string representation of the XMLList object.</returns>
        ///
        /// <remarks>
        /// For XMLList objects that have simple content, this method returns the value of the text
        /// node in it (or the concatenation of all the values, if the list has more than one text
        /// node). Otherwise, a formatted XML string is returned.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() {
            if (m_items.length == 0)
                return "";
            if (m_items.length == 1)
                return (string)m_items[0];

            int count = 0;

            for (int i = 0, n = m_items.length; i < n; i++) {
                XMLNodeType nodeType = m_items[i].nodeType;
                if (nodeType == XMLNodeType.ELEMENT)
                    return toXMLString();
                if (nodeType == XMLNodeType.ATTRIBUTE || nodeType == XMLNodeType.TEXT)
                    count++;
            }

            string[] strArr = new string[count];
            count = 0;

            for (int i = 0, n = m_items.length; i < n; i++) {
                XMLNodeType nodeType = m_items[i].nodeType;
                if (nodeType == XMLNodeType.ATTRIBUTE || nodeType == XMLNodeType.TEXT)
                    strArr[count++] = m_items[i].nodeText;
            }
            return String.Concat(strArr);
        }

        /// <summary>
        /// Converts this XMLList to a formatted XML string. Unlike the <c>toString()</c> method,
        /// this method always uses XML syntax even if this list has simple content.
        /// </summary>
        /// <returns>The string representation of the XMLList object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toXMLString() {
            if (m_items.length == 0)
                return "";

            XMLWriter writer = new XMLWriter();
            return writer.makeString(this);
        }

    }
}
