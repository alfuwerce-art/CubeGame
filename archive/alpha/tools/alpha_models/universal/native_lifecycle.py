"""Execute Alpha lifecycle helpers with the existing strict CPU, not a new animation engine.

Results are native-code evidence for supplied inputs. Call sites/input gates and world side
effects are separate evidence; the renderer's attack_timing is never assumed to be a lifecycle.
"""
import struct
from tools.alpha_models.universal import drawmodel_emu as de

SELECT_ATTACK = 0x445020
ADVANCE_HELD = 0x445410
ITEM_COOLDOWN = 0x4C6B80
READY_TO_START = 0x43E350
IDLE_TAIL_START,IDLE_TAIL_END=0x49C272,0x49C2E6


def call_creature(image, creature, va, ecx_offset=0, args=(),hooks=None):
    run=de.DrawModelRun(image)
    cpu=de.EmuCPU(image,stubs=run.stubs,max_steps=300000)
    cpu.hooks.update(hooks or {})
    cpu.mem.map(de.STACK_BASE,de.STACK_SIZE)
    c=cpu.mem.map(de.HEAP_BASE,0x2000,creature)
    misc=cpu.mem.map(de.Layout.MISC,0x1000)
    struct.pack_into('<II',misc,0x800,de.Layout.MISC+0x800,de.Layout.MISC+0x800)
    struct.pack_into('<I',c,0x1178,de.Layout.MISC+0x800)
    result=cpu.run(va,args=args,ecx=de.HEAP_BASE+ecx_offset)
    return result,bytes(c)


def select_attack(image, creature):
    result,_=call_creature(image,creature,SELECT_ATTACK)
    return result&255


def advance_held(image, creature):
    selected=[]
    result,c=call_creature(image,creature,ADVANCE_HELD,hooks={SELECT_ATTACK:lambda cpu:selected.append(True)})
    return {'result':result&255,'selected':bool(selected),'action':c[0x68],'timer_ms':struct.unpack_from('<i',c,0x6c)[0],'creature':c}


def item_cooldown(image, creature, item_offset=0x1e8):
    result,_=call_creature(image,creature,ITEM_COOLDOWN,ecx_offset=item_offset)
    return result


def ready_to_start(image,creature):
    result,_=call_creature(image,creature,READY_TO_START)
    return bool(result&255)


def idle_tail(image,creature,secondary_held=False):
    """Exact player input-tail bytes: excludes latched actions; >10000ms, NOT strike end.

    EDI=GameController; +5 is the actual secondary-button field (decompilation loses EDI).
    The block writes action only: the timer is not reset. Slot +0x1020 type 20 may route to 82.
    """
    run=de.DrawModelRun(image);cpu=de.EmuCPU(image,stubs=run.stubs,max_steps=300000)
    cpu.mem.map(de.STACK_BASE,de.STACK_SIZE)
    c=cpu.mem.map(de.HEAP_BASE,0x2000,creature)
    misc=cpu.mem.map(de.Layout.MISC,0x1000)
    struct.pack_into('<II',misc,0x800,de.Layout.MISC+0x800,de.Layout.MISC+0x800)
    struct.pack_into('<I',c,0x1178,de.Layout.MISC+0x800)
    controller=de.HEAP_BASE+0x300000
    header=cpu.mem.map(controller,0x100);header[5]=int(secondary_held)
    cpu.mem.map(controller+0x8006d0,4,struct.pack('<I',de.HEAP_BASE))
    cpu.r['edi']=controller;cpu.r['esp']=de.STACK_BASE+0x80000;cpu.eip=IDLE_TAIL_START
    while cpu.eip!=IDLE_TAIL_END:cpu.step()
    return {'action':c[0x68],'timer_ms':struct.unpack_from('<i',c,0x6c)[0],'creature':bytes(c)}
