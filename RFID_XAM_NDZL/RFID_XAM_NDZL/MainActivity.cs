using System;
using Android.App;
using Android.Widget;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Com.Zebra.Rfid.Api3;
using System.Collections.Generic;
using System.Threading;
using Android.Util;

namespace RFID_XAM_NDZL
{
	[Activity(Label = "@string/app_name",  MainLauncher = true)]
	public class MainActivity : Activity, IRfidEventsListener
    {
        private static Readers readers;
        private static IList<ReaderDevice> availableRFIDReaderList;
        private static ReaderDevice readerDevice;
        private static RFIDReader Reader;

        private static String TAG = "RFID_XAM_NDZL";

        Button b1, b2, b3, bt50, bt90;
        TextView tvStatus, tvTags;
        private int RSSI_THRESHOLD = -100;

        protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.content_main);

            b1 = FindViewById<Button>(Resource.Id.button1);
            b2 = FindViewById<Button>(Resource.Id.button2);
            b3 = FindViewById<Button>(Resource.Id.button3);
            bt50 = FindViewById<Button>(Resource.Id.Thresh50);
            bt90 = FindViewById<Button>(Resource.Id.Thresh90);

            tvStatus = FindViewById<TextView>(Resource.Id.tvStatus);
            tvTags = FindViewById<TextView>(Resource.Id.tvTags);
            
            b1.Click += delegate
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    GetAvailableReaders();

