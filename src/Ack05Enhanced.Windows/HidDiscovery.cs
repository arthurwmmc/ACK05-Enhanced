using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Ack05Enhanced.Windows;

public static class HidDiscovery
{
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public static IReadOnlyList<HidDeviceInfo> EnumerateAck05Collections()
    {
        HidNativeMethods.HidD_GetHidGuid(out var hidGuid);
        var deviceInfoSet = HidNativeMethods.SetupDiGetClassDevs(
            ref hidGuid,
            null,
            IntPtr.Zero,
            HidNativeMethods.DigcfPresent | HidNativeMethods.DigcfDeviceInterface);

        if (deviceInfoSet == InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var devices = new List<HidDeviceInfo>();
            for (uint index = 0; ; index++)
            {
                var interfaceData = new HidNativeMethods.SpDeviceInterfaceData
                {
                    Size = Marshal.SizeOf<HidNativeMethods.SpDeviceInterfaceData>(),
                };

                if (!HidNativeMethods.SetupDiEnumDeviceInterfaces(
                        deviceInfoSet,
                        IntPtr.Zero,
                        ref hidGuid,
                        index,
                        ref interfaceData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == HidNativeMethods.ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                var path = GetDevicePath(deviceInfoSet, ref interfaceData);
                var device = TryReadDeviceInfo(path);
                if (device?.IsAck05 == true)
                {
                    devices.Add(device);
                }
            }

            return devices;
        }
        finally
        {
            HidNativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    public static Microsoft.Win32.SafeHandles.SafeFileHandle OpenForMonitoring(
        HidDeviceInfo device,
        bool requireWriteAccess)
    {
        var desiredAccess = HidNativeMethods.GenericRead;
        if (requireWriteAccess)
        {
            desiredAccess |= HidNativeMethods.GenericWrite;
        }

        var handle = HidNativeMethods.CreateFile(
            device.Path,
            desiredAccess,
            HidNativeMethods.FileShareRead | HidNativeMethods.FileShareWrite,
            IntPtr.Zero,
            HidNativeMethods.OpenExisting,
            HidNativeMethods.FileFlagOverlapped,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(
                error,
                requireWriteAccess
                    ? "Could not open the ACK05 vendor HID collection for read/write. " +
                      "Exit the XPPen application and try again."
                    : "Could not open the selected ACK05 HID collection for read-only monitoring.");
        }

        return handle;
    }

    private static string GetDevicePath(
        IntPtr deviceInfoSet,
        ref HidNativeMethods.SpDeviceInterfaceData interfaceData)
    {
        HidNativeMethods.SetupDiGetDeviceInterfaceDetail(
            deviceInfoSet,
            ref interfaceData,
            IntPtr.Zero,
            0,
            out var requiredSize,
            IntPtr.Zero);

        var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
            if (!HidNativeMethods.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet,
                    ref interfaceData,
                    detailBuffer,
                    requiredSize,
                    out _,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4))
                ?? throw new InvalidOperationException("HID device path was null.");
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }

    private static HidDeviceInfo? TryReadDeviceInfo(string path)
    {
        using var handle = HidNativeMethods.CreateFile(
            path,
            0,
            HidNativeMethods.FileShareRead | HidNativeMethods.FileShareWrite,
            IntPtr.Zero,
            HidNativeMethods.OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        var attributes = new HidNativeMethods.HiddAttributes
        {
            Size = Marshal.SizeOf<HidNativeMethods.HiddAttributes>(),
        };
        if (!HidNativeMethods.HidD_GetAttributes(handle, ref attributes))
        {
            return null;
        }

        if (attributes.VendorId != 0x28BD || attributes.ProductId != 0x0202)
        {
            return null;
        }

        if (!HidNativeMethods.HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return null;
        }

        HidNativeMethods.HidpCaps caps;
        try
        {
            if (HidNativeMethods.HidP_GetCaps(preparsedData, out caps) < 0)
            {
                return null;
            }
        }
        finally
        {
            HidNativeMethods.HidD_FreePreparsedData(preparsedData);
        }

        return new HidDeviceInfo(
            path,
            attributes.VendorId,
            attributes.ProductId,
            caps.UsagePage,
            caps.Usage,
            caps.InputReportByteLength,
            caps.OutputReportByteLength,
            ReadManufacturer(handle),
            ReadProduct(handle));
    }

    private static string? ReadManufacturer(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle)
    {
        var buffer = new StringBuilder(256);
        return HidNativeMethods.HidD_GetManufacturerString(handle, buffer, buffer.Capacity * 2)
            ? buffer.ToString()
            : null;
    }

    private static string? ReadProduct(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle)
    {
        var buffer = new StringBuilder(256);
        return HidNativeMethods.HidD_GetProductString(handle, buffer, buffer.Capacity * 2)
            ? buffer.ToString()
            : null;
    }
}
