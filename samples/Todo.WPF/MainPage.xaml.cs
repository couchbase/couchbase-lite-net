using Couchbase.Lite;
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
        private LiveQuery _query;
        private Database _db;

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
            InitializeCouchbase();
        }

        private void InitializeCouchbase()
        {
            _db = Manager.SharedInstance.GetDatabase("wpf-lite");

            var view = _db.GetExistingView("todos");
            
            if (view == null)
            {
                view = _db.GetView("todos");
                view.SetMap((props, emit) =>
                {
                    Console.WriteLine("Mapper mapping");
                    emit(DateTime.UtcNow.ToString(), props["text"]);
                }, "1");
            }
            _query = view.CreateQuery().ToLiveQuery();
            _query.Changed += QueryChanged;
            _query.Completed += QueryCompleted;
            _query.Start();
        }

        private void QueryCompleted(object sender, QueryCompletedEventArgs e)
        {
            if (e.Rows.Count == 0)
            {
                var input = FindName("NewTodoBox") as TextBox;
                input.Focus();
            }
        }

        private void QueryChanged(object sender, QueryChangeEventArgs e)
        {
            Console.WriteLine("Found {0} new rows", e.Rows.Count());
            UpdateDataContextSync(e.Rows);
        }

        private void CreateNewDocument(string text = null)
        {
            // Insert a new doc.
            var props = new Dictionary<string, object>
                {
                    { "text", text ?? "Create a new todo!" },
                    { "completed", false }
                };
            var doc = _db.CreateDocument();
            var rev = doc.CreateRevision();
            rev.SetProperties(props);
            rev.Save();
        }

        private void UpdateDataContextSync(QueryEnumerator results)
        {
            var rows = results.Select(row => row.Document);
            Todos.Clear();
            foreach (var row in rows)
            {
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
    }

    public class DocumentToTextConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType == typeof(String))
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
