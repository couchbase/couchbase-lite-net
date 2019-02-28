#!/usr/bin/python

import glob
import re
from datetime import date

type_map = {"uint32_t":"uint","int32_t":"int","uint8_t":"byte","uint64_t":"ulong","uint16_t":"ushort","int8_t":"sbyte",
            "unsigned":"uint","int64_t":"long"}
def parse_enum(filename):
    fin = open(filename, "r")
    name_to_type = {}
    name_to_entries = {}
    current_name = ""
    entries = []
    flags = []
    in_enum = 0
    in_comment = 0
    for line in fin:
        if line.lstrip().startswith("//"):
            continue
        if in_enum > 0:
            match = re.match(r'[ |\t]*} ?([A-Za-z0-9]*);', line)
            if match:
                if in_enum == 2:
                    current_name = match.group(1)
                    name_to_type[current_name] = None
                
                name_to_entries[current_name] = entries
                entries = [] 
                in_enum = 0
            else:
                if in_comment > 0:
                    if "*/" in line:
                        in_comment -= 1
                    
                    continue
                    
                if "/*" in line and not "*/" in line:
                    in_comment += 1
                        
                stripped = re.search(r'\s*([A-Za-z0-9=\- _\(\)\|]+,?)', line)
                if not stripped:
                    continue
                    
                entry = stripped.group(1)
                stripped = re.search("(?:k)?(?:C4|Rev)?(?:FL|Log|DB_|Encryption|Error|NetErr)?(.*)", entry)
                final_entry = stripped.group(1).rstrip()
                for prefix in ["kFL"]:
                    final_entry = final_entry.replace(prefix, "")

                entries.append(final_entry)
        else:
            definition = re.search("typedef C4_(ENUM|OPTIONS)\((.*?), (.*?)\) {", line)
            if definition:
                in_enum = 1
                current_name = definition.group(3)
                name_to_type[current_name] = type_map[definition.group(2)]
                if definition.group(1) == "OPTIONS":
                    flags.append(current_name)
            elif re.match(r'\s*typedef enum {', line):
                in_enum = 2

        if len(name_to_type) == 0:
            continue
            
    out_text = ""
    for name in name_to_type:
        if name in flags:
            out_text += "    [Flags]\n"
        if name_to_type[name]:
            out_text += "    internal enum {} : {}\n    {{\n".format(name, name_to_type[name])
        else:
            out_text += "    internal enum {}\n    {{\n".format(name)

        for entry in name_to_entries[name]:
            out_text += "        {}\n".format(entry)

        out_text += "    }\n\n"
                
    return out_text[:-2]
