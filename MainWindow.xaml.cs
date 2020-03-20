﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.SQLite;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LaunchMe
{
    public partial class ListItemData : StackPanel
    {
        public string Display { get; set; }
        public string Data { get; set; }
        public Image Icon { get; set; }
        public StackPanel MainPane { get; set; }
        public StackPanel SecondaryPane { get; set; }

        public short Padding { get; set; } = 3;

        public ListItemData(string display, string data, string iconpath, int iconSize) : base()
        {
            Display = display;
            Data = data;
            
            var iconSource = System.Drawing.Icon.ExtractAssociatedIcon(iconpath);
            Icon = new Image()
            {
                Source = Imaging.CreateBitmapSourceFromHIcon(
                    iconSource.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()),
                Margin = new Thickness(Padding),
                Width = iconSize,
                Height = iconSize,
            };

            Orientation = Orientation.Vertical;

            MainPane = new StackPanel() { Orientation = Orientation.Horizontal };
            MainPane.Children.Add(Icon);
            MainPane.Children.Add(new TextBlock() { Text = Display, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(Padding), TextTrimming = TextTrimming.CharacterEllipsis } );

            SecondaryPane = new StackPanel() { Orientation = Orientation.Horizontal };
            SecondaryPane.Children.Add(new TextBlock()
            {
                Text = Data,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(Padding),
                Foreground = (System.Windows.Media.SolidColorBrush)(new System.Windows.Media.BrushConverter().ConvertFrom("#999999")),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Children.Add(MainPane);
            Children.Add(SecondaryPane);

        }

        public override string ToString()
        {
            return Display;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly string WinPath = Environment.GetEnvironmentVariable("PATH");
        public static double InternalPadding { get; set; }

        public int DefaultFontSize { get; set; }
        public System.Windows.Media.FontFamily FontFace { get; set; } = new System.Windows.Media.FontFamily("Segoe UI");

        public string DBPath { get; set; } = "Apps.sqlite";

        List<ListItemData> SearchResults = new List<ListItemData>();

        int MaxResults = 5;

        // Global hotkey shenanigans 
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001; //ALT
        private const uint VK_SPACE = 0x20; // SPACE
        private const int HOTKEY_ID = 90001; // Over 9000 haha get it

        private HwndSource source;

        public MainWindow()
        {
            InitializeComponent();
            var res = GetResolution();
            Width = res[0] * .22;
            Height = res[1] * .2;
            InternalPadding = Height * 0.8;
            
            // Why duplicate effort
            if (!File.Exists("Apps.sqlite"))
            {
                InitDB();
            }
            // TODO
            // fix :)
            ScanPrograms();

            // init fonts n' stuff
            userInput.FontFamily = previewResult.FontFamily = FontFace;
            userInput.FontSize = previewResult.FontSize = Height * 0.25;
            userInput.FontWeight = previewResult.FontWeight = FontWeight.FromOpenTypeWeight(300);
            userInput.Focus();

            // Bind search results display to SearchResult list
            listResults.ItemsSource = SearchResults;
        }

        // This is for Global hotkeys
        protected override void OnSourceInitialized(EventArgs e)
        {
            // https://social.technet.microsoft.com/wiki/contents/articles/30568.wpf-implementing-global-hot-keys.aspx
            base.OnSourceInitialized(e);

            IntPtr handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);

            RegisterHotKey(handle, HOTKEY_ID, MOD_ALT, VK_SPACE);
        }

        // So is this
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // https://social.technet.microsoft.com/wiki/contents/articles/30568.wpf-implementing-global-hot-keys.aspx
            const int WM_HOTKEY = 0x0312;
            switch (msg)
            {
                case WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        case HOTKEY_ID:
                            int vkey = (((int)lParam >> 16) & 0xFFFF);
                            if (vkey == VK_SPACE)
                            {
                                if (WindowState == WindowState.Minimized)
                                {
                                    WindowState = WindowState.Normal;
                                    userInput.Focus();
                                }
                            }
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private void InitDB()
        {
            SQLiteConnection.CreateFile("Apps.sqlite");

            SQLiteConnection connection = new SQLiteConnection($"Data Source={DBPath};version=3;");
            connection.Open();

            var sql = "CREATE TABLE 'applications'( "
            + "'id'    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, "
            + "'name'  TEXT NOT NULL, "
            + "'path'  TEXT NOT NULL UNIQUE" 
            + ");";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();
            cmd.Dispose();

            connection.Close();
        }

        public async void ScanPrograms()
        {
            List<SQLiteCommand> toAdd = new List<SQLiteCommand>();
            
            // Find executables in PATHs
            var mask = "*.exe";
            var sources = WinPath.Split(';').ToList<string>();
            foreach (var path in sources)
            {
                // Catch directory not found
                try
                {
                    // Catch... something else who knows
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(path, mask, SearchOption.AllDirectories))
                        {
                            var cmd = new SQLiteCommand($"INSERT INTO applications (name, path) " +
                                $"VALUES( @name, @path );");
                            cmd.Parameters.AddWithValue("name", f.Split('\\').Last());
                            cmd.Parameters.AddWithValue("path", f);
                            if (!toAdd.Contains(cmd)) {
                                toAdd.Add(cmd);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                catch (DirectoryNotFoundException ex)
                {
                    continue;
                }
            }

            // Write that ish to the DB
            using (var connection = new SQLiteConnection($"Data Source={DBPath};version=3;"))
            {
                connection.Open();
                foreach (var cmd in toAdd)
                {
                    cmd.Connection = connection;
                    try
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (SQLiteException){
                        return;
                    }
                }
            }
        }

        // Waste of a method
        public List<double> GetResolution()
        {
            var screenWidth = Convert.ToInt32(SystemParameters.PrimaryScreenWidth);
            var screenHeight = Convert.ToInt32(SystemParameters.PrimaryScreenHeight);
            return new List<double>(new double[] { screenWidth, screenHeight });
        }

        private void UserInput_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                //Close(); // This sucks d00d
                WindowState = WindowState.Minimized;
            }
            if (e.Key == Key.Enter)
            {
                // Try to start the process
                try
                {
                    Process.Start(listResults.SelectedIndex == -1 ? SearchResults[0].Data : SearchResults[listResults.SelectedIndex].Data);
                }
                catch (IndexOutOfRangeException)
                {
                    listResults.SelectedIndex = 0;
                }
                // Display other exceptions
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                // Minimize again
                finally
                {
                    WindowState = WindowState.Minimized;
                }
            }
            // Mouseless functionality --> down & up navigate the results 
            // without losing focus of the textbox {big brain}
            if (e.Key == Key.Down)
            {
                if (listResults.SelectedIndex == SearchResults.Count) { return; } // Out of range
                listResults.SelectedIndex ++;
            }
            if (e.Key == Key.Up)
            {
                if (listResults.SelectedIndex == -1) { return; } // Out of range
                listResults.SelectedIndex --;
            }
        }

        private async void UserInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Don't search for < 3 characters
            // TODO:
            // Make configurable? --> prolly
            if (userInput.Text.Length < 3)
            {
                listResults.Opacity = 0;
                previewResult.Text = string.Empty;
                return;
            }

            SearchResults = new List<ListItemData>();

            var sql = $@"SELECT name, path FROM applications WHERE name LIKE '%{userInput.Text}%';"; // This is just bad
                                                                                                     // LIKE may not support parametization (?)

            using (var connection = new SQLiteConnection($"Data Source={DBPath};version=3;"))
            {
                connection.Open();
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    // Async because why not
                    var reader = await cmd.ExecuteReaderAsync();
                    while (reader.Read())
                    {
                        SearchResults.Add(new ListItemData(reader.GetString(0), reader.GetString(1), reader.GetString(1), 24));
                    }
                }
                connection.Close();
            }
            if (SearchResults.Count > 0)
            {
                listResults.SelectedIndex = 0;
                listResults.Opacity = 1;
                listResults.ItemsSource = SearchResults;
                if (listResults.SelectedIndex == -1)
                {
                    previewResult.Text = SearchResults[0].Display;
                }
                else
                {
                    previewResult.Text = SearchResults[listResults.SelectedIndex].Display;
                }
                // TODO
                // searchIcon.Source; change to app icon later
                // searchIcon.Source = new BitmapImage();
            }
            else
            {
                previewResult.Text = string.Empty;
                listResults.Opacity = 0;
            }
        }

        private void UserInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // This breaks SQL and why are you typing it anyway
            if (e.Text.Contains("'"))
            {
                e.Handled = true;
            }
        }

        // Pfft can't lose focus now
        private void UserInput_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            e.Handled = true;
        }

        private void ListResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                previewResult.Text = SearchResults[listResults.SelectedIndex].Display;
            }
            catch { }
        }
    }
}
