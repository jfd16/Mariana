using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    [AVM2ExportClass]
    public interface OperatorsTest_IA { }

    [AVM2ExportClass]
    public class OperatorsTest_CA : ASObject {
        [AVM2ExportTrait]
        public int foo() => 0;

        [AVM2ExportTrait]
        public int foo2() => 1;

        [AVM2ExportTrait]
        public static int bar(int x) => x;

        [AVM2ExportTrait]
        public static int bar2(int x) => x;
    }

    [AVM2ExportClass]
    public class OperatorsTest_CB : OperatorsTest_CA, OperatorsTest_IA { }

    public class OperatorsTest {

        public enum CompareOpResult {
            LESS,
            EQUAL,
            NAN
        }

        static OperatorsTest() {
            TestAppDomain.ensureClassesLoaded(
                typeof(OperatorsTest_CA),
                typeof(OperatorsTest_CB)
            );
        }

        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        private static readonly ASObject uniqueObject = new ASObject();
        private static readonly ASArray uniqueArray = new ASArray();
        private static readonly ASFunction uniqueFunction = ASFunction.createEmpty();
        private static readonly ASObject uniqueObjectClassA = new OperatorsTest_CA();
        private static readonly ASObject uniqueObjectClassB = new OperatorsTest_CB();
        private static readonly ASNamespace uniqueNamespace = new ASNamespace("x", "abc");
        private static readonly ASQName uniqueQName = new ASQName("abc", "def");
        private static readonly ASXML uniqueXml = xmlElement("a", children: new[] {xmlElement("b"), xmlElement("c")});
        private static readonly ASXMLList uniqueXmlList = xmlList(xmlElement("a"), xmlText(""));

        private static readonly ASObject boxedNaN = Double.NaN;

        private static readonly ASAny NULL = ASAny.@null;

        private static readonly ASAny UNDEF = ASAny.undefined;

        private static ASXML xmlText(string value) => ASXML.createNode(XMLNodeType.TEXT, null, value);

        private static ASXML xmlCdata(string value) => ASXML.createNode(XMLNodeType.CDATA, null, value);

        private static ASXML xmlComment(string value) => ASXML.createNode(XMLNodeType.COMMENT, null, value);

        private static ASXML xmlAttribute(string name, string value) => ASXML.internalCreateAttribute(ASQName.parse(name), value);

        private static ASXML xmlProcInstr(string name, string value) =>
            ASXML.createNode(XMLNodeType.PROCESSING_INSTRUCTION, new ASQName(name), value);

        private static ASObject objWithMethods(params (string name, ASAny returnVal)[] methods) {
            var obj = new ASObject();

            for (int i = 0; i < methods.Length; i++)
                obj.AS_setProperty(methods[i].name, SpyFunctionObject.withReturn(methods[i].returnVal));

            return obj;
        }

        private static ASXML xmlElement(
            string name, (string name, string value)[] attributes = null, ASXML[] children = null, ASNamespace[] nsDecls = null)
        {
            attributes = attributes ?? Array.Empty<(string, string)>();
            children = children ?? Array.Empty<ASXML>();
            nsDecls = nsDecls ?? Array.Empty<ASNamespace>();

            return ASXML.internalCreateElement(
                ASQName.parse(name),
                attributes.Select(x => ASXML.internalCreateAttribute(ASQName.parse(x.name), x.value)).ToArray(),
                children,
                nsDecls
            );
        }

        private static ASXMLList xmlList(params ASXML[] elements) => new ASXMLList(elements);

        /// <summary>
        /// Produces "equivalent" forms of the given pair of values where XML objects are wrapped
        /// in single-element XMLLists. Used to produce data for tests where single-element XMLLists
        /// are expected to produce the same results as XML objects.
        /// </summary>
        private static IEnumerable<(ASAny x, ASAny y)> expandXmlArgs((ASAny x, ASAny y) pair) {
            ASXML xAsXml = pair.x.value as ASXML, yAsXml = pair.y.value as ASXML;

            if (xAsXml != null && yAsXml != null) {
                return new (ASAny, ASAny)[] {
                    (xAsXml, yAsXml),
                    (xAsXml, xmlList(yAsXml)),
                    (xmlList(xAsXml), yAsXml),
                    (xmlList(xAsXml), xmlList(yAsXml))
                };
            }
            else if (xAsXml != null) {
                return new (ASAny, ASAny)[] {(xAsXml, pair.y), (xmlList(xAsXml), pair.y)};
            }
            else if (yAsXml != null) {
                return new (ASAny, ASAny)[] {(pair.x, yAsXml), (pair.x, xmlList(yAsXml))};
            }
            else {
                return new[] {pair};
            }
        }

        private static (ASAny x, ASAny y)[] equalityOperatorTests_data_alwaysEqual() => new (ASAny x, ASAny y)[] {
            (x: NULL, y: NULL),
            (x: UNDEF, y: UNDEF),

            (x: 0, y: 0),
            (x: 0, y: 0u),
            (x: 0, y: 0.0),
            (x: 0, y: NEG_ZERO),
            (x: 1000, y: 1000),
            (x: 1000, y: 1000.0),
            (x: Int32.MinValue, y: (double)Int32.MinValue),
            (x: Int32.MaxValue, y: (uint)Int32.MaxValue),
            (x: UInt32.MaxValue, y: (double)UInt32.MaxValue),
            (x: Double.Epsilon, y: Double.Epsilon),
            (x: 0.39522149, y: 0.39522149),
            (x: Double.MaxValue, y: Double.MaxValue),
            (x: Double.PositiveInfinity, y: Double.PositiveInfinity),
            (x: Double.NegativeInfinity, y: Double.NegativeInfinity),

            (x: "", y: ""),
            (x: "abc", y: "abc"),
            (x: "abcu8dfs&(^JSD*(S&d80usi)(s;??f;", y: "abcu8dfs&(^JSD*(S&d80usi)(s;??f;"),
            (x: "héllo", y: "héllo"),
            (x: "abcd\0efg\uffffgh", y: "abcd\0efg\uffffgh"),
            (x: "\ud8ff\udd33\ude49\ud789", y: "\ud8ff\udd33\ude49\ud789"),
            (x: "124", y: "124"),

            (x: true, y: true),
            (x: false, y: false),

            (x: uniqueObject, y: uniqueObject),
            (x: uniqueArray, y: uniqueArray),
            (x: uniqueNamespace, y: uniqueNamespace),
            (x: uniqueQName, y: uniqueQName),
            (x: uniqueFunction, y: uniqueFunction),
            (x: uniqueObjectClassA, y: uniqueObjectClassA),
            (x: uniqueObjectClassB, y: uniqueObjectClassB),
            (x: uniqueXml, y: uniqueXml),
            (x: uniqueXmlList, y: uniqueXmlList),

            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(uniqueObjectClassA),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(uniqueObjectClassA)
            ),
            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("bar").createMethodClosure(),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("bar").createMethodClosure()
            )
        };

        private static (ASAny x, ASAny y)[] equalityOperatorTests_data_weakEqualOnly() => new (ASAny x, ASAny y)[] {
            (x: NULL, y: UNDEF),

            (x: 0, y: "0"),
            (x: 0, y: ""),
            (x: 0.0, y: " \n\t\r"),
            (x: 0u, y: "  0x0 "),
            (x: 0u, y: "+000000"),
            (x: NEG_ZERO, y: "0"),
            (x: 1234, y: "1234"),
            (x: 1234, y: "1.234E+3"),
            (x: 1234, y: "1234.0"),
            (x: 123456000, y: "12.3456e7"),
            (x: 817, y: "  +817  "),
            (x: Int32.MinValue, y: "-2147483648"),
            (x: 1e+25, y: "10000000000000000000000000"),
            (x: 4.39e-10, y: "0.0000000004390000"),
            (x: -2048.0, y: "-0X800"),
            (x: 9007199254740996.0, y: "9007199254740997"),
            (x: Double.PositiveInfinity, y: "Infinity"),
            (x: Double.NegativeInfinity, y: "-Infinity"),

            (x: true, y: 1),
            (x: true, y: "1"),
            (x: true, y: " \r 1 \n "),
            (x: false, y: ""),
            (x: false, y: "0"),
            (x: false, y: " \t  \v\n \r "),

            (x: new ASNamespace(""), y: new ASNamespace("")),
            (x: new ASNamespace("abc"), y: new ASNamespace("abc")),
            (x: new ASNamespace("p", "abc"), y: new ASNamespace("q", "abc")),

            (x: new ASQName("abcd"), y: new ASQName("abcd")),
            (x: new ASQName("abc", "def"), new ASQName("abc", "def")),
            (x: new ASQName("p", "abc", "def"), new ASQName("q", "abc", "def")),

            (x: 1234, y: objWithMethods(("valueOf", 1234))),
            (x: 1234, y: objWithMethods(("toString", 1234))),
            (x: 1234, y: objWithMethods(("valueOf", "+1234.0"))),
            (x: 1234, y: objWithMethods(("valueOf", new ASObject()), ("toString", "1234"))),
            (x: 1234, y: objWithMethods(("valueOf", 1234), ("toString", "abcd"))),
            (x: true, y: objWithMethods(("valueOf", true))),
            (x: false, y: objWithMethods(("toString", false))),
            (x: true, y: objWithMethods(("valueOf", "1.0"), ("toString", "0.0"))),
            (x: 0, y: objWithMethods(("toString", false))),
            (x: "1234", y: objWithMethods(("valueOf", 1234))),
            (x: "1234.000", y: objWithMethods(("toString", 1234))),
            (x: "abcd", y: objWithMethods(("valueOf", "abcd"))),
            (x: "abcd", y: objWithMethods(("valueOf", "abcd"), ("toString", new ASObject()))),
            (x: "abcd", y: objWithMethods(("toString", "abcd"))),
            (x: "abcd", y: objWithMethods(("valueOf", "abcd"), ("toString", "efgh"))),

            (x: UNDEF, y: xmlList()),
            (x: UNDEF, y: xmlText("undefined")),
            (x: UNDEF, y: xmlAttribute("a", "undefined")),
            (x: UNDEF, y: xmlCdata("undefined")),
            (x: UNDEF, y: xmlElement("foo", children: new[] {xmlText("undefined")})),
            (x: UNDEF, y: xmlElement("foo", attributes: new[] {("a", "")}, children: new[] {xmlText("undef"), xmlText("ined")})),

            (x: NULL, y: xmlText("null")),
            (x: NULL, y: xmlAttribute("a", "null")),
            (x: NULL, y: xmlCdata("null")),
            (x: NULL, y: xmlElement("foo", children: new[] {xmlText("null")})),
            (x: NULL, y: xmlElement("foo", attributes: new[] {("a", "")}, children: new[] {xmlText("nu"), xmlText("ll")})),

            (x: 1940394, y: xmlText("1940394")),
            (x: 840.288831, y: xmlAttribute("abcd", "840.288831")),
            (x: true, y: xmlCdata("true")),
            (x: 123456789, y: xmlElement("foo", children: new[] {xmlText("123456789")})),
            (x: Double.NaN, y: xmlElement("foo", children: new[] {xmlText("NaN")})),
            (x: false, y: xmlElement("foo", children: new[] {xmlText("false")})),
            (x: "qwertyu", y: xmlText("qwertyu")),
            (x: "qwertyu", y: xmlAttribute("abcd", "qwertyu")),
            (x: new ConvertibleMockObject(stringValue: "qwertyu"), y: xmlText("qwertyu")),
            (x: "<>&", y: xmlText("<>&")),

            (x: "", y: xmlElement("a")),
            (x: "", y: xmlElement("a", attributes: new[] {("x", "1")})),

            (
                x: 17321998,
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1"), ("b", "2")},
                    children: new[] {xmlText("173"), xmlText("219"), xmlText("98")}
                )
            ),
            (
                x: "hello world how are you",
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1"), ("b", "2")},
                    children: new[] {xmlText("hello "), xmlText("world how"), xmlText(" are you")}
                )
            ),

            (x: xmlText("1234"), y: xmlText("1234")),
            (x: xmlText("1234"), y: xmlCdata("1234")),
            (x: xmlText("abcd"), y: xmlAttribute("a", "abcd")),
            (x: xmlAttribute("a", "abcd"), y: xmlAttribute("a", "abcd")),
            (x: xmlAttribute("a", "abcd"), y: xmlAttribute("b", "abcd")),
            (x: xmlAttribute("A::a", "abcd"), y: xmlAttribute("B::a", "abcd")),
            (x: xmlAttribute("a", ""), y: xmlAttribute("b", "")),
            (x: xmlText("hello world"), y: xmlElement("a", children: new[] {xmlText("hello world")})),
            (x: xmlAttribute("x", "hello world"), y: xmlElement("a", children: new[] {xmlText("hello world")})),

            (
                x: xmlCdata("hello world how are you"),
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1"), ("b", "2")},
                    children: new[] {xmlText("hello "), xmlText("world how"), xmlText(" are you")}
                )
            ),
            (
                x: xmlAttribute("xyz", "hello world how are you"),
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1"), ("b", "2")},
                    children: new[] {xmlText("hello "), xmlText("world how"), xmlText(" are you")}
                )
            ),

            (x: xmlComment("hello"), y: xmlComment("hello")),
            (x: xmlProcInstr("a", "hello"), y: xmlProcInstr("a", "hello")),

            (x: xmlElement("hello"), y: xmlElement("hello")),

            (
                x: xmlElement("hello", attributes: new[] {("x", "1")}),
                y: xmlElement("hello", attributes: new[] {("x", "1")})
            ),
            (
                x: xmlElement("hello", attributes: new[] {("x", "1"), ("y", "2"), ("z", "abcd"), ("w", "")}),
                y: xmlElement("hello", attributes: new[] {("x", "1"), ("y", "2"), ("z", "abcd"), ("w", "")})
            ),
            (
                x: xmlElement("hello", attributes: new[] {("x", "1"), ("y", "2"), ("z", "abcd"), ("w", "")}),
                y: xmlElement("hello", attributes: new[] {("y", "2"), ("w", ""), ("z", "abcd"), ("x", "1")})
            ),
            (
                x: xmlElement("abc", children: new[] {xmlText("hello"), xmlText("world")}),
                y: xmlElement("abc", children: new[] {xmlText("hello"), xmlText("world")})
            ),
            (
                x: xmlElement("A::abc", children: new[] {xmlText("hello"), xmlText("world")}),
                y: xmlElement("A::abc", children: new[] {xmlText("hello"), xmlText("world")})
            ),
            (
                x: xmlElement("abc", children: new[] {xmlText("hello"), xmlCdata("world")}),
                y: xmlElement("abc", children: new[] {xmlText("hello"), xmlCdata("world")})
            ),
            (
                x: xmlElement("abc", children: new[] {xmlText("hello"), xmlCdata("world")}),
                y: xmlElement("abc", children: new[] {xmlText("hello"), xmlCdata("world")}, nsDecls: new[] {new ASNamespace("p", "Foo")})
            ),

            (
                x: xmlElement(
                    "html",
                    attributes: new[] {("lang", "en")},
                    children: new[] {
                        xmlText(""),
                        xmlElement(
                            "head",
                            children: new[] {
                                xmlElement("meta", attributes: new[] {("charset", "utf-8")}),
                                xmlElement("meta", attributes: new[] {("name", "generator"), ("content", "xyz")}),
                                xmlElement("meta", attributes: new[] {("name", "referrer"), ("content", "origin")}),
                                xmlElement("link", attributes: new[] {("rel", "stylesheet"), ("href", "http://example.css")}),
                                xmlElement("script", attributes: new[] {("type", "text/javascript"), ("src", "http://example.js"), ("async", "async")}),
                            }
                        ),
                        xmlText("\r\n"),
                        xmlElement(
                            "body",
                            attributes: new[] {("class", "my-class-1")},
                            children: new[] {
                                xmlText("Hello"),
                                xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                                xmlElement("img", attributes: new[] {("src", "http://example/image.jpg"), ("width", "200"), ("height", "200"), ("alt", "Alt text")}),
                                xmlElement(
                                    "http://www.w3.org/2000/svg::svg",
                                    attributes: new[] {("width", "300"), ("height", "300")},
                                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                                    children: new[] {
                                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                                    }
                                )
                            }
                        )
                    }
                ),
                y: xmlElement(
                    "html",
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/1999/xhtml")},
                    attributes: new[] {("lang", "en")},
                    children: new[] {
                        xmlText(""),
                        xmlElement(
                            "head",
                            children: new[] {
                                xmlElement("meta", attributes: new[] {("charset", "utf-8")}),
                                xmlElement("meta", attributes: new[] {("name", "generator"), ("content", "xyz")}),
                                xmlElement("meta", attributes: new[] {("content", "origin"), ("name", "referrer")}),
                                xmlElement("link", attributes: new[] {("rel", "stylesheet"), ("href", "http://example.css")}),
                                xmlElement("script", attributes: new[] {("async", "async"), ("src", "http://example.js"), ("type", "text/javascript")}),
                            }
                        ),
                        xmlText("\r\n"),
                        xmlElement(
                            "body",
                            attributes: new[] {("class", "my-class-1")},
                            children: new[] {
                                xmlText("Hello"),
                                xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                                xmlElement("img", attributes: new[] {("height", "200"), ("src", "http://example/image.jpg"), ("alt", "Alt text"), ("width", "200")}),
                                xmlElement(
                                    "http://www.w3.org/2000/svg::svg",
                                    attributes: new[] {("width", "300"), ("height", "300")},
                                    nsDecls: new[] {new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                                    children: new[] {
                                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                                    }
                                )
                            }
                        )
                    }
                )
            ),

            (x: xmlList(), y: xmlList()),

            (
                x: xmlList(xmlText("foo"), xmlText("bar")),
                y: xmlList(xmlText("foo"), xmlText("bar"))
            ),
            (
                x: xmlList(xmlText("foo"), xmlText("bar")),
                y: xmlList(xmlElement("p", children: new[] {xmlText("foo")}), xmlElement("p", children: new[] {xmlText("bar")}))
            ),
            (
                x: xmlList(xmlText("foo"), xmlElement("p", children: new[] {xmlText("bar")})),
                y: xmlList(xmlElement("p", children: new[] {xmlText("foo")}), xmlText("bar"))
            ),
            (
                x: xmlList(xmlText("foo"), xmlCdata("bar")),
                y: xmlList(xmlCdata("foo"), xmlText("bar"))
            ),
            (
                x: xmlList(
                    xmlText("hello"),
                    xmlElement("b", children: new[] {xmlElement("c", children: new[] {xmlText("hello")})})
                ),
                y: xmlList(
                    xmlElement("a", attributes: new[] {("x", "123")}, children: new[] {xmlText("hello")}),
                    xmlElement("b", children: new[] {xmlElement("c", children: new[] {xmlText("hello")})})
                )
            ),
            (
                x: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")}
                    ),
                    xmlComment(".123.456."),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlElement("a", attributes: new[] {("p", "")}, children: new[] {xmlText("Hello!")})
                ),
                y: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("y", "20"), ("x", "100")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")},
                        nsDecls: new[] {new ASNamespace("hello", "Hello")}
                    ),
                    xmlComment(".123.456."),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlAttribute("aaa", "Hello!")
                )
            )
        };

        private static (ASAny x, ASAny y)[] equalityOperatorTests_data_alwaysNotEqual() => new (ASAny x, ASAny y)[] {
            (x: NULL, y: 0),
            (x: NULL, y: 1),
            (x: NULL, y: Double.NaN),
            (x: NULL, y: false),
            (x: NULL, y: ""),
            (x: NULL, y: "null"),
            (x: NULL, y: new ASObject()),

            (x: UNDEF, y: 0),
            (x: UNDEF, y: 1),
            (x: UNDEF, y: Double.NaN),
            (x: UNDEF, y: false),
            (x: UNDEF, y: ""),
            (x: UNDEF, y: "undefined"),
            (x: UNDEF, y: new ASObject()),

            (x: 0, y: 0.1),
            (x: 0, y: Double.Epsilon),
            (x: Int32.MinValue, y: (uint)Int32.MaxValue + 1),
            (x: -1, y: UInt32.MaxValue),
            (x: -1.0, y: UInt32.MaxValue),
            (x: 0.1 + 0.2, y: 0.3),
            (x: Double.PositiveInfinity, y: Double.NegativeInfinity),
            (x: boxedNaN, y: boxedNaN),
            (x: Double.NaN, y: Double.NaN),

            (x: "", y: "abcd"),
            (x: "ABCD", y: "abcd"),
            (x: "héllo", y: "he\u0301llo"),
            (x: "abcd", y: "abcd\0"),
            (x: "abcd\0efg\uffffgh", y: "abcd\0efg\uffffgi"),
            (x: "\ud8ff\udd33\ude49\ud789", y: "\ud8ff\udd33\ude46\ud789\ue33a"),
            (x: "123", y: "0123"),
            (x: "123", y: "+123"),
            (x: "123", y: "123hello"),
            (x: "255", y: "0xFF"),
            (x: "0", y: "0.0"),

            (x: 1, y: "2"),
            (x: 0.0045392, y: "0.004539224"),
            (x: 0.0045392, y: "0.004539224"),
            (x: 9007199254740996.0, y: "9007199254740998"),
            (x: Double.PositiveInfinity, y: "infinity"),
            (x: Double.NaN, y: ""),
            (x: Double.NaN, y: "NaN"),

            (x: true, y: false),
            (x: true, y: 2),
            (x: true, y: "abc"),
            (x: true, y: "true"),
            (x: false, y: "false"),
            (x: false, y: Double.NaN),

            (x: new ASDate(1000), y: new ASDate(1000)),

            (x: new ASObject(), y: new ASObject()),
            (x: new ASArray(), y: new ASArray()),
            (x: new ASArray(new ASAny[] {1, 2, 3}), new ASArray(new ASAny[] {1, 2, 3})),
            (x: new ASNamespace("abc"), y: new ASNamespace("def")),
            (x: new ASQName("abc", "def"), y: new ASQName("abc", "ghi")),
            (x: new ASQName("abc", "def"), y: new ASQName("xyz", "def")),
            (x: new OperatorsTest_CA(), y: new OperatorsTest_CA()),
            (x: new OperatorsTest_CA(), y: new OperatorsTest_CB()),

            (x: ASFunction.createEmpty(), y: ASFunction.createEmpty()),

            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(uniqueObjectClassA),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo2").createMethodClosure(uniqueObjectClassA)
            ),
            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(uniqueObjectClassA),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(new OperatorsTest_CA())
            ),
            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(uniqueObjectClassA),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo2").createMethodClosure(new OperatorsTest_CA())
            ),
            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("bar").createMethodClosure(),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("bar2").createMethodClosure()
            ),
            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createFunctionClosure(),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createFunctionClosure()
            ),
            (
                x: Class.fromType(typeof(OperatorsTest_CA)).getMethod("bar").createFunctionClosure(),
                y: Class.fromType(typeof(OperatorsTest_CA)).getMethod("bar").createFunctionClosure()
            ),

            (x: 1234, y: objWithMethods(("valueOf", 1235))),
            (x: 1234, y: objWithMethods(("toString", "-1234"))),
            (x: boxedNaN, y: objWithMethods(("valueOf", boxedNaN))),
            (x: "abcd", y: objWithMethods(("valueOf", "efgh"))),
            (x: "abcd", y: objWithMethods(("toString", "efgh"))),
            (x: 1234, y: objWithMethods(("valueOf", 1235), ("toString", 1234))),
            (x: "abcd", y: objWithMethods(("valueOf", "abc"), ("toString", "abcd"))),
            (x: true, y: objWithMethods(("valueOf", 0), ("toString", 1))),
            (x: true, y: objWithMethods(("valueOf", 2), ("toString", true))),
            (x: NULL, y: objWithMethods(("valueOf", NULL))),
            (x: NULL, y: objWithMethods(("valueOf", 0))),
            (x: UNDEF, y: objWithMethods(("valueOf", UNDEF))),
            (x: UNDEF, y: objWithMethods(("valueOf", UNDEF), ("toString", UNDEF))),
            (x: 1234, y: objWithMethods(("valueOf", UNDEF), ("toString", 1234))),
            (x: "abcd", y: objWithMethods(("valueOf", UNDEF), ("toString", "abcd"))),
            (x: objWithMethods(("valueOf", 1234)), y: objWithMethods(("valueOf", 1234))),
            (x: objWithMethods(("toString", "abcd")), y: objWithMethods(("toString", "abcd"))),

            (x: UNDEF, y: xmlList(xmlText(""), xmlText("undefin"), xmlCdata("ed"))),
            (x: NULL, y: xmlList(xmlText("n"), xmlText(""), xmlCdata("ull"))),

            (x: 1940394, y: xmlText("1940395")),
            (x: 1940394, y: xmlText("1940394.000")),
            (x: 840.288831, y: xmlComment("840.288831")),
            (x: 1111, y: xmlProcInstr("a", "1111")),
            (x: 4523, y: xmlElement("foo", attributes: new[] {("a", "4523")})),
            (x: true, y: xmlText("1")),
            (x: false, y: xmlElement("foo", attributes: new[] {("a", "false")})),

            (x: "qwertyu", y: xmlText("zwertyu")),
            (x: "qwertyu", y: xmlComment("qwertyu")),
            (x: "héllo", y: xmlAttribute("abcd", "he\u0301llo")),

            (
                x: 123456789,
                y: xmlElement(
                    "foo",
                    children: new[] {
                        xmlElement("bar", children: new[] {xmlText("123456789")})
                    }
                )
            ),
            (
                x: 17321998,
                y: xmlList(xmlText("173"), xmlText(""), xmlAttribute("a", "21"), xmlText("99"), xmlText(""), xmlCdata("8"))
            ),
            (
                x: "hello world how are you",
                y: xmlList(xmlText("hel"), xmlText("lo w"), xmlAttribute("a", "orld "), xmlText("how a"), xmlText(""), xmlCdata("re you"))
            ),
            (
                x: new ConvertibleMockObject(stringValue: "hello world how are you"),
                y: xmlList(xmlText("hel"), xmlText("lo w"), xmlAttribute("a", "orld "), xmlText("how a"), xmlText(""), xmlCdata("re you"))
            ),

            (x: "<a>Hello</a>", y: xmlElement("a", children: new[] {xmlText("Hello")})),
            (x: "<!--xyz-->", y: xmlComment("xyz")),
            (x: "<?foo xyz>", y: xmlProcInstr("foo", "xyz")),
            (x: "<a><b/><b/></a>", y: xmlElement("a", children: new[] {xmlElement("b"), xmlElement("b")})),
            (x: "&lt;&gt;&amp;", y: xmlText("<>&")),

            (x: xmlText("hello"), y: xmlText("Hello")),
            (x: xmlText("hello"), y: xmlText("  hello  ")),
            (x: xmlText("hello"), y: xmlComment("hello")),
            (x: xmlText("hello"), y: xmlElement("a", children: new[] {xmlElement("b", children: new[] {xmlText("hello")})})),

            (x: UNDEF, y: xmlText("UNDEFINED")),
            (x: UNDEF, y: xmlList(xmlText("undefined"), xmlText(""))),
            (x: UNDEF, y: xmlList(xmlText("undef"), xmlText("ined"))),
            (x: UNDEF, y: xmlElement("a", children: new[] {xmlElement("b", children: new[] {xmlText("undefined")})})),

            (x: NULL, y: xmlText("NULL")),
            (x: NULL, y: xmlList(xmlText("null"), xmlText(""))),
            (x: NULL, y: xmlList(xmlText("nu"), xmlText("ll"))),
            (x: NULL, y: xmlElement("a", children: new[] {xmlElement("b", children: new[] {xmlText("null")})})),

            (
                x: xmlElement("foo", attributes: new[] {("x", "1")}, children: new[] {xmlText("hello")}),
                y: xmlElement("foo", attributes: new[] {("x", "2")}, children: new[] {xmlText("hello")})
            ),
            (
                x: xmlElement("foo", attributes: new[] {("x", "1")}, children: new[] {xmlText("hello")}),
                y: xmlElement("foo", attributes: new[] {("x", " 1 ")}, children: new[] {xmlText("hello")})
            ),
            (
                x: xmlElement("foo", attributes: new[] {("x", "1")}, children: new[] {xmlText("hello")}),
                y: xmlElement("foo", children: new[] {xmlText("hello")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("hello")}),
                y: xmlElement("foo", children: new[] {xmlCdata("hello")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("abcd xyz")}),
                y: xmlElement("bar", children: new[] {xmlText("abcd xyz")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("abcd xyz")}),
                y: xmlElement("A::foo", children: new[] {xmlText("abcd xyz")})
            ),
            (
                x: xmlElement("foo"),
                y: xmlElement("foo", attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")})
            ),
            (
                x: xmlElement("foo", attributes: new[] {("c", "l98b7"), ("a", "1yr")}),
                y: xmlElement("foo", attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")})
            ),
            (
                x: xmlElement("foo", attributes: new[] {("c", "l98b7"), ("a", "1yr"), ("b", "h75kq")}),
                y: xmlElement("foo", attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")})
            ),
            (
                x: xmlElement("foo", attributes: new[] {("c", "l98b7"), ("X::a", "1yr"), ("b", "h78kq")}),
                y: xmlElement("foo", attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("a"), xmlText("b"), xmlText("c")}),
                y: xmlElement("foo", children: new[] {xmlText("a"), xmlText("b"), xmlCdata("c")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("a"), xmlText("b"), xmlText("c")}),
                y: xmlElement("foo", children: new[] {xmlText("abc")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("a"), xmlText("b"), xmlText("c")}),
                y: xmlElement("foo", children: new[] {xmlText("a"), xmlText("b"), xmlText(""), xmlText("c")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("abcd")}),
                y: xmlElement("foo", children: new[] {xmlText("  abcd  ")})
            ),
            (
                x: xmlElement("foo", children: new[] {xmlText("héllo")}),
                y: xmlElement("foo", children: new[] {xmlText("he\u0301llo")})
            ),
            (
                x: xmlElement("a", children: new[] {xmlElement("b"), xmlElement("c")}),
                y: xmlElement("a", children: new[] {xmlElement("b", children: new[] {xmlElement("c")})})
            ),
            (
                x: xmlElement(
                    "foo",
                    attributes: new[] {("c", "l98b7"), ("a", "1yr"), ("b", "h78kq")},
                    children: new[] {xmlText("a"), xmlText("b"), xmlText("c")}),
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")},
                    children: new[] {xmlText("a"), xmlText("d"), xmlText("c")}
                )
            ),
            (
                x: xmlElement(
                    "foo",
                    attributes: new[] {("c", "l98b7"), ("a", "1yr"), ("b", "h78kq")},
                    children: new[] {xmlText("a"), xmlText("b"), xmlText("c")}),
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")},
                    children: new[] {xmlText("a"), xmlText("b"), xmlText("c"), xmlText("d")}
                )
            ),
            (
                x: xmlElement(
                    "foo",
                    attributes: new[] {("c", "l98b7"), ("a", "1yr"), ("b", "h78kq")},
                    children: new[] {xmlText("a"), xmlText("b"), xmlText("c")}),
                y: xmlElement(
                    "foo",
                    attributes: new[] {("a", "1yr"), ("b", "h78kq"), ("c", "l98b7")},
                    children: new[] {xmlText("a"), xmlText("b"), xmlText("c"), xmlText("d")}
                )
            ),
            (
                x: xmlElement(
                    "http://www.w3.org/2000/svg::svg",
                    attributes: new[] {("width", "300"), ("height", "300")},
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                    children: new[] {
                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                    }
                ),
                y: xmlElement(
                    "http://www.w3.org/2000/svg::svg",
                    attributes: new[] {("width", "300"), ("height", "300")},
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                    children: new[] {
                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L100,0 Z")})
                    }
                )
            ),
            (
                x: xmlElement(
                    "http://www.w3.org/2000/svg::svg",
                    attributes: new[] {("width", "300"), ("height", "300")},
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                    children: new[] {
                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                    }
                ),
                y: xmlElement(
                    "http://www.w3.org/2000/svg::svg",
                    attributes: new[] {("width", "300"), ("height", "300")},
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                    children: new[] {
                        xmlElement("path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                    }
                )
            ),

            (
                x: xmlElement(
                    "html",
                    attributes: new[] {("lang", "en")},
                    children: new[] {
                        xmlElement(
                            "head",
                            children: new[] {
                                xmlElement("meta", attributes: new[] {("charset", "utf-8")}),
                                xmlElement("script", attributes: new[] {("type", "text/javascript"), ("src", "http://example.js"), ("async", "async")}),
                            }
                        ),
                        xmlElement(
                            "body",
                            attributes: new[] {("class", "my-class-1")},
                            children: new[] {
                                xmlText("Hello"),
                                xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                                xmlElement(
                                    "http://www.w3.org/2000/svg::svg",
                                    attributes: new[] {("width", "300"), ("height", "300")},
                                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                                    children: new[] {
                                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")}),
                                        xmlElement("http://www.w3.org/2000/svg::text", attributes: new[] {("x", "0"), ("y", "0")}, children: new[] {xmlText("Hello")})
                                    }
                                )
                            }
                        )
                    }
                ),
                y: xmlElement(
                    "html",
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/1999/xhtml")},
                    attributes: new[] {("lang", "en")},
                    children: new[] {
                        xmlElement(
                            "head",
                            children: new[] {
                                xmlElement("meta", attributes: new[] {("charset", "utf-8")}),
                                xmlElement("script", attributes: new[] {("async", "async"), ("src", "http://example.js"), ("type", "text/javascript")}),
                            }
                        ),
                        xmlElement(
                            "body",
                            attributes: new[] {("class", "my-class-1")},
                            children: new[] {
                                xmlText("Hello"),
                                xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                                xmlElement(
                                    "http://www.w3.org/2000/svg::svg",
                                    attributes: new[] {("width", "300"), ("height", "300")},
                                    nsDecls: new[] {new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                                    children: new[] {
                                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")}),
                                        xmlElement("http://www.w3.org/2000/svg::text", attributes: new[] {("x", "0"), ("y", "0")}, children: new[] {xmlCdata("Hello")})
                                    }
                                )
                            }
                        )
                    }
                )
            ),

            (
                x: xmlElement(
                    "html",
                    attributes: new[] {("lang", "en")},
                    children: new[] {
                        xmlElement(
                            "head",
                            children: new[] {
                                xmlElement("meta", attributes: new[] {("charset", "utf-8")}),
                                xmlElement("script", attributes: new[] {("type", "text/javascript"), ("src", "http://example.js"), ("async", "async")}),
                            }
                        ),
                        xmlElement(
                            "body",
                            attributes: new[] {("class", "my-class-1")},
                            children: new[] {
                                xmlText("Hello"),
                                xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                                xmlElement(
                                    "http://www.w3.org/2000/svg::svg",
                                    attributes: new[] {("width", "300"), ("height", "300")},
                                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/2000/svg"), new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                                    children: new[] {
                                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")}),
                                        xmlElement("http://www.w3.org/2000/svg::text", attributes: new[] {("x", "0"), ("y", "0")}, children: new[] {xmlText("Hello")})
                                    }
                                )
                            }
                        )
                    }
                ),
                y: xmlElement(
                    "html",
                    nsDecls: new[] {new ASNamespace("", "http://www.w3.org/1999/xhtml")},
                    attributes: new[] {("lang", "en")},
                    children: new[] {
                        xmlElement(
                            "head",
                            children: new[] {
                                xmlElement("meta", attributes: new[] {("charset", "utf-8")}),
                                xmlElement("script", attributes: new[] {("async", "async"), ("src", "http://example.js"), ("type", "text/javascript")}),
                            }
                        ),
                        xmlElement(
                            "body",
                            attributes: new[] {("class", "my-class-1")},
                            children: new[] {
                                xmlText("Hello"),
                                xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                                xmlElement(
                                    "http://www.w3.org/2000/svg::svg",
                                    attributes: new[] {("width", "300"), ("height", "300")},
                                    nsDecls: new[] {new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                                    children: new[] {
                                        xmlElement("http://www.w3.org/2000/svg::text", attributes: new[] {("x", "0"), ("y", "0")}, children: new[] {xmlText("Hello")}),
                                        xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                                    }
                                )
                            }
                        )
                    }
                )
            ),

            (x: xmlList(), y: xmlText("")),
            (x: xmlList(), y: xmlText("helloworld")),
            (x: xmlText("helloworld"), y: xmlList(xmlText("hello"), xmlText("world"))),

            (
                x: xmlList(xmlText("a"), xmlText("b"), xmlText("c")),
                y: xmlList(xmlText("a"), xmlText("B"), xmlText("c"))
            ),
            (
                x: xmlList(xmlText("a"), xmlText("b"), xmlText("c")),
                y: xmlList(xmlText("a"), xmlText("b"), xmlText(""), xmlText("c"))
            ),
            (
                x: xmlList(xmlText("a"), xmlText("b"), xmlText("c")),
                y: xmlList(xmlText("a"), xmlText("b"), xmlComment("c"))
            ),
            (
                x: xmlList(xmlText("hello"), xmlText(" world"), xmlText("")),
                y: xmlList(xmlText("hel"), xmlText("lo w"), xmlText("orld"))
            ),
            (
                x: xmlList(
                    xmlText("hello"),
                    xmlElement("b", children: new[] {xmlElement("c", children: new[] {xmlText("hello")})})
                ),
                y: xmlList(
                    xmlElement("a", attributes: new[] {("x", "123")}, children: new[] {xmlText("hello")}),
                    xmlElement("b", attributes: new[] {("x", "123")}, children: new[] {xmlElement("c", children: new[] {xmlText("hello")})})
                )
            ),

            (
                x: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")}
                    ),
                    xmlComment(".123.456."),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlElement("a", attributes: new[] {("p", "")}, children: new[] {xmlText("Hello!")})
                ),
                y: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("y", "20"), ("x", "100")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")},
                        nsDecls: new[] {new ASNamespace("hello", "Hello")}
                    ),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlAttribute("aaa", "Hello!")
                )
            ),

            (
                x: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")}
                    ),
                    xmlComment(".123.456."),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlElement("a", attributes: new[] {("p", "")}, children: new[] {xmlText("Hello!")})
                ),
                y: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")},
                        nsDecls: new[] {new ASNamespace("hello", "Hello")}
                    ),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::pq"), xmlElement("Hello::rs")}
                    ),
                    xmlAttribute("aaa", "Hello!")
                )
            ),

            (
                x: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")}
                    ),
                    xmlComment(".123.456."),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlElement("a", attributes: new[] {("p", "")}, children: new[] {xmlText("Hello!")})
                ),
                y: xmlList(
                    xmlElement(
                        "foo",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("v", children: new[] {xmlText("abcd...efgh")}), xmlElement("Hello::pq")},
                        nsDecls: new[] {new ASNamespace("hello", "Hello")}
                    ),
                    xmlElement(
                        "bar",
                        attributes: new[] {("x", "100"), ("y", "20")},
                        children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                    ),
                    xmlAttribute("aaa", "Hello!")
                )
            )
        };

        public static IEnumerable<object[]> weakEqualsTest_data() =>
            Enumerable.Empty<object[]>()
                .Concat(equalityOperatorTests_data_alwaysEqual().Select(p => new object[] {p.x, p.y, true}))
                .Concat(equalityOperatorTests_data_weakEqualOnly().SelectMany(expandXmlArgs).Select(p => new object[] {p.x, p.y, true}))
                .Concat(equalityOperatorTests_data_alwaysNotEqual().SelectMany(expandXmlArgs).Select(p => new object[] {p.x, p.y, false}));

        [Theory]
        [MemberData(nameof(weakEqualsTest_data))]
        public void weakEqualsTest(ASAny x, ASAny y, bool expected) {
            Assert.Equal(expected, ASAny.AS_weakEq(x, y));
            Assert.Equal(expected, ASAny.AS_weakEq(y, x));

            if (!x.isUndefined && !y.isUndefined) {
                Assert.Equal(expected, ASObject.AS_weakEq(x.value, y.value));
                Assert.Equal(expected, ASObject.AS_weakEq(y.value, x.value));
            }
        }

        public static IEnumerable<object[]> strictEqualsTest_data() =>
            Enumerable.Empty<object[]>()
                .Concat(equalityOperatorTests_data_alwaysEqual().Select(p => new object[] {p.x, p.y, true}))
                .Concat(equalityOperatorTests_data_weakEqualOnly().SelectMany(expandXmlArgs).Select(p => new object[] {p.x, p.y, false}))
                .Concat(equalityOperatorTests_data_alwaysNotEqual().SelectMany(expandXmlArgs).Select(p => new object[] {p.x, p.y, false}));

        [Theory]
        [MemberData(nameof(strictEqualsTest_data))]
        public void strictEqualsTest(ASAny x, ASAny y, bool expected) {
            Assert.Equal(expected, ASAny.AS_strictEq(x, y));
            Assert.Equal(expected, ASAny.AS_strictEq(y, x));

            if (!x.isUndefined && !y.isUndefined) {
                Assert.Equal(expected, ASObject.AS_strictEq(x.value, y.value));
                Assert.Equal(expected, ASObject.AS_strictEq(y.value, x.value));
            }
        }

        public static IEnumerable<object[]> weakEqualsTest_XML_data() =>
            Enumerable.Empty<(ASAny x, ASAny y, bool result)>()
                .Concat(equalityOperatorTests_data_alwaysEqual().Select(p => (p.x, p.y, result: true)))
                .Concat(equalityOperatorTests_data_weakEqualOnly().Select(p => (p.x, p.y, result: true)))
                .Concat(equalityOperatorTests_data_alwaysNotEqual().Select(p => (p.x, p.y, result: false)))
                .Where(p => (p.x.isNull || p.x.value is ASXML) && (p.y.isNull || p.y.value is ASXML))
                .Select(p => new object[] {(ASXML)p.x.value, (ASXML)p.y.value, p.result});

        [Theory]
        [MemberData(nameof(weakEqualsTest_XML_data))]
        public void weakEqualsTest_XML(ASXML x, ASXML y, bool expected) {
            Assert.Equal(expected, ASXML.AS_weakEq(x, y));
            Assert.Equal(expected, ASXML.AS_weakEq(y, x));
        }

        public static IEnumerable<object[]> weakEqualsTest_XMLList_data() =>
            Enumerable.Empty<(ASAny x, ASAny y, bool result)>()
                .Concat(equalityOperatorTests_data_alwaysEqual().SelectMany(expandXmlArgs).Select(p => (p.x, p.y, result: true)))
                .Concat(equalityOperatorTests_data_weakEqualOnly().SelectMany(expandXmlArgs).Select(p => (p.x, p.y, result: true)))
                .Concat(equalityOperatorTests_data_alwaysNotEqual().SelectMany(expandXmlArgs).Select(p => (p.x, p.y, result: false)))
                .Where(p => (p.x.isNull || p.x.value is ASXMLList) && (p.y.isNull || p.y.value is ASXMLList))
                .Select(p => new object[] {(ASXMLList)p.x.value, (ASXMLList)p.y.value, p.result});

        [Theory]
        [MemberData(nameof(weakEqualsTest_XMLList_data))]
        public void weakEqualsTest_XMLList(ASXMLList x, ASXMLList y, bool expected) {
            Assert.Equal(expected, ASXMLList.AS_weakEq(x, y));
            Assert.Equal(expected, ASXMLList.AS_weakEq(y, x));
        }

        public static IEnumerable<object[]> addTest_data() {
            var rawTestDataNonXML = new (ASAny x, ASAny y, ASAny result)[] {
                (x: UNDEF, y: UNDEF, result: Double.NaN),
                (x: UNDEF, y: NULL, result: Double.NaN),
                (x: NULL, y: UNDEF, result: Double.NaN),
                (x: NULL, y: NULL, result: 0),

                (x: 0, y: 0, result: 0),
                (x: 0, y: 0.0, result: 0),
                (x: 1.0, y: 2u, result: 3),
                (x: 1, y: 2.5, result: 3.5),
                (x: 0.1, y: 0.2, result: 0.1 + 0.2),
                (x: Int32.MaxValue, y: 1, result: (double)Int32.MaxValue + 1.0),
                (x: Int32.MaxValue, y: Int32.MaxValue, result: (double)Int32.MaxValue * 2.0),
                (x: UInt32.MaxValue, y: 1, result: (double)UInt32.MaxValue + 1.0),
                (x: UInt32.MaxValue, y: UInt32.MaxValue, result: (double)UInt32.MaxValue * 2.0),
                (x: Int32.MinValue, y: -1, result: (double)Int32.MinValue - 1.0),
                (x: UInt32.MaxValue, y: -0.5, result: (double)UInt32.MaxValue - 0.5),
                (x: Double.MaxValue, y: Double.MinValue, result: 0),
                (x: Double.MaxValue, y: Double.MaxValue, result: Double.PositiveInfinity),
                (x: Double.PositiveInfinity, y: 1, result: Double.PositiveInfinity),
                (x: Double.PositiveInfinity, y: -1, result: Double.PositiveInfinity),
                (x: Double.PositiveInfinity, y: Double.PositiveInfinity, result: Double.PositiveInfinity),
                (x: Double.NegativeInfinity, y: 1, result: Double.NegativeInfinity),
                (x: Double.NegativeInfinity, y: -1, result: Double.NegativeInfinity),
                (x: Double.PositiveInfinity, y: Double.NegativeInfinity, result: Double.NaN),
                (x: Double.NaN, y: 1234, result: Double.NaN),
                (x: 1234, y: Double.NaN, result: Double.NaN),
                (x: Double.NaN, y: Double.PositiveInfinity, result: Double.NaN),
                (x: Double.NaN, y: Double.NaN, result: Double.NaN),

                (x: true, y: 4.5, result: 5.5),
                (x: 10000, y: false, result: 10000),
                (x: UInt32.MaxValue, y: true, result: (double)UInt32.MaxValue + 1.0),
                (x: true, y: true, result: 2),
                (x: false, y: false, result: 0),
                (x: true, y: Double.NaN, result: Double.NaN),
                (x: NULL, y: 4.5, result: 4.5),
                (x: 4.5, y: NULL, result: 4.5),
                (x: UNDEF, y: 4.5, result: Double.NaN),
                (x: 4.5, y: UNDEF, result: Double.NaN),

                (x: "", y: 123, result: "123"),
                (x: 123, y: "", result: "123"),
                (x: 123.4, y: "55", result: "123.455"),
                (x: "hello ", y: -13842, result: "hello -13842"),
                (x: "hello ", y: true, result: "hello true"),
                (x: "hello ", y: Double.NaN, result: "hello NaN"),
                (x: false, y: "", result: "false"),
                (x: "", y: "", result: ""),
                (x: "", y: "hello", result: "hello"),
                (x: "\ud833", y: "\ude7b", result: "\ud833\ude7b"),
                (x: "hello ", y: "world", result: "hello world"),
                (x: "hello ", y: NULL, result: "hello null"),
                (x: "hello ", y: UNDEF, result: "hello undefined"),
                (x: NULL, y: "", result: "null"),
                (x: UNDEF, y: "", result: "undefined"),

                (x: NULL, y: new ASDate(0), result: "null" + (new ASDate(0)).AS_toString()),
                (x: new ASDate(0), y: UNDEF, result: (new ASDate(0)).AS_toString() + "undefined"),
                (x: 1234, y: new ASDate(0), result: "1234" + (new ASDate(0)).AS_toString()),
                (x: "hello", y: new ASDate(0), result: "hello" + (new ASDate(0)).AS_toString()),
                (x: new ASDate(0), y: new ASDate(1), result: (new ASDate(0)).AS_toString() + (new ASDate(1)).AS_toString()),

                (x: 12, y: objWithMethods(("valueOf", 10)), result: 22),
                (x: objWithMethods(("valueOf", 10)), y: 12, result: 22),
                (x: 12, y: objWithMethods(("toString", 20)), result: 32),
                (x: objWithMethods(("toString", 20)), y: 12, result: 32),
                (x: 12, y: objWithMethods(("valueOf", 10), ("toString", 20)), result: 22),
                (x: objWithMethods(("valueOf", 10), ("toString", 20)), y: 12, result: 22),

                (x: true, y: objWithMethods(("valueOf", 10)), result: 11),
                (x: false, y: objWithMethods(("valueOf", true)), result: 1),
                (x: objWithMethods(("toString", true)), y: true, result: 2),
                (x: objWithMethods(("toString", false), ("valueOf", true)), y: true, result: 2),

                (x: NULL, y: objWithMethods(("valueOf", 4)), result: 4),
                (x: UNDEF, y: objWithMethods(("valueOf", 4)), result: Double.NaN),
                (x: NULL, y: objWithMethods(("toString", 4)), result: 4),
                (x: UNDEF, y: objWithMethods(("toString", 4)), result: Double.NaN),
                (x: NULL, y: objWithMethods(("valueOf", 7), ("toString", 4)), result: 7),
                (x: UNDEF, y: objWithMethods(("valueOf", 7), ("toString", 4)), result: Double.NaN),

                (x: objWithMethods(("valueOf", true)), y: NULL, result: 1),
                (x: objWithMethods(("toString", true)), y: NULL, result: 1),
                (x: objWithMethods(("valueOf", true), ("toString", 100)), y: NULL, result: 1),
                (x: objWithMethods(("valueOf", 100), ("toString", true)), y: NULL, result: 100),
                (x: objWithMethods(("valueOf", true)), y: UNDEF, result: Double.NaN),
                (x: objWithMethods(("toString", true)), y: UNDEF, result: Double.NaN),
                (x: objWithMethods(("valueOf", 32), ("toString", false)), y: UNDEF, result: Double.NaN),

                (x: "abc", y: objWithMethods(("toString", "ghi")), result: "abcghi"),
                (x: objWithMethods(("toString", "ghi")), y: "abc", result: "ghiabc"),
                (x: "abc", y: objWithMethods(("valueOf", "def"), ("toString", "ghi")), result: "abcghi"),
                (x: objWithMethods(("valueOf", "def"), ("toString", "ghi")), y: "abc", result: "ghiabc"),

                (
                    x: new ASDate(0),
                    y: objWithMethods(("valueOf", "def"), ("toString", "ghi")),
                    result: (new ASDate(0)).AS_toString() + "ghi"
                ),
                (
                    x: objWithMethods(("valueOf", "def"), ("toString", "ghi")),
                    y: new ASDate(0),
                    result: "ghi" + (new ASDate(0).AS_toString())
                ),

                (x: 123, y: objWithMethods(("valueOf", "def")), result: "123def"),
                (x: objWithMethods(("valueOf", "def")), y: 123, result: "def123"),
                (x: 123, y: objWithMethods(("toString", "ghi")), result: "123ghi"),
                (x: objWithMethods(("toString", "ghi")), y: 123, result: "ghi123"),
                (x: 123, y: objWithMethods(("valueOf", "def"), ("toString", "ghi")), result: "123def"),
                (x: objWithMethods(("valueOf", "def"), ("toString", "ghi")), y: 123, result: "def123"),
                (x: false, y: objWithMethods(("valueOf", "def"), ("toString", "ghi")), result: "falsedef"),
                (x: objWithMethods(("valueOf", "def"), ("toString", "ghi")), y: true, result: "deftrue"),

                (x: "abc", y: objWithMethods(("toString", 456)), result: "abc456"),
                (x: objWithMethods(("toString", 456)), y: "abc", result: "456abc"),
                (x: "abc", y: objWithMethods(("toString", true)), result: "abctrue"),
                (x: objWithMethods(("toString", false)), y: "abc", result: "falseabc"),
                (x: "abc", y: objWithMethods(("valueOf", 123), ("toString", 456)), result: "abc456"),
                (x: objWithMethods(("valueOf", 123), ("toString", 456)), y: "abc", result: "456abc"),
                (x: "abc", y: objWithMethods(("valueOf", true), ("toString", false)), result: "abcfalse"),
                (x: objWithMethods(("valueOf", false), ("toString", true)), y: "abc", result: "trueabc"),

                (x: NULL, y: objWithMethods(("valueOf", NULL)), result: 0),
                (x: objWithMethods(("valueOf", NULL)), y: NULL, result: 0),
                (x: NULL, y: objWithMethods(("toString", NULL)), result: 0),
                (x: objWithMethods(("toString", NULL)), y: NULL, result: 0),
                (x: UNDEF, y: objWithMethods(("valueOf", UNDEF)), result: Double.NaN),
                (x: objWithMethods(("valueOf", UNDEF)), y: UNDEF, result: Double.NaN),
                (x: UNDEF, y: objWithMethods(("toString", UNDEF)), result: Double.NaN),
                (x: objWithMethods(("toString", UNDEF)), y: UNDEF, result: Double.NaN),

                (x: NULL, y: objWithMethods(("valueOf", 2)), result: 2),
                (x: NULL, y: objWithMethods(("toString", 3)), result: 3),
                (x: NULL, y: objWithMethods(("valueOf", true)), result: 1),
                (x: NULL, y: objWithMethods(("toString", false)), result: 0),
                (x: NULL, y: objWithMethods(("valueOf", 2), ("toString", 3)), result: 2),
                (x: NULL, y: objWithMethods(("valueOf", true), ("toString", 3)), result: 1),
                (x: objWithMethods(("valueOf", 2), ("toString", 3)), y: NULL, result: 2),
                (x: objWithMethods(("valueOf", 2), ("toString", "abcd")), y: NULL, result: 2),
                (x: objWithMethods(("valueOf", false), ("toString", "abcd")), y: NULL, result: 0),
                (x: NULL, y: objWithMethods(("valueOf", NULL), ("toString", 3)), result: 0),
                (x: NULL, y: objWithMethods(("valueOf", NULL), ("toString", true)), result: 0),
                (x: objWithMethods(("valueOf", UNDEF), ("toString", 3)), y: NULL, result: Double.NaN),
                (x: objWithMethods(("valueOf", UNDEF), ("toString", "abcd")), y: NULL, result: Double.NaN),

                (x: UNDEF, y: objWithMethods(("valueOf", 2)), result: Double.NaN),
                (x: UNDEF, y: objWithMethods(("toString", 3)), result: Double.NaN),
                (x: UNDEF, y: objWithMethods(("valueOf", 2), ("toString", 3)), result: Double.NaN),
                (x: UNDEF, y: objWithMethods(("valueOf", 2), ("toString", "abcd")), result: Double.NaN),
                (x: UNDEF, y: objWithMethods(("valueOf", true), ("toString", "abcd")), result: Double.NaN),
                (x: objWithMethods(("valueOf", 2), ("toString", 3)), y: UNDEF, result: Double.NaN),
                (x: objWithMethods(("valueOf", NULL), ("toString", 3)), y: UNDEF, result: Double.NaN),
                (x: objWithMethods(("valueOf", NULL), ("toString", "abcd")), y: UNDEF, result: Double.NaN),
                (x: objWithMethods(("valueOf", false), ("toString", "abcd")), y: UNDEF, result: Double.NaN),

                (x: 44, y: objWithMethods(("valueOf", NULL)), result: 44),
                (x: objWithMethods(("toString", NULL)), y: 44, result: 44),
                (x: 44, y: objWithMethods(("valueOf", UNDEF)), result: Double.NaN),
                (x: objWithMethods(("toString", UNDEF)), y: 44, result: Double.NaN),
                (x: 44, y: objWithMethods(("valueOf", NULL), ("toString", 10)), result: 44),
                (x: objWithMethods(("valueOf", UNDEF), ("toString", 10)), y: 44, result: Double.NaN),

                (x: true, y: objWithMethods(("valueOf", NULL)), result: 1),
                (x: objWithMethods(("toString", NULL)), y: false, result: 0),
                (x: true, y: objWithMethods(("valueOf", UNDEF)), result: Double.NaN),
                (x: objWithMethods(("toString", UNDEF)), y: false, result: Double.NaN),
                (x: true, y: objWithMethods(("valueOf", NULL), ("toString", 10)), result: 1),
                (x: objWithMethods(("valueOf", UNDEF), ("toString", 10)), y: false, result: Double.NaN),

                (x: objWithMethods(("toString", NULL)), y: "abcd", result: "nullabcd"),
                (x: objWithMethods(("toString", UNDEF)), y: "abcd", result: "undefinedabcd"),
                (x: "abcd", y: objWithMethods(("valueOf", "ABC"), ("toString", NULL)), result: "abcdnull"),
                (x: objWithMethods(("valueOf", "ABC"), ("toString", UNDEF)), y: "abcd", result: "undefinedabcd"),

                (x: NULL, y: objWithMethods(("valueOf", "abcd")), result: "nullabcd"),
                (x: UNDEF, y: objWithMethods(("valueOf", "abcd")), result: "undefinedabcd"),
                (x: NULL, y: objWithMethods(("toString", "abcd")), result: "nullabcd"),
                (x: UNDEF, y: objWithMethods(("toString", "abcd")), result: "undefinedabcd"),
                (x: objWithMethods(("valueOf", "abcd")), y: NULL, result: "abcdnull"),
                (x: objWithMethods(("valueOf", "abcd")), y: UNDEF, result: "abcdundefined"),
                (x: objWithMethods(("toString", "abcd")), y: NULL, result: "abcdnull"),
                (x: objWithMethods(("toString", "abcd")), y: UNDEF, result: "abcdundefined"),

                (x: objWithMethods(("valueOf", "abcd"), ("toString", "ABCD")), y: NULL, result: "abcdnull"),
                (x: NULL, y: objWithMethods(("valueOf", "abcd"), ("toString", 123)), result: "nullabcd"),
                (x: UNDEF, y: objWithMethods(("valueOf", "abcd"), ("toString", 123)), result: "undefinedabcd"),

                (
                    x: objWithMethods(("valueOf", 12)),
                    y: objWithMethods(("valueOf", 34.5)),
                    result: 46.5
                ),
                (
                    x: objWithMethods(("toString", 12)),
                    y: objWithMethods(("toString", 34.5)),
                    result: 46.5
                ),
                (
                    x: objWithMethods(("valueOf", 12)),
                    y: objWithMethods(("toString", 34.5)),
                    result: 46.5
                ),
                (
                    x: objWithMethods(("valueOf", 12), ("toString", "ab")),
                    y: objWithMethods(("valueOf", 34.5), ("toString", "cd")),
                    result: 46.5
                ),
                (
                    x: objWithMethods(("valueOf", 12), ("toString", "ab")),
                    y: objWithMethods(("toString", 34.5)),
                    result: 46.5
                ),
                (
                    x: objWithMethods(("toString", 12)),
                    y: objWithMethods(("valueOf", 34.5), ("toString", "ABC")),
                    result: 46.5
                ),
                (
                    x: objWithMethods(("valueOf", true)),
                    y: objWithMethods(("toString", 34.5)),
                    result: 35.5
                ),
                (
                    x: objWithMethods(("valueOf", true), ("toString", 488)),
                    y: objWithMethods(("toString", false)),
                    result: 1
                ),
                (
                    x: objWithMethods(("valueOf", "abc")),
                    y: objWithMethods(("valueOf", "def")),
                    result: "abcdef"
                ),
                (
                    x: objWithMethods(("toString", "abc")),
                    y: objWithMethods(("toString", "def")),
                    result: "abcdef"
                ),
                (
                    x: objWithMethods(("valueOf", "abc"), ("toString", "ABC")),
                    y: objWithMethods(("valueOf", "def"), ("toString", "DEF")),
                    result: "abcdef"
                ),
                (
                    x: objWithMethods(("valueOf", "abc"), ("toString", "ABC")),
                    y: objWithMethods(("toString", "def")),
                    result: "abcdef"
                ),
                (
                    x: objWithMethods(("valueOf", 123)),
                    y: objWithMethods(("valueOf", "abc")),
                    result: "123abc"
                ),
                (
                    x: objWithMethods(("valueOf", false)),
                    y: objWithMethods(("valueOf", "abc")),
                    result: "falseabc"
                ),
                (
                    x: objWithMethods(("toString", 123)),
                    y: objWithMethods(("valueOf", "abc")),
                    result: "123abc"
                ),
                (
                    x: objWithMethods(("valueOf", "abc"), ("toString", 123)),
                    y: objWithMethods(("valueOf", 456), ("toString", "ABC")),
                    result: "abc456"
                ),
                (
                    x: objWithMethods(("valueOf", "abc"), ("toString", 123)),
                    y: objWithMethods(("valueOf", 456), ("toString", "ABC")),
                    result: "abc456"
                ),
                (
                    x: objWithMethods(("valueOf", "abc"), ("toString", true)),
                    y: objWithMethods(("valueOf", false), ("toString", "ABC")),
                    result: "abcfalse"
                ),
                (
                    x: objWithMethods(("valueOf", NULL)),
                    y: objWithMethods(("valueOf", NULL)),
                    result: 0
                ),
                (
                    x: objWithMethods(("toString", NULL)),
                    y: objWithMethods(("toString", NULL)),
                    result: 0
                ),
                (
                    x: objWithMethods(("valueOf", UNDEF)),
                    y: objWithMethods(("toString", NULL)),
                    result: Double.NaN
                ),
                (
                    x: objWithMethods(("toString", UNDEF)),
                    y: objWithMethods(("toString", UNDEF)),
                    result: Double.NaN
                ),
                (
                    x: objWithMethods(("valueOf", NULL)),
                    y: objWithMethods(("toString", 1238)),
                    result: 1238
                ),
                (
                    x: objWithMethods(("valueOf", true)),
                    y: objWithMethods(("toString", NULL)),
                    result: 1
                ),
                (
                    x: objWithMethods(("valueOf", true)),
                    y: objWithMethods(("valueOf", UNDEF), ("toString", "abc")),
                    result: Double.NaN
                ),
                (
                    x: objWithMethods(("valueOf", UNDEF), ("toString", NULL)),
                    y: objWithMethods(("valueOf", UNDEF), ("toString", NULL)),
                    result: Double.NaN
                ),
                (
                    x: objWithMethods(("valueOf", 123), ("toString", UNDEF)),
                    y: objWithMethods(("valueOf", NULL), ("toString", 456)),
                    result: 123
                ),
                (
                    x: objWithMethods(("valueOf", UNDEF), ("toString", "hello")),
                    y: objWithMethods(("toString", "abcd")),
                    result: "undefinedabcd"
                ),
                (
                    x: objWithMethods(("valueOf", UNDEF), ("toString", "hello")),
                    y: objWithMethods(("valueOf", 123), ("toString", "abcd")),
                    result: Double.NaN
                ),
                (
                    x: objWithMethods(("toString", NULL)),
                    y: objWithMethods(("valueOf", "hello"), ("toString", UNDEF)),
                    result: "nullhello"
                ),
                (
                    x: objWithMethods(("valueOf", "hello"), ("toString", 123)),
                    y: objWithMethods(("valueOf", true), ("toString", false)),
                    result: "hellotrue"
                ),
                (
                    x: objWithMethods(("valueOf", UNDEF), ("toString", "abc")),
                    y: objWithMethods(("valueOf", "def"), ("toString", NULL)),
                    result: "undefineddef"
                ),

                (x: "hello", y: xmlText("world"), result: "helloworld"),
                (x: xmlText("hello"), y: "world", result: "helloworld"),
                (x: xmlText("hello"), y: 123, result: "hello123"),
                (x: xmlText("hello"), y: true, result: "hellotrue"),
                (x: NULL, y: xmlText("world"), result: "nullworld"),
                (x: UNDEF, y: xmlText("world"), result: "undefinedworld"),
                (x: "hello", y: xmlCdata("world"), result: "helloworld"),
                (x: xmlCdata("hello"), y: "world", result: "helloworld"),
                (x: "hello", y: xmlAttribute("a", "world"), result: "helloworld"),
                (x: xmlAttribute("a", "hello"), y: "world", result: "helloworld"),
                (x: "hello", y: xmlElement("a", children: new[] {xmlText("world")}), result: "helloworld"),
                (x: xmlElement("a", children: new[] {xmlText("hello")}), y: "world", result: "helloworld"),
                (x: "hello", y: xmlElement("a", attributes: new[] {("x", "1")}), result: "hello"),
                (x: xmlElement("a", attributes: new[] {("x", "1")}), y: "world", result: "world"),
                (x: 123, y: xmlElement("a", children: new[] {xmlText("world")}), result: "123world"),
                (x: true, y: xmlElement("a", children: new[] {xmlText("world")}), result: "trueworld"),
                (x: xmlElement("a", children: new[] {xmlText("hello")}), y: NULL, result: "hellonull"),
                (x: xmlElement("a", children: new[] {xmlText("hello")}), y: UNDEF, result: "helloundefined"),

                (x: "hello", y: xmlList(), result: "hello"),
                (x: 1234, y: xmlList(), result: "1234"),
                (x: UNDEF, y: xmlList(), result: "undefined"),
                (x: "hello", y: xmlList(xmlText("world"), xmlText("123")), "helloworld123")
            };

            var testDataXML = new (ASAny x, ASAny y)[] {
                (x: xmlText("hello"), y: xmlText("world")),
                (x: xmlText("hello"), y: xmlCdata("world")),
                (x: xmlAttribute("a", "hello"), y: xmlAttribute("b", "world")),
                (x: xmlElement("a"), y: xmlElement("b")),
                (x: xmlElement("a", attributes: new[] {("x", "1")}), y: xmlElement("b", attributes: new[] {("x", "1")})),
                (x: xmlElement("a", children: new[] {xmlText("hello")}), y: xmlText("world")),
                (x: uniqueXml, y: uniqueXml),

                (x: xmlList(), y: xmlList()),
                (x: xmlList(), y: xmlText("hello")),
                (x: xmlElement("a", attributes: new[] {("x", "1")}, children: new[] {xmlText("hello")}), y: xmlList()),
                (x: xmlList(), y: xmlList(xmlText("a"), xmlText("b"), xmlText("c"))),
                (x: xmlList(xmlElement("a"), xmlText("b"), xmlElement("c")), y: xmlList()),
                (x: xmlElement("abc"), y: xmlList(xmlText("a"), xmlText("b"), xmlText("c"))),
                (x: xmlList(xmlElement("a"), xmlText("b"), xmlElement("c")), y: xmlElement("abc")),
                (x: xmlComment("abc"), y: xmlList(xmlText("a"), xmlText("b"), xmlText("c"))),
                (x: xmlList(xmlElement("a"), xmlText("b"), xmlElement("c")), y: xmlComment("abc")),
                (x: uniqueXmlList, y: uniqueXmlList),
                (x: uniqueXmlList, y: uniqueXmlList[0]),
                (x: uniqueXmlList[0], y: uniqueXmlList),

                (
                    x: xmlList(xmlText("a"), xmlText("b"), xmlText("c")),
                    y: xmlList(xmlText("a"), xmlText("b"), xmlText("c"))
                ),
                (
                    x: xmlList(xmlText("a"), xmlText("b"), xmlText("c")),
                    y: xmlList(xmlElement("a"), xmlComment("b"), xmlText("c"), xmlProcInstr("d", "..."))
                ),
                (
                    x: xmlList(xmlElement("a"), xmlComment("b"), xmlText("c"), xmlProcInstr("d", "...")),
                    y: xmlList(xmlText("a"), xmlText("b"), xmlText("c"))
                ),

                (
                    x: xmlList(
                        xmlText("Hello"),
                        xmlElement("p", attributes: new[] {("style", "font-size:120%")}, children: new[] {xmlText("Lorem ipsum")}),
                        xmlElement("img", attributes: new[] {("height", "200"), ("src", "http://example/image.jpg"), ("alt", "Alt text"), ("width", "200")}),
                        xmlElement(
                            "http://www.w3.org/2000/svg::svg",
                            attributes: new[] {("width", "300"), ("height", "300")},
                            nsDecls: new[] {new ASNamespace("xlink", "http://www.w3.org/1999/xlink")},
                            children: new[] {
                                xmlElement("http://www.w3.org/2000/svg::path", attributes: new[] {("d", "M 0,0 L 100,100 L 100,0 Z")})
                            }
                        )
                    ),
                    y: xmlList(
                        xmlElement(
                            "foo",
                            attributes: new[] {("y", "20"), ("x", "100")},
                            children: new[] {xmlText("abcd...efgh"), xmlElement("Hello::pq")},
                            nsDecls: new[] {new ASNamespace("hello", "Hello")}
                        ),
                        xmlComment(".123.456."),
                        xmlElement(
                            "bar",
                            attributes: new[] {("x", "100"), ("y", "20")},
                            children: new[] {xmlElement("Hello::pq"), xmlElement("Hello::rs"), xmlElement("Hello::pq")}
                        ),
                        xmlAttribute("aaa", "Hello!")
                    )
                )
            };

            var testCasesNonXML =
                rawTestDataNonXML.SelectMany(p => expandXmlArgs((p.x, p.y)).Select(q => (q.x, q.y, p.result)));

            var testCasesXML =
                testDataXML
                    .SelectMany(expandXmlArgs)
                    .Select(p => {
                        var xList = (p.x.value is ASXML xXml) ? xmlList(xXml) : (ASXMLList)p.x.value;
                        var yList = (p.y.value is ASXML yXml) ? xmlList(yXml) : (ASXMLList)p.y.value;
                        return (p.x, p.y, (ASAny)ASXMLList.fromEnumerable(xList.getItems().Concat(yList.getItems())));
                    });

            return Enumerable.Concat(testCasesNonXML, testCasesXML).Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(addTest_data))]
        public void addTest(ASAny x, ASAny y, ASAny expectedResult) {
            checkResult(ASAny.AS_add(x, y));

            if (x.isDefined && y.isDefined)
                checkResult(ASObject.AS_add(x.value, y.value));

            void checkResult(ASObject result) {
                if (expectedResult.value is ASXMLList expectedXmlList) {
                    var resultList = Assert.IsType<ASXMLList>(result);
                    Assert.Equal(expectedXmlList.length(), resultList.length());

                    for (int i = 0; i < resultList.length(); i++)
                        Assert.Same(expectedXmlList[i], resultList[i]);
                }
                else if (expectedResult.value is ASString) {
                    Assert.IsType<ASString>(result);
                    Assert.Equal((string)expectedResult, (string)result);
                }
                else {
                    Assert.True(ASObject.AS_isNumeric(result));
                    AssertHelper.floatIdentical((double)expectedResult, (double)result);
                }
            }
        }

        public static IEnumerable<object[]> compareOpsTest_data() {
            var objConvertingToNaN = objWithMethods(("valueOf", Double.NaN));
            var objConvertingToUndefined = objWithMethods(("valueOf", UNDEF));

            return TupleHelper.toArrays<ASAny, ASAny, CompareOpResult>(
                #pragma warning disable 8123

                (x: 0, y: 0, result: CompareOpResult.EQUAL),
                (x: 0.0, y: NEG_ZERO, result: CompareOpResult.EQUAL),
                (x: 0, y: 1, result: CompareOpResult.LESS),
                (x: 0.1, y: 0.1, result: CompareOpResult.EQUAL),
                (x: 0.3, y: 0.1 + 0.2, result: CompareOpResult.LESS),
                (x: Int32.MaxValue, y: (double)Int32.MaxValue, result: CompareOpResult.EQUAL),
                (x: Int32.MaxValue, y: (uint)Int32.MaxValue + 1, result: CompareOpResult.LESS),
                (x: (double)Int32.MinValue - 1.0, y: Int32.MinValue, result: CompareOpResult.LESS),
                (x: UInt32.MaxValue, y: (double)UInt32.MaxValue + 1.0, result: CompareOpResult.LESS),
                (x: Double.MaxValue, y: Double.MaxValue, result: CompareOpResult.EQUAL),
                (x: Double.PositiveInfinity, y: Double.PositiveInfinity, result: CompareOpResult.EQUAL),
                (x: Double.NegativeInfinity, y: Double.NegativeInfinity, result: CompareOpResult.EQUAL),
                (x: 0, y: Double.PositiveInfinity, result: CompareOpResult.LESS),
                (x: Double.NegativeInfinity, y: 0, result: CompareOpResult.LESS),
                (x: Double.NegativeInfinity, y: Double.PositiveInfinity, result: CompareOpResult.LESS),
                (x: Double.NaN, y: 0, result: CompareOpResult.NAN),
                (x: Double.NaN, y: 0.0, result: CompareOpResult.NAN),
                (x: Double.NaN, y: Double.PositiveInfinity, result: CompareOpResult.NAN),
                (x: Double.NaN, y: Double.NaN, result: CompareOpResult.NAN),
                (x: boxedNaN, y: boxedNaN, result: CompareOpResult.NAN),

                (x: "", y: "", result: CompareOpResult.EQUAL),
                (x: "", y: "abc", result: CompareOpResult.LESS),
                (x: "abc", y: "abc", result: CompareOpResult.EQUAL),
                (x: "abc", y: "abd", result: CompareOpResult.LESS),
                (x: "abc", y: "abcd", result: CompareOpResult.LESS),
                (x: "ABC", y: "ABc", result: CompareOpResult.LESS),
                (x: "123", y: "13", result: CompareOpResult.LESS),
                (x: "he\u0301llo", y: "héllo", result: CompareOpResult.LESS),
                (x: "encyclopaedia", y: "encyclopædia", result: CompareOpResult.LESS),
                (x: "encyclopbedia", y: "encyclopædia", result: CompareOpResult.LESS),
                (x: "\ud800", y: "\ud800", result: CompareOpResult.EQUAL),
                (x: "\ud800", y: "\ud801", result: CompareOpResult.LESS),
                (x: "\udc00", y: "\udc01", result: CompareOpResult.LESS),

                (x: 0, y: "", result: CompareOpResult.EQUAL),
                (x: 123, y: "123", result: CompareOpResult.EQUAL),
                (x: 123, y: " \t 123 \r\n ", result: CompareOpResult.EQUAL),
                (x: 123, y: "123.000", result: CompareOpResult.EQUAL),
                (x: 123.456, y: "1.23456E2", result: CompareOpResult.EQUAL),
                (x: -123.456, y: "-1234560e-4", result: CompareOpResult.EQUAL),
                (x: 2047, y: "0x7ff", result: CompareOpResult.EQUAL),
                (x: 2047, y: " 0x7fF  ", result: CompareOpResult.EQUAL),
                (x: -2047, y: "-0X7FF", result: CompareOpResult.EQUAL),
                (x: 1000000, y: "1e+6", result: CompareOpResult.EQUAL),

                (x: 123, y: "123.00001", result: CompareOpResult.LESS),
                (x: -123, y: "-122.99999", result: CompareOpResult.LESS),
                (x: 123.456, y: "0.12346e+3", result: CompareOpResult.LESS),
                (x: -123.456, y: "0.12346e+3", result: CompareOpResult.LESS),
                (x: 301, y: "2389", result: CompareOpResult.LESS),
                (x: -301, y: "-97", result: CompareOpResult.LESS),
                (x: 123, y: "\r  123.00001  \t\v  \n", result: CompareOpResult.LESS),
                (x: 2047, y: " 0x80a  ", result: CompareOpResult.LESS),
                (x: 2046.99999, y: "0x7ff", result: CompareOpResult.LESS),
                (x: 999999, y: "1e+6", result: CompareOpResult.LESS),
                (x: "122.9999", y: 123, result: CompareOpResult.LESS),
                (x: "  \r 122.9999 \t\n ", y: 123, result: CompareOpResult.LESS),
                (x: "-123.0001", y: 123, result: CompareOpResult.LESS),
                (x: "2819", y: 2930, result: CompareOpResult.LESS),
                (x: " 2819  ", y: 2930, result: CompareOpResult.LESS),
                (x: "  -2933", y: -2930, result: CompareOpResult.LESS),
                (x: "58", y: 301, result: CompareOpResult.LESS),
                (x: "-10284", y: -301, result: CompareOpResult.LESS),
                (x: "0x7ff", y: 2048.000001, result: CompareOpResult.LESS),
                (x: " 0x7ff  ", y: 2048.000001, result: CompareOpResult.LESS),
                (x: " -0X7FE\n", y: -2045.999999, result: CompareOpResult.LESS),
                (x: "1e+6", y: 1000001, result: CompareOpResult.LESS),

                (x: Double.PositiveInfinity, y: "Infinity", result: CompareOpResult.EQUAL),
                (x: Double.PositiveInfinity, y: "  Infinity  ", result: CompareOpResult.EQUAL),
                (x: Double.NegativeInfinity, y: "-Infinity", result: CompareOpResult.EQUAL),
                (x: Double.NegativeInfinity, y: "-Infinity", result: CompareOpResult.EQUAL),
                (x: Double.NegativeInfinity, y: "\t\n\n  -Infinity \r", result: CompareOpResult.EQUAL),

                (x: Double.NegativeInfinity, y: "-1939", result: CompareOpResult.LESS),
                (x: Double.NegativeInfinity, y: "Infinity", result: CompareOpResult.LESS),
                (x: "-Infinity", y: -1938, result: CompareOpResult.LESS),
                (x: "-Infinity", y: 2.39488e+300, result: CompareOpResult.LESS),
                (x: -5e+201, y: "Infinity", result: CompareOpResult.LESS),
                (x: "382.49939", y: Double.PositiveInfinity, result: CompareOpResult.LESS),
                (x: " -Infinity  ", y: Double.PositiveInfinity, result: CompareOpResult.LESS),

                (x: Double.NaN, y: "NaN", result: CompareOpResult.NAN),
                (x: Double.NaN, y: "", result: CompareOpResult.NAN),
                (x: Double.NaN, y: "1234", result: CompareOpResult.NAN),
                (x: 123, y: "hello", result: CompareOpResult.NAN),
                (x: 123.456, y: "123,456", result: CompareOpResult.NAN),

                (x: false, y: false, result: CompareOpResult.EQUAL),
                (x: true, y: true, result: CompareOpResult.EQUAL),
                (x: false, y: true, result: CompareOpResult.LESS),
                (x: -1, y: false, result: CompareOpResult.LESS),
                (x: true, y: 1.0000001, result: CompareOpResult.LESS),
                (x: false, y: "0.0000001", result: CompareOpResult.LESS),
                (x: "-0.0000001", y: false, result: CompareOpResult.LESS),
                (x: true, y: "1.0000001", result: CompareOpResult.LESS),
                (x: "0.9999999", y: true, result: CompareOpResult.LESS),
                (x: false, y: "false", result: CompareOpResult.NAN),
                (x: true, y: "true", result: CompareOpResult.NAN),

                (x: NULL, y: NULL, result: CompareOpResult.EQUAL),
                (x: NULL, y: 0.0, result: CompareOpResult.EQUAL),
                (x: NULL, y: false, result: CompareOpResult.EQUAL),
                (x: NULL, y: 123, result: CompareOpResult.LESS),
                (x: -123, y: NULL, result: CompareOpResult.LESS),
                (x: NULL, y: Double.Epsilon, result: CompareOpResult.LESS),
                (x: NULL, y: "123", result: CompareOpResult.LESS),
                (x: "-123", y: NULL, result: CompareOpResult.LESS),
                (x: NULL, y: true, result: CompareOpResult.LESS),

                (x: UNDEF, y: UNDEF, result: CompareOpResult.NAN),
                (x: UNDEF, y: NULL, result: CompareOpResult.NAN),
                (x: UNDEF, y: 123, result: CompareOpResult.NAN),
                (x: UNDEF, y: -123, result: CompareOpResult.NAN),
                (x: UNDEF, y: "", result: CompareOpResult.NAN),
                (x: UNDEF, y: "123", result: CompareOpResult.NAN),
                (x: UNDEF, y: "zzz", result: CompareOpResult.NAN),
                (x: UNDEF, y: false, result: CompareOpResult.NAN),
                (x: UNDEF, y: true, result: CompareOpResult.NAN),

                (x: 123, y: objWithMethods(("valueOf", 123)), result: CompareOpResult.EQUAL),
                (x: 123, y: objWithMethods(("valueOf", "+123.0000")), result: CompareOpResult.EQUAL),
                (x: -123, y: objWithMethods(("valueOf", "-123.0000")), result: CompareOpResult.EQUAL),
                (x: 123, y: objWithMethods(("toString", 123)), result: CompareOpResult.EQUAL),
                (x: 123, y: objWithMethods(("toString", "123.0000")), result: CompareOpResult.EQUAL),
                (x: 123, y: objWithMethods(("valueOf", 123), ("toString", 125)), result: CompareOpResult.EQUAL),
                (x: 123, y: objWithMethods(("valueOf", "123.0000"), ("toString", 125)), result: CompareOpResult.EQUAL),
                (x: -123, y: objWithMethods(("valueOf", "-123.0000"), ("toString", 125)), result: CompareOpResult.EQUAL),
                (x: -123, y: objWithMethods(("valueOf", "-123.0000"), ("toString", Double.NaN)), result: CompareOpResult.EQUAL),
                (x: -123, y: objWithMethods(("valueOf", "-123.0000"), ("toString", UNDEF)), result: CompareOpResult.EQUAL),

                (x: 123, y: objWithMethods(("valueOf", 125)), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("valueOf", "+125.0000")), result: CompareOpResult.LESS),
                (x: -123.0002, y: objWithMethods(("valueOf", "-123.0001")), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("toString", 125)), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("toString", "125.0000")), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("valueOf", 125), ("toString", 121)), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("valueOf", "125.0000"), ("toString", 121)), result: CompareOpResult.LESS),
                (x: -123, y: objWithMethods(("valueOf", "-1229999e-4"), ("toString", -121)), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("valueOf", 125), ("toString", Double.NaN)), result: CompareOpResult.LESS),
                (x: 123, y: objWithMethods(("valueOf", 125), ("toString", UNDEF)), result: CompareOpResult.LESS),

                (x: "123", y: objWithMethods(("valueOf", 123)), result: CompareOpResult.EQUAL),
                (x: "+123.0000", y: objWithMethods(("valueOf", 123)), result: CompareOpResult.EQUAL),
                (x: "-123.0000", y: objWithMethods(("valueOf", -123.0)), result: CompareOpResult.EQUAL),
                (x: "123", y: objWithMethods(("toString", 123)), result: CompareOpResult.EQUAL),
                (x: "123.0000", y: objWithMethods(("toString", 123)), result: CompareOpResult.EQUAL),
                (x: "123", y: objWithMethods(("valueOf", 123), ("toString", "125")), result: CompareOpResult.EQUAL),
                (x: "-123", y: objWithMethods(("valueOf", -123), ("toString", "125")), result: CompareOpResult.EQUAL),

                (x: "123", y: objWithMethods(("valueOf", 124)), result: CompareOpResult.LESS),
                (x: "123.999999", y: objWithMethods(("valueOf", 124)), result: CompareOpResult.LESS),
                (x: "-125", y: objWithMethods(("valueOf", -124)), result: CompareOpResult.LESS),
                (x: "123", y: objWithMethods(("toString", 123.00001)), result: CompareOpResult.LESS),
                (x: "+123.0000", y: objWithMethods(("toString", 124)), result: CompareOpResult.LESS),
                (x: "123", y: objWithMethods(("valueOf", 126), ("toString", "121")), result: CompareOpResult.LESS),
                (x: "-223.7", y: objWithMethods(("valueOf", -223.4), ("toString", "-101")), result: CompareOpResult.LESS),

                (x: "abcd", y: objWithMethods(("valueOf", "abcd")), result: CompareOpResult.EQUAL),
                (x: "abcd", y: objWithMethods(("toString", "abcd")), result: CompareOpResult.EQUAL),
                (x: "abcd", y: objWithMethods(("valueOf", "abcd"), ("toString", "efgh")), result: CompareOpResult.EQUAL),
                (x: "abcd", y: objWithMethods(("valueOf", "abcde")), result: CompareOpResult.LESS),
                (x: "abcd", y: objWithMethods(("toString", "abdd")), result: CompareOpResult.LESS),
                (x: "abcd", y: objWithMethods(("valueOf", "abdd"), ("toString", "aaa")), result: CompareOpResult.LESS),
                (x: "abcd", y: objWithMethods(("valueOf", "abdd"), ("toString", UNDEF)), result: CompareOpResult.LESS),
                (x: "3456", y: objWithMethods(("valueOf", "445"), ("toString", 5013)), result: CompareOpResult.LESS),

                (x: false, y: objWithMethods(("valueOf", false)), result: CompareOpResult.EQUAL),
                (x: false, y: objWithMethods(("valueOf", true)), result: CompareOpResult.LESS),
                (x: true, y: objWithMethods(("valueOf", 2)), result: CompareOpResult.LESS),
                (x: false, y: objWithMethods(("toString", false)), result: CompareOpResult.EQUAL),
                (x: false, y: objWithMethods(("toString", true)), result: CompareOpResult.LESS),
                (x: true, y: objWithMethods(("toString", 2)), result: CompareOpResult.LESS),
                (x: false, y: objWithMethods(("valueOf", false), ("toString", true)), result: CompareOpResult.EQUAL),
                (x: false, y: objWithMethods(("valueOf", true), ("toString", -1)), result: CompareOpResult.LESS),
                (x: true, y: objWithMethods(("valueOf", 2), ("toString", false)), result: CompareOpResult.LESS),
                (x: true, y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: true, y: objWithMethods(("valueOf", "true")), result: CompareOpResult.NAN),

                (x: NULL, y: objWithMethods(("valueOf", 0)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", "")), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", false)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", 1)), result: CompareOpResult.LESS),
                (x: NULL, y: objWithMethods(("valueOf", " 1 ")), result: CompareOpResult.LESS),
                (x: NULL, y: objWithMethods(("toString", 0)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("toString", "")), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("toString", false)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("toString", NULL)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("toString", 1)), result: CompareOpResult.LESS),
                (x: NULL, y: objWithMethods(("toString", "1")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", -1)), y: NULL, result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", "-1")), y: NULL, result: CompareOpResult.LESS),
                (x: NULL, y: objWithMethods(("valueOf", 0), ("toString", "123")), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", "0"), ("toString", "abc")), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", NULL), ("toString", 123)), result: CompareOpResult.EQUAL),
                (x: NULL, y: objWithMethods(("valueOf", 12), ("toString", "")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", -1), ("toString", "123")), y: NULL, result: CompareOpResult.LESS),
                (x: NULL, y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: NULL, y: objWithMethods(("valueOf", "null")), result: CompareOpResult.NAN),
                (x: NULL, y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: NULL, y: objWithMethods(("toString", "null")), result: CompareOpResult.NAN),
                (x: NULL, y: objWithMethods(("valueOf", "abcd"), ("toString", "1234")), result: CompareOpResult.NAN),

                (x: 0, y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: "", y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: " 0 ", y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: false, y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: -1, y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.LESS),
                (x: "-Infinity", y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.LESS),
                (x: 0, y: objWithMethods(("toString", NULL)), result: CompareOpResult.EQUAL),
                (x: "", y: objWithMethods(("toString", NULL)), result: CompareOpResult.EQUAL),
                (x: " 0 ", y: objWithMethods(("toString", NULL)), result: CompareOpResult.EQUAL),
                (x: false, y: objWithMethods(("toString", NULL)), result: CompareOpResult.EQUAL),
                (x: -1, y: objWithMethods(("toString", NULL)), result: CompareOpResult.LESS),
                (x: "-Infinity", y: objWithMethods(("toString", NULL)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", NULL)), y: 1, result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", NULL)), y: true, result: CompareOpResult.LESS),
                (x: 0, y: objWithMethods(("valueOf", NULL), ("toString", -123)), result: CompareOpResult.EQUAL),
                (x: false, y: objWithMethods(("valueOf", NULL), ("toString", -123)), result: CompareOpResult.EQUAL),
                (x: -1, y: objWithMethods(("valueOf", NULL), ("toString", -10)), result: CompareOpResult.LESS),
                (x: "-123", y: objWithMethods(("valueOf", NULL), ("toString", -10)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", NULL), ("toString", true)), y: false, result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", NULL), ("toString", "50")), y: 2, result: CompareOpResult.LESS),
                (x: "hello", y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.NAN),
                (x: "hello", y: objWithMethods(("valueOf", NULL), ("toString", "hello")), result: CompareOpResult.NAN),
                (x: "null", y: objWithMethods(("valueOf", NULL), ("toString", 1234)), result: CompareOpResult.NAN),

                (x: UNDEF, y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("valueOf", 0)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("valueOf", 1234)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("valueOf", "undefined")), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("valueOf", false)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("toString", NULL)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("toString", 0)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("toString", 1234)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("toString", "undefined")), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("toString", false)), result: CompareOpResult.NAN),
                (x: UNDEF, y: objWithMethods(("valueOf", "undefined"), ("toString", "undefined")), result: CompareOpResult.NAN),

                (x: NULL, y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: 0, y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: 1234, y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: "undefined", y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: false, y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: NULL, y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: 0, y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: 1234, y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: "undefined", y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: false, y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: NULL, y: objWithMethods(("valueOf", UNDEF), ("toString", NULL)), result: CompareOpResult.NAN),
                (x: 0, y: objWithMethods(("valueOf", UNDEF), ("toString", 1234)), result: CompareOpResult.NAN),
                (x: 1234, y: objWithMethods(("valueOf", UNDEF), ("toString", 1235)), result: CompareOpResult.NAN),
                (x: "undefined", y: objWithMethods(("valueOf", UNDEF), ("toString", "zzz")), result: CompareOpResult.NAN),
                (x: false, y: objWithMethods(("valueOf", UNDEF), ("toString", true)), result: CompareOpResult.NAN),

                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("valueOf", 123)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("valueOf", "123")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("valueOf", 123.0)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", "abcd")), y: objWithMethods(("valueOf", "abcd")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", true)), y: objWithMethods(("valueOf", true)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", true)), y: objWithMethods(("valueOf", 1.0)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", 1234.56)), y: objWithMethods(("valueOf", "123456E-2")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("valueOf", " 0 ")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("valueOf", 125)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("valueOf", " 125.0 ")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", "abcd")), y: objWithMethods(("valueOf", "abce")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", "2453")), y: objWithMethods(("valueOf", "269")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", false)), y: objWithMethods(("valueOf", true)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", true)), y: objWithMethods(("valueOf", "2")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("valueOf", 1)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", Double.NaN)), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", 1234)), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", "NaN")), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", false)), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("valueOf", "abc")), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("valueOf", "null")), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", UNDEF)), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", 1234)), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", "undefined")), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", false)), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),

                (x: objWithMethods(("toString", 123)), y: objWithMethods(("toString", 123)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("toString", "123")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("toString", 123.0)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", "abcd")), y: objWithMethods(("toString", "abcd")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", true)), y: objWithMethods(("toString", true)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", true)), y: objWithMethods(("toString", 1.0)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", 1234.56)), y: objWithMethods(("toString", "123456E-2")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("toString", NULL)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("toString", " 0 ")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("toString", 125)), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("toString", " 125.0 ")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", "abcd")), y: objWithMethods(("toString", "abce")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", "2453")), y: objWithMethods(("toString", "269")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", false)), y: objWithMethods(("toString", true)), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", true)), y: objWithMethods(("toString", "2")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("toString", 1)), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", Double.NaN)), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", 1234)), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", "NaN")), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", false)), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("toString", "abc")), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("toString", "null")), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", UNDEF)), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", 1234)), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", "undefined")), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", false)), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),

                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("toString", 123)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("valueOf", "123")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("toString", 123.0)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", "abcd")), y: objWithMethods(("valueOf", "abcd")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", true)), y: objWithMethods(("toString", true)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", true)), y: objWithMethods(("valueOf", 1.0)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", 1234.56)), y: objWithMethods(("toString", "123456E-2")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("valueOf", NULL)), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("toString", " 0 ")), result: CompareOpResult.EQUAL),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("valueOf", 125)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", 123)), y: objWithMethods(("toString", " 125.0 ")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", "abcd")), y: objWithMethods(("valueOf", "abce")), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", "2453")), y: objWithMethods(("toString", "269")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", false)), y: objWithMethods(("valueOf", true)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", true)), y: objWithMethods(("toString", "2")), result: CompareOpResult.LESS),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("valueOf", 1)), result: CompareOpResult.LESS),
                (x: objWithMethods(("valueOf", Double.NaN)), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", 1234)), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", "NaN")), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", false)), y: objWithMethods(("valueOf", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("toString", Double.NaN)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", 123)), y: objWithMethods(("valueOf", "abc")), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", NULL)), y: objWithMethods(("toString", "null")), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", UNDEF)), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", 1234)), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", "undefined")), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("valueOf", false)), y: objWithMethods(("toString", UNDEF)), result: CompareOpResult.NAN),
                (x: objWithMethods(("toString", NULL)), y: objWithMethods(("valueOf", UNDEF)), result: CompareOpResult.NAN),

                (
                    x: objWithMethods(("valueOf", 1234), ("toString", 3456)),
                    y: objWithMethods(("valueOf", 1234), ("toString", 4567)),
                    result: CompareOpResult.EQUAL
                ),
                (
                    x: objWithMethods(("valueOf", 1234), ("toString", 3456)),
                    y: objWithMethods(("toString", "  +1234  ")),
                    result: CompareOpResult.EQUAL
                ),
                (
                    x: objWithMethods(("valueOf", "abcd"), ("toString", 1234)),
                    y: objWithMethods(("valueOf", "abcd"), ("toString", 1235)),
                    result: CompareOpResult.EQUAL
                ),
                (
                    x: objWithMethods(("valueOf", true), ("toString", false)),
                    y: objWithMethods(("valueOf", 1.0), ("toString", -1.0)),
                    result: CompareOpResult.EQUAL
                ),
                (
                    x: objWithMethods(("valueOf", false), ("toString", UNDEF)),
                    y: objWithMethods(("valueOf", 0.0), ("toString", Double.NaN)),
                    result: CompareOpResult.EQUAL
                ),
                (
                    x: objWithMethods(("valueOf", " -0 "), ("toString", " 1.0 ")),
                    y: objWithMethods(("valueOf", NULL)),
                    result: CompareOpResult.EQUAL
                ),
                (
                    x: objWithMethods(("valueOf", 1234), ("toString", 1234)),
                    y: objWithMethods(("valueOf", 3456), ("toString", "1234")),
                    result: CompareOpResult.LESS
                ),
                (
                    x: objWithMethods(("valueOf", 1234), ("toString", Double.NaN)),
                    y: objWithMethods(("valueOf", 3456), ("toString", UNDEF)),
                    result: CompareOpResult.LESS
                ),
                (
                    x: objWithMethods(("valueOf", "abcd"), ("toString", 1234)),
                    y: objWithMethods(("valueOf", "abcf"), ("toString", 1234)),
                    result: CompareOpResult.LESS
                ),
                (
                    x: objWithMethods(("valueOf", "123"), ("toString", "abc")),
                    y: objWithMethods(("valueOf", 153), ("toString", "aac")),
                    result: CompareOpResult.LESS
                ),
                (
                    x: objWithMethods(("valueOf", false), ("toString", true)),
                    y: objWithMethods(("valueOf", true)),
                    result: CompareOpResult.LESS
                ),
                (
                    x: objWithMethods(("toString", "")),
                    y: objWithMethods(("valueOf", true), ("toString", false)),
                    result: CompareOpResult.LESS
                ),
                (
                    x: objWithMethods(("valueOf", 1), ("toString", 2)),
                    y: objWithMethods(("valueOf", Double.NaN), ("toString", 5)),
                    result: CompareOpResult.NAN
                ),
                (
                    x: objWithMethods(("valueOf", 1), ("toString", 2)),
                    y: objWithMethods(("valueOf", UNDEF), ("toString", 5)),
                    result: CompareOpResult.NAN
                ),
                (
                    x: objWithMethods(("valueOf", 1), ("toString", 2)),
                    y: objWithMethods(("valueOf", "abcd"), ("toString", 5)),
                    result: CompareOpResult.NAN
                ),

                (x: objConvertingToNaN, y: objConvertingToNaN, result: CompareOpResult.NAN),
                (x: objConvertingToUndefined, y: objConvertingToUndefined, result: CompareOpResult.NAN)

                #pragma warning restore 8123
            );
        }

        [Theory]
        [MemberData(nameof(compareOpsTest_data))]
        public void compareOpsTest(ASAny x, ASAny y, CompareOpResult expectedResult) {
            bool isLess = false, isLessEq = false, isGreater = false, isGreaterEq = false;

            if (expectedResult != CompareOpResult.NAN) {
                isLess = expectedResult != CompareOpResult.EQUAL;
                isLessEq = true;
                isGreater = false;
                isGreaterEq = !isLess;
            }

            Assert.Equal(isLess, ASAny.AS_lessThan(x, y));
            Assert.Equal(isLess, ASAny.AS_greaterThan(y, x));
            Assert.Equal(isLessEq, ASAny.AS_lessEq(x, y));
            Assert.Equal(isLessEq, ASAny.AS_greaterEq(y, x));
            Assert.Equal(isGreater, ASAny.AS_greaterThan(x, y));
            Assert.Equal(isGreater, ASAny.AS_lessThan(y, x));
            Assert.Equal(isGreaterEq, ASAny.AS_greaterEq(x, y));
            Assert.Equal(isGreaterEq, ASAny.AS_lessEq(y, x));

            if (x.isDefined && y.isDefined) {
                Assert.Equal(isLess, ASObject.AS_lessThan(x.value, y.value));
                Assert.Equal(isLess, ASObject.AS_greaterThan(y.value, x.value));
                Assert.Equal(isLessEq, ASObject.AS_lessEq(x.value, y.value));
                Assert.Equal(isLessEq, ASObject.AS_greaterEq(y.value, x.value));
                Assert.Equal(isGreater, ASObject.AS_greaterThan(x.value, y.value));
                Assert.Equal(isGreater, ASObject.AS_lessThan(y.value, x.value));
                Assert.Equal(isGreaterEq, ASObject.AS_greaterEq(x.value, y.value));
                Assert.Equal(isGreaterEq, ASObject.AS_lessEq(y.value, x.value));
            }
        }

        public static IEnumerable<object[]> stringAddTest_data = TupleHelper.toArrays(
            (Array.Empty<string>(), ""),

            (new string[] {null}, "null"),
            (new[] {""}, ""),
            (new[] {"hello"}, "hello"),

            (new string[] {null, null}, "nullnull"),
            (new[] {"", null}, "null"),
            (new[] {null, ""}, "null"),
            (new[] {"hello", null}, "hellonull"),
            (new[] {null, "hello"}, "nullhello"),
            (new[] {"", ""}, ""),
            (new[] {"", "hello"}, "hello"),
            (new[] {"hello", ""}, "hello"),
            (new[] {"hello", "hello"}, "hellohello"),
            (new[] {"abc", "defg"}, "abcdefg"),
            (new[] {"he", "\u0301llo"}, "he\u0301llo"),
            (new[] {"\ud854", "\udd43"}, "\ud854\udd43"),
            (new[] {"abcd\0", "defg"}, "abcd\0defg"),

            (new string[] {null, null, null}, "nullnullnull"),
            (new[] {null, null, ""}, "nullnull"),
            (new[] {null, "", null}, "nullnull"),
            (new[] {null, "", ""}, "null"),
            (new[] {null, null, "hello"}, "nullnullhello"),
            (new[] {"abc", null, "def"}, "abcnulldef"),
            (new[] {null, "", "abc"}, "nullabc"),

            (new[] {"", "", ""}, ""),
            (new[] {"", "a", ""}, "a"),
            (new[] {"a", "", "b"}, "ab"),
            (new[] {"abc", "def", ""}, "abcdef"),
            (new[] {"", "abc", "def"}, "abcdef"),
            (new[] {"abc", "", "def"}, "abcdef"),
            (new[] {"abc", "de", "fghi"}, "abcdefghi"),
            (new[] {"abc", "de\0d", "fghi"}, "abcde\0dfghi"),

            (new string[] {null, null, null, null}, "nullnullnullnull"),
            (new[] {null, "abc", "", null}, "nullabcnull"),
            (new[] {"a", null, null, "b"}, "anullnullb"),
            (new[] {null, null, "", null}, "nullnullnull"),
            (new[] {"a", null, "b", null}, "anullbnull"),
            (new[] {"abc", null, "de", ""}, "abcnullde"),
            (new[] {"", "", "", ""}, ""),
            (new[] {"", "abc", "", "def"}, "abcdef"),
            (new[] {"ab", "", "", ""}, "ab"),
            (new[] {"abcd", "", "", "1234"}, "abcd1234"),
            (new[] {"", "12", "34", ""}, "1234"),
            (new[] {"ab", "cd", "ef", "ghi"}, "abcdefghi"),
            (new[] {"ab", "ab", "ab", "ab"}, "abababab"),
            (new[] {"ab", "cd\0", "ef", "ghi"}, "abcd\0efghi"),
            (new[] {"ab", "cd", "ef", "ghi\0"}, "abcdefghi\0"),

            (new string[] {null, null, null, null, null, null}, "nullnullnullnullnullnull"),
            (new[] {"ab", null, "", null, "cd", "", "", "EF", null, "gHI", "12", ""}, "abnullnullcdEFnullgHI12"),
            (new[] {"ab", "Cd", "132", "gJr", "12*&5d", "", "ghTRz", "(89"}, "abCd132gJr12*&5dghTRz(89"),
            (new[] {"\ud800\udfff\ud800", "", "\udfee\udc87", "\ud894\ud76a\ud803", "\udd66"}, "\ud800\udfff\ud800\udfee\udc87\ud894\ud76a\ud803\udd66")
        );

        [Theory]
        [MemberData(nameof(stringAddTest_data))]
        public void stringAddTest(string[] args, string expected) {
            Assert.Equal(expected, ASString.AS_add(args));

            if (args.Length == 2)
                Assert.Equal(expected, ASString.AS_add(args[0], args[1]));
            else if (args.Length == 3)
                Assert.Equal(expected, ASString.AS_add(args[0], args[1], args[2]));
            else if (args.Length == 4)
                Assert.Equal(expected, ASString.AS_add(args[0], args[1], args[2], args[3]));
        }

        public static IEnumerable<object[]> stringCompareOpsTest_data = TupleHelper.toArrays<string, string, CompareOpResult>(
            #pragma warning disable 8123

            (x: "", y: "", result: CompareOpResult.EQUAL),
            (x: "abcd", y: "abcd", result: CompareOpResult.EQUAL),
            (x: "\ud800\udfff", y: "\ud800\udfff", result: CompareOpResult.EQUAL),
            (x: "\udfff\ud800", y: "\udfff\ud800", result: CompareOpResult.EQUAL),

            (x: "", y: "abc", result: CompareOpResult.LESS),
            (x: "abc", y: "abd", result: CompareOpResult.LESS),
            (x: "abc", y: "abcd", result: CompareOpResult.LESS),
            (x: "abc", y: "abda", result: CompareOpResult.LESS),
            (x: "abc", y: "b", result: CompareOpResult.LESS),
            (x: "abC", y: "abc", result: CompareOpResult.LESS),
            (x: "he\u0301llo", y: "héllo", result: CompareOpResult.LESS),
            (x: "encyclopaedia", y: "encyclopædia", result: CompareOpResult.LESS),
            (x: "encyclopbedia", y: "encyclopædia", result: CompareOpResult.LESS),
            (x: "\ud800", y: "\ud801", result: CompareOpResult.LESS),
            (x: "\udc00 ", y: "\udc01 ", result: CompareOpResult.LESS),

            (x: null, y: null, result: CompareOpResult.EQUAL),
            (x: null, y: "", result: CompareOpResult.EQUAL),
            (x: null, y: "  \r\n  \u200b \t  \n \u3000", result: CompareOpResult.EQUAL),
            (x: null, y: "0", result: CompareOpResult.EQUAL),
            (x: null, y: "  -0.00000e+32 ", result: CompareOpResult.EQUAL),
            (x: null, y: "123", result: CompareOpResult.LESS),
            (x: null, y: " +123", result: CompareOpResult.LESS),
            (x: "-0.00192", y: null, result: CompareOpResult.LESS),
            (x: "\r\n -Infinity ", y: null, result: CompareOpResult.LESS),
            (x: null, y: "abc", result: CompareOpResult.NAN),
            (x: null, y: "null", result: CompareOpResult.NAN),
            (x: null, y: "NaN", result: CompareOpResult.NAN)

            #pragma warning restore 8123
        );

        [Theory]
        [MemberData(nameof(stringCompareOpsTest_data))]
        public void stringCompareOpsTest(string x, string y, CompareOpResult expectedResult) {
            bool isLess = false, isLessEq = false, isGreater = false, isGreaterEq = false;

            if (expectedResult != CompareOpResult.NAN) {
                isLess = expectedResult != CompareOpResult.EQUAL;
                isLessEq = true;
                isGreater = false;
                isGreaterEq = !isLess;
            }

            Assert.Equal(isLess, ASString.AS_lessThan(x, y));
            Assert.Equal(isLess, ASString.AS_greaterThan(y, x));
            Assert.Equal(isLessEq, ASString.AS_lessEq(x, y));
            Assert.Equal(isLessEq, ASString.AS_greaterEq(y, x));
            Assert.Equal(isGreater, ASString.AS_greaterThan(x, y));
            Assert.Equal(isGreater, ASString.AS_lessThan(y, x));
            Assert.Equal(isGreaterEq, ASString.AS_greaterEq(x, y));
            Assert.Equal(isGreaterEq, ASString.AS_lessEq(y, x));
        }

        public static IEnumerable<object[]> typeofTest_data() => TupleHelper.toArrays(
            (UNDEF, "undefined"),

            (NULL, "object"),
            (new ASObject(), "object"),
            (new OperatorsTest_CA(), "object"),
            (new ASArray(), "object"),

            (0, "number"),
            (0u, "number"),
            (1, "number"),
            (-1, "number"),
            (139304, "number"),
            (49583u, "number"),
            (-381933, "number"),
            (Int32.MaxValue, "number"),
            (UInt32.MaxValue, "number"),
            (Int32.MinValue, "number"),
            (0.0, "number"),
            (7.5, "number"),
            (-93882.0, "number"),
            (2.49388436e+39, "number"),
            (-7.8009e-128, "number"),
            (Double.MaxValue, "number"),
            (Double.PositiveInfinity, "number"),
            (Double.NegativeInfinity, "number"),
            (Double.NaN, "number"),

            (true, "boolean"),
            (false, "boolean"),

            ("", "string"),
            ("abcd", "string"),

            (xmlElement("foo"), "xml"),
            (xmlText("abcd"), "xml"),
            (xmlAttribute("foo", "1"), "xml"),
            (xmlList(), "xml"),

            (ASFunction.createEmpty(), "function"),
            (Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createMethodClosure(uniqueObjectClassA), "function"),
            (Class.fromType(typeof(OperatorsTest_CA)).getMethod("foo").createFunctionClosure(), "function")
        );

        [Theory]
        [MemberData(nameof(typeofTest_data))]
        public void typeofTest(ASAny x, string expected) {
            Assert.Equal(expected, ASAny.AS_typeof(x));

            if (!x.isUndefined)
                Assert.Equal(expected, ASObject.AS_typeof(x.value));
        }

        public static IEnumerable<object[]> checkFilterTest_data() => TupleHelper.toArrays(
            (UNDEF, false),
            (NULL, false),

            (0, false),
            (123u, false),
            (1.3029, false),
            (Double.NaN, false),
            ("", false),
            ("hello", false),
            (true, false),

            (new ASObject(), false),
            (new OperatorsTest_CA(), false),

            (xmlText(""), true),
            (xmlText("hello"), true),
            (xmlCdata("hello"), true),
            (xmlComment("..."), true),
            (xmlElement("a"), true),
            (xmlElement("a", attributes: new[] {("x", "1")}), true),
            (xmlElement("a", children: new[] {xmlText("hello")}), true),
            (xmlElement("a", children: new[] {xmlElement("b"), xmlElement("c")}), true),

            (xmlList(), true),
            (xmlList(xmlText("hello")), true),
            (xmlList(xmlElement("a", attributes: new[] {("x", "1")})), true),
            (xmlList(xmlElement("a", children: new[] {xmlElement("b"), xmlElement("c")})), true),
            (xmlList(xmlText("hello"), xmlText("world")), true),
            (xmlList(xmlElement("a", attributes: new[] {("x", "1")}), xmlElement("b", attributes: new[] {("x", "2")})), true)
        );

        [Theory]
        [MemberData(nameof(checkFilterTest_data))]
        public void checkFilterTest(ASAny obj, bool expectedResult) {
            if (expectedResult)
                ASObject.AS_checkFilter(obj.value);
            else
                AssertHelper.throwsErrorWithCode(ErrorCode.FILTER_NOT_SUPPORTED, () => ASObject.AS_checkFilter(obj.value));
        }

        public static IEnumerable<object[]> applyTypeTest_data() {
            var testcases = new List<(ASObject, ASAny[], ASObject)>();
            var genericVectorClass = Class.fromType(typeof(ASVector<>));

            void addVectorInstTestCase(Type elementType) {
                Class elementClass = Class.fromType(elementType);
                Class vectorClass = (elementClass == null) ? Class.fromType(typeof(ASVectorAny)) : elementClass.getVectorClass();
                testcases.Add((
                    genericVectorClass.classObject,
                    new ASAny[] {(elementClass == null) ? ASAny.@null : elementClass.classObject},
                    vectorClass.classObject
                ));
            }

            addVectorInstTestCase(typeof(ASAny));
            addVectorInstTestCase(typeof(ASObject));
            addVectorInstTestCase(typeof(int));
            addVectorInstTestCase(typeof(uint));
            addVectorInstTestCase(typeof(double));
            addVectorInstTestCase(typeof(string));
            addVectorInstTestCase(typeof(bool));
            addVectorInstTestCase(typeof(OperatorsTest_CA));
            addVectorInstTestCase(typeof(OperatorsTest_IA));

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(applyTypeTest_data))]
        public void applyTypeTest(ASObject type, ASAny[] typeArgs, ASObject expected) {
            Assert.Same(expected, ASObject.AS_applyType(type, typeArgs));
        }

        public static IEnumerable<object[]> applyTypeTest_throwsError_data() => TupleHelper.toArrays<ASObject, ASAny[], object>(
            #pragma warning disable 8123
            (
                type: null,
                typeArgs: Array.Empty<ASAny>(),
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: null,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: null,
                typeArgs: new ASAny[] {Class.fromType(typeof(ASVector<>)).classObject},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: null,
                typeArgs: new ASAny[] {1, 2, 3},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: 123,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: new ASObject(),
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: ASFunction.createEmpty(),
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),

            (
                type: Class.fromType(typeof(int)).classObject,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.NONGENERIC_TYPE_APPLICATION
            ),
            (
                type: Class.fromType(typeof(ASArray)).classObject,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.NONGENERIC_TYPE_APPLICATION
            ),
            (
                type: Class.fromType(typeof(int)).classObject,
                typeArgs: new ASAny[] {1, 2, default, 4},
                errorCode: ErrorCode.NONGENERIC_TYPE_APPLICATION
            ),
            (
                type: Class.fromType(typeof(ASVectorAny)).classObject,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.NONGENERIC_TYPE_APPLICATION
            ),

            (
                type: Class.fromType(typeof(ASVector<int>)).classObject,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.NONGENERIC_TYPE_APPLICATION
            ),

            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: Array.Empty<ASAny>(),
                errorCode: ErrorCode.TYPE_ARGUMENT_COUNT_INCORRECT
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject, Class.fromType(typeof(int)).classObject},
                errorCode: ErrorCode.TYPE_ARGUMENT_COUNT_INCORRECT
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {Class.fromType(typeof(int)).classObject, 1, 2, 3},
                errorCode: ErrorCode.TYPE_ARGUMENT_COUNT_INCORRECT
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {default, default},
                errorCode: ErrorCode.TYPE_ARGUMENT_COUNT_INCORRECT
            ),

            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {default},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {1234},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {"String"},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {new ASObject()},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            ),
            (
                type: Class.fromType(typeof(ASVector<>)).classObject,
                typeArgs: new ASAny[] {ASFunction.createEmpty()},
                errorCode: ErrorCode.MARIANA__APPLYTYPE_NON_CLASS
            )

            #pragma warning restore 8123
        );

        [Theory]
        [MemberData(nameof(applyTypeTest_throwsError_data))]
        public void applyTypeTest_throwsError(ASObject type, ASAny[] typeArgs, object errorCode) {
            AssertHelper.throwsErrorWithCode((ErrorCode)errorCode, () => ASObject.AS_applyType(type, typeArgs));
        }

        public static IEnumerable<object[]> instanceOfTest_data() {
            var testcases = new List<(ASObject, ASObject, bool)>();

            var objectClass = Class.fromType(typeof(ASObject)).classObject;
            var interfaceClass = Class.fromType(typeof(OperatorsTest_IA)).classObject;

            void addTestCaseWithClass(ASObject obj, Type type, bool result) =>
                testcases.Add((obj, Class.fromType(type).classObject, result));

            addTestCaseWithClass(null, typeof(ASObject), false);
            addTestCaseWithClass(null, typeof(ASArray), false);
            addTestCaseWithClass(null, typeof(int), false);
            addTestCaseWithClass(null, typeof(OperatorsTest_CA), false);
            addTestCaseWithClass(null, typeof(OperatorsTest_IA), false);

            addTestCaseWithClass(new ASObject(), typeof(ASObject), true);
            addTestCaseWithClass(new ASObject(), typeof(OperatorsTest_CA), false);
            addTestCaseWithClass(new ASObject(), typeof(OperatorsTest_IA), false);

            addTestCaseWithClass(new ASArray(), typeof(ASArray), true);
            addTestCaseWithClass(new ASArray(), typeof(ASObject), true);
            addTestCaseWithClass(new ASArray(), typeof(OperatorsTest_CA), false);

            addTestCaseWithClass(new OperatorsTest_CA(), typeof(ASObject), true);
            addTestCaseWithClass(new OperatorsTest_CA(), typeof(OperatorsTest_CA), true);
            addTestCaseWithClass(new OperatorsTest_CA(), typeof(OperatorsTest_CB), false);
            addTestCaseWithClass(new OperatorsTest_CB(), typeof(ASArray), false);
            addTestCaseWithClass(new OperatorsTest_CB(), typeof(ASObject), true);
            addTestCaseWithClass(new OperatorsTest_CB(), typeof(OperatorsTest_CB), true);
            addTestCaseWithClass(new OperatorsTest_CB(), typeof(OperatorsTest_CA), true);
            addTestCaseWithClass(new OperatorsTest_CB(), typeof(ASArray), false);

            // instanceof should return false for interfaces
            addTestCaseWithClass(new OperatorsTest_CA(), typeof(OperatorsTest_IA), false);
            addTestCaseWithClass(new OperatorsTest_CB(), typeof(OperatorsTest_IA), false);

            // int and uint use Number's prototype, so instanceof returns true if the class is Number.
            addTestCaseWithClass(1, typeof(int), false);
            addTestCaseWithClass(1, typeof(uint), false);
            addTestCaseWithClass(1, typeof(double), true);
            addTestCaseWithClass(1, typeof(ASObject), true);
            addTestCaseWithClass(1, typeof(OperatorsTest_IA), false);
            addTestCaseWithClass(1u, typeof(int), false);
            addTestCaseWithClass(1u, typeof(uint), false);
            addTestCaseWithClass(1u, typeof(double), true);
            addTestCaseWithClass(1u, typeof(ASObject), true);
            addTestCaseWithClass(1u, typeof(OperatorsTest_IA), false);
            addTestCaseWithClass(1.5, typeof(int), false);
            addTestCaseWithClass(1.5, typeof(uint), false);
            addTestCaseWithClass(1.5, typeof(double), true);
            addTestCaseWithClass(1.5, typeof(ASObject), true);
            addTestCaseWithClass(1.5, typeof(OperatorsTest_IA), false);

            addTestCaseWithClass(false, typeof(bool), true);
            addTestCaseWithClass(false, typeof(int), false);
            addTestCaseWithClass(false, typeof(double), false);
            addTestCaseWithClass(false, typeof(string), false);
            addTestCaseWithClass(false, typeof(ASObject), true);
            addTestCaseWithClass(false, typeof(OperatorsTest_IA), false);

            addTestCaseWithClass("hello", typeof(bool), false);
            addTestCaseWithClass("hello", typeof(int), false);
            addTestCaseWithClass("hello", typeof(double), false);
            addTestCaseWithClass("hello", typeof(string), true);
            addTestCaseWithClass("hello", typeof(ASObject), true);
            addTestCaseWithClass("hello", typeof(OperatorsTest_IA), false);

            addTestCaseWithClass(ASXML.createNode(XMLNodeType.TEXT), typeof(ASXML), true);
            addTestCaseWithClass(ASXML.createNode(XMLNodeType.TEXT), typeof(ASXMLList), false);
            addTestCaseWithClass(new ASXMLList(new[] {ASXML.createNode(XMLNodeType.TEXT)}), typeof(ASXML), false);
            addTestCaseWithClass(new ASXMLList(new[] {ASXML.createNode(XMLNodeType.TEXT)}), typeof(ASXMLList), true);

            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).prototypeObject, typeof(ASObject), true);
            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).prototypeObject, typeof(OperatorsTest_CA), true);
            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).prototypeObject, typeof(OperatorsTest_CB), false);

            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).classObject, typeof(ASObject), true);
            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).classObject, typeof(ASClass), true);
            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).classObject, typeof(OperatorsTest_CA), false);
            addTestCaseWithClass(Class.fromType(typeof(OperatorsTest_CB)).classObject, typeof(OperatorsTest_CB), false);

            var func1 = ASFunction.createEmpty();
            var func2 = ASFunction.createEmpty();
            var func3 = ASFunction.createEmpty();
            var func4 = ASFunction.createEmpty();

            func2.prototype = ASObject.AS_createWithPrototype(func1.prototype);
            func3.prototype = ASObject.AS_createWithPrototype(func2.prototype);
            func4.prototype = ASObject.AS_createWithPrototype(func1.prototype);

            var constructedObj1 = ASObject.AS_createWithPrototype(func1.prototype);
            var constructedObj2 = ASObject.AS_createWithPrototype(func2.prototype);
            var constructedObj3 = ASObject.AS_createWithPrototype(func3.prototype);
            var constructedObj4 = ASObject.AS_createWithPrototype(func4.prototype);

            testcases.Add((constructedObj1, func1, true));
            testcases.Add((constructedObj1, func2, false));
            testcases.Add((constructedObj1, func3, false));
            testcases.Add((constructedObj1, func4, false));
            testcases.Add((constructedObj1, objectClass, true));
            testcases.Add((constructedObj1, interfaceClass, false));

            testcases.Add((constructedObj2, func1, true));
            testcases.Add((constructedObj2, func2, true));
            testcases.Add((constructedObj2, func3, false));
            testcases.Add((constructedObj2, func4, false));
            testcases.Add((constructedObj2, objectClass, true));
            testcases.Add((constructedObj2, interfaceClass, false));

            testcases.Add((constructedObj3, func1, true));
            testcases.Add((constructedObj3, func2, true));
            testcases.Add((constructedObj3, func3, true));
            testcases.Add((constructedObj3, func4, false));
            testcases.Add((constructedObj3, objectClass, true));
            testcases.Add((constructedObj4, interfaceClass, false));

            testcases.Add((constructedObj4, func1, true));
            testcases.Add((constructedObj4, func2, false));
            testcases.Add((constructedObj4, func3, false));
            testcases.Add((constructedObj4, func4, true));
            testcases.Add((constructedObj4, objectClass, true));
            testcases.Add((constructedObj4, interfaceClass, false));

            testcases.Add((func1, func1, false));
            testcases.Add((func2, func2, false));
            testcases.Add((func2, func1, false));
            testcases.Add((func3, func3, false));
            testcases.Add((func4, func2, false));
            testcases.Add((func3, func3, false));
            testcases.Add((func1.prototype, func1, false));
            testcases.Add((func2.prototype, func1, true));
            testcases.Add((func3.prototype, func3, false));
            testcases.Add((func3.prototype, func2, true));
            testcases.Add((func3.prototype, func1, true));

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(instanceOfTest_data))]
        public void instanceOfTest(ASObject obj, ASObject type, bool expectedResult) {
            Assert.Equal(expectedResult, ASObject.AS_instanceof(obj, type));
        }

        public static IEnumerable<object[]> instanceOfTest_typeArgNotClassOrFunction_data = TupleHelper.toArrays<ASObject, ASObject>(
            (null, null),
            (new ASObject(), null),
            (null, 1),
            (new ASObject(), 1),
            ("hello", "String"),
            (new ASArray(), "Array"),
            (123, true),
            (new ASObject(), new ASObject()),
            (new OperatorsTest_CA(), new OperatorsTest_CA())
        );

        [Theory]
        [MemberData(nameof(instanceOfTest_typeArgNotClassOrFunction_data))]
        public void instanceOfTest_typeArgNotClassOrFunction(ASObject obj, ASObject type) {
            AssertHelper.throwsErrorWithCode(ErrorCode.INSTANCEOF_NOT_CLASS_OR_FUNCTION, () => ASObject.AS_instanceof(obj, type));
        }

    }

}
