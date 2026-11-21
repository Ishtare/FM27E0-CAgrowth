"""Restore the complete name-bank facts from the lossless source snapshot, without FM or network access."""
from pathlib import Path
import argparse
import gzip
import hashlib
import json

def unpack(source, output):
    text = gzip.decompress(source.read_bytes()).decode('utf-8').splitlines()
    if not text or text[0] != 'FMEDITOR-NAMES-1':
        raise ValueError('Unknown name-bank format')
    markers = {'[countries]':'countries.tsv', '[words]':'words.tsv', '[contexts]':'contexts.tsv'}
    sections = {}
    current = None
    for line in text[1:]:
        if line in markers:
            current = markers[line]
            if current in sections:
                raise ValueError('Duplicate name-bank section')
            sections[current] = []
        elif current is None:
            raise ValueError('Missing name-bank section')
        else:
            sections[current].append(line)
    if set(sections) != set(markers.values()):
        raise ValueError('Incomplete name-bank snapshot')
    output.mkdir(parents=True, exist_ok=True)
    report = {}
    for filename, rows in sections.items():
        content = ('\ufeff'+'\r\n'.join(rows)+'\r\n').encode('utf-8')
        target = output/filename
        if target.exists() and target.read_bytes() != content:
            raise ValueError('Refusing to overwrite changed data: '+str(target))
        target.write_bytes(content)
        report[filename] = {'rows':len(rows)-1, 'bytes':len(content), 'sha256':hashlib.sha256(content).hexdigest()}
    return report

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path, help='Folder for the restored TSV source facts')
    parser.add_argument('--source', type=Path, default=Path(__file__).resolve().parent.parent/'name-data/name-bank.gz')
    args = parser.parse_args()
    print(json.dumps(unpack(args.source, args.output), ensure_ascii=False, indent=2))