                    RunOnUiThread(() =>
                    {
                        if (readerDevice != null)
                            tvStatus.Text = "Reader " + readerDevice.Address;
                        else
                            tvStatus.Text = "Reader NOT FOUND";
                    });
                });
            };

            b2.Click += delegate {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    Connect();

                });
            };

            b3.Click += delegate {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    RunOnUiThread(() =>
                    {
                        tvStatus.Text = "Disconnecting";
                        tvTags.Text = "...";
                        tagListDict.Clear();
                    });

                    Disconnect();
                });
            };

            bt50.Click += delegate {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    RSSI_THRESHOLD = -50;

                });
            };

            bt90.Click += delegate {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    RSSI_THRESHOLD = -90;
                });
            };

        }

        private void GetAvailableReaders()
        {
            readerDevice = null;
            // SDK
            if (readers == null)
            {
                readers = new Readers(this, ENUM_TRANSPORT.ServiceSerial);
            }
            try
            {
                if (readers != null)
                {
                    if (readers.AvailableRFIDReaderList != null)
                    {
                        availableRFIDReaderList =  readers.AvailableRFIDReaderList;
                        if (availableRFIDReaderList.Count != 0)
                        {
                            readerDevice = availableRFIDReaderList[0];
                            Reader = readerDevice.RFIDReader;
                        }
                    }
                }

            }
            catch (InvalidUsageException e)
            {
                e.PrintStackTrace();
            }
            catch (OperationFailureException e)
            {
                e.PrintStackTrace();
                Log.Debug(TAG, "OperationFailureException " + e.VendorMessage);
            }
        }

        private void Connect()
        {
            if (Reader != null && !Reader.IsConnected)
            {
                RunOnUiThread(() =>
                {
                    Toast.MakeText(this, "Connecting", ToastLength.Short).Show();
                });

                Reader.Connect();

                RunOnUiThread(() =>
                {
                    if(Reader.IsConnected)
                        tvStatus.Text = "Connected";
                });

                ConfigureReader();

            }
        }

        private void ConfigureReader()
        {
            if (Reader.IsConnected)
            {
                TriggerInfo _triggerInfo = new TriggerInfo();
                _triggerInfo.StartTrigger.TriggerType = START_TRIGGER_TYPE.StartTriggerTypeImmediate;
                _triggerInfo.StopTrigger.TriggerType = STOP_TRIGGER_TYPE.StopTriggerTypeImmediate;
                try
                {
                    // receive events from reader
                    // based on the interface IRfidEventsListener declared for this class
                    Reader.Events.AddEventsListener(this);

                    // HH event
                    Reader.Events.SetHandheldEvent(true);
                    // tag event with tag data
                    Reader.Events.SetTagReadEvent(true);
                    Reader.Events.SetAttachTagDataWithReadEvent(false);

                    // set trigger mode as rfid so scanner beam will not come
                    Reader.Config.SetTriggerMode(ENUM_TRIGGER_MODE.RfidMode, true);

                    // configure for antenna and singulation etc.
                    Reader.Config.SetUniqueTagReport(true);
                    
                    // set start and stop triggers
                    Reader.Config.StartTrigger = _triggerInfo.StartTrigger;
                    Reader.Config.StopTrigger = _triggerInfo.StopTrigger;
                }
                catch (InvalidUsageException e)
                {
                    e.PrintStackTrace();
                }
                catch (OperationFailureException e)
                {
                    e.PrintStackTrace();
                }
            }
        }

        private void Disconnect()
        {
            if (Reader != null)
            {
                Reader.Events.RemoveEventsListener(this);
                Reader.Disconnect();
                RunOnUiThread(() =>
                {
                    if (!Reader.IsConnected)
                        tvStatus.Text = "Disconnected";
                });

            }
        }

        private static HashSet<String> tagListDict = new HashSet<string>();

        void IRfidEventsListener.EventReadNotify(RfidReadEvents p0)
        {
            TagData[] myTags = Reader.Actions.GetReadTags(100);

            if (myTags != null)
            {
                Log.Debug(TAG, "Read Notification: size=" + myTags.Length);
                for (int index = 0; index < myTags.Length; index++)

                {
                    Log.Debug(TAG, "Tag ID " + myTags[index].TagID);
                    String tagID = "id:" + myTags[index].TagID;
                    String tagRSSI= "rssi:" + myTags[index].PeakRSSI;

                    ////SINGULATION
                    
                    if (!tagListDict.Contains(tagID) && myTags[index].PeakRSSI>=RSSI_THRESHOLD) {
                        tagListDict.Add(tagID);
                        //PrintOnScreen(tagID+" "+tagRSSI);
                    }
                    

                    PrintOnScreen(tagID + " " + tagRSSI);  //to test native singulation

                    PrintStatus("Distinct tags seen: #"+ tagListDict.Count);

                    //if (myTags[index].OpCode == ACCESS_OPERATION_CODE.AccessOperationRead &&
                    //    myTags[index].OpStatus == ACCESS_OPERATION_STATUS.AccessSuccess)
                    //{
                    //    if (myTags[index].MemoryBankData.Length > 0)
                    //    {
                    //        Log.Debug(TAG, " Mem Bank Data " + myTags[index].MemoryBankData);
                    //    }
                    //}
                }
            }
        }

        void PrintOnScreen(string _s) {

            RunOnUiThread(() =>
            {
                if (tvTags.Text.Split('\n').Length > 15)
                    tvTags.Text = "";
                tvTags.Text += "\n" + _s;
            });

        }

        void PrintStatus(string _s)
        {

            RunOnUiThread(() =>
            {
                tvStatus.Text = _s; 
            });

        }

        void IRfidEventsListener.EventStatusNotify(RfidStatusEvents p0)
        {
            Log.Debug(TAG, "Status Notification: " +  p0.StatusEventData.StatusEventType);
            

            RunOnUiThread(() =>
            {
                tvStatus.Text = "" + p0.StatusEventData.StatusEventType;
            });

            if (p0.StatusEventData.StatusEventType == STATUS_EVENT_TYPE.HandheldTriggerEvent)
            {
                if (p0.StatusEventData.HandheldTriggerEventData.HandheldEvent ==
                HANDHELD_TRIGGER_EVENT_TYPE.HandheldTriggerPressed)
                {
                    ThreadPool.QueueUserWorkItem(o => PerformInventory());

                    RunOnUiThread(() =>
                    {
                        tvStatus.Text = "HandheldTriggerPressed => PERFORMING INVENTORY";
                    });
                }
                if (p0.StatusEventData.HandheldTriggerEventData.HandheldEvent ==
                HANDHELD_TRIGGER_EVENT_TYPE.HandheldTriggerReleased)
                {
                    ThreadPool.QueueUserWorkItem(o => StopInventory());
                    RunOnUiThread(() =>
                    {
                        tvStatus.Text = "HandheldTriggerReleased => STOPPING INVENTORY";
                    });
                }
            }
        }

        private void StopInventory()
        {
            try
            {
                Reader.Actions.Inventory.Stop();
                RunOnUiThread(() =>
                {

                });
            }
            catch (InvalidUsageException e)
            {
                e.PrintStackTrace();
            }
            catch (OperationFailureException e)
            {
                e.PrintStackTrace();
            }
        }

        private void PerformInventory()
        {

            try
            {
                Reader.Actions.Inventory.Perform();
                RunOnUiThread(() =>
                {

                });
            }
            catch (InvalidUsageException e)
            {
                e.PrintStackTrace();
            }
            catch (OperationFailureException e)
            {
                e.PrintStackTrace();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            try
            {
                if (Reader != null)
                {
                    Reader.Events.RemoveEventsListener(this);
                    Reader.Disconnect();
                    Reader = null;
                }
                readers.Dispose();
                readers = null;
            }
            catch (InvalidUsageException e)
            {
                e.PrintStackTrace();
            }
            catch (OperationFailureException e)
            {
                e.PrintStackTrace();
            }
            catch (Exception e)
            {
                e.StackTrace.ToString();
            }
        }
    }
}

