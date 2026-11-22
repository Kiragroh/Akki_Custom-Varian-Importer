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
$dispatcher = [Windows.Threading.Dispatcher]::CurrentDispatcher
[Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new($dispatcher))
function Invoke-Action([string]$name) {
    $button = $window.FindName($name)
    $button.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
    $timeout = [Diagnostics.Stopwatch]::StartNew()
    while (!$window.FindName('actions').IsEnabled) {
        if ($timeout.Elapsed.TotalSeconds -gt 30) { throw "Operation timed out: $name" }
        $dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Background)
        Start-Sleep -Milliseconds 10
    }
}
Invoke-Action 'previewButton'
Invoke-Action 'runButton'
$window.FindName('logTextBox').Text
