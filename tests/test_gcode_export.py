"""Slice a real two-color Hi project and check the exported firmware metadata."""
import argparse
import json
import math
from pathlib import Path
import re
import subprocess
import zipfile

p = argparse.ArgumentParser()
p.add_argument('--binary', type=Path, required=True)
p.add_argument('--profiles', type=Path, required=True)
p.add_argument('--work', type=Path, required=True)
a = p.parse_args()
binary, profiles, work = a.binary.resolve(), a.profiles.resolve(), a.work.resolve()
work.mkdir(parents=True, exist_ok=True)
lookup = {}
for vendor in ('Creality', 'OrcaFilamentLibrary'):
    for path in (profiles / vendor).rglob('*.json'):
        data = json.loads(path.read_text(encoding='utf-8'))
        if 'name' in data:
            lookup[data['name']] = data

def flatten(name):
    data = lookup[name]
    result = flatten(data['inherits']) if data.get('inherits') else {}
    result.update(data)
    result.pop('inherits', None)
    result['from'] = 'User'
    return result

machine = flatten('Creality Hi 0.4 nozzle - Extrautior')
machine.pop('print_host', None)
machine.pop('print_host_webui', None)
process = flatten('0.20mm Standard @Extrautior Hi')
# This fixture tests metadata independently of compacted-tower placement rules.
process['enable_prime_tower'] = '0'
material = flatten('Sunlu HS PLA+ 2.0 @Extrautior Hi')
for name, data in (('machine', machine), ('process', process), ('filament', material)):
    (work / (name + '.json')).write_text(json.dumps(data), encoding='utf-8')
vertices = [(x, y, z) for z in (0, 10) for y in (0, 10) for x in (0, 10)]
with (work / 'cube.stl').open('w') as f:
    f.write('solid cube\n')
    for i, j, k, l in ((0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)):
        for triangle in ((vertices[i],vertices[j],vertices[k]),(vertices[i],vertices[k],vertices[l])):
            f.write('facet normal 0 0 0\nouter loop\n')
            for point in triangle:
                f.write('vertex %g %g %g\n' % point)
            f.write('endloop\nendfacet\n')
    f.write('endsolid cube\n')
changes = {'mode':'MultiAsSingle','gcodes':[
    {'print_z':5.0,'type':'ToolChange','extruder':2,'color':'#000000','extra':''},
    {'print_z':5.4,'type':'ToolChange','extruder':1,'color':'#FFFFFF','extra':''}]}
(work / 'changes.json').write_text(json.dumps(changes), encoding='utf-8')

def run(tag, arguments):
    with (work / (tag + '.log')).open('w', encoding='utf-8') as log:
        result = subprocess.run([str(binary), '--datadir', str(work/'datadir'), *arguments],
                                cwd=work, stdout=log, stderr=subprocess.STDOUT, timeout=120)
    if result.returncode:
        raise RuntimeError(f'{tag} failed ({result.returncode}): ' + (work/(tag+'.log')).read_text(errors='replace')[-3000:])

filament = str(work/'filament.json')
run('baseline', ['--load-settings', str(work/'machine.json')+';'+str(work/'process.json'),
    '--load-filaments', filament+';'+filament, '--load-custom-gcodes', str(work/'changes.json'),
    '--arrange','1','--slice','0','--outputdir',str(work),'--export-3mf','baseline.3mf',str(work/'cube.stl')])
for factor in (0, 0.35, 0.7, 1, 1.3, 2):
    case = work / str(factor)
    case.mkdir(exist_ok=True)
    source = case / 'input.3mf'
    with zipfile.ZipFile(work/'baseline.3mf') as src, zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():
            body = src.read(item.filename)
            if item.filename == 'Metadata/project_settings.config':
                config = json.loads(body)
                config['flush_multiplier'] = [str(factor)]
                config['flush_volumes_matrix'] = ['0','100','200','0']
                body = json.dumps(config).encode()
            dst.writestr(item, body)
    run(str(factor), ['--slice','0','--outputdir',str(case),'--export-3mf','output.3mf',str(source)])
    with zipfile.ZipFile(case/'output.3mf') as z:
        gcode = z.read('Metadata/plate_1.gcode').decode()
        saved = json.loads(z.read('Metadata/project_settings.config'))
    multiplier = re.findall(r'^; flush_multiplier = (.+)$', gcode, re.M)
    matrix = re.findall(r'^; flush_volumes_matrix = (.+)$', gcode, re.M)
    assert len(multiplier) == len(matrix) == 1
    assert [float(x) for x in multiplier[0].strip().split(',')] == [1.0], (factor, multiplier)
    assert [float(x) for x in matrix[0].strip().split(',')] == [0, math.floor(100*factor+0.5), math.floor(200*factor+0.5), 0], (factor, matrix)
    assert [float(x) for x in saved['flush_volumes_matrix']] == [0,100,200,0], 'Slicing rewrote the project matrix'
print('PASS: actual Hi G-code export at 0, .35, .7, 1, 1.3, 2; multiplier always 1, volumes scaled once.')
