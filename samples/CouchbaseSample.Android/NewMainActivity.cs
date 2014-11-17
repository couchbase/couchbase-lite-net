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
using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Support.V4.Widget;
using Android.Widget;
using CouchbaseSample.Android;
using CouchbaseSample.Android.Helper;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;
using Android.Runtime;
using CouchbaseSample.Android.Document;
using System.Linq;
using Android.Views;
using Couchbase.Lite.Android;
using Java.Util;
using Java.Lang;
using Android.Text;

namespace CouchbaseSample.Android
{
    public class MainActivity : Activity, ListDrawerFragment.IListSelectionCallback
    {
        private ListDrawerFragment mDrawerFragment;

        private System.String mTitle;

        private Database GetDatabase()
        {
            var application = (CouchbaseSample.Android.Application)Application;
            return application.GetDatabase();
        }

        private string GetCurrentListId()
        {
            var application = (CouchbaseSample.Android.Application)Application;
            string currentListId = application.GetCurrentListId();
            if (currentListId == null)
            {
                try
                {
                    QueryEnumerator enumerator = List.GetQuery(GetDatabase()).Run();
                    if (enumerator.Count() > 0)
                    {
                        currentListId = enumerator.GetRow(0).Document.Id;
                    }
                }
                catch (CouchbaseLiteException)
                {
                }
            }
            return currentListId;
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestWindowFeature(WindowFeatures.Progress);
            SetContentView(Resource.Layout.activity_main);
            mDrawerFragment = (ListDrawerFragment)FragmentManager.FindFragmentById(Resource.Id.
                navigation_drawer);
            mTitle = Title;
            mDrawerFragment.SetUp(Resource.Id.navigation_drawer, (DrawerLayout)FindViewById(Resource.Id.drawer_layout
            ));
            string currentListId = GetCurrentListId();
            if (currentListId != null)
            {
                DisplayListContent(currentListId);
            }
            // Log the current user in and start replication sync
            Application application = (CouchbaseSample.Android.Application)Application;
            application.GetOnSyncProgressChangeObservable().AddObserver(new _Observer_109(this
            ));
            application.GetOnSyncUnauthorizedObservable().AddObserver(new _Observer_125(this)
            );
            // clear the saved user id, since our session is no longer valid
            // and we want to show the login button
            if (application.GetCurrentUserId() != null)
            {
                switch (application.GetAuthenticationType())
                {
                case CouchbaseSample.Android.Application.AuthenticationType.CustomCookie:
                    {
                        // since the user has already logged in before, assume that we
                        // can start sync using the persisted cookie.  if it's expired,
                        // a message will be shown and the user can login.
                        StartSyncWithStoredCustomCookie();
                        break;
                    }

                case CouchbaseSample.Android.Application.AuthenticationType.Facebook:
                    {
                        throw new NotImplementedException();
                        LoginWithFacebookAndStartSync();
                        break;
                    }
                }
            }
        }

        private sealed class _Observer_109 : IObserver
        {
            #region IObserver implementation

            public void Update (Observable observable, Java.Lang.Object data)
            {
                this._enclosing.RunOnUiThread(new _Runnable_112(this, data));
            }

            #endregion

            #region IDisposable implementation

            public void Dispose ()
            {
                throw new NotImplementedException ();
            }

            #endregion

            #region IJavaObject implementation

            public IntPtr Handle {
                get {
                    throw new NotImplementedException ();
                }
            }

            #endregion

            public _Observer_109(MainActivity _enclosing)
            {
                this._enclosing = _enclosing;
            }

            private sealed class _Runnable_112 : IRunnable
            {
                public void Dispose ()
                {
                }

                public IntPtr Handle {
                    get ; set;
                }

                public _Runnable_112(_Observer_109 _enclosing, object data)
                {
                    this._enclosing = _enclosing;
                    this.data = data;
                    this.Handle = _enclosing._enclosing.Handle;
                }

