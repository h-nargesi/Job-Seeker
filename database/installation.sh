#!/bin/bash
# rm -f data.sqlite3

##### Structure
sqlite3 data.sqlite3 < structure/agency.sql;
sqlite3 data.sqlite3 < structure/job-option.sql;
sqlite3 data.sqlite3 < structure/job.sql;
sqlite3 data.sqlite3 < structure/trend.sql;
echo "Database Updated"