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

import java.io.PrintWriter;
import java.io.StringWriter;

public class SystemLogger implements Logger {
    private final static java.util.logging.Logger logger = java.util.logging.Logger.getLogger("com.couchbase.cblite");

    @Override
    public void v(String tag, String msg) {
        logger.finer(tag + ": " + msg);
    }

    @Override
    public void v(String tag, String msg, Throwable tr) {
        logger.finer(tag + ": " + msg + "\n" + getStackTraceString(tr));
    }

    @Override
    public void d(String tag, String msg) {
        logger.fine(tag + ": " + msg);
    }

    @Override
    public void d(String tag, String msg, Throwable tr) {
        logger.fine(tag + ": " + msg + "\n" + getStackTraceString(tr));
    }

    @Override
    public void i(String tag, String msg) {
        logger.info(tag + ": " + msg);
    }

    @Override
    public void i(String tag, String msg, Throwable tr) {
        logger.info(tag + ": " + msg + "\n" + getStackTraceString(tr));
    }

    @Override
    public void w(String tag, String msg) {
        logger.warning(tag + ": " + msg);
    }

    @Override
    public void w(String tag, Throwable tr) {
        logger.warning(tag + ": " + "\n" + getStackTraceString(tr));
    }

    @Override
    public void w(String tag, String msg, Throwable tr) {
        logger.warning(tag + ": " + msg + "\n" + getStackTraceString(tr));
    }

    @Override
    public void e(String tag, String msg) {
        logger.severe(tag + ": " + msg);
    }

    @Override
    public void e(String tag, String msg, Throwable tr) {
        logger.severe(tag + ": " + msg + "\n" + getStackTraceString(tr));
    }

    private static String getStackTraceString(Throwable tr) {
        if (tr == null) return "";

        StringWriter stringWriter = new StringWriter();
        PrintWriter printWriter = new PrintWriter(stringWriter);
        tr.printStackTrace(printWriter);

        return stringWriter.toString();
    }
}