#!/usr/bin/env python3
"""Remove unused generated console passcodes before publishing Unity settings."""
import argparse
from pathlib import Path
import re

def sanitize(path):
    source=path.read_text(encoding='utf-8-sig')
    cleaned,count=re.subn(r'^  ps4Passcode:.*\n','',source,flags=re.MULTILINE)
    # This project uses development APK signing, never a custom signing key.
    for field in ('AndroidKeystoreName','AndroidKeyaliasName','ps4NPTitleSecret','metroCertificatePassword'):
        match=re.search(r'^  '+field+r':([^\n]*)',cleaned,re.MULTILINE)
        if match and match.group(1).strip():
            raise ValueError(f'Nonempty sensitive setting: {field}. Review locally; do not publish.')
    path.write_text(cleaned,encoding='utf-8')
    return count

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('path',type=Path,nargs='?',default=Path('UnityProject/ProjectSettings/ProjectSettings.asset'))
    print(f'Removed {sanitize(parser.parse_args().path)} unused generated field(s).')
