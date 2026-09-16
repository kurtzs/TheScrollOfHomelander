#!/usr/bin/env python3
import sys
from query import main

raise SystemExit(main(["find-member", *sys.argv[1:]]))
