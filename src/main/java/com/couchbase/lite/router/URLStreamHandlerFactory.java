package com.couchbase.lite.router;

import java.net.URL;
import java.net.URLStreamHandler;


public class URLStreamHandlerFactory implements java.net.URLStreamHandlerFactory {

    public static final String SCHEME = "cblite";  // eg, cblite://

    @Override
    public URLStreamHandler createURLStreamHandler(String protocol) {
        if(SCHEME.equals(protocol)) {
            return new URLHandler();
        }
        return null;
    }

    public static void registerSelfIgnoreError() {
        try {
            URL.setURLStreamHandlerFactory(new URLStreamHandlerFactory());
        } catch (Error e) {
            //usually you should never catch an Error
            //but I can't see how to avoid this
        }
    }

}
