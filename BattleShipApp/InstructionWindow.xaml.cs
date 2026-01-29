using System.Windows;

namespace BattleShipApp
{
    public partial class InstructionsWindow : Window
    {
        public InstructionsWindow()
        {
            InitializeComponent();
            // This allows dragging the window by clicking anywhere on it
            this.MouseLeftButtonDown += (s, e) => this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}