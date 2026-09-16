#!/usr/bin/env python3
import sys
from query import main

raise SystemExit(main(["find-type", *sys.argv[1:]]))
