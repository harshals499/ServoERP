"""Find resource-backed captions still rendered in English in native QA captures.

This is a conservative acceptance check, not proof of complete UI coverage. The QA
fixture uses synthetic names; arbitrary customer data must not become captions.
"""
from pathlib import Path
import argparse
import json
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("evidence_directory", type=Path)
parser.add_argument("--fail-on-untranslated", action="store_true")
args = parser.parse_args()
resource_path = Path(__file__).resolve().parents[2] / "SOURCE_CODE/Resources/Localization/Strings.mr-IN.resx"
resources = {node.attrib["name"].casefold(): node.findtext("value", "") for node in ET.parse(resource_path).findall("data")}
expected_file = args.evidence_directory / "expected-surfaces.txt"
expected = expected_file.read_text(encoding="utf-8-sig").splitlines() if expected_file.exists() else []
report = args.evidence_directory / "regression.txt"
# Windows PowerShell redirects native output as UTF-16; PowerShell 7 uses UTF-8.
report_bytes = report.read_bytes() if report.exists() else b""
report_text = report_bytes.decode("utf-16" if report_bytes.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig")
report_lines = report_text.strip().splitlines()
missing = [name for name in expected if not (args.evidence_directory / (name + "-caption-review.txt")).exists()]
if not expected or missing or not report_lines or not report_lines[-1].startswith("PASS TOTAL "):
    result = {"captureComplete": False, "untranslatedCount": None, "missingSurfaces": missing, "reason": "A completed functional run and every expected Marathi capture are required; zero findings in an interrupted run is not a pass."}
    (args.evidence_directory / "rendered-caption-audit.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(result["reason"])
    raise SystemExit(2)
findings = []
for path in sorted(args.evidence_directory.glob("*-caption-review.txt")):
    for caption in path.read_text(encoding="utf-8-sig").splitlines():
        translated = resources.get(caption.casefold())
        if translated and any("\u0900" <= char <= "\u097f" for char in translated) and caption != translated:
            findings.append({"surface": path.stem.replace("-caption-review", ""), "english": caption, "marathi": translated})
result = {"captureComplete": True, "expectedSurfaces": len(expected), "scope": "Resource-backed English text found in native synthetic-fixture UI captures; not an exhaustive runtime audit.", "untranslatedCount": len(findings), "findings": findings}
(args.evidence_directory / "rendered-caption-audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print("Untranslated rendered resource captions:", len(findings))
if args.fail_on_untranslated and findings:
    raise SystemExit(1)
