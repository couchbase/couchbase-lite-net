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

using System.Collections.Generic;
using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using CouchbaseSample.Android;
using CouchbaseSample.Android.Document;
using CouchbaseSample.Android.Helper;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;
using System;
using Android.Views;

namespace CouchbaseSample.Android
{
	public class ShareActivity : Activity
	{
		public const string ShareActivityCurrentListIdExtra = "current_list_id";

		public const string StateCurrentListId = "current_list_id";

		private ShareActivity.UserAdapter mAdapter = null;

		private string mCurrentListId = null;

        private Couchbase.Lite.Document mCurrentList = null;

		private Database GetDatabase()
		{
			Application application = (Application)GetApplication();
			return application.GetDatabase();
		}

        public event EventHandler<MenuItemOnMenuItemClickEventArgs> Click;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(R.Layout.activity_share);
			GetActionBar().SetDisplayHomeAsUpEnabled(true);
			if (savedInstanceState != null)
			{
				mCurrentListId = savedInstanceState.GetString(StateCurrentListId);
			}
			else
			{
				Intent intent = GetIntent();
				mCurrentListId = intent.GetStringExtra(ShareActivityCurrentListIdExtra);
			}
			mCurrentList = GetDatabase().GetDocument(mCurrentListId);
			Application application = (Application)GetApplication();
			Query query = Profile.GetQuery(GetDatabase(), application.GetCurrentUserId());
			mAdapter = new ShareActivity.UserAdapter(this, this, query.ToLiveQuery());
			ListView listView = (ListView)FindViewById(R.ID.listView);
			listView.SetAdapter(mAdapter);
		}

		protected override void OnSaveInstanceState(Bundle savedInstanceState)
		{
			savedInstanceState.PutString(StateCurrentListId, mCurrentListId);
			base.OnSaveInstanceState(savedInstanceState);
		}

		public override bool OnCreateOptionsMenu(Menu menu)
		{
			GetMenuInflater().Inflate(R.Menu.share, menu);
			return true;
		}

		public override bool OnOptionsItemSelected(MenuItem item)
		{
			switch (item.GetItemId())
			{
				case R.ID.home:
				{
					Finish();
					return true;
				}
			}
			return base.OnOptionsItemSelected(item);
		}

		protected override void OnDestroy()
		{
			mAdapter.Invalidate();
			base.OnDestroy();
		}

		private class UserAdapter : LiveQueryAdapter
		{
			public UserAdapter(ShareActivity _enclosing, Context context, LiveQuery query) : 
				base(context, query)
			{
				this._enclosing = _enclosing;
			}

			private bool IsMemberOfTheCurrentList(Couchbase.Lite.Document user)
			{
				IList<string> members = (IList<string>)this._enclosing.mCurrentList.GetProperty("members"
					);
				return members != null ? members.Contains(user.GetId()) : false;
			}

			public override global::Android.Views.View GetView(int position, global::Android.Views.View convertView
				, ViewGroup parent)
			{
				if (convertView == null)
				{
					LayoutInflater inflater = (LayoutInflater)parent.GetContext().GetSystemService(Context
						.LayoutInflaterService);
					convertView = inflater.Inflate(R.Layout.view_user, null);
				}
				Couchbase.Lite.Document task = (Couchbase.Lite.Document)this.GetItem(position);
				TextView text = (TextView)convertView.FindViewById(R.ID.text);
				text.SetText((string)task.GetProperty("name"));
				Couchbase.Lite.Document user = (Couchbase.Lite.Document)this.GetItem(position);
				CheckBox checkBox = (CheckBox)convertView.FindViewById(R.ID.@checked);
				bool @checked = this.IsMemberOfTheCurrentList(user);
				checkBox.SetChecked(@checked);
				checkBox.SetOnClickListener(new _OnClickListener_118(this, checkBox, user));
				return convertView;
			}

			private sealed class _OnClickListener_118 : View.OnClickListener
			{
				public _OnClickListener_118(UserAdapter _enclosing, CheckBox checkBox, Couchbase.Lite.Document
					 user)
				{
					this._enclosing = _enclosing;
					this.checkBox = checkBox;
					this.user = user;
				}

				public void OnClick(global::Android.Views.View view)
				{
					try
					{
						if (checkBox.IsChecked())
						{
							List.AddMemberToList(this._enclosing._enclosing.mCurrentList, user);
						}
						else
						{
							List.RemoveMemberFromList(this._enclosing._enclosing.mCurrentList, user);
						}
					}
					catch (CouchbaseLiteException e)
					{
						Log.E(Application.Tag, "Cannot update a member to a list", e);
					}
				}

				private readonly UserAdapter _enclosing;

				private readonly CheckBox checkBox;

				private readonly Couchbase.Lite.Document user;
			}

			private readonly ShareActivity _enclosing;
		}
	}
}
