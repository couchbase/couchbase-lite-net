using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace Todo.WPF
{
    /// <summary>
    /// Interaction logic for ConfigWindow.xaml
    /// </summary>
    public partial class ConfigWindow : Window
    {
        private readonly SimpleViewModel _sourceViewModel;
        public ConfigWindow()
        {
            InitializeComponent();
        }

        internal ConfigWindow(SimpleViewModel sourceViewModel) : this()
        {
            _sourceViewModel = sourceViewModel;
            DataContext = new SimpleViewModel(sourceViewModel);
        }

        private void SaveChanges(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SimpleViewModel;
            _sourceViewModel.LoadFrom(viewModel);
            _sourceViewModel.Save();
            Close();
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
