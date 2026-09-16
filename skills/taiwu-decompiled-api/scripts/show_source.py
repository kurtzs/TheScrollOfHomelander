#!/usr/bin/env python3
import sys
from query import main

raise SystemExit(main(["show-source", *sys.argv[1:]]))
