#!/usr/bin/env python3
"""Phase 37 text audit for Retro Hoops.

1. GLYPHS: every character in a runtime string or char literal must exist in LiberationSans (the TextMesh Pro
   font the UI uses); a missing glyph draws as an empty box on the iPhone. The font's code points are listed in
   liberation_sans_codepoints.txt (read from Assets/TextMesh Pro/Fonts/LiberationSans.ttf with fontTools).
2. SPANISH: literal UI text (Loc.T("..."), UiKit.Label/Button(.., "..."), Mode(.., "TITLE", "blurb")) that has
   no Spanish entry in Loc.cs. Lines built from pieces are not checked.

Usage: python3 tools/TextAudit/text_audit.py [--strict]   (--strict: exit 1 if anything is missing)
"""
import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.join(HERE, "..", "..", "RetroHoops", "Assets", "Scripts")

STRING = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
CHAR = re.compile(r"'((?:[^'\\\n]|\\.))'")
UI_CALL = re.compile(r'(?:Loc\.T\(|UiKit\.(?:Label|Button)\([^,"()]*,\s*|\bMode\(\w+,\s*|UiControls\.(?:ChoiceRow|ToggleRow|SliderRow|Stat)\(\w+,\s*|\bStepper\(\w+,\s*|\bOpenOverlay\()"((?:[^"\\]|\\.)*)"(?=\s*[,)])(?:\s*,\s*"((?:[^"\\]|\\.)*)"(?=\s*[,)]))?')
NEEDS_WORDS = re.compile(r"[A-Za-z]{3}")
SKIP_TEXT = re.compile(r"^[A-Z0-9 .:/+\-·•]*$")  # numbers / codes only


def runtime_files():
    for f in sorted(glob.glob(os.path.join(SCRIPTS, "**", "*.cs"), recursive=True)):
        if os.sep + "Editor" + os.sep in f:
            continue
        yield f


def code_lines(path):
    for i, line in enumerate(open(path, encoding="utf-8"), 1):
        s = line.strip()
        if s.startswith("//"):
            continue
        yield i, line


def glyph_problems(ok):
    out = []
    for f in runtime_files():
        for i, line in code_lines(f):
            for m in list(STRING.finditer(line)) + list(CHAR.finditer(line)):
                for ch in m.group(1):
                    if ord(ch) not in ok:
                        out.append((os.path.relpath(f, SCRIPTS), i, ch))
    return out


def spanish_keys():
    src = open(os.path.join(SCRIPTS, "Logic", "Localization", "Loc.cs"), encoding="utf-8").read()
    return set(re.findall(r'\["((?:[^"\\]|\\.)*)"\]\s*=', src))


def spanish_missing(keys):
    missing = {}
    for f in runtime_files():
        text = open(f, encoding="utf-8").read()
        for m in UI_CALL.finditer(text):
            for t in (m.group(1), m.group(2)):
                if not t or t in keys or not NEEDS_WORDS.search(t) or "<" in t:
                    continue
                if SKIP_TEXT.match(t) and len(t) < 4:
                    continue
                missing.setdefault(t, os.path.relpath(f, SCRIPTS))
    return missing


def main():
    ok = set(int(l) for l in open(os.path.join(HERE, "liberation_sans_codepoints.txt")) if l.strip()) | {9, 10}
    glyphs = glyph_problems(ok)
    keys = spanish_keys()
    missing = spanish_missing(keys)
    print("GLYPHS missing from LiberationSans: %d" % len(glyphs))
    for f, i, ch in glyphs:
        print("  %s:%d  %r U+%04X" % (f, i, ch, ord(ch)))
    print("SPANISH: %d entries, %d literal UI lines without one" % (len(keys), len(missing)))
    for t, f in sorted(missing.items(), key=lambda kv: kv[1]):
        print("  %-40s %s" % (f, t))
    if "--strict" in sys.argv and (glyphs or missing):
        sys.exit(1)


if __name__ == "__main__":
    main()
