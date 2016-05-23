using System;
using Android.App;
using Android.Views;
using Android.OS;

namespace meetfriends
{
	public class DialogSignUp : DialogFragment
	{
		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			base.OnCreateView (inflater, container, savedInstanceState);

			var view = inflater.Inflate (Resource.Layout.DialogSignUp, container, false);

			return view;
		}
	}
}

