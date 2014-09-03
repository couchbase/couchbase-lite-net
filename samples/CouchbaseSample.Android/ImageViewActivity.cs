/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using CouchbaseSample.Android;
using Sharpen;

namespace CouchbaseSample.Android
{
    public class ImageViewActivity : Activity
    {
        public const string IntentImage = "image";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestWindowFeature(Window.FeatureNoTitle);
            GetWindow().SetFlags(WindowManager.LayoutParams.FlagFullscreen, WindowManager.LayoutParams
                .FlagFullscreen);
            SetContentView(R.Layout.activity_image_view);
            byte[] byteArray = GetIntent().GetByteArrayExtra("image");
            Bitmap image = BitmapFactory.DecodeByteArray(byteArray, 0, byteArray.Length);
            ImageView imageView = (ImageView)FindViewById(R.ID.image);
            imageView.SetImageBitmap(image);
            imageView.SetOnClickListener(new _OnClickListener_33(this));
        }

        private sealed class _OnClickListener_33 : View.OnClickListener
        {
            public _OnClickListener_33(ImageViewActivity _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void OnClick(Android.View.View v)
            {
                this._enclosing.Finish();
            }

            private readonly ImageViewActivity _enclosing;
        }

        public override bool OnCreateOptionsMenu(Menu menu)
        {
            GetMenuInflater().Inflate(R.Menu.image_view, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(MenuItem item)
        {
            return base.OnOptionsItemSelected(item);
        }
    }
}
