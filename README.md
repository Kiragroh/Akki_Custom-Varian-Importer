# AKKI · DICOM batch importer for ARIA

Send DICOM files from a folder to a configured storage receiver on Windows. Preview the batch by modality, test the connection and check what the receiver reported for each transfer. AKKI runs independently of Eclipse and was originally written for a research project using Eclipse 15.1.

[Download the Windows build](https://github.com/Kiragroh/Akki_Custom-Varian-Importer/releases/tag/v1.1.0) · [Historical receiver setup notes](Akki_ARIA_Importer_MG/Manual_Adding%20of%20requiered%20DicomDaemon.pdf)

![AKKI source folder, receiver settings and activity window with synthetic example data](docs/interface.png)

## Run a batch

1. Extract the release ZIP on Windows with .NET Framework 4.8 installed. Keep its contents together and start `Akki_ARIA_Importer_MG.exe`. The older executable in the repository is a historical snapshot.
2. Choose **Browse…**, select a dedicated folder and check **Include subdirectories**. **Preview files** counts eligible objects by modality without sending them. It excludes filenames containing the case-sensitive text `_ignore` and files without a readable, nonblank Modality. The preview shows counts only. AKKI transmits the original objects with their identifiers.
3. Enter the **Receiver AE**, **Host or IP**, **Port** and **Calling AE** supplied by your DICOM administrator. Use **Test connection** to issue a C-ECHO. Settings are saved for the Windows user when the window closes. The distributed configuration contains examples.
4. **Send DICOM files** rescans the selected folder and performs a C-ECHO before the first C-STORE. Progress advances per object. The summary counts success (`0000`), warning (`Bxxx`), failure and missing replies separately. After an interruption, check the receiver before retrying because it may already have accepted part of the batch.

## Eclipse / ARIA 18

AKKI uses DICOM C-ECHO and C-STORE through fo-dicom. It requires no ESAPI assembly, Eclipse plug-in registration or open patient. For an Eclipse 18 installation, configure the DICOM receiver for the calling AE and the storage classes and transfer syntaxes used by your files.

A site connection check verified C-ECHO and negotiation of CT Image, RT Structure Set, RT Plan and RT Dose Storage. Synthetic C-STORE transfers were tested against a local receiver. To verify the full workflow on a new installation, send an approved test case to its test receiver and check the resulting objects and references in ARIA.

**RT dependencies.** AKKI sends files in directory enumeration order. If the receiver requires CT → RTSTRUCT → RTPLAN → RTDOSE, use one folder per stage and verify each stage at the receiver.

## Build and test

Use Windows, Visual Studio or Build Tools with the .NET desktop workload, and the .NET Framework 4.8 targeting pack. In a Developer PowerShell at the repository root:

```powershell
$sln = 'Akki_ARIA_Importer_MG/SourceCode/Akki_ARIA_Importer_MG/Akki_ARIA_Importer_MG.sln'
msbuild $sln -restore -p:RestorePackagesConfig=true -p:Configuration=Release '-p:Platform=Any CPU'
$exe = Join-Path (Split-Path $sln) 'Akki_ARIA_Importer_MG/bin/Release/Akki_ARIA_Importer_MG.exe'
python -m pip install 'pydicom>=2.4,<3' 'pynetdicom>=2.1,<3'
python tests/test_loopback.py $exe
```

The test invokes the compiled WPF preview and import controls. A loopback receiver returns success, warning and failure for synthetic objects. No site configuration is needed.

This independent research tool is provided without warranty or vendor support. Validate the receiving workflow in an isolated test environment before clinical use.
