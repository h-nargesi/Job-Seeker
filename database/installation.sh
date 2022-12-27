#!/bin/bash
rm -f data.sqlite3

##### Structure
echo "Structure"
sqlite3 data.sqlite3 < structure/agency.sql;
sqlite3 data.sqlite3 < structure/trend.sql;
sqlite3 data.sqlite3 < structure/job.sql;
sqlite3 data.sqlite3 < structure/job-option.sql;