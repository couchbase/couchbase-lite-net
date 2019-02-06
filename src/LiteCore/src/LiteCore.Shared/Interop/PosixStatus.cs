// 
//  PosixStatus.cs
// 
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Reflection;
using System.Runtime.InteropServices;

using JetBrains.Annotations;

namespace Couchbase.Lite
{
    /// <summary>
    /// The base codes that are guaranteed to be the same on any POSIX system
    /// </summary>
    public abstract class PosixBase
    {
        /// <summary>
        /// Operation not permitted 
        /// </summary>
        public static readonly int EPERM = 1;

        /// <summary>
        /// No such file or directory
        /// </summary>
        public static readonly int ENOENT = 2;

        /// <summary>
        /// No such process
        /// </summary>
        public static readonly int ESRCH = 3;

        /// <summary>
        /// Interrupted system call
        /// </summary>
        public static readonly int EINTR = 4;

        /// <summary>
        /// Input/output error
        /// </summary>
        public static readonly int EIO = 5;

        /// <summary>
        /// Device not configured
        /// </summary>
        public static readonly int ENXIO = 6;

        /// <summary>
        /// Argument list too long
        /// </summary>
        public static readonly int E2BIG = 7;

        /// <summary>
        /// Exec format error
        /// </summary>
        public static readonly int ENOEXEC = 8;

        /// <summary>
        /// Bad file descriptor
        /// </summary>
        public static readonly int EBADF = 9;

        /// <summary>
        /// No child processes
        /// </summary>
        public static readonly int ECHILD = 10; 

        /// <summary>
        /// Cannot allocate memory
        /// </summary>
        public static readonly int ENOMEM = 12;

        /// <summary>
        /// Permission denied
        /// </summary>
        public static readonly int EACCES = 13;

        /// <summary>
        /// Bad address
        /// </summary>
        public static readonly int EFAULT = 14;

        /// <summary>
        /// Device / Resource busy
        /// </summary>
        public static readonly int EBUSY = 16;

        /// <summary>
        /// File exists
        /// </summary>
        public static readonly int EEXIST = 17;

        /// <summary>
        /// Cross-device link
        /// </summary>
        public static readonly int EXDEV = 18;

        /// <summary>
        /// Operation not supported by device
        /// </summary>
        public static readonly int ENODEV = 19;

        /// <summary>
        /// Not a directory
        /// </summary>
        public static readonly int ENOTDIR = 20;

        /// <summary>
        /// Is a directory
        /// </summary>
        public static readonly int EISDIR = 21;

        /// <summary>
        /// Invalid argument
        /// </summary>
        public static readonly int EINVAL = 22;

        /// <summary>
        /// Too many open files in system
        /// </summary>
        public static readonly int ENFILE = 23;

        /// <summary>
        /// Too many open files
        /// </summary>
        public static readonly int EMFILE = 24;

        /// <summary>
        /// Inappropriate ioctl for device
        /// </summary>
        public static readonly int ENOTTY = 25;

        /// <summary>
        /// File too large
        /// </summary>
        public static readonly int EFBIG = 27;

        /// <summary>
        /// No space left on device
        /// </summary>
        public static readonly int ENOSPC = 28;

        /// <summary>
        /// Illegal seek
        /// </summary>
        public static readonly int ESPIPE = 29;

        /// <summary>
        /// Read-only file system
        /// </summary>
        public static readonly int EROFS = 30;

        /// <summary>
        /// Too many links
        /// </summary>
        public static readonly int EMLINK = 31;

        /// <summary>
        /// Broken pipe
        /// </summary>
        public static readonly int EPIPE = 32;

        /* math software */
        /// <summary>
        /// Numerical argument out of domain
        /// </summary>
        public static readonly int EDOM = 33;

        /// <summary>
        /// Result too large
        /// </summary>
        public static readonly int ERANGE = 34;

