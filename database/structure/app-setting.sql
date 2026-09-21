create table if not exists AppSetting (
	Key				text		not null	primary key,
	Value			text		not null
);

insert or ignore into AppSetting (Key, Value) values
	('floor', '70'),
	('aipassmark', '60'),
	('scorecap', '300'),
	('w_regex', '0.25'),
	('w_ai', '0.85'),
	('memorycap', '500'),
	('remotehybrid', '0');
