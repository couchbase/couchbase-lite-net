using System;
using System.Drawing;
using System.Text;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sample
{
	public partial class SampleViewController : UIViewController
	{
		public SampleViewController () : base ("SampleViewController", null)
		{
		}
		
		partial void Parse (NSObject sender)
		{
			string json = Json.Text;
			try
			{
				JObject obj = JObject.Parse (json);

				StringBuilder builder = new StringBuilder();
				foreach (var kvp in obj)
					builder.AppendLine (String.Format ("{0} = {1}", kvp.Key, kvp.Value));

				Output.Text = builder.ToString();
			}
			catch (FormatException fex)
			{
				Output.Text = fex.ToString();
			}
			catch (JsonException jex)
			{
				Output.Text = jex.ToString();
			}

			Json.ResignFirstResponder();
		}
		
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			// Return true for supported orientations
			return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
		}
	}
}

