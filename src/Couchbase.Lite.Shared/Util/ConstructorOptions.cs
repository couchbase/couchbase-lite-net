//
// ConstructorOptions.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredPropertyAttribute : Attribute
    {
        public bool CreateDefault { get; set; }
    }
        
    internal abstract class ConstructorOptions
    {
        private readonly string Tag;
        private readonly List<PropertyInfo> _uncheckedProps = new List<PropertyInfo>();

        protected ConstructorOptions()
        {
            Tag = GetType().Name;
            var allProps = GetType().GetProperties(BindingFlags.DeclaredOnly | BindingFlags.NonPublic);
            foreach (var prop in allProps) {
                if (prop.PropertyType.IsValueType) {
                    Log.To.NoDomain.W(Tag, "Skipping {0} required attribute because it is a value type", prop.Name);
                    continue;
                }

                var reqAtt = (RequiredPropertyAttribute)prop.GetCustomAttributes(typeof(RequiredPropertyAttribute), false).FirstOrDefault();
                if (reqAtt != null) {
                    if (reqAtt.CreateDefault) {
                        try {
                            prop.SetValue(this, Activator.CreateInstance(prop.PropertyType));
                        } catch(Exception) {
                            Log.To.NoDomain.W(Tag, "{0} has no default constructor, cannot set default value", prop.Name);
                            _uncheckedProps.Add(prop);
                        }
                    } else {
                        _uncheckedProps.Add(prop);
                    }
                }
            }
        }

        public void Validate()
        {
            foreach (var prop in _uncheckedProps) {
                if (prop.GetValue(this) == null) {
                    Log.To.NoDomain.E(Tag, "{0} is marked as required and cannot be null, throwing...", prop.Name);
                    throw new NullReferenceException(String.Format("{0} cannot be null", prop.Name));
                }
            }
        }
    }
}

