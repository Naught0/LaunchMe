using System;
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
using System.Drawing;

namespace LaunchMe
{
    public partial class ListItemData : ListViewItem
    {
        public string Display { get; set; }
        public string Data { get; set; }
        public Icon Icon { get; set; }
        public ListItemData(string display, string data, Icon icon)
        {
            Display = display;
            Data = data;
            Icon = icon;
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
        string WinPath = Environment.GetEnvironmentVariable("PATH");
        public double InternalPadding { get; set; }

        public int DefaultFontSize { get; set; }
        public System.Windows.Media.FontFamily FontFace { get; set; } = new System.Windows.Media.FontFamily("Segoe UI");

        public string DBPath { get; set; } = "Apps.sqlite";

        List<ListItemData> SearchResults = new List<ListItemData>();

        bool ResultsWindowShown = false;

        int MaxResults = 5;

        public MainWindow()
        {
            InitializeComponent();
            var res = GetResolution();
            Width = res[0] * .22;
            Height = res[1] * .18;
            InternalPadding = Height * 0.8;

            InitDB();
            ScanPrograms();

            userInput.FontFamily = FontFace;
            userInput.FontSize = Height * 0.3;
            userInput.FontWeight = FontWeight.FromOpenTypeWeight(300);
            userInput.Focus();

            listResults.ItemsSource = SearchResults;
        }

        private void InitDB()
        {
            SQLiteConnection.CreateFile("Apps.sqlite");

            SQLiteConnection connection = new SQLiteConnection($"Data Source={DBPath};version=3;");
            connection.Open();

            var sql = "CREATE TABLE 'applications'( "
            + "'id'    INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT UNIQUE, "
            + "'name'  TEXT NOT NULL, "
            + "'path'  TEXT NOT NULL" 
            + ");";
            SQLiteCommand cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();

            connection.Close();
        }

        public async void ScanPrograms()
        {
            List<SQLiteCommand> toAdd = new List<SQLiteCommand>();
            
            // Find executables in registry
            // Find executables in PATHs
            var mask = "*.exe";
            var sources = WinPath.Split(';').ToList<string>();
            foreach (var path in sources)
            {
                try
                {
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
                    catch (SQLiteException ex){
                        MessageBox.Show("\n\n" + cmd.Parameters[0].Value.ToString());
                    }
                }
            }
        }

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
                Close();
            }
            if (e.Key == Key.Enter)
            {
                try
                {
                    Process.Start(SearchResults[listResults.SelectedIndex].Data);
                }
                catch (IndexOutOfRangeException ex)
                {
                    listResults.SelectedIndex = 0;
                }
                catch
                {
                    return;
                }
            }
        }


        private async void UserInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (userInput.Text.Length < 3)
            {
                listResults.Opacity = 0;
                return;
            }

            SearchResults = new List<ListItemData>();

            var sql = $@"SELECT name, path FROM applications WHERE name LIKE '%{userInput.Text}%';"; // This doesn't work

            using (var connection = new SQLiteConnection($"Data Source={DBPath};version=3;"))
            {
                connection.Open();
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    var reader = await cmd.ExecuteReaderAsync();
                    while (reader.Read())
                    {
                        SearchResults.Add(new ListItemData(reader.GetString(0), reader.GetString(1), System.Drawing.Icon.ExtractAssociatedIcon(reader.GetString(1))));
                    }
                }
                connection.Close();
            }
            if (SearchResults.Count > 0)
            {
                listResults.SelectedIndex = 0;
                listResults.Opacity = 1;
                listResults.ItemsSource = SearchResults;
                // TODO
                // searchIcon.Source; change to app icon later
                //searchIcon.Source = new BitmapImage();
                //foreach (var result in SearchResults)
                //{
                //    listResults.Items.Add(result);
                //}
                //List<string> toRemove = new List<string>();
                //foreach (var item in listResults.Items)
                //{
                //    if (!SearchResults.Contains(item))
                //    {
                //        toRemove.Add(item);
                //    }
                //}
                //foreach (var item in toRemove)
                //{
                //    listResults.Items.Remove(item);
                //}
            }
            else
            {
                // clear list
                //listResults.Items.Clear();
                listResults.Opacity = 0;
            }
        }

        private void UserInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Contains("'"))
            {
                e.Handled = true;
            }
        }
    }
}
