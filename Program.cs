using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

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
    }

    public static class Program {
        static Guid DEVINTERFACE_VOLUME = Guid.Parse("{53F5630D-B6BF-11D0-94F2-00A0C91EFB8B}");

        public class Parameters {
            public readonly HashSet<string> Options = new HashSet<string>();
            public readonly Dictionary<DeviceNumber, string> DriveLetters = new Dictionary<DeviceNumber, string>();
            public readonly HashSet<uint> InstanceIDs = new HashSet<uint>();
        }

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
                // STORAGE_GET_DEVICE_NUMBER will fail for dynamic disks with ERROR_INCORRECT_FUNCTION,
                //  so we try VOLUME_GET_VOLUME_DISK_EXTENTS instead

                if (w32ex.NativeErrorCode != Native.ERROR_INCORRECT_FUNCTION) {
                    failureReason = w32ex;
                    return false;
                }

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
            public Parameters AppParams;
        }

        public static void DoEnableDisable (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo,
            string deviceId, EnableDisableParameters parms
        ) {
            if (devInfo.ClassGuid != parms.ClassGuid)
                return;
            if (devInfo.DevInst != parms.DevInst)
                return;

            var options = parms.AppParams.Options;

            if ((options.Count == 0) || options.Contains("disable")) {
                Console.WriteLine(
                    "// Physical ID for volume {0} is #{1}. You can use this ID to re-enable the volume.",
                    parms.DriveLetter, devInfo.DevInst
                );

                Console.Write("Disabling volume {0}... ", parms.DriveLetter);
                try {
                    Native.ChangeDeviceEnabledState(
                        devs, ref devInfo,
                        DiClassInstallState.DICS_DISABLE,
                        DiClassInstallScope.DICS_FLAG_CONFIGSPECIFIC,
                        DiClassInstallFunction.DIF_PROPERTYCHANGE
                    );

                    Console.WriteLine("ok.");
                } catch (Exception ex) {
                    Console.WriteLine("failed.");
                }
            }

            if ((options.Count == 0) || options.Contains("enable")) {
                Console.Write("Enabling volume {0}... ", parms.DriveLetter);
                try {
                    Native.ChangeDeviceEnabledState(
                        devs, ref devInfo,
                        DiClassInstallState.DICS_ENABLE,
                        DiClassInstallScope.DICS_FLAG_CONFIGSPECIFIC,
                        DiClassInstallFunction.DIF_PROPERTYCHANGE
                    );

                    Console.WriteLine("ok.");
                } catch (Exception ex) {
                    Console.WriteLine("failed.");
                }
            }
        }

        public static void EnumInterfacesCallback (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, 
            ref Native.SP_DEVICE_INTERFACE_DATA interfaceData,
            string devicePath, Parameters appParams
        ) {
            // We've got a device interface of type Volume. See if it matches either of our selection criteria

            var parms = new EnableDisableParameters {
                ClassGuid = devInfo.ClassGuid,
                DevInst = devInfo.DevInst,
                AppParams = appParams
            };

            if (appParams.InstanceIDs.Contains(devInfo.DevInst)) {
                parms.DriveLetter = String.Format("#{0}", devInfo.DevInst);
            } else {

                DeviceNumber devNumber;
                Exception failureReason;

                if (!GetDeviceNumber(devicePath, out devNumber, out failureReason))
                    return;

                string driveLetter;
                if (!appParams.DriveLetters.TryGetValue(devNumber, out driveLetter))
                    return;

                appParams.DriveLetters.Remove(devNumber);

                parms.DriveLetter = driveLetter;
            }

            Native.EnumerateDevices(
                null, DiGetClassFlags.DIGCF_ALLCLASSES,
                null, DoEnableDisable, parms
            );
        }

        public static void EnumDevicesCallback (
            Native.DeviceInfoListHandle devs, ref Native.SP_DEVINFO_DATA devInfo, 
            string deviceId, Parameters parms
        ) {
            // For each device, enumerate its device interfaces that are of type Volume
            Native.EnumerateDeviceInterfaces(
                devs, ref devInfo, ref DEVINTERFACE_VOLUME, EnumInterfacesCallback, parms
            );
        }

        public static void Main (string[] driveLetters) {
            var parms = new Parameters();

            var helpStrings = new HashSet<string>(new[] { "help", "?", "-?", "-h", "-help", "--help" });
            if ((driveLetters.Length == 0) || 
                ((driveLetters.Length == 1) && helpStrings.Contains(driveLetters[0].Trim().ToLower()))
            ) {
                Console.WriteLine("DeviceRemount v1.0");
                Console.WriteLine("==================");
                Console.WriteLine("Usage:");
                Console.WriteLine("To remount a drive provide its drive letter:");
                Console.WriteLine("DeviceRemount DRIVE: [--disable] [--enable]");
                Console.WriteLine("Or provide its physical volume ID:");
                Console.WriteLine("DeviceRemount #VOLUMEID [--disable] [--enable]");
                Console.WriteLine("The optional --disable and --enable parameters allow you to disable or enable the volume instead of remounting it.");

                return;
            }

            foreach (var driveLetter in driveLetters) {
                if (driveLetter.StartsWith("--")) {
                    parms.Options.Add(driveLetter.Replace("--", "").ToLower());
                } else if (driveLetter.StartsWith("#")) {
                    parms.InstanceIDs.Add(uint.Parse(driveLetter.Substring(1)));
                } else {
                    var devicePath = String.Format(@"\\.\{0}", driveLetter.ToUpper());
                    if (!devicePath.EndsWith(":"))
                        devicePath += ":";

                    Exception failureReason;
                    DeviceNumber devNumber;
                    if (GetDeviceNumber(devicePath, out devNumber, out failureReason)) {
                        parms.DriveLetters.Add(devNumber, driveLetter);
                    } else {
                        throw new Exception("Failed to access drive " + driveLetter, failureReason);
                    }
                }
            }

            Native.EnumerateDevices(
                null, DiGetClassFlags.DIGCF_DEVICEINTERFACE,
                null, EnumDevicesCallback, parms
            );
        }
    }
}
