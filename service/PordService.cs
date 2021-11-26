using System;
using System.Windows;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using System.Threading;
using Newtonsoft.Json;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace MicroPods.service
{
	public partial class PordService : Window
	{
		/*
		 https://juejin.cn/post/6865605933284720654
		 */

		public Dictionary<string, Object> status;

		public delegate void UpdateUIDelegate(Dictionary<string, Object> status);
		public UpdateUIDelegate updateUIDelegate;


		private BluetoothLEAdvertisementWatcher watcher;

		/*private BluetoothLEManufacturerData manufacturerData;*/

		static string[] AIRPODS_UUIDS =
		{
			"74ec2172-0bad-4d01-8f77-997b2be0722a",
			"2a72e02b-7b99-778f-014d-ad0b7221ec74"
		};

		static ushort AIRPODS_MANUFACTURER = 76;
		static int AIRPODS_DATA_LENGTH = 27;
		static int MIN_RSSI = -60;

		public PordService()
		{

			Debug.WriteLine("创建 Pord 服务");

			initBluetooth();
			watcher.Start();
		}


		private void initBluetooth()
		{
			watcher = new BluetoothLEAdvertisementWatcher();

			var manufacturerData = new BluetoothLEManufacturerData();

			manufacturerData.CompanyId = AIRPODS_MANUFACTURER;
			/*manufacturerData.CompanyId = 0x014C;*/

			var writer = new DataWriter();
			/*writer.WriteUInt16(0x0719);*/
			byte[] manufacturerDataData = new byte[AIRPODS_DATA_LENGTH];
			manufacturerDataData[0] = 7;
			manufacturerDataData[1] = 25;

			writer.WriteBytes(manufacturerDataData);
			// manufacturerData.Data = writer.DetachBuffer();



			/*foreach (string uuid in AIRPODS_UUIDS)
            {
				watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(Guid.Parse(uuid));
			}*/
			// 过滤条件：companyId = 0x014C 且值为：0x0719的广播。可自行修改或删除
			watcher.AdvertisementFilter.Advertisement.ManufacturerData.Add(manufacturerData);
			// 根据信号强度过滤
			watcher.SignalStrengthFilter.InRangeThresholdInDBm = -50;
			watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -55;
			watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(2000);
			watcher.Received += OnAdvertisementReceived;
			watcher.Stopped += OnAdvertisementWatcherStopped;
		}

		private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
		{
			// 信号强度
			Int16 rssi = eventArgs.RawSignalStrengthInDBm;
			// rssi check 

			if (rssi < MIN_RSSI)
			{
				// Debug.WriteLine("rssi check fail");
				return;
			}


			// 蓝牙地址
			/*string addr = CommonUtil.btAddrFormat2(eventArgs.BluetoothAddress.ToString("x8"));*/
			string addr = eventArgs.BluetoothAddress.ToString("x8");

			string manufacturerDataString = "";
			var manufacturerSections = eventArgs.Advertisement.ManufacturerData;
			if (manufacturerSections.Count > 0)
			{
				// Only print the first one of the list
				var manufacturerData = manufacturerSections[0];


				// data len check

				if (manufacturerData.Data.Length != AIRPODS_DATA_LENGTH)
				{
					// Debug.WriteLine("data len check fail");
					return;
				}

				var data = new byte[manufacturerData.Data.Length];
				using (var reader = DataReader.FromBuffer(manufacturerData.Data))
				{
					reader.ReadBytes(data);
				}
				manufacturerDataString = BitConverter.ToString(data);
			}


			string str = manufacturerDataString.Replace("-", "");

			bool isFlipped = PordService.isFlipped(str); ;
			Console.WriteLine(isFlipped);
			var charArr = str.ToCharArray();

			char leftStatus = ((char)charArr.GetValue(isFlipped ? 12 : 13));
			char rightStatus = ((char)charArr.GetValue(isFlipped ? 13 : 12));
			char caseStatus = ((char)charArr.GetValue(15));
			char singleStatus = ((char)charArr.GetValue(13));

			char chargeStatus = ((char)charArr.GetValue(14));

			bool chargeL = (chargeStatus & (isFlipped ? 0b00000010 : 0b00000001)) != 0;
			bool chargeR = (chargeStatus & (isFlipped ? 0b00000001 : 0b00000010)) != 0;
			bool chargeCase = (chargeStatus & 0b00000100) != 0;
			bool chargeSingle = (chargeStatus & 0b00000001) != 0;


			char inEarStatus = ((char)charArr.GetValue(11));
			bool inEarL = (inEarStatus & (isFlipped ? 0b00001000 : 0b00000010)) != 0;
			bool inEarR = (inEarStatus & (!isFlipped ? 0b00001000 : 0b00000010)) != 0;

			char model = (char)charArr.GetValue(7);
			Dictionary<string, Object> map = new Dictionary<string, Object>();
			map.Add("model", model);
			map.Add("leftStatus", leftStatus);
			map.Add("rightStatus", rightStatus);
			map.Add("caseStatus", caseStatus);
			map.Add("singleStatus", singleStatus);


			map.Add("chargeL", chargeL);
			map.Add("chargeR", chargeR);
			map.Add("chargeCase", chargeCase);
			map.Add("chargeSingle", chargeSingle);


			map.Add("inEarL", inEarL);
			map.Add("inEarR", inEarR);

			// 5s 有效
			map.Add("validTime", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + 5);

			this.status = map;

			Debug.WriteLine(String.Format("{0} {1}", DateTime.Now, JsonConvert.SerializeObject(map)));

			if (this.updateUIDelegate != null)
			{
				// this.updateUIDelegate(status);
				this.Dispatcher.Invoke(this.updateUIDelegate, this.status);

			}
		
		}

		private async void OnAdvertisementWatcherStopped(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementWatcherStoppedEventArgs eventArgs)
		{
			/*this.btnScan.Dispatcher.Invoke(() =>
			{
			});*/
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			watcher.Received -= OnAdvertisementReceived;
			watcher.Stopped -= OnAdvertisementWatcherStopped;
			watcher.Stop();
		}


		public static bool isFlipped(string str)
		{
			str = str.Replace("-", "");
			char c = (char)str.ToCharArray().GetValue(10);
			return (c & 0x02) == 0;

		}

	}
}
