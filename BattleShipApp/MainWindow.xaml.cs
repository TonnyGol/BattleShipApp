using BattleshipGame;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;



namespace BattleShipApp
{
    public partial class MainWindow : Window
    {
        private AppConfig? _appConfig;
        private MqttService _mqttService;

        private bool _keepSending = true;

        private TimeSpan _time;
        private DispatcherTimer _gameTimer;
        
        private int _currentScore;

        private string[][] gameBoard;
        private string[][] enemyBoard;

        private DispatcherTimer _placementTimer;
        private bool _isPlacementTimerRunning = false;

        private List<int>[] _myShips;
        private int _currentShipIndex = 0;
        private int _partsReceivedCount = 0;
        private readonly int[] _shipSizes = { 5, 4, 3, 3, 2 };

        private List<int>[] _enemyShips = new List<int>[5];

        private SpriteAnimator _missAnim;
        private SpriteAnimator _explosionAnim;
        private SpriteAnimator _shieldBreakAnim;

        private const string shipPart = "ship";
        private const string shieldPart = "shield";
        private const string destroyedShipPart = "destroyed";

        // Initializers
        // -----------------
        public MainWindow()
        {
            InitializeComponent();
           
            // 1. Read the text from the file
            string jsonString = File.ReadAllText("config.json");

            _appConfig = JsonSerializer.Deserialize<AppConfig>(jsonString);

            // 2. Create the MQTT Service
            _mqttService = new MqttService();

            _mqttService.MessageReceived += OnMqttMessageReceived;

            InitializeMqtt();

            InitializeGameStats();

            _ = StartPeriodicSender();

            // Initialize my board to check defense and attacks
            gameBoard =
            [
                new string[10] { " ", " ", " ", " ", " ", " ", " ", " ", " ", " " },
                new string[10] { " ", " ", " ", " ", " ", " ", " ", " ", " ", " " },
                new string[10] { " ", " ", " ", " ", " ", " ", " ", " ", " ", " " },
                new string[10] { " ", " ", " ", " ", " ", " ", " ", " ", " ", " " },
                new string[10] { " ", " ", " ", " ", " ", " ", " ", " ", " ", " " }
            ];

            _myShips = new List<int>[5];
            for (int i = 0; i < 5; i++)
            {
                _myShips[i] = new List<int>();
            }

            InitializeAnimations();

            _placementTimer = new DispatcherTimer();
            _placementTimer.Interval = TimeSpan.FromSeconds(25);
            _placementTimer.Tick += OnPlacementTimerElapsed;

        }

        private async void InitializeMqtt()
        {
            try
            {
                if (_appConfig == null)
                    throw new Exception("AppConfig is null");

                string clientId = _appConfig.ClientIdPrefix;
                await _mqttService.ConnectAsync(_appConfig.MqttBrokerIp, clientId);
                await _mqttService.SubscribeAsync(_appConfig.MqttTopics["I_am_alive"]);
                await _mqttService.SubscribeAsync(_appConfig.MqttTopics["MessagesReceive"]);
                await _mqttService.SubscribeAsync(_appConfig.MqttTopics["PlayFlow"]);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading config or connecting: " + ex.Message);
            }
        }

        private void InitializeGameStats()
        {
            _currentScore = 0; 
            ScoreText.Text = _currentScore.ToString();

            // 2. Setup Timer for 60 Minutes
            _time = TimeSpan.FromMinutes(5); // 5 minutes for testing
            _gameTimer = new DispatcherTimer();
            _gameTimer.Interval = TimeSpan.FromSeconds(1); 
            _gameTimer.Tick += GameTimer_Tick;
        }

        private void InitializeAnimations()
        {
            _explosionAnim = new SpriteAnimator(
                "pack://application:,,,/resources/explosionAnimation/explosion_",
                48,
                50
                );
            _missAnim = new SpriteAnimator(
                "pack://application:,,,/resources/missAnimation/miss_",
                14,
                100
                );
        }

        // MQTT MESSAGE HANDLER
        // ------------------------