                public void Run()
                {
                    Application.SyncProgress progress = (Application.SyncProgress)data;
                    if (progress.totalCount > 0 && progress.completedCount < progress.totalCount)
                    {
                        this._enclosing._enclosing.SetProgressBarIndeterminateVisibility(true);
                    }
                    else
                    {
                        this._enclosing._enclosing.SetProgressBarIndeterminateVisibility(false);
                    }
                }

                private readonly _Observer_109 _enclosing;

                private readonly object data;
            }

            private readonly MainActivity _enclosing;
        }

        private sealed class _Observer_125 : IObserver
        {
            public _Observer_125(MainActivity _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Update(Observable observable, object data)
            {
                this._enclosing.RunOnUiThread(new _Runnable_128(this));
            }

            private sealed class _Runnable_128 : IRunnable
            {
                public _Runnable_128(_Observer_125 _enclosing)
                {
                    this._enclosing = _enclosing;
                }

                public void Run()
                {
                    Log.D(CouchbaseSample.Android.Application.Tag, "OnSyncUnauthorizedObservable called, show toast");
                    Application application = (CouchbaseSample.Android.Application)this._enclosing._enclosing.GetApplication();
                    application.SetCurrentUserId(null);
                    this._enclosing._enclosing.InvalidateOptionsMenu();
                    string msg = "Sync unable to continue due to invalid session/login";
                    Toast.MakeText(this._enclosing._enclosing.GetApplicationContext(), msg, Toast.LengthLong
                    ).Show();
                }

                private readonly _Observer_125 _enclosing;
            }

            private readonly MainActivity _enclosing;
        }

        private void DisplayListContent(string listDocId)
        {
            Couchbase.Lite.Document document = GetDatabase().GetDocument(listDocId);
            ActionBar.Subtitle = (string)document.GetProperty("title");
            FragmentManager fragmentManager = FragmentManager;
            fragmentManager.BeginTransaction().Replace(Resource.Id.container, MainActivity.TasksFragment
                .NewInstance(listDocId)).Commit();
            Application application = (CouchbaseSample.Android.Application)Application;
            application.SetCurrentListId(listDocId);
        }

        public virtual void OnListSelected(string id)
        {
            DisplayListContent(id);
        }

        public virtual void RestoreActionBar()
        {
            ActionBar actionBar = ActionBar;
            actionBar.NavigationMode = ActionBarNavigationMode.Standard;
            actionBar.SetDisplayShowTitleEnabled(true);
            actionBar.Title = mTitle;
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            if (!mDrawerFragment.IsDrawerOpen())
            {
                MenuInflater.Inflate(Resource.Menu.main, menu);
                // Add Login button if the user has not been logged in.
                Application application = (CouchbaseSample.Android.Application)Application;
                if (application.GetCurrentUserId() == null)
                {
                    var shareMenuItem = menu.Add(Resource.String.action_login);
                    shareMenuItem.SetShowAsAction(ShowAsAction.Never);
                    shareMenuItem.SetOnMenuItemClickListener(new _OnMenuItemClickListener_198(this));
                }
                // Add Share button if the user has been logged in
                if (application.GetCurrentUserId() != null && GetCurrentListId() != null)
                {
                    IMenuItem shareMenuItem = menu.Add(Resources.GetString(Resource.String.action_share));
                    shareMenuItem.SetShowAsAction(ShowAsAction.Never);
                    shareMenuItem.SetOnMenuItemClickListener(new _OnMenuItemClickListener_220(this));
                }
                RestoreActionBar();
                return true;
            }
            return base.OnCreateOptionsMenu(menu);
        }

        private sealed class _OnMenuItemClickListener_198 : global::Android.Widget.PopupMenu.IOnMenuItemClickListener
        {
            public _OnMenuItemClickListener_198(MainActivity _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public bool OnMenuItemClick(IMenuItem menuItem)
            {
                Application application = (Application)this._enclosing.Application;
                switch (application.GetAuthenticationType())
                {
                case Application.AuthenticationType.CustomCookie:
                    {
                        this._enclosing.LoginWithCustomCookieAndStartSync();
                        break;
                    }

                case Application.AuthenticationType.Facebook:
                    {
                        this._enclosing.LoginWithFacebookAndStartSync();
                        break;
                    }
                }
                this._enclosing.InvalidateOptionsMenu();
                return true;
            }

            private readonly MainActivity _enclosing;
        }

