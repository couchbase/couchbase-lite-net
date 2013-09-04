package com.couchbase.cblite.support;

public class Range {
    private int location;
    private int length;

    Range(int location, int length) {
        this.location = location;
        this.length = length;
    }

    public int getLocation() {
        return location;
    }

    public int getLength() {
        return length;
    }
}
