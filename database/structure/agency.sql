create table if not exists Agency (
	AgencyID		integer not null	primary key,
	Title			text	not null	unique,
	Active			integer	not null	default 3,
	Domain			text	not null,
	Link			text	not null,
	UserName		text		null,
	Password		text		null,
	Settings		text		null,

	unique			(Title)
);

insert into Agency (Title, Domain, Link, UserName, Password, Settings)
values
	('Indeed',		'(.+\.)?indeed\.com$',			'https://indeed.com',		'username', 'password',
					'{ "running": 0, "domains": ["https://au.indeed.com/", "https://nl.indeed.com/", "https://de.indeed.com/"] }'),

	('IamExpat',	'(.+\.)?iamexpat\.(nl|de|ch|com)$',	'http://iamexpat.nl',	'username', 'passwords',
					'{ "running": 0, "urls": ["nl/career/jobs-netherlands", "de/career/jobs-germany", "ch/career/jobs-switzerland"]}'),

	('LinkedIn',	'(.+\.)?linkedin\.com$',		'http://linkedin.com',		'username', 'password',
					'{ "running": 0, "locations": ["Netherlands", "Germany", "Australia", "Sweden"]}')
on conflict (Title) do update set 
	Domain = excluded.Domain, Link = excluded.Link,
	UserName = excluded.UserName, Password = excluded.Password, 
	Settings = excluded.Settings;
