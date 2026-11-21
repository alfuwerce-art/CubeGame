"""Byte-level extraction of Cube World Alpha 2013 creature poses (S05A-2 / D58).

Modules
-------
``pe_bytes``
    Minimal 32-bit PE reader and ``E8 rel32`` call scanner, standard library only.
``alpha_writes``
    Recovers ``*arrayElem_stride4(index) = value`` sites — the instruction shape that every
    pose assignment in ``Creature_drawModel`` compiles to — including the hidden ECX base,
    which the Ghidra reconstruction loses.
``pose_table``
    Turns those sites into the per-state pose table of ``Creature_drawModel`` (73 states)
    and cross-checks the result against the reconstructed source.

Nothing here ships or copies game bytes: the tools read a user-supplied ``Cube.exe`` and
print derived numbers, and the unit tests run on synthetic images.
"""
