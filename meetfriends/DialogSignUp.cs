using System;
using Android.App;
using Android.Views;
using Android.OS;
using Android.Widget;

namespace meetfriends
{
	public class OnSignUpEventArgs : EventArgs 
	{
		public string FirstName { get; set; }
		public string Password { get; set; }
		public string Email {get; set; }

		public OnSignUpEventArgs(string firstName, string email, string password) : base()
		{
			FirstName = firstName;
			Email = email;
			Password = password;
		}
	}

	public class DialogSignUp : DialogFragment
	{

		private EditText mFirstName;
		private EditText mPassword;
		private EditText mEmail;
		private Button mButtonSignUp;

		public event EventHandler<OnSignUpEventArgs> mOnSignUpComplete;

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			base.OnCreateView (inflater, container, savedInstanceState);

			var view = inflater.Inflate (Resource.Layout.DialogSignUp, container, false);

			mFirstName = view.FindViewById<EditText> (Resource.Id.txtFirstName);
			mPassword = view.FindViewById<EditText> (Resource.Id.txtPassword);
			mEmail = view.FindViewById<EditText> (Resource.Id.txtEmail);
			mButtonSignUp = view.FindViewById<Button> (Resource.Id.btnDialogEmail);

			mButtonSignUp.Click += mButtonSignUp_Click;

			return view;
		}

		void mButtonSignUp_Click(object sender, EventArgs e)
		{
			mOnSignUpComplete.Invoke(this, new OnSignUpEventArgs(mFirstName.Text, mEmail.Text, mPassword.Text));
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

