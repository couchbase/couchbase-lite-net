// 
//  Assertions.cs
// 
//  Copyright (c) 2025 Couchbase, Inc All rights reserved.
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

using Shouldly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CI = System.Globalization.CultureInfo;

namespace Couchbase.Lite;

public static class Assertions
{
    private static bool IsNumericType(object? obj)
    {
        return obj is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static bool IsDateType(object? obj)
    {
        return obj is DateTime or DateTimeOffset;
    }

    private static bool IsDictionary(object? obj)
    {
        return obj is IDictionary ||
                obj is IDictionaryObject ||
               (obj != null && obj.GetType().GetInterfaces()
                   .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)));
    }

    private static DateTimeOffset ToDateTimeOffset(this object input)
    {
        if(input is DateTimeOffset dto) {
            return dto;
        }

        if(input is DateTime dt) {
            return new DateTimeOffset(dt, TimeSpan.Zero);
        }

        throw new ArgumentException("Object is not a DateTime or DateTimeOffset");
    }

    private static IDictionary<string, object?> ToDictionary(this object input)
    {
        switch (input) {
            case IDictionary<string, object?> d:
                return d;
            case IDictionaryObject dObj:
                return dObj.ToDictionary();
            case IDictionary iDict:
            {
                var result = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in iDict) {
                    result[entry.Key.ToString().ShouldNotBeNull()] = entry.Value;
                }
                return result;
            }
            default:
                throw new ArgumentException("Object is not a dictionary");
        }
    }

    public static void ShouldNotBeEquivalentTo(this object? actual, object? expected, string? customMessage = null)
    {
        var areEquivalent = true;
        try {
            actual.ShouldBeEquivalentTo(expected);
        } catch(ShouldAssertException) {
            areEquivalent = false;
        }

        areEquivalent.ShouldBeFalse(customMessage);
    }

    // Add back in the behavior of FluentAssertions to disregards int vs long, etc. when
    // making an equivalency check
    public static void ShouldBeEquivalentToFluent(this object? actual, object? expected, string? customMessage = null)
    {
        if (actual == null && expected == null) {
            return;
        }

        actual.ShouldNotBeNull(customMessage);
        expected.ShouldNotBeNull(customMessage);


        // Case 1: Both objects are numeric types directly
        if (IsNumericType(actual) && IsNumericType(expected)) {
            Convert.ToDouble(actual, CI.InvariantCulture).ShouldBe(Convert.ToDouble(expected, CI.InvariantCulture), 
                Double.Epsilon, customMessage);
            return;
        }

        // Case 2: DateTime
        if(IsDateType(actual) && IsDateType(expected)) {
            expected.ToDateTimeOffset().ShouldBe(actual.ToDateTimeOffset(), 
                TimeSpan.FromMilliseconds(1), customMessage);
            return;
        }

        // Case 3: Both are strings (take care of this now because strings are
        // also IEnumerable)
        if (actual is string aStr && expected is string eStr) {
            aStr.ShouldBe(eStr, customMessage);
            return;
        }

        // Case 4: Both blobs
        if(actual is Blob && expected is Blob) {
            actual.Equals(expected).ShouldBeTrue(customMessage);
            return;
        }

        // Case 5: Both dictionaries
        if (IsDictionary(actual) && IsDictionary(expected)) {
            var aDict = ToDictionary(actual);
            var eDict = ToDictionary(expected);
            aDict.Count.ShouldBe(eDict.Count, customMessage);
            foreach(var key in eDict.Keys) {
                aDict.ContainsKey(key).ShouldBeTrue(customMessage);
                aDict[key].ShouldBeEquivalentToFluent(eDict[key], customMessage);
            }
            return;
        }

        // Case 6: Both are otherwise collections
        if(actual is IEnumerable aEnum && expected is IEnumerable eEnum) {
            var aList = aEnum.Cast<object?>().ToList();
            var eList = eEnum.Cast<object?>().ToList();
            aList.Count.ShouldBe(eList.Count, customMessage);
            for (int i = 0; i < aList.Count; i++) {
                aList[i].ShouldBeEquivalentToFluent(eList[i], customMessage);
            }
            return;
        }


        // Final fallback to default behavior
        actual.ShouldBeEquivalentTo(expected);
    }

    public static void ShouldContainKeys<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, params TKey[] keys)
        where TKey : notnull
    {
        foreach(var key in keys) {
            dictionary.ShouldContainKey(key);
        }
    }

    public static void ShouldEqual(this object? actual, object? expected, string? customMessage = null)
    {
        if (actual == null && expected == null) {
            return;
        }

        if(ReferenceEquals(actual, expected)) {
            return;
        }

        actual.ShouldNotBeNull(customMessage);
        expected.ShouldNotBeNull(customMessage);
        actual.Equals(expected).ShouldBeTrue(customMessage);
    }

    public static void ShouldNotEqual(this object? actual, object? expected, string? customMessage = null)
    {
        if (actual == null && expected == null) {
            return;
        }

        if (ReferenceEquals(actual, expected)) {
            return;
        }

        actual.ShouldNotBeNull(customMessage);
        expected.ShouldNotBeNull(customMessage);
        actual.Equals(expected).ShouldBeFalse(customMessage);
    }
}