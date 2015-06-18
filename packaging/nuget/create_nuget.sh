#!/bin/sh
nuget pack -BasePath ../.. couchbase-lite.nuspec 
nuget pack -BasePath ../.. couchbase-lite-listener.nuspec 
nuget pack -BasePath ../.. couchbase-lite-listener-bonjour.nuspec 
