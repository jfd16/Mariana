using System;
using Mariana.AVM2.Core;

using static Mariana.Common.ReflectUtil;

using FI = System.Reflection.FieldInfo;
using MI = System.Reflection.MethodInfo;
using PI = System.Reflection.PropertyInfo;
using CI = System.Reflection.ConstructorInfo;

using ArgsSpan = System.ReadOnlySpan<Mariana.AVM2.Core.ASAny>;

namespace Mariana.AVM2.Compiler {

    internal static class KnownMembers {

        private static readonly Type s_QNameRef = typeof(QName).MakeByRefType();
        private static readonly Type s_NamespaceSetRef = typeof(NamespaceSet).MakeByRefType();

        #region Conversions

        public static readonly MI boolToAny =
            getMemberFromExpr<Func<bool, ASAny>, MI>(x => x);

        public static readonly MI intToAny =
            getMemberFromExpr<Func<int, ASAny>, MI>(x => x);

        public static readonly MI uintToAny =
            getMemberFromExpr<Func<uint, ASAny>, MI>(x => x);

        public static readonly MI numberToAny =
            getMemberFromExpr<Func<double, ASAny>, MI>(x => x);

        public static readonly MI stringToAny =
            getMemberFromExpr<Func<string, ASAny>, MI>(x => x);

        public static readonly MI anyFromObject =
            getMemberFromExpr<Func<ASObject, ASAny>, MI>(x => x);

        public static readonly MI numberToInt =
            getMemberFromExpr<Func<double, int>, MI>(x => ASNumber.AS_toInt(x));

        public static readonly MI stringToInt =
            getMemberFromExpr<Func<string, int>, MI>(x => ASString.AS_toInt(x));

        public static readonly MI objectToInt =
            getMemberFromExpr<Func<ASObject, int>, MI>(x => ASObject.AS_toInt(x));

        public static readonly MI anyToInt =
            getMemberFromExpr<Func<ASAny, int>, MI>(x => ASAny.AS_toInt(x));

        public static readonly MI numberToUint =
            getMemberFromExpr<Func<double, uint>, MI>(x => ASNumber.AS_toUint(x));

        public static readonly MI stringToUint =
            getMemberFromExpr<Func<string, uint>, MI>(x => ASString.AS_toUint(x));

        public static readonly MI objectToUint =
            getMemberFromExpr<Func<ASObject, uint>, MI>(x => ASObject.AS_toUint(x));

        public static readonly MI anyToUint =
            getMemberFromExpr<Func<ASAny, uint>, MI>(x => ASAny.AS_toUint(x));

        public static readonly MI stringToNumber =
            getMemberFromExpr<Func<string, double>, MI>(x => ASString.AS_toNumber(x));

        public static readonly MI objectToNumber =
            getMemberFromExpr<Func<ASObject, double>, MI>(x => ASObject.AS_toNumber(x));

        public static readonly MI anyToNumber =
            getMemberFromExpr<Func<ASAny, double>, MI>(x => ASAny.AS_toNumber(x));

        public static readonly MI boolToString =
            getMemberFromExpr<Func<bool, string>, MI>(x => ASBoolean.AS_convertString(x));

        public static readonly MI intToString =
            getMemberFromExpr<Func<int, string>, MI>(x => ASint.AS_convertString(x));

        public static readonly MI uintToString =
            getMemberFromExpr<Func<uint, string>, MI>(x => ASuint.AS_convertString(x));

        public static readonly MI numberToString =
            getMemberFromExpr<Func<double, string>, MI>(x => ASNumber.AS_convertString(x));

        public static readonly MI stringToStringConvert =
            getMemberFromExpr<Func<string, string>, MI>(x => ASString.AS_convertString(x));

        public static readonly MI objectToStringCoerce =
            getMemberFromExpr<Func<ASObject, string>, MI>(x => ASObject.AS_coerceString(x));

        public static readonly MI objectToStringConvert =
            getMemberFromExpr<Func<ASObject, string>, MI>(x => ASObject.AS_convertString(x));

        public static readonly MI anyToStringCoerce =
            getMemberFromExpr<Func<ASAny, string>, MI>(x => ASAny.AS_coerceString(x));

        public static readonly MI anyToStringConvert =
            getMemberFromExpr<Func<ASAny, string>, MI>(x => ASAny.AS_convertString(x));

        public static readonly MI numberToBool =
            getMemberFromExpr<Func<double, bool>, MI>(x => ASNumber.AS_toBoolean(x));

        public static readonly MI stringToBool =
            getMemberFromExpr<Func<string, bool>, MI>(x => ASString.AS_toBoolean(x));

        public static readonly MI objectToBool =
            getMemberFromExpr<Func<ASObject, bool>, MI>(x => ASObject.AS_toBoolean(x));

        public static readonly MI anyToBool =
            getMemberFromExpr<Func<ASAny, bool>, MI>(x => ASAny.AS_toBoolean(x));

        public static readonly MI boolToObject =
            getMemberFromExpr<Func<bool, ASObject>, MI>(x => x);

        public static readonly MI intToObject =
            getMemberFromExpr<Func<int, ASObject>, MI>(x => x);