        /// <summary>
        /// Gets the correct value for a given code (based on the name of the error e.g. ECONNREFUSED)
        /// for the currently executing OS
        /// </summary>
        /// <param name="name">The name of the code to get the value for (e.g. ECONNREFUSED)</param>
        /// <returns>The correct code for the given error, or 0 if the name does not exist</returns>
        public static int GetCode([NotNull]string name)
        {
            if (name == null) {
                throw new ArgumentNullException(nameof(name));
            }

            Type classType;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                classType = typeof(PosixWindows);
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                classType = typeof(PosixMac);
            } else {
                classType = typeof(PosixLinux);
            }

            var field = classType.GetField(name.ToUpperInvariant(), BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            return (int?)field?.GetValue(null) ?? 0;
        }

        /// <summary>
        /// Checks whether or not the given code is the same as the code for the POSIX
        /// error name in a cross platform way.
        /// </summary>
        /// <param name="name">The name of the code to get the value for (e.g. ECONNREFUSED)</param>
        /// <param name="code">The code to check</param>
        /// <returns><c>true</c> if the code matches, otherwise <c>false</c></returns>
        public static bool IsError([NotNull]string name, int code)
        {
            var codeForName = GetCode(name);
            return code == codeForName;
        }
    }

    /// <summary>
    /// POSIX codes that are specific to Windows
    /// </summary>
    public sealed class PosixWindows : PosixBase
    {
        /// <summary>
        /// Resource temporarily unavailable
        /// </summary>
        public static readonly int EAGAIN = 11;

        /// <summary>
        /// Resource deadlock avoided
        /// </summary>
        public static readonly int EDEADLK = 36;

        /// <summary>
        /// File name too long
        /// </summary>
        public static readonly int ENAMETOOLONG = 38;

        /// <summary>
        /// No locks available
        /// </summary>
        public static readonly int ENOLCK = 39;

        /// <summary>
        /// Function not implemented
        /// </summary>
        public static readonly int ENOSYS = 40;

        /// <summary>
        /// Directory not empty
        /// </summary>
        public static readonly int ENOTEMPTY = 41;

        /// <summary>
        /// Illegal byte sequence
        /// </summary>
        public static readonly int EILSEQ = 42;

        /// <summary>
        /// Address already in use
        /// </summary>
        public static readonly int EADDRINUSE = 100;

        /// <summary>
        /// Can't assign requested address
        /// </summary>
        public static readonly int EADDRNOTAVAIL = 101;

        /// <summary>
        /// Address family not supported by protocol family
        /// </summary>
        public static readonly int EAFNOSUPPORT = 102;

        /// <summary>
        /// Operation already in progress
        /// </summary>
        public static readonly int EALREADY = 103;

        /// <summary>
        /// Bad message
        /// </summary>
        public static readonly int EBADMSG = 104;

        /// <summary>
        /// Operation canceled
        /// </summary>
        public static readonly int ECANCELED = 105;

        /// <summary>
        /// Software caused connection abort
        /// </summary>
        public static readonly int ECONNABORTED = 106;

        /// <summary>
        /// Connection refused
        /// </summary>
        public static readonly int ECONNREFUSED = 107;

        /// <summary>
        /// Connection reset by peer
        /// </summary>
        public static readonly int ECONNRESET = 108;

        /// <summary>
        /// Destination address required
        /// </summary>
        public static readonly int EDESTADDRREQ = 109;

        /// <summary>
        /// No route to host
        /// </summary>
        public static readonly int EHOSTUNREACH = 110;

        /// <summary>
        /// Identifier removed
        /// </summary>
        public static readonly int EIDRM = 111;

        /// <summary>
        /// Operation now in progress
        /// </summary>
        public static readonly int EINPROGRESS = 112;

        /// <summary>
        /// Socket is already connected
        /// </summary>
        public static readonly int EISCONN = 113;

        /// <summary>
        /// Too many levels of symbolic links
        /// </summary>
        public static readonly int ELOOP = 114;

        /// <summary>
        /// Message too long
        /// </summary>
        public static readonly int EMSGSIZE = 115;

