"""Verify that a Windows Rust executable/DLL matches a PDB with types and source files."""
import argparse
import json
import math
import struct
from pathlib import Path


def read_streams(pdb):
    """Read the MSF directory without loading the complete PDB into memory."""
    header = pdb.read(56)
    if not header.startswith(b"Microsoft C/C++ MSF 7.00"):
        raise ValueError("Expected an MSF 7 PDB")
    block_size, _, _, directory_size, _, block_map = struct.unpack_from("<6I", header, 32)
    pdb.seek(block_map * block_size)
    directory_blocks = struct.unpack("<" + "I" * math.ceil(directory_size / block_size),
                                    pdb.read(4 * math.ceil(directory_size / block_size)))
    directory = bytearray()
    for block in directory_blocks:
        pdb.seek(block * block_size)
        directory.extend(pdb.read(block_size))
    count = struct.unpack_from("<I", directory)[0]
    sizes = struct.unpack_from("<" + "I" * count, directory, 4)
    offset = 4 + 4 * count
    streams = []
    for size in sizes:
        blocks = 0 if size == 0xFFFFFFFF else math.ceil(size / block_size)
        entries = struct.unpack_from("<" + "I" * blocks, directory, offset)
        offset += blocks * 4
        streams.append((size, entries))

    def read(index, limit=None):
        size, blocks = streams[index]
        wanted = size if limit is None else min(size, limit)
        result = bytearray()
        for block in blocks:
            pdb.seek(block * block_size)
            result.extend(pdb.read(min(block_size, wanted - len(result))))
            if len(result) >= wanted:
                break
        return result
    return read


def codeview(image):
    """Extract the image's RSDS PDB identity from the PE debug directory."""
    with image.open("rb") as binary:
        binary.seek(0x3C)
        pe = struct.unpack("<I", binary.read(4))[0]
        binary.seek(pe)
        if binary.read(4) != b"PE\0\0":
            raise ValueError("Expected a PE image")
        coff = binary.read(20)
        sections = struct.unpack_from("<H", coff, 2)[0]
        optional_size = struct.unpack_from("<H", coff, 16)[0]
        optional = binary.read(optional_size)
        directories = 112 if struct.unpack_from("<H", optional)[0] == 0x20B else 96
        debug_rva, debug_size = struct.unpack_from("<II", optional, directories + 6 * 8)
        section_rows = [binary.read(40) for _ in range(sections)]
        for section in section_rows:
            virtual_size, address, raw_size, raw = struct.unpack_from("<4I", section, 8)
            if address <= debug_rva < address + max(virtual_size, raw_size):
                binary.seek(raw + debug_rva - address)
                entries = binary.read(debug_size)
                for index in range(0, len(entries), 28):
                    entry = entries[index:index + 28]
                    if struct.unpack_from("<I", entry, 12)[0] == 2:
                        size, _, pointer = struct.unpack_from("<3I", entry, 16)
                        binary.seek(pointer)
                        record = binary.read(size)
                        if record[:4] == b"RSDS":
                            return record[4:20], struct.unpack_from("<I", record, 20)[0]
    raise ValueError("No RSDS PDB reference in image")


def verify(image, symbols, source):
    """Check identity, type records and the requested project's source-file entry."""
    guid, age = codeview(image)
    with symbols.open("rb") as pdb:
        read = read_streams(pdb)
        info = read(1, 28)
        if info[12:28] != guid or struct.unpack_from("<I", info, 8)[0] != age:
            raise ValueError("Image/PDB GUID or age mismatch")
        types = read(2, 20)
        first, last = struct.unpack_from("<II", types, 8)
        if last <= first:
            raise ValueError("PDB contains no full type records")
        dbi = read(3)
        module_size, contribution_size, map_size, source_size = struct.unpack_from("<4I", dbi, 24)
        files = dbi[64 + module_size + contribution_size + map_size:][:source_size]
        normalized = bytes(files).replace(b"\\", b"/").lower()
        expected = str(source.resolve()).replace("\\", "/").lower().encode()
        if expected not in normalized:
            raise ValueError("Requested project source file is absent from the PDB")
        return {"image": str(image.resolve()), "pdb": str(symbols.resolve()),
                "source": str(source.resolve()), "typeRecords": last - first,
                "sourceInfoBytes": source_size, "matchingPdb": True, "fullDebug": True}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path)
    parser.add_argument("pdb", type=Path)
    parser.add_argument("source", type=Path)
    options = parser.parse_args()
    print(json.dumps(verify(options.image, options.pdb, options.source)))
