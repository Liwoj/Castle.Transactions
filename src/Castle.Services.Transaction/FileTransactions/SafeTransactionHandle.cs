#region License
// Copyright 2004-2022 Castle Project - https://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace Castle.Services.Transaction
{
    using System;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;

    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// A safe file handle on the transaction resource.
    /// </summary>

#if NETFRAMEWORK
    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
#endif
    public sealed class SafeTransactionHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public SafeTransactionHandle()
            : base(true)
        {
        }

        /// <summary>
        /// Constructor for taking a pointer to a transaction.
        /// </summary>
        /// <param name="handle">The transactional handle.</param>
#if NETFRAMEWORK
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif
        public SafeTransactionHandle(IntPtr handle)
            : base(true)
        {
            base.handle = handle;
        }

        protected override bool ReleaseHandle()
        {
            if (!(IsInvalid || IsClosed))
            {
                return CloseHandle(handle);
            }

            return IsInvalid || IsClosed;
        }

        /*
         * BOOL WINAPI CloseHandle(__in HANDLE hObject);
         */
        [DllImport("kernel32.dll")]
        //[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        //[SuppressUnmanagedCodeSecurity]
        private static extern bool CloseHandle(IntPtr handle);
    }
}