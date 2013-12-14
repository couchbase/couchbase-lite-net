using System;

namespace Sharpen
{
  public static class UriHelpers
  {
    public static Uri FromFile(FilePath file)
    {
      return file.ToURI();
    }
  }
}

