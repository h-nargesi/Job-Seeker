create table Job (
	JobID			integer		not null	primary key,
	RegTime			timestamp	not null	default current_timestamp,
	AgencyID		integer 	not null,
	Code			text		not null,
	Title			text			null,
	State			text		not null,
	Score			integer			null,
	Url				text		not null,
	Html			text			null,
	Link			text			null,
	Log				text			null,
	Tries			text			null,

	unique			(AgencyID, Code),
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
);

create unique index UQ_Job_Url on Job (AgencyID, Code);