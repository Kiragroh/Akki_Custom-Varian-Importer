# Akki_Custom-Varian-Importer
Import Dicom files from many patients at once without even opening Eclipse.

I build this little tool named Akki because I had to import many plans of many patients for a specific research project. Eclipse (TreatmentPlanningSystem from Varian) let you only import for one patient at a time.  
For development I utilized the VarianAPI-Book (https://github.com/VarianAPIs/VarianAPIs.github.io/blob/master/VarianApiBook.pdf), and the C#-package EvilDicom from Rex Cardan (https://github.com/rexcardan/Evil-DICOM). Both can easily be recommended.  
I think I build a tool that any Eclipse-User easily could make use of. You only need to create a extra Dicom-Daemon that will process the import requests. This can be done in under 5 minutes with the manual I also uploaded (extracted from the VarianAPI-Book).  
My program can process all Dicom files in a folder (with or without subdirectories). All none- or damaged- Dicom-files will be skipped.  
You can start the program immediately by using the EXE in the main folder. The source code for further development is also uploaded (subfolder: 'Akki_ARIA_Importer_MG') but can be deleted after downloading the repository if you are only interested in using the EXE. All other files should remain for the program to work.  
Requierements:  
1.) Windows operating system  
2.) rund the .exe in the downloaded and then extracted folder  
3.) have 'Microsoft .NET Framework 4.5' installed. Usually, it is automatically installed on a windows machine. On an internet PC, you will be prompted to install. Otherwise download here: https://www.microsoft.com/de-de/download/details.aspx?id=30653)  
Disclaimer: I do not work for Varian and this program should be used carefully. It is advised to try it on a non clinical workstation.
Asked questions:  
1.) Why no Export-Function?  
-> can be easily added. Export-request will be handled with the same DICOM-Daemon but Export is more complicated in the way that you have to give specific information to the Daemon. What modality (CT, RS, RD, RP, etc) from which patient? All DICOM-data or specific stuff? You get this information easily in conjunction with the Eclipse Scripting API. I already had a DataMining-Tool which I could equip with the Export-Function what solved the 'complication' immediately (last example: https://github.com/Kiragroh/ESAPI_Showcase_ComplexScripts). Import is easy because you present the Daemon with Dicom-Files and it handles the storage and Error-detection (you cannot import DICOMs with a script that cannot be also imported with Eclipse).
