// 
//  C4Base.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4LogCallback(C4LogDomain* domain, C4LogLevel level, IntPtr message, IntPtr args);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void C4ExtraInfoDestructor(void* ptr);

    internal unsafe static partial class Native
    {
        public static void c4db_release(C4Database* db) => c4base_release(db);

        public static void* c4db_retain(C4Database* db) => c4base_retain(db);

        public static void c4query_release(C4Query* query) => c4base_release(query);

        public static void c4cert_release(C4Cert* cert) => c4base_release(cert);

        public static void* c4cert_retain(C4Cert* cert) => c4base_retain(cert);

        public static void c4keypair_release(C4KeyPair* keyPair) => c4base_release(keyPair);

        public static void* c4keypair_retain(C4KeyPair* keyPair) => c4base_retain(keyPair);

        public static void FLSliceResult_Release(FLSliceResult flSliceResult) => _FLBuf_Release(flSliceResult.buf);
    }

    [ExcludeFromCodeCoverage]
    internal partial struct C4Error
    {
        #region Constructors

        public C4Error(C4ErrorDomain domain, int code)
        {
            this.code = (Int24)code;
            this.domain = domain;
            internal_info = 0;
        }

        public C4Error(C4ErrorCode code) : this(C4ErrorDomain.LiteCoreDomain, (int) code)
        {
        }

        public C4Error(FLError code) : this(C4ErrorDomain.FleeceDomain, (int) code)
        {
        }

        public C4Error(C4NetworkErrorCode code) : this(C4ErrorDomain.NetworkDomain, (int) code)
        {
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            if (obj is C4Error other) {
                return other.code == code && other.domain == domain;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Hasher.Start
                .Add(code)
                .Add(domain)
                .GetHashCode();
        }

        #endregion

    }

    [Serializable]
    internal struct Int24 : IComparable, IFormattable, IConvertible, IComparable<Int24>, IComparable<Int32>, IEquatable<Int24>, IEquatable<Int32>
    {
        #region Constants

        private const int MaxValue32 = 8388607;
        private const int MinValue32 = -8388608;
        private const int BitMask = -16777216;

        #endregion

        #region Variables

        private readonly int _value; // 4-byte integer

        #endregion

        #region Constructors

        public Int24(int value)
        {
            if (value > (MaxValue32 + 1) || value < MinValue32)
                throw new OverflowException($"Value of {value} is out of 24-bit signed integer range.");

            _value = ApplyBitMask(value);
        }

        #endregion

        #region Public Methods

        public int CompareTo(Int24 value)
        {
            return CompareTo((int) value);
        }

        public int CompareTo(int value)
        {
            return (_value < value ? -1 : (_value > value ? 1 : 0));
        }

        public int CompareTo(object obj)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object obj)
        {
            if (obj is int || obj is Int24)
                return Equals((int) obj);

            return false;
        }

        public bool Equals(Int24 obj)
        {
            return Equals((int) obj);
        }

        public bool Equals(int obj)
        {
            return (_value == obj);
        }

        public override int GetHashCode()
        {
            return _value;
        }
        
        #endregion

        #region IConvertible

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return _value;
        }

        public TypeCode GetTypeCode()
        {
            return TypeCode.Int32;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            throw new NotImplementedException();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Comparison Operators

        public static bool operator ==(Int24 value1, Int24 value2)
        {
            return value1.Equals(value2);
        }

        public static bool operator ==(Int24 value1, int value2)
        {
            return ((int) value1).Equals(value2);
        }

        public static bool operator !=(Int24 value1, Int24 value2)
        {
            return !value1.Equals(value2);
        }

        public static bool operator !=(Int24 value1, int value2)
        {
            return !((int) value1).Equals(value2);
        }

        public static bool operator <(Int24 value1, int value2)
        {
            return (value1.CompareTo(value2) < 0);
        }

        public static bool operator >(Int24 value1, int value2)
        {
            return (value1.CompareTo(value2) > 0);
        }

        #endregion

        #region Explicit Narrowing Conversions

        public static explicit operator Int24(Enum value)
        {
            return new Int24(Convert.ToInt32(value));
        }

        public static explicit operator Int24(int value)
        {
            return new Int24(value);
        }

        public static explicit operator C4ErrorCode(Int24 v)
        {
            return (C4ErrorCode) ((int)v);
        }

        #endregion

        #region Implicit Widening Conversions

        public static implicit operator Int24(C4ErrorCode value)
        {
            return new Int24((int) value);
        }

        public static implicit operator int(Int24 value)
        {
            return ((IConvertible) value).ToInt32(null);
        }

        #endregion

        #region Arithmetic Operators
        public static Int24 operator +(Int24 value1, Int24 value2)
        {
            return (Int24) ((int) value1 + (int) value2);
        }

        public static int operator +(int value1, Int24 value2)
        {
            return (value1 + (int) value2);
        }

        public static int operator +(Int24 value1, int value2)
        {
            return ((int) value1 + value2);
        }

        #endregion

        #region Private Methods

        private static int ApplyBitMask(int value)
        {
            // Check bit 23, the sign bit in a signed 24-bit integer
            if ((value & 0x00800000) > 0) {
                // If the sign-bit is set, this number will be negative - set all high-byte bits (keeps 32-bit number in 24-bit range)
                value |= BitMask;
            } else {
                // If the sign-bit is not set, this number will be positive - clear all high-byte bits (keeps 32-bit number in 24-bit range)
                value &= ~BitMask;
            }

            return value;
        }

        #endregion
    }
}