/**
 * Original iOS version by  Jens Alfke
 * Ported to Android by Marty Schoch
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

package com.couchbase.lite.internal;

import java.io.IOException;
import java.util.Collections;
import java.util.List;
import java.util.Map;

import org.codehaus.jackson.map.ObjectWriter;

import com.couchbase.lite.CBLManager;

/**
 * A request/response/document body, stored as either JSON or a Map<String,Object>
 */
public class CBLBody {

    private byte[] json;
    private Object object;

    public CBLBody(byte[] json) {
        this.json = json;
    }

    public CBLBody(Map<String, Object> properties) {
        this.object = properties;
    }

    public CBLBody(List<?> array) {
        this.object = array;
    }

    public static CBLBody bodyWithProperties(Map<String,Object> properties) {
        CBLBody result = new CBLBody(properties);
        return result;
    }

    public static CBLBody bodyWithJSON(byte[] json) {
        CBLBody result = new CBLBody(json);
        return result;
    }

    public byte[] getJson() {
        if (json == null) {
            lazyLoadJsonFromObject();
        }
        return json;
    }

    private void lazyLoadJsonFromObject() {
        if (object == null) {
            throw new IllegalStateException("Both json and object are null for this body: " + this);
        }
        try {
            json = CBLManager.getObjectMapper().writeValueAsBytes(object);
        } catch (IOException e) {
            throw new RuntimeException(e);
        }
    }

    public Object getObject() {
        if (object == null) {
            lazyLoadObjectFromJson();
        }
        return object;
    }

    private void lazyLoadObjectFromJson() {
        if (json == null) {
            throw new IllegalStateException("Both object and json are null for this body: " + this);
        }
        try {
            object = CBLManager.getObjectMapper().readValue(json, Object.class);
        } catch (IOException e) {
            throw new RuntimeException(e);
        }
    }

    public boolean isValidJSON() {
        if (object == null) {
            boolean gotException = false;
            if (json == null) {
                throw new IllegalStateException("Both object and json are null for this body: " + this);
            }
            try {
                object = CBLManager.getObjectMapper().readValue(json, Object.class);
            } catch (IOException e) {
            }
        }
        return object != null;
    }

    public byte[] getPrettyJson() {
        Object properties = getObject();
        if(properties != null) {
            ObjectWriter writer = CBLManager.getObjectMapper().writerWithDefaultPrettyPrinter();
            try {
                json = writer.writeValueAsBytes(properties);
            } catch (IOException e) {
                throw new RuntimeException(e);
            }
        }
        return getJson();
    }

    public String getJSONString() {
        return new String(getJson());
    }

    @SuppressWarnings("unchecked")
    public Map<String, Object> getProperties() {
        Object object = getObject();
        if(object instanceof Map) {
            Map<String, Object> map = (Map<String, Object>) object;
            return Collections.unmodifiableMap(map);
        }
        return null;
    }

    public Object getPropertyForKey(String key) {
        Map<String,Object> theProperties = getProperties();
        return theProperties.get(key);
    }

}
