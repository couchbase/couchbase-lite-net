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

using Android.Content;
using Android.Graphics;
using Android.Widget;

namespace CouchbaseSample.Android.Helper
{
    /// <summary>Created by Pasin Suriyentrakorn <pasin@couchbase.com> on 4/13/14.</summary>
    /// <remarks>Created by Pasin Suriyentrakorn <pasin@couchbase.com> on 4/13/14.</remarks>
    public class RoundedImageView : ImageView
    {
        private const float Radius = 90;

        public RoundedImageView(Context context) : base(context)
        {
        }

        public RoundedImageView(Context context, AttributeSet attrs) : base(context, attrs
            )
        {
        }

        public RoundedImageView(Context context, AttributeSet attrs, int defStyle) : base
            (context, attrs, defStyle)
        {
        }

        protected override void OnDraw(Canvas canvas)
        {
            Android.Graphics.Drawable.Drawable drawable = GetDrawable();
            if (drawable is BitmapDrawable)
            {
                RectF rectF = new RectF(drawable.GetBounds());
                int restoreCount = canvas.SaveLayer(rectF, null, Canvas.AllSaveFlag);
                GetImageMatrix().MapRect(rectF);
                Paint paint = ((BitmapDrawable)drawable).GetPaint();
                paint.SetAntiAlias(true);
                paint.SetColor(unchecked((int)(0xff000000)));
                canvas.DrawARGB(0, 0, 0, 0);
                canvas.DrawRoundRect(rectF, Radius, Radius, paint);
                Xfermode restoreMode = paint.GetXfermode();
                paint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.SrcIn));
                base.OnDraw(canvas);
                // Restore paint and canvas
                paint.SetXfermode(restoreMode);
                canvas.RestoreToCount(restoreCount);
            }
            else
            {
                base.OnDraw(canvas);
            }
        }
    }
}