        /// <summary>
        /// Network is down
        /// </summary>
        public static readonly int ENETDOWN = 116;

        /// <summary>
        /// Network dropped connection on reset
        /// </summary>
        public static readonly int ENETRESET = 117;

        /// <summary>
        /// Network is unreachable
        /// </summary>
        public static readonly int ENETUNREACH = 118;

        /// <summary>
        /// No buffer space available
        /// </summary>
        public static readonly int ENOBUFS = 119;

        /// <summary>
        /// No message available on STREAM
        /// </summary>
        public static readonly int ENODATA = 120;

        /// <summary>
        /// Link has been severed
        /// </summary>
        public static readonly int ENOLINK = 121;

        /// <summary>
        /// No message of desired type
        /// </summary>
        public static readonly int ENOMSG = 122;

        /// <summary>
        /// Protocol not available
        /// </summary>
        public static readonly int ENOPROTOOPT = 123;

        /// <summary>
        /// No STREAM resources
        /// </summary>
        public static readonly int ENOSR = 124;

        /// <summary>
        /// Not a STREAM
        /// </summary>
        public static readonly int ENOSTR = 125;

        /// <summary>
        /// Socket is not connected
        /// </summary>
        public static readonly int ENOTCONN = 126;

        /// <summary>
        /// State not recoverable
        /// </summary>
        public static readonly int ENOTRECOVERABLE = 127;

        /// <summary>
        /// Socket operation on non-socket
        /// </summary>
        public static readonly int ENOTSOCK = 128;

        /// <summary>
        /// Operation not supported
        /// </summary>
        public static readonly int ENOTSUP = 129;

        /// <summary>
        /// Operation not supported on socket
        /// </summary>
        public static readonly int EOPNOTSUPP = 130;

        /// <summary>
        /// Undefined error
        /// </summary>
        public static readonly int EOTHER = 131;

        /// <summary>
        /// Value too large to be stored in data type
        /// </summary>
        public static readonly int EOVERFLOW = 132;

        /// <summary>
        /// Previous owner died
        /// </summary>
        public static readonly int EOWNERDEAD = 133;

        /// <summary>
        /// Protocol error
        /// </summary>
        public static readonly int EPROTO = 134;

        /// <summary>
        /// Protocol not supported
        /// </summary>
        public static readonly int EPROTONOSUPPORT = 135;

        /// <summary>
        /// Protocol wrong type for socket
        /// </summary>
        public static readonly int EPROTOTYPE = 136;

        /// <summary>
        /// STREAM ioctl timeout
        /// </summary>
        public static readonly int ETIME = 137;

        /// <summary>
        /// Operation timed out
        /// </summary>
        public static readonly int ETIMEDOUT = 138;

        /// <summary>
        /// Text file busy
        /// </summary>
        public static readonly int ETXTBSY = 139;

        /// <summary>
        /// Operation would block
        /// </summary>
        public static readonly int EWOULDBLOCK = 140;
    }

    /// <summary>
    /// A class containing POSIX codes that are unique to macOS
    /// </summary>
    public sealed class PosixMac : PosixBase
    {
        /// <summary>
        /// Resource deadlock avoided
        /// </summary>
        public static readonly int EDEADLK = 11;

        /// <summary>
        /// Block device required
        /// </summary>
        public static readonly int ENOTBLK = 15;

        /// <summary>
        /// Text file busy
        /// </summary>
        public static readonly int ETXTBSY = 26;

        /// <summary>
        /// Resource temporarily unavailable
        /// </summary>
        public static readonly int EAGAIN = 35;

        /// <summary>
        /// Operation would block
        /// </summary>
        public static readonly int EWOULDBLOCK = EAGAIN;

        /// <summary>
        /// Operation now in progress
        /// </summary>
        public static readonly int EINPROGRESS = 36;

        /// <summary>
        /// Operation already in progress
        /// </summary>
        public static readonly int EALREADY = 37;

        /// <summary>
        /// Socket operation on non-socket
        /// </summary>
        public static readonly int ENOTSOCK = 38;

