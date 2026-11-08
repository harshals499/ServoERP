"""Apply reviewed English/Marathi pairs with ResourceManager's case-insensitive keys."""
from pathlib import Path
import json
import sys
import xml.etree.ElementTree as ET

reviewed = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
for filename in ("Strings.resx", "Strings.mr-IN.resx"):
    path = Path("SOURCE_CODE/Resources/Localization") / filename
    tree = ET.parse(path)
    root = tree.getroot()
    nodes = {}
    for node in list(root.findall("data")):
        key = node.attrib["name"].casefold()
        if key in nodes:
            root.remove(node)
        else:
            nodes[key] = node
    for key, value in reviewed.items():
        node = nodes.get(key.casefold())
        if node is None:
            node = ET.SubElement(root, "data", {"name": key, "{http://www.w3.org/XML/1998/namespace}space": "preserve"})
            ET.SubElement(node, "value").text = key
            nodes[key.casefold()] = node
        if filename == "Strings.mr-IN.resx":
            node.find("value").text = value
    ET.indent(tree, space="  ")
    tree.write(path, encoding="utf-8", xml_declaration=True)
print("Reviewed overrides applied:", len(reviewed))
