package com.couchbase.lite.router;

import java.net.URL;
import java.net.URLStreamHandler;
import java.net.URLStreamHandlerFactory;


public class CBLURLStreamHandlerFactory implements URLStreamHandlerFactory {

    public static final String SCHEME = "cblite";  // eg, cblite://

    @Override
    public URLStreamHandler createURLStreamHandler(String protocol) {
        if(SCHEME.equals(protocol)) {
            return new CBLURLHandler();
        }
        return null;
    }

    public static void registerSelfIgnoreError() {
        try {
            URL.setURLStreamHandlerFactory(new CBLURLStreamHandlerFactory());
        } catch (Error e) {
            //usually you should never catch an Error
            //but I can't see how to avoid this
        }
    }

}
