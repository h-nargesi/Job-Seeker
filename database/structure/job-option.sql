create table JobOption (
	JobOptionID		integer	not null	primary key,
	Efective		bit		not null	default 1,
	Score			integer	not null,
	Title			text	not null,
	Pattern			text	not null,
	Options			text	not null,

	unique			(Title)
);

create unique index UQ_JobOption_Name on JobOption (Title);

insert into JobOption (Score, Title, Pattern, Options)
values
	--	Programming languages
		(50,	'C# dot net',	'\b(c#|(dot ?|\.)net)\b', 'field')
	,	(10,	'Java',			'\b(java|jvm)\b', 'field')
	,	(50,	'Javascript',	'\b(javascript|typescript)\b', 'field')
	,	(50,	'Angular',		'\bangular\b', 'field')

	--	Company Benefits
	,	(99,	'Relocation',	'\brelocation\b|\bvisa\b(.+\bsupport)?', 'benefit')
	,	(01,	'Salary',		'\bsalary\b.*([\d,]+).*\b(hour|day|week|month|year)\b', 'salary-2')
	;

/*
	return if re.{\b(c#|(dot ?|\.)net)\b'} ok else not ok

	var @m = re.{\b(c#|(dot ?|\.)net)\b'}
	# mul, sum, div, sub
	var @s = mul @m.1 ok

	return @s
*/