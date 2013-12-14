using System;

namespace Sharpen
{
  public static class UriHelpers
  {
    public static Uri FromFile(this FilePath file)
    {
      return file.ToURI();
    }
  }
}