        /// <summary>
        /// Destination address required
        /// </summary>
        public static readonly int EDESTADDRREQ = 39;

        /// <summary>
        /// Message too long
        /// </summary>
        public static readonly int EMSGSIZE = 40;

        /// <summary>
        /// Protocol wrong type for socket
        /// </summary>
        public static readonly int EPROTOTYPE = 41;

        /// <summary>
        /// Protocol not available
        /// </summary>
        public static readonly int ENOPROTOOPT = 42;

        /// <summary>
        /// Protocol not supported
        /// </summary>
        public static readonly int EPROTONOSUPPORT = 43;

        /// <summary>
        /// Socket type not supported
        /// </summary>
        public static readonly int ESOCKTNOSUPPORT = 44;

        /// <summary>
        /// Operation not supported
        /// </summary>
        public static readonly int ENOTSUP = 45;

        /// <summary>
        /// Operation not supported on socket
        /// </summary>
        public static readonly int EOPNOTSUPP = ENOTSUP;

        /// <summary>
        /// Protocol family not supported
        /// </summary>
        public static readonly int EPFNOSUPPORT = 46;

        /// <summary>
        /// Address family not supported by protocol family
        /// </summary>
        public static readonly int EAFNOSUPPORT = 47;

        /// <summary>
        /// Address already in use
        /// </summary>
        public static readonly int EADDRINUSE = 48;

        /// <summary>
        /// Can't assign requested address
        /// </summary>
        public static readonly int EADDRNOTAVAIL = 49;

        /// <summary>
        /// Network is down
        /// </summary>
        public static readonly int ENETDOWN = 50;

        /// <summary>
        /// Network is unreachable
        /// </summary>
        public static readonly int ENETUNREACH = 51;

        /// <summary>
        /// Network dropped connection on reset
        /// </summary>
        public static readonly int ENETRESET = 52;

        /// <summary>
        /// Software caused connection abort
        /// </summary>
        public static readonly int ECONNABORTED = 53;

        /// <summary>
        /// Connection reset by peer
        /// </summary>
        public static readonly int ECONNRESET = 54;

        /// <summary>
        /// No buffer space available
        /// </summary>
        public static readonly int ENOBUFS = 55;

        /// <summary>
        /// Socket is already connected
        /// </summary>
        public static readonly int EISCONN = 56;

        /// <summary>
        /// Socket is not connected
        /// </summary>
        public static readonly int ENOTCONN = 57;

        /// <summary>
        /// Can't send after socket shutdown
        /// </summary>
        public static readonly int ESHUTDOWN = 58;

        /// <summary>
        /// Too many references: can't splice
        /// </summary>
        public static readonly int ETOOMANYREFS = 59;

        /// <summary>
        /// Operation timed out
        /// </summary>
        public static readonly int ETIMEDOUT = 60;

        /// <summary>
        /// Connection refused
        /// </summary>
        public static readonly int ECONNREFUSED = 61;

        /// <summary>
        /// Too many levels of symbolic links
        /// </summary>
        public static readonly int ELOOP = 62;

        /// <summary>
        /// File name too long
        /// </summary>
        public static readonly int ENAMETOOLONG = 63;

        /// <summary>
        /// Host is down
        /// </summary>
        public static readonly int EHOSTDOWN = 64;

        /// <summary>
        /// No route to host
        /// </summary>
        public static readonly int EHOSTUNREACH = 65;

        /// <summary>
        /// Directory not empty
        /// </summary>
        public static readonly int ENOTEMPTY = 66;

        /// <summary>
        /// Too many processes
        /// </summary>
        public static readonly int EPROCLIM = 67;

        /// <summary>
        /// Too many users
        /// </summary>
        public static readonly int EUSERS = 68;

        /// <summary>
        /// Disc quota exceeded
        /// </summary>
        public static readonly int EDQUOT = 69;

        /// <summary>
        /// Stale NFS file handle
        /// </summary>
        public static readonly int ESTALE = 70;

