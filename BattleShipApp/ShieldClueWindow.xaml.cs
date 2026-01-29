using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BattleShipApp
{
    public partial class ShieldClueWindow : Window
    {
        private const string water = " ";

        // Property to store the result
        public Point SelectedCoordinate { get; private set; } = new Point(-1, -1);

        // Constructor now accepts string[][]
        public ShieldClueWindow(string[][] gameBoard)
        {
            InitializeComponent();
            GenerateBoard(gameBoard);
        }

        private void GenerateBoard(string[][] board)
        {
            ShieldGrid.Children.Clear();

            // Use board.Length to automatically adjust to the board size (usually 10)
            for (int r = 0; r < board.Length; r++)
            {
                for (int c = 0; c < board[r].Length; c++)
                {
                    Button btn = new Button();

                    // Style the button
                    btn.Margin = new Thickness(1);
                    btn.BorderThickness = new Thickness(0);
                    btn.Tag = new Point(r, c); // Store coordinate
                    btn.Click += GridCell_Click;

                    // Get value
                    string? cellValue = (board[r] != null && c < board[r].Length) ? board[r][c] : null;

                    // 1. Check if it is Water
                    if (cellValue == null || cellValue.Equals(water))
                    {
                        // Empty Water -> Disabled
                        btn.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                        btn.IsEnabled = false;
                        btn.Opacity = 0.5;
                    }
                    // 2. Check if it is DESTROYED (New Logic)
                    else if (cellValue == "destroyed")
                    {
                        // Destroyed Part -> Disabled (Red)
                        btn.Background = Brushes.Red;
                        btn.IsEnabled = false; // Cannot shield a destroyed part!
                        btn.ToolTip = "Cannot shield destroyed part";
                        btn.Opacity = 0.7;
                    }
                    // 3. It is a Healthy Ship
                    else
                    {
                        // Valid target for shield
                        btn.Content = "O"; // Optional: Show a symbol or the ship ID
                        btn.Foreground = Brushes.White;
                        btn.Background = Brushes.DodgerBlue; // Ship Color
                        btn.ToolTip = "Click to Shield this part";
                        btn.Cursor = System.Windows.Input.Cursors.Hand;
                    }

                    ShieldGrid.Children.Add(btn);
                }
            }
        }

        private void GridCell_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Point point)
            {
                SelectedCoordinate = point;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}