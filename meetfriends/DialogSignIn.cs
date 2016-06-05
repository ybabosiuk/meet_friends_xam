using System;
using Android.App;
using Android.Views;
using Android.OS;
using Android.Widget;

namespace meetfriends
{
	public class OnSignInEventArgs : EventArgs 
	{
		public string Password { get; set; }
		public string Email {get; set; }

		public OnSignInEventArgs(string email, string password) : base()
		{
			Email = email;
			Password = password;
		}
	}

	public class DialogSignIn : DialogFragment
	{

		private EditText mPassword;
		private EditText mEmail;
		private Button mButtonSignIn;

		public event EventHandler<OnSignInEventArgs> mOnSignInComplete;

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			base.OnCreateView (inflater, container, savedInstanceState);

			var view = inflater.Inflate (Resource.Layout.DialogSignIn, container, false);

			mPassword = view.FindViewById<EditText> (Resource.Id.txtPassword);
			mEmail = view.FindViewById<EditText> (Resource.Id.txtEmail);
			mButtonSignIn = view.FindViewById<Button> (Resource.Id.btnDialogEmail);

			mButtonSignIn.Click += mButtonSignIn_Click;

			return view;
		}

		void mButtonSignIn_Click(object sender, EventArgs e)
		{
			mOnSignInComplete.Invoke(this, new OnSignInEventArgs(mEmail.Text, mPassword.Text));
			this.Dismiss ();
		}

		public override void OnActivityCreated (Bundle savedInstanceState)
		{
			Dialog.Window.RequestFeature (WindowFeatures.NoTitle);

			base.OnActivityCreated (savedInstanceState);

			Dialog.Window.Attributes.WindowAnimations = Resource.Style.dialog_animation;
		}
	}
}

