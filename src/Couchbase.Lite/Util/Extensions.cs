using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Lite.Util
{
    public static class Extensions
    {
        public static U Get<T, U>(this IDictionary<T, U> d, T key)
        {
            U val = default(U);
            d.TryGetValue(key, out val);
            return val;
        }

        public static U Get<T, U>(this IReadOnlyDictionary<T, U> d, T key)
        {
            U val = default(U);
            d.TryGetValue(key, out val);
            return val;
        }

        public static bool TryCast<T>(object obj, out T castVal)
        {
            //If the types already match then things are easy
            if(obj is T) {
                castVal = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                castVal = (T)Convert.ChangeType(obj, typeof(T));
            } catch(Exception) {
                castVal = default(T);
                return false;
            }

            return true;
        }

        public static T CastOrDefault<T>(object obj, T defaultVal)
        {
            T retVal;
            if(obj != null && TryCast<T>(obj, out retVal)) {
                return retVal;
            }

            return defaultVal;
        }

        public static T CastOrDefault<T>(object obj)
        {
            return CastOrDefault<T>(obj, default(T));
        }

        public static T GetCast<T>(this IDictionary<string, object> collection, string key)
        {
            return collection.GetCast(key, default(T));
        }

        public static T GetCast<T>(this IDictionary<string, object> collection, string key, T defaultVal)
        {
            object value = collection.Get(key);
            return CastOrDefault<T>(value, defaultVal);
        }

        public static T GetCast<T>(this IReadOnlyDictionary<string, object> collection, string key)
        {
            return collection.GetCast(key, default(T));
        }

        public static T GetCast<T>(this IReadOnlyDictionary<string, object> collection, string key, T defaultVal)
        {
            object value = collection.Get(key);
            return CastOrDefault<T>(value, defaultVal);
        }

        public static bool TryGetValue<T>(this IDictionary<string, object> dic, string key, out T value)
        {
            value = default(T);
            object obj;
            if(!dic.TryGetValue(key, out obj)) {
                return false;
            }

            //If the types already match then things are easy
            if((obj is T)) {
                value = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                value = (T)Convert.ChangeType(obj, typeof(T));
                return true;
            } catch(Exception) {
                return false;
            }
        }

        public static bool TryGetValue<T>(this IReadOnlyDictionary<string, object> dic, string key, out T value)
        {
            value = default(T);
            object obj;
            if(!dic.TryGetValue(key, out obj)) {
                return false;
            }

            //If the types already match then things are easy
            if((obj is T)) {
                value = (T)obj;
                return true;
            }

            try {
                //Take the slow route for things like boxed value types
                value = (T)Convert.ChangeType(value, typeof(T));
                return true;
            } catch(Exception) {
                return false;
            }
        }
    }
}
