using System;
using System.Text;
using Android.App;
using Android.Widget;
using Android.OS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Json.NET.Android.Sample
{
	[Activity(Label = "Json.NET.Android.Sample", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		private EditText json;
		private TextView output;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.Main);

			FindViewById<Button> (Resource.Id.Parse).Click += OnClickParse;
			this.json = FindViewById<EditText> (Resource.Id.Json);
			this.output = FindViewById<TextView> (Resource.Id.Output);
		}

		private void OnClickParse (object sender, EventArgs eventArgs)
		{
			try
			{
				JObject obj = JObject.Parse (this.json.Text);

				StringBuilder builder = new StringBuilder();
				foreach (var kvp in obj)
					builder.AppendLine (kvp.Key + " = " + kvp.Value);

				this.output.Text = builder.ToString();
			}
			catch (FormatException ex)
			{
				this.output.Text = ex.ToString();
			}
			catch (JsonException ex)
			{
				this.output.Text = ex.ToString();
			}
		}
	}
}