        /// <summary>
        /// Too many levels of remote in path
        /// </summary>   
        public static readonly int EREMOTE = 71;

        /// <summary>
        /// RPC struct is bad
        /// </summary>
        public static readonly int EBADRPC = 72;

        /// <summary>
        /// RPC version wrong
        /// </summary>
        public static readonly int ERPCMISMATCH = 73;

        /// <summary>
        /// RPC prog. not avail
        /// </summary>
        public static readonly int EPROGUNAVAIL = 74;

        /// <summary>
        /// Program version wrong
        /// </summary>
        public static readonly int EPROGMISMATCH = 75;

        /// <summary>
        /// Bad procedure for program
        /// </summary>
        public static readonly int EPROCUNAVAIL = 76;

        /// <summary>
        /// No locks available
        /// </summary>
        public static readonly int ENOLCK = 77;

        /// <summary>
        /// Function not implemented
        /// </summary>
        public static readonly int ENOSYS = 78;

        /// <summary>
        /// Inappropriate file type or format
        /// </summary>
        public static readonly int EFTYPE = 79;

        /// <summary>
        /// Authentication error
        /// </summary>
        public static readonly int EAUTH = 80;

        /// <summary>
        /// Need authenticator
        /// </summary>
        public static readonly int ENEEDAUTH = 81;

        /// <summary>
        /// Device power is off
        /// </summary>
        public static readonly int EPWROFF = 82;

        /// <summary>
        /// Device error, e.g. paper out
        /// </summary>
        public static readonly int EDEVERR = 83;

        /// <summary>
        /// Value too large to be stored in data type
        /// </summary>
        public static readonly int EOVERFLOW = 84;

        /// <summary>
        /// Bad executable
        /// </summary>
        public static readonly int EBADEXEC = 85;

        /// <summary>
        /// Bad CPU type in executable
        /// </summary>
        public static readonly int EBADARCH = 86;

        /// <summary>
        /// Shared library version mismatch
        /// </summary>
        public static readonly int ESHLIBVERS = 87;

        /// <summary>
        /// Malformed Macho file
        /// </summary>
        public static readonly int EBADMACHO = 88;

        /// <summary>
        /// Operation canceled
        /// </summary>
        public static readonly int ECANCELED = 89;

        /// <summary>
        /// Identifier removed
        /// </summary>
        public static readonly int EIDRM = 90;

        /// <summary>
        /// No message of desired type
        /// </summary>
        public static readonly int ENOMSG = 91;

        /// <summary>
        /// Illegal byte sequence
        /// </summary>
        public static readonly int EILSEQ = 92;

        /// <summary>
        /// Attribute not found
        /// </summary>
        public static readonly int ENOATTR = 93;

        /// <summary>
        /// Bad message
        /// </summary>
        public static readonly int EBADMSG = 94;

        /// <summary>
        /// Multihop attempted
        /// </summary>
        public static readonly int EMULTIHOP = 95;

        /// <summary>
        /// No message available on STREAM
        /// </summary>
        public static readonly int ENODATA = 96;

        /// <summary>
        /// Reserved
        /// </summary>
        public static readonly int ENOLINK = 97;

        /// <summary>
        /// No STREAM resources
        /// </summary>
        public static readonly int ENOSR = 98;

        /// <summary>
        /// Not a STREAM
        /// </summary>
        public static readonly int ENOSTR = 99;

        /// <summary>
        /// Protocol error
        /// </summary>
        public static readonly int EPROTO = 100;

        /// <summary>
        /// STREAM ioctl timeout
        /// </summary>
        public static readonly int ETIME = 101;

        /// <summary>
        /// No such policy registered
        /// </summary>
        public static readonly int ENOPOLICY = 103;

        /// <summary>
        /// State not recoverable
        /// </summary>
        public static readonly int ENOTRECOVERABLE = 104;

        /// <summary>
        /// Previous owner died
        /// </summary>
        public static readonly int EOWNERDEAD = 105;

