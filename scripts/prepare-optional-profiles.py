"""Create the unchecked installer variant without the 11 Extrautior presets."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--payload', type=Path, required=True)
a = p.parse_args()
root = a.payload / 'root'
manifest_path = a.payload / 'manifest.json'
full = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
full['IncludeProfiles'] = True
manifest_path.write_text(json.dumps(full, indent=2), encoding='utf-8')
base = copy.deepcopy(full)
base['IncludeProfiles'] = False
owner_files = {f['RelativePath'] for f in full['Files']
               if f['RelativePath'].startswith('resources/profiles/Creality/')
               and Path(f['RelativePath']).name.startswith('Extrautior ')}
assert len(owner_files) == 11, owner_files
base['Files'] = [f for f in base['Files'] if f['RelativePath'] not in owner_files]
for entry in base['Files']:
    rel = entry['RelativePath']
    if rel not in ('resources/profiles/Creality.json', 'resources/profiles/Creality/machine/Creality Hi.json'):
        continue
    data = json.loads((root / rel).read_text(encoding='utf-8-sig'))
    if rel.endswith('/Creality.json'):
        data['version'] = '02.03.02.91'
        for section in ('machine_model_list', 'machine_list', 'process_list', 'filament_list'):
            data[section] = [x for x in data[section]
                             if 'resources/profiles/Creality/' + x['sub_path'] not in owner_files]
    else:
        data['default_materials'] = ';'.join(x for x in data['default_materials'].split(';')
                                              if not x.endswith('@Extrautior Hi'))
    source = '_variants/no-profiles/' + rel
    dest = root / source
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(data, indent=2), encoding='utf-8')
    entry.update(SourceRelativePath=source, Size=dest.stat().st_size,
                 Sha256=hashlib.sha256(dest.read_bytes()).hexdigest().upper())
(a.payload / 'manifest-no-profiles.json').write_text(json.dumps(base, indent=2), encoding='utf-8')
print('Prepared optional profiles: unchecked skips all 11 presets and their index/default references.')
