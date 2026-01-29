using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BattleshipGame
{
    public partial class FixShipWindow : Window
    {
        public int SelectedBlockID { get; private set; } = -1;

        public FixShipWindow(string[][] board, List<int>[] myShips)
        {
            InitializeComponent();
            GenerateFixGrid(board, myShips);
        }

        private void GenerateFixGrid(string[][] board, List<int>[] myShips)
        {
            for (int r = 0; r < board.Length; r++)
            {
                for (int c = 0; c < board[r].Length; c++)
                {
                    int id = r * 10 + c;
                    Button btn = new Button();

                    btn.Margin = new Thickness(1);
                    btn.BorderThickness = new Thickness(0);

                    // 1. Is the block DESTROYED?
                    if (board[r][c] == "destroyed")
                    {
                        if (IsShipAlive(id, board, myShips))
                        {
                            btn.Background = Brushes.Red;
                            btn.Cursor = System.Windows.Input.Cursors.Hand;
                            btn.ToolTip = "Click to Repair (Ship is still active)";

                            btn.Tag = id + 1;
                            btn.Click += Btn_Click;
                        }
                        else
                        {
                            btn.Background = Brushes.DarkRed;
                            btn.IsEnabled = false;
                            btn.ToolTip = "Cannot Repair: Ship is completely Sunk";
                            btn.Opacity = 0.5;
                        }
                    }
                    else
                    {
                        btn.Background = Brushes.LightGray;
                        btn.IsEnabled = false;
                        btn.Opacity = 0.3;
                    }

                    FixGrid.Children.Add(btn);
                }
            }
        }

        // FIX IS HERE
        private bool IsShipAlive(int blockID_0Based, string[][] board, List<int>[] myShips)
        {
            int targetID_1Based = blockID_0Based + 1;

            foreach (var ship in myShips)
            {
                if (ship.Contains(targetID_1Based))
                {
                    // Check all blocks in this specific ship
                    foreach (int partID_1Based in ship)
                    {
                        int r = (partID_1Based - 1) / 10;
                        int c = (partID_1Based - 1) % 10;

                        // If any part is NOT destroyed, the ship is alive
                        if (board[r][c] != "destroyed")
                        {
                            return true;
                        }
                    }
                    return false; // All parts destroyed
                }
            }
            return false;
        }

        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            Button clickedBtn = (Button)sender;
            SelectedBlockID = (int)clickedBtn.Tag;
            this.DialogResult = true;
            this.Close();
        }
    }
}