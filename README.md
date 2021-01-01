# Mariana

An ActionScript 3 runtime and bytecode-to-IL compiler for .NET, implemented in C#.

## .NET platform support

The current target framework is **.NET Standard 2.1**, which is compatible with .NET Core 3.0, Mono 6.4 and [some other platforms](https://docs.microsoft.com/en-us/dotnet/standard/net-standard). .NET Framework is not supported.

The runtime dynamically compiles ActionScript 3 bytecode into IL, so it will not work on AOT-only platforms (such as iOS).

## Components

* [Mariana.AVM2](/Mariana.AVM2): ActionScript 3 runtime and bytecode compiler.
* [Mariana.CodeGen](/Mariana.CodeGen): IL code generation library used by the AS3 compiler.
* [Mariana.Common](/Mariana.Common): Reusable components.

## Building

Build with <code>dotnet build -c *config*</code> where *config* is either `Debug` (for debug build) or `Release` (for release build).
