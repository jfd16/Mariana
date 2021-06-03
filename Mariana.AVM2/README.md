# Mariana.AVM2

The core ActionScript 3 runtime and bytecode compiler.

## Using ActionScript 3 objects from .NET code

All ActionScript 3 objects are instances of the `ASObject` class (which corresponds to the `Object`
class in AS3).

Creating a new instance of `Object`:
```csharp
var obj = new ASObject();
```
Getting and setting object properties:
```csharp
// ASAny is the "any" type (*) in ActionScript. It is like ASObject but can also
// hold the undefined value.
ASAny propX = obj.AS_getProperty("x");
ASAny propY = obj.AS_getProperty("y");
obj.AS_setProperty("x", 20);
obj.AS_setProperty("y", "Hello");
```
Calling methods:
```csharp
var args = new ASAny[] {1, 2, 3}
ASAny returnValue = obj.AS_callProperty("foo", args);
```
Using objects with traits:
```csharp
// AS3 standard library classes are prefixed with "AS" in .NET, so e.g. Date and Array
// would be called ASDate and ASArray.
var date = new ASDate();
// Declared properties and methods can be used directly
Console.WriteLine(date.getFullYear());
Console.WriteLine(date.toDateString());
date.month = 0;
// and can also be invoked dynamically
Console.WriteLine(date.AS_getProperty("fullYearUTC"));
date.AS_callProperty(new QName(Namespace.AS3, "setHours"), new ASAny[] {10});
```
Primitive types can be converted to and from objects:
```csharp
ASObject obj;
obj = ASObject.AS_fromInt(10);
obj = ASObject.AS_fromUint(UInt32.MaxValue);
obj = ASObject.AS_fromNumber(1.5);
obj = ASObject.AS_fromString("hello");
obj = ASObject.AS_fromBoolean(true);
// This can also be done using implicit conversions (in supported languages):
obj = 10;
obj = UInt32.MaxValue;
obj = 1.5;
obj = "hello";
obj = true;
// Conversion to primitives:
int i = ASObject.AS_toInt(obj);
uint u = ASObject.AS_toUint(obj);
double d = ASObject.AS_toNumber(obj);
bool b = ASObject.AS_toBoolean(obj);
string s = ASObject.AS_coerceString(obj);   // null converts to null
s = ASObject.AS_convertString(obj);         // null converts to "null"
// These conversions are also available as explicit conversions in
// supported languages:
i = (int)obj;
u = (uint)obj;
d = (double)obj;
b = (bool)obj;
s = (string)obj;    // Explicit conversion uses AS_coerceString.
// These conversions are also available for the ASAny type.
```
Static methods are available on `ASObject` and `ASAny` for some ActionScript 3 operators:
```csharp
ASObject.AS_typeof(obj);              // typeof
ASObject.AS_instanceof(obj, klass);   // instanceof
ASObject.AS_add(x, y);                // addition
ASObject.AS_weakEq(x, y);             // weak equality (==)
ASObject.AS_strictEq(x, y);           // strict equality (===)
ASObject.AS_cast<ASDate>(x);          // type cast to the given class
ASObject.AS_coerceType(obj, klass);   // late-bound type conversion
```

## Compiling and running ActionScript 3 code

