using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Akki_ARIA_Importer_MG.Properties;
using Dicom;
using Dicom.Network;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Akki_ARIA_Importer_MG
{
    public partial class MainWindow : Window
    {
        public string inputFolderPath;
        public MainWindow()
        {
            InitializeComponent();
            var settings = Settings.Default;
            inputFolderPath = Directory.Exists(settings.Input) ? settings.Input : "";
            pathInputTextBlock.Text = inputFolderPath;
            DaemonTitleTextBox.Text = settings.DaemonTitle;
            DaemonIpTextBox.Text = settings.DaemonIP;
            DaemonPortTextBox.Text = settings.DaemonPort;
            AEtitleTextBox.Text = settings.AEtitle;
        }

        private static string ReadModality(string path)
        {
            try
            {
                return DicomFile.Open(path, readOption: FileReadOption.ReadLargeOnDemand)
                    ?.Dataset?.GetSingleValue<string>(DicomTag.Modality);
            }
            catch (DicomException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private async Task<FileInfo[]> ScanAsync()
        {
            var folder = inputFolderPath;
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException();
            var scope = subDirCheckBox.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return await Task.Run(() => Directory.EnumerateFiles(folder, "*", scope)
                .Where(f => !Path.GetFileName(f).Contains("_ignore"))
                .Select(f => new FileInfo { Path = f, Modality = ReadModality(f) })
                .Where(f => !string.IsNullOrWhiteSpace(f.Modality)).ToArray());
        }

        private sealed class FileInfo
        {
            public string Path;
            public string Modality;
        }

        private void Report(string text)
        {
            ProgressTextBlock.Text = text;
            logTextBox.AppendText(text + Environment.NewLine);
            logTextBox.ScrollToEnd();
        }

        private async Task RunAsync(string status, Func<Task> operation)
        {
            if (!actions.IsEnabled) return;
            actions.IsEnabled = false;
            progressBar.Value = 0;
            ProgressTextBlock.Text = status;
            try { await operation(); }
            catch (DirectoryNotFoundException) { Report("Choose an existing source folder."); }
            catch (Exception ex) { Report("Operation stopped (" + ex.GetType().Name + ")."); }
            finally { actions.IsEnabled = true; }
        }

        private bool ValidConnection(out int port)
        {
            if (!int.TryParse(DaemonPortTextBox.Text, out port) || port < 1 || port > 65535)
            {
                Report("Enter a receiver port between 1 and 65535.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(DaemonIpTextBox.Text) ||
                !ValidAe(AEtitleTextBox.Text) || !ValidAe(DaemonTitleTextBox.Text))
            {
                Report("Enter a receiver host and both AE titles (1 to 16 printable ASCII characters).");
                return false;
            }
            return true;
        }

        private static bool ValidAe(string title)
        {
            return !string.IsNullOrWhiteSpace(title) && title.Length <= 16 &&
                title.All(c => c >= ' ' && c <= '~' && c != '\\');
        }

        private async Task SendAsync(DicomRequest request, int port)
        {
            var client = new DicomClient(DaemonIpTextBox.Text.Trim(), port, false,
                AEtitleTextBox.Text.Trim(), DaemonTitleTextBox.Text.Trim());
            await client.AddRequestAsync(request);
            await client.SendAsync();
        }

        private async Task<bool> EchoAsync(int port)
        {
            bool accepted = false;
            var request = new DicomCEchoRequest();
            request.OnResponseReceived = (sent, response) => accepted = response.Status.Code == 0;
            await SendAsync(request, port);
            return accepted;
        }

        private async void Echo_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync("Testing connection…", async () =>
            {
                int port;
                if (ValidConnection(out port))
                    Report(await EchoAsync(port) ? "Echo succeeded." : "Echo failed.");
            });
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync("Scanning folder…", async () =>
            {
                var files = await ScanAsync();
                if (files.Length == 0) { Report("No eligible DICOM files found."); return; }
                foreach (var group in files.GroupBy(f => f.Modality).OrderBy(g => g.Key))
                    Report(group.Key + ": " + group.Count());
                Report(files.Length + " eligible files. Preview complete.");
            });
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync("Preparing transfer…", async () =>
            {
                int port;
                if (!ValidConnection(out port)) return;
                var files = await ScanAsync();
                if (files.Length == 0) { Report("No eligible DICOM files found."); return; }
                if (!await EchoAsync(port)) { Report("Echo failed. No files sent."); return; }
                var result = new StoreSummary();
                for (int i = 0; i < files.Length; i++)
                {
                    ProgressTextBlock.Text = "Sending file " + (i + 1) + " of " + files.Length;
                    try
                    {
                        var path = files[i].Path;
                        var request = await Task.Run(() => new DicomCStoreRequest(path));
                        int? status = null;
                        request.OnResponseReceived = (sent, response) => status = response.Status.Code;
                        await SendAsync(request, port);
                        result.Record(status);
                        if (status != 0)
                            Report("File " + (i + 1) + ": " +
                                (status.HasValue ? "C-STORE 0x" + status.Value.ToString("X4") : "no response"));
                    }
                    catch (Exception ex)
                    {
                        Report("Stopped at file " + (i + 1) + " of " + files.Length + " (" +
                            ex.GetType().Name + "). Check the receiver before retrying. Prior replies: " + result);
                        return;
                    }
                    progressBar.Value = 100.0 * (i + 1) / files.Length;
                }
                Report(result.ToString());
            });
        }

        private void SetInputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = inputFolderPath };
            if (dialog.ShowDialog(this) != true) return;
            pathInputTextBlock.Text = inputFolderPath = dialog.SelectedPath;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!actions.IsEnabled) { e.Cancel = true; return; }
            var settings = Settings.Default;
            settings.Input = inputFolderPath;
            settings.DaemonTitle = DaemonTitleTextBox.Text;
            settings.DaemonIP = DaemonIpTextBox.Text;
            settings.DaemonPort = DaemonPortTextBox.Text;
            settings.AEtitle = AEtitleTextBox.Text;
            settings.Save();
            base.OnClosing(e);
        }

        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
        private void Disclaimer_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "Independent research software. Validate your receiver and workflow in an isolated test environment.", "Research use");
        }
        private void AboutMe_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "AKKI DICOM batch importer\nMaximilian Grohmann · github.com/Kiragroh", "About AKKI");
        }
    }
}
