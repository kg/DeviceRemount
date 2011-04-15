using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceRemount {
    public static class Program {
        public static unsafe void EnumInterfacesCallback (Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, ref Native.SP_DEVICE_INTERFACE_DATA interfaceData, string devicePath, object context) {
            Console.WriteLine("Interface classGuid = {0}, flags = {1}, path = {2}", interfaceData.InterfaceClassGuid, interfaceData.Flags, devicePath);

            try {
                var handle = Native.CreateFile(
                    devicePath, NativeFileAccess.GenericRead, System.IO.FileShare.ReadWrite,
                    IntPtr.Zero, System.IO.FileMode.Open, NativeFileFlags.None, IntPtr.Zero
                );

                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                    throw new Win32Exception(error);
                else if (handle.IsInvalid)
                    throw new Exception("Unable to open handle to volume '" + devicePath + "'");

                try {
                    var devNumber = new Native.STORAGE_DEVICE_NUMBER();
                    Native.DeviceIoControl(
                        handle, Native.IOCTL_STORAGE_GET_DEVICE_NUMBER, devNumber
                    );
                    Console.WriteLine("    Device {0}, partition {1}", devNumber.DeviceNumber, devNumber.PartitionNumber);

                } finally {
                    handle.Close();
                }
            } catch {
                Console.WriteLine("    Volume inaccessible");
            }
        }

        public static unsafe void EnumDevicesCallback (Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, string deviceId, object context) {
            Console.WriteLine("Device    classGuid = {0}, instanceId = {1}, deviceId = {2}", devInfo.ClassGuid, devInfo.DevInst, deviceId);

            var GUID_DEVINTERFACE_VOLUME = Guid.Parse("{53F5630D-B6BF-11D0-94F2-00A0C91EFB8B}");
            Native.EnumerateDeviceInterfaces(
                devs, ref devInfo, ref GUID_DEVINTERFACE_VOLUME, EnumInterfacesCallback, context
            );
        }

        public static void Main (string[] driveLetters) {
            var physicalDriveIds = new HashSet<UInt32>();

            foreach (var driveLetter in driveLetters) {
                var path = String.Format(@"\\.\{0}", driveLetter.ToUpper());
                if (!path.EndsWith(":"))
                    path += ":";

                var handle = Native.CreateFile(
                    path, NativeFileAccess.GenericRead, System.IO.FileShare.ReadWrite,
                    IntPtr.Zero, System.IO.FileMode.Open, NativeFileFlags.None, IntPtr.Zero
                );

                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                    throw new Win32Exception(error);
                else if (handle.IsInvalid)
                    throw new Exception("Unable to open handle to drive '" + driveLetter + "'");

                try {
                    var devNumber = new Native.STORAGE_DEVICE_NUMBER();
                    Native.DeviceIoControl(
                        handle, Native.IOCTL_STORAGE_GET_DEVICE_NUMBER, devNumber
                    );
                    Console.WriteLine("{0} is device {1}, partition {2}", driveLetter, devNumber.DeviceNumber, devNumber.PartitionNumber);

                } finally {
                    handle.Close();
                }
            }

            Native.EnumerateDevices(
                null, DiGetClassFlags.DIGCF_DEVICEINTERFACE,
                null, EnumDevicesCallback, null
            );
        }
    }
}