        private sealed class _OnMenuItemClickListener_220 : global::Android.Widget.PopupMenu.IOnMenuItemClickListener
        {
            public void Dispose ()
            {
                throw new NotImplementedException ();
            }

            public IntPtr Handle {
                get {
                    throw new NotImplementedException ();
                }
            }

            public _OnMenuItemClickListener_220(MainActivity _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public bool OnMenuItemClick(IMenuItem menuItem)
            {
                Intent intent = new Intent(this._enclosing, typeof(ShareActivity));
                intent.PutExtra(ShareActivity.ShareActivityCurrentListIdExtra, this._enclosing.GetCurrentListId
                    ());
                this._enclosing.StartActivity(intent);
                return true;
            }

            private readonly MainActivity _enclosing;
        }

        private void CreateNewList()
        {
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetTitle(Resources.GetString(Resource.String.title_dialog_new_list));
            EditText input = new EditText(this);
            input.SetMaxLines(1);
            input.SetSingleLine(true);
            input.SetHint(Resource.String.hint_new_list);
            alert.SetView(input);
            alert.SetPositiveButton("Ok", new _OnClickListener_248(this, input));
            // TODO: Show an error message.
            alert.SetNegativeButton("Cancel", new _OnClickListener_266());
            alert.Show();
        }

        private sealed class _OnClickListener_248 : IDialogInterfaceOnClickListener
        {
            public _OnClickListener_248(MainActivity _enclosing, EditText input)
            {
                this._enclosing = _enclosing;
                this.input = input;
            }

            public void OnClick(IDialogInterface dialog, int whichButton)
            {
                string title = ((IEditable)input).Text.ToString();
                if (title.Length == 0)
                {
                    return;
                }
                try
                {
                    string currentUserId = ((Application)this._enclosing.GetApplication()).GetCurrentUserId
                        ();
                    Couchbase.Lite.Document document = List.CreateNewList(this._enclosing.GetDatabase
                        (), title, currentUserId);
                    this._enclosing.DisplayListContent(document.GetId());
                    this._enclosing.InvalidateOptionsMenu();
                }
                catch (CouchbaseLiteException e)
                {
                    Log.E(Application.Tag, "Cannot create a new list", e);
                }
            }

            private readonly MainActivity _enclosing;

