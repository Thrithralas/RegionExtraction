using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RegionExtraction {

    public partial class MainWindow {

        public const string DefaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Rain World";
        
        public MainWindow() {
            InitializeComponent();
            Directory.CreateDirectory("./backups");
            if (ConfigurationManager.AppSettings["GamePath"].Length == 0)
                ConfigurationManager.AppSettings["GamePath"] = DefaultPath;
            Path.Text = ConfigurationManager.AppSettings["GamePath"];
        }

        private void BackupRegion_OnClick(object sender, RoutedEventArgs e) {
            try {
                var regions = Directory.GetDirectories(Path.Text + @"\World\Regions\");
                foreach (string region in regions) {
                    string regionCode = region[region.Length - 2].ToString() + region[region.Length - 1];
                    var regionPath = region + @"\world_" + regionCode + ".txt";
                    if (!File.Exists(regionPath)) {
                        TextRange fText = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                        fText.Text += $"| Critical : FileNotFoundException | No world file for region {region}\n";
                        fText.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Coral);
                        continue;
                    }

                    if (File.Exists($"./backups/{regionCode}.txt") && MessageBox.Show($"A backup for region {regionCode} already exists. Do you wish to overwrite it?", "Overwrite Backup", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                        continue;
                    }
                    using StreamReader streamReader = new StreamReader(regionPath);
                    using StreamWriter streamWriter = new StreamWriter($"./backups/{regionCode}.txt", false);

                    bool readingCreatures = false;
                    string line;
                    while ((line = streamReader.ReadLine()) != null) {
                        if (line == "END CREATURES") readingCreatures = false;
                        if (readingCreatures && line != String.Empty) streamWriter.WriteLine(line);
                        if (line == "CREATURES") readingCreatures = true;
                    }
                    TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                    text.Text += $"| OK | Backed up region {regionCode}\n";
                    text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Teal);
                }
                
                TextRange textOk = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                textOk.Text += "| Success | Backed up all regions!\n";
                textOk.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.ForestGreen);
            }
            catch (Exception ex) {
                HandleException(ex);
            }
        }

        private void HandleException(Exception ex) {
            TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
            text.Text +=  $"| Error : {ex.GetType().Name} | ";
            switch (ex) {
                case DirectoryNotFoundException dnfe:
                    Regex regex = new Regex(@"[^']+");
                    MatchCollection matches = regex.Matches(dnfe.Message);
                    text.Text += $"Could not find directory {matches.Cast<Match>().First(m => m.Value.Contains('/') || m.Value.Contains('\\')).Value}";
                    break;
                case SecurityException:
                case UnauthorizedAccessException :
                    text.Text += "A directory/file is inaccesible due to lack of permissions. Try running this app as administrator.";
                    break;
                default:
                    text.Text += ex.Message;
                    break;
            }

            text.Text += "\n";
            text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Crimson);
        }

        private void WipeBackup_OnClick(object sender, RoutedEventArgs e) {
            if (ConfigurationManager.AppSettings["ConfirmWipeBackup"] == "1") {
                var result = MessageBox.Show("Are you sure you want to wipe your backup?", "Wipe Backup", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes) {
                    TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                    text.Text += "| Warning | Wiping cancelled\n";
                    text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Gold);
                    return;
                }
            }
            int count = 0;
            int failed = 0;
            if (!Directory.Exists("./backups/")) {
                HandleException(new DirectoryNotFoundException("'./backup'"));
                return;
            }
            foreach (string file in Directory.GetFiles("./backups/")) {
                try {
                    File.Delete(file);
                    count++;
                }
                catch (IOException) {
                    TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                    text.Text += "| Critical : IOException | File is already open by another program.\n";
                    text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Coral);
                    failed++;
                }
                catch (Exception ex) {
                    HandleException(ex);
                }
            }

            TextRange textF = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
            textF.Text += $"| Success | Deleted {count} backup(s)";
            if (failed != 0) textF.Text += $" and failed to delete {failed} backup(s)";
            textF.Text += ".\n";
            textF.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.ForestGreen);

        }


        private void Path_OnTextChanged(object sender, TextChangedEventArgs e) {
            if (Path.Text != String.Empty) ConfigurationManager.AppSettings["GamePath"] = Path.Text;
        }

        private void Reset_OnClick(object sender, RoutedEventArgs e) {
            Path.Text = DefaultPath;
            ConfigurationManager.AppSettings["GamePath"] = DefaultPath;
        }

        private void LoadFromBackup_OnClick(object sender, RoutedEventArgs e) {
            if (!Directory.Exists("./backups/")) {
                HandleException(new DirectoryNotFoundException("'./backups'"));
                return;
            }

            if (!Directory.Exists(Path.Text)) {
                HandleException(new DirectoryNotFoundException(Path.Text));
                return;
            }

            int count = 0;
            try {
                foreach (string region in Directory.GetFiles("./backups")) {
                    string regionCode = region.Substring(10, 2);
                    string content = File.ReadAllText($"./backups/{regionCode}.txt");
                    List<string> fContent = File.ReadAllLines(Path.Text + $@"\World\Regions\{regionCode}\world_{regionCode}.txt").ToList();
                    int startIndex = fContent.IndexOf("CREATURES");
                    if (startIndex == -1 || fContent[startIndex + 1] != "END CREATURES") {
                        throw new ApplicationException($"Either your world file for {regionCode} contains creatures or doesn't contain a CREATURES header.");
                    }

                    fContent.Insert(startIndex+1, content);
                    File.WriteAllLines(Path.Text + $@"\World\Regions\{regionCode}\world_{regionCode}.txt", fContent);

                    TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                    text.Text += $"| OK | Loaded region {regionCode} from backup.\n";
                    text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Teal);
                    count++;
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) {
                TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                text.Text += "| Error : PathNotFoundException | A region is missing a region file.\n";
                text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Crimson);
            }
            catch (Exception ex) {
                HandleException(ex);
            }

            TextRange textF = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
            textF.Text += $"| Success | Updated {count} regions from backup.";
            textF.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.ForestGreen);

        }

        private void ClearCreatures_OnClick(object sender, RoutedEventArgs e) {
            try {
                var regions = Directory.GetDirectories(Path.Text + @"\World\Regions\");
                foreach (string region in regions) {
                    string regionCode = region[region.Length - 2].ToString() + region[region.Length - 1];
                    var regionPath = region + @"\world_" + regionCode + ".txt";
                    if (!File.Exists(regionPath)) {
                        TextRange fText = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                        fText.Text += $"| Critical : FileNotFoundException | No world file for region {region}\n";
                        fText.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Coral);
                        continue;
                    }

                    List<string> regionContent = File.ReadAllLines(regionPath).ToList();
                    bool take = true;
                    List<string> result = new();
                    foreach (var line in regionContent) {
                        if (line == "END CREATURES") take = true;
                        if (take) result.Add(line);
                        if (line == "CREATURES") take = false;

                    }
                    File.WriteAllLines(regionPath, result);
                    
                    TextRange text = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                    text.Text += $"| OK | Cleared the creatures in region {regionCode}\n";
                    text.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Teal);
                }
                
                TextRange textOk = new TextRange(OutputBox.Document.ContentEnd, OutputBox.Document.ContentEnd);
                textOk.Text += "| Success | Removed all creatures!\n";
                textOk.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.ForestGreen);
            }
            catch (Exception ex) {
                HandleException(ex);
            }
        }

        private void MainWindow_OnClosed(object sender, EventArgs eventArgs) {
            ConfigurationManager.AppSettings["GamePath"] = Path.Text;
        }
    }

}