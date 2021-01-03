using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Native {

    /// <summary>
    /// This attribute is applied to a type written in .NET code for exporting it to the AVM2 as a
    /// class.
    /// </summary>
    ///
    /// <remarks>
    /// For a type to be able to be exported as a native class to the AVM2, it must
    /// be a public non-nested type, and must either be an interface or a class
    /// deriving from  <see cref="ASObject"/>. The type must not be a generic type,
    /// with certain exceptions for some core AVM2 classes.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    public sealed class AVM2ExportClassAttribute : Attribute {

        /// <summary>
        /// The local name for the class. This does not include the namespace. If this is omitted, the
        /// name of the exported type is used.
        /// </summary>
        public string name;

        /// <summary>
        /// The namespace kind for the name of the class.
        /// </summary>
        /// <remarks>
        /// This must be a valid value of <see cref="NamespaceKind"/> other than
        /// <see cref="NamespaceKind.PRIVATE" qualifyHint="true"/>. If <see cref="nsUri"/> is
        /// omitted or null, the public namespace is used, and the value of this field is ignored. If
        /// this is omitted but <see cref="nsUri"/> is not, the default value
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/> is used.
        /// </remarks>
        public NamespaceKind nsKind;

        /// <summary>
        /// The name or URI of the namespace for the name of the class. If this is omitted or null,
        /// the public namespace is used, and the <see cref="nsKind"/> field is ignored.
        /// </summary>
        public string nsUri;

        /// <summary>
        /// If this is true, the class is dynamic (instances can have dynamic properties). This cannot
        /// be used for interfaces.
        /// </summary>
        public bool isDynamic;

        /// <summary>
        /// This must be set to true if the class exports prototype methods with
        /// <see cref="AVM2ExportPrototypeMethodAttribute"/>. If this is false, prototype methods
        /// will be ignored.
        /// </summary>
        public bool hasPrototypeMethods;

        /// <summary>
        /// This must be set to true if the class declared index lookup methods (such as <c>AS_getElement</c>,
        /// <c>AS_setElement</c> and <c>AS_deleteElement</c>) that should be used by the ABC compiler for
        /// a property lookup with a numeric key on an instance of the class. If this is false,
        /// index lookup methods will be ignored.
        /// </summary>
        public bool hasIndexLookupMethods;

        /// <summary>
        /// If this is true, the class will not be added to the global traits of its application
        /// domain. Such a class cannot be retreived for example with the
        /// <see cref="ApplicationDomain.getGlobalClass(in QName, Boolean)"/> method,
        /// and will not be available to compiled ActionScript code. The class, however,
        /// can be obtained through <see cref="Class.fromType"/>.
        /// </summary>
        public bool hiddenFromGlobal;

    }

    /// <summary>
    /// This attribute is used on members in a class exported to the AVM2 using
    /// <see cref="AVM2ExportClassAttribute"/> to declare which traits must be exported.
    /// </summary>
    ///
    /// <remarks>
    /// <para>Only members with public visibility can be exported as AVM2 traits. Non-public
    /// members are ignored.</para>
    /// <para>
    /// For a field to be exportable, its field type must be an exported type, the type
    /// <see cref="ASAny"/> or one of the primitive types <see cref="Int32"/>, <see cref="UInt32"/>,
    /// <see cref="Boolean"/>, <see cref="Double"/> or <see cref="String"/>.
    /// </para>
    /// <para>
    /// For a method to be exportable, its return type (if not void) and the types of all
    /// its parameters, with the possible exception of the last parameter's type being
    /// <see cref="RestParam"/>, must be an exported type or any type that is allowed as the
    /// type of an exported field. A method whose last parameter is of type
    /// <see cref="RestParam"/> corresponds to a method in AS3 that takes a "rest"
    /// parameter. Parameters can have default values. (They can also be defined using
    /// <see cref="ParamDefaultValueAttribute"/>.) Generic methods cannot be exported.
    /// </para>
    /// <para>
    /// For a constructor to be exportable, its parameter types must satisfy the
    /// same requirements as those for method parameters. Only one constructor per class
    /// can be exported.
    /// </para>
    /// <para>
    /// For a property to be exportable, its getter and setter methods (if defined)
    /// must be exportable methods. The methods themselves do not have to declare this
    /// attribute, unless they are to be exported separately in addition to the property
    /// definition. In addition, the getter method (if defined) must accept no parameters
    /// and return a value, the setter (if defined) must accept exactly one parameter and
    /// return void, and (if both the getter and setter are defined) the return type of
    /// the getter must be the the same as the parameter type of the setter.
    /// </para>
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor,
        AllowMultiple = false, Inherited = false)]
    public sealed class AVM2ExportTraitAttribute : Attribute {

        /// <summary>
        /// The local name for the trait. This does not include the namespace. If this is omitted, the
        /// declared name of the member is used.
        /// </summary>
        public string name;

        /// <summary>
        /// The kind of namespace for the name of the trait.
        /// </summary>
        /// <remarks>
        /// This must be a valid namespace kind other that <see cref="NamespaceKind.PRIVATE"/>.
        /// If <see cref="nsUri"/> is omitted or null, the public namespace is used, and the value of
        /// this field is ignored. If this is omitted but <see cref="nsUri"/> is not, the default value
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/> is used.
        /// </remarks>
        public NamespaceKind nsKind;

        /// <summary>
        /// The name or URI of the namespace for the name of the trait. If this is omitted or set to null,
        /// the public namespace is used, and the <see cref="nsKind"/> field is ignored.
        /// </summary>
        public string nsUri;

    }

    /// <summary>
    /// Indicates that a parameter in a method exported to the AVM2 is optional and provides a
    /// default value for it.
    /// </summary>
    ///
    /// <remarks>
    /// <para>This attribute can be used to define optional parameters in the following
    /// situations.</para>
    /// <list type="bullet">
    /// <item>When writing AVM2 exported methods in a language that does not support optional
    /// parameters. (If using a language that does support optional parameters, the default
    /// values in most cases can be specified directly and will be detected when the AVM2 loads
    /// the exported method.)</item>
    /// <item>When defining a parameter of type <see cref="ASNamespace"/> as optional, as there
    /// is no way to represent a compile-time constant of that type. In this case, the URI of
    /// the namespace to be used as the default value must be provided as a string.</item>
    /// <item>When the default value requires a boxing conversion to the parameter type. An
    /// example is a parameter of type <see cref="ASAny"/> having an integer default
    /// value.</item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class ParamDefaultValueAttribute : Attribute {

        /// <summary>
        /// The value of the optional parameter.
        /// </summary>
        internal readonly object m_value;

        /// <summary>
        /// Creates a new instance of the <see cref="ParamDefaultValueAttribute"/> class.
        /// </summary>
        /// <param name="value">The value of the optional parameter. For a parameter of type
        /// Namespace, set this to the URI of the default Namespace value as a string.</param>
        public ParamDefaultValueAttribute(object value) {
            m_value = value;
        }

    }

    /// <summary>
    /// This attribute is used in classes written for exporting to the AVM2 to export a method to
    /// the prototype object of the class. Such methods are used for backwards compatibility with
    /// ECMAScript.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// All methods marked with this attribute must be methods declared with public visibility.
    /// Non-public methods are ignored. In addition, use of this attribute for any method requires
    /// the class declaring it to be marked with a <see cref="AVM2ExportClassAttribute"/> with
    /// <see cref="AVM2ExportClassAttribute.hasPrototypeMethods" qualifyHint="true"/> set
    /// to true. set to true.
    /// </para>
    /// <para>This attribute may be applied to instance or static methods. Static methods are
    /// called from prototype objects by passing the receiver as the first argument to the
    /// method.</para>
    /// <para>The signature of a method exported as a prototype method must satisfy the
    /// same requirements as those of a method exported as a trait. See remarks on
    /// <see cref="AVM2ExportTraitAttribute"/> for further details.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AVM2ExportPrototypeMethodAttribute : Attribute {

        /// <summary>
        /// The name of the method in the prototype object. If this is omitted or null, the declared
        /// name of the method is used.
        /// </summary>
        public string name;

    }

    /// <summary>
    /// This attribute is used to export a module to the AVM2. Modules are used for exporting
    /// non-class traits that must be defined in the global scope (such as global methods).
    /// </summary>
    ///
    /// <remarks>
    /// <para>Traits in modules can be declared as exported in the same way as in classes, by using
    /// <see cref="AVM2ExportTraitAttribute"/>. However, only static members of the module type
    /// can be marked with the attribute. Instance members and constructors are ignored.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AVM2ExportModuleAttribute : Attribute { }

    /// <summary>
    /// This attribute is used to define metadata tags to classes and traits exported to the AVM2.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property
        | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Interface,
        AllowMultiple = true, Inherited = false)]
    public sealed class TraitMetadataAttribute : Attribute {

        /// <summary>
        /// The data string of the metadata tag.
        /// </summary>
        internal readonly string m_data;

        /// <summary>
        /// Creates a new instance of <see cref="TraitMetadataAttribute"/>.
        /// </summary>
        /// <param name="dataString">The data of the tag. See remarks below for the syntax.</param>
        ///
        /// <remarks>
        /// <para>The data string for a tag with data follows the specified syntax:</para>
        /// <code language="none">[<i>tagName</i>(<i>p(0)</i>, <i>p(1)</i>, <i>p(2)</i>, ..., <i>k(1)</i> = <i>v(1)</i>, <i>k(2)</i> = <i>v(2)</i>, ...)]</code>
        /// <para>Or (for a tag with only a name): <c>[<i>tagName</i>]</c>.</para>
        /// <list type="bullet">
        /// <item><i>tagName</i> is the title of the metadata tag.</item>
        /// <item><i>p(n)</i> represents an indexed value with index n.</item>
        /// <item><i>k(n)</i> = <i>v(n)</i> represents a key-value pair, with <i>k(n)</i>
        /// being the key and <i>v(n)</i> being the value.</item>
        /// <item>Either a comma or semicolon can be used as a separator between values.</item>
        /// <item><i>tagName</i>, <i>p(n)</i>, <i>k(n)</i> and <i>v(n)</i> must be
        /// surrounded by single or double quotes if they contain spaces or any of the following
        /// characters: <c>( ) [ ] , ; ' : = \</c></item>
        /// <item>
        /// In single or double-quoted strings, backslashes are used as escape characters if followed
        /// by a backslash or a single or double quote, and are literal if followed by any other
        /// character. Note that some languages (such as C#) use backslashes as escape characters in
        /// string literals, and in such languages backslashes must be doubled. For example, the
        /// strings <c>abc\d</c> and <c>abc'd</c> must be written as "abc\\\\d" and "abc\\'d"
        /// as string literals in these languages.
        /// </item>
        /// <item>Any space character, tab or new line that is not inside quotes is ignored.</item>
        /// </list>
        /// </remarks>
        public TraitMetadataAttribute(string dataString) {
            m_data = dataString;
        }

    }

    /// <summary>
    /// This attribute is used to declare special internal properties for some AVM2 core classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class AVM2ExportClassInternalAttribute : Attribute {

        /// <summary>
        /// A value from the <see cref="ClassTag"/> enumeration to be set as the value of the
        /// <see cref="Class.tag"/> property for the exported class.
        /// </summary>
        public ClassTag tag;

        /// <summary>
        /// The primitive type associated with the class.
        /// </summary>
        public Type primitiveType;

        /// <summary>
        /// Set this to true if the class defines exported traits (other than overridden methods)
        /// that hide traits inherited from its parent class having the same name.
        /// </summary>
        public bool hidesInheritedTraits;

        /// <summary>
        /// The <see cref="Type"/> representing the class whose prototype is to be used for
        /// instances of the exported class. If this is not specified, the prototype of the
        /// exported class itself is used.
        /// </summary>
        public Type usePrototypeOf;

    }

}
