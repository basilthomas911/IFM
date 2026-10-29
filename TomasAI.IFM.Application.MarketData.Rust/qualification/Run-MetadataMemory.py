"""Run the allocator-qualified metadata test in a Windows Job with a 256 MiB
private-memory ceiling. Only the dedicated test child is controlled; no trading
process, Host or API is attached or terminated. Build the test executable first.
"""
import ctypes as c
from ctypes import wintypes as w
import json
import os
from pathlib import Path
import subprocess
import sys

if os.name != "nt":
    raise SystemExit("This qualification requires Windows Job Objects")
if len(sys.argv) != 2:
    raise SystemExit("Usage: Run-MetadataMemory.py <metadata_memory test exe>")

class Basic(c.Structure):
    _fields_ = [("user", c.c_int64), ("job_user", c.c_int64), ("flags", w.DWORD),
                ("min_ws", c.c_size_t), ("max_ws", c.c_size_t), ("active", w.DWORD),
                ("affinity", c.c_size_t), ("priority", w.DWORD), ("scheduling", w.DWORD)]

class Io(c.Structure):
    _fields_ = [(name, c.c_uint64) for name in ["reads", "writes", "other", "read_bytes", "write_bytes", "other_bytes"]]

class Limits(c.Structure):
    _fields_ = [("basic", Basic), ("io", Io), ("process_memory", c.c_size_t),
                ("job_memory", c.c_size_t), ("peak_process", c.c_size_t), ("peak_job", c.c_size_t)]

kernel = c.WinDLL("kernel32", use_last_error=True)
kernel.CreateJobObjectW.argtypes = [c.c_void_p, w.LPCWSTR]
kernel.CreateJobObjectW.restype = w.HANDLE
kernel.SetInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD]
kernel.AssignProcessToJobObject.argtypes = [w.HANDLE, w.HANDLE]
kernel.QueryInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD, c.c_void_p]
kernel.CloseHandle.argtypes = [w.HANDLE]
ntdll = c.WinDLL("ntdll")
ntdll.NtResumeProcess.argtypes = [w.HANDLE]
ntdll.NtResumeProcess.restype = c.c_long

job = kernel.CreateJobObjectW(None, None)
if not job:
    raise c.WinError(c.get_last_error())
child = None
try:
    limits = Limits()
    limits.basic.flags = 0x100 | 0x2000  # process memory limit; kill child on Job close
    limits.process_memory = 256 * 1024 * 1024
    if not kernel.SetInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits)):
        raise c.WinError(c.get_last_error())
    # Suspended creation ensures the ceiling applies before test code executes.
    child = subprocess.Popen([str(Path(sys.argv[1]).resolve()), "--nocapture"],
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                             creationflags=0x4)
    if not kernel.AssignProcessToJobObject(job, w.HANDLE(int(child._handle))):
        raise c.WinError(c.get_last_error())
    if ntdll.NtResumeProcess(w.HANDLE(int(child._handle))) != 0:
        raise RuntimeError("Could not resume memory-qualified test child")
    output, _ = child.communicate(timeout=60)
    print(output.decode("utf-8", errors="replace"))
    if not kernel.QueryInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits), None):
        raise c.WinError(c.get_last_error())
    print(json.dumps({"memory_limit_bytes": limits.process_memory,
                      "peak_process_private_bytes": limits.peak_process,
                      "exit_code": child.returncode}))
    if child.returncode:
        raise SystemExit(child.returncode)
finally:
    if child is not None and child.poll() is None:
        child.kill()
        child.wait()
    kernel.CloseHandle(job)