        /// <summary>
        /// Interface output queue is full
        /// </summary>
        public static readonly int EQFULL = 106;
    }

    /// <summary>
    /// A class containing POSIX codes that are unique to the Linux kernel
    /// </summary>
    public sealed class PosixLinux : PosixBase
    {
        /// <summary>
        /// Resource temporarily unavailable
        /// </summary>
        public static readonly int EAGAIN = 11;

        /// <summary>
        /// Operation would block
        /// </summary>
        public static readonly int EWOULDBLOCK = EAGAIN;

        /// <summary>
        /// Block device required
        /// </summary>
        public static readonly int ENOTBLK = 15;

        /// <summary>
        /// Text file busy
        /// </summary>
        public static readonly int ETXTBSY = 26;

        /// <summary>
        /// Resource deadlock would occur
        /// </summary>
        public static readonly int EDEADLK = 35;

        /// <summary>
        /// File name too long
        /// </summary>
        public static readonly int ENAMETOOLONG = 36;

        /// <summary>
        /// No record locks available
        /// </summary>
        public static readonly int ENOLCK = 37;

        /// <summary>
        /// Function not implemented
        /// </summary>
        public static readonly int ENOSYS = 38;

        /// <summary>
        /// Directory not empty
        /// </summary>
        public static readonly int ENOTEMPTY = 39;

        /// <summary>
        /// Too many symbolic links encountered
        /// </summary>
        public static readonly int ELOOP = 40;

        /// <summary>
        /// No message of desired type
        /// </summary>
        public static readonly int ENOMSG = 42;

        /// <summary>
        /// Identifier removed
        /// </summary>
        public static readonly int EIDRM = 43;

        /// <summary>
        /// Channel number out of range
        /// </summary>
        public static readonly int ECHRNG = 44;

        /// <summary>
        /// Level 2 not synchronized
        /// </summary>
        public static readonly int EL2NSYNC = 45;

        /// <summary>
        /// Level 3 halted
        /// </summary>
        public static readonly int EL3HLT = 46;

        /// <summary>
        /// Level 3 reset
        /// </summary>
        public static readonly int EL3RST = 47;

        /// <summary>
        /// Link number out of range
        /// </summary>
        public static readonly int ELNRNG = 48;

        /// <summary>
        /// Protocol driver not attached
        /// </summary>
        public static readonly int EUNATCH = 49;

        /// <summary>
        /// No CSI structure available
        /// </summary>
        public static readonly int ENOCSI = 50;

        /// <summary>
        /// Level 2 halted
        /// </summary>
        public static readonly int EL2HLT = 51;

        /// <summary>
        /// Invalid exchange
        /// </summary>
        public static readonly int EBADE = 52;

        /// <summary>
        /// Invalid request descriptor
        /// </summary>
        public static readonly int EBADR = 53;

        /// <summary>
        /// Exchange full
        /// </summary>
        public static readonly int EXFULL = 54;

        /// <summary>
        /// No anode
        /// </summary>
        public static readonly int ENOANO = 55;

        /// <summary>
        /// Invalid request code
        /// </summary>
        public static readonly int EBADRQC = 56;

        /// <summary>
        /// Invalid slot
        /// </summary>
        public static readonly int EBADSLT = 57;

        /// <summary>
        /// Bad font file format
        /// </summary>
        public static readonly int EBFONT = 59;
        
        /// <summary>
        /// Device not a stream
        /// </summary>
        public static readonly int ENOSTR = 60;
        
        /// <summary>
        /// No data available
        /// </summary>
        public static readonly int ENODATA = 61;

        /// <summary>
        /// Timer expired
        /// </summary>
        public static readonly int ETIME = 62;

        /// <summary>
        /// Out of streams resources
        /// </summary>
        public static readonly int ENOSR = 63;

        /// <summary>
        /// Machine is not on the network
        /// </summary>
        public static readonly int ENONET = 64;

        /// <summary>
        /// Package not installed
        /// </summary>
        public static readonly int ENOPKG = 65;

