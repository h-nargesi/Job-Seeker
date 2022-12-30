create table JobOption (
	JobOptionID		integer	not null	primary key,
	Efective		bit		not null	default 1,
	Category		text	not null,
	Score			integer	not null,
	Title			text	not null,
	Pattern			text	not null,
	Settings	text		null,

	unique			(Title)
);

create unique index UQ_JobOption_Name on JobOption (Title);

insert into JobOption (Score, Category, Title, Pattern, Settings)
values
	--	Programming languages
		(50,	'field',	'C# dot net',	'\b(c#|(dot ?|\.)net)\b', null)
	,	(10,	'field',	'Java',			'\b(java|jvm)\b', null)
	,	(50,	'field',	'Javascript',	'\b(javascript|typescript)\b', null)
	,	(50,	'field',	'Angular',		'\bangular\b', null)

	--	Company Benefits
	,	(99,	'benefit',		'Relocation',	'\brelocation\b|\bvisa\b(.+\bsupport)?', null)
	,	(01,	'salary',		'Salary',		'\bsalary\b.*?(\d[\d,]*000).*?\b(month|year)\b',
				'{ "money": 1, "period": 2 }')
	;

/*
	return if re.{\b(c#|(dot ?|\.)net)\b'} ok else not ok

	var @m = re.{\b(c#|(dot ?|\.)net)\b'}
	# mul, sum, div, sub
	var @s = mul @m.1 ok

	return @s
*/