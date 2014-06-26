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

using System;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Widget;
using CouchbaseSample.Android;
using CouchbaseSample.Android.Document;
using Couchbase.Lite;
using Sharpen;
using Android.Views;
using Java.Lang;
using System.Linq;

namespace CouchbaseSample.Android
{
    public class ListDrawerFragment : global::Android.Support.V4.App.Fragment
	{
		private const string Tag = "ToDoLite";

		private const string ListsView = "lists_view";

		private const string StateSelectedListId = "selected_list_id";

		private ListDrawerFragment.IListSelectionCallback mCallbacks;

		private ActionBarDrawerToggle mDrawerToggle;

		private DrawerLayout mDrawerLayout;

		private ListView mDrawerListView;

        private global::Android.Views.View mFragmentContainerView;

		private int mCurrentSelectedPosition = -1;

		private ListDrawerFragment.ListsAdapter mListsAdapter;

		private LiveQuery mListsLiveQuery;

		public ListDrawerFragment()
		{
		}

		private Database GetDatabase()
		{
            var application = (Application)Activity.Application;
			return application.GetDatabase();
		}

		private int GetCurrentSelectedPosition(QueryEnumerator enumerator)
		{
			if (enumerator == null)
			{
				return -1;
			}
			var application = (Application)Activity.Application;
			string currentListId = application.GetCurrentListId();
			if (currentListId == null)
			{
				return enumerator.Count() > 0 ? 0 : -1;
			}
			int position = 0;
            foreach(var row in enumerator)
            {
                if (currentListId.Equals(row.Document.Id))
                {
                    break;
                }
                ++position;
            }
			return position;
		}

		private LiveQuery GetLiveQuery()
		{
			LiveQuery query = List.GetQuery(GetDatabase()).ToLiveQuery();
            query.Changed += async (sender, e) =>
            {
                var changedEnumerator = e.Rows;
                mListsAdapter.Update(changedEnumerator);
                int position = GetCurrentSelectedPosition(changedEnumerator
                );
                if (position != -1 && position != mCurrentSelectedPosition)
                {
                    SelectListItem(position, false);
                }
            };
			return query;
		}

		private void RestartLiveQuery()
		{
			if (mListsLiveQuery != null)
			{
				mListsLiveQuery.Stop();
			}
			mListsLiveQuery = GetLiveQuery();
			mListsLiveQuery.Start();
		}

		public virtual void RefreshLists()
		{
			RestartLiveQuery();
		}

		public override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			mListsAdapter = new ListDrawerFragment.ListsAdapter(this, Activity, null);
			RestartLiveQuery();
		}

		public override void OnActivityCreated(Bundle savedInstanceState)
		{
			base.OnActivityCreated(savedInstanceState);
			SetHasOptionsMenu(true);
		}

        public override global::Android.Views.View OnCreateView(LayoutInflater inflater, ViewGroup
			 container, Bundle savedInstanceState)
		{
            mDrawerListView = (ListView)inflater.Inflate(Resource.Layout.fragment_list_drawer, container, false);
            mDrawerListView.ItemClick += async (sender, e) =>
                SelectListItem (e.Position, true);
			
            mDrawerListView.SetAdapter(mListsAdapter);

			if (mCurrentSelectedPosition > mListsAdapter.GetCount())
			{
				mDrawerListView.SetItemChecked(mCurrentSelectedPosition, true);
			}

			return mDrawerListView;
		}

		public virtual bool IsDrawerOpen()
		{
			return mDrawerLayout != null && mDrawerLayout.IsDrawerOpen(mFragmentContainerView);
		}

		/// <summary>Users of this fragment must call this method to set up the navigation drawer interactions.
		/// 	</summary>
		/// <remarks>Users of this fragment must call this method to set up the navigation drawer interactions.
		/// 	</remarks>
		/// <param name="fragmentId">The android:id of this fragment in its activity's layout.
		/// 	</param>
		/// <param name="drawerLayout">The DrawerLayout containing this fragment's UI.</param>
		public virtual void SetUp(int fragmentId, DrawerLayout drawerLayout)
		{
			mFragmentContainerView = Activity.FindViewById(fragmentId);
			mDrawerLayout = drawerLayout;
			mDrawerLayout.SetDrawerShadow(Resource.Drawable.drawer_shadow, GravityCompat.Start);
			ActionBar actionBar = GetActionBar();
			actionBar.SetDisplayHomeAsUpEnabled(true);
			actionBar.SetHomeButtonEnabled(true);
			// ActionBarDrawerToggle ties together the the proper interactions
			// between the navigation drawer and the action bar app icon.

			// calls onPrepareOptionsMenu()
			// calls onPrepareOptionsMenu()
			// Defer code dependent on restoration of previous instance state.
            mDrawerLayout.DrawerOpened += async (sender, e) => {
                mDrawerToggle.OnDrawerOpened(e.DrawerView);
                if (!IsAdded)
                {
                    return;
                }
                Activity.InvalidateOptionsMenu();
                mDrawerToggle.SyncState();
            };
            mDrawerLayout.DrawerClosed += async (sender, e) => {
                mDrawerToggle.OnDrawerClosed(e.DrawerView);
                if (!IsAdded)
                {
                    return;
                }
                Activity.InvalidateOptionsMenu();
                mDrawerToggle.SyncState();
            };

			mDrawerLayout.SetDrawerListener(mDrawerToggle);
		}


