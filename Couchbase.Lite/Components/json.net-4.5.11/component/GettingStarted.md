For simple conversions to and from JSON strings and .NET objects,
JsonConvert provides the SerializeObject and DeserializeObject methods.

```csharp
public class Person
{
	public string Name { get; set; }
	public DateTime Birthday { get; set; }
}

Person person = new Person { Name = "Bob", Birthday = new DateTime (1987, 2, 2) };
string output = Newtonsoft.Json.JsonConvert.SerializeObject (person);
Console.WriteLine (output);
Console.WriteLine();

person = Newtonsoft.Json.JsonConvert.DeserializeObject<Person> (output);
Console.WriteLine ("{0} - {1}", person.Name, person.Birthday);
```

For dealing with JSON objects in more direct form, there's LINQ to JSON:

```csharp
string json = @"{ Name: 'Bob', HairColor: 'Brown' }";
var bob = Newtonsoft.Json.Linq.JObject.Parse (json);

Console.WriteLine ("{0} with {1} hair", (string)bob["Name"], (string)bob["HairColor"]);
```