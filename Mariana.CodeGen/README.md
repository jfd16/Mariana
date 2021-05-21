# Mariana.CodeGen

A dynamic IL code generation library used by the ActionScript compiler.

## Usage example

```csharp
using System;
using System.Reflection;
using System.Reflection.Metadata;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;

public class Example {

    public static void Main() {
        // Create a new assembly.
        var asmBuilder = new AssemblyBuilder("MyDynamicAssembly", new Version(1, 0, 0, 0));

        // Create a class named Point
        var typeBuilder = asmBuilder.defineType("Point", TypeAttributes.Public | TypeAttributes.Sealed);

        var doubleType = TypeSignature.forPrimitiveType(PrimitiveTypeCode.Double);

        // Create two fields "x" and "y" of type double
        var xField = typeBuilder.defineField("x", doubleType, FieldAttributes.Public);
        var yField = typeBuilder.defineField("y", doubleType, FieldAttributes.Public);

        // Create a constructor with two arguments
        var ctorBuilder = typeBuilder.defineConstructor(
            MethodAttributes.Public,
            new[] {doubleType, doubleType}
        );

        // Define the parameter names (this is optional)
        ctorBuilder.defineParameter(0, "x");
        ctorBuilder.defineParameter(1, "y");

        // The metadataContext of an AssemblyBuilder is what is used to reference external types
        // and members in the generated code.
        var context = asmBuilder.metadataContext;

        // Create a new ILBuilder. This will be used for emitting method bodies.
        var ilBuilder = new ILBuilder(context.ilTokenProvider);

        // Emit the body of the constructor.
        ilBuilder.emit(ILOp.ldarg_0);

        ilBuilder.emit(ILOp.call, context.getMemberHandle(typeof(object).GetConstructor(Array.Empty<Type>())));
        // The context.getMemberHandle call is optional here since we passed context.ilTokenProvider
        // to the ILBuilder. This can also be emitted as:
        // ilBuilder.emit(ILOp.call, typeof(object).GetConstructor(Array.Empty<Type>()));

        ilBuilder.emit(ILOp.ldarg_0);
        ilBuilder.emit(ILOp.ldarg_1);
        ilBuilder.emit(ILOp.stfld, xField.handle);
        ilBuilder.emit(ILOp.ldarg_0);
        ilBuilder.emit(ILOp.ldarg_2);
        ilBuilder.emit(ILOp.stfld, yField.handle);
        ilBuilder.emit(ILOp.ret);

        // Save the emitted IL to the constructor. Calling createMethodBody will also
        // reset the ILBuilder so that it can be used to emit code for another method.
        ctorBuilder.setMethodBody(ilBuilder.createMethodBody());

        // Create a static method named "distance" that calculates the distance
        // between two points...
        var distanceMethodBuilder = typeBuilder.defineMethod(
            "distance",
            attributes: MethodAttributes.Public | MethodAttributes.Static,
            returnType: doubleType,
            paramTypes: new[] {
                TypeSignature.forClassType(typeBuilder.handle),
                TypeSignature.forClassType(typeBuilder.handle)
            }
        );

        ilBuilder.emit(ILOp.ldarg_1);
        ilBuilder.emit(ILOp.ldfld, xField.handle);
        ilBuilder.emit(ILOp.ldarg_0);
        ilBuilder.emit(ILOp.ldfld, xField.handle);
        ilBuilder.emit(ILOp.sub);
        ilBuilder.emit(ILOp.dup);
        ilBuilder.emit(ILOp.mul);

        ilBuilder.emit(ILOp.ldarg_1);
        ilBuilder.emit(ILOp.ldfld, yField.handle);
        ilBuilder.emit(ILOp.ldarg_0);
        ilBuilder.emit(ILOp.ldfld, yField.handle);
        ilBuilder.emit(ILOp.sub);
        ilBuilder.emit(ILOp.dup);
        ilBuilder.emit(ILOp.mul);

        ilBuilder.emit(ILOp.add);
        ilBuilder.emit(ILOp.call, typeof(Math).GetMethod(nameof(Math.Sqrt)));
        ilBuilder.emit(ILOp.ret);

        distanceMethodBuilder.setMethodBody(ilBuilder.createMethodBody());

        // Create the dynamic assembly...
        var emitResult = asmBuilder.emit();
        // Load it...
        var loadedAssembly = Assembly.Load(emitResult.peImageBytes);
        // Get the emitted Point type and distance method
        Type pointType = loadedAssembly.ManifestModule.ResolveType(
            emitResult.tokenMapping.getMappedToken(typeBuilder.handle));
        MethodBase distanceMethod = loadedAssembly.ManifestModule.ResolveMethod(
            emitResult.tokenMapping.getMappedToken(distanceMethodBuilder.handle));
    }

}
```

## Validating generated assemblies

This library is intended for high performance use and while it may detect some simple
errors, it does not guarantee full validity of generated assemblies. It is recommended to use
a tool such as [ILVerify](https://www.nuget.org/packages/dotnet-ilverify/) to identify and debug
any possible code generation issues.

## Limitations

The following features are not supported by the CodeGen library:
* Multi-module assemblies
* Debug information
* Custom attributes
* Defining nested types (referencing external nested types is supported)
* Fields with RVAs (static data)
* Explicit struct layout
* Function pointer types
* The `calli` IL instruction
* Defining P/Invoke methods
* Defining custom modifiers in signatures (referencing external methods with custom
modifiers in their signatures is supported)
