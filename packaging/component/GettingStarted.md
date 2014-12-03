# Getting Started with Couchbase

You'll probably create a new document when the user creates a persistent data item in your app, such as a reminder, a photograph or a high score. To save this, you'll construct a JSON-compatible representation of the data, then instantiate a new `Document` and save the data to it.

Here's an example from Grocery Sync:

```
    var vals =  new Dictionary<String,Object> {
        { "text" , value },
        { "check" , false },
        { "created_at" , jsonDate }
    };
```

Next, ask the Database (instantiated when you initialized Couchbase Lite, remember?) for a new document. This doesn't add anything to the database yet; just like the New command in a typical Mac or Windows app, the document won't be stored on disk until you save some data into it. Continuing from the previous example:

```
    var doc = Database.CreateDocument();
```

## Saving A Document

Finally save the contents to the document:

```
    var result = doc.PutProperties (vals);
    if (result == null)
        throw new ApplicationException ("failed to save a new document");
```

## Reading A Document

If later on you want to retrieve the contents of the document, you'll need to obtain the `Document` object representing it, then get the contents from that object.

There are two ways to get the `Document`:

 1. You might know its ID (maybe you kept it in memory, maybe you got it from `NSUserDefaults` or even from a property of another document), in which case you can call `database.DocumentWithID`.
 2. Or you might be iterating the results of a view query (or `AllDocument`, which is a special view), in which case you can get it from the `QueryRow`'s `document` property.

Then to get the document's contents, access its `properties` property:

```
	Document doc = this.Database.GetExistingDocument(documentID);
	IDictionary<string,object> contents = doc.Properties;
```

Alternatively, you can use the shortcut `PropertyForKey` to get one property at a time:

```
	var text = (string) doc.Properties["text"];
	var checked = (bool) doc.Properties["check"];
```

You might be wondering which of these lines actually hits the database. The answer is that the `Document` starts out empty and loads its contents on demand, then caches them in memory; so it's the call to `document.Properties` in the first example, or the first `PropertyForKey` call in the second example. Afterwards, getting properties is as cheap as a dictionary lookup. (For this reason it's best not to keep references to huge numbers of `Document` objects, or you'll end up storing all their contents in memory. Instead, rely on queries to look up documents as you need them.)

## Updating A Document

Updating a document is trivial: You just call `PutProperties` again.

OK, it's not quite that trivial. Remember the dry theoretical discussion of Multiversion Concurrency Control (MVCC) back in section 2? Here's where it gets real. When you update a document, Couchbase Lite wants to know _which revision you updated_, so it can stop you if there were any updates in the meantime. (Otherwise, you would wipe out those updates by overwriting them.) I'll get into update-conflict handling in a little bit; for now, just realize that Couchbase Lite wants to see that `_rev` property in the properties you're putting.

Fortunately this is painlessly accomplished, since the `_rev` property was already in the dictionary you got from the `Document`. So all you need to do is _modify the properties dictionary_ and hand back the modified dictionary, which still contains the `_rev` property, to `PutProperties`

```
    var newProperties = new Dictionary<String, Object>(doc.Properties);
	newProperties["tag"] = 4567;
```

`newProperties` is now a copy of the existing document (including the important `_rev` property), with the value of the `checked` property toggled.

Finally you save the document the same way you did when you created it:

```
    SavedVersion newVersion = doc.PutProperties(newProperties);
    if (newVersion == null)
        ShowErrorAlert("Couldn't update the item.");
```

## Deleting A Document

Deleting is a lot like updating; instead of calling `PutProperties:` you call `DeleteDocument:`. Here's the sample code, which should be familiar looking by now:

```
    doc.Delete();
    if (!doc.Deleted) ...        
```
