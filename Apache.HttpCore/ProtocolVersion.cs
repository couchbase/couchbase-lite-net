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
using System.Text;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Represents a protocol version.</summary>
	/// <remarks>
	/// Represents a protocol version. The "major.minor" numbering
	/// scheme is used to indicate versions of the protocol.
	/// <p>
	/// This class defines a protocol version as a combination of
	/// protocol name, major version number, and minor version number.
	/// Note that
	/// <see cref="Equals(object)">Equals(object)</see>
	/// and
	/// <see cref="GetHashCode()">GetHashCode()</see>
	/// are defined as
	/// final here, they cannot be overridden in derived classes.
	/// </p>
	/// </remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public class ProtocolVersion : ICloneable
	{
		private const long serialVersionUID = 8950662842175091068L;

		/// <summary>Name of the protocol.</summary>
		/// <remarks>Name of the protocol.</remarks>
		protected internal readonly string protocol;

		/// <summary>Major version number of the protocol</summary>
		protected internal readonly int major;

		/// <summary>Minor version number of the protocol</summary>
		protected internal readonly int minor;

		/// <summary>Create a protocol version designator.</summary>
		/// <remarks>Create a protocol version designator.</remarks>
		/// <param name="protocol">the name of the protocol, for example "HTTP"</param>
		/// <param name="major">the major version number of the protocol</param>
		/// <param name="minor">the minor version number of the protocol</param>
		public ProtocolVersion(string protocol, int major, int minor)
		{
			this.protocol = Args.NotNull(protocol, "Protocol name");
			this.major = Args.NotNegative(major, "Protocol minor version");
			this.minor = Args.NotNegative(minor, "Protocol minor version");
		}

		/// <summary>Returns the name of the protocol.</summary>
		/// <remarks>Returns the name of the protocol.</remarks>
		/// <returns>the protocol name</returns>
		public string GetProtocol()
		{
			return protocol;
		}

		/// <summary>Returns the major version number of the protocol.</summary>
		/// <remarks>Returns the major version number of the protocol.</remarks>
		/// <returns>the major version number.</returns>
		public int GetMajor()
		{
			return major;
		}

		/// <summary>Returns the minor version number of the HTTP protocol.</summary>
		/// <remarks>Returns the minor version number of the HTTP protocol.</remarks>
		/// <returns>the minor version number.</returns>
		public int GetMinor()
		{
			return minor;
		}

		/// <summary>Obtains a specific version of this protocol.</summary>
		/// <remarks>
		/// Obtains a specific version of this protocol.
		/// This can be used by derived classes to instantiate themselves instead
		/// of the base class, and to define constants for commonly used versions.
		/// <br/>
		/// The default implementation in this class returns <code>this</code>
		/// if the version matches, and creates a new
		/// <see cref="ProtocolVersion">ProtocolVersion</see>
		/// otherwise.
		/// </remarks>
		/// <param name="major">the major version</param>
		/// <param name="minor">the minor version</param>
		/// <returns>
		/// a protocol version with the same protocol name
		/// and the argument version
		/// </returns>
		public virtual Org.Apache.Http.ProtocolVersion ForVersion(int major, int minor)
		{
			if ((major == this.major) && (minor == this.minor))
			{
				return this;
			}
			// argument checking is done in the constructor
			return new Org.Apache.Http.ProtocolVersion(this.protocol, major, minor);
		}

		/// <summary>
		/// Obtains a hash code consistent with
		/// <see cref="Equals(object)">Equals(object)</see>
		/// .
		/// </summary>
		/// <returns>the hashcode of this protocol version</returns>
		public sealed override int GetHashCode()
		{
			return this.protocol.GetHashCode() ^ (this.major * 100000) ^ this.minor;
		}

		/// <summary>Checks equality of this protocol version with an object.</summary>
		/// <remarks>
		/// Checks equality of this protocol version with an object.
		/// The object is equal if it is a protocl version with the same
		/// protocol name, major version number, and minor version number.
		/// The specific class of the object is <i>not</i> relevant,
		/// instances of derived classes with identical attributes are
		/// equal to instances of the base class and vice versa.
		/// </remarks>
		/// <param name="obj">the object to compare with</param>
		/// <returns>
		/// <code>true</code> if the argument is the same protocol version,
		/// <code>false</code> otherwise
		/// </returns>
		public sealed override bool Equals(object obj)
		{
			if (this == obj)
			{
				return true;
			}
			if (!(obj is Org.Apache.Http.ProtocolVersion))
			{
				return false;
			}
			Org.Apache.Http.ProtocolVersion that = (Org.Apache.Http.ProtocolVersion)obj;
			return ((this.protocol.Equals(that.protocol)) && (this.major == that.major) && (this
				.minor == that.minor));
		}

		/// <summary>Checks whether this protocol can be compared to another one.</summary>
		/// <remarks>
		/// Checks whether this protocol can be compared to another one.
		/// Only protocol versions with the same protocol name can be
		/// <see cref="CompareToVersion(ProtocolVersion)">compared</see>
		/// .
		/// </remarks>
		/// <param name="that">the protocol version to consider</param>
		/// <returns>
		/// <code>true</code> if
		/// <see cref="CompareToVersion(ProtocolVersion)">compareToVersion</see>
		/// can be called with the argument, <code>false</code> otherwise
		/// </returns>
		public virtual bool IsComparable(Org.Apache.Http.ProtocolVersion that)
		{
			return (that != null) && this.protocol.Equals(that.protocol);
		}

		/// <summary>Compares this protocol version with another one.</summary>
		/// <remarks>
		/// Compares this protocol version with another one.
		/// Only protocol versions with the same protocol name can be compared.
		/// This method does <i>not</i> define a total ordering, as it would be
		/// required for
		/// <see cref="System.IComparable{T}">System.IComparable&lt;T&gt;</see>
		/// .
		/// </remarks>
		/// <param name="that">the protocol version to compare with</param>
		/// <returns>
		/// a negative integer, zero, or a positive integer
		/// as this version is less than, equal to, or greater than
		/// the argument version.
		/// </returns>
		/// <exception cref="System.ArgumentException">
		/// if the argument has a different protocol name than this object,
		/// or if the argument is <code>null</code>
		/// </exception>
		public virtual int CompareToVersion(Org.Apache.Http.ProtocolVersion that)
		{
			Args.NotNull(that, "Protocol version");
			Args.Check(this.protocol.Equals(that.protocol), "Versions for different protocols cannot be compared: %s %s"
				, this, that);
			int delta = GetMajor() - that.GetMajor();
			if (delta == 0)
			{
				delta = GetMinor() - that.GetMinor();
			}
			return delta;
		}

		/// <summary>Tests if this protocol version is greater or equal to the given one.</summary>
		/// <remarks>Tests if this protocol version is greater or equal to the given one.</remarks>
		/// <param name="version">the version against which to check this version</param>
		/// <returns>
		/// <code>true</code> if this protocol version is
		/// <see cref="IsComparable(ProtocolVersion)">comparable</see>
		/// to the argument
		/// and
		/// <see cref="CompareToVersion(ProtocolVersion)">compares</see>
		/// as greater or equal,
		/// <code>false</code> otherwise
		/// </returns>
		public bool GreaterEquals(Org.Apache.Http.ProtocolVersion version)
		{
			return IsComparable(version) && (CompareToVersion(version) >= 0);
		}

		/// <summary>Tests if this protocol version is less or equal to the given one.</summary>
		/// <remarks>Tests if this protocol version is less or equal to the given one.</remarks>
		/// <param name="version">the version against which to check this version</param>
		/// <returns>
		/// <code>true</code> if this protocol version is
		/// <see cref="IsComparable(ProtocolVersion)">comparable</see>
		/// to the argument
		/// and
		/// <see cref="CompareToVersion(ProtocolVersion)">compares</see>
		/// as less or equal,
		/// <code>false</code> otherwise
		/// </returns>
		public bool LessEquals(Org.Apache.Http.ProtocolVersion version)
		{
			return IsComparable(version) && (CompareToVersion(version) <= 0);
		}

		/// <summary>Converts this protocol version to a string.</summary>
		/// <remarks>Converts this protocol version to a string.</remarks>
		/// <returns>a protocol version string, like "HTTP/1.1"</returns>
		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append(this.protocol);
			buffer.Append('/');
			buffer.Append(Sharpen.Extensions.ToString(this.major));
			buffer.Append('.');
			buffer.Append(Sharpen.Extensions.ToString(this.minor));
			return buffer.ToString();
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		public virtual object Clone()
		{
			return base.MemberwiseClone();
		}
	}
}
