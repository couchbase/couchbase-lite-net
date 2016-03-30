using Couchbase.Lite;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Todo.WPF
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private const string DOCUMENT_DISPLAY_PROPERTY_NAME = "text";
        private const string CHECKBOX_PROPERTY_NAME = "check";
        private const string CREATION_DATE_PROPERTY_NAME = "created_at";

        private LiveQuery _query;
        private Database _db;
        private SimpleViewModel _viewModel;
        private Replication _puller;
        private Replication _pusher;

        public static readonly DependencyProperty TodosProperty 
            = DependencyProperty.Register("Todos", typeof(ObservableCollection<Document>), typeof(MainPage), new FrameworkPropertyMetadata());
        
        public ObservableCollection<Document> Todos
        {
            get { return (ObservableCollection<Document>)GetValue(TodosProperty); }
            set { SetValue(TodosProperty, value); }
        }

        public MainPage()
        {
            DataContext = this; // Because we're inside of a frame.
            Todos = new ObservableCollection<Document>();
            InitializeComponent();
            Loaded += (sender, args) => InitializeCouchbase();

            Dispatcher.ShutdownStarted += (sender, args) =>
            {
                _query.Stop();
                _query.Dispose();
                Manager.SharedInstance.Close();
            };
        }

        private void UpdateReplications(string syncURL)
        {
            
            if (_puller != null)
            {
                _puller.Stop();
            }

            if(_pusher != null)
            {
                _pusher.Stop();
            }

            if (String.IsNullOrEmpty(syncURL))
            {
                return;
            }

            var uri = new Uri(syncURL);
            _puller = _db.CreatePullReplication(uri);
            _puller.Continuous = true;
            _puller.Start();

            _pusher = _db.CreatePushReplication(uri);
            _pusher.Continuous = true;
            _pusher.Start();
        }

        private void InitializeCouchbase()
        {
            ConsoleManager.Show();
            var opts = new DatabaseOptions() {
                Create = true
            };

            // Uncomment this line to get encryption functionality
            // On the source project the storage.sqlcipher.net45 project
            // must be referenced.  On the package project the 
            // Couchbase.Lite.Storage.SQLCipher package must be included
            //opts.EncryptionKey = new SymmetricKey("foo");

            // Uncomment this line to get ForestDB functionality
            // Make sure to either reference the storage.forestdb.net45
            // project or include the Couchbase.Lite.Storage.ForestDB
            // nuget package as necessary
            //opts.StorageType = StorageEngineTypes.ForestDB;

            _db = Manager.SharedInstance.OpenDatabase("wpf-lite", opts);
            _viewModel = new SimpleViewModel(new SimpleModel(Manager.SharedInstance, "wpf-lite"));
            if (_viewModel.SyncURL != null)
            {
                UpdateReplications(_viewModel.SyncURL);
            }

            _viewModel.PropertyChanged += (sender, args) =>
            {
                Console.WriteLine("Replication URL changed to {0}", _viewModel.SyncURL);
                UpdateReplications(_viewModel.SyncURL);
            };

            var view = _db.GetView("todos");
            if (view.Map == null)
            {
                view.SetMap((props, emit) =>
                {
                    object date;
                    if (!props.TryGetValue(CREATION_DATE_PROPERTY_NAME, out date)) {
                        return;
                    }

                    object deleted;
                    if (props.TryGetValue("_deleted", out deleted)) {
                        return;
                    }

                    emit(date, props["text"]);
                }, "1");
            }

            _query = view.CreateQuery().ToLiveQuery();
            _query.Changed += QueryChanged;
            _query.Start();
        }

        private void QueryChanged(object sender, QueryChangeEventArgs e)
        {
            if (Todos.Count == e.Rows.Count) {
                return;
            }

            Console.WriteLine("Query reports {0} rows", e.Rows.Count);
            UpdateDataContextSync(e.Rows);
        }

        private void CreateNewDocument(string text = null)
        {
            // Insert a new doc.
            var props = new Dictionary<string, object>
            {
                { DOCUMENT_DISPLAY_PROPERTY_NAME, text ?? "Create a new todo!" },
                { CHECKBOX_PROPERTY_NAME, false },
                { CREATION_DATE_PROPERTY_NAME, DateTime.Now }
            };

            var doc = _db.CreateDocument();
            doc.PutProperties(props);
        }

        private void UpdateDataContextSync(QueryEnumerator results)
        {
            var rows = results.Select(row => row.Document);
            Todos.Clear();
            foreach (var row in rows) {
                Todos.Add(row);
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Key.Equals(Key.Enter))
                return;

            var box = (TextBox)sender;
            var text = box.Text;
            CreateNewDocument(text);
            box.Clear();
        }

        private void OnRowCheck(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var doc = (Document)checkBox.DataContext;
            doc.Update(rev =>
            {
                var props = rev.UserProperties;
                var existingChecked = (bool)props[CHECKBOX_PROPERTY_NAME];
                if (existingChecked == checkBox.IsChecked) {
                    return false;
                }

                props[CHECKBOX_PROPERTY_NAME] = checkBox.IsChecked;
                rev.SetUserProperties(props);
                return true;
            });
        }

        private void LaunchReplicationConfig(object sender, RoutedEventArgs e)
        {
            var window = new ConfigWindow(_viewModel);
            window.ShowDialog();
        }

        private void ToggleConsoleAction(object sender, RoutedEventArgs e)
        {
            ConsoleManager.Toggle();
        }
    }

    public class DocumentToTextConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType == typeof(String) || targetType == typeof(bool?))
                return ((Document)value).GetProperty(parameter.ToString());
            else
                throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
