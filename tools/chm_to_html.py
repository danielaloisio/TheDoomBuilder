#!/usr/bin/env python3
"""Turns the extracted Refmanual.chm (7z x Refmanual.chm) into the editor's HTML help.

    tools/chm_to_html.py <extracted chm dir> assets/Common/Help

Copies the pages and their images/styles (not the CHM internals) and builds index.html (the table of contents, from Contents.hhc).
"""
import html, os, re, shutil, sys

src, dst = sys.argv[1], sys.argv[2]
skip_ext = {'.hhc', '.hhk'}
os.makedirs(dst, exist_ok=True)
for root, dirs, files in os.walk(src):
    dirs[:] = [d for d in dirs if not d.startswith(('$', '#'))]
    for f in files:
        if f.startswith(('$', '#')) or os.path.splitext(f)[1].lower() in skip_ext:
            continue
        rel = os.path.relpath(os.path.join(root, f), src)
        os.makedirs(os.path.dirname(os.path.join(dst, rel)) or dst, exist_ok=True)
        shutil.copy2(os.path.join(root, f), os.path.join(dst, rel))

text = open(os.path.join(src, 'Contents.hhc'), encoding='latin-1').read()
out, depth = [], 0
for token in re.finditer(r'<UL>|</UL>|<param name="Name" value="([^"]*)">\s*(?:<param name="Local" value="([^"]*)">)?', text, re.I):
    t = token.group(0).upper()
    if t == '<UL>':
        out.append('<ul>'); depth += 1
    elif t == '</UL>':
        out.append('</ul>'); depth -= 1
    else:
        name, local = html.escape(token.group(1)), token.group(2)
        out.append('<li><a href="%s">%s</a></li>' % (html.escape(local), name) if local else '<li>%s</li>' % name)
open(os.path.join(dst, 'index.html'), 'w', encoding='utf-8').write(
    '<!DOCTYPE html>\n<html><head><meta charset="utf-8"><title>Ultimate Doom Builder - Reference Manual</title>'
    '<link rel="stylesheet" href="default.css"></head><body><h1>Reference Manual</h1>\n' + '\n'.join(out) + '\n</body></html>\n')