        public static readonly MI uintToObject =
            getMemberFromExpr<Func<uint, ASObject>, MI>(x => x);

        public static readonly MI numberToObject =
            getMemberFromExpr<Func<double, ASObject>, MI>(x => x);

        public static readonly MI stringToObject =
            getMemberFromExpr<Func<string, ASObject>, MI>(x => x);

        public static readonly MI anyGetObject =
            getMemberFromExpr<Func<ASAny, ASObject>, PI>(x => x.value).GetGetMethod();

        public static readonly MI anyGetIsDefined =
            getMemberFromExpr<Func<ASAny, bool>, PI>(x => x.isDefined).GetGetMethod();

        public static readonly MI objectCast =
            getMemberFromExpr<Func<ASObject, ASObject>, MI>(x => ASObject.AS_cast<ASObject>(x), stripTypeArgs: true);

        public static readonly MI anyCast =
            getMemberFromExpr<Func<ASAny, ASObject>, MI>(x => ASAny.AS_cast<ASObject>(x), stripTypeArgs: true);

        #endregion

        #region Operators

        public static readonly MI objectAdd =
            getMemberFromExpr<Func<ASObject, ASObject, ASObject>, MI>((x, y) => ASObject.AS_add(x, y));

        public static readonly MI anyAdd =
            getMemberFromExpr<Func<ASAny, ASAny, ASObject>, MI>((x, y) => ASAny.AS_add(x, y));

        public static readonly MI stringAdd =
            getMemberFromExpr<Func<string, string, string>, MI>((x, y) => ASString.AS_add(x, y));

        public static readonly MI objWeakEq =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_weakEq(x, y));

