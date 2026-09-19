drop table if exists Memory;

create table Memory (
	MemoryID		integer 	not null	primary key,
	Scope			text		not null,
	AgencyDomain	text		not null	default '*',
	FieldKey		text		not null,
	FieldLabel		text			null,
	Kind			text		not null,
	Confirmed		bit			not null	default 0,
	Value			text		not null,
	Note			text			null,
	UseCount		integer		not null	default 0,
	CreatedAt		timestamp	not null	default current_timestamp,
	UpdatedAt		timestamp	not null	default current_timestamp
);
