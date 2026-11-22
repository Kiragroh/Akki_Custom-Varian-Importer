using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Dicom.Network;
using System.Runtime.InteropServices;
using Akki_ARIA_Importer_MG.Properties;
using JR.Utils.GUI.Forms;
using System.Drawing;
using Dicom;

namespace Akki_ARIA_Importer_MG
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string logtext;
        public string inputFolderPath;
        public string outputFolderPath;
        public string defaultFolderPath;
        //public string outputFilename;
        //public StreamWriter reportFile;
        // holds standard error output collected during run of the DCMTK script
        private static StringBuilder stdErr = new StringBuilder("");
        

        public MainWindow()
        {
            InitializeComponent();
            // Add existing WPF control to the script window.
            //var mainControl = new EclipseDataMiner.MainWindow();
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string scriptVersion = fvi.FileVersion;
            //Window window = Window.GetWindow(this);
            Title = "Akki - ARIA-Importer by MG (v." + scriptVersion + ")";


            ProgressTextBlock.Text = "Press Start to begin Import";
            progressBar.Value = 0;

            
            if (Directory.Exists(Settings.Default.Input))
            {
                inputFolderPath = Settings.Default.Input;
            }
            else
            {
                inputFolderPath = string.Empty;
            }
            if (Directory.Exists(Settings.Default.Output))
            {
                outputFolderPath = Settings.Default.Output;
            }
            else
            {
                outputFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)+@"\anon";
                
            }
            
            pathInputTextBlock.Text = inputFolderPath;
            Settings.Default.Input = inputFolderPath;
            pathOutputTextBlock.Text = outputFolderPath;
            Settings.Default.Output = outputFolderPath;


            ShowLogMsg("Input path: " + inputFolderPath);
            //ShowLogMsg("Output path: " + outputFolderPath);

        }
        private void runButton_Click(object sender, RoutedEventArgs e)
        {
            runButton.IsEnabled = false;
            progressBar.Value = 0;
            try
            {
                int port;
                if (!TryGetReceiverPort(out port)) return;
                if (!Directory.Exists(inputFolderPath))
                {
                    Report("Select an existing input folder.");
                    return;
                }
                var files = FindCandidates(inputFolderPath);
                if (files.Length == 0)
                {
                    Report("No eligible DICOM files found.");
                    return;
                }

                if (!Echo(port))
                {
                    Report("Echo failed. No files sent.");
                    return;
                }
                var result = new StoreSummary();
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        var request = new DicomCStoreRequest(files[i]);
                        int? status = null;
                        request.OnResponseReceived = (sent, response) => status = response.Status.Code;
                        Send(request, port);
                        result.Record(status);
                        if (status != 0)
                            ShowLogMsg("File " + (i + 1) + ": " +
                                (status.HasValue ? "C-STORE 0x" + status.Value.ToString("X4") : "no response"));
                    }
                    catch (Exception ex)
                    {
                        Report("Stopped at file " + (i + 1) + " of " + files.Length +
                            " (" + ex.GetType().Name + "). Check receiver before retrying. " + result);
                        return;
                    }
                    progressBar.Value = 100.0 * (i + 1) / files.Length;
                }
                Report(result.ToString());
            }
            catch (Exception ex)
            {
                Report("Cannot complete import (" + ex.GetType().Name + ").");
            }
            finally
            {
                runButton.IsEnabled = true;
            }
        }

        private string[] FindCandidates(string folder)
        {
            var scope = subDirCheckBox.IsChecked == false
                ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            return Directory.EnumerateFiles(folder, "*", scope)
                .Where(f => !Path.GetFileName(f).Contains("_ignore") &&
                    DumpSingleDicomTag(f, "0008,0060") != "fail").ToArray();
        }

        private void Report(string message)
        {
            ShowLogMsg(message);
            ProgressTextBlock.Text = message;
        }

        private bool TryGetReceiverPort(out int port)
        {
            if (int.TryParse(DaemonPortTextBox.Text, out port) && port >= 1 && port <= 65535)
                return true;
            Report("Enter a receiver port between 1 and 65535.");
            return false;
        }

        private bool Echo(int port)
        {
            bool accepted = false;
            var request = new DicomCEchoRequest();
            request.OnResponseReceived = (sent, response) => accepted = response.Status.Code == 0;
            Send(request, port);
            return accepted;
        }

        private void Send(DicomRequest request, int port)
        {
            var client = new DicomClient();
            client.AddRequest(request);
            client.Send(DaemonIpTextBox.Text.Trim(), port, false,
                AEtitleTextBox.Text.Trim(), DaemonTitleTextBox.Text.Trim());
        }

        /// <summary>
        /// ShowLogMsg
        /// </summary>
        /// <param name="dataFile"></param>
        private void ShowLogMsg(string text)
        {
            logTextBox.AppendText(text + "\n");
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToEnd();
        }

        /// <summary>
        /// window closing event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.DaemonTitle = DaemonTitleTextBox.Text;
            Settings.Default.DaemonIP = DaemonIpTextBox.Text;
            Settings.Default.DaemonPort = DaemonPortTextBox.Text;
            Settings.Default.AEtitle= AEtitleTextBox.Text;
            Settings.Default.Save();
            base.OnClosing(e);
        }

        /// <summary>
        /// Set input and output filename and folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetInputFolderButton_Click(object sender, RoutedEventArgs e)
        {

            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            fbd.Description = "Select DICOM File input folder";
            //fbd.RootFolder = Environment.SpecialFolder.Favorites;
            fbd.SelectedPath = inputFolderPath;
            fbd.ShowNewFolderButton = true;
            fbd.UseDescriptionForTitle=true;
            
            if (fbd.ShowDialog() == true)
            {
                inputFolderPath = fbd.SelectedPath;
                pathInputTextBlock.Text = inputFolderPath;
                Settings.Default.Input = inputFolderPath;
                ShowLogMsg("\nnew Input path: " + inputFolderPath+"\n");

            }
            
        }

        private void SetOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {

            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            fbd.Description = "Select DICOM File output folder";
            fbd.SelectedPath = outputFolderPath;
            
            fbd.ShowNewFolderButton = true;
            fbd.UseDescriptionForTitle = true;

            if (fbd.ShowDialog() == true)
            {
                outputFolderPath = fbd.SelectedPath;
                pathOutputTextBlock.Text = outputFolderPath;
                Settings.Default.Output = outputFolderPath;
                ShowLogMsg("\nnew Output path: " + outputFolderPath+ "\n");
            }

        }
       

        public string MakeFilenameValid(string s)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (char ch in invalidChars)
            {
                s = s.Replace(ch, '_');
            }
            return s;
        }

        private string DumpSingleDicomTag(string dicomFile, string tagNumber)
        {
            var result = "empty";
            try
            {
                var dataset = DicomFile.Open(dicomFile,readOption:FileReadOption.ReadLargeOnDemand).Dataset;
                var tag = Dicom.DicomTag.Parse(tagNumber);
                result = dataset.GetString(tag);
            }
            catch
            {
                 result = "fail";
            }
            return result;
        }
        private string DumpPeakDicomTags(string dicomFile)
        {
            var result = "empty";
            try
            {
                var dataset = DicomFile.Open(dicomFile, readOption: FileReadOption.ReadLargeOnDemand).Dataset;
                var tagPN = Dicom.DicomTag.Parse("0010,0010");
                string resultPN = dataset.GetString(tagPN);
                var tagPID = Dicom.DicomTag.Parse("0010,0020");
                string resultPID = dataset.GetString(tagPID);
                var tagMod = Dicom.DicomTag.Parse("0008,0060");
                string resultMod = dataset.GetString(tagMod);
                result = resultPN + "_"+resultPID+"_" + resultMod;

            }
            catch
            {
                result = "fail";
            }
            return result;
        }
        

        private async void PeakInputFolderButton_Click(object sender, RoutedEventArgs e)
        {           
            if (Directory.Exists(inputFolderPath) )
            {

                string dir = inputFolderPath;
                var toAnonymize = FindCandidates(dir);
                int np = toAnonymize.Length;
                ShowLogMsg("\nNew Input-Peaking has started.");
                ShowLogMsg("Number of DICOM files for peaking: " + np.ToString());

                if (np > 0)
                {
                    ShowLogMsg("");
                    int count = 1;
                    List<string> peakList = new List<string>();
                    foreach (var file in toAnonymize)
                    {
                        try
                        {                            
                            //var dcm = EvilDICOM.Core.DICOMObject.Read(file);
                            await Task.Run(() => peakList.Add(DumpPeakDicomTags(file) + "_#"));
                            progressBar.Value = count * 100 / np;
                            ProgressTextBlock.Text = "Reading DICOM-Files....";
                            count++;
                        }
                        catch
                        {
                            ShowLogMsg("Error reading " + file);
                            count++;
                        }
                    }
                    var query = peakList.GroupBy(x => x.ToString(), (y, z) => new { Name = y, Count = z.Count() });
                    
                    // and to test...
                    foreach (var item in query)
                    {
                        ShowLogMsg(item.Name.ToString()+ item.Count.ToString());
                    }
                    
                    progressBar.Value = 0;
                    ProgressTextBlock.Text = "DICOM-Peak finished - Press Start to begin Import";
                }
                else
                {
                    ShowLogMsg("No Dicom-Peak possible. Folder does not contain Dicom-Files.");
                }
            }
            else
            {
                ShowLogMsg("\nNo Dicom-Peak possible. Folder does not exist.");
            }
        }

        private async void PeakOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(outputFolderPath))
            {
                string dir = outputFolderPath;
                var toAnonymize = FindCandidates(dir);
                int np = toAnonymize.Length;
                ShowLogMsg("\nNew Output-Peaking has started.");
                ShowLogMsg("Number of DICOM files for peaking: " + np.ToString());

                if (np > 0)
                {
                    ShowLogMsg("");
                    int count = 1;
                    List<string> peakList = new List<string>();
                    foreach (var file in toAnonymize)
                    {
                        try
                        {
                            await Task.Run(() => peakList.Add(DumpPeakDicomTags(file) + "_#"));
                            progressBar.Value = count * 100 / np;
                            ProgressTextBlock.Text = "Reading DICOM-Files....";
                            count++;
                        }
                        catch
                        {
                            ShowLogMsg("Error reading " + file);
                            count++;
                        }
                    }
                    var query = peakList.GroupBy(x => x.ToString(), (y, z) => new { Name = y, Count = z.Count() });

                    // and to test...
                    foreach (var item in query)
                    {
                        ShowLogMsg(item.Name.ToString() + item.Count.ToString());
                    }

                    progressBar.Value = 0;
                    ProgressTextBlock.Text = "DICOM-Peak finished - Press Start to begin Import";
                }
                else
                {
                    ShowLogMsg("No Dicom-Peak possible. Folder does not contain Dicom-Files.");
                }
            }
            else
            {
                ShowLogMsg("\nNo Dicom-Peak possible. Folder does not exist.");
            }
        }

        private void echoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int port;
                if (!TryGetReceiverPort(out port)) return;
                bool reached = Echo(port);
                echoButton.Background = reached ? System.Windows.Media.Brushes.LawnGreen :
                    System.Windows.Media.Brushes.PaleVioletRed;
                Report(reached ? "Echo succeeded." : "Echo failed.");
            }
            catch (Exception ex)
            {
                Report("Echo failed (" + ex.GetType().Name + ").");
            }
        }

        private void AboutMe_Click(object sender, RoutedEventArgs e)
        {
            FlexibleMessageBox.Show("See more of my apps on GitHub:\n\nhttps://github.com/Kiragroh\n\nPersonal information can be found on my LinkedIn:\n\nhttps://www.linkedin.com/in/maximilian-grohmann-b70588b1\n\nHave fun.\nMax", "About me");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Disclaimer_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This program is not tested for commercial or clinical use.\n\nYou use it at your own risk, and you are responsible for the interpretation of any results.", "Disclaimer");
        }

        

        private void selectDevFileButton_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaOpenFileDialog fbd = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            fbd.Multiselect = false;
            fbd.Title = "Select Dev-File-Template";
           /* if (File.Exists(Settings.Default.DevFile))
            { fbd.FileName = Settings.Default.DevFile; }
            else { fbd.FileName = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); }

            fbd.Filter = "DICOM files (*.dcm)|*.dcm|All Files (*.*)|*.*";

            if (fbd.ShowDialog() == true)
            {
                try
                {
                    var dcm = EvilDICOM.Core.DICOMObject.Read(fbd.FileName);
                    if (dcm.FindFirst(TagHelper.Modality).DData.ToString() == "RTDOSE")
                    {
                        selectedDevFileTextBox.Text = fbd.FileName;
                        Settings.Default.DevFile = fbd.FileName;
                        ShowLogMsg("new Dev-File-Template: " + fbd.FileName);
                        selectDevFileButton.Background = System.Windows.Media.Brushes.LawnGreen;
                    }
                    else
                    {
                        MessageBox.Show("The selected DICOM file does not have the required 'RTDOSE' modality.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        //ChangeDummyTemplateButton.Background = System.Windows.Media.Brushes.PaleVioletRed;
                    }
                }
                catch
                {
                    MessageBox.Show("DICOM reading failed", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    //ChangeDummyTemplateButton.Background = System.Windows.Media.Brushes.PaleVioletRed;
                }
            }*/
        }

        private void DevButton_Click(object sender, RoutedEventArgs e)
        {
            
            /*string dir = inputFolderPath;
            var toAnonymize = Enumerable.Empty<string>();
            if (subDirCheckBox.IsChecked == false)
            {
                toAnonymize = Directory.GetFiles(dir).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
            }
            else
            {
                toAnonymize = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
            }
            foreach (string file in toAnonymize)
            {

                try
                {
                    ShowLogMsg(file);

                }
                catch { }
            }*/

        }
        
    }
    

}
