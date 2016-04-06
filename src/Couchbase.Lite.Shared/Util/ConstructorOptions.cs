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

        public Type ConcreteType { get; set; }
    }
        
    internal abstract class ConstructorOptions
    {
        private readonly string Tag;

        protected ConstructorOptions()
        {
            Tag = GetType().Name;
        }

        public void Validate()
        {
            var allProps = GetType().GetProperties(BindingFlags.Instance
                | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in allProps) {
                var reqAtt = (RequiredPropertyAttribute)prop.GetCustomAttributes(typeof(RequiredPropertyAttribute), false).FirstOrDefault();
                if (reqAtt != null) {
                    if (prop.PropertyType.IsValueType) {
                        Log.To.NoDomain.W(Tag, "Skipping {0} required attribute because it is a value type", prop.Name);
                        continue;
                    }
                        
                    if (prop.GetValue(this, null) == null) {
                        if (reqAtt.CreateDefault) {
                            try {
                                prop.SetValue(this, Activator.CreateInstance(reqAtt.ConcreteType ?? prop.PropertyType), null);
                            } catch (Exception) {
                                Log.To.NoDomain.E(Tag, "{0} has no suitable constructor, cannot set default value, throwing...", prop.Name);
                                throw new InvalidOperationException(String.Format("Couldn't set default value on an instance of {0}", Tag));
                            }
                        } else {
                            Log.To.NoDomain.E(Tag, "Required property {0} missing value, throwing...", prop.Name);
                            throw new ArgumentNullException(String.Format("{0}.{1}", Tag, prop.Name));
                        }
                    }
                }
            }
        }
    }
}

