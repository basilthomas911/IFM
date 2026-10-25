"""Development-only Windows PE dependency audit; no third-party Python packages."""
import hashlib
import json
import struct
import sys
from pathlib import Path


def audit(path):
    data = Path(path).read_bytes()
    def read(fmt, offset):
        return struct.unpack_from(fmt, data, offset)
    if data[:2] != b'MZ':
        raise ValueError('Package is not a Windows executable')
    pe, = read('<I', 0x3c)
    if data[pe:pe+4] != b'PE\0\0':
        raise ValueError('Invalid PE signature')
    machine, count = read('<HH', pe+4)
    size, = read('<H', pe+20)
    optional = pe+24
    magic, = read('<H', optional)
    if machine != 0x8664 or magic != 0x20b:
        raise ValueError('Expected x64 PE32+ executable')
    image_base, = read('<Q', optional+24)
    directories = optional+112
    number, = read('<I', optional+108)
    if number < 15:
        raise ValueError('Missing PE data directories')
    if any(read('<II', directories+14*8)):
        raise ValueError('A CLR header is present')
    sections = [read('<IIII', optional+size+i*40+8) for i in range(count)]
    def offset(rva):
        for virtual_size, address, raw_size, raw in sections:
            if address <= rva < address+max(virtual_size, raw_size):
                return raw+rva-address
        raise ValueError('PE RVA is outside its sections')
    def name(rva):
        start = offset(rva)
        end = data.index(b'\0', start, start+512)
        return data[start:end].decode('ascii')
    imports = set()
    rva, length = read('<II', directories+8)
    if rva:
        cursor = offset(rva)
        for _ in range(1024):
            entry = read('<IIIII', cursor)
            if not any(entry):
                break
            imports.add(name(entry[3]).lower())
            cursor += 20
        else:
            raise ValueError('Import descriptor bound exceeded')
    delayed, _ = read('<II', directories+13*8)
    if delayed:
        cursor = offset(delayed)
        for _ in range(1024):
            entry = read('<IIIIIIII', cursor)
            if not any(entry):
                break
            imports.add(name(entry[1] if entry[0] & 1 else entry[1]-image_base).lower())
            cursor += 32
        else:
            raise ValueError('Delay import descriptor bound exceeded')
    forbidden = [dll for dll in imports if dll.startswith(('vcruntime', 'msvcp', 'hostfxr', 'hostpolicy', 'coreclr', 'mscoree'))]
    if forbidden:
        raise ValueError('Non-standalone runtime imports: '+', '.join(sorted(forbidden)))
    # The adapted calendar widget ships in the single executable. Its complete
    # upstream permission notice must travel with that executable as well.
    calendar_license = Path(__file__).resolve().parents[1] / 'src/date_picker/LICENSE-MIT'
    notice = calendar_license.read_text(encoding='utf-8').replace('\r\n', '\n').encode('utf-8')
    if notice not in data.replace(b'\r\n', b'\n'):
        raise ValueError('Embedded calendar MIT permission notice is missing')
    return {'file': Path(path).name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
            'machine': 'x64', 'clrHeader': False, 'imports': sorted(imports), 'calendarLicenseEmbedded': True,
            'qualification': 'Static PE/import audit only; clean Windows and feature acceptance remain required.'}


if __name__ == '__main__':
    report = audit(sys.argv[1])
    encoded = json.dumps(report, indent=2)
    if len(sys.argv) > 2:
        Path(sys.argv[2]).write_text(encoded+'\n', encoding='utf-8')
    print(encoded)
