Json.NET is a high-performance JSON framework.

## Features

 - Flexible JSON serializer for converting between .NET objects and JSON
 - LINQ to JSON for manually reading and writing JSON
 - High performance, faster than .NET's built-in JSON serializers
 - Write indented, easy to read JSON
 - Convert JSON to and from XML

For simple conversions to and from JSON strings and .NET objects,
JsonConvert provides the SerializeObject and DeserializeObject methods.

```csharp
using Newtonsoft.Json;
...

public class Person
{
    public string Name { get; set; }
    public DateTime Birthday { get; set; }
}

void PersonToJsonToPersonExample ()
{
    var person = new Person { Name = "Bob", Birthday = new DateTime (1987, 2, 2) };
    var json = JsonConvert.SerializeObject (person);
    Console.WriteLine ("JSON representation of person: {0}", json);
    var person2 = JsonConvert.DeserializeObject<Person> (json);
    Console.WriteLine ("{0} - {1}", person2.Name, person2.Birthday);
}
```

For dealing with JSON data in more direct form, without mapping them to C# classes, use LINQ to JSON:

```csharp
using Newtonsoft.Json.Linq;
...

void LinqExample ()
{
    string json = @"{ Name: 'Bob', HairColor: 'Brown' }";
    var bob = JObject.Parse (json);
    
    Console.WriteLine ("{0} with {1} hair", bob["Name"], bob["HairColor"]);
}
```
