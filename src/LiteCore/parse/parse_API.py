#!/usr/bin/python

"""
parse_API.py

This file is meant to parse C++ headers into a syntax that is parseable
into other things like C# bindings and export symbol lists.  It is customizable
via an external config file (--config) which has the following properties:

'skip_files': []            Files to exclude from processing
'excluded': []              Functions to exclude from processing
'force_no_bridge': []       Functions to not create a bridge for, even if it contains
                            parameters that would otherwise require one
'param_bridge_types': []    A list of parameters that will trigger a bridge
'return_bridge_types': []   A list of return types that will trigger a bridge
'literals': []              A list of functions to take a predefined definition for
'reserved':[]               A list of keywords that cannot be used as parameter names
'default_param_name': {}    A map of types to default parameter names (useful for anonymous C parameters)
'type_map': {}              A map of C types to C# types
"""

import CppHeaderParser
import glob
import re
import argparse
import importlib

seen_functions = set()

if __name__ == "__main__":
    parser = argparse.ArgumentParser("Parses C++ headers into an abstract representation for processing")

    parser.add_argument("-o", "--output-dir", help="The directory to store the output files in (default current directory)")
    parser.add_argument("-c", "--config", help="A configuration file with data to help customize the parsing")
    parser.add_argument("-l", "--symbol-list", help="A list of symbols for inclusion")
    parser.add_argument("-v", "--verbose", help="Enable verbose output", action='store_true')
    args = parser.parse_args()
    output_dir = args.output_dir if args.output_dir is not None else ""
    config_module = {}
    if args.config is not None:
        config_module = importlib.import_module(args.config)


    symbol_list = []
    if args.symbol_list is not None:
        symbol_file = open(args.symbol_list, "r")
        symbol_list = symbol_file.read().splitlines()
        end_index = symbol_list.index("; C4Tests")
        symbol_list = symbol_list[:end_index]
        symbol_file.close()

    skip_files = []
    if hasattr(config_module, "skip_files"):
        skip_files = getattr(config_module, "skip_files")

    for file in glob.iglob("./*.h"):
        skip = False
        for skipFile in skip_files:
            if file.endswith(skipFile):
                skip = True

        if skip:
            print("Skipping {}".format(file))
            continue
        
        if args.verbose is not None:
            print("Processing {}".format(file))

        #HACK: Typedefs choke CppHeaderParser if the struct and typename have the same name (i.e. typedef struct foo foo)
        fin = open(file, "r")
        file_contents = fin.read().replace("C4NONNULL", "").replace("FLNONNULL","")
        file_contents = re.sub("typedef.*", "", file_contents)
        fin.close()

        lines = []
        cppHeader = CppHeaderParser.CppHeader(file_contents, "string")

        for variable in ["excluded", "force_no_bridge", "param_bridge_types", "return_bridge_types", "literals", "reserved"]:
            locals()[variable] = []
            if hasattr(config_module, variable):
                 locals()[variable] = getattr(config_module, variable)

        for variable in ["default_param_name", "type_map"]:
            locals()[variable] = {}
            if hasattr(config_module, variable):
                 locals()[variable] = getattr(config_module, variable)

        for function in cppHeader.functions:
            fn_name = function["name"]
            if fn_name in seen_functions:
                continue

            if fn_name in excluded or (symbol_list and not fn_name in symbol_list):
                continue

            if fn_name in literals:
                lines.append(literals[fn_name])
                continue

            seen_functions.add(fn_name)
            return_type = function["rtnType"].replace(" ","").replace("struct_","").replace("struct","").replace("const","").replace("staticinline","")
            if return_type in type_map:
                return_type = type_map[return_type]

            bridge_def = [".nobridge", ".{}".format(return_type), fn_name]
            if return_type in return_bridge_types:
                bridge_def[0] = ".bridge"

            for param in function["parameters"]:
                type = param["type"].replace("const","").replace(" ","")
                if param["array"]:
                    type += "[]"

                if type == "void":
                    continue

                if type in param_bridge_types:
                    bridge_def[0] = ".bridge"

                name = param["name"]
                if len(name) == 0:
                    name = default_param_name[type] if type in default_param_name else "x"

                if type in type_map:
                    type = type_map[type]

                if name in reserved:
                    name = "@{}".format(name)

                bridge_def.append("{}:{}".format(type, name))

            if fn_name in force_no_bridge:
                bridge_def[0] = ".nobridge"

            line = " ".join(bridge_def)
            lines.append(line)

        if len(lines) == 0:
            continue

        fout = open(output_dir + file[2].upper() + file[3:-2] + "_native.cs.template", "w")
        fout.write('\n'.join(lines))
        fout.close()
