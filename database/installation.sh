#!/bin/bash
# rm -f data.sqlite3

##### Structure
sqlite3 data.sqlite3 < structure/agency.sql;
sqlite3 data.sqlite3 < structure/job-option.sql;
sqlite3 data.sqlite3 < structure/job.sql;
sqlite3 data.sqlite3 "ALTER TABLE Job ADD COLUMN Attempts integer not null default 0" || echo "Job.Attempts already exists"
sqlite3 data.sqlite3 "UPDATE Job SET Attempts = LENGTH(Tries) - LENGTH(REPLACE(Tries, char(10), '')) + 1 WHERE Tries IS NOT NULL AND Tries != '' AND Attempts = 0"
sqlite3 data.sqlite3 < structure/trend.sql;
sqlite3 data.sqlite3 < structure/passwords.sql;
echo "Database Updated"