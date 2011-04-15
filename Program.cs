using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceRemount {
    public struct DeviceNumber {
        public readonly int Device;
        public readonly int Partition;
        public readonly int Type;

        public DeviceNumber (int device, int partition, int type) {
            Device = device;
            Partition = partition;
            Type = type;
        }

        public override int GetHashCode () {
            return Device.GetHashCode() ^ Partition.GetHashCode() ^ Type.GetHashCode();
        }

        public bool Equals (DeviceNumber rhs) {
            return (Device == rhs.Device) && 
                (Partition == rhs.Partition) &&
                (Type == rhs.Type);
        }

        public override bool Equals (object obj) {
            if (obj is DeviceNumber) {
                return Equals((DeviceNumber)obj);
            } else {
                return base.Equals(obj);
            }
        }

        public override string ToString () {
            return String.Format("<{0}: {1}, {2}>", Type, Device, Partition);
        }
    }

    public static class Program {
        public static unsafe bool GetDeviceNumber (string path, out DeviceNumber result, out Exception failureReason) {
            result = new DeviceNumber();
            failureReason = null;

            var handle = Native.CreateFile(
                path, NativeFileAccess.GenericRead, System.IO.FileShare.ReadWrite,
                IntPtr.Zero, System.IO.FileMode.Open, NativeFileFlags.None, IntPtr.Zero
            );

            var error = Marshal.GetLastWin32Error();
            if (error != 0) {
                failureReason = new Win32Exception(error);
                return false;
            } else if (handle.IsInvalid) {
                return false;
            }

            try {
                var devNumber = new Native.STORAGE_DEVICE_NUMBER();
                Native.DeviceIoControl(
                    handle, Native.IOCTL_STORAGE_GET_DEVICE_NUMBER, devNumber
                );

                result = new DeviceNumber(devNumber.DeviceNumber, devNumber.PartitionNumber, devNumber.DeviceType);
                return true;
            } catch (Win32Exception w32ex) {
                if (w32ex.NativeErrorCode != Native.ERROR_INCORRECT_FUNCTION) {
                    failureReason = w32ex;
                    return false;
                }

                // STORAGE_GET_DEVICE_NUMBER will fail for dynamic disks, so we 
                //  try VOLUME_GET_VOLUME_DISK_EXTENTS instead
                int extentSize = Marshal.SizeOf(typeof(Native.DISK_EXTENT));
                int headerSize = Marshal.SizeOf(typeof(Native.VOLUME_DISK_EXTENTS_HEADER));

                const int bufferSize = 64;
                var buffer = new byte[ headerSize + (extentSize * bufferSize) ];

                var header = new Native.VOLUME_DISK_EXTENTS_HEADER {
                    NumberOfDiskExtents = bufferSize
                };

                fixed (byte* pBuffer = buffer) {
                    Marshal.StructureToPtr(header, new IntPtr(pBuffer), false);

                    Native.DeviceIoControl(
                        handle, Native.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, 
                        new IntPtr(pBuffer), (UInt32)buffer.Length
                    );

                    Marshal.PtrToStructure(new IntPtr(pBuffer), header);

                    var pExtents = pBuffer + headerSize;
                    var extent = new Native.DISK_EXTENT();

                    if (header.NumberOfDiskExtents > 1)
                        throw new NotImplementedException("Volumes spread across multiple physical disks are not supported");

                    Marshal.PtrToStructure(new IntPtr(pExtents), extent);

                    result = new DeviceNumber(-1024, (int)extent.DiskNumber, -1024);
                    return true;
                }
            } catch (Exception ex) {
                failureReason = ex;
                return false;
            } finally {
                handle.Close();
            }
        }

        public class EnableDisableParameters {
            public Guid ClassGuid;
            public UInt32 DevInst;
            public string DriveLetter;
        }

        public static void DoEnableDisable (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo,
            string deviceId, EnableDisableParameters parms
        ) {
            if (devInfo.ClassGuid != parms.ClassGuid)
                return;
            if (devInfo.DevInst != parms.DevInst)
                return;

            /*
            var installParams = new Native.SP_DEVINSTALL_PARAMS();
            installParams.cbSize = (UInt32)Marshal.SizeOf(installParams.GetType());

            if (!Native.SetupDiGetDeviceInstallParams(
                devs.DangerousGetHandle(), ref devInfo, ref installParams
            )) {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
             */

            Console.Write("Resetting volume {0}... ", parms.DriveLetter);
            try {
                Native.ChangeDeviceEnabledState(
                    devs, ref devInfo,
                    DiClassInstallState.DICS_PROPCHANGE,
                    DiClassInstallScope.DICS_FLAG_GLOBAL,
                    DiClassInstallFunction.DIF_PROPERTYCHANGE
                );

                Native.ChangeDeviceEnabledState(
                    devs, ref devInfo,
                    DiClassInstallState.DICS_PROPCHANGE,
                    DiClassInstallScope.DICS_FLAG_CONFIGSPECIFIC,
                    DiClassInstallFunction.DIF_PROPERTYCHANGE
                );

                Console.WriteLine("ok.");
            } catch (Exception ex) {
                Console.WriteLine("failed.");
            }
        }

        public static void EnumInterfacesCallback (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, 
            ref Native.SP_DEVICE_INTERFACE_DATA interfaceData, 
            string devicePath, Dictionary<DeviceNumber, string> searchDevices
        ) {

            DeviceNumber devNumber;
            Exception failureReason;
            if (!GetDeviceNumber(devicePath, out devNumber, out failureReason))
                return;

            string driveLetter;
            if (!searchDevices.TryGetValue(devNumber, out driveLetter))
                return;

            searchDevices.Remove(devNumber);

            var parms = new EnableDisableParameters {
                ClassGuid = devInfo.ClassGuid,
                DevInst = devInfo.DevInst,
                DriveLetter = driveLetter
            };

            Native.EnumerateDevices(
                null, DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_ALLCLASSES, 
                null, DoEnableDisable, parms
            );
        }

        public static void EnumDevicesCallback (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, 
            string deviceId, Dictionary<DeviceNumber, string> searchDevices
        ) {

            var GUID_DEVINTERFACE_VOLUME = Guid.Parse("{53F5630D-B6BF-11D0-94F2-00A0C91EFB8B}");

            Native.EnumerateDeviceInterfaces(
                devs, ref devInfo, ref GUID_DEVINTERFACE_VOLUME, EnumInterfacesCallback, searchDevices
            );
        }

        public static void Main (string[] driveLetters) {
            var physicalDriveIds = new Dictionary<DeviceNumber, string>();

            foreach (var driveLetter in driveLetters) {
                var devicePath = String.Format(@"\\.\{0}", driveLetter.ToUpper());
                if (!devicePath.EndsWith(":"))
                    devicePath += ":";

                Exception failureReason;
                DeviceNumber devNumber;
                if (GetDeviceNumber(devicePath, out devNumber, out failureReason)) {
                    physicalDriveIds.Add(devNumber, driveLetter);
                } else {
                    throw new Exception("Failed to access drive " + driveLetter, failureReason);
                }
            }

            Native.EnumerateDevices(
                null, DiGetClassFlags.DIGCF_DEVICEINTERFACE,
                null, EnumDevicesCallback, physicalDriveIds
            );
        }
    }
}
