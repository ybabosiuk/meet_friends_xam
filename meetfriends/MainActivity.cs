using Android.App;
using Android.Widget;
using Android.OS;
using System.Net;
using System.Text;
using System.IO;
using System;
using LBXamarinSDK;
using LBXamarinSDK.LBRepo;
using System.Threading;

namespace meetfriends
{
	[Activity (Label = "meetfriends", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{

		private Button mBtnSingUp; 

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
//
//			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

//			mBtnSingUp = FindViewById<Button> (Resource.Id.btnSignUp);
//
//			mBtnSingUp.Click += (object sender, EventArgs e) => {
//				//Pull up dialog
//				FragmentTransaction transaction = FragmentManager.BeginTransaction();
//				DialogSignUp signUpDialog = new DialogSignUp();
//				signUpDialog.Show(transaction, "Dialog Fragment");
//			};

//			LBXamarinSDK.Gateway getaway = new Gateway ();
//			Console.WriteLine(Gateway.isConnected (10)); 
//
			var event1 = new Event () {
				title = "string",
				from = Convert.ToDateTime("2016-05-20T00:00:00.000Z"),
				to = Convert.ToDateTime("2016-05-20T00:00:00.000Z"),
				description = "string",
				type = "string",
				id = "573eb110a207e7bf086b6b05",
				userId = "573eac451a7e152a08ccb914"
			};


			Gateway.SetDebugMode (true);
			Console.WriteLine ("test1");
			//Console.WriteLine(Events.Count ().ToString());
			Console.WriteLine(LBXamarinSDK.Gateway.isConnected().ToString());
			Console.WriteLine ("test");



		}

	}
}


