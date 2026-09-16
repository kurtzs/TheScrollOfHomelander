#!/usr/bin/env python3
import sys
from query import main

raise SystemExit(main(["find-patch-targets", *sys.argv[1:]]))
