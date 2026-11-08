from pathlib import Path
import re,xml.etree.ElementTree as E,json,os,argparse
os.chdir(Path(__file__).resolve().parent.parent)
parser=argparse.ArgumentParser();parser.add_argument("--fail-on-missing",action="store_true");args=parser.parse_args()
base=Path('SOURCE_CODE'); resources={x.attrib['name'].casefold() for x in E.parse(base/'Resources/Localization/Strings.mr-IN.resx').findall('data')}
patterns=[r'LanguageManager\.SetCaption\([^,\n]+,\s*"((?:[^"\\]|\\.)+)"', r'LanguageManager\.(?:Get|GetMessage|Format)\(\s*"((?:[^"\\]|\\.)+)"', r'\b(?:Text|HeaderText|ToolTipText|AccessibleName|AccessibleDescription)\s*=\s*(?:\$)?"((?:[^"\\]|\\.)+)"',r'\b(?:PlaceLabeledControl|AddModernSettingsCard)\([^,\n]+,\s*"((?:[^"\\]|\\.)+)"',r'\bMessageBox\.Show\(\s*(?:this\s*,\s*)?(?:\$)?"((?:[^"\\]|\\.)+)"',r'\.WithMessage\(\s*"((?:[^"\\]|\\.)+)"']
patterns.extend([
 r'\b(?:Title|Subtitle|Description|Calculation)\s*=\s*"((?:[^"\\]|\\.)+)"',
 r'\bDestination\(\s*"((?:[^"\\]|\\.)+)"',
 r'\bDestination\(\s*"(?:[^"\\]|\\.)+"\s*,\s*"((?:[^"\\]|\\.)+)"',
 r'\bAddSettingsDirectoryGroup\(\s*"((?:[^"\\]|\\.)+)"',
 r'\bLanguageManager\.AddChoice\([^,\n]+,\s*"((?:[^"\\]|\\.)+)"',
 r'\bCreate(?:Workspace|Sales)(?:Dashboard|Editor)\([^,\n]+,\s*"((?:[^"\\]|\\.)+)"',
 r'\bCreate(?:Workspace|Sales)(?:Dashboard|Editor)\([^,\n]+,\s*"(?:[^"\\]|\\.)+"\s*,\s*"((?:[^"\\]|\\.)+)"'
])
rows=[]
excluded=[]
for folder in ['UI','Infrastructure','Validators','OfflineDesktop']:
 for p in (base/folder).rglob('*.cs'):
  s=p.read_text(encoding='utf-8-sig')
  for pattern in patterns:
   for m in re.finditer(pattern,s):
    raw=m.group(1)
    try: value=json.loads('"'+raw+'"')
    except json.JSONDecodeError: value=raw
    # This is C# text embedded in the card export template, not a UI literal.
    if p.name == 'GlobalCardStateService.cs' and value.strip() == '+ DisplayTitle(info) +':
     excluded.append({'file':p.as_posix(),'text':value,'reason':'Embedded export-template code falsely matched as a C# property assignment'})
     continue
    if re.search('[A-Za-z]{2}',value) and value.casefold() not in resources:
     rows.append({'file':p.as_posix(),'line':s.count('\n',0,m.start())+1,'text':value})
rows=list({(r['file'],r['line'],r['text']):r for r in rows}.values())
Path('Docs/Localization/untranslated-candidates.json').write_text(json.dumps({'note':'Conservative static candidate audit, not proof of complete runtime coverage. Includes data, protected content and interpolated strings requiring manual classification. Excludes many helper-built strings and services.','excludedCandidates':excluded,'marathiResourceCount':len(resources),'missingCandidateCount':len(rows),'candidates':rows},ensure_ascii=False,indent=2),encoding='utf-8')
print('Missing candidates:',len(rows),'Unique:',len({r['text'] for r in rows}))
from collections import Counter
print(Counter(r['file'] for r in rows).most_common(8))

if args.fail_on_missing and rows: raise SystemExit(1)
