﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.SmartCards;
using System.Threading.Tasks;
using Pcsc.Common;
using Pcsc;
using NfcSample;
using System.Diagnostics;
using Windows.Storage.Streams;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DangerousTags
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static readonly byte[] PasswordStatic = { 0x11, 0x22, 0x33, 0x44 };
        private static readonly byte[] PasswordAcknowledgeStatic = { 0xAB, 0xCD };

        public MainPage()
        {
            this.InitializeComponent();
        }

        SmartCardReader m_cardReader;

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // First try to find a reader that advertises as being NFC
            var deviceInfo = await SmartCardReaderUtils.GetFirstSmartCardReaderInfo(SmartCardReaderKind.Nfc);
            if (deviceInfo == null)
            {
                // If we didn't find an NFC reader, let's see if there's a "generic" reader meaning we're not sure what type it is
                deviceInfo = await SmartCardReaderUtils.GetFirstSmartCardReaderInfo(SmartCardReaderKind.Any);
            }

            if (deviceInfo == null)
            {
                LogMessage("NFC card reader mode not supported on this device");
                return;
            }

            if (!deviceInfo.IsEnabled)
            {
                var msgbox = new Windows.UI.Popups.MessageDialog("Your NFC proximity setting is turned off, you will be taken to the NFC proximity control panel to turn it on");
                msgbox.Commands.Add(new Windows.UI.Popups.UICommand("OK"));
                await msgbox.ShowAsync();

                // This URI will navigate the user to the NFC proximity control panel
                NfcUtils.LaunchNfcProximitySettingsPage();
                return;
            }

            if (m_cardReader == null)
            {
                m_cardReader = await SmartCardReader.FromIdAsync(deviceInfo.Id);
                m_cardReader.CardAdded += cardReader_CardAdded;
                m_cardReader.CardRemoved += cardReader_CardRemoved;
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Clean up
            if (m_cardReader != null)
            {
                m_cardReader.CardAdded -= cardReader_CardAdded;
                m_cardReader.CardRemoved -= cardReader_CardRemoved;
                m_cardReader = null;
            }

            base.OnNavigatingFrom(e);
        }

        private void cardReader_CardRemoved(SmartCardReader sender, CardRemovedEventArgs args)
        {
            LogMessage("Card removed");

            var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                UidPanel.Visibility = Visibility.Visible;
                AccessPanel.Visibility = Visibility.Visible;
            });
        }

        private async void cardReader_CardAdded(SmartCardReader sender, CardAddedEventArgs args)
        {
            await HandleCard(args.SmartCard);
        }

        /// <summary>
        /// Sample code to hande a couple of different cards based on the identification process
        /// </summary>
        /// <returns>None</returns>
        private async Task HandleCard(SmartCard card)
        {
            try
            {
                // Connect to the card
                using (SmartCardConnection connection = await card.ConnectAsync())
                {
                    // Try to identify what type of card it was
                    IccDetection cardIdentification = new IccDetection(card, connection);
                    await cardIdentification.DetectCardTypeAync();
                    LogMessage("Connected to card\r\nPC/SC device class: " + cardIdentification.PcscDeviceClass.ToString());
                    LogMessage("Card name: " + cardIdentification.PcscCardName.ToString());
                    LogMessage("ATR: " + BitConverter.ToString(cardIdentification.Atr));

                    if ((cardIdentification.PcscDeviceClass == Pcsc.Common.DeviceClass.StorageClass) &&
                        (cardIdentification.PcscCardName == Pcsc.CardName.MifareUltralightC
                        || cardIdentification.PcscCardName == Pcsc.CardName.MifareUltralight
                        || cardIdentification.PcscCardName == Pcsc.CardName.MifareUltralightEV1))
                    {
                        // Handle MIFARE Ultralight
                        MifareUltralight.AccessHandler mifareULAccess = new MifareUltralight.AccessHandler(connection);

                        await mifareULAccess.ReadCapsAsync();

                        if (!mifareULAccess.isNTag21x)
                        {
                            var ignored1 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                            {
                                var msgbox = new Windows.UI.Popups.MessageDialog("This application only works with the NXP NTAG21x line of NFC chips. Sorry.");
                                msgbox.Commands.Add(new Windows.UI.Popups.UICommand("OK"));
                                await msgbox.ShowAsync();
                            });

                            return;
                        }

                        bool authenticated = false;

                        try
                        {
                            await mifareULAccess.AuthenticateWithPassword(PasswordStatic, PasswordAcknowledgeStatic);
                            authenticated = true; 
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Exception sending provisioning password: " + ex);
                        }

                        if (!authenticated)
                        {
                            try
                            {
                                await mifareULAccess.ProvisionPassword(true, 0, PasswordStatic, PasswordAcknowledgeStatic);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Exception sending provisioning password: " + ex);
                            }
                        }

                        bool accessCountEnabled = false;

                        try
                        {
                            uint responseAccess = await mifareULAccess.GetAccessCountAsync();
                            LogMessage("ReadCount:  " + responseAccess.ToString());

                            var ignored2 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                accessCount.Text = responseAccess.ToString();
                            });

                            accessCountEnabled = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Exception sending Getting Access Count: " + ex);
                        }

                        if (!accessCountEnabled)
                        {
                            var ignored3 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                accessCount.Text = " ";
                                accessCountEnable.Visibility = Visibility.Visible;
                            });

                            await mifareULAccess.EnableAccessCountAsync();
                        }

                        for (byte i = 0; i < mifareULAccess.Blocks; i++)
                        {
                            byte[] response = await mifareULAccess.ReadAsync((byte)(4 * i));
                            for (byte y = 0; y < 4; y++)
                            {
                                byte[] buf4 = new byte[4];
                                Array.Copy(response, y * 4, buf4, 0, 4);
                                LogMessage((i * 4 + y).ToString("x2") + ": " + BitConverter.ToString(buf4));
                            }
                        }

                        byte[] responseUid = await mifareULAccess.GetUidAsync();
                        string uidString = BitConverter.ToString(responseUid);
                        LogMessage("UID:  " + uidString);

                        var ignored4 = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            uid.Text = uidString;
                            UidPanel.Visibility = Visibility.Visible;
                        });

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception handling card: " + ex.ToString());
            }
        }

        public void LogMessage(string message)
        {
            if (!this.Dispatcher.HasThreadAccess)
            {
                var ignored = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { LogMessage(message); });
                return;
            }

            Debug.WriteLine(message);
            if (Diagnostics.Text != "")
            {
                Diagnostics.Text += "\r\n";
            }
            Diagnostics.Text += message;
            StatusBlockScroller.ChangeView(0, StatusBlockScroller.ExtentHeight, 1);
        }

        private void savePassword_Click(object sender, RoutedEventArgs e)
        {

        }

        private void accessCountEnable_Click(object sender, RoutedEventArgs e)
        {


        }
    }
}
