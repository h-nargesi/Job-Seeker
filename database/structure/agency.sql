create table Agency (
	AgencyID		integer not null	primary key,
	Title			text	not null	unique,
	Active			integer	not null	default 3,
	Domain			text	not null,
	Link			text	not null,
	UserName		text		null,
	Password		text		null,
	Settings		text		null
);

create unique index UQ_Agency_Title on Agency (Title);

insert into Agency (Title, Domain, Link, UserName, Password, Settings)
values
	('Indeed',		'(.+\.)?indeed\.com$',			'https://indedd.com',
					'hamed.nargesi.jar@gmail.com', 's@lm0nElla-009',
					'{ "running": 0, "locations": ["Australia", "Netherlands", "Germany", "Sweden"] }'),

	('IamExpat',	'(.+\.)?iamexpat\.(nl|de|ch|com)$',	'http://iamexpat.nl',
					'hamed.nargesi.jar@gmail.com', 's@lm0nElla',
					'{ "running": 0, "searchs": ["nl/career/jobs-netherlands", "de/career/jobs-germany", "ch/career/jobs-switzerland"]}'),

	('LinkedIn',	'(.+\.)?linkedin\.com$',		'http://linkedin.com',
					'hamed.nargesi.jar@gmail.com', 'CrguFW7SmtbHDDi',
					'{ "running": 0, "locations": ["Netherlands", "Germany", "Australia", "Sweden"]}');

-- update Agency set Active = 0 where Title in ('Indeed', 'IamExpat');