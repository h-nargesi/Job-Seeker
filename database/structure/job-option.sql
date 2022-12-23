create table JobOption (
	JobOptionID		integer	not null	primary key,
	Efective		bit		not null,
	Score			integer	not null,
	Title			text	not null,
	Regexp			text	not null,
	Options			text	not null,

	unique			(Title)
);

create unique index UQ_JobOption_Name on JobOption (Title);

insert into JobOption (Efective, Score, Title, Regexp, Options)
values
	--	Programming languages
		(1, 50,	'C# dot net',	'\b(c#|(dot ?|\.)net)\b', 'field')
	,	(1, 10,	'Java',			'\b(java|jvm)\b', 'field')
	,	(1, 50,	'Javascript',	'\b(javascript|typescript)\b', 'field')
	,	(1, 50,	'Angular',		'\bangular\b', 'field')

	--	Company Benefits
	,	(1, 99,	'Relocation',	'\brelocation\b|\bvisa\b(.+\bsupport)?', 'benefit')
	,	(1, 1,	'Salary',		'(\bsalary\b.*)?([\d,]+).*\b(hour|day|week|month|year)\b', 'salary')
	;