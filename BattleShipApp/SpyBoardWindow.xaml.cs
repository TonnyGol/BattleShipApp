using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BattleshipGame
{
    public partial class SpyBoardWindow : Window
    {
        public SpyBoardWindow(string[][] enemyBoard)
        {
            InitializeComponent();
            RenderBoard(enemyBoard);
        }

        private void RenderBoard(string[][] board)
        {
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    // Create a rectangle for this cell
                    Rectangle rect = new Rectangle();
                    rect.Margin = new Thickness(1); // Small gap between blocks

                    // Determine color based on the string value
                    string cellValue = board[r][c];

                    switch (cellValue)
                    {
                        case "ship":
                            // Reveal the ship! (Dark Gray)
                            rect.Fill = Brushes.DarkSlateGray;
                            break;

                        case "shiled": // (sic)
                            // Shielded ship (Orange/Gold)
                            rect.Fill = Brushes.Orange;
                            break;

                        case "destroyed":
                            // Already hit parts (Red)
                            rect.Fill = Brushes.Red;
                            break;

                        default:
                            // Empty water " " (Light Blue)
                            rect.Fill = Brushes.LightBlue;
                            break;
                    }

                    // Add the rectangle to the UniformGrid
                    SpyGrid.Children.Add(rect);
                }
            }
        }
    }
}