The runtime accepts compiled AS3 bytecode as an ABC file. These files are generated directly
by an ActionScript 3 compiler, or (more commonly) embedded in other file formats such as SWF.
The runtime API currently does not provide any means of extracting embedded ABC files in SWFs, but this
can be done using the [RABCDAsm](https://github.com/CyberShadow/RABCDAsm) command-line tool.

### An example

```AS3
// Assume that a file named abc_file.abc exists which contains this class:
package com.example {
    public class Main {
        public function Main() {}

        public function foo(x: Number, y: Number): Number {
            return x + y;
        }
    }
}
```

```csharp
using System;
using Mariana.AVM2.Core;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Compiler;

class Example {

    public static void Main() {
        // Create a new application domain. Any classes and other definitions from
        // the compiled code will go here.
        var domain = new ApplicationDomain();

        // Create a ScriptLoader (a compiler instance). An instance of ScriptCompileOptions
        // can be passed to createScriptLoader() to override the default configuration.
        var scriptLoader = domain.createScriptLoader();

        // Load and compile the ABC bytecode
        var abcFile = ABCFile.readFromFile("./abc_file.abc");
        scriptLoader.compile(abcFile);

        // Finish the compilation
        scriptLoader.runCompiledScripts();

        // Create an instance of the Main class
        ASAny instance = domain.getGlobalClass(new QName("com.example", "Main")).construct(ReadOnlySpan<ASAny>.Empty);

        // Invoke the "foo" method and print the result...
        double result = (double)instance.AS_callProperty("foo", new ASAny[] {1, 2});
        Console.WriteLine(result);
    }

}
```

### Debugging

The following configuration options are available (in `ScriptCompileOptions`) to aid in debugging:
* `enableTracing`: Set this to true to output a trace of the compilation state of each method
(this includes the ABC bytecode instructions, the types of the stack values pushed and/or popped by
each instruction, local variable states and other information). This only works in a debug build
(with the `DEBUG` conditional compilation symbol set) and is ignored in release builds.
* Assemblies generated by the compiler can be intercepted by a custom assembly loader
(`assemblyLoader`), which can, for example, save them to .dll files so that they can be
inspected using the [ILVerify](https://www.nuget.org/packages/dotnet-ilverify/) tool
and/or a decompiler to check for any code generation problems.

## Authoring classes for the AVM2

The runtime provides attributes (in the `Mariana.AVM2.Native` namespace) that can be
used for writing classes in a .NET language that can be consumed by ActionScript 3 code.

### A sample class

```csharp
using System;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;

[AVM2ExportClass]
public class MyClass : ASObject {
    [AVM2ExportTrait]   // Exports an instance field
    public int x;

    [AVM2ExportTrait(name = "Y")]   // Exports with a different name
    public readonly int y;

    [AVM2ExportTrait]   // Exports a property
    public ASDate date => new ASDate();

    [AVM2ExportTrait(name = "zz", nsUri = "abcd")]  // Exports with a name in a namespace
    public int z {
        get => x;
        set => x = value;
    }

    [AVM2ExportTrait]   // Static field
    public static int q;

    [AVM2ExportTrait]   // Constants of supported types can also be exported
    public const double PI = 3.1416;

    [AVM2ExportTrait]   // Exports a constructor
    public MyClass(int x) => this.x = x;

    [AVM2ExportTrait]   // Exports a method
    public void myMethod(int x, double y, string z, ASArray arr) {}

    [AVM2ExportTrait]   // Exports a static method
    public static void myStaticMethod() {}

    [AVM2ExportTrait]   // Exports a virtual method (non-final in AS3)
    public virtual void myVirtMethod(int x, double y, string z, ASArray arr) {}

    // Method that takes a ...rest parameter
    [AVM2ExportTrait]
    public void methodWithRest(int x, int y, RestParam rest) {}

    // Method that takes optional parameters
    [AVM2ExportTrait]
    public int methodWithOptional1(int x, [ParamDefaultValue(0)] int y) => 1;

    // Optional parameters can be specified directly in supported languages
    [AVM2ExportTrait]
    public int methodWithOptional2(int x, int y = 0) => 1;

    // OptionalParam<T> can also be used if no default value can be provided. Use
    // the isSpecified property to check if the argument was passed or not.
    [AVM2ExportTrait]
    public int methodWithOptional3(int x, OptionalParam<int> y) => 1;

    // Classes can have unexported methods and other members. These are available to .NET code but not to AS3.
    public int nonExportedMethod() => 1000;
    // Unexported members have no restrictions on visibility or signature.
    private string _privateMethod(DateTime dt) => dt.ToString();
}
```

### Exporting into the global scope

Global functions, variables and properties can be exported by defining them in a module. A module
is written in a similar way to a class, except that the `AVM2ExportModule` attribute is used instead
of `AVM2ExportClass`, and only static members can be exported. Module types *can* be abstract (unlike
classes), which allows for instance the use of C# `static` classes (which are internally `abstract` and
`sealed`).

### Importing classes

For classes and module members to be visible to compiled ActionScript 3 code, they must be
imported into an application domain. They can be imported into the system domain, where they will
be visible to all compiled code, or into a non-system domain so that only code compiled in that
domain can use them. Imported can be done in one of the following ways:

* Using the `ApplicationDomain.loadNativeClass` and `ApplicationDomain.loadNativeModule` methods,
to import classes and modules individually.
* Using the `ApplicationDomain.loadNativeClassesFromAssembly`, which imports all the classes and
modules from an assembly that have the appropriate attributes.

Importing a class or module will automatically import any dependent classes (such as those used
in member signatures) into the same application domain if they have the `AVM2ExportClass`
attribute applied and have not been imported yet.

### Restrictions on exported classes and members

A class exported to the AVM2 is subject to these restrictions:
* The class must be a subclass of `ASObject`, or an interface.
* The class must have public visibility and must not be a nested class.
* The class must not be abstract (with the exception of interfaces).
* The class must not be generic.*
* The base class of the class must also be exported, if the class is not an interface.
* If the class is an interface, all of its declared members and interfaces that it
extends must also be exported.

<small>* An exception to this rule exists internally in the runtime for the `Vector` class.</small>

A class member exported is subject to these restrictions: (These also apply to
global members exported from modules)
* The member must be a field, property, method, constructor or constant.
* The member must have public visibility.
* Methods cannot be generic.
* At most one constructor can be exported per class.
* At most one method with a given name can be exported per class.
* The type used as the type of a field or property, the return type of a method or the type of
a method or constructor parameter must be one of:
  * The following primitive types: `Int32`, `UInt32`, `Double`, `String`, `Boolean`.
(these correspond to the AS3 types `int`, `uint`, `Number`, `String` and `Boolean`, respectively).
  * The `ASAny` type, which corresponds to `*` in AS3.
  * The `ASObject` type, or any subclass of it that is exported.
  * The `void` type as a method return type.
  * In addition, method and constructor parameters can use the `OptionalParam<T>` type (where
`T` is one of the above types), and the last parameter can have the type `RestParam`.

## Exception handling

Any exception thrown by AS3 code can be caught on the .NET side by catching `AVM2Exception`.
The `thrownValue` property of the exception instance will contain the object that was thrown
from AS3.

To create an exception from .NET code that is catchable by AS3 code, create an instance of
`AVM2Exception` by passing in the object that will be visible to the AS3 side into the
constructor. (This is usually an instance of `Error` or one of its subclasses, but any
value can be thrown).

A `NullReferenceException` thrown by defererencing a null reference in compiled AS3 code
or in .NET code called from it (such as a method on an exported class) will be intercepted
by the runtime when an AS3 method with a try-catch block is reached and converted to
the appropriate error (`ReferenceError`) which can be caught.

## ActionScript 3 built-in class and function support

The runtime provides implementations of the following builtin classes and functions:
* Core classes: `Object`, `int`, `uint`, `Number`, `String`, `Boolean`, `Array`, `Vector`,
`Date`, `Class`, `Function`, `Math`, `JSON`, `RegExp`
* E4X: `XML`, `XMLList`, `Namespace`, `QName`
* Error classes: `Error`, `ArgumentError`, `DefinitionError`, `RangeError`, `ReferenceError`,
`SecurityError`, `SyntaxError`, `TypeError`, `URIError`, `VerifyError`
* Global functions: `decodeURI`, `decodeURIComponent`, `encodeURI`, `encodeURIComponent`,
`escape`, `isFinite`, `isNaN`, `isXMLName`, `parseFloat`, `parseInt`, `trace`, `unescape`

The builtin classes must be prefixed with `AS` when used from .NET code, so for
example `Array` is called `ASArray`. The global functions are available as static methods on
the `ASGlobal` class.

## Global memory instructions

For running AS3 programs that contain global memory manipulation instructions (such as those created with Alchemy/CrossBridge or from AS3 source code with [domain memory intrinsics](https://www.jacksondunstan.com/articles/2314)), a global memory buffer must be provided on which these instructions will operate. This can be set for the application domain in which the program is loaded by passing the buffer (as a byte array) to the `setGlobalMemory` method on the associated `ApplicationDomain`.
