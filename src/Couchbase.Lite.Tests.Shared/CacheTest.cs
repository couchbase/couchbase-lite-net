using System;
using NUnit.Framework;
using Couchbase.Lite.Util;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    public class CacheTest : LiteTestCase
    {        
        [Test]
        public void TestWeakReference()
        {
            WeakReference weakReference = null;
            var action = new Action(() =>
            {
                weakReference = new WeakReference(new Object());
            });
            action();

            GC.Collect();

            var isAlive = weakReference.IsAlive;
            var target = weakReference.Target;

            Assert.IsNull(target);
            Assert.IsFalse(isAlive);
        }

        [Test]
        public void TestRetainInRefObjects()
        {
            var cache = new Cache<string, object>(1);

            var key1 = "key1";
            var value1 = new Object();

            var key2 = "key2";
            var value2 = new Object();

            cache.Put(key1, value2);
            cache.Put(key2, value2);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var v1 = cache.Get(key1);
            var v2 = cache.Get(key2);

            Assert.IsNotNull(v1);
            Assert.IsNotNull(v2);
        }

        [Test]
        public void TestPruneNonRefObjects()
        {
            var cache = new Cache<string, object>(1);

            var key1 = "key1";
            var key2 = "key2";

            var action = new Action(() =>
            {
                cache.Put(key1, new Object());
                cache.Put(key2, new Object());
            });
            action();

            GC.Collect();

            var v1 = cache.Get(key1);
            var v2 = cache.Get(key2);

            Assert.IsTrue((v1 == null || v2 == null) && !(v1 == null && v2 == null));
        }

        [Test]
        public void TestPutAndRemove()
        {
            var cache = new Cache<string, object>(1);

            var key = "key";
            var value = new Object();

            Assert.IsNull(cache.Get(key));
            cache.Put(key, value);
            Assert.AreEqual(value, cache.Get(key));

            cache.Remove(key);
            Assert.IsNull(cache.Get(key));
        }

        [Test]
        public void TestClear()
        {
            var cache = new Cache<string, object>(1);

            var key = "key";
            var value = new Object();

            cache.Put(key, value);
            Assert.AreEqual(value, cache.Get(key));

            cache.Clear();
            Assert.IsNull(cache.Get(key));
        }
    }
}

