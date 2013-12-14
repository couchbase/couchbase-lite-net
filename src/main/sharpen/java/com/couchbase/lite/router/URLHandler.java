package com.couchbase.lite.router;

import java.io.IOException;
import java.net.URL;
import java.net.URLStreamHandler;


public class URLHandler extends URLStreamHandler {

    @Override
    protected java.net.URLConnection openConnection(URL u) throws IOException {
        return new URLConnection(u);
    }

}
