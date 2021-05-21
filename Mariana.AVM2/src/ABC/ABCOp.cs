﻿using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// An enumeration of all the ActionScript 3 bytecode instruction opcodes.
    /// </summary>
    /// <remarks>
    /// For the documentation of the AVM2 instruction set, see the
    /// <a href="https://www.adobe.com/content/dam/acom/en/devnet/pdf/avm2overview.pdf">AVM2 overview.</a>
    /// </remarks>
    public enum ABCOp : byte {
        #pragma warning disable 1591

        add = 0xA0,
        add_i = 0xC5,
        applytype = 0x53,
        astype = 0x86,
        astypelate = 0x87,
        bitand = 0xA8,
        bitnot = 0x97,
        bitor = 0xA9,
        bitxor = 0xAA,
        bkpt = 0x01,
        bkptline = 0xF2,
        call = 0x41,
        callmethod = 0x43,
        callproperty = 0x46,
        callproplex = 0x4C,
        callpropvoid = 0x4F,
        callstatic = 0x44,
        callsuper = 0x45,
        callsupervoid = 0x4E,
        checkfilter = 0x78,
        coerce = 0x80,
        coerce_a = 0x82,
        coerce_b = 0x81,
        coerce_d = 0x84,
        coerce_i = 0x83,
        coerce_o = 0x89,
        coerce_s = 0x85,
        coerce_u = 0x88,
        construct = 0x42,
        constructprop = 0x4A,
        constructsuper = 0x49,
        convert_b = 0x76,
        convert_d = 0x75,
        convert_i = 0x73,
        convert_o = 0x77,
        convert_s = 0x70,
        convert_u = 0x74,
        debug = 0xEF,
        debugfile = 0xF1,
        debugline = 0xF0,
        declocal = 0x94,
        declocal_i = 0xC3,
        decrement = 0x93,
        decrement_i = 0xC1,
        deleteproperty = 0x6A,
        divide = 0xA3,
        dup = 0x2A,
        dxns = 0x06,
        dxnslate = 0x07,
        equals = 0xAB,
        esc_xattr = 0x72,
        esc_xelem = 0x71,
        finddef = 0x5F,
        findproperty = 0x5E,
        findpropstrict = 0x5D,
        getdescendants = 0x59,
        getglobalscope = 0x64,
        getglobalslot = 0x6E,
        getlex = 0x60,
        getlocal = 0x62,
        getlocal0 = 0xD0,
        getlocal1 = 0xD1,
        getlocal2 = 0xD2,
        getlocal3 = 0xD3,
        getproperty = 0x66,
        getscopeobject = 0x65,
        getslot = 0x6C,
        getsuper = 0x04,
        greaterequals = 0xB0,
        greaterthan = 0xAF,
        hasnext = 0x1F,
        hasnext2 = 0x32,
        ifeq = 0x13,
        iffalse = 0x12,
        ifge = 0x18,
        ifgt = 0x17,
        ifle = 0x16,
        iflt = 0x15,
        ifne = 0x14,
        ifnge = 0x0F,
        ifngt = 0x0E,
        ifnle = 0x0D,
        ifnlt = 0x0C,
        ifstricteq = 0x19,
        ifstrictne = 0x1A,
        iftrue = 0x11,
        @in = 0xB4,
        inclocal = 0x92,
        inclocal_i = 0xC2,
        increment = 0x91,
        increment_i = 0xC0,
        initproperty = 0x68,
        instanceof = 0xB1,
        istype = 0xB2,
        istypelate = 0xB3,
        jump = 0x10,
        kill = 0x08,
        label = 0x09,
        lessequals = 0xAE,
        lessthan = 0xAD,
        lookupswitch = 0x1B,
        lshift = 0xA5,
        modulo = 0xA4,
        multiply = 0xA2,
        multiply_i = 0xC7,
        negate = 0x90,
        negate_i = 0xC4,
        newactivation = 0x57,
        newarray = 0x56,
        newcatch = 0x5A,
        newclass = 0x58,
        newfunction = 0x40,
        newobject = 0x55,
        nextname = 0x1E,
        nextvalue = 0x23,
        nop = 0x02,
        not = 0x96,
        pop = 0x29,
        popscope = 0x1D,
        pushbyte = 0x24,
        pushdouble = 0x2F,
        pushfalse = 0x27,
        pushint = 0x2D,
        pushnamespace = 0x31,
        pushnan = 0x28,
        pushnull = 0x20,
        pushscope = 0x30,
        pushshort = 0x25,
        pushstring = 0x2C,
        pushtrue = 0x26,
        pushuint = 0x2E,
        pushundefined = 0x21,
        pushwith = 0x1C,
        returnvalue = 0x48,
        returnvoid = 0x47,
        rshift = 0xA6,
        setglobalslot = 0x6F,
        setlocal = 0x63,
        setlocal0 = 0xD4,
        setlocal1 = 0xD5,
        setlocal2 = 0xD6,
        setlocal3 = 0xD7,
        setproperty = 0x61,
        setslot = 0x6D,
        setsuper = 0x05,
        strictequals = 0xAC,
        subtract = 0xA1,
        subtract_i = 0xC6,
        swap = 0x2B,
        @throw = 0x03,
        timestamp = 0xF3,
        @typeof = 0x95,
        urshift = 0xA7,

        // Global memory opcodes
        lix8 = 0x33,
        lix16 = 0x34,
        li8 = 0x35,
        li16 = 0x36,
        li32 = 0x37,
        lf32 = 0x38,
        lf64 = 0x39,
        si8 = 0x3A,
        si16 = 0x3B,
        si32 = 0x3C,
        sf32 = 0x3D,
        sf64 = 0x3E,
        sxi1 = 0x50,
        sxi8 = 0x51,
        sxi16 = 0x52,

        #pragma warning restore 1591
    }

}
