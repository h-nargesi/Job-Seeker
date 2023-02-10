drop table if exists JobOption;

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
	 	(90,	'field',	'C#.NET',			'\basp\.net\b|\bc# ?\.net\b|\bc#|(\bdot ?|\.)net\b', null)
	,	(80,	'field',	'Java',				'\b(java|jvm)\b', null)
	,	(30,	'field',	'GO-Lang',			'\b(go[- ]?lang|(?-i)G[Oo])\b', null)
	,	(60,	'field',	'Frontend',			'\b(angular|javascript|typescript|jquery|client[- ]side script(ing)?)\b', null)
	,	(90,	'field',	'Expert-SQL',		'\b(oracle|sql server|pl[- /]?sql|t[/-]?sql|ms[- ]?sql|sql|database)\b', null)
	,	(10,	'field',	'Low-Level',		'\b(html|css|json)\b', null)
	,	(10,	'field',	'Machine-Learning',	'\b((machine|deep)[- ]learning|natural[- ]network|(?-i)ML|(?-i)AI)\b', null)
	,	(30,	'field',	'Python',			'\bpython\b', null)

	--	Technologies
	,	(10,	'tech',		'Web-API',			'\b((web[- ]?)?api|web services)\b', null)
	,	(10,	'tech',		'TensorFlow',		'\btensor[- ]?flow\b', null)
	,	(05,	'tech',		'Git',				'\bgit\b', null)

	--	Company Production
	,	(30,	'production',	'ERP',			'\berp\b', null)

	--	Company Benefits
	,	(170,	'benefit',	'Relocation',		'\brelocation(\s+(support|package))?\b|\bvisa(\s+(support|sponsorship))\b', null)
	,	(02,	'salary',	'Salary',			'\bsalary\b.*?(\d[\d,]*(000|k))(.+?\b(month|year)\b)?',
				'{ "money": 1, "period": 4 }')

	--	Keywords
	,	(02,	'keywords',	'Full-stack',		'\bfull[- ]?stack\b', null)
	,	(01,	'keywords',	'Developer',		'\bdeveloper\b', null)
	,	(02,	'keywords',	'Mid-Level',		'\b(mid|medium)[- ]level\b', null)
	,	(02,	'keywords',	'Front-End',		'\bfront[- ]?end\b', null)
	,	(02,	'keywords',	'Back-End',			'\bback[- ]?end\b', null)

	--	Rejection
	--,	(01,	'reject',	'react',			'\b(react)\b', null)
	;

/*
	return if re.{\b(c#|(dot ?|\.)net)\b'} ok else not ok

	var @m = re.{\b(c#|(dot ?|\.)net)\b'}
	# mul, sum, div, sub
	var @s = mul @m.1 ok

	return @s
*/