
// Copyright (C) 2011-2012 Luca Piccioni
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

// Field is never assigned to, and will always have its default value null
// Note: fields in SimdLibrary class will be assigned by means of reflections, indeed disable the warning on this module

#pragma warning disable 649

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenGL
{
	/// <summary>
	/// Main interface for executing operations on memory.
	/// </summary>
	public unsafe class Memory
	{
		#region Constructors

		/// <summary>
		/// Static constructor.
		/// </summary>
		static Memory()
		{
			// Try to load optional library
			//LoadSimdExtensions();

			// Ensure MemoryCopy functionality
			EnsureMemoryCopy();
		}

		#endregion

		#region CPU Information

		/// <summary>
		/// Delegate used for getting CPU information.
		/// </summary>
		/// <returns></returns>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		[System.Security.SuppressUnmanagedCodeSecurity()]
		[return : MarshalAs(UnmanagedType.Struct)]
		private delegate void GetCpuInformation(ref CpuInformation cpuInformation);

		#endregion

		#region Memory Copy

		/// <summary>
		/// Delegate used for copy memory.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="src"></param>
		/// <param name="bytes"></param>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		[System.Security.SuppressUnmanagedCodeSecurity()]
		private delegate void MemoryCopyDelegate(void *dst, void* src, ulong bytes);

		/// <summary>
		/// Cached delegate for copy memory.
		/// </summary>
		private static MemoryCopyDelegate MemoryCopyPointer;

		/// <summary>
		/// Copy memory.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="src"></param>
		/// <param name="bytes"></param>
		public static unsafe void MemoryCopy(void* dst, void* src, ulong bytes)
		{
			MemoryCopyPointer(dst, src, bytes);
#if false
			byte *dstPointer = (byte *)dst, srcPointer = (byte *)src;
			byte *dstEnd = dstPointer + bytes;

			while (dstPointer < dstEnd) {
				*dstPointer = *srcPointer;
				dstPointer++; srcPointer++;
			}
#endif
		}

		/// <summary>
		/// Copy memory.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="src"></param>
		/// <param name="bytes"></param>
		public static void MemoryCopy(IntPtr dst, IntPtr src, ulong bytes)
		{
			MemoryCopy(dst.ToPointer(), src.ToPointer(), bytes);
		}

		/// <summary>
		/// Copy memory.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="src"></param>
		/// <param name="bytes"></param>
		public static void MemoryCopy(IntPtr dst, Array src, ulong bytes)
		{
			MemoryCopy(dst, src, 0, bytes);
		}

		/// <summary>
		/// Copy memory from array to unmanaged memory.
		/// </summary>
		/// <param name="dst">
		/// A <see cref="System.IntPtr"/> that specify the address of the destination unmanaged memory.
		/// </param>
		/// <param name="src">
		/// A <see cref="System.Array"/> that specify the source array object.
		/// </param>
		/// <param name="srcOffset">
		/// A <see cref="System.UInt32"/> that specify the offset to apply to memory copied from <paramref name="src"/>. This
		/// value is expressed in bytes.
		/// </param>
		/// <param name="bytes">
		/// A <see cref="System.UInt64"/> that specify the number of bytes to copy.
		/// </param>
		public static void MemoryCopy(IntPtr dst, Array src, uint srcOffset, ulong bytes)
		{
			if (dst == IntPtr.Zero)
				throw new ArgumentNullException("dst");
			if (src == null)
				throw new ArgumentNullException("src");
			if (src.Rank > 1)
				throw new ArgumentException("multidimensional array", "src");

			GCHandle srcArray = GCHandle.Alloc(src, GCHandleType.Pinned);
			try {
				IntPtr srcArrayPtr = new IntPtr(srcArray.AddrOfPinnedObject().ToInt64() + (long)srcOffset);

				// Copy from array to aligned buffer
				MemoryCopyPointer(dst.ToPointer(), srcArrayPtr.ToPointer(), bytes);
			} finally {
				srcArray.Free();
			}
		}

		/// <summary>
		/// Copy memory.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="src"></param>
		/// <param name="bytes"></param>
		public static void MemoryCopy(Array dst, IntPtr src, ulong bytes)
		{
			GCHandle dstArray = GCHandle.Alloc(dst, GCHandleType.Pinned);

			try {
				// Copy from array to aligned buffer
				MemoryCopyPointer(
					dstArray.AddrOfPinnedObject().ToPointer(), 
					src.ToPointer(),
					bytes
					);
			} finally {
				dstArray.Free();
			}
		}

		/// <summary>
		/// Copy memory.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="src"></param>
		/// <param name="bytes"></param>
		public static void MemoryCopy(Array dst, Array src, ulong bytes)
		{
			GCHandle dstArray = GCHandle.Alloc(dst, GCHandleType.Pinned);
			GCHandle srcArray = GCHandle.Alloc(src, GCHandleType.Pinned);

			try {
				// Copy from array to aligned buffer
				MemoryCopyPointer(
					dstArray.AddrOfPinnedObject().ToPointer(), 
					srcArray.AddrOfPinnedObject().ToPointer(),
					bytes
					);
			} finally {
				dstArray.Free();
				srcArray.Free();
			}
		}

		/// <summary>
		/// Ensure <see cref="MemoryCopy"/> functionality.
		/// </summary>
		private static void EnsureMemoryCopy()
		{
			if (MemoryCopyPointer == null) {
				IntPtr memoryCopyPtr;

				switch (Environment.OSVersion.Platform) {
					case PlatformID.Win32Windows:
					case PlatformID.Win32NT:
					case PlatformID.Win32S:
						memoryCopyPtr = GetProcAddress.GetAddress("msvcrt.dll", "memcpy");
						if (memoryCopyPtr != IntPtr.Zero) {
							MemoryCopyPointer = (MemoryCopyDelegate)Marshal.GetDelegateForFunctionPointer(memoryCopyPtr, typeof(MemoryCopyDelegate));
							return;
						}
						
						throw new NotSupportedException("no suitable memcpy support");
					case PlatformID.Unix:
						memoryCopyPtr = GetProcAddress.GetAddress("libc.so.6", "memcpy");
						if (memoryCopyPtr != IntPtr.Zero) {
							MemoryCopyPointer = (MemoryCopyDelegate)Marshal.GetDelegateForFunctionPointer(memoryCopyPtr, typeof(MemoryCopyDelegate));
							return;
						}
						
						throw new NotSupportedException("no suitable memcpy support");
					default:
						throw new NotSupportedException("no suitable memcpy support");
				}
			}
		}

		#endregion

		/// <summary>
		/// Delegate used for 
		/// </summary>
		/// <param name="result"></param>
		/// <param name="left"></param>
		/// <param name="right"></param>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		[System.Security.SuppressUnmanagedCodeSecurity()]
		internal delegate void Matrix4x4MultiplyOfDelegate(float* result, float* left, float* right);

		/// <summary>
		/// Delegate used for 
		/// </summary>
		/// <param name="result"></param>
		/// <param name="count"></param>
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		[System.Security.SuppressUnmanagedCodeSecurity()]
		internal delegate void Matrix4x4ConcatenateDelegate(float*[] result, uint count);

		/// <summary>
		/// Cached delegate for multiply two matrices.
		/// </summary>
		internal static Matrix4x4MultiplyOfDelegate Matrix4x4_Multiply_Matrix4x4;

		/// <summary>
		/// Cached delegate for multiply a chain of matrices.
		/// </summary>
		internal static Matrix4x4ConcatenateDelegate Matrix4x4_Concatenate;

		/// <summary>
		/// Load available SIMD extensions.
		/// </summary>
		public static void LoadSimdExtensions()
		{
			const string AssemblyPath = "OpenGL.Simd.dll";

			IntPtr getCpuInformationPtr = IntPtr.Zero;

			if (File.Exists(AssemblyPath) == false)
				return;

			try {
				getCpuInformationPtr = GetProcAddress.GetAddress(AssemblyPath, "GetCpuInformation");	
			} catch { /* Ignore exception, leave 'getCpuInformationPtr' equals to 'IntPtr.Zero' */ }

			// No CPU information? Ahi ahi ahi
			if (getCpuInformationPtr == IntPtr.Zero)
				return;

			GetCpuInformation getCpuInformation = (GetCpuInformation)Marshal.GetDelegateForFunctionPointer(getCpuInformationPtr, typeof(GetCpuInformation));
			CpuInformation cpuInfo = new CpuInformation();
			
			getCpuInformation(ref cpuInfo);

			if (cpuInfo.SimdSupport != SimdTechnology.None) {
				FieldInfo[] fields = typeof(Memory).GetFields(BindingFlags.Static | BindingFlags.NonPublic);

				foreach (FieldInfo fieldInfo in fields) {

					// Test for SSSE3 support
					if ((cpuInfo.SimdSupport & SimdTechnology.SSSE3) != 0) {
						string entryPoint = String.Format("{0}_{1}", fieldInfo.Name, "SSSE3");
						IntPtr address = GetProcAddress.GetAddress(AssemblyPath, entryPoint);

						if (address != IntPtr.Zero) {
							fieldInfo.SetValue(null, Marshal.GetDelegateForFunctionPointer(address, fieldInfo.FieldType));
							continue;
						}
					}

					// Test for SSE3 support
					if ((cpuInfo.SimdSupport & SimdTechnology.SSE3) != 0) {
						string entryPoint = String.Format("{0}_{1}", fieldInfo.Name, "SSE3");
						IntPtr address = GetProcAddress.GetAddress(AssemblyPath, entryPoint);

						if (address != IntPtr.Zero) {
							fieldInfo.SetValue(null, Marshal.GetDelegateForFunctionPointer(address, fieldInfo.FieldType));
							continue;
						}
					}

					// Test for SSE2 support
					if ((cpuInfo.SimdSupport & SimdTechnology.SSE2) != 0) {
						string entryPoint = String.Format("{0}_{1}", fieldInfo.Name, "SSE2");
						IntPtr address = GetProcAddress.GetAddress(AssemblyPath, entryPoint);

						if (address != IntPtr.Zero) {
							fieldInfo.SetValue(null, Marshal.GetDelegateForFunctionPointer(address, fieldInfo.FieldType));
							continue;
						}
					}

					// Test for SSE support
					if ((cpuInfo.SimdSupport & SimdTechnology.SSE) != 0) {
						string entryPoint = String.Format("{0}_{1}", fieldInfo.Name, "SSE");
						IntPtr address = GetProcAddress.GetAddress(AssemblyPath, entryPoint);

						if (address != IntPtr.Zero) {
							fieldInfo.SetValue(null, Marshal.GetDelegateForFunctionPointer(address, fieldInfo.FieldType));
							continue;
						}
					}

					// Test for MMX support
					if ((cpuInfo.SimdSupport & SimdTechnology.MMX) != 0) {
						string entryPoint = String.Format("{0}_{1}", fieldInfo.Name, "MMX");
						IntPtr address = GetProcAddress.GetAddress(AssemblyPath, entryPoint);

						if (address != IntPtr.Zero) {
							fieldInfo.SetValue(null, Marshal.GetDelegateForFunctionPointer(address, fieldInfo.FieldType));
							continue;
						}
					}

					// Reset field
					fieldInfo.SetValue(null, null);
				}
			}
		}
	}
}