        public static readonly MI objStrictEq =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_strictEq(x, y));

        public static readonly MI objLt =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_lessThan(x, y));

        public static readonly MI objGt =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_greaterThan(x, y));

        public static readonly MI objLeq =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_lessEq(x, y));

        public static readonly MI objGeq =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_greaterEq(x, y));

        public static readonly MI anyWeakEq =
            getMemberFromExpr<Func<ASAny, ASAny, bool>, MI>((x, y) => ASAny.AS_weakEq(x, y));

        public static readonly MI anyStrictEq =
            getMemberFromExpr<Func<ASAny, ASAny, bool>, MI>((x, y) => ASAny.AS_strictEq(x, y));

        public static readonly MI anyLt =
            getMemberFromExpr<Func<ASAny, ASAny, bool>, MI>((x, y) => ASAny.AS_lessThan(x, y));

        public static readonly MI anyGt =
            getMemberFromExpr<Func<ASAny, ASAny, bool>, MI>((x, y) => ASAny.AS_greaterThan(x, y));

        public static readonly MI anyLeq =
            getMemberFromExpr<Func<ASAny, ASAny, bool>, MI>((x, y) => ASAny.AS_lessEq(x, y));

        public static readonly MI anyGeq =
            getMemberFromExpr<Func<ASAny, ASAny, bool>, MI>((x, y) => ASAny.AS_greaterEq(x, y));

        public static readonly MI strEquals =
            getMemberFromExpr<Func<string, string, bool>, MI>((x, y) => x == y);

        public static readonly MI strLt =
            getMemberFromExpr<Func<string, string, bool>, MI>((x, y) => ASString.AS_lessThan(x, y));

        public static readonly MI strLeq =
            getMemberFromExpr<Func<string, string, bool>, MI>((x, y) => ASString.AS_lessEq(x, y));

        public static readonly MI strGt =
            getMemberFromExpr<Func<string, string, bool>, MI>((x, y) => ASString.AS_greaterThan(x, y));

        public static readonly MI strGeq =
            getMemberFromExpr<Func<string, string, bool>, MI>((x, y) => ASString.AS_greaterEq(x, y));

        public static readonly MI xmlNamespaceEquals =
            getMemberFromExpr<Func<ASNamespace, ASNamespace, bool>, MI>((x, y) => ASNamespace.AS_equals(x, y));

        public static readonly MI xmlQnameEquals =
            getMemberFromExpr<Func<ASQName, ASQName, bool>, MI>((x, y) => ASQName.AS_equals(x, y));

        public static readonly MI objIsType =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_isType(x, y));

        public static readonly MI objAsType =
            getMemberFromExpr<Func<ASObject, ASObject, ASObject>, MI>((x, y) => ASObject.AS_asType(x, y));

        public static readonly MI objIsInt =
            getMemberFromExpr<Func<ASObject, bool>, MI>(x => ASObject.AS_isInt(x));

        public static readonly MI objIsUint =
            getMemberFromExpr<Func<ASObject, bool>, MI>(x => ASObject.AS_isUint(x));

        public static readonly MI objIsNumeric =
            getMemberFromExpr<Func<ASObject, bool>, MI>(x => ASObject.AS_isNumeric(x));

        public static readonly MI objInstanceof =
            getMemberFromExpr<Func<ASObject, ASObject, bool>, MI>((x, y) => ASObject.AS_instanceof(x, y));

        public static readonly MI objTypeof =
            getMemberFromExpr<Func<ASObject, string>, MI>(x => ASObject.AS_typeof(x));

        public static readonly MI anyTypeof =
            getMemberFromExpr<Func<ASAny, string>, MI>(x => ASAny.AS_typeof(x));

        #endregion

        #region PropertyBinding

        public static readonly MI objHasPropertyQName =
            getMemberFromExpr<Func<ASObject, QName, BindOptions, bool>, MI>((x, n, b) => x.AS_hasProperty(n, b));

        public static readonly MI objHasPropertyNsSet =
            getMemberFromExpr<Func<ASObject, string, NamespaceSet, BindOptions, bool>, MI>((x, n, s, b) => x.AS_hasProperty(n, s, b));

        public static readonly MI objHasPropertyKey =
            getMemberFromExpr<Func<ASObject, ASAny, BindOptions, bool>, MI>((x, k, b) => x.AS_hasPropertyObj(k, b));

        public static readonly MI objHasPropertyKeyNsSet =
            getMemberFromExpr<Func<ASObject, ASAny, NamespaceSet, BindOptions, bool>, MI>((x, k, s, b) => x.AS_hasPropertyObj(k, s, b));

        public static readonly MI anyHasPropertyQName =
            getMemberFromExpr<Func<ASAny, QName, BindOptions, bool>, MI>((x, n, b) => x.AS_hasProperty(n, b));

        public static readonly MI anyHasPropertyNsSet =
            getMemberFromExpr<Func<ASAny, string, NamespaceSet, BindOptions, bool>, MI>((x, n, s, b) => x.AS_hasProperty(n, s, b));

        public static readonly MI anyHasPropertyKey =
            getMemberFromExpr<Func<ASAny, ASAny, BindOptions, bool>, MI>((x, k, b) => x.AS_hasPropertyObj(k, b));

        public static readonly MI anyHasPropertyKeyNsSet =
            getMemberFromExpr<Func<ASAny, ASAny, NamespaceSet, BindOptions, bool>, MI>((x, k, s, b) => x.AS_hasPropertyObj(k, s, b));

        public static readonly MI objGetPropertyQName =
            getMemberFromExpr<Func<ASObject, QName, BindOptions, ASAny>, MI>((x, n, b) => x.AS_getProperty(n, b));

        public static readonly MI objGetPropertyNsSet =
            getMemberFromExpr<Func<ASObject, string, NamespaceSet, BindOptions, ASAny>, MI>((x, n, s, b) => x.AS_getProperty(n, s, b));

        public static readonly MI objGetPropertyKey =
            getMemberFromExpr<Func<ASObject, ASAny, BindOptions, ASAny>, MI>((x, k, b) => x.AS_getPropertyObj(k, b));

        public static readonly MI objGetPropertyKeyNsSet =
            getMemberFromExpr<Func<ASObject, ASAny, NamespaceSet, BindOptions, ASAny>, MI>((x, k, s, b) => x.AS_getPropertyObj(k, s, b));

        public static readonly MI anyGetPropertyQName =
            getMemberFromExpr<Func<ASAny, QName, BindOptions, ASAny>, MI>((x, n, b) => x.AS_getProperty(n, b));

        public static readonly MI anyGetPropertyNsSet =
            getMemberFromExpr<Func<ASAny, string, NamespaceSet, BindOptions, ASAny>, MI>((x, n, s, b) => x.AS_getProperty(n, s, b));

        public static readonly MI anyGetPropertyKey =
            getMemberFromExpr<Func<ASAny, ASAny, BindOptions, ASAny>, MI>((x, k, b) => x.AS_getPropertyObj(k, b));

        public static readonly MI anyGetPropertyKeyNsSet =
            getMemberFromExpr<Func<ASAny, ASAny, NamespaceSet, BindOptions, ASAny>, MI>((x, k, s, b) => x.AS_getPropertyObj(k, s, b));

        public static readonly MI objSetPropertyQName =
            getMemberFromExpr<Action<ASObject, QName, ASAny, BindOptions>, MI>((x, n, v, b) => x.AS_setProperty(n, v, b));

        public static readonly MI objSetPropertyNsSet =
            getMemberFromExpr<Action<ASObject, string, NamespaceSet, ASAny, BindOptions>, MI>((x, n, s, v, b) => x.AS_setProperty(n, s, v, b));

        public static readonly MI objSetPropertyKey =
            getMemberFromExpr<Action<ASObject, ASAny, ASAny, BindOptions>, MI>((x, k, v, b) => x.AS_setPropertyObj(k, v, b));

        public static readonly MI objSetPropertyKeyNsSet =
            getMemberFromExpr<Action<ASObject, ASAny, NamespaceSet, ASAny, BindOptions>, MI>((x, k, s, v, b) => x.AS_setPropertyObj(k, s, v, b));

        public static readonly MI anySetPropertyQName =
            getMemberFromExpr<Action<ASAny, QName, ASAny, BindOptions>, MI>((x, n, v, b) => x.AS_setProperty(n, v, b));

        public static readonly MI anySetPropertyNsSet =
            getMemberFromExpr<Action<ASAny, string, NamespaceSet, ASAny, BindOptions>, MI>((x, n, s, v, b) => x.AS_setProperty(n, s, v, b));

        public static readonly MI anySetPropertyKey =
            getMemberFromExpr<Action<ASAny, ASAny, ASAny, BindOptions>, MI>((x, k, v, b) => x.AS_setPropertyObj(k, v, b));

        public static readonly MI anySetPropertyKeyNsSet =
            getMemberFromExpr<Action<ASAny, ASAny, NamespaceSet, ASAny, BindOptions>, MI>((x, k, s, v, b) => x.AS_setPropertyObj(k, s, v, b));

        public static readonly MI objDelPropertyQName =
            getMemberFromExpr<Func<ASObject, QName, BindOptions, bool>, MI>((x, n, b) => x.AS_deleteProperty(n, b));

        public static readonly MI objDelPropertyNsSet =
            getMemberFromExpr<Func<ASObject, string, NamespaceSet, BindOptions, bool>, MI>((x, n, s, b) => x.AS_deleteProperty(n, s, b));

        public static readonly MI objDelPropertyKey =
            getMemberFromExpr<Func<ASObject, ASAny, BindOptions, bool>, MI>((x, k, b) => x.AS_deletePropertyObj(k, b));

        public static readonly MI objDelPropertyKeyNsSet =
            getMemberFromExpr<Func<ASObject, ASAny, NamespaceSet, BindOptions, bool>, MI>((x, k, s, b) => x.AS_deletePropertyObj(k, s, b));

        public static readonly MI anyDelPropertyQName =
            getMemberFromExpr<Func<ASAny, QName, BindOptions, bool>, MI>((x, n, b) => x.AS_deleteProperty(n, b));

        public static readonly MI anyDelPropertyNsSet =
            getMemberFromExpr<Func<ASAny, string, NamespaceSet, BindOptions, bool>, MI>((x, n, s, b) => x.AS_deleteProperty(n, s, b));

        public static readonly MI anyDelPropertyKey =
            getMemberFromExpr<Func<ASAny, ASAny, BindOptions, bool>, MI>((x, k, b) => x.AS_deletePropertyObj(k, b));

        public static readonly MI anyDelPropertyKeyNsSet =
            getMemberFromExpr<Func<ASAny, ASAny, NamespaceSet, BindOptions, bool>, MI>((x, k, s, b) => x.AS_deletePropertyObj(k, s, b));

        public static readonly MI objGetDescQName =
            getMemberFromExpr<Func<ASObject, QName, BindOptions, ASAny>, MI>((x, n, b) => x.AS_getDescendants(n, b));

        public static readonly MI objGetDescNsSet =
            getMemberFromExpr<Func<ASObject, string, NamespaceSet, BindOptions, ASAny>, MI>((x, n, s, b) => x.AS_getDescendants(n, s, b));

        public static readonly MI objGetDescKey =
            getMemberFromExpr<Func<ASObject, ASAny, BindOptions, ASAny>, MI>((x, k, b) => x.AS_getDescendantsObj(k, b));

        public static readonly MI objGetDescKeyNsSet =
            getMemberFromExpr<Func<ASObject, ASAny, NamespaceSet, BindOptions, ASAny>, MI>((x, k, s, b) => x.AS_getDescendantsObj(k, s, b));

        public static readonly MI anyGetDescQName =
            getMemberFromExpr<Func<ASAny, QName, BindOptions, ASAny>, MI>((x, n, b) => x.AS_getDescendants(n, b));

        public static readonly MI anyGetDescNsSet =
            getMemberFromExpr<Func<ASAny, string, NamespaceSet, BindOptions, ASAny>, MI>((x, n, s, b) => x.AS_getDescendants(n, s, b));

        public static readonly MI anyGetDescKey =
            getMemberFromExpr<Func<ASAny, ASAny, BindOptions, ASAny>, MI>((x, k, b) => x.AS_getDescendantsObj(k, b));

        public static readonly MI anyGetDescKeyNsSet =
            getMemberFromExpr<Func<ASAny, ASAny, NamespaceSet, BindOptions, ASAny>, MI>((x, k, s, b) => x.AS_getDescendantsObj(k, s, b));

        // Since Span cannot be used as a generic argument we can't use getMemberFromExpr to retrieve
        // these methods, have to fall back to reflection API.

        public static readonly MI objCallPropertyQName =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_callProperty), new[] {s_QNameRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objCallPropertyNsSet =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_callProperty), new[] {typeof(string), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objCallPropertyKey =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_callProperty), new[] {typeof(ASAny), typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objCallPropertyKeyNsSet =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_callProperty), new[] {typeof(ASAny), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyCallPropertyQName =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_callProperty), new[] {s_QNameRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyCallPropertyNsSet =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_callProperty), new[] {typeof(string), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyCallPropertyKey =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_callProperty), new[] {typeof(ASAny), typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyCallPropertyKeyNsSet =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_callProperty), new[] {typeof(ASAny), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objConstructPropertyQName =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_constructProperty), new[] {s_QNameRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objConstructPropertyNsSet =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_constructProperty), new[] {typeof(string), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objConstructPropertyKey =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_constructProperty), new[] {typeof(ASAny), typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI objConstructPropertyKeyNsSet =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_constructProperty), new[] {typeof(ASAny), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyConstructPropertyQName =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_constructProperty), new[] {s_QNameRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyConstructPropertyNsSet =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_constructProperty), new[] {typeof(string), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyConstructPropertyKey =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_constructProperty), new[] {typeof(ASAny), typeof(ArgsSpan), typeof(BindOptions)});

        public static readonly MI anyConstructPropertyKeyNsSet =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_constructProperty), new[] {typeof(ASAny), s_NamespaceSetRef, typeof(ArgsSpan), typeof(BindOptions)});

        #endregion

        public static readonly MI objInvoke =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_invoke), new[] {typeof(ASAny), typeof(ArgsSpan)});

        public static readonly MI anyInvoke =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_invoke), new[] {typeof(ASAny), typeof(ArgsSpan)});

        public static readonly MI objConstruct =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_construct), new[] {typeof(ArgsSpan)});

        public static readonly MI anyConstruct =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_construct), new[] {typeof(ArgsSpan)});

        public static readonly MI traitGetValueStatic =
            getMemberFromExpr<Func<Trait, ASAny>, MI>(t => t.getValue());

        public static readonly MI traitGetValueInst =
            getMemberFromExpr<Func<Trait, ASAny, ASAny>, MI>((t, x) => t.getValue(x));

        public static readonly MI traitSetValueStatic =
            getMemberFromExpr<Action<Trait, ASAny>, MI>((t, v) => t.setValue(v));

        public static readonly MI traitSetValueInst =
            getMemberFromExpr<Action<Trait, ASAny, ASAny>, MI>((t, x, v) => t.setValue(x, v));

        public static readonly MI traitInvokeStatic =
            typeof(Trait).GetMethod(nameof(Trait.invoke), new[] {typeof(ArgsSpan)});

        public static readonly MI traitInvokeInst =
            typeof(Trait).GetMethod(nameof(Trait.invoke), new[] {typeof(ASAny), typeof(ArgsSpan)});

        public static readonly MI traitInvokeInstWithReceiver =
            typeof(Trait).GetMethod(nameof(Trait.invoke), new[] {typeof(ASAny), typeof(ASAny), typeof(ArgsSpan)});

        public static readonly MI traitConstructStatic =
            typeof(Trait).GetMethod(nameof(Trait.construct), new[] {typeof(ArgsSpan)});

        public static readonly MI traitConstructInst =
            typeof(Trait).GetMethod(nameof(Trait.construct), new[] {typeof(ASAny), typeof(ArgsSpan)});

        public static readonly CI objectCtor =
            getMemberFromExpr<Func<ASObject>, CI>(() => new ASObject());

        public static readonly CI arrayCtorWithNoArgs =
            getMemberFromExpr<Func<ASArray>, CI>(() => new ASArray());

        public static readonly CI arrayCtorWithLength =
            getMemberFromExpr<Func<int, ASArray>, CI>(x => new ASArray(x));

        public static readonly CI arrayCtorWithSpan =
            typeof(ASArray).GetConstructor(new[] {typeof(ArgsSpan)});

        public static readonly MI vecAnyFromObject =
            getMemberFromExpr<Func<ASObject, ASVectorAny>, MI>(x => ASVectorAny.fromObject(x));

        public static readonly MI vectorFromObject =
            getMemberFromExpr<Func<ASObject, ASVector<int>>, MI>(x => ASVector<int>.fromObject(x), stripTypeArgs: true);

        public static readonly CI vectorObjectCtor =
            getMemberFromExpr<Func<int, bool, ASVector<ASObject>>, CI>((x, f) => new ASVector<ASObject>(x, f));

        public static readonly MI vectorPushOneArg =
            getMemberFromExpr<Func<ASVector<int>, int, int>, MI>((x, v) => x.push(v), stripTypeArgs: true);

        public static readonly MI arraySetUintIndex =
            getMemberFromExpr<Action<ASArray, uint, ASAny>, MI>((a, x, v) => a.AS_setElement(x, v));

        public static readonly MI arrayPushOneArg =
            getMemberFromExpr<Func<ASArray, ASAny, uint>, MI>((a, v) => a.push(v));

        public static readonly CI dateCtorNoArgs =
            getMemberFromExpr<Func<ASDate>, CI>(() => new ASDate());

        public static readonly CI dateCtorFromValue =
            getMemberFromExpr<Func<double, ASDate>, CI>(x => new ASDate(x));

        public static readonly CI dateCtorFromString =
            getMemberFromExpr<Func<string, ASDate>, CI>(s => new ASDate(s));

        public static readonly CI dateCtorFromComponents =
            getMemberFromExpr<Func<double, double, double, double, double, double, double, bool, ASDate>, CI>(
                (y, m, d, h, mi, s, ms, u) => new ASDate(y, m, d, h, mi, s, ms, u)
            );

        public static readonly MI dateToString =
            getMemberFromExpr<Func<ASDate, string>, MI>(x => x.AS_toString());

        public static readonly MI xmlParse =
            getMemberFromExpr<Func<string, ASXML>, MI>(s => ASXML.parse(s));

        public static readonly MI xmlFromXmlCopy =
            getMemberFromExpr<Func<ASXML, ASXML>, MI>(x => ASXML.copy(x));

        public static readonly MI xmlFromXmlList =
            getMemberFromExpr<Func<ASXMLList, bool, ASXML>, MI>((x, copy) => ASXML.fromXMLList(x, copy));

        public static readonly CI xmlListCtorEmpty =
            getMemberFromExpr<Func<ASXMLList>, CI>(() => new ASXMLList());

        public static readonly MI xmlListParse =
            getMemberFromExpr<Func<string, ASXMLList>, MI>(s => ASXMLList.parse(s));

        public static readonly MI xmlListFromXml =
            getMemberFromExpr<Func<ASXML, ASXMLList>, MI>(x => ASXMLList.fromXML(x));

        public static readonly MI xmlListFromXmlListCopy =
            getMemberFromExpr<Func<ASXMLList, ASXMLList>, MI>(x => ASXMLList.shallowCopy(x));

        public static readonly CI xmlNsCtorFromURI =
            getMemberFromExpr<Func<string, ASNamespace>, CI>(x => new ASNamespace(x));

        public static readonly CI xmlNsCtorFromPrefixAndURI =
            getMemberFromExpr<Func<string, string, ASNamespace>, CI>((p, u) => new ASNamespace(p, u));

        public static readonly MI xmlNsFromQname =
            getMemberFromExpr<Func<ASQName, ASNamespace>, MI>(q => q.getNamespace());

        public static readonly CI xmlQnameCtorFromLocalName =
            getMemberFromExpr<Func<string, ASQName>, CI>(s => new ASQName(s));

        public static readonly CI xmlQnameCtorFromUriAndLocal =
            getMemberFromExpr<Func<string, string, ASQName>, CI>((s, t) => new ASQName(s, t));

        public static readonly CI xmlQnameCtorFromNsAndLocal =
            getMemberFromExpr<Func<ASNamespace, string, ASQName>, CI>((s, t) => new ASQName(s, t));

        public static readonly CI namespaceCtorFromURI =
            getMemberFromExpr<Func<string, Namespace>, CI>(x => new Namespace(x));

        public static readonly MI namespaceFromXmlNs =
            getMemberFromExpr<Func<ASNamespace, Namespace>, MI>(x => Namespace.fromASNamespace(x));

        public static readonly MI qnameFromXmlQname =
            getMemberFromExpr<Func<ASQName, QName>, MI>(x => QName.fromASQName(x));

        public static readonly MI qnamePublicName =
            getMemberFromExpr<Func<string, QName>, MI>(x => QName.publicName(x));

        public static readonly CI qnameCtorFromUriAndLocalName =
            getMemberFromExpr<Func<string, string, QName>, CI>((ns, name) => new QName(ns, name));

        public static readonly CI qnameCtorFromNsAndLocalName =
            getMemberFromExpr<Func<Namespace, string, QName>, CI>((ns, name) => new QName(ns, name));

        public static readonly MI mathMin2I =
            getMemberFromExpr<Func<int, int, int>, MI>((x, y) => Math.Min(x, y));

        public static readonly MI mathMin2U =
            getMemberFromExpr<Func<uint, uint, uint>, MI>((x, y) => Math.Min(x, y));

        public static readonly MI mathMin2D =
            getMemberFromExpr<Func<double, double, double>, MI>((x, y) => Math.Min(x, y));

        public static readonly MI mathMax2I =
            getMemberFromExpr<Func<int, int, int>, MI>((x, y) => Math.Max(x, y));

        public static readonly MI mathMax2U =
            getMemberFromExpr<Func<uint, uint, uint>, MI>((x, y) => Math.Max(x, y));

        public static readonly MI mathMax2D =
            getMemberFromExpr<Func<double, double, double>, MI>((x, y) => Math.Max(x, y));

        public static readonly MI strGetLength =
            getMemberFromExpr<Func<string, int>, PI>(x => x.Length).GetMethod;

        public static readonly MI intToStringWithRadix =
            getMemberFromExpr<Func<int, int, string>, MI>((x, r) => ASint.toString(x, r));

        public static readonly MI uintToStringWithRadix =
            getMemberFromExpr<Func<uint, int, string>, MI>((x, r) => ASuint.toString(x, r));

        public static readonly MI numberToStringWithRadix =
            getMemberFromExpr<Func<double, int, string>, MI>((x, r) => ASNumber.toString(x, r));

        public static readonly MI intValueOf =
            getMemberFromExpr<Func<int, int>, MI>(x => ASint.valueOf(x));

        public static readonly MI uintValueOf =
            getMemberFromExpr<Func<uint, uint>, MI>(x => ASuint.valueOf(x));

        public static readonly MI numberValueOf =
            getMemberFromExpr<Func<double, double>, MI>(x => ASNumber.valueOf(x));

        public static readonly MI boolValueOf =
            getMemberFromExpr<Func<bool, bool>, MI>(x => ASBoolean.valueOf(x));

        public static readonly MI strValueOf =
            getMemberFromExpr<Func<string, string>, MI>(x => ASString.valueOf(x));

        public static readonly CI regexpCtorWithRegexp =
            getMemberFromExpr<Func<ASRegExp, ASRegExp>, CI>(x => new ASRegExp(x));

        public static readonly CI regexpCtorWithPattern =
            getMemberFromExpr<Func<string, string, ASRegExp>, CI>((p, f) => new ASRegExp(p, f));

        public static readonly MI regexpLazyConstruct =
            typeof(ASRegExp).GetMethod(nameof(ASRegExp.lazyConstructRegExp));

        public static readonly FI undefinedField =
            getMemberFromExpr<Func<ASAny>, FI>(() => ASAny.undefined);

        public static readonly MI getDxns =
            getMemberFromExpr<Func<ASNamespace>, MI>(() => ASNamespace.getDefault());

        public static readonly MI setDxns =
            getMemberFromExpr<Action<ASNamespace>, MI>(x => ASNamespace.setDefault(x));

        public static readonly MI escapeXmlElem =
            getMemberFromExpr<Func<string, string>, MI>(x => ASXML.escapeText(x));

        public static readonly MI escapeXmlAttr =
            getMemberFromExpr<Func<string, string>, MI>(x => ASXML.escapeAttribute(x));

        public static readonly MI objHasNext =
            getMemberFromExpr<Func<ASObject, int, int>, MI>((x, i) => x.AS_nextIndex(i));

        public static readonly MI objNextName =
            getMemberFromExpr<Func<ASObject, int, ASAny>, MI>((x, i) => x.AS_nameAtIndex(i));

        public static readonly MI objNextValue =
            getMemberFromExpr<Func<ASObject, int, ASAny>, MI>((x, i) => x.AS_valueAtIndex(i));

        public static readonly MI objCheckFilter =
            getMemberFromExpr<Action<ASObject>, MI>(x => ASObject.AS_checkFilter(x));

        public static readonly MI hasnext2 =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_hasnext2));

        public static readonly MI applyType =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_applyType), new[] {typeof(ASObject), typeof(ArgsSpan)});

        public static readonly CI rtScopeStackNew =
            getMemberFromExpr<Func<int, RuntimeScopeStack, RuntimeScopeStack>, CI>((c, p) => new RuntimeScopeStack(c, p));

        public static readonly MI rtScopeStackPush =
            getMemberFromExpr<Action<RuntimeScopeStack, ASObject, BindOptions>, MI>((s, o, b) => s.push(o, b));

        public static readonly MI rtScopeStackPop =
            getMemberFromExpr<Action<RuntimeScopeStack>, MI>(scope => scope.pop());

        public static readonly MI rtScopeStackClear =
            getMemberFromExpr<Action<RuntimeScopeStack, int>, MI>((scope, n) => scope.clear(n));

        public static readonly MI rtScopeStackClone =
            getMemberFromExpr<Func<RuntimeScopeStack, RuntimeScopeStack>, MI>(scope => scope.clone());

        public static readonly MI rtScopeStackFindQname =
            getMemberFromExpr<Func<RuntimeScopeStack, QName, int, bool, bool, ASObject>, MI>(
                (scope, q, start, attr, strict) => scope.findProperty(q, start, attr, strict)
            );

        public static readonly MI rtScopeStackFindNsSet =
            getMemberFromExpr<Func<RuntimeScopeStack, string, NamespaceSet, int, bool, bool, ASObject>, MI>(
                (scope, name, ns, start, attr, strict) => scope.findProperty(name, ns, start, attr, strict)
            );

        public static readonly MI rtScopeStackFindKey =
            getMemberFromExpr<Func<RuntimeScopeStack, ASAny, int, bool, bool, ASObject>, MI>(
                (scope, k, start, attr, strict) => scope.findPropertyObj(k, start, attr, strict)
            );

        public static readonly MI rtScopeStackFindKeyNsSet =
            getMemberFromExpr<Func<RuntimeScopeStack, string, NamespaceSet, int, bool, bool, ASObject>, MI>(
                (scope, k, ns, start, attr, strict) => scope.findPropertyObj(k, ns, start, attr, strict)
            );

        public static readonly MI rtScopeStackGetQname =
            getMemberFromExpr<Func<RuntimeScopeStack, QName, int, bool, bool, ASAny>, MI>(
                (scope, q, start, attr, strict) => scope.getProperty(q, start, attr, strict)
            );

        public static readonly MI rtScopeStackGetNsSet =
            getMemberFromExpr<Func<RuntimeScopeStack, string, NamespaceSet, int, bool, bool, ASAny>, MI>(
                (scope, name, ns, start, attr, strict) => scope.getProperty(name, ns, start, attr, strict)
            );

        public static readonly MI rtScopeStackGetKey =
            getMemberFromExpr<Func<RuntimeScopeStack, ASAny, int, bool, bool, ASAny>, MI>(
                (scope, k, start, attr, strict) => scope.getPropertyObj(k, start, attr, strict)
            );

        public static readonly MI rtScopeStackGetKeyNsSet =
            getMemberFromExpr<Func<RuntimeScopeStack, string, NamespaceSet, int, bool, bool, ASAny>, MI>(
                (scope, k, ns, start, attr, strict) => scope.getPropertyObj(k, ns, start, attr, strict)
            );

        public static readonly MI getObjectDynamicPropCollection =
            getMemberFromExpr<Func<ASObject, DynamicPropertyCollection>, PI>(obj => obj.AS_dynamicProps).GetMethod;

        public static readonly MI dynamicPropCollectionSet =
            getMemberFromExpr<Action<DynamicPropertyCollection, string, ASAny, bool>, MI>(
                (x, key, val, isEnum) => x.setValue(key, val, isEnum)
            );

        public static readonly MI classGetClassObj =
            getMemberFromExpr<Func<Class, ASClass>, PI>(x => x.classObject).GetMethod;

        public static readonly MI methodTraitCreateMethodClosure =
            getMemberFromExpr<Func<MethodTrait, ASObject, ASFunction>, MI>((m, o) => m.createMethodClosure(o));

        public static readonly MI methodTraitCreateFunctionClosure =
            getMemberFromExpr<Func<MethodTrait, object, ASFunction>, MI>((m, s) => m.createFunctionClosure(s));

        public static readonly FI scopedClosureReceiverObj =
            getMemberFromExpr<Func<ScopedClosureReceiver, ASObject>, FI>(x => x.receiver);

        public static readonly FI scopedClosureReceiverScope =
            getMemberFromExpr<Func<ScopedClosureReceiver, object>, FI>(x => x.scope);

        public static readonly CI newException =
            getMemberFromExpr<Func<ASAny, AVM2Exception>, CI>(x => new AVM2Exception(x));

        public static readonly MI createExceptionFromCodeAndMsg =
            getMemberFromExpr<Func<RuntimeTypeHandle, string, int, AVM2Exception>, MI>(
                (type, msg, code) => AVM2Exception.create(type, msg, code)
            );

        public static readonly MI createNullRefException =
            getMemberFromExpr<Func<AVM2Exception>, MI>(
                () => AVM2Exception.createNullReferenceError()
            );

        public static readonly MI createArgCountException =
            getMemberFromExpr<Func<string, int, int, AVM2Exception>, MI>(
                (name, exp, recv) => AVM2Exception.createArgCountMismatchError(name, exp, recv)
            );

        public static readonly MI tryUnwrapCaughtException =
            typeof(AVM2Exception).GetMethod(nameof(AVM2Exception.tryUnwrapCaughtException));

        public static readonly CI optionalParamCtor =
            getMemberFromExpr<Func<int, OptionalParam<int>>, CI>(x => new OptionalParam<int>(x), stripTypeArgs: true);

        public static readonly FI optionalParamMissing =
            getMemberFromExpr<Func<OptionalParam<int>>, FI>(() => OptionalParam<int>.missing, stripTypeArgs: true);

        public static readonly MI emptyArrayOfAny =
            getMemberFromExpr<Func<ASAny[]>, MI>(() => Array.Empty<ASAny>());

        public static readonly CI restParamFromSpan =
            typeof(RestParam).GetConstructor(new[] {typeof(ReadOnlySpan<ASAny>)});

        public static readonly CI restParamFromArray =
            typeof(RestParam).GetConstructor(new[] {typeof(ASAny[])});

        public static readonly MI restParamGetLength =
            typeof(RestParam).GetProperty(nameof(RestParam.length)).GetMethod;

        public static readonly MI restParamGetSpan =
            typeof(RestParam).GetMethod(nameof(RestParam.getSpan));

        public static readonly MI restParamGetElementI =
            typeof(RestParam).GetMethod(nameof(RestParam.AS_getElement), new[] {typeof(int)});

        public static readonly MI restParamGetElementU =
            typeof(RestParam).GetMethod(nameof(RestParam.AS_getElement), new[] {typeof(uint)});

        public static readonly MI restParamGetElementD =
            typeof(RestParam).GetMethod(nameof(RestParam.AS_getElement), new[] {typeof(double)});

        public static readonly CI roSpanOfAnyFromArray =
            typeof(ReadOnlySpan<ASAny>).GetConstructor(new[] {typeof(ASAny[])});

        public static readonly MI roSpanOfAnyGet =
            typeof(ReadOnlySpan<ASAny>).GetProperty("Item").GetMethod;

        public static readonly MI roSpanOfAnyLength =
            typeof(ReadOnlySpan<ASAny>).GetProperty(nameof(ReadOnlySpan<ASAny>.Length)).GetMethod;

        public static readonly MI roSpanOfAnySlice =
            typeof(ReadOnlySpan<ASAny>).GetMethod(nameof(ReadOnlySpan<ASAny>.Slice), new[] {typeof(int)});

        public static readonly MI roSpanOfAnyEmpty =
            typeof(ReadOnlySpan<ASAny>).GetProperty(nameof(ReadOnlySpan<ASAny>.Empty)).GetMethod;

        public static readonly CI systemObjectCtor =
            getMemberFromExpr<Func<object>, CI>(() => new object());

    }

}
