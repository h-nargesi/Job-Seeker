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
	('Indeed',		'(.+\.)?indeed\.com$',				'https://indeed.com',		'username', 'password',
					'{ "running": 0, "methods": [
						{ "Title": "NL", "Url": "https://nl.indeed.com/" },
						{ "Title": "AU", "Url": "https://au.indeed.com/" },
						{ "Title": "DE", "Url": "https://de.indeed.com/" },
						{ "Title": "SE", "Url": "https://se.indeed.com/" },
						{ "Title": "OM", "Url": "https://om.indeed.com/" },
						{ "Title": "QA", "Url": "https://qa.indeed.com/" },
						{ "Title": "UK", "Url": "https://uk.indeed.com/" }]
					}'),

	('IamExpat',	'(.+\.)?iamexpat\.(nl|de|ch|com)$',	'http://iamexpat.nl',		'username', 'passwords',
					'{ "running": 0, "methods": [
						{ "Title": "NL", "Url": "nl/career/jobs-netherlands" },
						{ "Title": "DE", "Url": "de/career/jobs-germany" },
						{ "Title": "CH", "Url": "ch/career/jobs-switzerland" }]
					}'),

	('LinkedIn',	'(.+\.)?linkedin\.com$',			'https://linkedin.com',		'username', 'password',
					'{ "running": 0, "methods": [
						{ "Title": "NL", "Url": "Netherlands" },
						{ "Title": "AU", "Url": "Australia" },
						{ "Title": "DE", "Url": "Germany" },
						{ "Title": "SE", "Url": "Sweden" },
						{ "Title": "AM", "Url": "Armenia" },
						{ "Title": "OM", "Url": "Oman" },
						{ "Title": "QA", "Url": "Qatar" },
						{ "Title": "UK", "Url": "United Kingdom" }]
					}'),

	('Bayt',		'(.+\.)?bayt\.com$',				'https://www.bayt.com/',	'username', 'password',
					'{ "running": 0, "methods": [
						{ "Title": "OM", "Url": "oman" },
						{ "Title": "QA", "Url": "qatar" }]
					}'),

	('Stepstone',	'(.+\.)?stepstone\.de$',			'https://stepstone.de',		'username', 'password',	null)
on conflict (Title) do update set
	Domain = excluded.Domain,
	Link = excluded.Link,
	Settings = excluded.Settings;
