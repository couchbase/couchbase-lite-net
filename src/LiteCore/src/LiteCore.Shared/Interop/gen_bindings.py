import os
import glob
import re
from datetime import date

TEMPLATE = """//
// %(filename)s
//
// Copyright (c) %(year)d Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Linq;
using System.Runtime.InteropServices;

using LiteCore.Util;

namespace LiteCore.Interop
{

    internal unsafe static partial class Native
    {
%(native)s
    }

    internal unsafe static partial class NativeRaw
    {
%(native_raw)s
    }
}
"""

METHOD_DECORATION = "        [DllImport(Constants.DllName, CallingConvention = CallingConvention.Cdecl)]\n"
METHOD_DECORATION_IOS = "        [DllImport(Constants.DllNameIos, CallingConvention = CallingConvention.Cdecl)]\n"
bridge_literals = {}
raw_literals = {}
bridge_params = []

def transform_raw(arg_type, arg_name):
    template = get_template(arg_type, "raw")
    if template:
        return template.format(arg_name)

    if arg_type.endswith("*[]"):
        return "{}** {}".format(arg_type[:-3], arg_name)

    return " ".join([arg_type, arg_name])

def transform_raw_return(arg_type):
    template = get_template(arg_type, "raw_return")
    if template:
        return template.format(arg_type)

    return arg_type

def transform_bridge(arg_type):
    template = get_template(arg_type, "bridge")
    if template:
        return template

    return arg_type

def get_template(name, template_type):
    try:
        fin = open("templates/{}_{}.template".format(name.replace("*", "_ptr"), template_type), "r")
        retVal = fin.read().strip("\n")
        #print "Got {} template for '{}':\n{}".format(template_type, name, retVal)
    except:
        return None

    return retVal

def generate_bridge_sig(pieces, bridge_args):
    retVal = ""
    if(len(pieces) > 2):
        for args in pieces[2:]:
            arg_info = args.split(":")
            bridge = transform_bridge(arg_info[0])
            if bridge != arg_info[0]:
                bridge_args.append(args)

            retVal += "{} {}, ".format(bridge, arg_info[1])

        retVal = retVal[:-2]

    retVal += ")\n        {\n"
    return retVal

def generate_using_parameters_begin(bridge_args):
    retVal = ""
    if len(bridge_args) > 0:
        for bridge_arg in bridge_args[:-1]:
            split_args = bridge_arg.split(':')
            template = get_template(split_args[0], "using")
            if template:
                template = template.format(split_args[1])
                retVal += "            {}\n".format(template)

        split_args = bridge_args[-1].split(':')
        template = get_template(split_args[0], "using")
        if template:
            template = template.format(split_args[1])
            retVal += "            {} {{\n".format(template)

    return retVal

def generate_using_parameters_end(bridge_args):
    retVal = ""
    for arg in bridge_args:
        if not arg.startswith("UIntPtr"):
            retVal += "            }\n"
            break

    return retVal

def bridge_parameter(param, return_space):
    splitPiece = param.split(":")
    template = get_template(splitPiece[0], "bridge_param")
    if not template:
        return "{}, ".format(splitPiece[1])

    if splitPiece[0] != "UIntPtr":
        return_space[0] = "                "

    return template.format(splitPiece[1]) + ", "

def generate_return_value(pieces):
    raw_call_params = "("
    return_space = ["            "]
    retval_type = pieces[0][1:]
    if len(pieces) > 2:
        for piece in pieces[2:-1]:
            raw_call_params += bridge_parameter(piece, return_space)

        raw_call_params += bridge_parameter(pieces[-1], return_space)[:-2]

    raw_call_params += ")"
    if retval_type == "void":
        return "{}NativeRaw.{}{};\n".format(return_space[0], pieces[1], raw_call_params)

    template = get_template(retval_type, "return")
    if template:
        return template.format(return_space[0], pieces[1], raw_call_params) + "\n"

    return "{}return NativeRaw.{}{};\n".format(return_space[0], pieces[1], raw_call_params)

def insert_bridge(collection, pieces):
    if pieces[1] in bridge_literals:
        collection.append(bridge_literals[pieces[1]])
        return

    line = "        public static {} {}(".format(transform_bridge(pieces[0][1:]), pieces[1])
    bridge_args = []
    line += generate_bridge_sig(pieces, bridge_args)
    line += generate_using_parameters_begin(bridge_args)
    line += generate_return_value(pieces)
    line += generate_using_parameters_end(bridge_args)
    line += "        }\n\n"
    collection.append(line)

def insert_raw(collection, pieces, ios):
    if ios:
        collection.append(METHOD_DECORATION_IOS)
    else:
        collection.append(METHOD_DECORATION)

    if(pieces[1] in raw_literals):
        collection.append(raw_literals[pieces[1]])
        return

    if(pieces[0] == ".bool"):
        collection.append("        [return: MarshalAs(UnmanagedType.U1)]\n")

    line = "        public static extern {} {}(".format(transform_raw_return(pieces[0][1:]), pieces[1])
    if(len(pieces) > 2):
        for args in pieces[2:]:
            split_args = args.split(':')
            line += "{}, ".format(transform_raw(split_args[0], split_args[1]))

        line = line[:-2]

    line += ');\n\n'
    collection.append(line)

def read_literals(filename, collection):
    try:
        fin = open(filename, "r")
    except IOError:
        return

    key = ""
    value = ""
    for line in fin:
        if re.match(r'\S', line):
            if len(value) > 0:
                collection[key] = value.replace("\t","    ")

            key = line.rstrip("\n")
            value = ""
        else:
            value += line

    fin.close()
    collection[key] = value

read_literals("bridge_literals.txt", bridge_literals)
read_literals("raw_literals.txt", raw_literals)
for filename in glob.iglob("*.template"):
    native = []
    native_ios = []
    native_raw = []
    native_raw_ios = []
    out_filename = os.path.splitext(filename)[0]
    outs = open(out_filename, "w")

    ins = open(filename, "r")
    for line in ins:
        pieces = line.split()
        if(pieces[0] == ".bridge"):
            bridge_params.append(pieces[1:])
            insert_bridge(native, pieces[1:])
            insert_bridge(native_ios, pieces[1:])
            insert_raw(native_raw, pieces[1:], False)
            insert_raw(native_raw_ios, pieces[1:], True)
        else:
            insert_raw(native, pieces[1:], False)
            insert_raw(native_ios, pieces[1:], True)

    output = TEMPLATE % {"filename":out_filename, "year":date.today().year, "native": ''.join(native), "native_raw": ''.join(native_raw)}
    outs.write(output)
    outs.close()

    out_filename = "{}_ios.cs".format(os.path.splitext(out_filename)[0])
    outs = open(out_filename, "w")
    output = TEMPLATE % {"filename":out_filename, "year":date.today().year, "native": ''.join(native_ios), "native_raw": ''.join(native_raw_ios)}
    outs.write(output)
    outs.close()


