using System;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.Devices.Geolocation;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using System.Net.Http;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AzureIoTSuiteUWPDevice
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    
    public sealed partial class MainPage : Page
    {
        [DataContract]
        internal class DeviceProperties
        {
            [DataMember]
            internal string DeviceID;

            [DataMember]
            internal bool HubEnabledState;

            [DataMember]
            internal string CreatedTime;

            [DataMember]
            internal string DeviceState;

            [DataMember]
            internal string UpdatedTime;

            [DataMember]
            internal string Manufacturer;

            [DataMember]
            internal string ModelNumber;

            [DataMember]
            internal string SerialNumber;

            [DataMember]
            internal string FirmwareVersion;

            [DataMember]
            internal string Platform;

            [DataMember]
            internal string Processor;

            [DataMember]
            internal string InstalledRAM;

            [DataMember]
            internal double Latitude;

            [DataMember]
            internal double Longitude;

        }

        [DataContract]
        internal class CommandParameter
        {
            [DataMember]
            internal string Name;

            [DataMember]
            internal string Type;
        }

        [DataContract]
        internal class Command
        {
            [DataMember]
            internal string Name;

            [DataMember]
            internal CommandParameter[] Parameters;
        }

        [DataContract]
        internal class Thermostat
        {
            [DataMember]
            internal DeviceProperties DeviceProperties;

            [DataMember]
            internal Command[] Commands;

            [DataMember]
            internal bool IsSimulatedDevice;

            [DataMember]
            internal string Version;

            [DataMember]
            internal string ObjectType;
        }

        [DataContract]
        internal class TelemetryData
        {
            [DataMember]
            internal string DeviceId;

            [DataMember]
            internal double Temperature;

            [DataMember]
            internal double Humidity;

            [DataMember]
            internal double ExternalTemperature;

        }

        const string const_deviceId = "beacon42_001";
        const string const_hostName = "Pi2SuiteGC.azure-devices.net";
        const string const_deviceKey = "Ho6Yyy9NePlNiiPaDZn2zQ==";

        private string deviceId;
        private string hostName;
        private string deviceKey;
        private string connectionString;

        private bool SendDataToAzureIoTHub = false;
        private double Temperature = 50;
        private double Humidity = 50;
        private double ExternalTemperature = 50;
        private Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        private DeviceClient deviceClient;

        Task ReceivingTask;
        public MainPage()
        {
            this.InitializeComponent();

            // Restore local settings
            deviceId = const_deviceId;
            this.TBDeviceID.Text = deviceId;
            hostName = const_hostName;
            this.TBHostName.Text = hostName;
            deviceKey = const_deviceKey;
            this.TBDeviceKey.Text = deviceKey;

            ConnectToggle.IsEnabled = checkConfig();

            deviceId = this.TBDeviceID.Text;
            hostName = this.TBHostName.Text;
            deviceKey = this.TBDeviceKey.Text;
            ConnectToggle_Checked(this,null);
            toggleButton_Checked(this, null);
            Task.Run(SendDataToAzure);
        }
        private bool checkConfig()
        {
            return ((this.TBDeviceID.Text != null) && (this.TBHostName.Text != null) && (this.TBDeviceKey != null) &&
                    (this.TBDeviceID.Text != "") && (this.TBHostName.Text != "") && (this.TBDeviceKey.Text != ""));
        }

        private byte[] Serialize(object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            return Encoding.UTF8.GetBytes(json);

        }

        private dynamic DeSerialize(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject(text);
        }

        private async void sendDeviceMetaData()
        {
            DeviceProperties device = new DeviceProperties();
            Thermostat thermostat = new Thermostat();

            thermostat.ObjectType = "DeviceInfo";
            thermostat.IsSimulatedDevice = false;
            thermostat.Version = "1.0";

            device.HubEnabledState = true;
            device.DeviceID = deviceId;
            device.Manufacturer = "Raspberry";
            device.ModelNumber = "Pi2";
            device.SerialNumber = "5849735293875";
            device.FirmwareVersion = "10";
            device.Platform = "Windows 10";
            device.Processor = "RaspBerry";
            device.InstalledRAM = "976 MB";
            device.DeviceState = "normal";

            string pos = await GetGeoLocFromWeb();
            var nloc = pos.IndexOf("loc");
            var sub01 = pos.Substring(nloc + 7);
            string location = sub01.Substring(0, sub01.IndexOf('"'));
            var x = location.Split(',');
            device.Latitude = System.Convert.ToDouble(x[0]);
            device.Longitude = System.Convert.ToDouble(x[1]);

            thermostat.DeviceProperties = device;

            Command TriggerAlarm = new Command();
            TriggerAlarm.Name = "TriggerAlarm";
            CommandParameter param = new CommandParameter();
            param.Name = "Message";
            param.Type = "String";
            TriggerAlarm.Parameters = new CommandParameter[] { param };

            thermostat.Commands = new Command[] { TriggerAlarm };

            try
            {
                var msg = new Message(Serialize(thermostat));
                if (deviceClient != null)
                {
                    await deviceClient.SendEventAsync(msg);
                }
            }
            catch (System.Exception e)
            {
                Debug.Write("Exception while sending device meta data :\n" + e.Message.ToString());
            }

            Debug.Write("Sent meta data to IoT Suite\n" + hostName);

        }

        const string WebAPIURL = "http://ipinfo.io";
        public async Task<string> GetGeoLocFromWeb()
        {
            Debug.WriteLine("IoTSuite::MakeWebApiCall");

            string responseString = "{\"ip\": \"201.214.17.189\",\"hostname\": \"No Hostname\", \"city\": \"\",  \"region\": \"\",  \"country\": \"CL\",  \"loc\": \"-33.4378,-70.6503\",  \"org\": \"AS22047 VTR BANDA ANCHA S.A.\"}";
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Make the call
                    responseString = await client.GetStringAsync(WebAPIURL);

                    // Let us know what the returned string was
                    Debug.WriteLine(String.Format("Response string: [{0}]", responseString));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            
            return responseString;
        }

        private async void sendDeviceTelemetryData()
        {
            TelemetryData data = new TelemetryData();
            data.DeviceId = deviceId;
            data.Temperature = Temperature + 0.55;
            data.Humidity = Humidity + 0.55;
            data.ExternalTemperature = ExternalTemperature + 0.55;
            if (Temperature == 50)
            {
                Temperature = 45;
            }
            else
            {
                Temperature = 50;
            }
            if (Humidity == 50)
            {
                Humidity = 55;
            }
            else
            {
                Humidity = 50;
            }

            try
            {
                var msg = new Message(Serialize(data));
                if (deviceClient != null)
                {
                    await deviceClient.SendEventAsync(msg);
                }
            }
            catch (System.Exception e)
            {
                Debug.Write("Exception while sending device telemetry data :\n" + e.Message.ToString());
            }
            Debug.Write("Sent telemetry data to IoT Suite\nTemperature=" + string.Format("{0:0.00}", Temperature) + "\nHumidity=" + string.Format("{0:0.00}", Humidity));

        }

        private async Task ReceiveDataFromAzure()
        {

            while (true)
            {
                Message message = await deviceClient.ReceiveAsync();
                if (message != null)
                {
                    try
                    {
                        dynamic command = DeSerialize(message.GetBytes());
                        if (command.Name == "TriggerAlarm")
                        {
                            // Received a new message, display it
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            async () =>
                            {
                                var dialogbox = new MessageDialog("Received message from Azure IoT Hub: " + command.Parameters.Message.ToString());
                                await dialogbox.ShowAsync();
                            });
                            // We received the message, indicate IoTHub we treated it
                            await deviceClient.CompleteAsync(message);
                        }
                    }
                    catch
                    {
                        await deviceClient.RejectAsync(message);
                    }
                }
            }
        }

        private async Task SendDataToAzure()
        {

            while (true)
            {
                if (SendDataToAzureIoTHub)
                {
                    sendDeviceTelemetryData();
                }
                await Task.Delay(1000);
            }
        }

        private void toggleButton_Checked(object sender, RoutedEventArgs e)
        {
            SendDataToggle.Content = "Sending Data to IoT Suite";
            SendDataToAzureIoTHub = true;
        }

        private void toggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            SendDataToggle.Content = "Press to send data to IoT Suite";
            SendDataToAzureIoTHub = false;
        }

        private void TempSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Temperature = TempSlider.Value;
        }

        private void HmdtSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Humidity = HmdtSlider.Value;
        }

        private void TBDeviceID_TextChanged(object sender, TextChangedEventArgs e)
        {
            deviceId = TBDeviceID.Text;
            localSettings.Values["DeviceId"] = deviceId;
            ConnectToggle.IsEnabled = checkConfig();
        }
        private void TBHostName_TextChanged(object sender, TextChangedEventArgs e)
        {
            hostName = TBHostName.Text;
            localSettings.Values["HostName"] = hostName;
            ConnectToggle.IsEnabled = checkConfig();
        }

        private void TBDeviceKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            deviceKey = TBDeviceKey.Text;
            localSettings.Values["DeviceKey"] = deviceKey;
            ConnectToggle.IsEnabled = checkConfig();
        }

        private void ConnectToggle_Checked(object sender, RoutedEventArgs e)
        {
            connectToIoTSuite();
            if (deviceClient != null)
            {
                SendDataToggle.IsEnabled = true;
                TBDeviceID.IsEnabled = false;
                TBDeviceKey.IsEnabled = false;
                TBHostName.IsEnabled = false;
                ConnectToggle.Content = "Connected to IoT Suite";
            }
        }

        private void ConnectToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            SendDataToggle.IsChecked = false;
            SendDataToggle.IsEnabled = false;
            TBDeviceID.IsEnabled = true;
            TBDeviceKey.IsEnabled = true;
            TBHostName.IsEnabled = true;
            disconnectFromIoTSuite();
            ConnectToggle.Content = "Press to connect to IoT Suite";
        }

        private async void connectToIoTSuite()
        {
            connectionString = "HostName=" + hostName + ";DeviceId=" + deviceId + ";SharedAccessKey=" + deviceKey;
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Http1);
                await deviceClient.OpenAsync();
                sendDeviceMetaData();
                ReceivingTask = Task.Run(ReceiveDataFromAzure);
            }
            catch
            {
                Debug.Write("Error while trying to connect to IoT Hub");
                deviceClient = null;
            }
        }

        private async void disconnectFromIoTSuite()
        {
            if (deviceClient != null)
            {
                try
                {
                    await deviceClient.CloseAsync();
                    deviceClient = null;
                }
                catch
                {
                    Debug.Write("Error while trying close the IoT Hub connection");
                }
            }
        }
    }
}
