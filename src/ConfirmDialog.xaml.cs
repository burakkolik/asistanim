using System.Windows;
using System.Windows.Input;

namespace Asistanim
{
    public partial class ConfirmDialog : Window
    {
        public enum Choice { Cancel, Save, Discard }
        public Choice Result { get; private set; } = Choice.Cancel;

        public ConfirmDialog()
        {
            InitializeComponent();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private void Save_Click(object sender, RoutedEventArgs e) { Result = Choice.Save; Close(); }
        private void Discard_Click(object sender, RoutedEventArgs e) { Result = Choice.Discard; Close(); }
        private void Cancel_Click(object sender, RoutedEventArgs e) { Result = Choice.Cancel; Close(); }
    }
}
