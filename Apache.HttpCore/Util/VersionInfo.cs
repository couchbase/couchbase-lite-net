/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Org.Apache.Http.Util;
using Sharpen;
using System.Reflection;

namespace Org.Apache.Http.Util
{
	/// <summary>Provides access to version information for HTTP components.</summary>
	/// <remarks>
	/// Provides access to version information for HTTP components.
	/// Static methods are used to extract version information from property
	/// files that are automatically packaged with HTTP component release JARs.
	/// <br/>
	/// All available version information is provided in strings, where
	/// the string format is informal and subject to change without notice.
	/// Version information is provided for debugging output and interpretation
	/// by humans, not for automated processing in applications.
	/// </remarks>
	/// <since>4.0</since>
	public class VersionInfo
	{
		/// <summary>A string constant for unavailable information.</summary>
		/// <remarks>A string constant for unavailable information.</remarks>
		public const string Unavailable = "UNAVAILABLE";

		/// <summary>The filename of the version information files.</summary>
		/// <remarks>The filename of the version information files.</remarks>
		public const string VersionPropertyFile = "version.properties";

		public const string PropertyModule = "info.module";

		public const string PropertyRelease = "info.release";

		public const string PropertyTimestamp = "info.timestamp";

		/// <summary>The package that contains the version information.</summary>
		/// <remarks>The package that contains the version information.</remarks>
		private readonly string infoPackage;

		/// <summary>The module from the version info.</summary>
		/// <remarks>The module from the version info.</remarks>
		private readonly string infoModule;

		/// <summary>The release from the version info.</summary>
		/// <remarks>The release from the version info.</remarks>
		private readonly string infoRelease;

		/// <summary>The timestamp from the version info.</summary>
		/// <remarks>The timestamp from the version info.</remarks>
		private readonly string infoTimestamp;

		/// <summary>The classloader from which the version info was obtained.</summary>
		/// <remarks>The classloader from which the version info was obtained.</remarks>
		private readonly string infoClassloader;

		/// <summary>Instantiates version information.</summary>
		/// <remarks>Instantiates version information.</remarks>
		/// <param name="pckg">the package</param>
		/// <param name="module">the module, or <code>null</code></param>
		/// <param name="release">the release, or <code>null</code></param>
		/// <param name="time">the build time, or <code>null</code></param>
		/// <param name="clsldr">the class loader, or <code>null</code></param>
		protected internal VersionInfo(string pckg, string module, string release, string
			 time, string clsldr)
		{
			// the property names
			Args.NotNull(pckg, "Package identifier");
			infoPackage = pckg;
			infoModule = (module != null) ? module : Unavailable;
			infoRelease = (release != null) ? release : Unavailable;
			infoTimestamp = (time != null) ? time : Unavailable;
			infoClassloader = (clsldr != null) ? clsldr : Unavailable;
		}

		/// <summary>Obtains the package name.</summary>
		/// <remarks>
		/// Obtains the package name.
		/// The package name identifies the module or informal unit.
		/// </remarks>
		/// <returns>the package name, never <code>null</code></returns>
		public string GetPackage()
		{
			return infoPackage;
		}

		/// <summary>Obtains the name of the versioned module or informal unit.</summary>
		/// <remarks>
		/// Obtains the name of the versioned module or informal unit.
		/// This data is read from the version information for the package.
		/// </remarks>
		/// <returns>the module name, never <code>null</code></returns>
		public string GetModule()
		{
			return infoModule;
		}

		/// <summary>Obtains the release of the versioned module or informal unit.</summary>
		/// <remarks>
		/// Obtains the release of the versioned module or informal unit.
		/// This data is read from the version information for the package.
		/// </remarks>
		/// <returns>the release version, never <code>null</code></returns>
		public string GetRelease()
		{
			return infoRelease;
		}

		/// <summary>Obtains the timestamp of the versioned module or informal unit.</summary>
		/// <remarks>
		/// Obtains the timestamp of the versioned module or informal unit.
		/// This data is read from the version information for the package.
		/// </remarks>
		/// <returns>the timestamp, never <code>null</code></returns>
		public string GetTimestamp()
		{
			return infoTimestamp;
		}

		/// <summary>Obtains the classloader used to read the version information.</summary>
		/// <remarks>
		/// Obtains the classloader used to read the version information.
		/// This is just the <code>toString</code> output of the classloader,
		/// since the version information should not keep a reference to
		/// the classloader itself. That could prevent garbage collection.
		/// </remarks>
		/// <returns>the classloader description, never <code>null</code></returns>
		public string GetClassloader()
		{
			return infoClassloader;
		}

		/// <summary>Provides the version information in human-readable format.</summary>
		/// <remarks>Provides the version information in human-readable format.</remarks>
		/// <returns>a string holding this version information</returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(20 + infoPackage.Length + infoModule.Length 
				+ infoRelease.Length + infoTimestamp.Length + infoClassloader.Length);
			sb.Append("VersionInfo(").Append(infoPackage).Append(':').Append(infoModule);
			// If version info is missing, a single "UNAVAILABLE" for the module
			// is sufficient. Everything else just clutters the output.
			if (!Unavailable.Equals(infoRelease))
			{
				sb.Append(':').Append(infoRelease);
			}
			if (!Unavailable.Equals(infoTimestamp))
			{
				sb.Append(':').Append(infoTimestamp);
			}
			sb.Append(')');
			if (!Unavailable.Equals(infoClassloader))
			{
				sb.Append('@').Append(infoClassloader);
			}
			return sb.ToString();
		}

		/// <summary>Loads version information for a list of packages.</summary>
		/// <remarks>Loads version information for a list of packages.</remarks>
		/// <param name="pckgs">the packages for which to load version info</param>
		/// <param name="clsldr">
		/// the classloader to load from, or
		/// <code>null</code> for the thread context classloader
		/// </param>
		/// <returns>
		/// the version information for all packages found,
		/// never <code>null</code>
		/// </returns>
