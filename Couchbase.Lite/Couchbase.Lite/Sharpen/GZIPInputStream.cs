namespace Sharpen
{
	using System;
	using System.IO.Compression;

	internal class GZIPInputStream : InputStream
	{
        public static readonly int GzipMagic = 0;

		public GZIPInputStream (InputStream s)
		{
			Wrapped = new GZipStream (s, CompressionMode.Decompress);
		}
	}
}
