/**
 * Created by Wayne Carter.
 *
 * Copyright (c) 2012 Couchbase, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

package com.couchbase.lite.storage;


import com.couchbase.lite.Database;
import com.couchbase.lite.util.Log;
import com.couchbase.lite.util.TextUtils;

import java.io.IOException;
import java.io.InputStream;
import java.net.URL;
import java.util.ServiceLoader;

public class SQLiteStorageEngineFactory {

    public static SQLiteStorageEngine createStorageEngine() {

        String classname = "";

        try {
            InputStream inputStream = Thread.currentThread().getContextClassLoader().getResourceAsStream("services/com.couchbase.lite.storage.SQLiteStorageEngine");
            byte[] bytes = TextUtils.read(inputStream);
            classname = new String(bytes);
            Log.d(Database.TAG, "Loading storage engine: " + classname);
            Class clazz = Class.forName(classname);
            SQLiteStorageEngine storageEngine = (SQLiteStorageEngine) clazz.newInstance();
            return storageEngine;

        } catch (Exception e) {
            throw new RuntimeException("Failed to load storage engine from: " + classname, e);
        }

    }
}
