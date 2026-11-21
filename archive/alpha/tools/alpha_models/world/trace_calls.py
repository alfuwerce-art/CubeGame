"""Trace direct callees of one Alpha function: target, stack args (as int), x87 result on return."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.terrain_oracle import Oracle  # noqa: E402

exe, fn, x, z = sys.argv[1], int(sys.argv[2], 16), int(sys.argv[3]), int(sys.argv[4])
watch = {int(a, 16) for a in sys.argv[5].split(",")}
o = Oracle(exe, 666666)
for rx in range(509, 516):
    for rz in range(509, 516):
        o.cell(rx, rz)
log = []


def make_entry(target):
    def entry(cpu):
        sp = cpu.r["esp"]
        ret = cpu.mem.u(sp, 4)
        args = [cpu.mem.u(sp + 4 + 4 * i, 4) for i in range(4)]
        rec = {"target": hex(target), "args": args, "ecx": cpu.r["ecx"]}
        log.append(rec)

        def on_ret(c, rec=rec, ret=ret):
            rec["st0"] = c.fpu[-1] if c.fpu else None
            c.hooks.pop(ret, None)
        cpu.hooks[ret] = on_ret
    return entry


for t in watch:
    o.cpu.hooks[t] = make_entry(t)
result = o.call_float(fn, [x, z, 0])
for rec in log:
    print(rec)
print("result", result)
