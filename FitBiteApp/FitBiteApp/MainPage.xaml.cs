using System;
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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text;
using System.Threading;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using GHIElectronics.UWP.Shields;
using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Net.Http;
using System.Net.Http.Headers;

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FitBiteApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Timer imageCaptureTimer;
        private Timer dataCaptureTimer;
        private TimeSpan imageCaptureTimerInterval = new TimeSpan(0, 0, 30); // 30 seconds.
        private TimeSpan dataCaptureTimerInterval = new TimeSpan(0, 0, 15); // 15 seconds.
        private string AZURE_STORAGE_ACCOUNT = "<Enter Azure Storage Account Name>";
        private string AZURE_STORAGE_ACCOUNT_ACCESS_KEY  = "<Enter Azure Storage Primary key>";
        private string AZURE_STORAGE_CONTAINER_NAME  = "<Enter Azure Storage Container Name>";
        private MediaCapture mediaCapture;
        FEZHAT hat;
        DispatcherTimer timer;
        Random random = new Random();

        static string AZURE_IOT_HUB_URI = "<Enter iotHub URI>"; 
        static string IOT_DEVICE_ID = "<Enter your IOT_DEVICE_ID>"; 
        static string IOT_DEVICE_KEY = "<Enter Device Key>"; 

        DeviceClient deviceClient;

        ConnectTheDotsHelper ctdHelper;
        public MainPage()
        {
            this.InitializeComponent();
            this.initialize();
            this.deviceClient = DeviceClient.Create(AZURE_IOT_HUB_URI,
                   AuthenticationMethodFactory.
                       CreateAuthenticationWithRegistrySymmetricKey(IOT_DEVICE_ID, IOT_DEVICE_KEY),
                   TransportType.Http1);
            this.imageCaptureTimer = new Timer(this.imageCaptureTimerExecuted, new object(), this.imageCaptureTimerInterval, this.imageCaptureTimerInterval);

            this.dataCaptureTimer = new Timer(this.dataCaptureTimerExecuted, new object(), this.dataCaptureTimerInterval, this.dataCaptureTimerInterval);

        }

        private async void dataCaptureTimerExecuted(object state)
        {
            try
            {
                // Light Sensor
                ConnectTheDotsSensor lSensor = ctdHelper.sensors.Find(item => item.measurename == "Light");
                lSensor.value = this.hat.GetLightLevel();

                // Temperature Sensor
                var tSensor = ctdHelper.sensors.Find(item => item.measurename == "Temperature");
                tSensor.value = this.hat.GetTemperature();

                //   this.TempTextBox.Text = tSensor.value.ToString("N2", CultureInfo.InvariantCulture);
                var sSensor = GetSoundIntensity();
                var subtractDays = random.Next(1, 30);
                Event info = new Event()
                {

                    TimeStamp1 = DateTime.UtcNow,
                    IOT_DEVICE_ID = random.Next(1, 5),
                    Temperature = tSensor.value,
                    Light = lSensor.value,
                    Sound = sSensor
                };
                var serializedString = JsonConvert.SerializeObject(info);
                Message data = new Message(Encoding.UTF8.GetBytes(serializedString));
                await deviceClient.SendEventAsync(data);
                System.Diagnostics.Debug.WriteLine("Temperature: {0} °C, Light {1}, Sound {2}", tSensor.value.ToString("N2", CultureInfo.InvariantCulture), lSensor.value.ToString("P2", CultureInfo.InvariantCulture), sSensor);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

        }

        private double GetSoundIntensity()
        {
            HttpClient client = new HttpClient();
         
            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
            string soundIntensityBaseUri = "<Enter the URI for SoundIntensity Rest API>";
            // List data response.
            HttpResponseMessage response = client.GetAsync(soundIntensityBaseUri+"/soundintensity").Result;  // Blocking call!
            double sSensorValue = 0;
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body. Blocking!
                var result = response.Content.ReadAsStringAsync().Result;
                if (result != null)
                    sSensorValue = Convert.ToDouble(result);
                return sSensorValue;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return 0;
            }

        }

        private async void initialize()
        {
            // Create a new instance of a MediaCapture object and Initialize it Asynchronously.
            this.mediaCapture = new MediaCapture();
            await this.mediaCapture.InitializeAsync();
           // this.previewElement.Source = this.mediaCapture;
            await this.mediaCapture.StartPreviewAsync();
            var deviceInfo = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();

            // Hard coding guid for sensors. Not an issue for this particular application which is meant for testing and demos
            List<ConnectTheDotsSensor> sensors = new List<ConnectTheDotsSensor> {
                new ConnectTheDotsSensor("2298a348-e2f9-4438-ab23-82a3930662ab", "Light", "L"),
                new ConnectTheDotsSensor("d93ffbab-7dff-440d-a9f0-5aa091630201", "Temperature", "C"),
            };

            ctdHelper = new ConnectTheDotsHelper(serviceBusNamespace: "SERVICE_BUS_NAMESPACE",
                eventHubName: "EVENT_HUB_NAME",
                keyName: "SHARED_ACCESS_POLICY_NAME",
                key: "SHARED_ACCESS_POLICY_KEY",
                displayName: deviceInfo.FriendlyName,
                organization: "YOUR_ORGANIZATION_OR_SELF",
                location: "YOUR_LOCATION",
                sensorList: sensors);

            // Initialize FEZ HAT shield
            this.hat = await FEZHAT.CreateAsync();
        }

        private string buildDateTimeStamp()
        {
            StringBuilder sb = new StringBuilder();
            DateTime currentDate = DateTime.Now;
            sb.Append(currentDate.Year.ToString());
            if (currentDate.Month.ToString().Length == 1)
                sb.Append("0" + currentDate.Month.ToString());
            else
                sb.Append(currentDate.Month.ToString());
            if (currentDate.Day.ToString().Length == 1)
                sb.Append("0" + currentDate.Day.ToString());
            else
                sb.Append(currentDate.Day.ToString());
            // Add the Hour to the StringBuilder.
            if (currentDate.Hour.ToString().Length == 1)
                sb.Append("0" + currentDate.Hour.ToString());
            else
                sb.Append(currentDate.Hour.ToString());
            // Add the Minute to the StringBuilder.
            if (currentDate.Minute.ToString().Length == 1)
                sb.Append("0" + currentDate.Minute.ToString());
            else
                sb.Append(currentDate.Minute.ToString());
            // Add the Second to the StringBuilder.
            if (currentDate.Second.ToString().Length == 1)
                sb.Append("0" + currentDate.Second.ToString());
            else
                sb.Append(currentDate.Second.ToString());
            // Return the value.
            return sb.ToString();
        }

        private async void imageCaptureTimerExecuted(object sender)
        {
            StorageFile photoFile;
            string photoFileName = this.buildDateTimeStamp() + ".jpg";

            // Create the PhotoFile in the PicturesLibrary System folder.
            photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(photoFileName, CreationCollisionOption.ReplaceExisting);

            // Create an ImageEncodingProperties object for the photo file.
            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();

            // Using the MediaCapture, capture the photo and save it to the PicturesLibrary System folder.
            await this.mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);

            // Create an Azure CloudStorageAccount object.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=" + this.AZURE_STORAGE_ACCOUNT + ";AccountKey=" + this.AZURE_STORAGE_ACCOUNT_ACCESS_KEY );

            // Create a CloudBlobClient object using the CloudStorageAccount.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Create a CloudBlobContain object using the CloudBlobClient.
            CloudBlobContainer container = blobClient.GetContainerReference(this.AZURE_STORAGE_CONTAINER_NAME );

            //  Create a CloudBlockBlob using the CloudBlobContainer.
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(photoFileName);

            //  Using the CloudBlockBlob, Upload the file to Azure.
            await blockBlob.UploadFromFileAsync(photoFile);
        }
        private async void SetupHat()
        {
            this.hat = await FEZHAT.CreateAsync();

            this.timer = new DispatcherTimer();

            this.timer.Interval = TimeSpan.FromMilliseconds(500);
            // this.timer.Tick += this.Timer_Tick;

            this.timer.Start();
        }
     
    }
}
