# AKKI · DICOM batch importer for ARIA

AKKI is a standalone Windows application for sending a folder of DICOM files to a configured storage receiver without opening Eclipse for each patient. I originally wrote it for a research project using Eclipse 15.1. It uses fo-dicom to inspect files and send DICOM requests. Compatibility with other receivers or Eclipse versions requires local testing.

## Build the current source

Use Windows, Visual Studio or Build Tools with the .NET desktop workload, and the .NET Framework 4.8 targeting pack. From a **Developer PowerShell** at the repository root:

```powershell
$sln = 'Akki_ARIA_Importer_MG/SourceCode/Akki_ARIA_Importer_MG/Akki_ARIA_Importer_MG.sln'
msbuild $sln -restore -p:RestorePackagesConfig=true -p:Configuration=Release '-p:Platform=Any CPU'
$exe = Join-Path (Split-Path $sln) 'Akki_ARIA_Importer_MG/bin/Release/Akki_ARIA_Importer_MG.exe'
& $exe
```

Keep the generated executable, configuration, DLLs and icons together. The prebuilt executable elsewhere in the repository is historical. Build the source above for the current transfer reporting.

## Try a batch

1. Prepare a dedicated input folder with synthetic DICOM objects. Select **Set Input-Folder** and check whether **Include subdirectories** is appropriate. **Peak** previews the folder and can display patient names and IDs in the window.
2. Enter the receiving daemon's AE title, IP and port, then the calling AE title expected by the receiver. The shipped example settings need to be replaced with those of your test environment.
3. Use **Echo** to check connectivity. **Start Import -> Aria** sends eligible objects individually by C-STORE. AKKI skips filenames containing the case-sensitive text `_ignore` and files whose Modality tag it cannot read. It performs no anonymisation.
4. Read the transfer summary. The interface waits while each transfer completes. Status `0000` counts as accepted, `Bxxx` as a warning, other returned statuses as failed and a missing reply as unconfirmed. Check the receiver for warnings, uncertain transfers and the objects actually imported in ARIA. A read or send exception stops the batch. Earlier transfers can already have reached the receiver, so inspect its records before retrying.

**RT object order matters.** AKKI does not sort dependencies or promise directory enumeration order. If the receiver needs CT, RTSTRUCT, RTPLAN and RTDOSE in sequence, prepare separate folders and send one stage at a time, checking each at the receiver.

## Verify locally

The loopback test opens the compiled WPF window in memory, invokes its import button, and supplies synthetic files to a local test receiver. It covers an empty candidate list, filename filtering, recursive discovery and a mixed set of success, warning and failure replies. After the build, run in a Windows Python environment with the test dependencies:

```powershell
python -m pip install 'pydicom>=2.4,<3' 'pynetdicom>=2.1,<3'
python tests/test_loopback.py $exe
```

When you close the window, AKKI saves the folder and connection settings for the next session. If the saved input folder is unavailable, select a new one.

## Background

[Receiver setup notes (historical PDF)](Akki_ARIA_Importer_MG/Manual_Adding%20of%20requiered%20DicomDaemon.pdf) · [Original interface screenshot](Akki_ARIA_Importer_MG/GUI-screenshot.PNG) · [Varian API Book](https://github.com/VarianAPIs/VarianAPIs.github.io/blob/master/VarianApiBook.pdf) · [EvilDICOM](https://github.com/rexcardan/Evil-DICOM)

This independent research example is provided without warranty or vendor support. Validate the entire workflow in an isolated non-clinical environment before considering any other use.
