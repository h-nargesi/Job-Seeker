#!/bin/bash
# rm -f data.sqlite3

##### Structure
sqlite3 data.sqlite3 < structure/agency.sql;
sqlite3 data.sqlite3 < structure/job-option.sql;
sqlite3 data.sqlite3 < structure/job.sql;
sqlite3 data.sqlite3 < structure/ai-run.sql;
sqlite3 data.sqlite3 < structure/app-setting.sql;
sqlite3 data.sqlite3 < structure/trend.sql;
sqlite3 data.sqlite3 < structure/memory.sql;
sqlite3 data.sqlite3 < structure/passwords.sql;
echo "Database Updated"