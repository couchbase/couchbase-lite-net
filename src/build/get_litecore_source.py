#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from configparser import ConfigParser
from pathlib import Path
from git import Repo, RemoteProgress
import os

class CloneProgress(RemoteProgress):
    def update(self, op_code, cur_count, max_count = 100.0, message = ""):
        op_string = ""
        if op_code & RemoteProgress.CHECKING_OUT:
            op_string = "CHECKING_OUT"
        elif op_code & RemoteProgress.COMPRESSING:
            op_string = "COMPRESSING"
        elif op_code & RemoteProgress.RECEIVING:
            op_string = "RECEIVING"
        elif op_code & RemoteProgress.RESOLVING:
            op_string = "RESOLVING"
        else:
            op_string = str(op_code)

        print(f"\033[K{op_string}: {cur_count} of {max_count} completed...", end="\r")

class SubmoduleProgress(RemoteProgress):
    def update(self, op_code, cur_count, max_count = 100.0, message = ""):
        # TODO: Why do these still print on multiple lines?
        # Something about the message itself
        print("\033[K" + message, end="\r")

def checkout_litecore():
    script_path = Path(__file__).parent
    ini_path = script_path / ".." / ".." / "core_version.ini"
    parser = ConfigParser()
    parser.read(ini_path)
    git_revision = parser["hashes"]["ce"]
    os.chdir(script_path / ".." / ".." / "vendor")
    if not Path("couchbase-lite-core").is_dir():
        Repo.clone_from("https://github.com/couchbase/couchbase-lite-core", "couchbase-lite-core", progress=CloneProgress())

    repo = Repo("couchbase-lite-core")
    repo.head.set_commit(git_revision)
    repo.submodule_update(recursive=True, init=True, progress=SubmoduleProgress())
    print(f"Checked out couchbase-lite-core @ {git_revision}")

if __name__ == "__main__":
    checkout_litecore()