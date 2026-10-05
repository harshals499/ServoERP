"""Source candidates and runtime evidence are separate; neither implies business success."""
from pathlib import Path
import re,json,csv
out=Path('TEST_RESULTS/action-audit-1.1.486');out.mkdir(parents=True,exist_ok=True)
definitions={}
class_pattern=re.compile(r'(?:(public|internal|private)\s+)?(?:sealed\s+|partial\s+|abstract\s+)*class\s+(\w+)(?:\s*:\s*([^\r\n{]+))?\s*\{')
lexemes=re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\x27(?:\\.|[^\x27\\])*\x27')
for p in [*Path('SOURCE_CODE/UI').rglob('*.cs'),Path('SOURCE_CODE/Infrastructure/ServoConfirmDialog.cs')]:
 if 'Controls' in p.parts:continue
 source=p.read_text(encoding='utf-8-sig',errors='replace')
 masked=lexemes.sub(lambda m:re.sub(r'[^\n]',' ',m.group()),source)
 for match in class_pattern.finditer(masked):
  depth,end=1,match.end()
  while depth and end<len(masked):
   depth+=(masked[end]=='{')-(masked[end]=='}');end+=1
  name=match.group(2)
  entry=definitions.setdefault(name,{'files':[],'bodies':[],'base':''})
  entry['files'].append(str(p).replace('\\','/'));entry['bodies'].append(source[match.end():end-1])
  if match.group(3):entry['base']=match.group(3).strip()
actions={'Add':r'\b(Add|New|Create)\b','Save':r'\b(Save|Apply|Update|Record)\b','Delete':r'\b(Delete|Remove|Archive|Void)\b','Filter':r'\b(Filter|Filters|Search)\b','ClearFilters':r'Clear\s+Filters|Reset\s+filters','SelectAll':r'Select\s+(all|shown)','ClearSelection':r'Clear\s+(all\s+)?selection','Close':r'\b(Close|Cancel|Back)\b','Refresh':r'\bRefresh\b'}
views=[]
for name,entry in sorted(definitions.items()):
 base=entry['base']
 if not re.search(r'Form|Page|Control|Panel',base):continue
 if name in ('BaseForm','BaseUserControl','DeferredPageControl','ResizableCard','LazyTabPage','SpotlightOverlay'):continue
 if 'HoverChart' in base or ('Panel' in base and not name.endswith('Dashboard')):continue
 source='\n'.join(entry['bodies']);labels=re.findall(r'"([^"\r\n]{1,120})"',source);candidates=[]
 for action,pattern in actions.items():
  if any(re.search(pattern,label,re.I) for label in labels):candidates.append(action)
  elif action=='ClearFilters' and 'CreateClearFilters(' in source:candidates.append(action)
  elif action in ('SelectAll','ClearSelection') and 'CreateSelectionActions(' in source:candidates.append(action)
 views.append({'Screen':name,'Files':';'.join(sorted(set(entry['files']))),'Base':base,'SourceActionCandidates':','.join(candidates),'SourceTabs':','.join(sorted(set(re.findall(r'new\s+TabPage\s*\(\s*"([^"\r\n]+)"',source)))),'RuntimeControlsCaptured':(out/(name+'-controls.csv')).exists(),'Evidence':'Class-scoped source candidates including partial/designer files. Labels and handler wiring do not prove business success; absence is not automatically a missing action.'})
with (out/'source-screen-inventory.csv').open('w',newline='',encoding='utf-8') as f:
 writer=csv.DictWriter(f,fieldnames=list(views[0]));writer.writeheader();writer.writerows(views)
(out/'source-screen-inventory.json').write_text(json.dumps(views,indent=2),encoding='utf-8')
print('Inventoried',len(views),'screen/dialog classes;',sum(v['RuntimeControlsCaptured'] for v in views),'have runtime control inventories')
