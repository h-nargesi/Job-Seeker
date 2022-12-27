create table Agency (
	AgencyID		integer not null	primary key,
	Title			text	not null	unique,
	Active			bit		not null	default 1,
	Domain			text	not null,
	Link			text	not null,
	UserName		text		null,
	Password		text		null
);

create unique index UQ_Agency_Title on Agency (Title);

insert into Agency (Title, Domain, Link, UserName, Password)
values
	('Indeed', '(.+\.)?indeed\.com$', 'https://indedd.com', 'hamed.nargesi.jar@gmail.com', 's@lm0nElla-009'),
	('IamExpat', '(.+\.)?iamexpat\.(nl|com)$', 'http://iamexpat.nl', 'hamed.nargesi.jar@gmail.com', 's@lm0nElla'),
	('LinkedIn', '(.+\.)?linkedin\.com$', 'http://linkedin.com', 'hamed.nargesi.jar@gmail.com', 'CrguFW7SmtbHDDi'),
	('Karboom', '(.+\.)?karboom\.io$', 'http://karboom.io', 'hamed.nargesi.jar@gmail.com', 'salmonella');