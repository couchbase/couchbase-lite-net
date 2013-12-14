// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the Xcode designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;

namespace Sample
{
	[Register ("SampleViewController")]
	partial class SampleViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextView Json { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextView Output { get; set; }

		[Action ("Parse:")]
		partial void Parse (MonoTouch.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (Json != null) {
				Json.Dispose ();
				Json = null;
			}

			if (Output != null) {
				Output.Dispose ();
				Output = null;
			}
		}
	}
}
