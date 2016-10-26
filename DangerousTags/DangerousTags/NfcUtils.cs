//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.SmartCards;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Windows.ApplicationModel;

namespace NfcSample
{
    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

    public class NfcUtils
    {
        public static async void LaunchNfcPaymentsSettingsPage()
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-nfctransactions:"));
        }

        public static async void LaunchNfcProximitySettingsPage()
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-proximity:"));
        }

        public static byte[] HexStringToBytes(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException();
            }  
            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
            {
                bytes[i / 2] = byte.Parse(hexString.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return bytes;
        }
    }

    public class CrcB
    {
        const ushort __crcBDefault = 0xffff;

        private static ushort UpdateCrc(byte b, ushort crc)
        {
            unchecked
            {
                byte ch = (byte)(b ^ (byte)(crc & 0x00ff));
                ch = (byte)(ch ^ (ch << 4));
                return (ushort)((crc >> 8) ^ (ch << 8) ^ (ch << 3) ^ (ch >> 4));
            }
        }

        public static ushort ComputeCrc(byte[] bytes)
        {
            var res = __crcBDefault;
            foreach (var b in bytes)
                res = UpdateCrc(b, res);
            return (ushort)~res;
        }
    }
}
