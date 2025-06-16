using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowGopher
{
    public partial class MainWindow : Window
    {
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        private static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsIconic(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [LibraryImport("kernel32.dll")]
        private static partial uint GetCurrentThreadId();

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);

        private IntPtr currentWindowHandle = IntPtr.Zero;
        private IntPtr targetWindowHandle = IntPtr.Zero;
        private DispatcherTimer? switchTimer;
        private readonly DispatcherTimer windowScanTimer;
        private int mainInterval = 23; // seconds
        private int backInterval = 3; // seconds
        private Dictionary<string, (IntPtr Handle, int ProcessId)> windowHandles = [];

        public MainWindow()
        {
            InitializeComponent();
            txtMainInterval.Text = mainInterval.ToString();
            txtBackInterval.Text = backInterval.ToString();

            // Initialize window scan timer
            windowScanTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            windowScanTimer.Tick += WindowScanTimer_Tick;
            windowScanTimer.Start();

            // Initial window scan
            ScanWindows();
        }

        private void WindowScanTimer_Tick(object? sender, EventArgs e)
        {
            ScanWindows();
        }

        private static string GetWindowDisplayName(Process proc)
        {
            string title = proc.MainWindowTitle;
            string processName = proc.ProcessName;
            string displayName;

            if (string.IsNullOrWhiteSpace(title))
            {
                displayName = $"[No Title] - {processName} (PID: {proc.Id})";
            }
            else
            {
                displayName = $"{processName} (PID: {proc.Id})";
            }

            return displayName;
        }

        private void ScanWindows()
        {
            var currentWindows = new Dictionary<string, (IntPtr Handle, int ProcessId)>();
            var selectedCurrentWindow = cmbCurrentWindow.SelectedItem?.ToString();
            var selectedTargetWindow = cmbTargetWindow.SelectedItem?.ToString();

            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.MainWindowHandle != IntPtr.Zero)  // Only include processes with a window
                {
                    string displayName = GetWindowDisplayName(proc);
                    currentWindows[displayName] = (proc.MainWindowHandle, proc.Id);
                }
            }

            // Update both ComboBoxes
            cmbCurrentWindow.Items.Clear();
            cmbTargetWindow.Items.Clear();
            foreach (var window in currentWindows)
            {
                cmbCurrentWindow.Items.Add(window.Key);
                cmbTargetWindow.Items.Add(window.Key);
            }

            // Store the handles for later use
            windowHandles = currentWindows;

            // Restore selections if possible
            if (!string.IsNullOrEmpty(selectedCurrentWindow) && currentWindows.ContainsKey(selectedCurrentWindow))
            {
                cmbCurrentWindow.SelectedItem = selectedCurrentWindow;
            }
            else if (cmbCurrentWindow.Items.Count > 0)
            {
                cmbCurrentWindow.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(selectedTargetWindow) && currentWindows.ContainsKey(selectedTargetWindow))
            {
                cmbTargetWindow.SelectedItem = selectedTargetWindow;
            }
            else if (cmbTargetWindow.Items.Count > 0)
            {
                cmbTargetWindow.SelectedIndex = 0;
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCurrentWindow.SelectedItem == null || cmbTargetWindow.SelectedItem == null)
            {
                MessageBox.Show("Select both current and target windows first.");
                return;
            }

            string currentWindowName = cmbCurrentWindow.SelectedItem.ToString() ?? string.Empty;
            string targetWindowName = cmbTargetWindow.SelectedItem.ToString() ?? string.Empty;

            if (currentWindowName == targetWindowName)
            {
                MessageBox.Show("Current and target windows must be different.");
                return;
            }

            if (!int.TryParse(txtMainInterval.Text, out mainInterval))
                mainInterval = 23;
            if (!int.TryParse(txtBackInterval.Text, out backInterval))
                backInterval = 3;

            // Get handles from our stored dictionary
            if (windowHandles.TryGetValue(currentWindowName, out var currentWindow) &&
                windowHandles.TryGetValue(targetWindowName, out var targetWindow))
            {
                currentWindowHandle = currentWindow.Handle;
                targetWindowHandle = targetWindow.Handle;

                switchTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(mainInterval)
                };
                switchTimer.Tick += SwitchTimer_Tick;
                switchTimer.Start();

                // Update UI to show running state
                btnStart.Visibility = Visibility.Collapsed;
                btnStop.Visibility = Visibility.Visible;
                statusIndicator.Fill = new SolidColorBrush(Colors.Green);
            }
            else
            {
                MessageBox.Show("Could not find one or both window handles.");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (switchTimer != null)
            {
                switchTimer.Stop();
                switchTimer = null;
            }

            // Update UI to show stopped state
            btnStart.Visibility = Visibility.Visible;
            btnStop.Visibility = Visibility.Collapsed;
            statusIndicator.Fill = new SolidColorBrush(Colors.Gray);
        }

        private static void ForceWindowToFront(IntPtr hWnd)
        {
            // Get the thread IDs
            uint currentThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);

            // Attach the threads
            AttachThreadInput(currentThreadId, targetThreadId, true);

            try
            {
                // Restore the window if it's minimized
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }

                // Make the window topmost temporarily
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

                // Force the window to the foreground
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, SW_SHOW);
            }
            finally
            {
                // Detach the threads
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }

        private async void SwitchTimer_Tick(object? sender, EventArgs e)
        {
            if (currentWindowHandle != IntPtr.Zero && targetWindowHandle != IntPtr.Zero)
            {
                ForceWindowToFront(targetWindowHandle);
                await Task.Delay(backInterval * 1000);
                ForceWindowToFront(currentWindowHandle);
            }
        }
    }
}