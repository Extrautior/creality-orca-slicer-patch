"""Reject incomplete plaintext vendor overrides of Orca's .opc release bundles."""
import argparse
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--payload', type=Path, required=True)
a = p.parse_args()
prefix = 'resources/profiles/'
for name in ('manifest.json', 'manifest-no-profiles.json'):
    path = a.payload / name
    if not path.exists():
        continue
    manifest = json.loads(path.read_text(encoding='utf-8-sig'))
    files = {entry['RelativePath']: entry for entry in manifest['Files']}
    def source(rel):
        entry = files[rel]
        return a.payload / 'root' / entry.get('SourceRelativePath', rel)
    index = json.loads(source(prefix + 'Creality.json').read_text(encoding='utf-8-sig'))
    required = {prefix + 'Creality.json'}
    for section in ('machine_model_list', 'machine_list', 'process_list', 'filament_list'):
        for entry in index[section]:
            required.add(prefix + 'Creality/' + entry['sub_path'])
    missing = sorted(rel for rel in required if rel not in files or not source(rel).is_file())
    if missing:
        raise SystemExit('Incomplete Creality vendor override: ' + ', '.join(missing[:12]))
    for rel in required:
        json.loads(source(rel).read_text(encoding='utf-8-sig'))
    owners = [rel for rel in files if Path(rel).name.startswith('Extrautior ')]
    assert len(owners) == (11 if manifest.get('IncludeProfiles', True) else 0)
    if not manifest.get('IncludeProfiles', True):
        model = json.loads(source(prefix + 'Creality/machine/Creality Hi.json').read_text())
        assert '@Extrautior Hi' not in model['default_materials']
    print(f'PASS: {name}: {len(required)} indexed JSON files complete, {len(owners)} optional presets.')
