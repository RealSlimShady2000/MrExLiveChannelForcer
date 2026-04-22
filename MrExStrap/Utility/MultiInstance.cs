// Adapted approach from robloxmanager by sasha / centerepic (MIT)
// https://gitlab.com/centerepic/robloxmanager
// Original behavior: after Roblox launches, enumerate its handle table and close
// the "ROBLOX_singletonEvent" event so the next launch can create its own. This
// lets multiple Roblox clients run at the same time. Same-user-only; no admin
// required. This is a C# port of that idea using NtQuery*/DuplicateHandle.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MrExStrap.Utility
{
    public static class MultiInstance
    {
        private const string LOG_IDENT = "MultiInstance";
        private const string SingletonEventSuffix = "ROBLOX_singletonEvent";

        // How long to wait for Roblox to actually create its singleton event before
        // we start hunting for handles. If we run too early, the event doesn't exist yet
        // and the next Roblox launch attempt wins the race and blocks ours.
        private static readonly TimeSpan EventCreationGrace = TimeSpan.FromSeconds(4);

        // Kick off the close in the background so we don't block launch.
        // Runs once per launch, safe to call when multi-instance is off (no-op path in caller).
        public static void ScheduleSingletonClose(int robloxPid)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(EventCreationGrace);
                    CloseSingletonEventsForPid(robloxPid);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT + "::Schedule", ex);
                }
            });
        }

        private static void CloseSingletonEventsForPid(int pid)
        {
            IntPtr srcProcess = OpenProcess(PROCESS_DUP_HANDLE | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (srcProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                App.Logger.WriteLine(LOG_IDENT, $"OpenProcess({pid}) failed: {err}. Skipping.");
                return;
            }

            try
            {
                var handles = EnumerateProcessHandles(pid);
                if (handles.Count == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"No handles enumerated for PID {pid}.");
                    return;
                }

                int closed = 0;
                foreach (var entry in handles)
                {
                    if (TryCloseIfSingletonEvent(srcProcess, entry))
                        closed++;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Closed {closed} singleton handle(s) in PID {pid}.");
            }
            finally
            {
                CloseHandle(srcProcess);
            }
        }

        private static bool TryCloseIfSingletonEvent(IntPtr srcProcess, SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX entry)
        {
            // Duplicate the target handle into our process so we can query it safely.
            if (!DuplicateHandle(srcProcess, entry.HandleValue, GetCurrentProcess(),
                out IntPtr dupHandle, 0, false, DUPLICATE_SAME_ACCESS))
            {
                return false;
            }

            try
            {
                // Type first — cheap, and filters out 99% of handles without risking a hang
                // on a name query for a synchronous pipe.
                string? typeName = QueryObjectType(dupHandle);
                if (typeName != "Event")
                    return false;

                string? objectName = QueryObjectName(dupHandle);
                if (string.IsNullOrEmpty(objectName))
                    return false;

                if (!objectName.EndsWith(SingletonEventSuffix, StringComparison.Ordinal))
                    return false;

                // Close in source — dup with DUPLICATE_CLOSE_SOURCE and discard.
                if (!DuplicateHandle(srcProcess, entry.HandleValue, GetCurrentProcess(),
                    out IntPtr closer, 0, false, DUPLICATE_CLOSE_SOURCE))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"DuplicateHandle(close) failed for {objectName}");
                    return false;
                }

                CloseHandle(closer);
                App.Logger.WriteLine(LOG_IDENT, $"Closed {objectName}");
                return true;
            }
            finally
            {
                CloseHandle(dupHandle);
            }
        }

        private static List<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX> EnumerateProcessHandles(int pid)
        {
            var result = new List<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();

            int length = 0x10000;
            IntPtr buffer = Marshal.AllocHGlobal(length);

            try
            {
                uint status;
                while (true)
                {
                    status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, length, out int returnLength);
                    if (status == STATUS_INFO_LENGTH_MISMATCH)
                    {
                        Marshal.FreeHGlobal(buffer);
                        length = Math.Max(length * 2, returnLength);
                        buffer = Marshal.AllocHGlobal(length);
                        continue;
                    }
                    break;
                }

                if (status != 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"NtQuerySystemInformation failed: 0x{status:X}");
                    return result;
                }

                long count = Marshal.ReadIntPtr(buffer).ToInt64();
                IntPtr arrayStart = IntPtr.Add(buffer, IntPtr.Size * 2);
                int entrySize = Marshal.SizeOf<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();

                IntPtr pidPtr = new IntPtr(pid);
                for (long i = 0; i < count; i++)
                {
                    var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(
                        IntPtr.Add(arrayStart, (int)(i * entrySize)));
                    if (entry.UniqueProcessId == pidPtr)
                        result.Add(entry);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Enumerate", ex);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return result;
        }

        // NtQueryObject can block on specific handle types. Run on a timed thread and
        // bail if it doesn't answer quickly. In practice Event handles answer in microseconds.
        private static string? QueryObjectType(IntPtr handle) => QueryObjectWithTimeout(handle, ObjectTypeInformation);
        private static string? QueryObjectName(IntPtr handle) => QueryObjectWithTimeout(handle, ObjectNameInformation);

        private static string? QueryObjectWithTimeout(IntPtr handle, int infoClass)
        {
            string? result = null;
            var thread = new Thread(() =>
            {
                try { result = QueryObjectInner(handle, infoClass); }
                catch { /* swallow, result stays null */ }
            })
            { IsBackground = true };

            thread.Start();
            if (!thread.Join(TimeSpan.FromMilliseconds(300)))
            {
                // Thread hung on a slow handle — abandon it, it's a background thread.
                return null;
            }
            return result;
        }

        private static string? QueryObjectInner(IntPtr handle, int infoClass)
        {
            int length = 0x1000;
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                uint status = NtQueryObject(handle, infoClass, buffer, length, out int returnLength);
                if (status == STATUS_INFO_LENGTH_MISMATCH)
                {
                    Marshal.FreeHGlobal(buffer);
                    length = Math.Max(returnLength, length * 2);
                    buffer = Marshal.AllocHGlobal(length);
                    status = NtQueryObject(handle, infoClass, buffer, length, out _);
                }
                if (status != 0)
                    return null;

                // Both ObjectType and ObjectName info start with a UNICODE_STRING at offset 0
                // (for ObjectName it's literally the struct; for ObjectType it's the TypeName field).
                var us = Marshal.PtrToStructure<UNICODE_STRING>(buffer);
                if (us.Buffer == IntPtr.Zero || us.Length == 0)
                    return null;

                return Marshal.PtrToStringUni(us.Buffer, us.Length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        #region P/Invoke

        private const uint PROCESS_DUP_HANDLE = 0x0040;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint DUPLICATE_CLOSE_SOURCE = 0x1;
        private const uint DUPLICATE_SAME_ACCESS = 0x2;
        private const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
        private const int SystemExtendedHandleInformation = 64;
        private const int ObjectNameInformation = 1;
        private const int ObjectTypeInformation = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
        {
            public IntPtr Object;
            public IntPtr UniqueProcessId;
            public IntPtr HandleValue;
            public uint GrantedAccess;
            public ushort CreatorBackTraceIndex;
            public ushort ObjectTypeIndex;
            public uint HandleAttributes;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [DllImport("ntdll.dll")]
        private static extern uint NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern uint NtQueryObject(IntPtr handle, int infoClass, IntPtr buffer, int length, out int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess,
            out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        #endregion
    }
}
