#!/usr/bin/env python3
"""Extracts the menu bar and toolbars of UDB's WinForms MainForm into a JSON model the Avalonia shell builds its UI from.

The WinForms designer file is regular: every ToolStrip item is `this.<name> = new ...ToolStripXxx()` followed by
`this.<name>.<Property> = ...` lines, and containers list their children in Items/DropDownItems.AddRange.

Usage (repo root):
    python3 tools/extract_mainform_ui.py <path to UDB Source/Core/Windows/MainForm.Designer.cs> > src/DoomBuilder.App/Resources/mainform-ui.json
"""
import json, re, sys
from collections import OrderedDict

src = open(sys.argv[1], encoding='utf-8-sig').read()

# ---- object declarations
classes = {}
for m in re.finditer(r'this\.(\w+) = new (?:[\w]+\.)*(\w+)\(', src):
    classes[m.group(1)] = m.group(2)

# ---- simple property assignments
props = {}
def setprop(name, prop, value):
    props.setdefault(name, {})[prop] = value

for m in re.finditer(r'this\.(\w+)\.(Text|Tag|ToolTipText|ShortcutKeyDisplayString|Name|DisplayStyle|CheckOnClick|Checked|Enabled|Visible)\s*=\s*(.+?);\s*$', src, re.M):
    name, prop, value = m.groups()
    if name not in classes: continue
    value = value.strip()
    if value.startswith('"') and value.endswith('"'):
        value = bytes(value[1:-1], 'utf-8').decode('unicode_escape')
    elif value in ('true', 'false'):
        value = value == 'true'
    elif prop == 'DisplayStyle':
        value = value.split('.')[-1]
    setprop(name, prop, value)

for m in re.finditer(r'this\.(\w+)\.Image\s*=\s*global::CodeImp\.DoomBuilder\.Properties\.Resources\.(\w+);', src):
    if m.group(1) in classes: setprop(m.group(1), 'Image', m.group(2))

clicks = {}
for m in re.finditer(r'this\.(\w+)\.Click \+= (?:new System\.EventHandler\()?(?:this\.)?(\w+)\)?;', src):
    if m.group(1) in classes: clicks[m.group(1)] = m.group(2)

# ---- containers
children = {}
for m in re.finditer(r'this\.(\w+)\.(?:DropDownItems|Items)\.AddRange\(new System\.Windows\.Forms\.ToolStripItem\[\] \{(.*?)\}\);', src, re.S):
    children[m.group(1)] = re.findall(r'this\.(\w+)', m.group(2))

def node(name):
    cls = classes.get(name, '?')
    if cls == 'ToolStripSeparator':
        return OrderedDict(type='separator', name=name)
    p = props.get(name, {})
    n = OrderedDict()
    # UDB has its own ToolStripActionButton/ToolStripActionMenuItem... (they show the shortcut of their action in the tooltip)
    kinds = [('MenuItem', 'menu'), ('SplitButton', 'split'), ('DropDownButton', 'dropdown'), ('Button', 'button'), ('StatusLabel', 'statuslabel'),
             ('Label', 'label'), ('ComboBox', 'combo'), ('TextBox', 'textbox')]
    n['type'] = next((k for suffix, k in kinds if cls.endswith(suffix)), cls)
    n['name'] = name
    for key, out in (('Text', 'text'), ('ToolTipText', 'tooltip'), ('Tag', 'action'), ('Image', 'image'),
                     ('ShortcutKeyDisplayString', 'shortcut'), ('CheckOnClick', 'checkonclick'), ('Checked', 'checked'),
                     ('DisplayStyle', 'displaystyle')):
        if key in p and p[key] not in ('', None): n[out] = p[key]
    if name in clicks: n['handler'] = clicks[name]
    if name in children: n['items'] = [node(c) for c in children[name]]
    return n

# top-level strips
strips = [n for n, c in classes.items() if c in ('MenuStrip', 'ToolStrip', 'StatusStrip')]
model = OrderedDict()
for s in strips:
    model[s] = OrderedDict(kind=classes[s], items=[node(c) for c in children.get(s, [])])

json.dump(model, sys.stdout, indent=1)
print()
