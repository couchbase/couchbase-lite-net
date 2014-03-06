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

import com.couchbase.lite.Manager;

/**
 * A request/response/document body, stored as either JSON or a Map<String,Object>
 */
public class Body {

    private byte[] json;
    private Object object;

    public Body(byte[] json) {
        this.json = json;
    }

    public Body(Map<String, Object> properties) {
        this.object = properties;
    }

    public Body(List<?> array) {
        this.object = array;
    }

    public static Body bodyWithProperties(Map<String,Object> properties) {
        Body result = new Body(properties);
        return result;
    }

    public static Body bodyWithJSON(byte[] json) {
        Body result = new Body(json);
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
            json = Manager.getObjectMapper().writeValueAsBytes(object);
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
            object = Manager.getObjectMapper().readValue(json, Object.class);
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
                object = Manager.getObjectMapper().readValue(json, Object.class);
            } catch (IOException e) {
            }
        }
        return object != null;
    }

    public byte[] getPrettyJson() {
        Object properties = getObject();
        if(properties != null) {
            ObjectWriter writer = Manager.getObjectMapper().writerWithDefaultPrettyPrinter();
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
        if (theProperties == null) {
            return null;
        }
        return theProperties.get(key);
    }

}