//		public static Org.Apache.Http.Util.VersionInfo[] LoadVersionInfo(string[] pckgs, 
//			ClassLoader clsldr)
//		{
//			Args.NotNull(pckgs, "Package identifier array");
//			IList<Org.Apache.Http.Util.VersionInfo> vil = new AList<Org.Apache.Http.Util.VersionInfo
//				>(pckgs.Length);
//			foreach (string pckg in pckgs)
//			{
//				Org.Apache.Http.Util.VersionInfo vi = LoadVersionInfo(pckg, clsldr);
//				if (vi != null)
//				{
//					vil.AddItem(vi);
//				}
//			}
//			return Sharpen.Collections.ToArray(vil, new Org.Apache.Http.Util.VersionInfo[vil.
//				Count]);
//		}

		/// <summary>Loads version information for a package.</summary>
		/// <remarks>Loads version information for a package.</remarks>
		/// <param name="pckg">
		/// the package for which to load version information,
		/// for example "org.apache.http".
		/// The package name should NOT end with a dot.
		/// </param>
		/// <param name="clsldr">
		/// the classloader to load from, or
		/// <code>null</code> for the thread context classloader
		/// </param>
		/// <returns>
		/// the version information for the argument package, or
		/// <code>null</code> if not available
		/// </returns>
//		public static Org.Apache.Http.Util.VersionInfo LoadVersionInfo(string pckg, ClassLoader
//			 clsldr)
//		{
//			Args.NotNull(pckg, "Package identifier");
//			ClassLoader cl = clsldr != null ? clsldr : Sharpen.Thread.CurrentThread().GetContextClassLoader
//				();
//			Properties vip = null;
//			// version info properties, if available
//			try
//			{
//				// org.apache.http      becomes
//				// org/apache/http/version.properties
//				InputStream @is = cl.GetResourceAsStream(pckg.Replace('.', '/') + "/" + VersionPropertyFile
//					);
//				if (@is != null)
//				{
//					try
//					{
//						Properties props = new Properties();
//						props.Load(@is);
//						vip = props;
//					}
//					finally
//					{
//						@is.Close();
//					}
//				}
//			}
//			catch (IOException)
//			{
//			}
//			// shamelessly munch this exception
//			Org.Apache.Http.Util.VersionInfo result = null;
//			if (vip != null)
//			{
//				result = FromMap(pckg, vip, cl);
//			}
//			return result;
//		}

		/// <summary>Instantiates version information from properties.</summary>
		/// <remarks>Instantiates version information from properties.</remarks>
		/// <param name="pckg">the package for the version information</param>
		/// <param name="info">
		/// the map from string keys to string values,
		/// for example
		/// <see cref="Sharpen.Properties">Sharpen.Properties</see>
		/// </param>
		/// <param name="clsldr">the classloader, or <code>null</code></param>
		/// <returns>the version information</returns>
//		protected internal static Org.Apache.Http.Util.VersionInfo FromMap<_T0>(string pckg
//			, IDictionary<_T0> info, ClassLoader clsldr)
//		{
//			Args.NotNull(pckg, "Package identifier");
//			string module = null;
//			string release = null;
//			string timestamp = null;
//			if (info != null)
//			{
//				module = (string)info.Get(PropertyModule);
//				if ((module != null) && (module.Length < 1))
//				{
//					module = null;
//				}
//				release = (string)info.Get(PropertyRelease);
//				if ((release != null) && ((release.Length < 1) || (release.Equals("${pom.version}"
//					))))
//				{
//					release = null;
//				}
//				timestamp = (string)info.Get(PropertyTimestamp);
//				if ((timestamp != null) && ((timestamp.Length < 1) || (timestamp.Equals("${mvn.timestamp}"
//					))))
//				{
//					timestamp = null;
//				}
//			}
//			// if info
//			string clsldrstr = null;
//			if (clsldr != null)
//			{
//				clsldrstr = clsldr.ToString();
//			}
//			return new Org.Apache.Http.Util.VersionInfo(pckg, module, release, timestamp, clsldrstr
//				);
//		}

		/// <summary>
		/// Sets the user agent to
		/// <code>"<name>/<release> (Java 1.5 minimum; Java/<java.version>)"</code>
		/// .
		/// <p/>
		/// For example:
		/// <pre>"Apache-HttpClient/4.3 (Java 1.5 minimum; Java/1.6.0_35)"</pre>
		/// </summary>
		/// <param name="name">the component name, like "Apache-HttpClient".</param>
		/// <param name="pkg">
		/// the package for which to load version information, for example "org.apache.http". The package name
		/// should NOT end with a dot.
		/// </param>
		/// <param name="cls">the class' class loader to load from, or <code>null</code> for the thread context class loader
		/// 	</param>
		/// <since>4.3</since>
		public static string GetUserAgent<_T0>(string name, string pkg, _T0 cls)
		{
			// determine the release version from packaged version info
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string release = version.ToString();
			string javaVersion = Runtime.GetProperty("java.version");
			return name + "/" + release + " (Java 1.5 minimum; Java/" + javaVersion + ")";
		}
	}
}