        /// <summary>
        /// Object is remote
        /// </summary>   
        public static readonly int EREMOTE = 66;

        /// <summary>
        /// Link has been severed
        /// </summary>
        public static readonly int ENOLINK = 67;

        /// <summary>
        /// Advertise error
        /// </summary>
        public static readonly int EADV = 68;

        /// <summary>
        /// Srmount error
        /// </summary>
        public static readonly int ESRMNT = 69;

        /// <summary>
        /// Communication error on send
        /// </summary>
        public static readonly int ECOMM = 70;

        /// <summary>
        /// Protocol error
        /// </summary>
        public static readonly int EPROTO = 71;

        /// <summary>
        /// Multihop attempted
        /// </summary>
        public static readonly int EMULTIHOP = 72;

        /// <summary>
        /// RFS specific error
        /// </summary>
        public static readonly int EDOTDOT = 73;

        /// <summary>
        /// Not a data message
        /// </summary>
        public static readonly int EBADMSG = 74;

        /// <summary>
        /// Value too large for defined data type
        /// </summary>
        public static readonly int EOVERFLOW = 75;

        /// <summary>
        /// Name not unique on network
        /// </summary>
        public static readonly int ENOTUNIQ = 76;

        /// <summary>
        /// File descriptor in bad state
        /// </summary>
        public static readonly int EBADFD = 77;

        /// <summary>
        /// Remote address changed
        /// </summary>
        public static readonly int EREMCHG = 78;

        /// <summary>
        /// Can not access a needed shared library
        /// </summary>
        public static readonly int ELIBACC = 79;

        /// <summary>
        /// Accessing a corrupted shared library
        /// </summary>
        public static readonly int ELIBBAD = 80;

        /// <summary>
        /// .lib section in a.out corrupted
        /// </summary>
        public static readonly int ELIBSCN = 81;

        /// <summary>
        /// Attempting to link in too many shared libraries
        /// </summary>
        public static readonly int ELIBMAX = 82;

        /// <summary>
        /// Cannot exec a shared library directly
        /// </summary>
        public static readonly int ELIBEXEC = 83;

        /// <summary>
        /// Illegal byte sequence
        /// </summary>
        public static readonly int EILSEQ = 84;

        /// <summary>
        /// Interrupted system call should be restarted
        /// </summary>
        public static readonly int ERESTART = 85;

        /// <summary>
        /// Streams pipe error
        /// </summary>
        public static readonly int ESTRPIPE = 86;

        /// <summary>
        /// Too many users
        /// </summary>
        public static readonly int EUSERS = 87;

        /// <summary>
        /// Socket operation on non-socket
        /// </summary>
        public static readonly int ENOTSOCK = 88;

        /// <summary>
        /// Destination address required
        /// </summary>
        public static readonly int EDESTADDRREQ = 89;

        /// <summary>
        /// Message too long
        /// </summary>
        public static readonly int EMSGSIZE = 90;

        /// <summary>
        /// Protocol wrong type for socket
        /// </summary>
        public static readonly int EPROTOTYPE = 91;

        /// <summary>
        /// Protocol not available
        /// </summary>
        public static readonly int ENOPROTOOPT = 92;

        /// <summary>
        /// Protocol not supported
        /// </summary>
        public static readonly int EPROTONOSUPPORT = 93;

        /// <summary>
        /// Socket type not supported
        /// </summary>
        public static readonly int ESOCKTNOSUPPORT = 94;

        /// <summary>
        /// Operation not supported on transport endpoint
        /// </summary>
        public static readonly int EOPNOTSUPP = 95;

        /// <summary>
        /// Protocol family not supported
        /// </summary>
        public static readonly int EPFNOSUPPORT = 96;

        /// <summary>
        /// Address family not supported by protocol
        /// </summary>
        public static readonly int EAFNOSUPPORT = 97;

        /// <summary>
        /// Address already in use
        /// </summary>
        public static readonly int EADDRINUSE = 98;

