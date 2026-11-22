"""Exercise the Release WPF import button against a synthetic, loopback-only SCP."""
import subprocess
import sys
import tempfile
from pathlib import Path

from pydicom.dataset import FileDataset, FileMetaDataset
from pydicom.uid import CTImageStorage, ExplicitVRLittleEndian, generate_uid
from pynetdicom import AE, evt
from pynetdicom.sop_class import Verification, PatientRootQueryRetrieveInformationModelFind
from pydicom.dataset import Dataset


def write_ct(path, number, patient_id="SYNTHETIC", issuer=""):
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
    ds.PatientID = patient_id
    ds.IssuerOfPatientID = issuer
    ds.InstanceNumber = number
    ds.save_as(str(path), write_like_original=False)


def main(executable):
    received = []
    statuses = {1: 0x0000, 2: 0xB000, 3: 0xA700}

    def store(event):
        number = int(event.dataset.InstanceNumber)
        received.append(number)
        return statuses[number]

    queries = []
    def find(event):
        key = event.identifier.PatientID
        queries.append(key)
        if key == "FAIL":
            yield 0xA700, None
            return
        if key == "NEW":
            return
        for issuer in (["A", "B"] if key == "DUP" else ["A"]):
            match = Dataset()
            match.PatientID = key
            match.IssuerOfPatientID = issuer
            yield 0xFF00, match

    ae = AE(ae_title="AKKI_TEST")
    ae.add_supported_context(Verification)
    ae.add_supported_context(CTImageStorage)
    ae.add_supported_context(PatientRootQueryRetrieveInformationModelFind)
    server = ae.start_server(("127.0.0.1", 0), block=False,
                             evt_handlers=[(evt.EVT_C_STORE, store), (evt.EVT_C_FIND, find)])
    try:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            def run(*args):
                return subprocess.run([
                    "powershell.exe", "-NoProfile", "-STA", "-File",
                    str(Path(__file__).with_name("invoke-import.ps1")),
                    str(Path(executable).resolve()), str(root),
                    str(server.server_address[1]), *args
                ], check=True, capture_output=True, text=True, timeout=60).stdout

            (root / "other.txt").write_text("ordinary text")
            write_ct(root / "skip_ignore.dcm", 99)
            assert "No eligible DICOM files" in run()
            (root / "sub").mkdir()
            for i in statuses:
                write_ct(root / "sub" / str(i), i)
            log = run()
            assert "CT: 3" in log and "Synthetic^Test" not in log and "SYNTHETIC" not in log, log
            assert sorted(received) == sorted(statuses), (received, log)
            assert "1 accepted, 1 warnings, 1 failed, 0 unconfirmed" in log, log
            for path in (root / "sub").iterdir():
                path.unlink()
            cases = [("KNOWN", "A", "Exact match"), ("NEW", "", "No visible match"),
                     ("FAIL", "", "Unconfirmed"), ("DUP", "A", "Unconfirmed"),
                     ("MISMATCH", "B", "Unconfirmed"), ("*", "", "Unconfirmed"),
                     ("", "", "Unconfirmed"), ("KNOWN", "A", "Exact match")]
            for i, (pid, issuer, _) in enumerate(cases):
                write_ct(root / "sub" / str(i), 1, pid, issuer)
            received.clear()
            rows = run("-CheckOnly")
            for pid, issuer, expected in cases:
                count = 2 if pid == "KNOWN" else 1
                assert f"{pid}|{issuer}|{count}|{expected}" in rows, rows
            assert not received and sorted(queries) == sorted(["KNOWN", "NEW", "FAIL", "DUP", "MISMATCH"]), queries
            summary = run("-ConfirmedOnly")
            assert received == [1, 1] and "Skipped by ID check: 6" in summary, summary
            server.shutdown()
            ae = AE(ae_title="AKKI_TEST")
            ae.add_supported_context(Verification)
            ae.add_supported_context(CTImageStorage)
            server = ae.start_server(("127.0.0.1", 0), block=False,
                                     evt_handlers=[(evt.EVT_C_STORE, store)])
            received.clear()
            rows = run("-CheckOnly")
            assert "KNOWN|A|2|Unconfirmed" in rows, rows
            assert "Skipped by ID check: 8" in run("-ConfirmedOnly")
            assert not received, received
    finally:
        server.shutdown()


if __name__ == "__main__":
    main(sys.argv[1])
    print("Loopback import checks passed.")
