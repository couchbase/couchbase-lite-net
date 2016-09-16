These are some of the guidelines that I follow when writing code (I am converting old code that doesn't follow this but it is a time consuming process so not all code in the base conforms to this yet, but I would like all new code to do so).  It is a work in progress.

- Use spaces, not tabs (more specifically four spaces per tab)
- `using` statements sorted alphabetically, with a line break between .NET namespaces and third party namespaces
- Brackets are as follows:
```c#
namespace 
{
  class
  {
    Method()
    {
      if {
          
      } else {
          
      }
          
      while {
          
      }
          
      for {
          
      }
          
      using {
          
      }
      
      try {
      
      } catch {
      
      } finally {
      
      }
          
      (closure) => 
      {
          
      }
    }
  }
}
```

- Private variables begin with an underscore and a lowercase letter (a-z) `_privateVar`
- Private static variables begin with an underscore and an uppercase letter (A-Z) `_StaticVar`
- Property names, constants, and static readonly fields use Pascal casing `PropertyName`
- All public non-changing fields should be static readonly as opposed to const
- Code should employ the following `#region` blocks structure (omitted if no items fall into that category)
```c#
#region Constants

#endregion

#region Variables

#endregion

#region Properties

#endregion

#region Constructors

#endregion

#region Public Methods

#endregion

#region Protected Methods
//If a method has more than one access modifier, put it in the
//highest region that it fits in
#endregion

#region Internal Methods

#endregion

#region Private Methods

#endregion

#region Overrides
//If there are a lot from multiple classes, separate by base class
#endregion

#region IWhatever
//Repeat for all implemented interfaces
#endregion

#region Nested Classes
//This helps get stuff out of the way for editors with no region folding
#endregion
```

- All public and protected methods should have documentation comments (i.e. the `///` kind).
- All modifiers should be written, regardless of their optionality (i.e. private variables should have the `private` keyword, even though it is the default)
- Use the compiler aliases for primitives (i.e. `bool` not `Boolean`, `int` not `Int32`, etc), except when using their constants or static methods.
- The code should include comments where needed, but only ones that make sense.