        /// <summary>
        /// Cannot assign requested address
        /// </summary>
        public static readonly int EADDRNOTAVAIL = 99;

        /// <summary>
        /// Network is down
        /// </summary>
        public static readonly int ENETDOWN = 100;

        /// <summary>
        /// Network is unreachable
        /// </summary>
        public static readonly int ENETUNREACH = 101;

        /// <summary>
        /// Network dropped connection because of reset
        /// </summary>
        public static readonly int ENETRESET = 102;

        /// <summary>
        /// Software caused connection abort
        /// </summary>
        public static readonly int ECONNABORTED = 103;

        /// <summary>
        /// Connection reset by peer
        /// </summary>
        public static readonly int ECONNRESET = 104;

        /// <summary>
        /// No buffer space available
        /// </summary>
        public static readonly int ENOBUFS = 105;

        /// <summary>
        /// Transport endpoint is already connected
        /// </summary>
        public static readonly int EISCONN = 106;

        /// <summary>
        /// Transport endpoint is not connected
        /// </summary>
        public static readonly int ENOTCONN = 107;

        /// <summary>
        /// Cannot send after transport endpoint shutdown
        /// </summary>
        public static readonly int ESHUTDOWN = 108;

        /// <summary>
        /// Too many references: cannot splice
        /// </summary>
        public static readonly int ETOOMANYREFS = 109;

        /// <summary>
        /// Connection timed out
        /// </summary>
        public static readonly int ETIMEDOUT = 110;

        /// <summary>
        /// Connection refused
        /// </summary>
        public static readonly int ECONNREFUSED = 111;

        /// <summary>
        /// Host is down
        /// </summary>
        public static readonly int EHOSTDOWN = 112;

        /// <summary>
        /// No route to host
        /// </summary>
        public static readonly int EHOSTUNREACH = 113;

        /// <summary>
        /// Operation already in progress
        /// </summary>
        public static readonly int EALREADY = 114;

        /// <summary>
        /// Operation now in progress
        /// </summary>
        public static readonly int EINPROGRESS = 115;

        /// <summary>
        /// Stale NFS file handle
        /// </summary>
        public static readonly int ESTALE = 116;

        /// <summary>
        /// Structure needs cleaning
        /// </summary>
        public static readonly int EUNCLEAN = 117;

        /// <summary>
        /// Not a XENIX named type file
        /// </summary>
        public static readonly int ENOTNAM = 118;

        /// <summary>
        /// No XENIX semaphores available
        /// </summary>
        public static readonly int ENAVAIL = 119;

        /// <summary>
        /// Is a named type file
        /// </summary>
        public static readonly int EISNAM = 120;

        /// <summary>
        /// Remote I/O error
        /// </summary>
        public static readonly int EREMOTEIO = 121;

        /// <summary>
        /// Quota exceeded
        /// </summary>
        public static readonly int EDQUOT = 122;

        /// <summary>
        /// No medium found
        /// </summary>
        public static readonly int ENOMEDIUM = 123;

        /// <summary>
        /// Wrong medium type
        /// </summary>
        public static readonly int EMEDIUMTYPE = 124;

        /// <summary>
        /// Operation Canceled
        /// </summary>
        public static readonly int ECANCELED = 125;

        /// <summary>
        /// Required key not available
        /// </summary>
        public static readonly int ENOKEY = 126;

        /// <summary>
        /// Key has expired
        /// </summary>
        public static readonly int EKEYEXPIRED = 127;

        /// <summary>
        /// Key has been revoked
        /// </summary>
        public static readonly int EKEYREVOKED = 128;

        /// <summary>
        /// Key was rejected by service
        /// </summary>
        public static readonly int EKEYREJECTED = 129;

        /// <summary>
        /// Owner died
        /// </summary>
        public static readonly int EOWNERDEAD = 130;

        /// <summary>
        /// State not recoverable
        /// </summary>
        public static readonly int ENOTRECOVERABLE = 131;
    }
}
