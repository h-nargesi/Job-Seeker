create table if not exists Job (
	JobID			integer		not null	primary key,
	RegTime			timestamp	not null	default current_timestamp,
	ModifiedOn		timestamp	not null	default current_timestamp,
	AgencyID		integer 	not null,
	Code			text		not null,
	Title			text			null,
	State			text		not null,
	Score			integer			null,
	Url				text		not null,
	Html			text			null,
	Content			text			null,
	Link			text			null,
	Log				text			null,
	Options			text			null,
	Tries			text			null,

	unique			(AgencyID, Code),
	foreign key		(AgencyID) references Agency (AgencyID) on delete no action
);