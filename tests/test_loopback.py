"""Exercise the Release WPF import button against a synthetic, loopback-only SCP."""
import subprocess
import sys
import tempfile
from pathlib import Path

from pydicom.dataset import FileDataset, FileMetaDataset
from pydicom.uid import CTImageStorage, ExplicitVRLittleEndian, generate_uid
from pynetdicom import AE, evt
from pynetdicom.sop_class import Verification


def write_ct(path, number):
    meta = FileMetaDataset()
    meta.MediaStorageSOPClassUID = CTImageStorage
    meta.MediaStorageSOPInstanceUID = generate_uid()
    meta.TransferSyntaxUID = ExplicitVRLittleEndian
    ds = FileDataset(str(path), {}, file_meta=meta, preamble=bytes(128))
    ds.is_little_endian = True
    ds.is_implicit_VR = False
    ds.SOPClassUID = CTImageStorage
    ds.SOPInstanceUID = meta.MediaStorageSOPInstanceUID
    ds.Modality = "CT"
    ds.PatientName = "Synthetic^Test"
    ds.PatientID = "SYNTHETIC"
    ds.InstanceNumber = number
    ds.save_as(str(path), write_like_original=False)


def main(executable):
    received = []
    statuses = {1: 0x0000, 2: 0xB000, 3: 0xA700}

    def store(event):
        number = int(event.dataset.InstanceNumber)
        received.append(number)
        return statuses[number]

    ae = AE(ae_title="AKKI_TEST")
    ae.add_supported_context(Verification)
    ae.add_supported_context(CTImageStorage)
    server = ae.start_server(("127.0.0.1", 0), block=False,
                             evt_handlers=[(evt.EVT_C_STORE, store)])
    try:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            def run():
                return subprocess.run([
                    "powershell.exe", "-NoProfile", "-STA", "-File",
                    str(Path(__file__).with_name("invoke-import.ps1")),
                    str(Path(executable).resolve()), str(root),
                    str(server.server_address[1])
                ], check=True, capture_output=True, text=True, timeout=60).stdout

            (root / "other.txt").write_text("ordinary text")
            write_ct(root / "skip_ignore.dcm", 99)
            assert "No eligible DICOM files" in run()
            (root / "sub").mkdir()
            for i in statuses:
                write_ct(root / "sub" / str(i), i)
            log = run()
            assert sorted(received) == sorted(statuses), (received, log)
            assert "1 accepted, 1 warnings, 1 failed, 0 unconfirmed" in log, log
    finally:
        server.shutdown()


if __name__ == "__main__":
    main(sys.argv[1])
    print("Loopback import checks passed.")
