# Mariana

An ActionScript 3 runtime and bytecode-to-IL compiler for .NET, implemented in C#.

## .NET platform support

This project targets **.NET Standard 2.1**, which is compatible with .NET Core 3.0, Mono 6.4 and [some other platforms](https://docs.microsoft.com/en-us/dotnet/standard/net-standard).

.NET Framework is not supported.

The runtime dynamically compiles ActionScript 3 bytecode into IL, so it will not work on AOT-only platforms (such as iOS).

## Usage

Sample usage code is available [here](/Mariana.AVM2/README.md).

## Features

* Full implementation of the [ActionScript Virtual Machine 2 specification](https://www.adobe.com/content/dam/acom/en/devnet/pdf/avm2overview.pdf).
* Run-time performance of AS3 code is comparable to equivalent C# code when everything is statically typed (no untyped variables or dynamic property access).
* Attribute based API for exposing .NET classes to AS3.
* Supports all [ActionScript 3 top-level classes and functions](https://help.adobe.com/en_US/FlashPlatform/reference/actionscript/3/package-detail.html).
* Full support for E4X (ECMAScript for XML).
* Supports AVM2 global memory instructions (used by code compiled with Alchemy/CrossBridge).

## API documentation

API documentation can be generated using the [`generateApiDocs.py`](/generateApiDocs.py) Python script. This requires [docfx](https://dotnet.github.io/docfx/) to be installed.

## Component index

* [Mariana.AVM2](/Mariana.AVM2): ActionScript 3 runtime and bytecode compiler.
* [Mariana.CodeGen](/Mariana.CodeGen): IL code generation library used by the AS3 compiler.
* [Mariana.Common](/Mariana.Common): Reusable components.

## Building

Build with <code>dotnet build -c *config*</code> where *config* is either `Debug` (for debug build) or `Release` (for release build).

## Running tests

Run `dotnet test` when in a test project directory.

To run specific test classes or methods, pass a [`--filter` argument](https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=xunit).

> Caution: Some tests are long running
