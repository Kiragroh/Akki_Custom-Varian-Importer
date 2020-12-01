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
using EvilDICOM;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Element;
using EvilDICOM.Network;
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
                inputFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
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
        private async void runButton_Click(object sender, RoutedEventArgs e)
        {
            
            // Store the details of the daemon (Ae Title , IP , port )
            var daemon = new Entity(DaemonTitleTextBox.Text, DaemonIpTextBox.Text, int.Parse(DaemonPortTextBox.Text));
            // Store the details of the client (Ae Title , port ) -> IP address is determined by CreateLocal() method
            var local = Entity.CreateLocal(AEtitleTextBox.Text, int.Parse(AEportTextBox.Text));
            // Set up a client ( DICOM SCU = Service Class User )
            var client = new DICOMSCU(local);
            var storer = client.GetCStorer(daemon);
            ushort msgId = 1;

            var canPing = client.Ping(daemon);

            if (canPing)
            {
                echoButton.Background = System.Windows.Media.Brushes.LawnGreen;
                progressBar.Value = 0;
                runButton.IsEnabled = false;

                if (!Directory.Exists(outputFolderPath))
                    Directory.CreateDirectory(outputFolderPath);

                runButton.IsEnabled = false;

                string dir = inputFolderPath;
                var toAnonymize = Enumerable.Empty<string>();
                if (subDirCheckBox.IsChecked == false)
                {
                    toAnonymize = Directory.GetFiles(dir).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
                }
                else
                {
                    toAnonymize = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
                }
                int np = toAnonymize.Count();
                ShowLogMsg("\nNew import process has started.");
                ShowLogMsg("Number of DICOM files for importing: " + np.ToString() + "\n");

                int count = 1;
                bool importSuccess = true;
                foreach (var file in toAnonymize)
                {
                    // Reads DICOM object into memory
                    var dcm = EvilDICOM.Core.DICOMObject.Read(file);
                    var response = storer.SendCStore(dcm, ref msgId);
                    // Write results to console
                    await Task.Run(() => ShowLogMsg($" DICOM C-Store of {Path.GetFileName(file)} from { local.AeTitle } => " +
                    $"{ daemon.AeTitle } {(EvilDICOM.Network.Enums.Status)response.Status }"));

                    progressBar.Value = count * 100 / np;
                    ProgressTextBlock.Text = "Import in progress....";

                    count++;

                }

                if (importSuccess == true)
                {
                    ShowLogMsg("\nImport completed successfully.");
                    ShowLogMsg("");
                    ProgressTextBlock.Text = "Import completed successfully";
                    progressBar.Value = 100;
                }
                else
                {
                    ShowLogMsg("\nImport completed with Erros.");
                    ShowLogMsg("");
                    ProgressTextBlock.Text = "Import completed with Errors";
                    progressBar.Value = 100;
                }


                runButton.IsEnabled = true;
            }
            else
            {
                echoButton.Background = System.Windows.Media.Brushes.PaleVioletRed;
                ShowLogMsg("\nNo import is possible. Echo to Daemon failed.");
                ProgressTextBlock.Text = "Echo failed. No Import was done.";
            }
            
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
            Settings.Default.AEport = AEportTextBox.Text;
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
                var toAnonymize = Enumerable.Empty<string>();
                if (subDirCheckBox.IsChecked == false)
                {
                    toAnonymize = Directory.GetFiles(dir).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
                }
                else
                {
                    toAnonymize = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
                }
                int np = toAnonymize.Count();
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
                var toAnonymize = Enumerable.Empty<string>();
                if (subDirCheckBox.IsChecked == false)
                {
                    toAnonymize = Directory.GetFiles(dir).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
                }
                else
                {
                    toAnonymize = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => DumpSingleDicomTag(f, "0008,0060") != "fail" & !Path.GetFileName(f).Contains("_ignore"));
                }
                int np = toAnonymize.Count();
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
                            var dcm = EvilDICOM.Core.DICOMObject.Read(file);
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
            // Store the details of the daemon (Ae Title , IP , port )
            var daemon = new Entity(DaemonTitleTextBox.Text, DaemonIpTextBox.Text, int.Parse(DaemonPortTextBox.Text));
            // Store the details of the client (Ae Title , port ) -> IP address is determined by CreateLocal() method
            var local = Entity.CreateLocal(AEtitleTextBox.Text, int.Parse(AEportTextBox.Text));
            // Set up a client ( DICOM SCU = Service Class User )
            var client = new DICOMSCU(local);
            var storer = client.GetCStorer(daemon);
            //ushort msgId = 1;

            var canPing = client.Ping(daemon);

            if (canPing)
            {
                echoButton.Background = System.Windows.Media.Brushes.LawnGreen;
                ShowLogMsg("\nImport is possible. Echo to Daemon succeeded.");
                ProgressTextBlock.Text = "Echo succeeded. Press Start to begin Import.";
            }
            else
            {
                echoButton.Background = System.Windows.Media.Brushes.PaleVioletRed;
                ShowLogMsg("\nNo import is possible. Echo to Daemon failed.");
                ProgressTextBlock.Text = "Echo failed. Import will also fail.";
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
