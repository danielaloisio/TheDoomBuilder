#!/usr/bin/env python3
"""Lists the interface texts that a language file does not translate yet.

    tools/missing_translations.py assets/Common/Languages/pt-BR.json

The English texts are the keys (see Core/Localization/Localizer.cs). They are collected from Loc.T("...") calls in the App, the menu and toolbar
texts of mainform-ui.json, and the settings pages of PreferencesModel. Print the result, translate it, add it to the "strings" table of the file.
"""
import glob, json, os, re, sys

root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
have = set(json.load(open(sys.argv[1], encoding='utf-8'))['strings'])
keys = []

for f in glob.glob(os.path.join(root, 'src', 'DoomBuilder.App', '**', '*.cs'), recursive=True):
    if os.sep + 'obj' + os.sep in f: continue
    for m in re.finditer(r'Loc\.T\("((?:[^"\\]|\\.)+)"', open(f, encoding='utf-8').read()):
        keys.append(m.group(1))

ui = json.load(open(os.path.join(root, 'src', 'DoomBuilder.App', 'Resources', 'mainform-ui.json'), encoding='utf-8'))
def walk(o):
    if isinstance(o, dict):
        for k in ('text', 'tooltip'):
            if isinstance(o.get(k), str) and o[k].strip(): keys.append(o[k])
        for v in o.values(): walk(v)
    elif isinstance(o, list):
        for v in o: walk(v)
walk(ui)

model = open(os.path.join(root, 'src', 'DoomBuilder.Core', 'Windows', 'PreferencesModel.cs'), encoding='utf-8').read()
for m in re.finditer(r'(?:Flag|Slider|Alpha|Choice|Folder|Text)\(\w+, "([^"]+)", "([^"]+)"', model): keys += [m.group(1), m.group(2)]
for m in re.finditer(r'Hue\("([^"]+)"', model): keys.append(m.group(1))
for m in re.finditer(r'new\[\] \{ ([^}]*) \}', model): keys += re.findall(r'"([^"]+)"', m.group(1))

missing = []
for k in keys:
    k = k.replace('\\r\\n', '\r\n')
    if k not in have and k not in missing and not re.fullmatch(r'[\d.,% ]*(mp)?', k): missing.append(k)
print(json.dumps(missing, ensure_ascii=False, indent=1))
print(len(missing), 'missing', file=sys.stderr)
