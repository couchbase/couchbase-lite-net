skip_files = ["c4Listener.h","c4Certificate.h"]
excluded = ["c4log", "c4vlog", "c4error_getMessageC", "c4str", "c4log_getDomain","c4repl_parseURL","c4SliceEqual","c4slice_free"]
force_no_bridge = ["c4repl_getResponseHeaders","c4repl_new","c4socket_gotHTTPResponse"]
default_param_name = {"C4SliceResult":"slice","C4WriteStream*":"stream","C4ReadStream*":"stream","C4Error*":"outError",
"C4BlobStore*":"store","C4BlobKey":"key","C4Slice":"slice","C4Key*":"key","bool":"b","double":"d","C4KeyReader*":"reader",
"C4DatabaseObserver*":"observer","C4DocumentObserver*":"observer","C4View*":"view","C4OnCompactCallback":"callback",
"C4Database*":"db","C4SequenceNumber":"sequence","C4StringResult":"str","C4String":"str","C4Query*":"query"}
param_bridge_types = ["C4Slice", "size_t", "size_t*","C4Slice[]", "C4String", "C4String[]"]
return_bridge_types = ["C4SliceResult", "C4Slice", "size_t", "byte*", "C4StringResult", "C4String"]
type_map = {"int32_t":"int","uint32_t":"uint","int64_t":"long","uint64_t":"ulong","size_t":"UIntPtr",
            "size_t*":"UIntPtr*","C4SequenceNumber":"ulong","C4SequenceNumber*":"ulong*","unsigned":"uint",
            "FLSharedKeys":"FLSharedKeys*","char*":"byte*","FLEncoder":"FLEncoder*","C4LogDomain":"C4LogDomain*",
            "FLDict":"FLDict*","C4FullTextID":"ulong","C4RemoteID":"uint","C4String*":"FLSlice*","FLDoc":"FLDoc*",
            "C4Timestamp": "long"}
reserved = ["string","params","ref"]
