using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;

namespace LaunchMe
{
    public class UserSettings
    {
        // Padding for elems -> probably remove
        public int DefaultFontSize { get; set; }
        public FontFamily FontFace { get; set; }
        public string DBPath { get; } = "Apps.sqlite";
        public int MaxResults { get; set; }
        public int FadeInTime { get; set; }
        public int FadeOutTime { get; set; }
        public int IconSize { get; set; }
        public Brush ColorBackground { get; set; }
        public Brush ColorBackgroundSecondary { get; set; }
        public Brush ColorForeground { get; set; }
        public Brush ColorForegroundPreview { get; set; }
        public Brush ColorForegroundFaded { get; set; }
        public short Padding { get; set; }
        public static Image SettingsImage { get; set; } = new Image()
        {
            Source = new BitmapImage(new Uri("Images/cog.png", UriKind.Relative))
        };
        public List<ListItemFileIcon> SettingsList { get; set; }

        public List<string> ScanFolders { get; set; } = new List<string>();
        public UserSettings()
        {
            ResetDefaults();
        }

        public void ResetDefaults()
        {
            // Colors
            ColorForegroundFaded = BrushFromHex("#999999");
            ColorForegroundPreview = BrushFromHex("#777777");
            ColorForeground = BrushFromHex("#ECEFF4");
            ColorBackgroundSecondary = BrushFromHex("#3b4252");
            ColorBackground = BrushFromHex("#2e3440");

            // Other stuff
            FontFace = new FontFamily("Segoe UI");
            MaxResults = 5;
            IconSize = 24;
            Padding = 3;
            SettingsList = new List<ListItemFileIcon>()
            {
                new ListItemFileIcon("Max Results", MaxResults.ToString(), SettingsImage, this),
            };

            // Fade times
            FadeInTime = 200;
            FadeOutTime = 150;

            // Paths
            var paths = new List<string>() {
                @"C:\Program Files",
                @"C:\Program Files (x86)",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)
            };
            paths.AddRange(Environment.GetEnvironmentVariable("PATH").Split(';'));

            // Add to prop
            //MessageBox.Show(string.Join("\n", paths));
            ScanFolders.AddRange(paths);
        }

        private Brush BrushFromHex(string hexCode)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexCode));
        }
    }
}
