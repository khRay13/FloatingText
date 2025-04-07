// using System.Text;
// using System.Windows;
// using System.Windows.Controls;
// using System.Windows.Data;
// using System.Windows.Documents;
// using System.Windows.Input;
// using System.Windows.Media;
// using System.Windows.Media.Imaging;
// using System.Windows.Navigation;
// using System.Windows.Shapes;

// namespace FloatingText;

// /// <summary>
// /// Interaction logic for MainWindow.xaml
// /// </summary>
// public partial class MainWindow : Window
// {
//     public MainWindow()
//     {
//         InitializeComponent();
//     }
// }

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FloatingTextApp
{
    public partial class MainWindow : Window
    {
        private Canvas floatingCanvas;
        private Window floatingWindow; // 單一透明視窗
        private Random random = new Random();
        private bool isRunning = false;
        private IntPtr keyboardHookId = IntPtr.Zero;
        private IntPtr mouseHookId = IntPtr.Zero;
        private Key monitoredKey = Key.Enter;
        private bool isKeyboardMode = true;

        // Windows API 定義
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        // private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_LBUTTONDOWN = 0x0201;
        private LowLevelKeyboardProc _keyboardProc = KeyboardHookCallback;
        private LowLevelMouseProc _mouseProc = MouseHookCallback;
        private static MainWindow _instance;

        public MainWindow()
        {
            InitializeComponent();
            _instance = this;

            // 初始化Canvas
            floatingCanvas = new Canvas
            {
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight
            };
            floatingCanvas.Background = Brushes.Transparent;

            // 初始化Window
            floatingWindow = new Window
            {
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Content = floatingCanvas
            };

            this.Loaded += MainWindow_Loaded;
            this.Closing += (s, e) => ShutdownApplication(); // 處理視窗「X」關閉
        }

        // 視窗載入時初始化
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化模式
            isKeyboardMode = keyboardMode.IsChecked == true;
            keyInput.IsReadOnly = !isKeyboardMode;
            if (!isKeyboardMode)
            {
                keyInput.Text = "Left Click";
            } else {
                keyInput.Text = "Enter";
            }

            // 綁定 Checked 事件
            keyboardMode.Checked += Mode_Checked;
            mouseMode.Checked += Mode_Checked;
        }

        // 啟動按鈕
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textInput.Text))
            {
                MessageBox.Show("未輸入文字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(timesInput.Text))
            {
                MessageBox.Show("未輸入顯示秒數", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(timesInput.Text, out double times) || times <= 0)
            {
                MessageBox.Show("顯示秒數必須為正數", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isRunning = true;
            this.WindowState = WindowState.Minimized;
            floatingWindow.Show();

            SetKeyboardHook();
            if (!isKeyboardMode) SetMouseHook();
        }

        // 結束按鈕
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            ShutdownApplication();
        }

        // 完全結束應用程式
        private void ShutdownApplication()
        {
            isRunning = false;
            UnhookKeyboard();
            UnhookMouse();
            floatingWindow.Close();
            Application.Current.Shutdown();
        }

        // 監聽鍵盤輸入框的按鍵
        private void KeyInput_KeyDown(object sender, KeyEventArgs e)
        {
            monitoredKey = e.Key;
            // monitoredMouseButton = null; // 重置滑鼠事件
            keyInput.Text = e.Key.ToString();
            e.Handled = true;
        }

        // 模式切換
        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            isKeyboardMode = keyboardMode.IsChecked == true;
            keyInput.IsReadOnly = !isKeyboardMode;
            if (!isKeyboardMode)
            {
                keyInput.Text = "Left Click"; // 預設滑鼠左鍵
            } else {
                keyInput.Text = "Enter";
            }
        }

        // 設置鍵盤鉤子
        private void SetKeyboardHook()
        {
            if (keyboardHookId == IntPtr.Zero)
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        // 設置滑鼠鉤子
        private void SetMouseHook()
        {
            if (mouseHookId == IntPtr.Zero)
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        // 移除鍵盤鉤子
        private void UnhookKeyboard()
        {
            if (keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookId);
                keyboardHookId = IntPtr.Zero;
            }
        }

        // 移除滑鼠鉤子
        private void UnhookMouse()
        {
            if (mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHookId);
                mouseHookId = IntPtr.Zero;
            }
        }

        // 鍵盤鉤子回調
        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                // Caps Lock 獨立處理
                if (key == Key.CapsLock && _instance.isRunning)
                {
                    bool capsLockState = Console.CapsLock;
                    if (!capsLockState && _instance.CheckCapsLock.IsChecked == true)
                    // if (!capsLockState)
                    {
                        _instance.Dispatcher.Invoke(() => _instance.CreateCapsLockWarning());
                    }
                }

                // 鍵盤模式觸發
                if (_instance.isKeyboardMode && vkCode == KeyInterop.VirtualKeyFromKey(_instance.monitoredKey) && _instance.isRunning)
                {
                    _instance.Dispatcher.Invoke(() => _instance.CreateFloatingText(false));
                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // 滑鼠鉤子回調
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_instance.isKeyboardMode && wParam == (IntPtr)WM_LBUTTONDOWN && _instance.isRunning)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                _instance.Dispatcher.Invoke(() => _instance.CreateFloatingText(true, hookStruct.pt.x, hookStruct.pt.y));
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // 創建 Caps Lock 提醒文字
        private void CreateCapsLockWarning()
        {
            double times = double.Parse(timesInput.Text);

            TextBlock textBlock = new TextBlock
            {
                Text = "提醒！！已啟用大寫",
                FontSize = 64,
                FontFamily = new FontFamily("Microsoft JhengHei"),
                FontWeight = FontWeights.Bold,
                Foreground = GetRandomColor()
            };

            ApplyOutline(textBlock);

            double startX = (SystemParameters.PrimaryScreenWidth - textBlock.ActualWidth) / 2;
            double startY = SystemParameters.PrimaryScreenHeight;

            Canvas.SetLeft(textBlock, startX);
            Canvas.SetTop(textBlock, startY);
            floatingCanvas.Children.Add(textBlock);

            AnimateText(textBlock, times);
        }

        // 創建飄浮文字
        private void CreateFloatingText(bool isMouseTriggered, double mouseX = 0, double mouseY = 0)
        {
            string text = textInput.Text + "+1";
            double times = double.Parse(timesInput.Text);

            double fontSize = double.Parse((fontSizeComboBox.SelectedItem as ComboBoxItem).Content.ToString());
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontFamily = new FontFamily("Microsoft JhengHei"),
                FontWeight = FontWeights.Bold,
                Foreground = GetRandomColor()
            };

            ApplyOutline(textBlock);

            double startX, startY;
            if (isMouseTriggered)
            {
                startX = mouseX - (textBlock.ActualWidth / 2);
                startY = mouseY;
            }
            else
            {
                startX = random.Next(0, (int)(SystemParameters.PrimaryScreenWidth - 100));
                startY = random.Next(0, (int)SystemParameters.PrimaryScreenHeight); // 全畫面隨機位置
            }

            Canvas.SetLeft(textBlock, startX);
            Canvas.SetTop(textBlock, startY);
            floatingCanvas.Children.Add(textBlock);

            AnimateText(textBlock, times);
        }

        // 動畫處理
        private void AnimateText(TextBlock textBlock, double times)
        {

            DoubleAnimation moveAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(times),
                EasingFunction = new QuadraticEase()
            };

            DoubleAnimation fadeAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(times)
            };

            moveAnimation.Completed += (s, e) =>
            {
                floatingCanvas.Children.Remove(textBlock);
            };

            textBlock.BeginAnimation(Canvas.TopProperty, moveAnimation);
            textBlock.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }

        // 隨機顏色
        private SolidColorBrush GetRandomColor()
        {
            Color color;
            do
            {
                color = Color.FromRgb((byte)random.Next(0, 256), (byte)random.Next(0, 256), (byte)random.Next(0, 256));
            } while (color.R < 50 && color.G < 50 && color.B < 50);
            return new SolidColorBrush(color);
        }

        // 文字外框
        private void ApplyOutline(TextBlock textBlock)
        {
            Color[] outlineColors = { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.White, Colors.Purple };
            textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = outlineColors[random.Next(outlineColors.Length)],
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 5,
                Opacity = 1
            };
        }

        // Windows API
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}