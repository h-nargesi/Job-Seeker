#!/bin/bash

# server root directory
SOURCEDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
SOURCEFILE="/manage.py"

# to run web server
python3 manage.py migrate
python3 "$SOURCEDIR$SOURCEFILE" runserver 0.0.0.0:80
