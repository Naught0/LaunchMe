﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;

namespace LaunchMe
{
    public partial class ListItemFileIcon : StackPanel
    {
        public string Display { get; set; }
        public string Data { get; set; }
        public Image Icon { get; set; }
        public StackPanel MainPane { get; set; }
        public StackPanel SecondaryPane { get; set; }

        public ListItemFileIcon(string display, string data, Image icon, UserSettings settings) : base()
        {
            Display = display;
            Data = data;
            // This is probably bad, but I was receiving an error before and now I'm not
            // Sue me
            Icon = new Image() { Source = icon.Source };

            Orientation = Orientation.Vertical;

            MainPane = new StackPanel() { Orientation = Orientation.Horizontal };
            MainPane.Children.Add(Icon);
            MainPane.Children.Add(new TextBlock() { Text = Display, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(settings.Padding), TextTrimming = TextTrimming.CharacterEllipsis });

            SecondaryPane = new StackPanel() { Orientation = Orientation.Horizontal };
            SecondaryPane.Children.Add(new TextBlock()
            {
                Text = Data,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(settings.Padding),
                Foreground = settings.ColorForegroundFaded,
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
        public UserSettings settings;
        // Current search results
        // Bound directly to the displayed elements
        List<ListItemFileIcon> SearchResults = new List<ListItemFileIcon>();

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
            settings = new UserSettings();
            settings.ResetDefaults();
            DataContext = settings;

            // Set size of window based on resolution
            var res = GetResolution();
            Width = res[0] * .22;
            Height = res[1] * .2;

            // Why duplicate effort
            if (!File.Exists("Apps.sqlite"))
            {
                if (MessageBox.Show("Could not find apps database, rescan?", "Rescan?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    InitDB();
                    ScanPrograms();
                }
            }

            // Do settings
            var settingsStr = new string[] { "IconSize", };

            // init fonts n' stuff
            userInput.FontFamily = previewResult.FontFamily = settings.FontFace;
            userInput.FontSize = previewResult.FontSize = Height * 0.25;
            userInput.FontWeight = previewResult.FontWeight = FontWeight.FromOpenTypeWeight(300);
            userInput.Focus();

            // Bind search results display to SearchResult list
            listResults.ItemsSource = SearchResults;

            // Fade window in
            Opacity = 0;
            FadeComponentIn(this);
        }

        public void ShowSettingsWindow()
        {
            var settingsWindow = new Window1();
            settingsWindow.Show();
        }

        void FadeComponentIn(FrameworkElement Component, double toOpacity = 1.0)
        {
            // Utterly neat startup fade in!
            // https://stackoverflow.com/questions/6512223/how-to-show-hide-wpf-window-by-blur-effect
            var sb = new Storyboard();
            var da = new DoubleAnimation(0.0, toOpacity, new Duration(new TimeSpan(0, 0, 0, 0, settings.FadeInTime))) { AutoReverse = false };
            Storyboard.SetTargetProperty(da, new PropertyPath(OpacityProperty));
            sb.Children.Clear();
            sb.Children.Add(da);
            sb.Begin(Component);
        }

        void FadeComponentOut(FrameworkElement Component)
        {
            var sb = new Storyboard();
            var da = new DoubleAnimation((double)Component.Opacity, 0, new Duration(new TimeSpan(0, 0, 0, 0, settings.FadeOutTime))) { AutoReverse = false };
            Storyboard.SetTargetProperty(da, new PropertyPath(OpacityProperty));
            sb.Children.Clear();
            sb.Children.Add(da);
            sb.Begin(Component);
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
                                WindowState = WindowState.Normal;
                                FadeComponentIn(this);
                            }
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private void ShowSettings()
        {
            listResults.ItemsSource = settings.SettingsList;
        }

        private void InitDB()
        {
            SQLiteConnection.CreateFile("Apps.sqlite");

            SQLiteConnection connection = new SQLiteConnection($"Data Source={settings.DBPath};version=3;");
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
            var masks = new List<string>() { 
                "*.exe", // "*.Exe", "*.EXE", 
                "*.lnk", //"*.Lnk", "*.LNK",
                //"*.bat", "*.Bat", "*.BAT",
                //"*.cmd", "*.Cmd", "*.CMD"
            };
            var errs = new List<string>();
            foreach (var path in settings.ScanFolders)
            {
                try
                {
                    try
                    {
                        var thing = masks.SelectMany(g => Directory.EnumerateFiles(path, g, SearchOption.AllDirectories));
                        foreach (var f in thing)
                        {
                            var cmd = new SQLiteCommand($"INSERT INTO applications (name, path) " +
                                $"VALUES( @name, @path );");
                            cmd.Parameters.AddWithValue("name", f.Split('\\').Last());
                            cmd.Parameters.AddWithValue("path", f);
                            if (!toAdd.Contains(cmd))
                            {
                                toAdd.Add(cmd);
                            }
                        }
                    }
                    // Access denied etc
                    catch (Exception ex) {
                        errs.Add(ex.Message);
                    }
                }
                // Directory not found
                catch (DirectoryNotFoundException)
                {
                    MessageBox.Show($"Could not find directory\n{path}");
                    continue;
                }
            }
            //MessageBox.Show(string.Join("\n", errs));

            // Write that ish to the DB
            using (var connection = new SQLiteConnection($"Data Source={settings.DBPath};version=3;"))
            {
                connection.Open();
                foreach (var cmd in toAdd)
                {
                    cmd.Connection = connection;
                    try
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (SQLiteException)
                    {
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
                FadeComponentOut(this);
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
                    if (MessageBox.Show($"Couldn't launch {SearchResults[0].Data}\nRemove from database?", "Error launching", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        // Remove from DB
                        // RemoveEntry(listResults.SelectedIndex == -1 ? SearchResults[0].Data : SearchResults[listResults.SelectedIndex].Data)
                    }
                    // Don't remove
                }
                // Minimize again and fade
                finally
                {
                    FadeComponentOut(this);
                    WindowState = WindowState.Minimized;
                }
            }
            // Mouseless functionality --> down & up navigate the results 
            // without losing focus of the textbox {big brain}
            if (e.Key == Key.Down)
            {
                if (listResults.SelectedIndex == SearchResults.Count) { return; } // Out of range
                listResults.SelectedIndex++;
            }
            if (e.Key == Key.Up)
            {
                if (listResults.SelectedIndex == -1) { return; } // Out of range
                listResults.SelectedIndex--;
            }
        }

        private List<ListItemFileIcon> GetSearchResults(string searchFor, int limit)
        {
            var ret = new List<ListItemFileIcon>();
            var sql = @"SELECT name, path FROM applications WHERE name LIKE @SearchFor LIMIT @Limit;";
            using (var connection = new SQLiteConnection($"Data Source={settings.DBPath};version=3;"))
            {
                connection.Open();
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@SearchFor", "%" + searchFor + "%");
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var name = reader.GetString(0);
                        var fp = reader.GetString(1);
                        var iconSource = System.Drawing.Icon.ExtractAssociatedIcon(fp);
                        // Have to wrap all UI components in this
                        // Gives a thread error 
                        var icon = Dispatcher.Invoke(() =>
                        {
                            return new Image()
                            {
                                Source = Imaging.CreateBitmapSourceFromHIcon(
                                    iconSource.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions()),
                                Margin = new Thickness(3),
                                Width = settings.IconSize,
                                Height = settings.IconSize
                            };
                        });
                        // Have to wrap all UI Components in this to avoid errors
                        ret.Add(Dispatcher.Invoke(() =>
                        {
                            return new ListItemFileIcon(name, fp, icon, settings);
                        }));
                    }
                    connection.Close();
                }
            }
            return ret;
        }

        private async void UserInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (userInput.Text.StartsWith(":"))
            {
                ShowSettings();
                return;
            }
            // Don't search for < 3 characters
            // TODO:
            // Make configurable? --> prolly
            if (userInput.Text.Length < 3)
            {
                FadeComponentOut(listResults);
                previewResult.Text = string.Empty;
                return;
            }

            var searchFor = userInput.Text;
            var maxResults = settings.MaxResults;

            var results = await Task.Run(() =>
            {
                return GetSearchResults(searchFor, maxResults);
            });

            SearchResults = results;

            if (SearchResults.Count > 0)
            {
                FadeComponentIn(listResults);
                listResults.SelectedIndex = 0;
                listResults.ItemsSource = SearchResults.Take(5);
                if (listResults.SelectedIndex == -1)
                {
                    previewResult.Text = SearchResults[0].Display;
                }
                else
                {
                    previewResult.Text = SearchResults[listResults.SelectedIndex].Display;
                }
            }
            // No results
            else
            {
                previewResult.Text = string.Empty;
                FadeComponentOut(listResults);
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

        private void Window_StateChanged(object sender, EventArgs e)
        {
            this.Focus();
            Keyboard.Focus(userInput);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
            {
                ShowSettingsWindow();
            }
        }
    }
}
