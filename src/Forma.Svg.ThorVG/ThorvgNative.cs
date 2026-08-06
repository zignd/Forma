// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Forma
{
    internal enum ThorvgResult
    {
        Success = 0,
        InvalidArgument = 1,
        OutOfMemory = 2,
        ParseFailed = 3,
        RenderFailed = 4,
        EngineFailed = 5,
    }

    internal static partial class ThorvgNative
    {
        internal const string LibraryName = "forma_thorvg";
        internal const uint ExpectedAbiVersion = 1;

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_abi_version")]
        internal static partial uint AbiVersion();

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_version")]
        internal static partial IntPtr Version();

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_last_error")]
        internal static unsafe partial nuint LastError(byte* output, nuint outputSize);

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_initialize")]
        internal static partial ThorvgResult Initialize();

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_terminate")]
        internal static partial ThorvgResult Terminate();

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_document_create")]
        internal static unsafe partial ThorvgResult DocumentCreate(
            byte* svg,
            nuint svgSize,
            out IntPtr document);

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_document_destroy")]
        internal static partial void DocumentDestroy(IntPtr document);

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_document_size")]
        internal static partial ThorvgResult DocumentSize(
            ThorvgDocumentHandle document,
            out float width,
            out float height);

        [LibraryImport(LibraryName, EntryPoint = "forma_thorvg_document_rasterize")]
        internal static unsafe partial ThorvgResult DocumentRasterize(
            ThorvgDocumentHandle document,
            uint width,
            uint height,
            byte* rgba,
            nuint rgbaSize);

        internal static unsafe string GetLastError()
        {
            Span<byte> buffer = stackalloc byte[512];
            fixed (byte* output = buffer)
            {
                LastError(output, checked((nuint)buffer.Length));
            }
            var terminator = buffer.IndexOf((byte)0);
            if (terminator < 0) terminator = buffer.Length;
            return System.Text.Encoding.UTF8.GetString(buffer.Slice(0, terminator));
        }
    }

    internal sealed class ThorvgDocumentHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private ThorvgDocumentHandle() : base(true)
        {
        }

        internal ThorvgDocumentHandle(IntPtr handle) : base(true) => SetHandle(handle);

        protected override bool ReleaseHandle()
        {
            ThorvgNative.DocumentDestroy(handle);
            return true;
        }
    }
}
