"""Format Rust.eGui and document declarations; --check verifies without writing.

Install the pinned parser dependencies from requirements-readability.txt first.
Two blank lines separate function definitions; existing comments are preserved.
"""
import argparse
import time
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from rust_readability import ROOT, code_tokens, format_source, rustfmt


def main():
    """Apply or check the readability policy for the application's Rust files."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--jobs", type=int, default=4)
    parser.add_argument("paths", nargs="*", type=Path)
    args = parser.parse_args()
    files = args.paths or sorted(f for folder in ("src", "tests", "benches", "tools") for f in (ROOT / folder).rglob("*.rs") if not {"target", "obj", "bin"}.intersection(f.relative_to(ROOT).parts))
    def process(path):
        original = path.read_bytes()
        canonical = rustfmt(original)
        formatted = format_source(canonical, path.resolve().relative_to(ROOT).as_posix(), preformatted=True).encode("utf-8")
        if code_tokens(canonical) != code_tokens(formatted):
            raise SystemExit(f"Non-comment Rust tokens changed: {path}")
        if original.replace(b"\r\n", b"\n") != formatted:
            if not args.check:
                if path.read_bytes() != original:
                    raise SystemExit(f"File changed while formatting: {path}")
                for attempt in range(5):
                    try:
                        path.write_bytes(formatted)
                        break
                    except OSError:
                        if attempt == 4:
                            raise
                        time.sleep(0.1)
            return path
        return None
    with ThreadPoolExecutor(max_workers=max(1,args.jobs)) as executor:
        changed = [path for path in executor.map(process,files) if path is not None]
    print(f"Checked {len(files)} Rust files; {'would update' if args.check else 'updated'} {len(changed)}.")
    if args.check and changed:
        for path in changed:
            print(path.relative_to(ROOT))
        raise SystemExit(1)


if __name__ == "__main__":
    main()
