create table JobOption (
	JobOptionID		integer	not null	primary key,
	Efective		bit		not null	default 1,
	Category		text	not null,
	Score			integer	not null,
	Title			text	not null,
	Pattern			text	not null,
	Settings		text		null,

	unique			(Title)
);

create unique index UQ_JobOption_Name on JobOption (Title);

insert into JobOption (Score, Category, Title, Pattern, Settings)
values
	--	Programming languages
		(98,	'field',	'C#.NET',		'\basp\.net\b|\bc# ?\.net\b|\bc#|(\bdot ?|\.)net\b', null)
	,	(80,	'field',	'Java',			'\b(java|jvm)\b', null)
	,	(45,	'field',	'Javascript',	'\b(javascript|typescript|jquery|client[- ]side script(ing)?)\b', null)
	,	(45,	'field',	'Angular',		'\bangular\b', null)
	,	(90,	'field',	'Expert-SQL',	'\b(oracle|sql server|pl[/- ]?sql|t[/-]?sql|ms[- ]?sql|my[- ]sql)\b', null)
	,	(30,	'field',	'Simple-SQL',	'\b(sql|database)\b',
				'{ "linked": "Expert-SQL" }')
	,	(10,	'field',	'Low-Level',	'\b(html|css|json)\b', null)
	
	--	Technologies
	,	(10,	'tech',		'Web-API',		'\b((web[- ]?)?api|web services)\b', null)
	,	(10,	'tech',		'Git',			'\bgit\b', null)

	--	Company Benefits
	,	(99,	'benefit',	'Relocation',	'\brelocation\b|\bvisa\b(.+?\bsupport)?', null)
	,	(02,	'salary',	'Salary',		'\bsalary\b.*?(\d[\d,]*000).*?\b(month|year)\b',
				'{ "money": 1, "period": 2 }')

	--	Keywords
	,	(03,	'keywords',	'Full-stack',	'\b(full[ -]?stack|fron( |-| and )back[\s-]end)\b', null)
	,	(01,	'keywords',	'Developer',	'\bdeveloper\b', null)
	,	(05,	'keywords',	'Mid-Level',	'\b((mid|medium)[- ]level)\b', null)
	,	(02,	'keywords',	'Front-End',	'\b(front[- ]end)\b', null)
	,	(05,	'keywords',	'Back-End',		'\b(back[- ]end)\b', null)
	;

/*
	return if re.{\b(c#|(dot ?|\.)net)\b'} ok else not ok

	var @m = re.{\b(c#|(dot ?|\.)net)\b'}
	# mul, sum, div, sub
	var @s = mul @m.1 ok

	return @s
*/