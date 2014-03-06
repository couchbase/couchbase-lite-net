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

package com.couchbase.lite.util;

import com.couchbase.lite.Database;

import java.io.InputStream;

public class LoggerFactory {

    public static Logger createLogger() {

        String classname = "";
        String resource = "services/com.couchbase.lite.util.Logger";

        try {
            InputStream inputStream = Thread.currentThread().getContextClassLoader().getResourceAsStream(resource);
            if (inputStream == null) {
                // Return default System logger.
                Log.d(Database.TAG, "Unable to load " + resource + " falling back to SystemLogger");
                return new SystemLogger();
            }
            byte[] bytes = TextUtils.read(inputStream);
            classname = new String(bytes);
            if (classname == null || classname.isEmpty()) {
                // Return default System logger.
                Log.d(Database.TAG, "Unable to load " + resource + " falling back to SystemLogger");
                return new SystemLogger();
            }
            Log.d(Database.TAG, "Loading logger: " + classname);
            Class clazz = Class.forName(classname);
            Logger logger = (Logger) clazz.newInstance();
            return logger;
        } catch (Exception e) {
            throw new RuntimeException("Failed to logger.  Resource: " + resource + " classname: " + classname, e);
        }


    }
}