            private readonly EditText input;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.Item();
            if (id == Resource.Id.action_new_list)
            {
                CreateNewList();
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        protected override void OnActivityResult(int requestCode, int resultCode, Intent 
            data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            Session.GetActiveSession().OnActivityResult(this, requestCode, resultCode, data);
        }

        private Session.OpenRequest GetFacebookOpenRequest()
        {
            Session.OpenRequest request = ((Session.OpenRequest)((Session.OpenRequest)new Session.OpenRequest
                (this).SetPermissions(Arrays.AsList("email"))).SetCallback(statusCallback));
            return request;
        }

        private void LoginWithFacebookAndStartSync()
        {
            Session session = Session.GetActiveSession();
            if (session == null)
            {
                session = new Session(this);
                Session.SetActiveSession(session);
            }
            if (!session.IsOpened() && !session.IsClosed())
            {
                session.OpenForRead(GetFacebookOpenRequest());
            }
            else
            {
                Session.OpenActiveSession(this, true, statusCallback);
            }
        }

        /// <summary>
        /// This is allows the user to enter a "fake" cookie, that would have to been
        /// obtained manually.
        /// </summary>
        /// <remarks>
        /// This is allows the user to enter a "fake" cookie, that would have to been
        /// obtained manually.  In a real app, this would look like:
        /// - Your app prompts user for credentials
        /// - Your app directly contacts your app server with these credentials
        /// - Your app server creates a session on the Sync Gateway, which returns a cookie
        /// - Your app server returns this cookie to your app
        /// Having obtained the cookie in the manner above, you would then call
        /// startSyncWithCustomCookie() with this cookie.
        /// </remarks>
        private void LoginWithCustomCookieAndStartSync()
        {
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            // if it's too much typing in the emulator, just replace hardcoded fake cookie here.
            string hardcodedFakeCookie = "376b9c707158a381a143060f1937935ede7cf75d";
            alert.SetTitle("Enter fake cookie");
            alert.SetMessage("See loginWithCustomCookieAndStartSync() for explanation.");
            // Set an EditText view to get user input
            EditText input = new EditText(this);
            input.SetText(hardcodedFakeCookie);
            alert.SetView(input);
//            alert.SetPositiveButton("Ok", new _OnClickListener_339(this, input));
//            alert.SetNegativeButton("Cancel", new _OnClickListener_350());
            // Canceled.
            alert.Show();
        }

//        private sealed class _OnClickListener_339 : DialogInterface.OnClickListener
//        {
//            public _OnClickListener_339(MainActivity _enclosing, EditText input)
//            {
//                this._enclosing = _enclosing;
//                this.input = input;
//            }
//
//            public void OnClick(DialogInterface dialog, int whichButton)
//            {
//                string value = ((Editable)input.GetText()).ToString();
//                Application application = (Application)this._enclosing.GetApplication();
//                application.SetCurrentUserId(value);
//                this._enclosing.StartSyncWithCustomCookie(value);
//            }
//
//            private readonly MainActivity _enclosing;
//
//            private readonly EditText input;
//        }
//
//        private sealed class _OnClickListener_350 : DialogInterface.OnClickListener
//        {
//            public _OnClickListener_350()
//            {
//            }
//
//            public void OnClick(DialogInterface dialog, int whichButton)
//            {
//            }
//        }

        private void StartSyncWithCustomCookie(string cookieVal)
        {
            string cookieName = "SyncGatewaySession";
            bool isSecure = false;
            bool httpOnly = false;
            // expiration date - 1 day from now
            Calendar cal = Calendar.GetInstance();
            cal.Time = new DateTime();
            int numDaysToAdd = 1;
            cal.Add(Calendar.Date, numDaysToAdd);
            DateTime expirationDate = cal.Time;
            Application application = (Application)this.Application;
            application.StartReplicationSyncWithCustomCookie(cookieName, cookieVal, "/", expirationDate
                , isSecure, httpOnly);
        }

        private void StartSyncWithStoredCustomCookie()
        {
            Application application = (Application)this.Application;
            application.StartReplicationSyncWithStoredCustomCookie();
        }

//        private class FacebookSessionStatusCallback : Session.StatusCallback
//        {
//            public virtual void Call(Session session, SessionState state, Exception exception
//            )
//            {
//                if (session == null || !session.IsOpened())
//                {
//                    return;
//                }
//                Request.NewMeRequest(session, new _GraphUserCallback_390(this, session)).ExecuteAsync
//                ();
//            }
//
//            private sealed class _GraphUserCallback_390 : Request.GraphUserCallback
//            {
//                public _GraphUserCallback_390(FacebookSessionStatusCallback _enclosing, Session session
//                )
//                {
//                    this._enclosing = _enclosing;
//                    this.session = session;
//                }
//
//                public void OnCompleted(GraphUser user, Response response)
//                {
//                    if (user != null)
//                    {
//                        string userId = (string)user.GetProperty("email");
//                        string name = (string)user.GetName();
//                        Application application = (Application)this._enclosing._enclosing.GetApplication(
//                        );
//                        string currentUserId = application.GetCurrentUserId();
//                        if (currentUserId != null && !currentUserId.Equals(userId))
//                        {
//                        }
//                        //TODO: Update Database and all UIs
//                        Couchbase.Lite.Document profile = null;
//                        try
//                        {
//                            profile = Profile.GetUserProfileById(this._enclosing._enclosing.GetDatabase(), userId
//                            );
//                            if (profile == null)
//                            {
//                                profile = Profile.CreateProfile(this._enclosing._enclosing.GetDatabase(), userId, 
//                                    name);
//                            }
//                        }
//                        catch (CouchbaseLiteException)
//                        {
//                        }
//                        try
//                        {
//                            List.AssignOwnerToListsIfNeeded(this._enclosing._enclosing.GetDatabase(), profile
//                            );
//                        }
//                        catch (CouchbaseLiteException)
//                        {
//                        }
//                        application.SetCurrentUserId(userId);
//                        application.StartReplicationSyncWithFacebookLogin(session.GetAccessToken(), userId
//                        );
//                        this._enclosing._enclosing.InvalidateOptionsMenu();
//                    }
//                }
//
//                private readonly FacebookSessionStatusCallback _enclosing;
//
//                private readonly Session session;
//            }
//
//            internal FacebookSessionStatusCallback(MainActivity _enclosing)
//            {
//                this._enclosing = _enclosing;
//            }
//
//            private readonly MainActivity _enclosing;
//        }

        public class TasksFragment : Fragment
        {
            private const string ArgListDocId = "id";

            private const int RequestTakePhoto = 1;

            private const int RequestChoosePhoto = 2;

            private const int ThumbnailSizePx = 150;

            private MainActivity.TasksFragment.TaskAdapter mAdapter;

            private string mImagePathToBeAttached;

            private Bitmap mImageToBeAttached;

            private Couchbase.Lite.Document mCurrentTaskToAttachImage;

            public static MainActivity.TasksFragment NewInstance(string id)
            {
                MainActivity.TasksFragment fragment = new MainActivity.TasksFragment();
                Bundle args = new Bundle();
                args.PutString(ArgListDocId, id);
                fragment.SetArguments(args);
                return fragment;
            }

            public TasksFragment()
            {
            }

            private Database GetDatabase()
            {
                Application application = (Application)Activity.GetApplication();
                return application.GetDatabase();
            }

            /// <exception cref="System.IO.IOException"></exception>
            private FilePath CreateImageFile()
            {
                string timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss").Format(new DateTime());
                string fileName = "TODO_LITE_" + timeStamp + "_";
                FilePath storageDir = Environment.GetExternalStoragePublicDirectory(Environment.DirectoryPictures
                );
                FilePath image = FilePath.CreateTempFile(fileName, ".jpg", storageDir);
                mImagePathToBeAttached = image.GetAbsolutePath();
                return image;
            }

            private void DispatchTakePhotoIntent()
            {
                Intent takePictureIntent = new Intent(MediaStore.ActionImageCapture);
                if (takePictureIntent.ResolveActivity(Activity.GetPackageManager()) != null)
                {
                    FilePath photoFile = null;
                    try
                    {
                        photoFile = CreateImageFile();
                    }
                    catch (IOException e)
                    {
                        Log.E(Application.Tag, "Cannot create a temp image file", e);
                    }
                    if (photoFile != null)
                    {
                        takePictureIntent.PutExtra(MediaStore.ExtraOutput, Uri.FromFile(photoFile));
                        StartActivityForResult(takePictureIntent, RequestTakePhoto);
                    }
                }
            }

            private void DispatchChoosePhotoIntent()
            {
                Intent intent = new Intent(Intent.ActionPick, MediaStore.Images.Media.ExternalContentUri
                );
                intent.SetType("image/*");
                StartActivityForResult(Intent.CreateChooser(intent, "Select File"), RequestChoosePhoto
                );
            }

            private void DeleteCurrentPhoto()
            {
                if (mImageToBeAttached != null)
                {
                    mImageToBeAttached.Recycle();
                    mImageToBeAttached = null;
                    ViewGroup createTaskPanel = (ViewGroup)Activity.FindViewById(Resource.Id.create_task
                    );
                    ImageView imageView = (ImageView)createTaskPanel.FindViewById(Resource.Id.image);
                    imageView.SetImageDrawable(GetResources().GetDrawable(Resource.Drawable.ic_camera));
                }
            }

            private void AttachImage(Couchbase.Lite.Document task)
            {
                CharSequence[] items;
                if (mImageToBeAttached != null)
                {
                    items = new CharSequence[] { "Take photo", "Choose photo", "Delete photo" };
                }
                else
                {
                    items = new CharSequence[] { "Take photo", "Choose photo" };
                }
                var builder = new AlertDialog.Builder(Activity);
                builder.SetTitle("Add picture");
                builder.SetItems(items, (sender, args)=>
                    {
                        var item = args.Which;
                        if (item == 0)
                        {
                            mCurrentTaskToAttachImage = task;
                            DispatchTakePhotoIntent();
                        }
                        else
                        {
                            if (item == 1)
                            {
                                mCurrentTaskToAttachImage = task;
                                DispatchChoosePhotoIntent();
                            }
                            else
                            {
                                DeleteCurrentPhoto();
                            }
                        }
                    });
                builder.Show();
            }
                
            private void DispatchImageViewIntent(Bitmap image)
            {
                ByteArrayOutputStream stream = new ByteArrayOutputStream();
                image.Compress(Bitmap.CompressFormat.Jpeg, 50, stream);
                byte[] byteArray = stream.ToByteArray();
                long l = byteArray.Length;
                Intent intent = new Intent(Activity, typeof(ImageViewActivity));
                intent.PutExtra(ImageViewActivity.IntentImage, byteArray);
                StartActivity(intent);
            }

            private void DeleteTask(int position)
            {
                Couchbase.Lite.Document task = (Couchbase.Lite.Document)mAdapter.GetItem(position
                );
                try
                {
                    Task.DeleteTask(task);
                }
                catch (CouchbaseLiteException e)
                {
                    Log.E(Application.Tag, "Cannot delete a task", e);
                }
            }

            public override global::Android.Views.View OnCreateView(LayoutInflater inflater, ViewGroup
                container, Bundle savedInstanceState)
            {
                ListView listView = (ListView)inflater.Inflate(Resource.Layout.fragment_main, container, 
                    false);
                string listId = Arguments.GetString(ArgListDocId);
                ViewGroup header = (ViewGroup)inflater.Inflate(Resource.Layout.view_task_create, listView
                    , false);
                ImageView imageView = (ImageView)header.FindViewById(Resource.Id.image);
                imageView.SetOnClickListener(new _OnClickListener_560(this));
                EditText text = (EditText)header.FindViewById(Resource.Id.text);
                text.SetOnKeyListener(new _OnKeyListener_568(this, text, listId));
                // Reset text and current selected photo if available.
                listView.AddHeaderView(header);
                listView.SetOnItemClickListener(new _OnItemClickListener_594());
                listView.SetOnItemLongClickListener(new _OnItemLongClickListener_608(this));
                LiveQuery query = Task.GetQuery(GetDatabase(), listId).ToLiveQuery();
                mAdapter = new MainActivity.TasksFragment.TaskAdapter(this, Activity, query);
                listView.Adapter = (mAdapter);
                return listView;
            }

            private sealed class _OnClickListener_560 : Android.Support.V4.View.OnClickListener
            {
                public _OnClickListener_560(TasksFragment _enclosing)
                {
                    this._enclosing = _enclosing;
                }

                public void OnClick(global::Android.Views.View v)
                {
                    this._enclosing.AttachImage(null);
                }

                private readonly TasksFragment _enclosing;
            }

            private sealed class _OnKeyListener_568 : Android.Support.V4.View.IOnKeyListener
            {
                public _OnKeyListener_568(TasksFragment _enclosing, EditText text, string listId)
                {
                    this._enclosing = _enclosing;
                    this.text = text;
                    this.listId = listId;
                }

                public bool OnKey(Android.Support.V4.View view, int i, KeyEvent keyEvent)
                {
                    if ((keyEvent.Action == KeyEventActions.Down) && (keyEvent.KeyCode == Keycode.Enter))
                    {
                        string inputText = (text.Text);//.ToString();
                        if (inputText.Length > 0)
                        {
                            try
                            {
                                Task.CreateTask(this._enclosing.GetDatabase(), inputText, this._enclosing.mImageToBeAttached
                                    , listId);
                            }
                            catch (CouchbaseLiteException e)
                            {
                                Log.E(Application.Tag, "Cannot create new task", e);
                            }
                        }
                        text.Text = (string.Empty);
                        this._enclosing.DeleteCurrentPhoto();
                        return true;
                    }
                    return false;
                }

                private readonly TasksFragment _enclosing;

                private readonly EditText text;

                private readonly string listId;

                public bool OnKey(global::Android.Views.View v, Keycode keyCode, KeyEvent e)
                {
                    throw new NotImplementedException();
                }

                public IntPtr Handle
                {
                    get { throw new NotImplementedException(); }
                }

                public void Dispose()
                {
                    throw new NotImplementedException();
                }
            }

            private sealed class _OnItemClickListener_594 : AdapterView.IOnItemClickListener
            {
                public _OnItemClickListener_594()
                {
                }

                public void OnItemClick<_T0>(AdapterView<_T0> adapter, global::Android.Views.View view, int
                    position, long id) where _T0:Adapter
                {
                    Couchbase.Lite.Document task = (Couchbase.Lite.Document)adapter.GetItemAtPosition
                        (position);
                    bool @checked = ((bool)task.GetProperty("checked"));
                    try
                    {
                        Task.UpdateCheckedStatus(task, @checked);
                    }
                    catch (CouchbaseLiteException e)
                    {
                        Log.E(Application.Tag, "Cannot update checked status", e);
                        Sharpen.Runtime.PrintStackTrace(e);
                    }
                }
            }

            private sealed class _OnItemLongClickListener_608 : AdapterView.IOnItemLongClickListener
            {
                public _OnItemLongClickListener_608(TasksFragment _enclosing)
                {
                    this._enclosing = _enclosing;
                }

                public bool OnItemLongClick<_T0>(AdapterView<_T0> parent, global::Android.Views.View view, 
                    int position, long id) where _T0:Adapter
                {
                    PopupMenu popup = new PopupMenu(this._enclosing.Activity, view);
                    popup.GetMenu().Add(this._enclosing.Resources.GetString(Resource.String.action_delete
                    ));
                    popup.SetOnMenuItemClickListener(new _OnMenuItemClickListener_614(this, position)
                    );
                    popup.Show();
                    return true;
                }

                private sealed class _OnMenuItemClickListener_614 : PopupMenu.IOnMenuItemClickListener
                {
                    public _OnMenuItemClickListener_614(_OnItemLongClickListener_608 _enclosing, int 
                        position)
                    {
                        this._enclosing = _enclosing;
                        this.position = position;
                    }

                    public bool OnMenuItemClick(IMenuItem item)
                    {
                        this._enclosing._enclosing.DeleteTask(position - 1);
                        return true;
                    }

                    private readonly _OnItemLongClickListener_608 _enclosing;

                    private readonly int position;
                }

                private readonly TasksFragment _enclosing;
            }

            public override void OnActivityResult(int requestCode, int resultCode, Intent data
            )
            {
                base.OnActivityResult(requestCode, resultCode, data);
                if (resultCode != ResultOk)
                {
                    if (mCurrentTaskToAttachImage != null)
                    {
                        mCurrentTaskToAttachImage = null;
                    }
                    return;
                }
                if (requestCode == RequestTakePhoto)
                {
                    mImageToBeAttached = BitmapFactory.DecodeFile(mImagePathToBeAttached);
                    // Delete the temporary image file
                    FilePath file = new FilePath(mImagePathToBeAttached);
                    file.Delete();
                    mImagePathToBeAttached = null;
                }
                else
                {
                    if (requestCode == RequestChoosePhoto)
                    {
                        try
                        {
                            Uri uri = data.GetData();
                            mImageToBeAttached = MediaStore.Images.Media.GetBitmap(Activity.GetContentResolver
                                (), uri);
                        }
                        catch (IOException e)
                        {
                            Log.E(Application.Tag, "Cannot get a selected photo from the gallery.", e);
                        }
                    }
                }
                if (mImageToBeAttached != null)
                {
                    if (mCurrentTaskToAttachImage != null)
                    {
                        try
                        {
                            Task.AttachImage(mCurrentTaskToAttachImage, mImageToBeAttached);
                        }
                        catch (CouchbaseLiteException e)
                        {
                            Log.E(Application.Tag, "Cannot attach an image to a task.", e);
                        }
                    }
                    else
                    {
                        // Attach an image for a new task
                        Bitmap thumbnail = ThumbnailUtils.ExtractThumbnail(mImageToBeAttached, ThumbnailSizePx
                            , ThumbnailSizePx);
                        ImageView imageView = (ImageView)Activity.FindViewById(Resource.Id.image);
                        imageView.SetImageBitmap(thumbnail);
                    }
                }
                // Ensure resetting the task to attach an image
                if (mCurrentTaskToAttachImage != null)
                {
                    mCurrentTaskToAttachImage = null;
                }
            }

            private class TaskAdapter : LiveQueryAdapter
            {
                public TaskAdapter(TasksFragment _enclosing, Context context, LiveQuery query) : 
                base(context, query)
                {
                    this._enclosing = _enclosing;
                }

                public override global::Android.Views.View GetView(int position, global::Android.Views.View convertView
                    , ViewGroup parent)
                {
                    if (convertView == null)
                    {
                        LayoutInflater inflater = (LayoutInflater)parent.GetContext().GetSystemService(Context
                            .LayoutInflaterService);
                        convertView = inflater.Inflate(Resource.Layout.view_task, null);
                    }
                    Couchbase.Lite.Document task = (Couchbase.Lite.Document)this.GetItem(position);
                    Bitmap image = null;
                    Bitmap thumbnail = null;
                    IList<Attachment> attachments = task.GetCurrentRevision().GetAttachments();
                    if (attachments != null && attachments.Count > 0)
                    {
                        Attachment attachment = attachments[0];
                        try
                        {
                            image = BitmapFactory.DecodeStream(attachment.GetContent());
                            thumbnail = ThumbnailUtils.ExtractThumbnail(image, MainActivity.TasksFragment.ThumbnailSizePx
                                , MainActivity.TasksFragment.ThumbnailSizePx);
                        }
                        catch (Exception e)
                        {
                            Log.E(Application.Tag, "Cannot decode the attached image", e);
                        }
                    }
                    Bitmap displayImage = image;
                    ImageView imageView = (ImageView)convertView.FindViewById(Resource.Id.image);
                    if (thumbnail != null)
                    {
                        imageView.SetImageBitmap(thumbnail);
                    }
                    else
                    {
                        imageView.SetImageDrawable(this._enclosing.GetResources().GetDrawable(Resource.Drawable.
                            ic_camera_light));
                    }
                    imageView.Click += async (sender, e) => {
                        if (displayImage != null)
                        {
                            DispatchImageViewIntent(displayImage);
                        }
                        else
                        {
                            AttachImage(task);
                        }
                    }; 
                    TextView text = (TextView)convertView.FindViewById(Resource.Id.text);
                    text.SetText((string)task.GetProperty("title"));
                    CheckBox checkBox = (CheckBox)convertView.FindViewById(Resource.Id.@checked);
                    bool checkedProperty = (bool)task.GetProperty("checked");
                    bool @checked = checkedProperty != null ? checkedProperty : false;
                    checkBox.SetChecked(@checked);
                    checkBox.Click += async (sender, e) => {
                        try
                        {
                            Task.UpdateCheckedStatus(task, checkBox.IsChecked());
                        }
                        catch (CouchbaseLiteException ex)
                        {
                            Log.E(((CouchbaseSample.Android.Application)Application).Tag, "Cannot update checked status", e);
                        }
                    };
                    return convertView;
                }

                private readonly TasksFragment _enclosing;
            }
        }

        public MainActivity()
        {
//            statusCallback = new MainActivity.FacebookSessionStatusCallback(this);
        }
    }
}
