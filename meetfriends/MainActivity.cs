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
using Android.Views;
using Newtonsoft.Json;

namespace meetfriends
{
	[Activity (Label = "meetfriends", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{

		private Button mBtnSingUp; 
		private ProgressBar mProgressBar;
		private Button mBtnSingIn;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
//
//			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			mBtnSingIn = FindViewById<Button> (Resource.Id.btnSignIn);
			mBtnSingUp = FindViewById<Button> (Resource.Id.btnSignUp);
			mProgressBar = FindViewById<ProgressBar> (Resource.Id.progressBar1);

			mBtnSingUp.Click += (object sender, EventArgs e) => {
				//Pull up dialog
				FragmentTransaction transaction = FragmentManager.BeginTransaction();
				DialogSignUp signUpDialog = new DialogSignUp();
				signUpDialog.Show(transaction, "Dialog Fragment");

				signUpDialog.mOnSignUpComplete += SignUpDialog_mOnSignUpComplete;

			};

			mBtnSingIn.Click += (object sender, EventArgs e) => {
				//Pull up dialog
				FragmentTransaction transaction = FragmentManager.BeginTransaction();
				DialogSignIn signInDialog = new DialogSignIn();
				signInDialog.Show(transaction, "Dialog Fragment");

				signInDialog.mOnSignInComplete += SignInDialog_mOnSignInComplete;

			};

		}

		void SignUpDialog_mOnSignUpComplete (object sender, OnSignUpEventArgs e)
		{
			Console.WriteLine (e.Email);
			Console.WriteLine (e.Password);

			mProgressBar.Visibility = ViewStates.Visible;

			Thread thread = new Thread(RequestMethod);
			thread.Start ();

		}


		async void SignInDialog_mOnSignInComplete (object sender, OnSignInEventArgs e)
		{
			Console.WriteLine (e.Email);
			Console.WriteLine (e.Password);

			mProgressBar.Visibility = ViewStates.Visible;

			var credentials = new LBXamarinSDK.User ();
			credentials.email = e.Email;
			credentials.password = e.Password;
			
			mProgressBar.Visibility = ViewStates.Visible;
			credentials = await Users.login (credentials);
		
			mProgressBar.Visibility = ViewStates.Invisible;
		
			Console.WriteLine("end");
		}


		private void RequestMethod(){
			Thread.Sleep (3000);

			RunOnUiThread (() => {
				mProgressBar.Visibility = ViewStates.Invisible;
			});

		}

	}
}