        private void OnMqttMessageReceived(string message, string topic)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            // We must use the Dispatcher to update the UI
            Dispatcher.Invoke(() =>
            {
                string playFlowTopic = _appConfig.MqttTopics["PlayFlow"];
                string receiveTopic = _appConfig.MqttTopics["MessagesReceive"];

                if (topic == playFlowTopic)
                {
                    HandlePlayFlowMessages(message);
                }
                else if (topic == receiveTopic)
                {
                    HandleGameRecevieMessages(message);
                }
                else 
                {
                    //MessageBox.Show($"Recived Message: ({message}) | topic: ({topic})");
                }
                
            });
        }

        private void HandlePlayFlowMessages(string message)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            if (message == _appConfig.MqttMessages["Reset"])
            {
                InitialScreen.Visibility = Visibility.Visible;
                ResetApp();
            }
            else if (message == _appConfig.MqttMessages["StartIntro"])
            {
                InstructionVideo.Visibility = Visibility.Visible;
                InstructionVideo.Play();
                // added timer for each ship placement after the video ends in the media ended event
            }
            else if (message == _appConfig.MqttMessages["Skip"])
            {
                InstructionVideo.Visibility = Visibility.Collapsed;
                InstructionVideo.Close();
                RunPlacementVideo();
                // close videos of ship instructions too
            }
            else if (message == _appConfig.MqttMessages["GameStart"])
            {
                // This will be the remove of the puting ships videos
                InitialScreen.Visibility = Visibility.Collapsed;
                _gameTimer.Start();
            }
            else 
            {
                //
            }
        }

        private void HandleGameRecevieMessages(string message)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            if (message == _appConfig.MqttMessages["ShieldPuzzel"])
            {
                _ = OpenShieldClueDialogAsync();
            }
            else if (message == _appConfig.MqttMessages["StrategicPuzzel"])
            {
                OpenStrategyClueDialog();
            }
            else if (message == _appConfig.MqttMessages["FixPartsPuzzel"])
            {
                OpenFixShipPartClueDialog();
            }
            else if (message.StartsWith("score"))
            {
                int redTeamScore = int.Parse(message.Replace("score:", "").Trim());
                _ = SendEndGameResultAsync(redTeamScore);
            }
            else if (message.StartsWith('y'))
            {
                string cleanMessage = message.Replace("y", "").Trim();

                if (int.TryParse(cleanMessage, out int blockPosition))
                {
                    // 1. Update the internal data
                    UpdateBoardData(blockPosition);

                    // 3. Check ship placement rules (Did we finish a ship?)
                    ProcessShipSequence(blockPosition);
                }
            }
            else if (message.StartsWith('b'))
            {
                try
                {
                    string jsonPart = message.Substring(2);
                    string[] boardAndList = jsonPart.Split("|");
                    enemyBoard = JsonSerializer.Deserialize<string[][]>(boardAndList[0]);
                    _enemyShips = JsonSerializer.Deserialize<List<int>[]>(boardAndList[1]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error parsing board: " + ex.Message);
                }

            }
            else if (message.StartsWith('f'))
            {
                // "f|5_12_23" - example format
                try
                {
                    //MessageBox.Show($"{message}");
                    string[] cordinates = message.Substring(2).Split('_');
                    ProcessAttack(cordinates);
                }
                catch (Exception ex)
                {
                    // Handle parsing errors (e.g., if message format is wrong)
                    Console.WriteLine($"Error parsing message: {message}. Error: {ex.Message}");
                }
            }
            else if (message.StartsWith('u'))
            {
                // "u|5,12,green" - example format
                string scoreBlockColor = message.Substring(2); // Removes "u|"

                string[] parts = scoreBlockColor.Split(',');

                int score = int.Parse(parts[0]);
                int blockPos = int.Parse(parts[1]);
                string color = parts[2];

                ProcessUpdateFromEnemy(score, blockPos, color);
            }
            else
            {
                //
            }
        }

        // GAME LOGIC METHODS
        // ------------------------

        private void CheckIfShipSunk_enemy(int hitBlockID, string[][] currentEnemyBoard)
        {
            if (_enemyShips == null)
            {
                Console.WriteLine("[ERROR] _enemyShips is NULL! The Ship List never arrived.");
                return;
            }

            foreach (List<int> ship in _enemyShips)
            {
                if (ship.Contains(hitBlockID))
                {

                    bool isSunk = true;

                    foreach (int shipBlockID in ship)
                    {
                        int r = (shipBlockID - 1) / 10;
                        int c = (shipBlockID - 1) % 10;

                        string status = currentEnemyBoard[r][c];

                        if (status != destroyedShipPart)
                        {
                            isSunk = false;
                            break;
                        }
                    }

                    if (isSunk)
                    {
                        foreach (int shipBlockID in ship)
                        {
                            UpdateBoardUI(shipBlockID, System.Windows.Media.Brushes.Blue);
                        }
                        AddScore(50);
                        ShowTemporaryMessage("Boom! Enemy Ship Sunk!");
                    }
                    return;
                }
            }
        }

        private void UpdateBoardData(int blockPosition)
        {
            int row = (blockPosition - 1) / 10;
            int col = (blockPosition - 1) % 10;
            gameBoard[row][col] = shipPart;
        }

        private void UpdateBoardUI(int blockPosition, SolidColorBrush color)
        {
            string borderName = $"B_{blockPosition}";

            // We use pattern matching 'is' to keep it safe and clean
            if (this.FindName(borderName) is Border targetBorder)
            {
                targetBorder.BorderThickness = new Thickness(4);
                targetBorder.BorderBrush = color;
            }
        }

        private void ProcessUpdateFromEnemy(int score, int blockPos, string color) 
        {
            AddScore(score);

            int r = (blockPos - 1) / 10;
            int c = (blockPos - 1) % 10;

            switch (color)
            {
                case "green":
                    UpdateBoardUI(blockPos, System.Windows.Media.Brushes.Green);
                    PlayHitAnimation(blockPos, color);
                    enemyBoard[r][c] = destroyedShipPart;
                    CheckIfShipSunk_enemy(blockPos, enemyBoard);
                    break;

                case "orange":
                    UpdateBoardUI(blockPos, System.Windows.Media.Brushes.Orange);
                    PlayHitAnimation(blockPos, color);
                    enemyBoard[r][c] = shipPart;
                    break;

                case "red":
                    UpdateBoardUI(blockPos, System.Windows.Media.Brushes.Red);
                    PlayHitAnimation(blockPos, color);
                    break;
            }
        }

        private void PlayHitAnimation(int blockPosition, string color)
        {
            string borderName = $"B_{blockPosition}";
            if (this.FindName(borderName) is Border targetBorder)
            {
                switch (color)
                {
                    case "green":
                        _explosionAnim.Play(targetBorder);
                        break;
                    case "orange":
                        //UpdateBoardUI(blockPosition, System.Windows.Media.Brushes.Orange);
                        break;
                    case "red":
                        _missAnim.Play(targetBorder);
                        break;
                }
            }
        }

        private void RunPlacementVideo()
        {
            if (!_isPlacementTimerRunning)
            {
                _isPlacementTimerRunning = true;
                _placementTimer.Start();
                try
                {
                    // Point to the file (Make sure Build Action = Content)
                    string videoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", $"Place_{_currentShipIndex+1}_Ship.mp4");
                    PlacementVideoPlayer.Source = new Uri(videoPath);

                    PlacementVideoPlayer.Visibility = Visibility.Visible;
                    PlacementVideoPlayer.Position = TimeSpan.Zero; // Rewind
                    PlacementVideoPlayer.Play(); 
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error playing video: " + ex.Message);
                }
            }
        }

        private async void ProcessAttack(string[] cordinates)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            foreach (string block in cordinates)
            {
                int blockPosition = int.Parse(block);

                int row = (blockPosition - 1) / 10;
                int col = (blockPosition - 1) % 10;

                string cellContent = gameBoard[row][col];
                string scoreToAdd = "0"; 

                switch (cellContent)
                {
                    case shipPart:
                        //MessageBox.Show("Ship Part Attacked !");
                        gameBoard[row][col] = destroyedShipPart;
                        scoreToAdd = _appConfig.Scores["AttackScore"];
                        //await _mqttService.PublishAsync(_appConfig.MqttTopics["MessagesReceive"], $"u|{scoreToAdd},{blockPosition},{"green"}");
                        await _mqttService.PublishAsync(_appConfig.MqttTopics["EnemyTeam"], $"u|{scoreToAdd},{blockPosition},{"green"}");
                        break;

                    case shieldPart:
                        //MessageBox.Show("Shield Part Attacked, we are still ok.");
                        gameBoard[row][col] = shipPart;
                        //await _mqttService.PublishAsync(_appConfig.MqttTopics["MessagesReceive"], $"u|{scoreToAdd},{blockPosition},{"orange"}");
                        await _mqttService.PublishAsync(_appConfig.MqttTopics["EnemyTeam"], $"u|{scoreToAdd},{blockPosition},{"orange"}");
                        break;

                    case destroyedShipPart:
                        await _mqttService.PublishAsync(_appConfig.MqttTopics["EnemyTeamTablet"], $"{blockPosition}");
                        break;

                    case " ":
                        //MessageBox.Show("Miss :) LoL");
                        //await _mqttService.PublishAsync(_appConfig.MqttTopics["MessagesReceive"], $"u|{scoreToAdd},{blockPosition},{"red"}");
                        await _mqttService.PublishAsync(_appConfig.MqttTopics["EnemyTeam"], $"u|{scoreToAdd},{blockPosition},{"red"}");
                        break;
                }
            }
        }

        private async void ProcessShipSequence(int blockPosition)
        {
            if (_currentShipIndex >= _shipSizes.Length) return;

            if (IsBlockOccupied(blockPosition))
            {
                ShowTemporaryMessage("Block is already occupied!");
                return;
            }

            _myShips[_currentShipIndex].Add(blockPosition);
            _partsReceivedCount++;

        }

        private async void OnPlacementTimerElapsed(object sender, EventArgs e)
        {
            _placementTimer.Stop();
            _isPlacementTimerRunning = false;

            PlacementVideoPlayer.Stop();
            PlacementVideoPlayer.Visibility = Visibility.Collapsed;

            int targetSize = _shipSizes[_currentShipIndex];
            int actualCount = _myShips[_currentShipIndex].Count;

            // CASE 1: TOO FEW BLOCKS
            if (actualCount < targetSize)
            {
                ShowTemporaryMessage($"Time's up! incomplete ship.\nExpected {targetSize}, found {actualCount}.");
                ResetCurrentShip();
                return;
            }

            // CASE 2: TOO MANY BLOCKS
            if (actualCount > targetSize)
            {
                ShowTemporaryMessage($"Time's up! Too many blocks placed.\nExpected {targetSize}, found {actualCount}.");
                ResetCurrentShip();
                return;
            }

            // CASE 3: CORRECT COUNT (Now check Shape)
            if (!IsValidShip(_myShips[_currentShipIndex]))
            {
                ShowTemporaryMessage($"Time's up! Invalid shape.\nBlocks must be connected and straight.");
                ResetCurrentShip();
                return;
            }

            // CASE 4: SUCCESS
            // If we get here, count is correct AND shape is valid.
            await FinalizeCurrentShip();
        }

        // --- HELPER FUNCTIONS ---
        // ------------------------

        private async Task FinalizeCurrentShip()
        {
            // 1. Validate Shape (Double check for the immediate logic path)
            if (!IsValidShip(_myShips[_currentShipIndex]))
            {
                ShowTemporaryMessage($"Invalid Ship Shape!");
                ResetCurrentShip();
                return;
            }

            // 2. Lock Visuals
            foreach (int item in _myShips[_currentShipIndex])
            {
                int row = (item - 1) / 10;
                int col = (item - 1) % 10;
                gameBoard[row][col] = shipPart;
            }

            // 3. Move Next
            _partsReceivedCount = 0;
            _currentShipIndex++;
            

            // Notify
            await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], _appConfig.MqttMessages["ShipPlaced"]);

            // 4. Check End Game
            if (_currentShipIndex >= _shipSizes.Length)
            {
                ShowTemporaryMessage("All ships placed! Ready for war.");
                string jsonBoard = JsonSerializer.Serialize(gameBoard);
                string jsonShips = JsonSerializer.Serialize(_myShips);
                await _mqttService.PublishAsync(_appConfig.MqttTopics["EnemyTeam"], $"b|{jsonBoard}|{jsonShips}");
                //await _mqttService.PublishAsync(_appConfig.MqttTopics["MessagesReceive"], $"b|{jsonBoard}|{jsonShips}");
                //await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], _appConfig.MqttMessages["GameStart"]);
                await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], _appConfig.MqttMessages["TeamReady"]);
            }
            else
            {
                // 5. Start Next Placement Video
                RunPlacementVideo();
            }
        }

        private void ResetCurrentShip()
        {
            // Clear Data
            _myShips[_currentShipIndex].Clear();
            _partsReceivedCount = 0;
            RunPlacementVideo();
        }

        private bool IsValidShip(List<int> parts)
        {
            parts.Sort();

            bool isHorizontal = true;
            int rowToCheck = (parts[0] - 1) / 10; 

            for (int i = 0; i < parts.Count - 1; i++)
            {
                // Must be consecutive numbers (Diff is 1)
                if (parts[i + 1] != parts[i] + 1)
                {
                    isHorizontal = false;
                    break;
                }

                // Must be in the same row (prevent wrapping from right edge to left edge)
                int nextRow = (parts[i + 1] - 1) / 10;
                if (nextRow != rowToCheck)
                {
                    isHorizontal = false;
                    break;
                }
            }

            // 3. Check if it is a valid VERTICAL line
            bool isVertical = true;
            for (int i = 0; i < parts.Count - 1; i++)
            {
                // Must be exactly 10 blocks apart (Diff is 10)
                if (parts[i + 1] != parts[i] + 10)
                {
                    isVertical = false;
                    break;
                }
            }

            // 4. Return true if it matches either pattern
            return isHorizontal || isVertical;
        }

        private bool IsBlockOccupied(int id)
        {
            // Check previous ships
            for (int i = 0; i < _currentShipIndex; i++)
            {
                if (_myShips[i].Contains(id)) return true;
            }
            // Check current ship
            if (_myShips[_currentShipIndex].Contains(id)) return true;

            return false;
        }

        public void AddScore(int points)
        {
            _currentScore += points;
            ScoreText.Text = _currentScore.ToString();
        }

        private void ShowTemporaryMessage(string message, int durationMs = 3000)
        {
            // Create a simple temporary window
            Window msgWindow = new Window()
            {
                WindowStyle = WindowStyle.None,       // No title bar/borders
                ResizeMode = ResizeMode.NoResize,     // Cannot resize
                AllowsTransparency = true,            // Optional: for rounded corners if you want
                Background = Brushes.DarkRed,         // Theme Color (Red for Error)
                Width = 400,
                Height = 150,
                Topmost = true,                       // Always on top
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false                 // Don't show in Windows taskbar
            };

            // Add the Text
            Border border = new Border()
            {
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.White,
                Padding = new Thickness(20)
            };

            TextBlock textBlock = new TextBlock()
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 20,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            border.Child = textBlock;
            msgWindow.Content = border;

            msgWindow.Show();

            Task.Delay(durationMs).ContinueWith(_ =>
            {
                // Dispatcher to close the UI window safely
                Dispatcher.Invoke(() => msgWindow.Close());
            });
        }

        // CLUE DIALOG METHODS
        // ------------------------

        private async Task OpenShieldClueDialogAsync()
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            ShieldClueWindow shieldClue = new ShieldClueWindow(gameBoard);

            if (shieldClue.ShowDialog() == true)
            {
                System.Windows.Point p = shieldClue.SelectedCoordinate;
                gameBoard[(int)p.X][(int)p.Y] = shieldPart;
                int blockPosition = ((int)p.X * 10) + (int)p.Y + 1;
                await _mqttService.PublishAsync(_appConfig.MqttTopics["Led"], $"y{blockPosition}");
            }
        }

        private async void OpenStrategyClueDialog()
        {
            ShowTemporaryMessage("Spy Satellite Activated!\n\nYou will see the enemy's board for exactly 3 seconds.\nMemorize the ship locations!");

            SpyBoardWindow spyWindow = new SpyBoardWindow(enemyBoard);

            spyWindow.Show();

            await Task.Delay(3000);

            spyWindow.Close();
        }

        private void OpenFixShipPartClueDialog()
        {
            ShowTemporaryMessage("Damage Control Team Ready!\n\nSelect a DESTROYED ship part to repair instantly.");

            // We pass the gameBoard so the window knows which parts are 'destroyed'
            FixShipWindow fixWindow = new FixShipWindow(gameBoard, _myShips);

            // Wait for the user to pick a block
            if (fixWindow.ShowDialog() == true)
            {
                int blockID = fixWindow.SelectedBlockID;
                int r = (blockID - 1) / 10;
                int c = (blockID - 1) % 10;

                gameBoard[r][c] = shipPart;

                ShowTemporaryMessage("Repair successful! Structure integrity restored.");
            }
        }

        private void OpenInstructionsDialog()
        {
            InstructionsWindow legend = new InstructionsWindow();

            legend.ShowDialog();
        }

        private void InstructionMark_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpenInstructionsDialog();
        }

        // EVENT HANDLERS
        // ------------------------

        private void InstructionVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            InstructionVideo.Visibility = Visibility.Collapsed;
            InstructionVideo.Close();
            RunPlacementVideo();
        }

        private void InstructionVideoFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"InstructionVideo Failed: {e.ErrorException.Message}");
        }

        // Key Down Event Handler (for testing purposes of mqtt messages recieve
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            switch (e.Key)
            {
                case Key.S:
                    await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], "run");
                    break;

                case Key.R:
                    await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], "ACTION,reset@");
                    break;

                case Key.Space:
                    await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], "skip");
                    break;

                case Key.Escape:
                    this.Close();
                    break;
            }
        }

        private async void GameTimer_Tick(object sender, EventArgs e)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            if (_time == TimeSpan.Zero)
            {
                _gameTimer.Stop();
                TimerText.Text = "00:00";
                MessageBox.Show("Time's up! Game Over.");
                await _mqttService.PublishAsync(_appConfig.MqttTopics["EnemyTeam"], $"score:{_currentScore}");
            }
            else
            {
                _time = _time.Add(TimeSpan.FromSeconds(-1));
                // Format as MM:SS (e.g., 59:59)
                TimerText.Text = _time.ToString(@"mm\:ss");
            }
        }

        private async Task SendEndGameResultAsync(int redTeamScore)
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            if (_currentScore > redTeamScore)
            {
                await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], "Win");
            }
            else
            {
                await _mqttService.PublishAsync(_appConfig.MqttTopics["PlayFlow"], "Lose");
            }
        }

        private void ResetApp()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
                Application.Current.Shutdown();

            }
            catch (Exception ex)
            {
                Application.Current.Shutdown();
                //MessageBox.Show("Error restarting application: " + ex.Message);
            }
        }

        private async Task StartPeriodicSender()
        {
            if (_appConfig == null)
                throw new Exception("AppConfig is null");

            while (_keepSending)
            {
                try
                {
                    if (_mqttService != null) // Safety check
                    {
                        await _mqttService.PublishAsync(_appConfig.MqttTopics["I_am_alive"], 
                            _appConfig.MqttMessages["I_am_alive"]);
                    }

                    await Task.Delay(10000); // 10 seconds
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background send failed: {ex.Message}");
                    await Task.Delay(5000); // Wait a bit before retrying
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        // XAML EXTRA FEATURES

    }
}