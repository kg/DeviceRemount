using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DeviceRemount {
    [Flags]
    public enum DiGetClassFlags : uint {
        Zero = 0,
        DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
        DIGCF_PRESENT = 0x00000002,
        DIGCF_ALLCLASSES = 0x00000004,
        DIGCF_PROFILE = 0x00000008,
        DIGCF_DEVICEINTERFACE = 0x00000010,
    }

    [Flags]
    public enum NativeFileAccess : uint {
        GenericRead = 0x80000000,
        GenericWrite = 0x40000000
    }

    [Flags]
    public enum NativeFileFlags : uint {
        None = 0,
        WriteThrough = 0x80000000,
        Overlapped = 0x40000000,
        NoBuffering = 0x20000000,
        RandomAccess = 0x10000000,
        SequentialScan = 0x8000000,
        DeleteOnClose = 0x4000000,
        BackupSemantics = 0x2000000,
        PosixSemantics = 0x1000000,
        OpenReparsePoint = 0x200000,
        OpenNoRecall = 0x100000
    }

    public static class Native {
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiClassGuidsFromNameEx (
            string className, out Guid classGuidArray,
            UInt32 classGuidArraySize, out UInt32 requiredSize,
            string machineName, IntPtr reserved
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList (
            IntPtr deviceInfoSet
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevsEx (
            IntPtr pClassGuid, string enumerator,
            IntPtr hwndParent, DiGetClassFlags flags,            
            IntPtr existingDeviceInfoSet, string machineName,
            IntPtr reserved
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiGetDeviceInfoListDetail (
            IntPtr deviceInfoSet, ref SP_DEVINFO_LIST_DETAIL_DATA output
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet, UInt32 memberIndex,
            ref SP_DEVINFO_DATA output
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces (
           IntPtr deviceInfoSet,
           ref SP_DEVINFO_DATA devInfo,
           ref Guid interfaceClassGuid,
           UInt32 memberIndex,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail (
           IntPtr deviceInfoSet,
           ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
           ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           ref SP_DEVINFO_DATA deviceInfoData
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Device_ID (
            UInt32 dnDevInst,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
            char[] buffer,
            int bufferSize,
            int ulFlags
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile (
            string filename,
            NativeFileAccess access,
            FileShare share,
            IntPtr security,
            FileMode mode,
            NativeFileFlags flags,
            IntPtr template
        );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "DeviceIoControl")]
        private static extern bool _DeviceIoControl (
            IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize,
            IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped
        );
        
        public delegate void EnumerateDevicesFunc (DeviceInfoListHandle devs, ref SP_DEVINFO_DATA devInfo, string deviceId, object context);
        public delegate void EnumerateDeviceInterfacesFunc (Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, ref Native.SP_DEVICE_INTERFACE_DATA interfaceData, string devicePath, object context);

        public const int MAX_DEVICE_ID_LEN = 200;
        public const int MAX_PATH = 260;
        public const int SP_MAX_MACHINENAME_LENGTH = (MAX_PATH + 3);

        public const int ERROR_INSUFFICIENT_BUFFER = 0x7A;
        public const int ERROR_NO_MORE_ITEMS = 259;

        public const int IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.Struct)]
            public Guid ClassGuid;
            public UInt32 DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Size = 287)]
        public struct SP_DEVINFO_LIST_DETAIL_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.Struct)]
            public Guid ClassGuid;
            public IntPtr RemoteMachineHandle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = SP_MAX_MACHINENAME_LENGTH)]
            public char[] RemoteMachineName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.Struct)]
            public Guid InterfaceClassGuid;
            public UInt32 Flags;
            public UIntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class STORAGE_DEVICE_NUMBER {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        public class DeviceInfoListHandle : SafeHandleZeroOrMinusOneIsInvalid {
            public DeviceInfoListHandle (IntPtr handle, bool ownsHandle)
                : base (ownsHandle) {
                SetHandle(handle);
            }

            protected override bool ReleaseHandle() {
 	            return SetupDiDestroyDeviceInfoList(base.handle);
            }
        }

        public static string GetDeviceId (ref SP_DEVINFO_DATA devInfo) {
            var buffer = new char[2048];
            var result = Native.CM_Get_Device_ID(
                devInfo.DevInst, buffer, buffer.Length, 0
            );

            if (result != 0)
                throw new Win32Exception(result);

            return new String(
                buffer, 0, 
                Array.FindIndex(buffer, (ch) => ch == 0)
            );
        }

        public static unsafe void DeviceIoControl<TOut> (SafeFileHandle file, UInt32 control, TOut outputBuffer)
            where TOut: class 
        {
            var pinned = GCHandle.Alloc(outputBuffer, GCHandleType.Pinned);
            try {
                uint bytesReturned;

                if (!_DeviceIoControl(
                    file.DangerousGetHandle(), control, IntPtr.Zero, 0,
                    pinned.AddrOfPinnedObject(), (UInt32)Marshal.SizeOf(outputBuffer),
                    out bytesReturned, IntPtr.Zero
                )) {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 0)
                        throw new Win32Exception(error);
                }
            } finally {
                pinned.Free();
            }
        }

        public static unsafe void EnumerateDeviceInterfaces (DeviceInfoListHandle devs, ref SP_DEVINFO_DATA devInfo, ref Guid interfaceGuid, EnumerateDeviceInterfacesFunc callback, object context) {
            try {
                var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(deviceInterfaceData.GetType());

                // This structure is strange :(
                var deviceInterfaceDetailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                deviceInterfaceDetailData.cbSize = 6;

                for (
                    UInt32 memberIndex = 0;
                    SetupDiEnumDeviceInterfaces(
                        devs.DangerousGetHandle(), ref devInfo,
                        ref interfaceGuid, memberIndex,
                        ref deviceInterfaceData
                    );
                    memberIndex++
                ) {
                    uint requiredSize;

                    SetupDiGetDeviceInterfaceDetail(
                        devs.DangerousGetHandle(), ref deviceInterfaceData, 
                        ref deviceInterfaceDetailData, 
                        (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA)),
                        out requiredSize, ref devInfo
                    );
                    var lastError = Marshal.GetLastWin32Error();
                    if (lastError != 0)
                        throw new Win32Exception(lastError);

                    callback(
                        devs, ref devInfo,
                        ref deviceInterfaceData, deviceInterfaceDetailData.DevicePath, 
                        context
                    );
                }

                {
                    var lastError = Marshal.GetLastWin32Error();
                    if ((lastError != 0) && (lastError != ERROR_NO_MORE_ITEMS))
                        throw new Win32Exception(lastError);
                }
            } finally {
            }
        }

        public static unsafe void EnumerateDevices (string machine, DiGetClassFlags flags, string[] deviceClassNames, EnumerateDevicesFunc callback, object context) {
            DeviceInfoListHandle devs = null;
            var devInfo = new SP_DEVINFO_DATA();
            var devInfoListDetail = new SP_DEVINFO_LIST_DETAIL_DATA();
            Guid[] deviceClassGuids;

            try {
                int numClasses = 0;

                if (deviceClassNames != null) {
                    deviceClassGuids = new Guid[deviceClassNames.Length];

                    foreach (var className in deviceClassNames) {
                        UInt32 numResults;

                        if (
                            !SetupDiClassGuidsFromNameEx(
                                className, out deviceClassGuids[numClasses],
                                1, out numResults,
                                machine, IntPtr.Zero
                            ) && (Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
                        ) {
                            throw new Exception("Unable to resolve class name '" + className + "'");
                        }

                        numClasses += 1;
                    }

                    var newClassGuids = new Guid[numClasses];
                    Array.Copy(deviceClassGuids, newClassGuids, numClasses);
                    deviceClassGuids = newClassGuids;
                } else {
                    deviceClassGuids = new Guid[0];
                }

                if (deviceClassGuids.Length > 0) {
                    for (int i = 0; i < deviceClassGuids.Length; i++) {
                        fixed (Guid* pGuid = &(deviceClassGuids[i])) {
                            var existingPtr = IntPtr.Zero;
                            if (devs != null)
                                existingPtr = devs.DangerousGetHandle();

                            var result = SetupDiGetClassDevsEx(
                                new IntPtr(pGuid), null, IntPtr.Zero, flags,
                                existingPtr, machine, IntPtr.Zero
                            );

                            var lastError = Marshal.GetLastWin32Error();
                            if (lastError != 0)
                                throw new Win32Exception(lastError);

                            if (devs == null)
                                devs = new DeviceInfoListHandle(result, true);
                        }
                    }
                } else {
                    devs = new DeviceInfoListHandle(
                        SetupDiGetClassDevsEx(
                            IntPtr.Zero, null, IntPtr.Zero,
                            flags | DiGetClassFlags.DIGCF_ALLCLASSES,
                            IntPtr.Zero, machine, IntPtr.Zero
                        ), true
                    );

                    var lastError = Marshal.GetLastWin32Error();
                    if (lastError != 0)
                        throw new Win32Exception(lastError);
                }

                if (devs.IsInvalid)
                    throw new Exception("Failed to create device info list");

                devInfoListDetail.cbSize = (UInt32)Marshal.SizeOf(devInfoListDetail.GetType());
                if (!SetupDiGetDeviceInfoListDetail(devs.DangerousGetHandle(), ref devInfoListDetail)) {
                    var lastError = Marshal.GetLastWin32Error();
                    if (lastError != 0)
                        throw new Win32Exception(lastError);

                    return;
                }

                devInfo.cbSize = (UInt32)Marshal.SizeOf(devInfo.GetType());
                for (UInt32 devIndex = 0; SetupDiEnumDeviceInfo(devs.DangerousGetHandle(), devIndex, ref devInfo); devIndex++)
                    callback(devs, ref devInfo, GetDeviceId(ref devInfo), context);

                {
                    var lastError = Marshal.GetLastWin32Error();
                    if ((lastError != 0) && (lastError != ERROR_NO_MORE_ITEMS))
                        throw new Win32Exception(lastError);
                }

            } finally {
                if (devs != null && !devs.IsClosed)
                    devs.Close();
            }        
        }
    }
}
