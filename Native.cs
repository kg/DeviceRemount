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

    public enum DiClassInstallState : uint {
        DICS_ENABLE	= 1,
        DICS_DISABLE = 2,
        DICS_PROPCHANGE = 3,
        DICS_START = 4,
        DICS_STOP = 5
    }

    public enum DiClassInstallFunction : uint {
        DIF_SELECTDEVICE = 1,
        DIF_INSTALLDEVICE = 2,
        DIF_ASSIGNRESOURCES = 3,
        DIF_PROPERTIES = 4,
        DIF_REMOVE = 5,
        DIF_FIRSTTIMESETUP = 6,
        DIF_FOUNDDEVICE = 7,
        DIF_SELECTCLASSDRIVERS = 8,
        DIF_VALIDATECLASSDRIVERS = 9,
        DIF_INSTALLCLASSDRIVERS = 10,
        DIF_CALCDISKSPACE = 11,
        DIF_DESTROYPRIVATEDATA = 12,
        DIF_VALIDATEDRIVER = 13,
        DIF_MOVEDEVICE = 14,
        DIF_DETECT = 15,
        DIF_INSTALLWIZARD = 16,
        DIF_DESTROYWIZARDDATA = 17,
        DIF_PROPERTYCHANGE = 18,
        DIF_ENABLECLASS = 19,
        DIF_DETECTVERIFY = 20,
        DIF_INSTALLDEVICEFILES = 21,
        DIF_UNREMOVE = 22,
        DIF_SELECTBESTCOMPATDRV = 23,
        DIF_ALLOW_INSTALL = 24,
        DIF_REGISTERDEVICE = 25,
        DIF_NEWDEVICEWIZARD_PRESELECT = 26,
        DIF_NEWDEVICEWIZARD_SELECT = 27,
        DIF_NEWDEVICEWIZARD_PREANALYZE = 28,
        DIF_NEWDEVICEWIZARD_POSTANALYZE = 29,
        DIF_NEWDEVICEWIZARD_FINISHINSTALL = 30,
        DIF_UNUSED1 = 31,
        DIF_INSTALLINTERFACES = 32,
        DIF_DETECTCANCEL = 33,
        DIF_REGISTER_COINSTALLERS = 34,
        DIF_ADDPROPERTYPAGE_ADVANCED = 35,
        DIF_ADDPROPERTYPAGE_BASIC = 36,
        DIF_RESERVED1 = 37,
        DIF_TROUBLESHOOTER = 38,
        DIF_POWERMESSAGEWAKE = 39
    }

    [Flags]
    public enum DiClassInstallScope : uint {
        DICS_FLAG_GLOBAL = 1,
        DICS_FLAG_CONFIGSPECIFIC = 2,
        DICS_FLAG_CONFIGGENERAL = 4,
    }

    public static class Native {
        public delegate void EnumerateDevicesFunc<TContext> (
            DeviceInfoListHandle devs, ref SP_DEVINFO_DATA devInfo,
            string deviceId, TContext context
        ) where TContext : class;

        public delegate void EnumerateDeviceInterfacesFunc<TContext> (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo,
            ref Native.SP_DEVICE_INTERFACE_DATA interfaceData, string devicePath,
            TContext context
        ) where TContext : class;

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
           IntPtr deviceInterfaceDetailData,
           UInt32 deviceInterfaceDetailDataSize,
           out UInt32 requiredSize,
           ref SP_DEVINFO_DATA deviceInfoData
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetupDiSetClassInstallParamsW")]
        public static extern bool _SetupDiSetClassInstallParams (
            IntPtr deviceInfoSet, 
            ref SP_DEVINFO_DATA deviceInfoData,
            IntPtr classInstallParams, 
            UInt32 classInstallParamsSize
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiCallClassInstaller (
            DiClassInstallFunction installFunction, 
            IntPtr deviceInfoSet,
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

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int CM_Get_Parent (
            out UInt32 parent, UInt32 child, UInt32 flags
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

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInstallParams (
            IntPtr hDevInfo, 
            ref SP_DEVINFO_DATA DeviceInfoData, 
            ref SP_DEVINSTALL_PARAMS DeviceInstallParams
        );

        public const int MAX_DEVICE_ID_LEN = 200;
        public const int MAX_PATH = 260;
        public const int SP_MAX_MACHINENAME_LENGTH = 263;

        public const uint ERROR_IN_WOW64 = 0xe0000235;
        public const int ERROR_INSUFFICIENT_BUFFER = 0x7A;
        public const int ERROR_NO_MORE_ITEMS = 259;
        public const int ERROR_INCORRECT_FUNCTION = 1;

        public const int IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080;
        public const int IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct SP_DEVINFO_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.Struct)]
            public Guid ClassGuid;
            public UInt32 DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Ansi)]
        public struct SP_DEVINFO_LIST_DETAIL_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.Struct)]
            public Guid ClassGuid;
            public IntPtr RemoteMachineHandle;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = SP_MAX_MACHINENAME_LENGTH)]
            public char[] RemoteMachineName;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct SP_DEVICE_INTERFACE_DATA {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.Struct)]
            public Guid InterfaceClassGuid;
            public UInt32 Flags;
            public UIntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public class STORAGE_DEVICE_NUMBER {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct SP_CLASSINSTALL_HEADER {
            public UInt32 cbSize;
            public DiClassInstallFunction InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public class SP_PROPCHANGE_PARAMS {
            public SP_CLASSINSTALL_HEADER Header;
            public DiClassInstallState StateChange;
            public DiClassInstallScope Scope;
            public UInt32 HwProfile;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public class DISK_EXTENT {
            public UInt32 DiskNumber;
            public Int64 StartingOffset;
            public Int64 ExtentLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public class VOLUME_DISK_EXTENTS_HEADER {
            public UInt32 NumberOfDiskExtents;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct SP_DEVINSTALL_PARAMS {
            public UInt32 cbSize;
            public UInt32 Flags;
            public UInt32 FlagsEx;
            public IntPtr hwndParent;
            public IntPtr InstallMsgHandler;
            public IntPtr InstallMsgHandlerContext;
            public IntPtr FileQueue;
            public IntPtr ClassInstallReserved;
            public UIntPtr Reserved;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string DriverPath;
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

        public static unsafe void DeviceIoControl<TOut> 
            (SafeFileHandle file, UInt32 control, TOut outputBuffer)
            where TOut: class 
        {
            var pinned = GCHandle.Alloc(outputBuffer, GCHandleType.Pinned);
            try {
                DeviceIoControl(
                    file, control,
                    pinned.AddrOfPinnedObject(), 
                    (UInt32)Marshal.SizeOf(outputBuffer)
                );
            } finally {
                pinned.Free();
            }
        }

        public static unsafe void DeviceIoControl (SafeFileHandle file, UInt32 control, IntPtr pBuffer, UInt32 bufferSize) {
            uint bytesReturned;

            if (!_DeviceIoControl(
                file.DangerousGetHandle(), control, IntPtr.Zero, 0,
                pBuffer, bufferSize, out bytesReturned, IntPtr.Zero
            )) {
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                    throw new Win32Exception(error);
            }
        }

        public static unsafe void SetupDiSetClassInstallParams<TParams> 
            (DeviceInfoListHandle deviceList, ref SP_DEVINFO_DATA deviceInfoData, TParams parms) 
            where TParams : class
        {
            var pinned = GCHandle.Alloc(parms, GCHandleType.Pinned);
            try {
                if (!_SetupDiSetClassInstallParams(
                    deviceList.DangerousGetHandle(), ref deviceInfoData,
                    pinned.AddrOfPinnedObject(), (UInt32)Marshal.SizeOf(parms)
                )) {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 0)
                        throw new Win32Exception(error);
                }
            } finally {
                pinned.Free();
            }
        }

        public static unsafe void EnumerateDeviceInterfaces<TContext> (
            DeviceInfoListHandle devs, ref SP_DEVINFO_DATA devInfo, 
            ref Guid interfaceGuid, EnumerateDeviceInterfacesFunc<TContext> callback, 
            TContext context
        ) where TContext : class {

            try {
                var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(deviceInterfaceData.GetType());

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

                    // We can't use a struct for this because the CLR's interpretation of
                    //  alignment is different from Win32's on x64 :(
                    var buffer = new byte[1024 + 4];
                    fixed (byte * pBuffer = buffer) {
                        *(UInt32 *)pBuffer = 8;

                        SetupDiGetDeviceInterfaceDetail(
                            devs.DangerousGetHandle(), ref deviceInterfaceData,
                            new IntPtr(pBuffer), (UInt32)buffer.Length,
                            out requiredSize, ref devInfo
                        );
                        var lastError = Marshal.GetLastWin32Error();
                        if (lastError != 0)
                            throw new Win32Exception(lastError);

                        var devicePath = new String(
                            (char *)(pBuffer + 4)
                        );

                        callback(
                            devs, ref devInfo,
                            ref deviceInterfaceData, devicePath,
                            context
                        );
                    }
                }

                {
                    var lastError = Marshal.GetLastWin32Error();
                    if ((lastError != 0) && (lastError != ERROR_NO_MORE_ITEMS))
                        throw new Win32Exception(lastError);
                }
            } finally {
            }
        }

        public static unsafe void EnumerateDevices<TContext> (
            string machine, DiGetClassFlags flags,
            string[] deviceClassNames, EnumerateDevicesFunc<TContext> callback, 
            TContext context
        ) where TContext : class {

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

        public static void ChangeDeviceEnabledState (
            DeviceInfoListHandle deviceInfoList, ref SP_DEVINFO_DATA deviceInfoData,
            DiClassInstallState state, DiClassInstallScope scope, 
            DiClassInstallFunction installFunction
        ) {
            var pcp = new SP_PROPCHANGE_PARAMS();

            pcp.Header.cbSize = (uint)Marshal.SizeOf(pcp.Header.GetType());
            pcp.Header.InstallFunction = installFunction;
            pcp.StateChange = state;
            pcp.Scope = scope;
            pcp.HwProfile = 0;

            SetupDiSetClassInstallParams(deviceInfoList, ref deviceInfoData, pcp);

            if (!SetupDiCallClassInstaller(
                installFunction, deviceInfoList.DangerousGetHandle(), ref deviceInfoData
            )) {
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                    throw new Win32Exception(error);
                else
                    throw new Exception("Failed to change device enabled state");
            }
        }
    }
}
