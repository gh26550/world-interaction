"""Copy only authored project files to the requested destination; never delete user files."""
from pathlib import Path
import shutil
import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('source',type=Path)
parser.add_argument('destination',type=Path)
args=parser.parse_args()
source=args.source.resolve()
destination=args.destination.resolve()
if source==destination or source in destination.parents:
    raise SystemExit('Destination must not be the source or its descendant')
ignore={'.git','Library','Temp','Logs','obj','UserSettings','.tools','.venv','__pycache__','results','Builds','.utmp','.gradle','.vs'}
def copy_tree(src,dst):
    dst.mkdir(parents=True,exist_ok=True)
    for item in src.iterdir():
        if item.name in ignore or item.suffix in {'.csproj','.sln'}:continue
        target=dst/item.name
        if item.is_dir():copy_tree(item,target)
        else:shutil.copy2(item,target)
copy_tree(source,destination)
print('Synced authored files to',destination)
