create table JobScore (
	JobID			integer not null,
	JobOptionID		integer	not null,

	primary key		(JobID, JobOptionID),
	foreign key		(JobID) references Job (JobID) on delete cascade,
	foreign key		(JobOptionID) references JobOption (JobOptionID) on delete cascade

) without rowid;
