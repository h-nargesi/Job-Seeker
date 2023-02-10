create table if not exists Setting (
	Name			text		not null	primary key,
	Value			text			null

) without rowid;

insert into Setting
values
	('Search Titles',	'["developer", "data scientist"]'),
	('Search Periods',	'3d')
on conflict (Name) do nothing;