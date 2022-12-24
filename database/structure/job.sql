create table Job (
	JobID			integer	not null	primary key,
	AgencyID		integer not null,
	Url				text	not null,
	Code			text	not null,
	Html			text		null,
	Title			text		null,
	State			text	not null,
	Log				text		null,

	unique			(AgencyID, Code),
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
);

create unique index UQ_Job_Url on Job (AgencyID, Code);