/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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

using Couchbase;
using Sharpen;

namespace Couchbase
{
	public sealed class R
	{
		public sealed class Attr
		{
		}

		public sealed class Dimen
		{
			/// <summary>Default screen margins, per the Android Design guidelines.</summary>
			/// <remarks>
			/// Default screen margins, per the Android Design guidelines.
			/// Customize dimensions originally defined in res/values/dimens.xml (such as
			/// screen margins) for sw720dp devices (e.g. 10" tablets) in landscape here.
			/// </remarks>
			public const int activity_horizontal_margin = unchecked((int)(0x7f040000));

			public const int activity_vertical_margin = unchecked((int)(0x7f040001));
		}

		public sealed class Drawable
		{
			public const int ic_launcher = unchecked((int)(0x7f020000));
		}

		public sealed class ID
		{
			public const int action_settings = unchecked((int)(0x7f080000));
		}

		public sealed class Layout
		{
			public const int activity_main = unchecked((int)(0x7f030000));
		}

		public sealed class Menu
		{
			public const int main = unchecked((int)(0x7f070000));
		}

		public sealed class String
		{
			public const int action_settings = unchecked((int)(0x7f050001));

			public const int app_name = unchecked((int)(0x7f050000));

			public const int hello_world = unchecked((int)(0x7f050002));
		}

		public sealed class Style
		{
			/// <summary>Base application theme, dependent on API level.</summary>
			/// <remarks>
			/// Base application theme, dependent on API level. This theme is replaced
			/// by AppBaseTheme from res/values-vXX/styles.xml on newer devices.
			/// Theme customizations available in newer API levels can go in
			/// res/values-vXX/styles.xml, while customizations related to
			/// backward-compatibility can go here.
			/// Base application theme for API 11+. This theme completely replaces
			/// AppBaseTheme from res/values/styles.xml on API 11+ devices.
			/// API 11 theme customizations can go here.
			/// Base application theme for API 14+. This theme completely replaces
			/// AppBaseTheme from BOTH res/values/styles.xml and
			/// res/values-v11/styles.xml on API 14+ devices.
			/// API 14 theme customizations can go here.
			/// </remarks>
			public const int AppBaseTheme = unchecked((int)(0x7f060000));

			/// <summary>Application theme.</summary>
			/// <remarks>
			/// Application theme.
			/// All customizations that are NOT specific to a particular API-level can go here.
			/// </remarks>
			public const int AppTheme = unchecked((int)(0x7f060001));
		}
	}
}
