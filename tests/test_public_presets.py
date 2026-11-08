"""Check the public preset bundle contains no personal connection data."""
import json
from pathlib import Path
import re
import sys

root = Path(sys.argv[1]) / 'resources/profiles/Creality'
files = list(root.glob('*/Extrautior *.json'))
assert len(files) == 11, f'Expected printer + 4 processes + 6 filaments, got {len(files)}'
names = set()
for path in files:
    raw = path.read_text(encoding='utf-8')
    data = json.loads(raw)
    names.add(data['name'])
    assert data['from'] == 'system' and data['instantiation'] == 'true'
    assert data['setting_id']
    assert not re.search(r'https?://|\b(?:\d{1,3}\.){3}\d{1,3}\b|[A-Z]:[\\/]|(?:[0-9a-f]{2}:){5}[0-9a-f]{2}', raw, re.I), path.name
    for key in data:
        assert key == 'host_type' or not re.search(r'host|password|token|api.?key|serial|device|account|user.?id|mac.?address|url|ip.?address', key, re.I), (path.name, key)
        assert key not in {'printer_settings_id', 'print_settings_id', 'filament_settings_id', 'version'}
    if data['type'] != 'machine':
        assert data['compatible_printers'] == ['Creality Hi 0.4 nozzle - Extrautior']
    if data['name'].startswith('Yasin ABS'):
        assert data['filament_type'] == ['ABS']
machine = next(json.loads(p.read_text(encoding='utf-8')) for p in files if p.parent.name == 'machine')
assert machine['default_print_profile'] in names
assert all(name in names for name in machine['default_filament_profile'])
print('PASS: 11 selectable public presets, valid defaults, no private connection data.')
