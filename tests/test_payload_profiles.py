"""Reject incomplete plaintext vendor overrides of Orca's .opc release bundles."""
import argparse
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--payload', type=Path, required=True)
a = p.parse_args()
manifest = json.loads((a.payload / 'manifest.json').read_text(encoding='utf-8-sig'))
files = {entry['RelativePath'] for entry in manifest['Files']}
prefix = 'resources/profiles/'
index = json.loads((a.payload / 'root' / prefix / 'Creality.json').read_text(encoding='utf-8-sig'))
required = {prefix + 'Creality.json'}
for section in ('machine_model_list', 'machine_list', 'process_list', 'filament_list'):
    for entry in index[section]:
        required.add(prefix + 'Creality/' + entry['sub_path'])
missing = sorted(path for path in required
                 if path not in files or not (a.payload / 'root' / path).is_file())
if missing:
    raise SystemExit('Incomplete Creality vendor override: ' + ', '.join(missing[:12]))
for path in required:
    json.loads((a.payload / 'root' / path).read_text(encoding='utf-8-sig'))
print(f'PASS: complete packaged Creality index, {len(required)} referenced JSON files present and readable.')
