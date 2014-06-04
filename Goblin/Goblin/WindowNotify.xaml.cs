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

namespace Goblin
{
    /// <summary>
    /// Interaction logic for WindowNotify.xaml
    /// </summary>
    public partial class WindowNotify : Window
    {
        public WindowNotify()
        {
            InitializeComponent();
        }

        public void SetText(string text)
        {
            TextBlockNotify.Text = text;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = Visibility.Hidden;
        }
    }
}
