using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace BattleShipApp
{
    internal class SpriteAnimator
    {
        private List<ImageBrush> _frames;
        private int _speedMs;

        // Constructor: Takes a base name and a count
        // Example: pathBase = "pack://application:,,,/resources/miss_", count = 14
        public SpriteAnimator(string pathBase, int frameCount, int speedMs)
        {
            _speedMs = speedMs;
            _frames = new List<ImageBrush>();

            try
            {
                for (int i = 0; i < frameCount; i++)
                {
                    // Construct path: "resources/miss_(0).png", "resources/miss_(1).png"...
                    // adjusting the number format based on your files
                    string fullPath = $"{pathBase}({i}).png";

                    BitmapImage img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(fullPath, UriKind.RelativeOrAbsolute);
                    img.CacheOption = BitmapCacheOption.OnLoad; // Load into memory now
                    img.EndInit();
                    img.Freeze(); // Optimize

                    ImageBrush brush = new ImageBrush(img);
                    brush.Freeze();
                    _frames.Add(brush);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading frames: {ex.Message}");
            }
        }



        public void Play(Border targetElement, Action onComplete = null)
        {
            if (_frames.Count == 0) return;

            int index = 0;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(_speedMs);

            timer.Tick += (sender, e) =>
            {
                if (index >= _frames.Count)
                {
                    timer.Stop();
                    onComplete?.Invoke();
                    return;
                }

                targetElement.Background = _frames[index];
                index++;
            };

            timer.Start();
        }
    }
}
