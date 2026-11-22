param([string]$Executable, [string]$Folder, [int]$Port)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$assembly = [Reflection.Assembly]::LoadFrom($Executable)
[Windows.Application]::ResourceAssembly = $assembly
$window = $assembly.CreateInstance('Akki_ARIA_Importer_MG.MainWindow')
$window.inputFolderPath = $Folder
$window.FindName('DaemonTitleTextBox').Text = 'AKKI_TEST'
$window.FindName('DaemonIpTextBox').Text = '127.0.0.1'
$window.FindName('DaemonPortTextBox').Text = [string]$Port
$window.FindName('AEtitleTextBox').Text = 'AKKI_SCU'
if ($window.FindName('DaemonIpTextBox').Text -ne '127.0.0.1' -or $window.FindName('DaemonPortTextBox').Text -ne [string]$Port) {throw 'Unexpected test endpoint'}
$button = $window.FindName('runButton')
$button.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
$window.FindName('logTextBox').Text
if (!$button.IsEnabled) { throw 'Import button remained disabled' }
