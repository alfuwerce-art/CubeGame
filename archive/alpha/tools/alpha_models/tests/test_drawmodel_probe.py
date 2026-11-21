"""Tests for tools.alpha_models.runtime.drawmodel_probe (failure/detach/restore safety).

These use a fake kernel32 so no process is touched: they prove the INT3 byte is always
restored (even when DebugActiveProcess fails or the loop raises), and that a clean detach
sets DebugSetProcessKillOnExit(FALSE) before DebugActiveProcessStop.
"""
import ctypes
import unittest

from tools.alpha_models.runtime import drawmodel_probe as dp

TARGET = 0x400000 + dp.MODEL_READ_RVA  # 0x4012A42 with a 0x400000 base


class FakeKernel:
    """Minimal kernel32 stand-in recording writes and detach calls."""

    def __init__(self, attach_ok=True, wait_result=0, wait_raise=None):
        self.attach_ok = attach_ok
        self.wait_result = wait_result
        self.wait_raise = wait_raise
        self.handle = 0x1234
        self.writes = []          # (address, bytes)
        self.kill_on_exit_args = []
        self.detach_calls = 0
        self.close_calls = 0
        self.memory = {}          # address -> bytes (ReadProcessMemory source)

    def OpenProcess(self, access, inherit, pid):
        return self.handle

    def ReadProcessMemory(self, handle, address, buffer, size, got):
        addr = address.value
        data = self.memory.get(addr, b"\x0F" if addr == TARGET else b"\x00" * size)
        data = (data + b"\x00" * size)[:size]
        ctypes.memmove(buffer, data, size)
        if hasattr(got, "_obj"):  # ctypes.byref() CArgObject -> write the pointee
            got._obj.value = size
        return True

    def WriteProcessMemory(self, handle, address, buffer, size, written):
        addr = address.value
        data = bytes(buffer)[:size]
        self.writes.append((addr, data))
        return True

    def DebugActiveProcess(self, pid):
        return self.attach_ok

    def DebugActiveProcessStop(self, pid):
        self.detach_calls += 1
        return True

    def DebugSetProcessKillOnExit(self, value):
        self.kill_on_exit_args.append(value)
        return True

    def CloseHandle(self, handle):
        self.close_calls += 1
        return True

    def WaitForDebugEvent(self, event, timeout):
        if self.wait_raise is not None:
            raise self.wait_raise
        return self.wait_result

    def ContinueDebugEvent(self, pid, tid, status):
        return True

    def OpenThread(self, access, inherit, tid):
        return 0x5678

    def Wow64GetThreadContext(self, thread, context):
        return True

    def Wow64SetThreadContext(self, thread, context):
        return True

    def CreateToolhelp32Snapshot(self, flags, pid):
        return 0  # empty snapshot -> exe_base returns (None, None)

    def Module32FirstW(self, snap, entry):
        return False


class ProbeFailureSafety(unittest.TestCase):
    def _run(self, **kwargs):
        kwargs.setdefault("base", 0x400000)
        kwargs.setdefault("module", "Cube.exe")
        return dp.run_probe(1234, kernel=FakeKernel(**kwargs))

    def test_restores_byte_on_debug_attach_failure(self):
        fake = FakeKernel(attach_ok=False)
        with self.assertRaises(RuntimeError):
            dp.run_probe(1234, kernel=fake, base=0x400000, module="Cube.exe")
        # INT3 was written, then the original byte was restored
        self.assertIn((TARGET, b"\xCC"), fake.writes)
        self.assertIn((TARGET, b"\x0F"), fake.writes)
        # never attached -> no detach, but handle closed
        self.assertEqual(fake.detach_calls, 0)
        self.assertEqual(fake.close_calls, 1)

    def test_module_not_found_raises_without_writing(self):
        fake = FakeKernel()
        with self.assertRaises(RuntimeError):
            dp.run_probe(1234, kernel=fake, base=None, module=None)
        # no INT3 write ever happened; exe_base closed its (empty) snapshot, run_probe the handle
        self.assertEqual(fake.writes, [])
        self.assertEqual(fake.close_calls, 2)
        self.assertEqual(fake.detach_calls, 0)

    def test_clean_detach_sequence(self):
        fake = FakeKernel(attach_ok=True, wait_result=0)  # timeout -> 0 hits
        hits, seen = dp.run_probe(1234, kernel=fake, base=0x400000, module="Cube.exe")
        self.assertEqual(hits, [])
        # kill-on-exit must be cleared before detaching
        self.assertEqual(fake.kill_on_exit_args, [False])
        self.assertEqual(fake.detach_calls, 1)
        self.assertEqual(fake.close_calls, 1)
        # original byte restored after the loop
        self.assertEqual(fake.writes[-1], (TARGET, b"\x0F"))

    def test_restores_and_detaches_on_loop_exception(self):
        class Boom(Exception):
            pass

        fake = FakeKernel(attach_ok=True, wait_raise=Boom())
        with self.assertRaises(Boom):
            dp.run_probe(1234, kernel=fake, base=0x400000, module="Cube.exe")
        # abnormal termination still restores the byte and detaches cleanly
        self.assertIn((TARGET, b"\x0F"), fake.writes)
        self.assertEqual(fake.kill_on_exit_args, [False])
        self.assertEqual(fake.detach_calls, 1)
        self.assertEqual(fake.close_calls, 1)


if __name__ == "__main__":
    unittest.main()