		private void SelectListItem(int position, bool closeDrawer)
		{
			mCurrentSelectedPosition = position;
			if (mDrawerListView != null)
			{
				mDrawerListView.SetItemChecked(position, true);
			}
			if (mDrawerLayout != null && closeDrawer)
			{
				mDrawerLayout.CloseDrawer(mFragmentContainerView);
			}
			if (mListsAdapter.GetCount() > position)
			{
				Couchbase.Lite.Document document = (Couchbase.Lite.Document)mListsAdapter.GetItem
					(position);
				Application application = (Application)Activity.Application;
				application.SetCurrentListId(document.Id);
				if (mCallbacks != null)
				{
					mCallbacks.OnListSelected(document.Id);
				}
			}
		}

		public override void OnAttach(Activity activity)
		{
			base.OnAttach(activity);
			try
			{
				mCallbacks = (ListDrawerFragment.IListSelectionCallback)activity;
			}
			catch (InvalidCastException)
			{
				throw new InvalidCastException("Activity must implement NavigationDrawerCallbacks."
					);
			}
		}

		public override void OnDetach()
		{
			base.OnDetach();
			mCallbacks = null;
		}

		public override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutInt(StateSelectedListId, mCurrentSelectedPosition);
		}

		public override void OnConfigurationChanged(Configuration newConfig)
		{
			base.OnConfigurationChanged(newConfig);
			// Forward the new configuration the drawer toggle component.
			mDrawerToggle.OnConfigurationChanged(newConfig);
		}

        public override void OnCreateOptionsMenu(IMenu menu, MenuInflater inflater)
		{
			// If the drawer is open, show the global app actions in the action bar. See also
			// showGlobalContextActionBar, which controls the top-left area of the action bar.
			if (mDrawerLayout != null && IsDrawerOpen())
			{
				inflater.Inflate(Resource.Menu.global, menu);
				ShowGlobalContextActionBar();
			}
			base.OnCreateOptionsMenu(menu, inflater);
		}

        public override bool OnOptionsItemSelected(IMenuItem item)
		{
			if (mDrawerToggle.OnOptionsItemSelected(item))
			{
				return true;
			}
			return base.OnOptionsItemSelected(item);
		}

		/// <summary>
		/// Per the navigation drawer design guidelines, updates the action bar to show the global app
		/// 'context', rather than just what's in the current screen.
		/// </summary>
		/// <remarks>
		/// Per the navigation drawer design guidelines, updates the action bar to show the global app
		/// 'context', rather than just what's in the current screen.
		/// </remarks>
		private void ShowGlobalContextActionBar()
		{
			ActionBar actionBar = GetActionBar();
			actionBar.SetDisplayShowTitleEnabled(true);
            actionBar.NavigationMode = ActionBarNavigationMode.Standard;
			actionBar.SetTitle(Resource.String.app_name);
		}

		private ActionBar GetActionBar()
		{
			return Activity.ActionBar;
		}

		/// <summary>Callbacks interface that all activities using this fragment must implement.
		/// 	</summary>
		/// <remarks>Callbacks interface that all activities using this fragment must implement.
		/// 	</remarks>
		public interface IListSelectionCallback
		{
			void OnListSelected(string id);
		}

		private class ListsAdapter : BaseAdapter
		{
			internal Context context;

			internal QueryEnumerator enumerator;

			public ListsAdapter(ListDrawerFragment _enclosing, Context context, QueryEnumerator
				 enumerator)
			{
				this._enclosing = _enclosing;
				this.context = context;
				this.enumerator = enumerator;
			}

			public int Count
			{
                get { return this.enumerator != null ? this.enumerator.Count() : 0; }
			}

            public override Java.Lang.Object GetItem(int i)
			{
                return (Java.Lang.Object)enumerator.GetRow(i).Document;
			}

			public override long GetItemId(int i)
			{
				return this.enumerator.GetRow(i).SequenceNumber;
			}

            public override global::Android.Views.View GetView(int position, global::Android.Views.View convertView
				, ViewGroup parent)
			{
				if (convertView == null)
				{
					var inflater = (LayoutInflater)parent.Context.GetSystemService(Context.LayoutInflaterService);
					convertView = inflater.Inflate(Resource.Layout.view_list_drawer, null);
				}
				Couchbase.Lite.Document document = (Couchbase.Lite.Document)GetItem(position);
				var textView = (TextView)convertView;
                textView.Text = document["title"];
				return convertView;
			}

			public virtual void Update(QueryEnumerator enumerator)
			{
                ((Activity)this.context).RunOnUiThread(new Action(() =>
                    {
                        this.enumerator = enumerator;
                        NotifyDataSetChanged();
                    }));
			}

			private readonly ListDrawerFragment _enclosing;
		}
	}
}
